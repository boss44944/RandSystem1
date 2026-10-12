using System.Text.Json;
using RandSystem1.Core.Random;

namespace RandSystem1.UnitTests;

public sealed class RandomBaselineTests
{
    private static readonly string[] Choices = ["UP", "DOWN", "FLAT"];

    private static BaselineRunResult Generate(
        string type, int seed = 42, int count = 1, int samples = 1,
        double alpha = 1.0, IReadOnlyList<string>? labels = null,
        IReadOnlyList<string>? options = null)
        => BaselineEngine.Run(new(type, options ?? Choices, seed, count, samples, alpha, labels));

    private static void AssertDistribution(
        IReadOnlyDictionary<string, double> probabilities, IReadOnlyList<string> options)
    {
        Assert.Equal(options.Count, probabilities.Count);
        foreach (var option in options)
        {
            double p = probabilities[option];
            Assert.True(double.IsFinite(p));
            Assert.InRange(p, 0.0, 1.0);
        }
        Assert.InRange(Math.Abs(probabilities.Values.Sum() - 1), 0, 1e-9);
    }

    [Fact]
    public void UniformReportsProbabilitySeparatelyFromSampledOption()
    {
        var run = Generate("uniform", samples: 100);
        Assert.True(run.IsSimulation);
        Assert.Equal(BaselineEngine.ProbabilityMeaning, run.ProbabilityMeaning);
        Assert.All(run.Members[0].Draws, draw =>
        {
            Assert.All(draw.Probabilities.Values, p => Assert.Equal(1.0 / 3, p, 12));
            Assert.Contains(draw.SampledOption, Choices);
        });
        Assert.True(run.Members[0].Draws.Select(x => x.SampledOption).Distinct().Count() > 1);
    }

    [Fact]
    public void UniformFrequencyConvergesWithoutPretendingEmpiricalModelAccuracy()
    {
        var draws = Generate("uniform", samples: 2000).Members[0].Draws;
        foreach (var choice in Choices)
        {
            double fraction = (double)draws.Count(x => x.SampledOption == choice) / draws.Count;
            Assert.InRange(fraction, 0.28, 0.39);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(256)]
    public void UniformSupportsNChoices(int n)
    {
        string[] options = Enumerable.Range(0, n).Select(x => $"choice{x}").ToArray();
        var run = Generate("uniform", options: options);
        AssertDistribution(run.Members[0].Draws[0].Probabilities, options);
        Assert.Equal(options[0], Generate("majority", options: [options[0]],
            labels: [options[0]]).Members[0].Draws[0].SampledOption);
    }

    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(1e-6)]
    [InlineData(0.05)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1e6)]
    public void DirichletGeneratesValidProbabilities(double alpha)
    {
        var run = Generate("dirichlet", count: 5, samples: 80, alpha: alpha);
        Assert.Equal(alpha, run.Alpha);
        Assert.All(run.Members.SelectMany(x => x.Draws), x =>
            AssertDistribution(x.Probabilities, Choices));
    }

    [Fact]
    public void DirichletProducesDifferentProbabilityVectorsPerDraw()
    {
        var draws = Generate("dirichlet", samples: 5).Members[0].Draws;
        Assert.NotEqual(draws[0].Probabilities["UP"], draws[1].Probabilities["UP"]);
    }

    [Fact]
    public void DirichletSingleChoiceIsAlwaysOne()
    {
        var draws = Generate("dirichlet", options: ["ONE"], samples: 20).Members[0].Draws;
        Assert.All(draws, x =>
        {
            Assert.Equal("ONE", x.SampledOption);
            Assert.Equal(1.0, x.Probabilities["ONE"]);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DirichletRejectsInvalidAlpha(double alpha)
    {
        Assert.Throws<ArgumentException>(() => Generate("dirichlet", alpha: alpha));
    }

    [Fact]
    public void MajorityUsesOnlySuppliedTrainingLabelsAndBreaksTiesByOptionOrder()
    {
        var labels = new[] { "DOWN", "DOWN", "DOWN", "UP" };
        var run = Generate("majority", labels: labels, samples: 30);
        Assert.All(run.Members[0].Draws, x =>
        {
            Assert.Equal("DOWN", x.SampledOption);
            Assert.Equal(1, x.Probabilities["DOWN"]);
            Assert.Equal(0, x.Probabilities["UP"]);
        });
        Assert.Equal("UP", Generate("majority", labels: ["UP", "DOWN"]).Members[0].Draws[0].SampledOption);
        Assert.Equal(4, run.TrainingLabelCount);
    }

    [Fact]
    public void PriorUsesOnlyTrainingFrequenciesAndSamplesFromThem()
    {
        var run = Generate("prior", labels: ["UP", "UP", "UP", "DOWN"], samples: 20);
        Assert.All(run.Members[0].Draws, x =>
        {
            Assert.Equal(0.75, x.Probabilities["UP"]);
            Assert.Equal(0.25, x.Probabilities["DOWN"]);
            Assert.Equal(0, x.Probabilities["FLAT"]);
            Assert.NotEqual("FLAT", x.SampledOption);
        });
    }

    [Fact]
    public void FittedBaselinesRejectMissingEmptyAndUnknownTrainingLabels()
    {
        Assert.Throws<ArgumentException>(() => Generate("prior"));
        Assert.Throws<ArgumentException>(() => Generate("majority", labels: []));
        Assert.Throws<ArgumentException>(() => Generate("prior", labels: ["FUTURE"]));
        Assert.Throws<ArgumentException>(() => Generate("uniform", labels: ["UP"]));
        Assert.Throws<ArgumentException>(() => Generate("dirichlet", labels: ["UP"]));
        Assert.Throws<ArgumentException>(() => Generate("uniform", labels: []));
    }

    [Fact]
    public void ReplayUsesStableMembersSeedsAndProbabilitySampleSequences()
    {
        foreach (string kind in new[] { "uniform", "dirichlet", "prior", "majority" })
        {
            var labels = kind is "prior" or "majority" ? new[] { "UP", "DOWN", "UP" } : null;
            var a = Generate(kind, seed: 2026, count: 10, samples: 15, labels: labels);
            var b = Generate(kind, seed: 2026, count: 10, samples: 15, labels: labels);
            Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
            Assert.Equal(10, a.Members.Select(m => m.SeedHex).Distinct().Count());
            Assert.Equal(10 * 15, a.Members.Sum(m => m.Draws.Count));
            Assert.All(a.Members, member =>
            {
                Assert.StartsWith("random-", member.ModelId);
                Assert.Equal(15, member.Draws.Count);
                Assert.Equal(Enumerable.Range(0, 15), member.Draws.Select(draw => draw.SampleIndex));
            });
        }
    }

    [Fact]
    public void DifferentSeedsCreateDifferentRandomEnsembles()
    {
        var a = Generate("dirichlet", seed: 10, count: 10, samples: 3);
        var b = Generate("dirichlet", seed: 11, count: 10, samples: 3);
        Assert.NotEqual(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.NotEqual(a.Members[0].Draws[0].Probabilities["UP"],
            a.Members[1].Draws[0].Probabilities["UP"]);
    }

    [Fact]
    public void RejectsInvalidCandidatesOrOversizedSimulations()
    {
        Assert.Throws<ArgumentException>(() => Generate("uniform", options: []));
        Assert.Throws<ArgumentException>(() => Generate("uniform", options: ["UP", "UP"]));
        Assert.Throws<ArgumentException>(() => Generate("uniform", options: ["", "UP"]));
        Assert.Throws<ArgumentException>(() => Generate("invalid"));
        Assert.Throws<ArgumentException>(() => Generate("uniform", count: 0));
        Assert.Throws<ArgumentException>(() => Generate("uniform", count: 201));
        Assert.Throws<ArgumentException>(() => Generate("uniform", count: 200, samples: 2000));
        Assert.Throws<ArgumentException>(() => Generate("uniform", samples: 2001));
    }

    [Fact]
    public void HonorsCancellationBeforeDrawing()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => BaselineEngine.Run(
            new("dirichlet", Choices, SamplesPerModel: 100), source.Token));
    }
}

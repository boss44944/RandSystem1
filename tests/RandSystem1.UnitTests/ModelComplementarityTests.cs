using RandSystem1.Core.Statistics;

namespace RandSystem1.UnitTests;

public sealed class ModelComplementarityTests
{
    private static readonly string[] Options = ["UP", "DOWN"];

    private static NamedModelChoices Model(string id, params string[] choices) =>
        new(id, choices.Select((choice, index) =>
            new ModelChoice($"case-{index}", choice)).ToArray());

    private static ObservedTruth[] Truth(params string[] labels) =>
        labels.Select((label, index) => new ObservedTruth($"case-{index}", label)).ToArray();

    private static ComplementarityReport Evaluate(
        IReadOnlyList<NamedModelChoices> models,
        IReadOnlyList<ObservedTruth>? truth = null) =>
        ModelComplementarity.Evaluate(new(Options, models, truth));

    [Fact]
    public void AnalyticalTenCaseFixtureMatchesAllFourContingencyCells()
    {
        // 5 both right, 2 A-only, 1 B-only, 2 both wrong.
        var truth = Truth("UP", "DOWN", "UP", "DOWN", "UP", "UP", "DOWN", "UP", "DOWN", "UP");
        var a = Model("A", "UP", "DOWN", "UP", "DOWN", "UP", "UP", "DOWN", "DOWN", "UP", "DOWN");
        var b = Model("B", "UP", "DOWN", "UP", "DOWN", "UP", "DOWN", "UP", "UP", "UP", "DOWN");
        var report = Evaluate([a, b], truth);
        var p = Assert.Single(report.Pairs);
        Assert.True(report.HasGroundTruth);
        Assert.Equal(10, p.Total);
        Assert.Equal(5, p.BothCorrect);
        Assert.Equal(2, p.BothWrong);
        Assert.Equal(2, p.OnlyACorrect);
        Assert.Equal(1, p.OnlyBCorrect);
        Assert.Equal(7, p.AgreementCount);
        Assert.Equal(3, p.DisagreementCount);
        Assert.Equal(0.3, p.DisagreementRate, 12);
        Assert.Equal(0.7, p.ModelAAccuracy!.Value, 12);
        Assert.Equal(0.6, p.ModelBAccuracy!.Value, 12);
        Assert.Equal(0.8, p.JointCorrectCoverage!.Value, 12);
        Assert.Equal(0.8, p.OracleUpperBound!.Value, 12);
        Assert.Equal(0.1, p.OracleGainOverBestSingle!.Value, 12);
        Assert.Equal(8 / Math.Sqrt(504), p.ErrorPhiCorrelation!.Value, 12);
        Assert.Equal(0.8, report.AtLeastOneCorrectCoverage!.Value, 12);
        Assert.Equal(0.1, report.OracleGainOverBestSingle!.Value, 12);
        Assert.Contains("NOT achievable", report.Interpretation);
    }

    [Fact]
    public void IdenticalModelsHaveNoFalseComplementaryGain()
    {
        var a = Model("A", "UP", "UP", "DOWN", "DOWN");
        var b = Model("B", "UP", "UP", "DOWN", "DOWN");
        var report = Evaluate([a, b], Truth("UP", "DOWN", "UP", "DOWN"));
        var p = Assert.Single(report.Pairs);
        Assert.Equal(0, p.DisagreementCount);
        Assert.Equal(0, p.OnlyACorrect);
        Assert.Equal(0, p.OnlyBCorrect);
        Assert.Equal(0, p.OracleGainOverBestSingle);
        Assert.Equal(1, p.ErrorPhiCorrelation);
        Assert.Equal(0.5, p.OracleUpperBound);
    }

    [Fact]
    public void MutuallyExclusiveBinaryErrorsHavePerfectNegativeErrorPhi()
    {
        var a = Model("A", "UP", "DOWN");
        var b = Model("B", "DOWN", "UP");
        var r = Evaluate([a, b], Truth("UP", "UP"));
        var p = Assert.Single(r.Pairs);
        Assert.Equal(0, p.BothCorrect);
        Assert.Equal(0, p.BothWrong);
        Assert.Equal(1, p.OnlyACorrect);
        Assert.Equal(1, p.OnlyBCorrect);
        Assert.Equal(-1, p.ErrorPhiCorrelation);
        Assert.Equal(1, p.OracleUpperBound);
        Assert.Equal(0.5, p.OracleGainOverBestSingle);
    }

    [Fact]
    public void PerfectAndAlwaysWrongYieldUndefinedErrorCorrelation()
    {
        var r = Evaluate([Model("perfect", "UP", "DOWN"), Model("wrong", "DOWN", "UP")],
            Truth("UP", "DOWN"));
        var p = Assert.Single(r.Pairs);
        Assert.Null(p.ErrorPhiCorrelation);
        Assert.Equal(1, p.OracleUpperBound);
        Assert.Equal(0, p.OracleGainOverBestSingle);
    }

    [Fact]
    public void LabeledMultiModelOverallOracleCanExceedEveryPairOracle()
    {
        var models = new[]
        {
            Model("A", "UP", "DOWN", "DOWN"),
            Model("B", "DOWN", "UP", "DOWN"),
            Model("C", "DOWN", "DOWN", "UP")
        };
        var report = Evaluate(models, Truth("UP", "UP", "UP"));
        Assert.Equal(3, report.ModelCount);
        Assert.Equal(3, report.Pairs.Count);
        Assert.All(report.Models, m => Assert.Equal(1.0 / 3, m.Accuracy!.Value, 12));
        Assert.All(report.Pairs, p => Assert.Equal(2.0 / 3, p.OracleUpperBound!.Value, 12));
        Assert.Equal(1, report.OracleUpperBound);
        Assert.Equal(2.0 / 3, report.OracleGainOverBestSingle!.Value, 12);
        Assert.Equal(3, report.Pairs.Select(p => p.ModelA + p.ModelB).Distinct().Count());
    }

    [Fact]
    public void UnlabeledModeNeverEmitsAccuracyErrorPhiOrOracle()
    {
        var report = Evaluate([
            Model("A", "UP", "DOWN", "UP"),
            Model("B", "DOWN", "DOWN", "UP")
        ]);
        Assert.False(report.HasGroundTruth);
        Assert.Null(report.AtLeastOneCorrectCoverage);
        Assert.Null(report.OracleUpperBound);
        Assert.Null(report.OracleGainOverBestSingle);
        Assert.All(report.Models, m => Assert.Null(m.Accuracy));
        var p = Assert.Single(report.Pairs);
        Assert.Equal(1, p.DisagreementCount);
        Assert.Equal(1.0 / 3, p.DisagreementRate, 12);
        Assert.Equal(2, p.AgreementCount);
        Assert.Null(p.BothCorrect);
        Assert.Null(p.BothWrong);
        Assert.Null(p.OnlyACorrect);
        Assert.Null(p.OnlyBCorrect);
        Assert.Null(p.JointCorrectCoverage);
        Assert.Null(p.ErrorPhiCorrelation);
        Assert.Null(p.OracleUpperBound);
        Assert.Null(p.ModelAAccuracy);
        Assert.Contains("NEVER proves", report.Interpretation);
        Assert.Equal(2, report.Models[0].PredictionCounts["UP"]);
        Assert.Equal(1, report.Models[0].PredictionCounts["DOWN"]);
    }

    [Fact]
    public void PredictionDisagreementIsNotEquivalentToErrorComplementarity()
    {
        var r = ModelComplementarity.Evaluate(new(["UP", "DOWN", "FLAT"], [
            new NamedModelChoices("A", [new("1", "UP")]),
            new NamedModelChoices("B", [new("1", "DOWN")])
        ], [new("1", "FLAT")]));
        var p = Assert.Single(r.Pairs);
        Assert.Equal(1, p.DisagreementCount);
        Assert.Equal(0, p.OnlyACorrect);
        Assert.Equal(0, p.OnlyBCorrect);
        Assert.Equal(1, p.BothWrong);
        Assert.Equal(0, p.OracleUpperBound);
    }

    [Fact]
    public void RejectsSwappedOrUnmatchedModelObservationIds()
    {
        var a = Model("A", "UP", "DOWN");
        var swapped = new NamedModelChoices("B", [new("case-1", "UP"), new("case-0", "DOWN")]);
        Assert.Throws<ArgumentException>(() => Evaluate([a, swapped]));
        Assert.Throws<ArgumentException>(() => Evaluate([a, Model("B", "UP")]));
        Assert.Throws<ArgumentException>(() => Evaluate([a, new NamedModelChoices("B",
        [new("other", "UP"), new("case-1", "DOWN")])]));
    }

    [Fact]
    public void RejectsGroundTruthIdOrderOrInvalidLabels()
    {
        var a = Model("A", "UP", "DOWN");
        var b = Model("B", "DOWN", "UP");
        Assert.Throws<ArgumentException>(() => Evaluate([a, b], [new("case-1", "UP"), new("case-0", "DOWN")]));
        Assert.Throws<ArgumentException>(() => Evaluate([a, b], [new("case-0", "UP")]));
        Assert.Throws<ArgumentException>(() => Evaluate([a, b], [new("case-0", "FLAT"), new("case-1", "UP")]));
    }

    [Fact]
    public void RejectsDuplicateOrInvalidModelIds()
    {
        Assert.Throws<ArgumentException>(() => Evaluate([Model("A", "UP"), Model("A", "DOWN")]));
        Assert.Throws<ArgumentException>(() => Evaluate([Model("", "UP"), Model("B", "DOWN")]));
        Assert.Throws<ArgumentException>(() => Evaluate([Model("single", "UP")]));
        Assert.Throws<ArgumentException>(() => Evaluate(Enumerable.Range(0, 21).Select(i => Model($"m{i}", "UP")).ToArray()));
    }

    [Fact]
    public void RejectsDuplicateEmptyUnknownOptionsAndInvalidPredictions()
    {
        Assert.Throws<ArgumentException>(() => ModelComplementarity.Evaluate(new(["UP", "UP"], [
            Model("A", "UP"), Model("B", "UP")
        ])));
        Assert.Throws<ArgumentException>(() => Evaluate([Model("A", "UP", "UP"),
            new NamedModelChoices("B", [new("case-0", "DOWN"), new("case-0", "UP")])]));
        Assert.Throws<ArgumentException>(() => Evaluate([Model("A", "FLAT"), Model("B", "UP")]));
        Assert.Throws<ArgumentException>(() => Evaluate([Model("A"), Model("B")]));
        Assert.Throws<ArgumentException>(() => Evaluate([Model("A", "UP"), Model("B", "FLAT")]));
    }

    [Fact]
    public void SingleObservationCanYieldOracleOneWithNoFalseCertainty()
    {
        var r = Evaluate([Model("A", "UP"), Model("B", "DOWN")], Truth("UP"));
        Assert.Equal(1, r.OracleUpperBound);
        Assert.Equal(0, r.OracleGainOverBestSingle);
        Assert.Null(r.Pairs[0].ErrorPhiCorrelation);
    }

    [Fact]
    public void HonorsCancellationAndDoesNotReportResults()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ModelComplementarity.Evaluate(
            new(Options, [Model("A", "UP"), Model("B", "DOWN")]), cts.Token));
    }

    [Fact]
    public void LargeValidUnlabeledDataHasBoundedPairwiseResults()
    {
        var models = Enumerable.Range(0, 4).Select(i =>
            Model($"m{i}", Enumerable.Repeat(i % 2 == 0 ? "UP" : "DOWN", 1000).ToArray()))
            .ToArray();
        var r = Evaluate(models);
        Assert.Equal(1000, r.SampleCount);
        Assert.Equal(6, r.Pairs.Count);
        Assert.Equal(1000, r.Pairs[0].DisagreementCount);
        Assert.Null(r.OracleUpperBound);
    }
}

using System.Text.Json;
using RandSystem1.Core.Statistics;

namespace RandSystem1.UnitTests;

public sealed class StatisticalEvaluationTests
{
    private static readonly string[] Choices = ["UP", "DOWN"];

    private static ScoredObservation Row(
        string id, string actual, string predicted, double up) =>
        new(id, actual, predicted, new Dictionary<string, double>
        {
            ["UP"] = up,
            ["DOWN"] = 1 - up
        });

    [Fact]
    public void PerfectBinaryPredictionsHaveZeroLossAndPerfectMacroScores()
    {
        var result = ClassificationMetrics.Evaluate(new(Choices,
        [
            Row("1", "UP", "UP", 1), Row("2", "DOWN", "DOWN", 0)
        ], CalibrationBins: 2));
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result.Accuracy);
        Assert.Equal(1, result.BalancedAccuracy);
        Assert.Equal(1, result.MacroPrecision);
        Assert.Equal(1, result.MacroRecall);
        Assert.Equal(1, result.MacroF1);
        Assert.Equal(0, result.Brier);
        Assert.Equal(0, result.LogLoss);
        Assert.Equal(0, result.Ece);
        Assert.Equal(1, result.ConfusionMatrix["UP"]["UP"]);
        Assert.Equal(1, result.ConfusionMatrix["DOWN"]["DOWN"]);
        Assert.Equal(2, result.Reliability.Sum(bin => bin.Count));
    }

    [Fact]
    public void UniformBinaryForecastHasKnownBrierAndLogLoss()
    {
        var result = ClassificationMetrics.Evaluate(new(Choices,
        [
            Row("1", "UP", "UP", 0.5),
            Row("2", "DOWN", "UP", 0.5)
        ]));
        Assert.Equal(0.5, result.Brier, precision: 12);
        Assert.Equal(Math.Log(2), result.LogLoss, precision: 12);
        Assert.Equal(0.5, result.Accuracy, precision: 12);
        Assert.Equal(0.5, result.BalancedAccuracy, precision: 12);
        Assert.Equal(0, result.PerClass.Single(x => x.Option == "DOWN").Precision);
    }

    [Fact]
    public void ImbalancedSamplesReportBalancedAccuracyNotJustMajorityAccuracy()
    {
        var obs = Enumerable.Range(0, 9)
            .Select(i => Row(i.ToString(), "UP", "UP", 1))
            .Append(Row("9", "DOWN", "UP", 1)).ToArray();
        var result = ClassificationMetrics.Evaluate(new(Choices, obs));
        Assert.Equal(0.9, result.Accuracy, 12);
        Assert.Equal(0.5, result.BalancedAccuracy, 12);
        Assert.Equal(0, result.PerClass.Single(x => x.Option == "DOWN").Recall);
        Assert.Equal(9, result.ConfusionMatrix["UP"]["UP"]);
        Assert.Equal(1, result.ConfusionMatrix["DOWN"]["UP"]);
    }

    [Fact]
    public void UnobservedClassDoesNotCreateFakeRecall()
    {
        var result = ClassificationMetrics.Evaluate(new(Choices, [Row("one", "UP", "UP", 1)]));
        Assert.Null(result.PerClass.Single(x => x.Option == "DOWN").Recall);
        Assert.Equal(1, result.BalancedAccuracy);
    }

    [Fact]
    public void ZeroProbabilityForTrueClassHasFiniteLogLoss()
    {
        var result = ClassificationMetrics.Evaluate(new(Choices, [Row("1", "DOWN", "UP", 1)]));
        Assert.Equal(-Math.Log(1e-15), result.LogLoss, precision: 11);
        Assert.True(double.IsFinite(result.LogLoss));
        Assert.Equal(2, result.Brier);
        Assert.Equal(1, result.Ece);
    }

    [Fact]
    public void EceReliabilityIsNonzeroForOverconfidentWrongPredictions()
    {
        var r = ClassificationMetrics.Evaluate(new(Choices,
        [
            Row("one", "DOWN", "UP", 0.9),
            Row("two", "UP", "UP", 0.9)
        ], CalibrationBins: 10));
        Assert.Equal(0.4, r.Ece, 12);
        Assert.Equal(2, r.Reliability[9].Count);
        Assert.Equal(0.9, r.Reliability[9].MeanConfidence, 12);
        Assert.Equal(0.5, r.Reliability[9].EmpiricalAccuracy, 12);
    }

    [Fact]
    public void ModelSkillUsesIdenticallyAlignedHeldOutBaselineLosses()
    {
        var model = new[] { Row("1", "UP", "UP", 1), Row("2", "DOWN", "DOWN", 0) };
        var baseModel = new[] { Row("1", "UP", "UP", 0.5), Row("2", "DOWN", "UP", 0.5) };
        var result = ClassificationMetrics.Evaluate(new(Choices, model, BaselineObservations: baseModel));
        Assert.Equal(1, result.Skill!.BrierSkill);
        Assert.Equal(1, result.Skill!.LogLossSkill);
        var same = ClassificationMetrics.Evaluate(new(Choices, baseModel, BaselineObservations: baseModel));
        Assert.Equal(0, same.Skill!.BrierSkill);
        Assert.Equal(0, same.Skill!.LogLossSkill);
    }

    [Fact]
    public void ZeroBaselineLossYieldsUndefinedSkillInsteadOfDivisionByZero()
    {
        var perfect = new[] { Row("1", "UP", "UP", 1) };
        var report = ClassificationMetrics.Evaluate(new(Choices, perfect, BaselineObservations: perfect));
        Assert.Null(report.Skill!.BrierSkill);
        Assert.Null(report.Skill!.LogLossSkill);
    }

    [Fact]
    public void RejectsEmptyMissingExtraProbabilitiesNaNAndMismatchedBaselines()
    {
        Assert.Throws<ArgumentException>(() =>
            ClassificationMetrics.Evaluate(new(Choices, [])));
        Assert.Throws<ArgumentException>(() =>
            ClassificationMetrics.Evaluate(new(Choices, [Row("1", "UP", "UP", double.NaN)])));
        Assert.Throws<ArgumentException>(() =>
            ClassificationMetrics.Evaluate(new(Choices,
            [
                new ScoredObservation("1", "UP", "UP", new Dictionary<string, double> { ["UP"] = 1 })
            ])));
        Assert.Throws<ArgumentException>(() =>
            ClassificationMetrics.Evaluate(new(Choices,
            [Row("1", "UP", "UP", 1)], BaselineObservations: [Row("2", "UP", "UP", 1)])));
        Assert.Throws<ArgumentException>(() =>
            ClassificationMetrics.Evaluate(new(Choices,
            [Row("1", "UP", "UP", 1)], CalibrationBins: 1)));
    }

    [Fact]
    public void PairedIidPerfectModelHasPositiveEffectAndSmallPermutationP()
    {
        var model = Enumerable.Range(0, 12)
            .Select(i => Row($"s{i}", i % 2 == 0 ? "UP" : "DOWN",
                i % 2 == 0 ? "UP" : "DOWN", i % 2 == 0 ? 1 : 0)).ToArray();
        var baseline = Enumerable.Range(0, 12)
            .Select(i => Row($"s{i}", i % 2 == 0 ? "UP" : "DOWN", "UP", 0.5)).ToArray();
        var result = StatisticalInference.Compare(new(Choices, model, baseline, SampleStructure: "iid"));
        Assert.Equal(12, result.Count);
        Assert.True(result.Improvement > 0);
        Assert.True(result.LowerConfidenceBound > 0);
        Assert.Equal(2.0 / (1 << 12), result.TwoSidedPermutationP!.Value, precision: 12);
    }

    [Fact]
    public void PairedNoDifferenceHasZeroEffectAndPValueOne()
    {
        var rows = Enumerable.Range(0, 20).Select(i => Row($"s{i}", "UP", "UP", 0.7)).ToArray();
        var result = StatisticalInference.Compare(new(Choices, rows, rows, SampleStructure: "iid", Replications: 200));
        Assert.Equal(0, result.Improvement);
        Assert.Equal(0, result.LowerConfidenceBound);
        Assert.Equal(0, result.UpperConfidenceBound);
        Assert.Equal(1, result.TwoSidedPermutationP);
    }

    [Fact]
    public void LongPairedPermutationIsSeededAndBonferroniBounded()
    {
        var model = Enumerable.Range(0, 20).Select(i => Row($"s{i}", "UP", "UP", 0.9)).ToArray();
        var baseline = Enumerable.Range(0, 20).Select(i => Row($"s{i}", "UP", "UP", 0.5)).ToArray();
        var request = new PairedComparisonRequest(Choices, model, baseline,
            SampleStructure: "iid", Replications: 1000, Seed: 42, Comparisons: 10);
        var a = StatisticalInference.Compare(request);
        var b = StatisticalInference.Compare(request);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal(Math.Min(1, 10 * a.TwoSidedPermutationP!.Value), a.BonferroniAdjustedP!.Value);
    }

    [Fact]
    public void TimeSeriesUsesBlockBootstrapWithoutMisleadingIidPValue()
    {
        var model = Enumerable.Range(0, 30).Select(i => Row($"t{i}", "UP", "UP", 0.9)).ToArray();
        var baseline = Enumerable.Range(0, 30).Select(i => Row($"t{i}", "UP", "UP", 0.5)).ToArray();
        var report = StatisticalInference.Compare(new(Choices, model, baseline, SampleStructure: "timeseries",
            BlockLength: 5, ChronologicalOrderConfirmed: true, Replications: 500));
        Assert.Null(report.TwoSidedPermutationP);
        Assert.Null(report.BonferroniAdjustedP);
        Assert.Equal("circular-moving-block-bootstrap", report.Method);
        Assert.True(report.LowerConfidenceBound > 0);
    }

    [Fact]
    public void InferenceRejectsImplicitIidAndMissingChronology()
    {
        var x = new[] { Row("one", "UP", "UP", 0.8), Row("two", "DOWN", "DOWN", 0.2) };
        Assert.Throws<ArgumentException>(() => StatisticalInference.Compare(new(Choices, x, x)));
        Assert.Throws<ArgumentException>(() => StatisticalInference.Compare(new(Choices, x, x,
            SampleStructure: "timeseries", BlockLength: 2)));
        Assert.Throws<ArgumentException>(() => StatisticalInference.Compare(new(Choices, x, x.Reverse().ToArray(),
            SampleStructure: "iid")));
        Assert.Throws<ArgumentException>(() => StatisticalInference.Compare(new(Choices, x, x,
            SampleStructure: "iid", ConfidenceLevel: double.NaN)));
    }

    [Fact]
    public void ExactBinomialRecoversAnalyticalHalfChanceTail()
    {
        var r = StatisticalInference.ExactBinomialUpperTail(new(10, 10, 0.5, true));
        Assert.Equal(1.0 / 1024, r.OneSidedUpperTailP, precision: 12);
        Assert.Equal(1, StatisticalInference.ExactBinomialUpperTail(new(0, 10, 0.5, true)).OneSidedUpperTailP);
        Assert.Equal(0, StatisticalInference.ExactBinomialUpperTail(new(1, 10, 0, true)).OneSidedUpperTailP);
        Assert.Equal(1, StatisticalInference.ExactBinomialUpperTail(new(10, 10, 1, true)).OneSidedUpperTailP);
    }

    [Fact]
    public void BinomialDemandsExplicitIndependenceAndValidProbability()
    {
        Assert.Throws<ArgumentException>(() =>
            StatisticalInference.ExactBinomialUpperTail(new(2, 10, 0.5, false)));
        Assert.Throws<ArgumentException>(() =>
            StatisticalInference.ExactBinomialUpperTail(new(2, 10, double.NaN, true)));
        Assert.Throws<ArgumentException>(() =>
            StatisticalInference.ExactBinomialUpperTail(new(11, 10, 0.5, true)));
    }

    [Fact]
    public void MonteCarloNullAccuracyIsSeedReproducibleAndNearChance()
    {
        var testLabels = Enumerable.Repeat("UP", 10)
            .Concat(Enumerable.Repeat("DOWN", 10)).ToArray();
        var config = new MonteCarloRequest(Choices, testLabels, Baseline: "uniform",
            Iterations: 500, Seed: 31,
            ObservedPredictions: Enumerable.Repeat("UP", 20).ToArray());
        var a = MonteCarloEvaluation.Run(config);
        var b = MonteCarloEvaluation.Run(config);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.True(a.IsSimulation);
        Assert.Equal(500, a.RandomAccuracies.Count);
        Assert.InRange(a.MeanAccuracy, 0.46, 0.54);
        Assert.Equal(0.5, a.ModelObservedAccuracy);
        Assert.InRange(a.EmpiricalUpperTailP!.Value, 0.2, 0.8);
        Assert.True(a.BestObservedRandomAccuracy >= a.NullMedian);
        Assert.True(a.MeanConfidenceUpper95 >= a.MeanConfidenceLower95);
    }

    [Fact]
    public void MonteCarloPriorAndMajorityUseTrainingOnly()
    {
        var labels = new[] { "UP", "UP", "UP", "DOWN" };
        var result = MonteCarloEvaluation.Run(new(Choices, ["UP", "DOWN", "UP", "DOWN"],
            Baseline: "majority", Iterations: 100,
            TrainingLabels: labels, ObservedPredictions: ["UP", "UP", "UP", "UP"]));
        Assert.Equal(4, result.TrainingCount);
        Assert.Equal(0.5, result.MeanAccuracy);
        Assert.Equal(0.5, result.BestObservedRandomAccuracy);
        Assert.Equal(1, result.EmpiricalUpperTailP);
        Assert.Equal(100, result.Iterations);
    }

    [Fact]
    public void MonteCarloRejectsEmptyUnknownOrFutureLabeledTrainingInput()
    {
        Assert.Throws<ArgumentException>(() => MonteCarloEvaluation.Run(
            new(Choices, [], Iterations: 100)));
        Assert.Throws<ArgumentException>(() => MonteCarloEvaluation.Run(
            new(Choices, ["UP"], Baseline: "prior", Iterations: 100)));
        Assert.Throws<ArgumentException>(() => MonteCarloEvaluation.Run(
            new(Choices, ["UP"], Baseline: "prior", Iterations: 100, TrainingLabels: ["FUTURE"])));
        Assert.Throws<ArgumentException>(() => MonteCarloEvaluation.Run(
            new(Choices, ["UP"], Iterations: 100, ObservedPredictions: ["DOWN", "UP"])));
        Assert.Throws<ArgumentException>(() => MonteCarloEvaluation.Run(
            new(Choices, ["UP"], Iterations: 10)));
        Assert.Throws<ArgumentException>(() => MonteCarloEvaluation.Run(
            new(Choices, ["UP"], Iterations: 100, TrainingLabels: ["UP"])));
    }

    [Fact]
    public void MonteCarloUsesIndependentSeedsForDirichletAndSupportsCancellation()
    {
        var result = MonteCarloEvaluation.Run(new(Choices, ["UP", "DOWN", "UP"],
            Baseline: "dirichlet", Iterations: 80, Alpha: 0.5, Seed: -2));
        Assert.Equal(80, result.RandomAccuracies.Count);
        Assert.Null(result.EmpiricalUpperTailP);
        Assert.True(result.RandomAccuracies.Distinct().Count() > 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => MonteCarloEvaluation.Run(
            new(Choices, ["UP"], Iterations: 100), cts.Token));
    }
}

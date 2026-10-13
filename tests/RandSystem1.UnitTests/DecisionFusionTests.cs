using RandSystem1.Core.Statistics;

namespace RandSystem1.UnitTests;

public sealed class DecisionFusionTests
{
    private static readonly string[] Options = ["UP", "DOWN"];
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static FusionModelPrediction Model(string id, double up, string status = "success", bool simulation = false) =>
        new(id, status, new Dictionary<string, double> { ["UP"] = up, ["DOWN"] = 1 - up }, simulation);
    private static FusionValidationEvidence Evidence(string id, double brier, string? segment = null, string split = "validation", int n = 100, DateTimeOffset? end = null) =>
        new(id, "direction", segment, split, n, brier, 1, 0.05, end ?? Now.AddDays(-10));
    private static FusionStrategyEvidence Proof(string strategy, string? segment = null, string split = "validation",
        double lower = 0.03, DateTimeOffset? end = null) =>
        new(strategy, "direction", segment, split, 100, lower, end ?? Now.AddDays(-1), ["A", "B"],
            strategy == "dynamic" ? new Dictionary<string, double> { ["A"] = 0.8, ["B"] = 0.2 } :
            strategy == "static" ? new Dictionary<string, double> { ["A"] = 0.75, ["B"] = 0.25 } :
            new Dictionary<string, double> { ["A"] = 0.5, ["B"] = 0.5 });
    private static FusionRequest Request(string strategy = "equal",
        IReadOnlyList<FusionModelPrediction>? models = null,
        FusionRiskPolicy? risk = null,
        IReadOnlyDictionary<string, double>? weights = null,
        IReadOnlyList<FusionValidationEvidence>? evidence = null,
        FusionStrategyEvidence? proof = null,
        string? segment = null) =>
        new("req-1", "direction", Options, models ?? [Model("A", 0.9), Model("B", 0.7)],
            strategy, weights, evidence, proof, segment, Now, risk);

    [Fact]
    public void EqualWeightAveragesProbabilitiesButDoesNotClaimUnvalidatedAdvantage()
    {
        var result = DecisionFusion.Predict(Request());
        Assert.Equal("ABSTAIN", result.Decision);
        Assert.Equal(0.8, result.Probabilities!["UP"], 12);
        Assert.Equal(0.2, result.Probabilities["DOWN"], 12);
        Assert.Equal("UP", result.PredictedOption);
        Assert.All(result.Models, m => Assert.Equal(0.5, m.Weight, 12));
        Assert.Contains("No eligible past validation", result.Reason);
    }

    [Fact]
    public void StaticWeightsAreNormalizedAndFrozenFusionProofCanPass()
    {
        var result = DecisionFusion.Predict(Request("static", weights:
            new Dictionary<string, double> { ["A"] = 3, ["B"] = 1 }, proof: Proof("static")));
        Assert.Equal("EXECUTE", result.Decision);
        Assert.Equal(0.85, result.Probabilities!["UP"], 12);
        Assert.Equal(0.75, result.Models[0].Weight, 12);
        Assert.Equal(0.25, result.Models[1].Weight, 12);
    }

    [Fact]
    public void IdenticalPredictionsHaveNoArtificialCertaintyBoost()
    {
        var result = DecisionFusion.Predict(Request(models: [Model("A", 0.62), Model("B", 0.62)], proof: Proof("equal")));
        Assert.Equal(0.62, result.Probabilities!["UP"], 12);
        Assert.Equal("EXECUTE", result.Decision);
    }

    [Fact]
    public void DisagreeingModelsCanAbstainForLowConfidence()
    {
        var result = DecisionFusion.Predict(Request(models: [Model("A", 0.9), Model("B", 0.05)], proof: Proof("equal")));
        Assert.Equal(0.475, result.Probabilities!["UP"], 12);
        Assert.Equal("ABSTAIN", result.Decision);
        Assert.Contains("confidence", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DynamicUsesOnlyMatchingPastValidationSegment()
    {
        var result = DecisionFusion.Predict(Request("dynamic",
            evidence: [Evidence("A", 0.2, "bull"), Evidence("B", 0.8, "bull")],
            proof: Proof("dynamic", "bull"), segment: "bull"));
        Assert.Equal("EXECUTE", result.Decision);
        Assert.Equal(0.8, result.Models[0].Weight, 12);
        Assert.Equal(0.2, result.Models[1].Weight, 12);
        Assert.Equal(0.86, result.Probabilities!["UP"], 12);
    }

    [Fact]
    public void PerformanceWeightsRequireBothModelsToHaveUsableEvidence()
    {
        var missing = DecisionFusion.Predict(Request("performance", evidence: [Evidence("A", 0.2)], proof: Proof("performance")));
        Assert.Equal("ABSTAIN", missing.Decision);
        Assert.Null(missing.Probabilities);
        var noSkill = DecisionFusion.Predict(Request("performance",
            evidence: [Evidence("A", 1), Evidence("B", 1)], proof: Proof("performance")));
        Assert.Equal("ABSTAIN", noSkill.Decision);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("training")]
    public void TestOrTrainingSplitCannotSelectWeights(string split)
    {
        var result = DecisionFusion.Predict(Request("performance",
            evidence: [Evidence("A", 0.2, split: split), Evidence("B", 0.4)], proof: Proof("performance")));
        Assert.Equal("ABSTAIN", result.Decision);
    }

    [Fact]
    public void FutureEvidenceCannotSelectWeightsOrApproveExecution()
    {
        var future = Now.AddDays(1);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request("performance",
            evidence: [Evidence("A", 0.2, end: future), Evidence("B", 0.5)], proof: Proof("performance"))).Decision);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request(proof: Proof("equal", end: future))).Decision);
    }

    [Fact]
    public void StrategyEvidenceCannotBeBorrowedAcrossStrategyOrScenario()
    {
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request("static",
            weights: new Dictionary<string, double> { ["A"] = 1, ["B"] = 1 },
            proof: Proof("equal"))).Decision);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request("dynamic",
            evidence: [Evidence("A", 0.2, "bear"), Evidence("B", 0.4, "bear")],
            segment: "bear", proof: Proof("dynamic", "bull"))).Decision);
    }

    [Fact]
    public void LowerSkillBoundAndMinimumSamplesMustPassStrictly()
    {
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request(proof: Proof("equal", lower: 0))).Decision);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request(
            risk: new FusionRiskPolicy(MinValidatedSkillLowerBound: 0.05), proof: Proof("equal", lower: 0.03))).Decision);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request(
            risk: new FusionRiskPolicy(MinValidationSamples: 150), proof: Proof("equal"))).Decision);
    }

    [Fact]
    public void DifferentStaticWeightsCannotReuseValidationProofForAnotherPlan()
    {
        var result = DecisionFusion.Predict(Request("static",
            weights: new Dictionary<string, double> { ["A"] = 1, ["B"] = 1 }, proof: Proof("static")));
        Assert.Equal("ABSTAIN", result.Decision);
        Assert.Contains("No eligible past validation", result.Reason);
    }

    [Fact]
    public void PerformanceWeightsCannotBorrowSpecializedSegmentEvidence()
    {
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request("performance",
            evidence: [Evidence("A", 0.2, "bull"), Evidence("B", 0.4, "bull")],
            proof: Proof("performance"))).Decision);
    }

    [Fact]
    public void OneTimeoutProducesExplicitStatusAndNoPartialExecution()
    {
        var result = DecisionFusion.Predict(Request(models: [Model("A", 0.9), new("B", "timeout")], proof: Proof("equal")));
        Assert.Equal("ABSTAIN", result.Decision);
        Assert.Equal("timeout", result.Models[1].Status);
        Assert.Equal(0, result.Models[1].Weight);
        Assert.Equal(1, result.Models[0].Weight);
    }

    [Fact]
    public void CorruptSuccessNeverSilentlyCountsAsNormal()
    {
        var malformed = new FusionModelPrediction("B", "success", new Dictionary<string, double> { ["UP"] = double.NaN, ["DOWN"] = 0 });
        var result = DecisionFusion.Predict(Request(models: [Model("A", 0.9), malformed], proof: Proof("equal")));
        Assert.Equal("ABSTAIN", result.Decision);
        Assert.Equal("invalid", result.Models[1].Status);
        Assert.Contains("invalid", result.Models[1].Reason);
    }

    [Fact]
    public void AllFailedAbstainsWithoutProbability()
    {
        var result = DecisionFusion.Predict(Request(models: [new("A", "failed"), new("B", "missing")]));
        Assert.Equal("ABSTAIN", result.Decision);
        Assert.Null(result.PredictedOption);
        Assert.Null(result.Probabilities);
        Assert.Equal(0, result.Models.Sum(x => x.Weight));
    }

    [Fact]
    public void SimulationRequiresExplicitPermissionEvenWhenValidatedEvidenceIsSupplied()
    {
        var models = new[] { Model("A", 0.9, simulation: true), Model("B", 0.8) };
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(Request(models: models, proof: Proof("equal"))).Decision);
        var allowed = DecisionFusion.Predict(Request(models: models,
            risk: new FusionRiskPolicy(AllowSimulatedExecution: true), proof: Proof("equal")));
        Assert.Equal("EXECUTE", allowed.Decision);
        Assert.True(allowed.IsSimulation);
    }

    [Fact]
    public void ConfiguredLossBenefitAndExposureConstraintsAreFailClosed()
    {
        var gate = new FusionRiskPolicy(MinExpectedBenefit: 0.1, MaxPotentialLoss: 0.2, MaxExposure: 0.1);
        var baseRequest = Request(risk: gate, proof: Proof("equal"));
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(baseRequest).Decision);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(baseRequest with
        { ExpectedBenefit = 0.12, PotentialLoss = 0.3, Exposure = 0.08 }).Decision);
        Assert.Equal("ABSTAIN", DecisionFusion.Predict(baseRequest with
        { ExpectedBenefit = 0.12, PotentialLoss = 0.18, Exposure = 0.2 }).Decision);
        Assert.Equal("EXECUTE", DecisionFusion.Predict(baseRequest with
        { ExpectedBenefit = 0.12, PotentialLoss = 0.18, Exposure = 0.08 }).Decision);
    }

    [Fact]
    public void InvalidWeightsAndRiskAreRejected()
    {
        Assert.Throws<ArgumentException>(() => DecisionFusion.Predict(Request("static", weights:
            new Dictionary<string, double> { ["A"] = double.NaN, ["B"] = 1 })));
        Assert.Throws<ArgumentException>(() => DecisionFusion.Predict(Request("static", weights:
            new Dictionary<string, double> { ["A"] = 0, ["B"] = 0 })));
        Assert.Throws<ArgumentException>(() => DecisionFusion.Predict(Request(
            risk: new FusionRiskPolicy(MinConfidence: double.PositiveInfinity))));
        Assert.Throws<ArgumentException>(() => DecisionFusion.Predict(Request(
            models: [Model("A", 0.9), Model("A", 0.5)])));
    }

    private static FusionHoldoutCase Case(string id, int day, string actual, double a, double b) =>
        new(id, Now.AddDays(day), actual,
            new Dictionary<string, IReadOnlyDictionary<string, double>>
            {
                ["A"] = Model("A", a).Probabilities!,
                ["B"] = Model("B", b).Probabilities!
            });
    private static FusionHoldoutRequest Holdout(params FusionHoldoutCase[] cases) =>
        new(Options, new Dictionary<string, double> { ["A"] = 0.5, ["B"] = 0.5 },
            "validation", Now.AddDays(-1), cases);

    [Fact]
    public void FrozenHoldoutComparisonCanDemonstrateFusionWorseThanBestSingle()
    {
        var result = FusionHoldoutComparison.Evaluate(Holdout(
            Case("c1", 1, "UP", 0.9, 0.3),
            Case("c2", 2, "UP", 0.9, 0.01),
            Case("c3", 3, "DOWN", 0.1, 0.99),
            Case("c4", 4, "DOWN", 0.1, 0.7)));
        Assert.Equal(1, result.BestSingleInHindsight.Accuracy);
        Assert.Equal("A", result.BestSingleInHindsight.ModelId);
        Assert.Equal(0.5, result.Fusion.Accuracy);
        Assert.Equal(-0.5, result.FusionAccuracyMinusBestSingle, 12);
        Assert.Equal(0.5, result.UniformRandomDistribution.Accuracy);
        Assert.True(result.Fusion.BrierLoss > result.BestSingleInHindsight.BrierLoss);
        Assert.Contains("HINDSIGHT", result.Interpretation);
    }

    [Fact]
    public void HoldoutCannotRefitWeightsOrUseTimeLeakedData()
    {
        Assert.Throws<ArgumentException>(() => FusionHoldoutComparison.Evaluate(
            Holdout(Case("c1", -2, "UP", 0.9, 0.5))));
        Assert.Throws<ArgumentException>(() => FusionHoldoutComparison.Evaluate(
            Holdout(Case("c1", 1, "UP", 0.9, 0.5), Case("c1", 2, "DOWN", 0.1, 0.5))));
        Assert.Throws<ArgumentException>(() => FusionHoldoutComparison.Evaluate(
            Holdout(Case("c1", 1, "UP", 0.9, 0.5)) with { WeightSourceSplit = "test" }));
        Assert.Throws<ArgumentException>(() => FusionHoldoutComparison.Evaluate(
            Holdout(Case("c1", 1, "UP", 0.9, 0.5)) with
            { FrozenWeights = new Dictionary<string, double> { ["A"] = double.NaN, ["B"] = 1 } }));
    }
}

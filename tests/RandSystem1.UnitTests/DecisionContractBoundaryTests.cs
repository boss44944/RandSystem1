using RandSystem1.Abstractions.Models;
using RandSystem1.Core.Validation;

namespace RandSystem1.UnitTests;

public sealed class DecisionContractBoundaryTests
{
    private static DecisionRequest Request() =>
        new("request-boundary", "multi_choice", ["UP", "DOWN"]);

    private static DecisionPrediction Prediction(
        string? selected = "UP",
        IReadOnlyDictionary<string, double>? probabilities = null,
        double latency = 1) =>
        new("request-boundary", "multi_choice", "test-adapter", "1.0",
            probabilities ?? new Dictionary<string, double>
            {
                ["UP"] = 0.6,
                ["DOWN"] = 0.4
            }, selected!, latency, DateTimeOffset.UtcNow, true);

    [Fact]
    public void RequestMustHaveIdentityAndCandidates()
    {
        Assert.Contains(DecisionValidator.ValidateRequest(new("", "", ["UP"])),
            error => error.Contains("RequestId", StringComparison.Ordinal));
        Assert.Contains(DecisionValidator.ValidateRequest(new("id", "task", [])),
            error => error.Contains("one option", StringComparison.Ordinal));
        Assert.Contains(DecisionValidator.ValidateRequest(null),
            error => error.Contains("required", StringComparison.Ordinal));
    }

    [Fact]
    public void PredictionCannotSelectOptionOutsideCandidateSet()
    {
        Assert.Contains(DecisionValidator.ValidatePrediction(Request(), Prediction(selected: "FLAT")),
            error => error.Contains("candidate", StringComparison.Ordinal));
    }

    [Fact]
    public void PredictionCannotIncludeExtraOrMissingCandidateProbabilities()
    {
        var values = new Dictionary<string, double>
        {
            ["UP"] = 0.5,
            ["UNKNOWN"] = 0.5
        };
        Assert.Contains(DecisionValidator.ValidatePrediction(Request(), Prediction(probabilities: values)),
            error => error.Contains("exactly the candidate options", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void PredictionLatencyMustBeFiniteAndNonnegative(double latency)
    {
        Assert.Contains(DecisionValidator.ValidatePrediction(Request(), Prediction(latency: latency)),
            error => error.Contains("LatencyMs", StringComparison.Ordinal));
    }

    [Fact]
    public void PredictionRequiresMatchingRequestAndTask()
    {
        var invalid = Prediction() with
        {
            RequestId = "other-request",
            TaskType = "other-task",
            ModelId = "",
            ModelVersion = "",
            PredictedAt = default
        };
        var errors = DecisionValidator.ValidatePrediction(Request(), invalid);
        Assert.Contains(errors, error => error.Contains("RequestId mismatch", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("TaskType mismatch", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("ModelId is required", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("ModelVersion is required", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("PredictedAt is required", StringComparison.Ordinal));
    }
}

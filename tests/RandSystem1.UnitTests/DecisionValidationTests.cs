using RandSystem1.Abstractions.Models;
using RandSystem1.Core.Validation;
using RandSystem1.Infrastructure.ModelAdapters;

namespace RandSystem1.UnitTests;

public sealed class DecisionValidationTests
{
    private static DecisionRequest NewRequest() =>
        new("case-001", "three_choice", new[] { "A", "B", "C" });

    [Fact]
    public void RequestRejectsDuplicateAndEmptyOptions()
    {
        Assert.NotEmpty(DecisionValidator.ValidateRequest(
            new("id", "task", new[] { "A", "A", "" })));
    }

    [Fact]
    public async Task MockPredictionHasValidUniformProbabilities()
    {
        var request = NewRequest();
        var prediction = await new MockUniformDecisionModel().PredictAsync(request);
        Assert.Empty(DecisionValidator.ValidatePrediction(request, prediction));
        Assert.True(prediction.IsSimulation);
        Assert.All(prediction.Probabilities.Values, p =>
            Assert.Equal(1.0 / 3, p, precision: 12));
    }

    [Fact]
    public async Task MockPredictionIsRepeatableExceptTimestampsAndLatency()
    {
        var model = new MockUniformDecisionModel();
        var a = await model.PredictAsync(NewRequest());
        var b = await model.PredictAsync(NewRequest());
        Assert.Equal(a.PredictedOption, b.PredictedOption);
        Assert.Equal(a.Probabilities, b.Probabilities);
    }

    [Fact]
    public async Task ModelSupportsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new MockUniformDecisionModel().PredictAsync(NewRequest(), source.Token));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void RejectsInvalidProbability(double invalid)
    {
        var request = new DecisionRequest("id", "task", new[] { "UP", "DOWN" });
        var prediction = new DecisionPrediction("id", "task", "model", "1.0",
            new Dictionary<string, double> { ["UP"] = invalid, ["DOWN"] = 1 - invalid },
            "UP", 0, DateTimeOffset.UtcNow, false);
        Assert.NotEmpty(DecisionValidator.ValidatePrediction(request, prediction));
    }

    [Fact]
    public void RejectsMissingCandidateProbability()
    {
        var request = NewRequest();
        var prediction = new DecisionPrediction("case-001", "three_choice", "model", "1",
            new Dictionary<string, double> { ["A"] = 0.5, ["B"] = 0.5 }, "A", 0,
            DateTimeOffset.UtcNow, false);
        Assert.Contains(DecisionValidator.ValidatePrediction(request, prediction),
            error => error.Contains("cover exactly"));
    }

    [Fact]
    public void RejectsProbabilitiesWhoseSumIsNotOne()
    {
        var request = new DecisionRequest("id", "task", new[] { "UP", "DOWN" });
        var prediction = new DecisionPrediction("id", "task", "model", "1",
            new Dictionary<string, double> { ["UP"] = 0.4, ["DOWN"] = 0.4 }, "UP", 0,
            DateTimeOffset.UtcNow, false);
        Assert.Contains(DecisionValidator.ValidatePrediction(request, prediction),
            error => error.Contains("sum to 1"));
    }
}

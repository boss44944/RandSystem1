using System.Diagnostics;
using RandSystem1.Abstractions.Interfaces;
using RandSystem1.Abstractions.Models;
using RandSystem1.Core.Validation;

namespace RandSystem1.Infrastructure.ModelAdapters;

/// <summary>
/// Deterministic simulated adapter for protocol testing ONLY.
/// Uniform probabilities are not calibrated estimates of any real AI model.
/// </summary>
public sealed class MockUniformDecisionModel : IDecisionModel
{
    public string ModelId => "mock-uniform";

    public Task<DecisionPrediction> PredictAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validationErrors = DecisionValidator.ValidateRequest(request);
        if (validationErrors.Count != 0)
            throw new ArgumentException(string.Join(" ", validationErrors), nameof(request));

        var watch = Stopwatch.StartNew();
        double probability = 1.0 / request.Options.Count;
        var probabilities = request.Options.ToDictionary(x => x, _ => probability, StringComparer.Ordinal);
        watch.Stop();

        var result = new DecisionPrediction(
            request.RequestId,
            request.TaskType,
            ModelId,
            "0.1.0-mock",
            probabilities,
            request.Options[0],
            watch.Elapsed.TotalMilliseconds,
            DateTimeOffset.UtcNow,
            IsSimulation: true);
        return Task.FromResult(result);
    }
}

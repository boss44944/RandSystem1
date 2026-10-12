namespace RandSystem1.Abstractions.Models;

/// <summary>Probabilities are predictions, not observed correctness frequencies.</summary>
public sealed record DecisionPrediction(
    string RequestId,
    string TaskType,
    string ModelId,
    string ModelVersion,
    IReadOnlyDictionary<string, double> Probabilities,
    string PredictedOption,
    double LatencyMs,
    DateTimeOffset PredictedAt,
    bool IsSimulation);

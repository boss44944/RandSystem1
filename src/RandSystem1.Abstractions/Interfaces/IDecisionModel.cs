using RandSystem1.Abstractions.Models;

namespace RandSystem1.Abstractions.Interfaces;

public interface IDecisionModel
{
    string ModelId { get; }

    Task<DecisionPrediction> PredictAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default);
}

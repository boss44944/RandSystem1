using RandSystem1.Abstractions.Models;

namespace RandSystem1.Core.Validation;

public static class DecisionValidator
{
    public const double ProbabilityTolerance = 1e-9;

    public static IReadOnlyList<string> ValidateRequest(DecisionRequest? request)
    {
        var errors = new List<string>();
        if (request is null)
        {
            errors.Add("Request is required.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.RequestId))
            errors.Add("RequestId is required.");
        if (string.IsNullOrWhiteSpace(request.TaskType))
            errors.Add("TaskType is required.");
        if (request.Options is null || request.Options.Count == 0)
        {
            errors.Add("At least one option is required.");
            return errors;
        }

        if (request.Options.Any(string.IsNullOrWhiteSpace))
            errors.Add("Options must be non-empty.");
        if (request.Options.Distinct(StringComparer.Ordinal).Count() != request.Options.Count)
            errors.Add("Options must be unique.");
        return errors;
    }

    public static IReadOnlyList<string> ValidatePrediction(DecisionRequest? request, DecisionPrediction? prediction)
    {
        var errors = new List<string>(ValidateRequest(request));
        if (prediction is null)
        {
            errors.Add("Prediction is required.");
            return errors;
        }

        if (request is null) return errors;
        if (prediction.RequestId != request.RequestId) errors.Add("RequestId mismatch.");
        if (prediction.TaskType != request.TaskType) errors.Add("TaskType mismatch.");
        if (string.IsNullOrWhiteSpace(prediction.ModelId)) errors.Add("ModelId is required.");
        if (string.IsNullOrWhiteSpace(prediction.ModelVersion)) errors.Add("ModelVersion is required.");
        if (prediction.PredictedAt == default) errors.Add("PredictedAt is required.");
        if (!double.IsFinite(prediction.LatencyMs) || prediction.LatencyMs < 0)
            errors.Add("LatencyMs must be finite and nonnegative.");
        if (request.Options is null || request.Options.Count == 0) return errors;
        if (!request.Options.Contains(prediction.PredictedOption))
            errors.Add("PredictedOption must be a candidate option.");

        if (prediction.Probabilities is null)
        {
            errors.Add("Probabilities are required.");
            return errors;
        }

        if (prediction.Probabilities.Count != request.Options.Count ||
            request.Options.Any(option => !prediction.Probabilities.ContainsKey(option)))
            errors.Add("Probabilities must cover exactly the candidate options.");
        if (prediction.Probabilities.Any(kv =>
                !double.IsFinite(kv.Value) || kv.Value < 0 || kv.Value > 1))
            errors.Add("Each probability must be finite and within [0,1].");

        double sum = prediction.Probabilities.Values.Sum();
        if (!double.IsFinite(sum) || Math.Abs(sum - 1.0) > ProbabilityTolerance)
            errors.Add("Probabilities must sum to 1.");

        return errors;
    }
}

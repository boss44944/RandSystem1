namespace RandSystem1.Core.Statistics;

/// <summary>A chronologically later, labeled case. Labels may only be read by Evaluate.</summary>
public sealed record FusionHoldoutCase(
    string ObservationId,
    DateTimeOffset ObservedAt,
    string Actual,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> ModelProbabilities);

public sealed record FusionHoldoutRequest(
    IReadOnlyList<string> Options,
    IReadOnlyDictionary<string, double> FrozenWeights,
    string WeightSourceSplit,
    DateTimeOffset WeightsFrozenAt,
    IReadOnlyList<FusionHoldoutCase> Cases);

public sealed record FusionScore(string ModelId, double Accuracy, double BrierLoss);

public sealed record FusionHoldoutReport(
    int Count,
    FusionScore Fusion,
    IReadOnlyList<FusionScore> Models,
    FusionScore BestSingleInHindsight,
    FusionScore UniformRandomDistribution,
    double FusionAccuracyMinusBestSingle,
    double FusionBrierMinusBestSingle,
    string Interpretation);

public static class FusionHoldoutComparison
{
    /// <summary>
    /// No label-dependent weight fitting occurs here. Weights MUST be frozen
    /// before the first test outcome; scores are descriptive, not a significance test.
    /// </summary>
    public static FusionHoldoutReport Evaluate(FusionHoldoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClassificationMetrics.ValidateOptions(request.Options);
        if (request.FrozenWeights is null || request.FrozenWeights.Count is < 1 or > 20 ||
            request.FrozenWeights.Keys.Any(string.IsNullOrWhiteSpace) ||
            request.FrozenWeights.Values.Any(v => !double.IsFinite(v) || v < 0) ||
            Math.Abs(request.FrozenWeights.Values.Sum() - 1) > 1e-9 ||
            !string.Equals(request.WeightSourceSplit, "validation", StringComparison.OrdinalIgnoreCase) ||
            request.WeightsFrozenAt == default)
            throw new ArgumentException("Require finite nonnegative normalized weights frozen from validation.");
        if (request.Cases is null || request.Cases.Count is < 1 or > 20000)
            throw new ArgumentException("Require 1..20000 aligned holdout cases.");

        var labels = request.Options.ToHashSet(StringComparer.Ordinal);
        var modelIds = request.FrozenWeights.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var correct = modelIds.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        var brier = modelIds.ToDictionary(x => x, _ => 0d, StringComparer.Ordinal);
        double fusionBrier = 0;
        int fusionCorrect = 0;
        double randomBrier = 0;

        foreach (var row in request.Cases)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.ObservationId) ||
                !ids.Add(row.ObservationId) || row.ObservedAt <= request.WeightsFrozenAt ||
                !labels.Contains(row.Actual) || row.ModelProbabilities is null ||
                row.ModelProbabilities.Count != modelIds.Length ||
                row.ModelProbabilities.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(modelIds) == false)
                throw new ArgumentException("Holdout cases must be uniquely identified, aligned, labeled, and later than frozen weights.");

            var merged = request.Options.ToDictionary(x => x, _ => 0d, StringComparer.Ordinal);
            foreach (var modelId in modelIds)
            {
                var vector = row.ModelProbabilities[modelId];
                if (vector is null || vector.Count != labels.Count ||
                    !vector.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(labels) ||
                    vector.Values.Any(v => !double.IsFinite(v) || v < 0 || v > 1) ||
                    Math.Abs(vector.Values.Sum() - 1) > 1e-9)
                    throw new ArgumentException("Holdout model probabilities must exactly cover options.");
                string pick = request.Options.Aggregate((a, b) => vector[b] > vector[a] ? b : a);
                if (pick == row.Actual) correct[modelId]++;
                foreach (var label in request.Options)
                {
                    double expected = label == row.Actual ? 1 : 0;
                    double delta = vector[label] - expected;
                    brier[modelId] += delta * delta;
                    merged[label] += request.FrozenWeights[modelId] * vector[label];
                }
            }
            string fusedPick = request.Options.Aggregate((a, b) => merged[b] > merged[a] ? b : a);
            if (fusedPick == row.Actual) fusionCorrect++;
            foreach (var label in request.Options)
            {
                double expected = label == row.Actual ? 1 : 0;
                fusionBrier += Math.Pow(merged[label] - expected, 2);
                randomBrier += Math.Pow(1d / request.Options.Count - expected, 2);
            }
        }

        int n = request.Cases.Count;
        var models = modelIds.Select(x => new FusionScore(x, correct[x] / (double)n, brier[x] / n)).ToArray();
        var best = models.OrderByDescending(x => x.Accuracy)
            .ThenBy(x => x.BrierLoss).ThenBy(x => x.ModelId, StringComparer.Ordinal).First();
        var fusion = new FusionScore("fusion", fusionCorrect / (double)n, fusionBrier / n);
        return new(n, fusion, models, best,
            new FusionScore("uniform-distribution-baseline", 1d / request.Options.Count, randomBrier / n),
            fusion.Accuracy - best.Accuracy, fusion.BrierLoss - best.BrierLoss,
            "Labeled later holdout; weights were frozen from earlier validation. Best single is selected IN HINDSIGHT only for descriptive comparison, not a tradeable selection. " +
            "Uniform accuracy is theoretical expected categorical sampling accuracy, not the argmax of uniform probabilities. " +
            "No statistical significance, calibration or profitability is implied.");
    }
}

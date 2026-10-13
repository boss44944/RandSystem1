namespace RandSystem1.Core.Statistics;

/// <summary>Predictions on fixed, aligned test cases, independent of whether ground truth is available.</summary>
public sealed record ModelChoice(string ObservationId, string PredictedOption);

public sealed record NamedModelChoices(string ModelId, IReadOnlyList<ModelChoice> Predictions);

/// <summary>Ground truth must be out-of-sample and have the exact same ID/order as every model.</summary>
public sealed record ObservedTruth(string ObservationId, string Actual);

public sealed record ComplementarityRequest(
    IReadOnlyList<string> Options,
    IReadOnlyList<NamedModelChoices> Models,
    IReadOnlyList<ObservedTruth>? GroundTruth = null);

public sealed record PerModelComplementarity(
    string ModelId,
    double? Accuracy,
    IReadOnlyDictionary<string, int> PredictionCounts);

public sealed record PairComplementarity(
    string ModelA,
    string ModelB,
    int Total,
    int AgreementCount,
    int DisagreementCount,
    double DisagreementRate,
    int? BothCorrect,
    int? BothWrong,
    int? OnlyACorrect,
    int? OnlyBCorrect,
    double? ModelAAccuracy,
    double? ModelBAccuracy,
    double? JointCorrectCoverage,
    double? ErrorPhiCorrelation,
    double? OracleUpperBound,
    double? OracleGainOverBestSingle);

public sealed record ComplementarityReport(
    int SampleCount,
    int ModelCount,
    bool HasGroundTruth,
    IReadOnlyList<PerModelComplementarity> Models,
    IReadOnlyList<PairComplementarity> Pairs,
    double? AtLeastOneCorrectCoverage,
    double? OracleUpperBound,
    double? OracleGainOverBestSingle,
    string Interpretation);

/// <summary>
/// Computes alignment-safe, descriptive model complementarity.
/// Oracle ceiling uses ground truth AFTER decisions and is never an actionable fusion policy.
/// </summary>
public static class ModelComplementarity
{
    public const int MaxModels = 20;
    public const int MaxSamples = 20000;
    private const int MaxPairwiseEvaluations = 4_000_000;

    public static ComplementarityReport Evaluate(
        ComplementarityRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClassificationMetrics.ValidateOptions(request.Options);
        if (request.Models is null || request.Models.Count is < 2 or > MaxModels)
            throw new ArgumentException($"Models must contain 2..{MaxModels} distinct model IDs.");
        if (request.Models.Any(m => m is null || string.IsNullOrWhiteSpace(m.ModelId)) ||
            request.Models.Select(m => m.ModelId).Distinct(StringComparer.Ordinal).Count() != request.Models.Count)
            throw new ArgumentException("Model IDs must be nonempty and unique.");
        var first = request.Models[0].Predictions;
        if (first is null || first.Count is < 1 or > MaxSamples ||
            (long)first.Count * request.Models.Count * (request.Models.Count - 1) / 2 > MaxPairwiseEvaluations)
            throw new ArgumentException("Nonempty observations and bounded pairwise evaluation count required.");
        var allowed = request.Options.ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in first)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.ObservationId) ||
                !ids.Add(row.ObservationId) || !allowed.Contains(row.PredictedOption))
                throw new ArgumentException("Reference predictions require unique IDs and valid choices.");
        }
        // Strict indexed alignment prevents accidental comparing different timestamps/cases.
        foreach (var model in request.Models)
        {
            if (model.Predictions is null || model.Predictions.Count != first.Count)
                throw new ArgumentException("All models must have equal-length predictions.");
            for (int i = 0; i < first.Count; i++)
            {
                var row = model.Predictions[i];
                if (row is null || row.ObservationId != first[i].ObservationId ||
                    !allowed.Contains(row.PredictedOption))
                    throw new ArgumentException("Models must align by observation ID and order, with valid choices.");
            }
        }

        bool labeled = request.GroundTruth is not null;
        if (labeled)
        {
            if (request.GroundTruth!.Count != first.Count)
                throw new ArgumentException("Ground truth must align with the exact number of model observations.");
            for (int i = 0; i < first.Count; i++)
            {
                var row = request.GroundTruth[i];
                if (row is null || row.ObservationId != first[i].ObservationId ||
                    !allowed.Contains(row.Actual))
                    throw new ArgumentException("Ground truth must align by ID/order with valid class labels.");
            }
        }

        var modelStats = new List<PerModelComplementarity>(request.Models.Count);
        var correct = labeled ? new bool[request.Models.Count][] : null;
        for (int m = 0; m < request.Models.Count; m++)
        {
            ct.ThrowIfCancellationRequested();
            var model = request.Models[m];
            var counts = request.Options.ToDictionary(k => k, _ => 0, StringComparer.Ordinal);
            if (labeled) correct![m] = new bool[first.Count];
            int correctCount = 0;
            for (int i = 0; i < first.Count; i++)
            {
                string prediction = model.Predictions[i].PredictedOption;
                counts[prediction]++;
                if (labeled && prediction == request.GroundTruth![i].Actual)
                {
                    correct![m][i] = true;
                    correctCount++;
                }
            }
            modelStats.Add(new PerModelComplementarity(
                model.ModelId, labeled ? (double)correctCount / first.Count : null, counts));
        }

        var pairs = new List<PairComplementarity>();
        for (int a = 0; a < request.Models.Count; a++)
        for (int b = a + 1; b < request.Models.Count; b++)
        {
            ct.ThrowIfCancellationRequested();
            int disagreements = 0;
            int bothCorrect = 0, bothWrong = 0, onlyA = 0, onlyB = 0;
            var left = request.Models[a].Predictions;
            var right = request.Models[b].Predictions;
            for (int i = 0; i < first.Count; i++)
            {
                if (left[i].PredictedOption != right[i].PredictedOption) disagreements++;
                if (!labeled) continue;
                bool ca = correct![a][i], cb = correct[b][i];
                if (ca && cb) bothCorrect++;
                else if (!ca && !cb) bothWrong++;
                else if (ca) onlyA++;
                else onlyB++;
            }
            double? phi = null;
            double? union = null, gain = null;
            if (labeled)
            {
                // Pearson/Phi association between binary error indicators:
                // +1 = co-occurring errors; -1 = mutually exclusive errors.
                double denominator = Math.Sqrt(
                    (double)(bothCorrect + onlyA) * (bothWrong + onlyB) *
                    (bothCorrect + onlyB) * (bothWrong + onlyA));
                if (denominator > 0)
                    phi = ((double)bothCorrect * bothWrong - (double)onlyA * onlyB) / denominator;
                union = (double)(bothCorrect + onlyA + onlyB) / first.Count;
                gain = union.Value - Math.Max(modelStats[a].Accuracy!.Value, modelStats[b].Accuracy!.Value);
            }
            pairs.Add(new PairComplementarity(
                request.Models[a].ModelId, request.Models[b].ModelId,
                first.Count, first.Count - disagreements, disagreements,
                (double)disagreements / first.Count,
                labeled ? bothCorrect : null, labeled ? bothWrong : null,
                labeled ? onlyA : null, labeled ? onlyB : null,
                modelStats[a].Accuracy, modelStats[b].Accuracy,
                union, phi, union, gain));
        }

        double? globalUnion = null, globalGain = null;
        if (labeled)
        {
            int correctAny = 0;
            for (int i = 0; i < first.Count; i++)
            {
                if (correct!.Any(model => model[i])) correctAny++;
            }
            globalUnion = (double)correctAny / first.Count;
            globalGain = globalUnion - modelStats.Max(m => m.Accuracy!.Value);
        }
        return new ComplementarityReport(
            first.Count, request.Models.Count, labeled, modelStats, pairs,
            globalUnion, globalUnion, globalGain,
            labeled
                ? "Contingency counts and accuracies use the same aligned held-out ground truth. " +
                  "Oracle = share of cases with at least one correct model, chosen using the ANSWER AFTER observation. " +
                  "This hindsight upper bound is NOT achievable fusion accuracy or proof of a deployable policy. " +
                  "Pair error Phi is undefined (null) when any error indicator has zero variance; " +
                  "disagreement and complementary correct cases are distinct. Do not fit fusion on test labels."
                : "Unlabeled mode reports only prediction frequency, pair agreement/disagreement and alignment. " +
                  "Without ground truth, accuracy, error correlation, joint correctness, skill and Oracle " +
                  "cannot be estimated. Prediction disagreement alone NEVER proves predictive skill.");
    }
}

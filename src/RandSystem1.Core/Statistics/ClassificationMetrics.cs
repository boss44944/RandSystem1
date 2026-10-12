namespace RandSystem1.Core.Statistics;

/// <summary>A test/holdout observation; one probability for each declared candidate.</summary>
public sealed record ScoredObservation(
    string ObservationId,
    string Actual,
    string Predicted,
    IReadOnlyDictionary<string, double> Probabilities);

public sealed record EvaluationRequest(
    IReadOnlyList<string> Options,
    IReadOnlyList<ScoredObservation> Observations,
    int CalibrationBins = 10,
    IReadOnlyList<ScoredObservation>? BaselineObservations = null);

public sealed record CalibrationBin(
    int Index, double LowerBound, double UpperBound,
    int Count, double MeanConfidence, double EmpiricalAccuracy);

public sealed record ClassScores(
    string Option, int Support, int PredictedCount, int TruePositives,
    double Precision, double? Recall, double? F1);

public sealed record SkillScores(
    double BaselineBrier, double BaselineLogLoss, double? BrierSkill, double? LogLossSkill);

public sealed record EvaluationReport(
    int Count, int ClassCount,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ConfusionMatrix,
    IReadOnlyList<ClassScores> PerClass,
    double Accuracy, double BalancedAccuracy,
    double MacroPrecision, double MacroRecall, double MacroF1,
    double Brier, double LogLoss, double Ece,
    IReadOnlyList<CalibrationBin> Reliability,
    SkillScores? Skill,
    string ProbabilityMeaning,
    string Assumptions);

public static class ClassificationMetrics
{
    public const double LogLossFloor = 1e-15;

    public static void ValidateOptions(IReadOnlyList<string>? options)
    {
        if (options is null || options.Count is < 1 or > 256 ||
            options.Any(string.IsNullOrWhiteSpace) ||
            options.Distinct(StringComparer.Ordinal).Count() != options.Count)
            throw new ArgumentException("Options require 1..256 unique nonempty labels.");
    }

    public static void ValidateObservations(
        IReadOnlyList<string> options, IReadOnlyList<ScoredObservation>? observations)
    {
        ValidateOptions(options);
        if (observations is null || observations.Count is < 1 or > 100000)
            throw new ArgumentException("Require 1..100000 labeled test observations.");
        var optionSet = options.ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in observations)
        {
            if (x is null || string.IsNullOrWhiteSpace(x.ObservationId) || !ids.Add(x.ObservationId))
                throw new ArgumentException("Observation IDs must be unique and nonempty.");
            if (!optionSet.Contains(x.Actual) || !optionSet.Contains(x.Predicted))
                throw new ArgumentException("Actual and predicted labels must be valid options.");
            if (x.Probabilities is null || x.Probabilities.Count != optionSet.Count ||
                x.Probabilities.Keys.Any(k => !optionSet.Contains(k)) ||
                options.Any(o => !x.Probabilities.ContainsKey(o)))
                throw new ArgumentException("Probabilities must cover exactly all candidates.");
            if (x.Probabilities.Values.Any(p => !double.IsFinite(p) || p < 0 || p > 1))
                throw new ArgumentException("All probabilities must be finite and within [0,1].");
            if (Math.Abs(x.Probabilities.Values.Sum() - 1) > 1e-9)
                throw new ArgumentException("Probabilities must sum to one.");
        }
    }

    public static double BrierLoss(IReadOnlyList<string> options, ScoredObservation x) =>
        options.Sum(o =>
        {
            double delta = x.Probabilities[o] - (o == x.Actual ? 1.0 : 0);
            return delta * delta;
        });

    public static double CrossEntropy(ScoredObservation x) =>
        -Math.Log(Math.Max(LogLossFloor, x.Probabilities[x.Actual]));

    public static EvaluationReport Evaluate(EvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateObservations(request.Options, request.Observations);
        if (request.CalibrationBins is < 2 or > 100)
            throw new ArgumentException("CalibrationBins must be 2..100.");

        SkillScores? skill = null;
        if (request.BaselineObservations is not null)
        {
            ValidateObservations(request.Options, request.BaselineObservations);
            if (request.Observations.Count != request.BaselineObservations.Count ||
                request.Observations.Where((x, i) =>
                    x.ObservationId != request.BaselineObservations[i].ObservationId ||
                    x.Actual != request.BaselineObservations[i].Actual).Any())
                throw new ArgumentException("Baseline must have identical test IDs, ordering, and truth labels.");
            double baseBrier = request.BaselineObservations.Average(x => BrierLoss(request.Options, x));
            double baseLog = request.BaselineObservations.Average(CrossEntropy);
            double loss = request.Observations.Average(x => BrierLoss(request.Options, x));
            double log = request.Observations.Average(CrossEntropy);
            skill = new SkillScores(baseBrier, baseLog,
                baseBrier <= 0 ? null : 1 - loss / baseBrier,
                baseLog <= 0 ? null : 1 - log / baseLog);
        }

        // One pass over observations, rather than O(observations * classes^2).
        var counts = request.Options.ToDictionary(
            actual => actual,
            _ => request.Options.ToDictionary(predicted => predicted, _ => 0, StringComparer.Ordinal),
            StringComparer.Ordinal);
        var supportCounts = request.Options.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        var predictedCounts = request.Options.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        foreach (var observation in request.Observations)
        {
            counts[observation.Actual][observation.Predicted]++;
            supportCounts[observation.Actual]++;
            predictedCounts[observation.Predicted]++;
        }
        var confusion = counts.ToDictionary(
            kv => kv.Key, kv => (IReadOnlyDictionary<string, int>)kv.Value, StringComparer.Ordinal);
        var perClass = new List<ClassScores>(request.Options.Count);
        foreach (string option in request.Options)
        {
            int tp = counts[option][option];
            int support = supportCounts[option];
            int predicted = predictedCounts[option];
            double precision = predicted == 0 ? 0 : (double)tp / predicted;
            double? recall = support == 0 ? null : (double)tp / support;
            double? f1 = recall is null ? null :
                precision + recall == 0 ? 0 : 2 * precision * recall / (precision + recall);
            perClass.Add(new ClassScores(option, support, predicted, tp, precision, recall, f1));
        }

        var present = perClass.Where(x => x.Support > 0).ToArray();
        int n = request.Observations.Count;
        var bins = new (int Count, double ConfidenceSum, int Correct)[request.CalibrationBins];
        foreach (var x in request.Observations)
        {
            // Reported-choice ECE: confidence is for the label actually predicted;
            // do not assume it is necessarily the argmax.
            double confidence = x.Probabilities[x.Predicted];
            int idx = Math.Min((int)(confidence * request.CalibrationBins), request.CalibrationBins - 1);
            var b = bins[idx];
            bins[idx] = (b.Count + 1, b.ConfidenceSum + confidence,
                b.Correct + (x.Actual == x.Predicted ? 1 : 0));
        }
        var reliability = new List<CalibrationBin>(bins.Length);
        double ece = 0;
        for (int i = 0; i < bins.Length; i++)
        {
            var b = bins[i];
            double confidence = b.Count == 0 ? 0 : b.ConfidenceSum / b.Count;
            double accuracy = b.Count == 0 ? 0 : (double)b.Correct / b.Count;
            if (b.Count > 0) ece += (double)b.Count / n * Math.Abs(confidence - accuracy);
            reliability.Add(new CalibrationBin(i, (double)i / bins.Length,
                (double)(i + 1) / bins.Length, b.Count, confidence, accuracy));
        }

        return new EvaluationReport(
            n, request.Options.Count, confusion, perClass,
            (double)request.Observations.Count(x => x.Predicted == x.Actual) / n,
            present.Average(x => x.Recall!.Value), present.Average(x => x.Precision),
            present.Average(x => x.Recall!.Value), present.Average(x => x.F1!.Value),
            request.Observations.Average(x => BrierLoss(request.Options, x)),
            request.Observations.Average(CrossEntropy), ece, reliability, skill,
            "submitted-model-probabilities-not-necessarily-calibrated",
            "All scoring requires ground truth and aligned out-of-sample observations; " +
            "macro scores average classes present in the test set (zero precision where never predicted); " +
            "Brier is multiclass sum of squares (not divided by class count); " +
            "top-label ECE is descriptive and is not proof of calibration.");
    }
}

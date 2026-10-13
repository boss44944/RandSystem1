namespace RandSystem1.Core.Statistics;

/// <summary>
/// A reported prediction is never a measured accuracy. Non-success statuses are
/// explicit, and an invalid success is excluded rather than silently repaired.
/// </summary>
public sealed record FusionModelPrediction(
    string ModelId,
    string Status,
    IReadOnlyDictionary<string, double>? Probabilities = null,
    bool IsSimulation = false);

/// <summary>
/// Caller-supplied, earlier validation evidence. Test/holdout statistics are not
/// eligible to select weights. LowerSkillBound is a conservative, separately
/// computed lower confidence limit, not the point estimate of skill.
/// </summary>
public sealed record FusionValidationEvidence(
    string ModelId,
    string TaskType,
    string? Segment,
    string Split,
    int SampleCount,
    double BrierLoss,
    double BaselineBrierLoss,
    double LowerSkillBound,
    DateTimeOffset ValidationEndedAt);

/// <summary>
/// Evidence for the frozen, entire fusion strategy, not merely for its component
/// models. It must be produced from earlier out-of-sample validation data.
/// </summary>
public sealed record FusionStrategyEvidence(
    string Strategy,
    string TaskType,
    string? Segment,
    string Split,
    int SampleCount,
    double LowerBrierSkillBound,
    DateTimeOffset ValidationEndedAt,
    IReadOnlyList<string> ModelIds,
    IReadOnlyDictionary<string, double> FrozenWeights);

public sealed record FusionRiskPolicy(
    double MinConfidence = 0.6,
    double MinValidatedSkillLowerBound = 0,
    int MinValidationSamples = 30,
    double? MinExpectedBenefit = null,
    double? MaxPotentialLoss = null,
    double? MaxExposure = null,
    bool AllowSimulatedExecution = false);

public sealed record FusionRequest(
    string RequestId,
    string TaskType,
    IReadOnlyList<string> Options,
    IReadOnlyList<FusionModelPrediction> Models,
    string Strategy = "equal",
    IReadOnlyDictionary<string, double>? StaticWeights = null,
    IReadOnlyList<FusionValidationEvidence>? Validation = null,
    FusionStrategyEvidence? StrategyEvidence = null,
    string? Segment = null,
    DateTimeOffset? DecisionAt = null,
    FusionRiskPolicy? Risk = null,
    double? ExpectedBenefit = null,
    double? PotentialLoss = null,
    double? Exposure = null);

public sealed record FusionModelStatus(
    string ModelId, string Status, string Reason, double Weight, bool IsSimulation);

public sealed record FusionResult(
    string RequestId,
    string Strategy,
    string Decision,
    string? PredictedOption,
    IReadOnlyDictionary<string, double>? Probabilities,
    double? Confidence,
    IReadOnlyList<FusionModelStatus> Models,
    string Reason,
    bool IsSimulation,
    string ProbabilityMeaning,
    string ValidationMeaning);

public static class DecisionFusion
{
    public const string Version = "fusion-0.5.0";

    public static FusionResult Predict(FusionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClassificationMetrics.ValidateOptions(request.Options);
        if (string.IsNullOrWhiteSpace(request.RequestId) || string.IsNullOrWhiteSpace(request.TaskType))
            throw new ArgumentException("RequestId and TaskType are required.");
        if (request.Models is null || request.Models.Count is < 1 or > 20 ||
            request.Models.Any(m => m is null || string.IsNullOrWhiteSpace(m.ModelId)) ||
            request.Models.Select(m => m.ModelId).Distinct(StringComparer.Ordinal).Count() != request.Models.Count)
            throw new ArgumentException("Require 1..20 distinct nonempty model identifiers.");

        string strategy = (request.Strategy ?? "").ToLowerInvariant();
        if (strategy is not ("equal" or "static" or "performance" or "dynamic"))
            throw new ArgumentException("Unknown fusion strategy.");

        var risk = request.Risk ?? new FusionRiskPolicy();
        ValidateRisk(risk, request);
        var decisionAt = request.DecisionAt ?? DateTimeOffset.UtcNow;
        if (decisionAt == default) throw new ArgumentException("DecisionAt must be set.");

        var optionSet = request.Options.ToHashSet(StringComparer.Ordinal);
        var valid = new List<FusionModelPrediction>();
        var state = new Dictionary<string, (string Status, string Reason)>(StringComparer.Ordinal);
        var audit = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var model in request.Models)
        {
            string status = (model.Status ?? "").ToLowerInvariant();
            if (status != "success")
            {
                if (status is not ("missing" or "failed" or "timeout" or "invalid" or "cancelled"))
                    throw new ArgumentException("Model status must be success, missing, failed, timeout, invalid or cancelled.");
                state.Add(model.ModelId, (status, "Model did not provide a successful prediction."));
                continue;
            }
            var p = model.Probabilities;
            if (p is null || p.Count != request.Options.Count ||
                p.Keys.Any(k => !optionSet.Contains(k)) ||
                p.Values.Any(v => !double.IsFinite(v) || v is < 0 or > 1) ||
                !double.IsFinite(p.Values.Sum()) ||
                Math.Abs(p.Values.Sum() - 1) > 1e-9)
            {
                state.Add(model.ModelId, ("invalid", "Successful model supplied an invalid probability vector."));
                continue;
            }
            state.Add(model.ModelId, ("success", "Valid probability vector."));
            valid.Add(model);
        }

        IReadOnlyDictionary<string, double> weights = new Dictionary<string, double>();
        string? unavailable = null;
        if (valid.Count != 0)
        {
            var raw = new Dictionary<string, double>(StringComparer.Ordinal);
            switch (strategy)
            {
                case "equal":
                    foreach (var model in valid)
                    {
                        raw.Add(model.ModelId, 1);
                        audit[model.ModelId] = "Equal weight among successful models.";
                    }
                    break;
                case "static":
                    if (request.StaticWeights is null ||
                        request.StaticWeights.Count != request.Models.Count ||
                        request.Models.Any(m => !request.StaticWeights.ContainsKey(m.ModelId)) ||
                        request.StaticWeights.Values.Any(x => !double.IsFinite(x) || x < 0) ||
                        request.StaticWeights.Values.Sum() <= 0 ||
                        !double.IsFinite(request.StaticWeights.Values.Sum()))
                        throw new ArgumentException("StaticWeights must include every model with finite nonnegative weights and positive sum.");
                    foreach (var model in valid)
                    {
                        raw.Add(model.ModelId, request.StaticWeights[model.ModelId]);
                        audit[model.ModelId] = "Caller-specified static weight.";
                    }
                    break;
                case "performance":
                case "dynamic":
                    // If a model has no valid, past validation evidence, it is not
                    // eligible for learned weighting. Never peek at test labels.
                    foreach (var model in valid)
                    {
                        var evidence = FindEvidence(request, model.ModelId, strategy, decisionAt, risk.MinValidationSamples);
                        if (evidence is null)
                        {
                            unavailable = "Missing or ineligible earlier validation evidence for some models.";
                            break;
                        }
                        double skill = 1 - evidence.BrierLoss / evidence.BaselineBrierLoss;
                        raw.Add(model.ModelId, Math.Max(0, Math.Min(1, skill)));
                        audit[model.ModelId] = $"Validation split: n={evidence.SampleCount}; Brier={evidence.BrierLoss:G6}; baseline={evidence.BaselineBrierLoss:G6}; model skill={skill:G6}; lower skill bound={evidence.LowerSkillBound:G6}; through {evidence.ValidationEndedAt:O}.";
                    }
                    break;
            }
            if (unavailable is null)
            {
                double sum = raw.Values.Sum();
                if (sum > 0 && double.IsFinite(sum))
                    weights = raw.ToDictionary(x => x.Key, x => x.Value / sum, StringComparer.Ordinal);
                else
                    unavailable = "No usable positive model weights.";
            }
        }

        var statuses = request.Models.Select(m =>
            new FusionModelStatus(m.ModelId, state[m.ModelId].Status,
                state[m.ModelId].Reason + (audit.TryGetValue(m.ModelId, out var detail) ? " " + detail : ""),
                weights.GetValueOrDefault(m.ModelId), m.IsSimulation)).ToArray();

        FusionResult Abstain(string reason, IReadOnlyDictionary<string, double>? probabilities = null, string? choice = null, double? confidence = null) =>
            new(request.RequestId, strategy, "ABSTAIN", choice, probabilities, confidence, statuses,
                reason, valid.Any(m => m.IsSimulation), "Model-reported probabilities, not empirically calibrated success rates.",
                "Evidence is caller-supplied; its validity and independence must be audited externally.");

        if (valid.Count == 0) return Abstain("All models failed or supplied invalid predictions.");
        if (unavailable is not null) return Abstain(unavailable);

        var fused = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var option in request.Options)
            fused.Add(option, valid.Sum(m => weights.GetValueOrDefault(m.ModelId) * m.Probabilities![option]));
        double total = fused.Values.Sum();
        if (total <= 0 || !double.IsFinite(total)) return Abstain("Numerically invalid fused probabilities.");
        foreach (var option in request.Options) fused[option] /= total;
        string choice = request.Options.Aggregate((best, next) => fused[next] > fused[best] ? next : best);
        double confidence = fused[choice];

        if (statuses.Any(s => s.Status != "success"))
            return Abstain("At least one requested model failed or was invalid; partial ensemble is not auto-approved.", fused, choice, confidence);
        if (valid.Any(m => m.IsSimulation) && !risk.AllowSimulatedExecution)
            return Abstain("Simulated model output cannot authorize execution by default.", fused, choice, confidence);
        if (confidence < risk.MinConfidence)
            return Abstain("Fused confidence is below the configured threshold; confidence is not validated accuracy.", fused, choice, confidence);

        var proof = request.StrategyEvidence;
        if (proof is null ||
            !string.Equals(proof.Strategy, strategy, StringComparison.OrdinalIgnoreCase) ||
            proof.TaskType != request.TaskType || proof.Segment != request.Segment ||
            !string.Equals(proof.Split, "validation", StringComparison.OrdinalIgnoreCase) ||
            proof.SampleCount < risk.MinValidationSamples ||
            !double.IsFinite(proof.LowerBrierSkillBound) ||
            proof.LowerBrierSkillBound <= risk.MinValidatedSkillLowerBound ||
            proof.LowerBrierSkillBound > 1 ||
            proof.ValidationEndedAt == default || proof.ValidationEndedAt >= decisionAt ||
            proof.ModelIds is null || proof.ModelIds.Count != valid.Count ||
            !proof.ModelIds.ToHashSet(StringComparer.Ordinal).SetEquals(valid.Select(x => x.ModelId)) ||
            proof.FrozenWeights is null || proof.FrozenWeights.Count != weights.Count ||
            weights.Any(kv => !proof.FrozenWeights.TryGetValue(kv.Key, out double v) ||
                !double.IsFinite(v) || Math.Abs(kv.Value - v) > 1e-9))
            return Abstain("No eligible past validation evidence of positive advantage for this full fusion strategy.", fused, choice, confidence);

        if (risk.MinExpectedBenefit is not null &&
            (request.ExpectedBenefit is null || request.ExpectedBenefit < risk.MinExpectedBenefit))
            return Abstain("Expected benefit does not meet the configured business gate.", fused, choice, confidence);
        if (risk.MaxPotentialLoss is not null &&
            (request.PotentialLoss is null || request.PotentialLoss > risk.MaxPotentialLoss))
            return Abstain("Potential loss breaches the configured business gate.", fused, choice, confidence);
        if (risk.MaxExposure is not null &&
            (request.Exposure is null || request.Exposure > risk.MaxExposure))
            return Abstain("Exposure breaches the configured business gate.", fused, choice, confidence);

        return new(request.RequestId, strategy, "EXECUTE", choice, fused, confidence, statuses,
            "All configured model, validation and risk gates passed (not a guarantee of profit).",
            valid.Any(m => m.IsSimulation),
            "Model-reported probabilities, not empirically calibrated success rates.",
            "Evidence is caller-supplied; independence, time provenance and claimed skill require external audit.");
    }

    private static FusionValidationEvidence? FindEvidence(
        FusionRequest request, string modelId, string strategy, DateTimeOffset at, int minSamples)
    {
        var rows = request.Validation?.Where(e => e is not null && e.ModelId == modelId &&
            e.TaskType == request.TaskType && (strategy == "dynamic" ? e.Segment == request.Segment : e.Segment is null)).ToArray();
        if (rows is not { Length: 1 }) return null;
        var e = rows[0];
        if (!string.Equals(e.Split, "validation", StringComparison.OrdinalIgnoreCase) ||
            e.ValidationEndedAt == default || e.ValidationEndedAt >= at ||
            e.SampleCount < minSamples ||
            !double.IsFinite(e.BrierLoss) || !double.IsFinite(e.BaselineBrierLoss) ||
            e.BrierLoss < 0 || e.BaselineBrierLoss <= 0 ||
            !double.IsFinite(e.LowerSkillBound) ||
            e.LowerSkillBound <= 0 || e.LowerSkillBound > 1)
            return null;
        return e;
    }

    private static void ValidateRisk(FusionRiskPolicy policy, FusionRequest request)
    {
        if (!double.IsFinite(policy.MinConfidence) || policy.MinConfidence is < 0 or > 1 ||
            !double.IsFinite(policy.MinValidatedSkillLowerBound) ||
            policy.MinValidatedSkillLowerBound is < 0 or >= 1 ||
            policy.MinValidationSamples is < 1 or > 100000)
            throw new ArgumentException("Risk confidence, skill and sample limits are invalid.");
        static void Check(double? value, string name)
        {
            if (value is not null && (!double.IsFinite(value.Value) || value.Value < 0))
                throw new ArgumentException(name + " must be finite and nonnegative.");
        }
        Check(policy.MinExpectedBenefit, nameof(policy.MinExpectedBenefit));
        Check(policy.MaxPotentialLoss, nameof(policy.MaxPotentialLoss));
        Check(policy.MaxExposure, nameof(policy.MaxExposure));
        Check(request.ExpectedBenefit, nameof(request.ExpectedBenefit));
        Check(request.PotentialLoss, nameof(request.PotentialLoss));
        Check(request.Exposure, nameof(request.Exposure));
    }
}

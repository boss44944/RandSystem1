using RandSystem1.Core.Validation;

namespace RandSystem1.Core.Random;

/// <summary>
/// A reproducible *simulation*. Neither these probabilities nor sampled choices
/// measure a real model's confidence or correctness.
/// </summary>
public sealed record BaselineRunRequest(
    string Baseline,
    IReadOnlyList<string> Options,
    int Seed = 42,
    int Count = 1,
    int SamplesPerModel = 1,
    double Alpha = 1.0,
    IReadOnlyList<string>? TrainingLabels = null);

public sealed record BaselineDraw(
    int SampleIndex,
    IReadOnlyDictionary<string, double> Probabilities,
    string SampledOption);

public sealed record BaselineMember(
    int ModelIndex,
    string ModelId,
    string SeedHex,
    IReadOnlyList<BaselineDraw> Draws);

public sealed record BaselineRunResult(
    string Baseline,
    string EngineVersion,
    int Seed,
    int Count,
    int SamplesPerModel,
    double? Alpha,
    int? TrainingLabelCount,
    bool IsSimulation,
    string ProbabilityMeaning,
    IReadOnlyList<BaselineMember> Members);

/// <summary>
/// Fixed internal SplitMix64 PRNG: seed replay does not depend on System.Random's
/// implementation. NOT suitable for cryptography or security-sensitive draws.
/// </summary>
internal sealed class StableRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextUInt64()
    {
        _state = unchecked(_state + 0x9E3779B97F4A7C15UL);
        ulong z = _state;
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    public double NextNormal()
    {
        double a = 1.0 - NextDouble();
        double b = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2 * Math.PI * b);
    }

    /// <summary>Log-Gamma sampler; log space avoids underflow for small alpha.</summary>
    public double NextLogGamma(double alpha)
    {
        if (alpha < 1.0)
            return NextLogGamma(alpha + 1.0) + Math.Log(1.0 - NextDouble()) / alpha;

        double d = alpha - 1.0 / 3.0;
        double c = 1.0 / Math.Sqrt(9.0 * d);
        while (true)
        {
            double x = NextNormal();
            double v = 1.0 + c * x;
            if (v <= 0) continue;
            v = v * v * v;
            double u = 1.0 - NextDouble();
            if (u < 1.0 - 0.0331 * x * x * x * x ||
                Math.Log(u) < 0.5 * x * x + d * (1.0 - v + Math.Log(v)))
                return Math.Log(d) + Math.Log(v);
        }
    }
}

public static class BaselineEngine
{
    public const string Version = "0.2.0";
    public const string ProbabilityMeaning = "baseline-design-probability-not-calibrated-model-confidence";
    private const int MaxModels = 200;
    private const int MaxSamplesPerModel = 2000;
    private const int MaxTotalDraws = 20000;
    private const int MaxOptions = 256;

    /// <summary>A training-only distribution. Any held-out labels must never be supplied.</summary>
    public static IReadOnlyDictionary<string, double> FitPrior(
        IReadOnlyList<string> options,
        IReadOnlyList<string> trainingLabels)
    {
        ValidateOptions(options);
        if (trainingLabels is null || trainingLabels.Count == 0)
            throw new ArgumentException("Nonempty training labels are required.", nameof(trainingLabels));
        var counts = options.ToDictionary(x => x, _ => 0L, StringComparer.Ordinal);
        foreach (string label in trainingLabels)
        {
            if (label is null || !counts.ContainsKey(label))
                throw new ArgumentException("Training label is not one of the options.", nameof(trainingLabels));
            counts[label]++;
        }
        var values = counts.Values.Select(x => (double)x / trainingLabels.Count).ToArray();
        return Distribution(options, values);
    }

    public static IReadOnlyDictionary<string, double> FitMajority(
        IReadOnlyList<string> options,
        IReadOnlyList<string> trainingLabels)
    {
        var priors = FitPrior(options, trainingLabels);
        // Ties are resolved using the caller-supplied option order.
        string winner = options[0];
        foreach (var option in options.Skip(1))
            if (priors[option] > priors[winner]) winner = option;
        return options.ToDictionary(x => x, x => x == winner ? 1.0 : 0.0, StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, double> Uniform(IReadOnlyList<string> options)
    {
        ValidateOptions(options);
        return Distribution(options, Enumerable.Repeat(1.0 / options.Count, options.Count).ToArray());
    }

    public static BaselineRunResult Run(BaselineRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOptions(request.Options);
        if (request.Count is < 1 or > MaxModels)
            throw new ArgumentException($"Count must be within [1,{MaxModels}].", nameof(request));
        if (request.SamplesPerModel is < 1 or > MaxSamplesPerModel ||
            (long)request.Count * request.SamplesPerModel > MaxTotalDraws)
            throw new ArgumentException("Batch size exceeds supported simulation limits.", nameof(request));

        string baseline = request.Baseline?.Trim().ToLowerInvariant() ?? "";
        if (baseline is not ("uniform" or "dirichlet" or "majority" or "prior"))
            throw new ArgumentException("Baseline must be uniform, dirichlet, majority or prior.", nameof(request));
        if (baseline == "dirichlet" && (!double.IsFinite(request.Alpha) || request.Alpha <= 0))
            throw new ArgumentException("Dirichlet alpha must be positive and finite.", nameof(request));
        if (baseline is "uniform" or "dirichlet" && request.TrainingLabels is not null)
            throw new ArgumentException("Random baselines do not accept training labels.", nameof(request));
        if (baseline is "majority" or "prior" && request.TrainingLabels is null)
            throw new ArgumentException("Training labels are required for fitted baselines.", nameof(request));
        if (request.TrainingLabels is { Count: > 100000 })
            throw new ArgumentException("Training label count exceeds 100000.", nameof(request));

        IReadOnlyDictionary<string, double>? fitted = baseline switch
        {
            "majority" => FitMajority(request.Options, request.TrainingLabels!),
            "prior" => FitPrior(request.Options, request.TrainingLabels!),
            _ => null
        };
        var fixedUniform = baseline == "uniform" ? Uniform(request.Options) : null;
        var root = new StableRandom(unchecked((ulong)(long)request.Seed));
        var members = new List<BaselineMember>(request.Count);
        for (var index = 0; index < request.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            ulong seed = root.NextUInt64();
            var rng = new StableRandom(seed);
            var draws = new List<BaselineDraw>(request.SamplesPerModel);
            for (int j = 0; j < request.SamplesPerModel; j++)
            {
                ct.ThrowIfCancellationRequested();
                IReadOnlyDictionary<string, double> probabilities = baseline switch
                {
                    "uniform" => fixedUniform!,
                    "dirichlet" => DrawDirichlet(request.Options, request.Alpha, rng),
                    "majority" or "prior" => fitted!,
                    _ => throw new InvalidOperationException("Unreachable baseline.")
                };
                string sampled = baseline == "majority"
                    ? request.Options.First(x => probabilities[x] == 1.0)
                    : DrawCategorical(request.Options, probabilities, rng);
                draws.Add(new BaselineDraw(j, probabilities, sampled));
            }
            members.Add(new BaselineMember(index, $"random-{baseline}-{index + 1:D3}",
                $"0x{seed:X16}", draws));
        }
        return new BaselineRunResult(
            baseline, Version, request.Seed, request.Count, request.SamplesPerModel,
            baseline == "dirichlet" ? request.Alpha : null,
            fitted is not null ? request.TrainingLabels!.Count : null,
            true, ProbabilityMeaning, members);
    }

    private static void ValidateOptions(IReadOnlyList<string>? options)
    {
        if (options is null || options.Count is < 1 or > MaxOptions ||
            options.Any(string.IsNullOrWhiteSpace) ||
            options.Distinct(StringComparer.Ordinal).Count() != options.Count)
            throw new ArgumentException($"Options must contain 1-{MaxOptions} unique nonempty labels.", nameof(options));
    }

    private static IReadOnlyDictionary<string, double> DrawDirichlet(
        IReadOnlyList<string> options, double alpha, StableRandom rng)
    {
        var logs = new double[options.Count];
        for (int i = 0; i < logs.Length; i++) logs[i] = rng.NextLogGamma(alpha);
        var max = logs.Max();
        // For subnormal alpha every log Gamma can underflow to -Infinity.
        // The alpha->0 limit places all probability mass on a single option.
        if (double.IsNegativeInfinity(max))
        {
            var probabilities = new double[options.Count];
            probabilities[(int)(rng.NextDouble() * options.Count)] = 1;
            return Distribution(options, probabilities);
        }
        var weights = logs.Select(x => Math.Exp(x - max)).ToArray();
        var sum = weights.Sum();
        return Distribution(options, weights.Select(x => x / sum).ToArray());
    }

    private static IReadOnlyDictionary<string, double> Distribution(
        IReadOnlyList<string> options, double[] values)
    {
        var result = new Dictionary<string, double>(options.Count, StringComparer.Ordinal);
        double prefix = 0.0;
        for (int i = 0; i < options.Count; i++)
        {
            // Final residual makes sum equal to one (up to floating point rounding).
            double value = i == options.Count - 1 ? Math.Max(0.0, 1.0 - prefix) : values[i];
            result.Add(options[i], value);
            prefix += value;
        }
        return result;
    }

    private static string DrawCategorical(
        IReadOnlyList<string> options,
        IReadOnlyDictionary<string, double> probabilities,
        StableRandom rng)
    {
        double threshold = rng.NextDouble();
        double sum = 0.0;
        foreach (string option in options)
        {
            sum += probabilities[option];
            if (threshold < sum) return option;
        }
        // Handle the last possible floating point rounding boundary.
        return options[^1];
    }
}

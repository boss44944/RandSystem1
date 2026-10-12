using RandSystem1.Core.Random;

namespace RandSystem1.Core.Statistics;

public sealed record MonteCarloRequest(
    IReadOnlyList<string> Options,
    IReadOnlyList<string> TestLabels,
    string Baseline = "uniform",
    int Iterations = 1000,
    int Seed = 42,
    double Alpha = 1.0,
    IReadOnlyList<string>? TrainingLabels = null,
    IReadOnlyList<string>? ObservedPredictions = null);

public sealed record MonteCarloReport(
    string Baseline, string EngineVersion, int Seed,
    int Iterations, int TestCount, int? TrainingCount,
    double MeanAccuracy, double StandardDeviation,
    double MeanConfidenceLower95, double MeanConfidenceUpper95,
    double NullPercentile05, double NullMedian,
    double NullPercentile95, double BestObservedRandomAccuracy,
    double? ModelObservedAccuracy, double? EmpiricalUpperTailP,
    IReadOnlyList<double> RandomAccuracies,
    bool IsSimulation, string Explanation);

public static class MonteCarloEvaluation
{
    public static MonteCarloReport Run(MonteCarloRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClassificationMetrics.ValidateOptions(request.Options);
        if (request.TestLabels is null || request.TestLabels.Count is < 1 or > 2000 ||
            (long)request.TestLabels.Count * request.Iterations > 1_000_000 ||
            request.Iterations is < 20 or > 10000)
            throw new ArgumentException(
                "Monte Carlo requires 20..10000 iterations and 1..2000 tests; at most one million draws.");
        var allowed = request.Options.ToHashSet(StringComparer.Ordinal);
        if (request.TestLabels.Any(x => !allowed.Contains(x)))
            throw new ArgumentException("Test labels must be in the candidate set.");
        if (request.ObservedPredictions is not null &&
            (request.ObservedPredictions.Count != request.TestLabels.Count ||
             request.ObservedPredictions.Any(x => !allowed.Contains(x))))
            throw new ArgumentException("Observed model choices must align one-to-one with all test labels.");
        string baseline = request.Baseline?.Trim().ToLowerInvariant() ?? "";
        if (baseline is not ("uniform" or "dirichlet" or "majority" or "prior"))
            throw new ArgumentException("Unsupported random baseline.");
        if (baseline is "majority" or "prior" && request.TrainingLabels is null)
            throw new ArgumentException("Fitted baselines require training-only labels.");
        if (baseline is "uniform" or "dirichlet" && request.TrainingLabels is not null)
            throw new ArgumentException("Random-only baselines do not accept training labels.");

        double? observedAccuracy = request.ObservedPredictions is null ? null :
            (double)Enumerable.Range(0, request.TestLabels.Count)
                .Count(i => request.TestLabels[i] == request.ObservedPredictions[i]) /
            request.TestLabels.Count;

        // Chunk the engine: 200 independent model draws per batch max, 20000 total draws.
        // A fixed SplitMix64 schedule derives all chunk seeds; replay does not rely on
        // a framework-global RNG. Each run uses the same fixed held-out questions.
        var seedSchedule = new StableRandom(unchecked((ulong)(long)request.Seed));
        var accuracies = new List<double>(request.Iterations);
        while (accuracies.Count < request.Iterations)
        {
            ct.ThrowIfCancellationRequested();
            int count = Math.Min(request.Iterations - accuracies.Count,
                Math.Min(200, 20000 / request.TestLabels.Count));
            int derivedSeed = unchecked((int)seedSchedule.NextUInt64());
            var batch = BaselineEngine.Run(new BaselineRunRequest(
                baseline, request.Options, derivedSeed,
                Count: count, SamplesPerModel: request.TestLabels.Count,
                Alpha: request.Alpha, TrainingLabels: request.TrainingLabels), ct);
            foreach (var member in batch.Members)
            {
                int correct = 0;
                for (int i = 0; i < member.Draws.Count; i++)
                    if (member.Draws[i].SampledOption == request.TestLabels[i]) correct++;
                accuracies.Add((double)correct / request.TestLabels.Count);
            }
        }

        double mean = accuracies.Average();
        double variance = accuracies.Sum(x => Math.Pow(x - mean, 2)) / (accuracies.Count - 1);
        double sd = Math.Sqrt(variance);
        double margin = 1.96 * sd / Math.Sqrt(request.Iterations);
        var sorted = accuracies.OrderBy(x => x).ToArray();
        // Add-one Monte Carlo tail correction, not a theoretical model p-value.
        double? empirical = observedAccuracy is null ? null :
            (double)(accuracies.Count(x => x >= observedAccuracy.Value - 1e-12) + 1) /
            (accuracies.Count + 1);

        return new MonteCarloReport(
            baseline, BaselineEngine.Version, request.Seed, request.Iterations,
            request.TestLabels.Count, request.TrainingLabels?.Count,
            mean, sd, Math.Max(0, mean - margin), Math.Min(1, mean + margin),
            StatisticalInference.Percentile(sorted, 0.05),
            StatisticalInference.Percentile(sorted, 0.50),
            StatisticalInference.Percentile(sorted, 0.95),
            sorted[^1], observedAccuracy, empirical,
            accuracies, true,
            "All simulated members use the same test labels; random samples are independent " +
            "across seeded members, not independent observations in time. The best observed random " +
            "run is a selection extreme, not the expected random performance. Percentiles describe " +
            "the random null distribution; mean confidence interval summarizes Monte Carlo error " +
            "conditional on this fixed dataset. Empirical upper-tail P uses an add-one correction " +
            "and is exploratory if models, split or baselines were chosen using test results. " +
            "No confidence interval here establishes population generalization or real AI calibration.");
    }
}

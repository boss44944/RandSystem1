using RandSystem1.Core.Random;

namespace RandSystem1.Core.Statistics;

public sealed record PairedComparisonRequest(
    IReadOnlyList<string> Options,
    IReadOnlyList<ScoredObservation> Model,
    IReadOnlyList<ScoredObservation> Baseline,
    string Loss = "brier",
    string SampleStructure = "unspecified",
    int BlockLength = 1,
    int Replications = 2000,
    int Seed = 42,
    double ConfidenceLevel = 0.95,
    int Comparisons = 1,
    bool ChronologicalOrderConfirmed = false);

public sealed record PairedComparisonReport(
    int Count, string Loss, string Method, string SampleStructure,
    double ModelMeanLoss, double BaselineMeanLoss, double Improvement,
    double LowerConfidenceBound, double UpperConfidenceBound,
    double ConfidenceLevel, double? TwoSidedPermutationP,
    double? BonferroniAdjustedP, int Comparisons,
    string Interpretation);

public sealed record ExactBinomialRequest(
    int Successes, int Trials, double FixedNullSuccessProbability,
    bool IndependentTrialsConfirmed);

public sealed record ExactBinomialReport(
    int Successes, int Trials, double NullProbability, double ObservedRate,
    double OneSidedUpperTailP, string Assumptions);

public static class StatisticalInference
{
    public static PairedComparisonReport Compare(PairedComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ClassificationMetrics.ValidateObservations(request.Options, request.Model);
        ClassificationMetrics.ValidateObservations(request.Options, request.Baseline);
        if (request.Model.Count != request.Baseline.Count ||
            request.Model.Where((o, i) =>
                o.ObservationId != request.Baseline[i].ObservationId ||
                o.Actual != request.Baseline[i].Actual).Any())
            throw new ArgumentException("Paired observations must align exactly on ID, order and ground truth.");
        string loss = request.Loss?.Trim().ToLowerInvariant() ?? "";
        if (loss is not ("brier" or "logloss"))
            throw new ArgumentException("Loss must be brier or logloss.");
        string structure = request.SampleStructure?.Trim().ToLowerInvariant() ?? "";
        if (structure is not ("iid" or "timeseries"))
            throw new ArgumentException("Explicit SampleStructure must be iid or timeseries; do not silently assume independence.");
        if (request.Replications is < 200 or > 20000)
            throw new ArgumentException("Replications must be 200..20000.");
        if (!double.IsFinite(request.ConfidenceLevel) ||
            request.ConfidenceLevel <= 0 || request.ConfidenceLevel >= 1)
            throw new ArgumentException("Confidence level must be between zero and one.");
        if (request.Comparisons is < 1 or > 10000)
            throw new ArgumentException("Comparisons must be 1..10000.");
        if (structure == "timeseries" &&
            (!request.ChronologicalOrderConfirmed || request.BlockLength < 2 ||
             request.BlockLength > request.Model.Count))
            throw new ArgumentException(
                "Time-series comparison requires chronological order confirmation and block length between 2 and sample count.");
        if (structure == "iid" && request.BlockLength != 1)
            throw new ArgumentException("IID comparison uses block length 1.");

        int n = request.Model.Count;
        Func<ScoredObservation, double> score = loss == "brier"
            ? x => ClassificationMetrics.BrierLoss(request.Options, x)
            : ClassificationMetrics.CrossEntropy;
        var modelLoss = request.Model.Select(score).ToArray();
        var baseLoss = request.Baseline.Select(score).ToArray();
        var differences = Enumerable.Range(0, n)
            .Select(i => baseLoss[i] - modelLoss[i]).ToArray();

        double improvement = differences.Average();
        var rng = new StableRandom(unchecked((ulong)(long)request.Seed));
        var bootstrap = new double[request.Replications];
        for (int b = 0; b < bootstrap.Length; b++)
        {
            double total = 0;
            if (structure == "iid")
            {
                for (int j = 0; j < n; j++) total += differences[Index(rng, n)];
            }
            else
            {
                // Circular moving-block bootstrap retains short-range serial dependence.
                for (int j = 0; j < n;)
                {
                    int start = Index(rng, n);
                    for (int k = 0; k < request.BlockLength && j < n; k++, j++)
                        total += differences[(start + k) % n];
                }
            }
            bootstrap[b] = total / n;
        }
        Array.Sort(bootstrap);
        double alpha = (1 - request.ConfidenceLevel) / 2;
        double lower = Percentile(bootstrap, alpha);
        double upper = Percentile(bootstrap, 1 - alpha);

        double? permutationP = null;
        double? correctedP = null;
        if (structure == "iid")
        {
            // Under the exchangeability null, swap paired model/baseline losses.
            // Exact for n <= 15; otherwise a seeded Monte Carlo randomization test.
            int extreme = 0;
            int permutations;
            if (n <= 15)
            {
                permutations = 1 << n;
                for (int mask = 0; mask < permutations; mask++)
                {
                    double total = 0;
                    for (int i = 0; i < n; i++)
                        total += ((mask & (1 << i)) == 0 ? 1 : -1) * differences[i];
                    if (Math.Abs(total / n) >= Math.Abs(improvement) - 1e-12) extreme++;
                }
                permutationP = (double)extreme / permutations;
            }
            else
            {
                permutations = request.Replications;
                for (int b = 0; b < permutations; b++)
                {
                    double total = 0;
                    for (int i = 0; i < n; i++)
                        total += ((rng.NextUInt64() & 1) == 0 ? 1 : -1) * differences[i];
                    if (Math.Abs(total / n) >= Math.Abs(improvement) - 1e-12) extreme++;
                }
                permutationP = (double)(extreme + 1) / (permutations + 1);
            }
            correctedP = Math.Min(1, permutationP.Value * request.Comparisons);
        }

        return new PairedComparisonReport(
            n, loss, structure == "iid" ? "paired-bootstrap-and-label-swap-permutation" : "circular-moving-block-bootstrap",
            structure, modelLoss.Average(), baseLoss.Average(), improvement,
            lower, upper, request.ConfidenceLevel, permutationP, correctedP,
            request.Comparisons,
            structure == "iid"
                ? "Positive improvement means lower model loss; paired permutation p requires " +
                  "exchangeability of model labels under the null and independent observation pairs. " +
                  "Bonferroni adjusts for the supplied total number of comparisons."
                : "Bootstrap blocks approximate within-series dependence. No IID permutation p-value " +
                  "is reported for correlated time-series. Block length is analyst-specified; " +
                  "sample order must be chronological and free from leakage. CI is approximate.");
    }

    public static ExactBinomialReport ExactBinomialUpperTail(ExactBinomialRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IndependentTrialsConfirmed)
            throw new ArgumentException("Exact binomial inference requires explicit IID Bernoulli null assumptions.");
        if (request.Trials is < 1 or > 100000 || request.Successes < 0 ||
            request.Successes > request.Trials)
            throw new ArgumentException("Invalid success/trial counts.");
        double p = request.FixedNullSuccessProbability;
        if (!double.IsFinite(p) || p < 0 || p > 1)
            throw new ArgumentException("Fixed null probability must be within [0,1].");
        double tail;
        if (request.Successes == 0) tail = 1;
        else if (p == 0) tail = 0;
        else if (p == 1) tail = 1;
        else
        {
            // Compute all upper-tail terms in log space: avoids overflow in binomial coefficients.
            double[] logFactorial = new double[request.Trials + 1];
            for (int i = 2; i < logFactorial.Length; i++)
                logFactorial[i] = logFactorial[i - 1] + Math.Log(i);
            var terms = new double[request.Trials - request.Successes + 1];
            double max = double.NegativeInfinity;
            for (int k = request.Successes; k <= request.Trials; k++)
            {
                double logTerm = logFactorial[request.Trials] -
                    logFactorial[k] - logFactorial[request.Trials - k] +
                    k * Math.Log(p) + (request.Trials - k) * Math.Log(1 - p);
                terms[k - request.Successes] = logTerm;
                max = Math.Max(max, logTerm);
            }
            tail = Math.Min(1, Math.Exp(max) * terms.Sum(term => Math.Exp(term - max)));
        }
        return new ExactBinomialReport(
            request.Successes, request.Trials, p,
            (double)request.Successes / request.Trials, tail,
            "One-sided P(X >= successes) under independently and identically distributed " +
            "Bernoulli trials with known fixed success probability. Not valid for correlated " +
            "time-series, heterogeneous per-observation chance, or selected best-of-many models.");
    }

    internal static int Index(StableRandom rng, int count) =>
        Math.Min((int)(rng.NextDouble() * count), count - 1);

    internal static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0 || p is < 0 or > 1 || !double.IsFinite(p))
            throw new ArgumentException("Percentile requires nonempty sorted data and p in [0,1].");
        double rank = p * (sorted.Count - 1);
        int lower = (int)Math.Floor(rank);
        if (lower == sorted.Count - 1) return sorted[lower];
        double weight = rank - lower;
        return sorted[lower] * (1 - weight) + sorted[lower + 1] * weight;
    }
}

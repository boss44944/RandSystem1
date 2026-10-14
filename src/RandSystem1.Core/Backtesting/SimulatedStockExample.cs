using RandSystem1.Core.Random;

namespace RandSystem1.Core.Backtesting;

/// <summary>Fixed single-symbol, 5-session synthetic UP/DOWN demonstration.
/// Not a source of real market prices, actual Jev/Laya predictions or evidence of model skill.</summary>
public sealed record StockExampleRequest(
    int Seed = 42,
    double InitialCapital = 10000,
    double FeeRate = 0.001,
    double SlippageRate = 0.001,
    double MaxAbsolutePosition = 1);

public sealed record StockExampleObservation(
    string ObservationId, string Symbol, DateTimeOffset SignalAt, DateTimeOffset LabelEndAt,
    double EntryPrice, double ExitPrice, string TrueLabel, double? Momentum5,
    double? Momentum15, string ModelVersion, string SinglePrediction,
    string SecondPrediction, string FusionPrediction, bool IsHeldOut);

public sealed record StockAccuracy(
    string StrategyId, int Cases, int Predictions, int Correct, int Abstentions,
    double? Accuracy, double Coverage);

public sealed record StockStrategySummary(
    string StrategyId, string Kind, int TestCases, int Predictions, int Correct,
    double? Accuracy, double Coverage, double? AccuracyWilsonLower95,
    double? AccuracyWilsonUpper95, double FinalCapital, double CumulativeReturn,
    double MaxDrawdown, double? AnnualizedReturn, double? AnnualizedSharpe,
    int Trades, double Turnover, double TotalCostsPaid, double MeanAbsoluteExposure,
    IReadOnlyList<BacktestPoint> EquityCurve);

public sealed record StockExampleReport(
    string ExampleVersion, string DatasetVersion, string DatasetHashSha256,
    int Seed, bool IsSimulation, string Instrument, string PredictionTask,
    int Periods, int SessionsPerPeriod, DateTimeOffset From, DateTimeOffset Until,
    int TestPeriods, WalkForwardConfiguration Window, BacktestCostPolicy Costs,
    IReadOnlyDictionary<string, string> ModelVersions,
    IReadOnlyList<StockExampleObservation> Observations,
    IReadOnlyList<WalkForwardWindow> Windows,
    IReadOnlyList<StockStrategySummary> Comparison,
    string Explanation);

public static class SimulatedStockExample
{
    public const string Version = "0.7.0";
    public const string Symbol = "SYNTHETIC-ONE";
    public const int PeriodCount = 32;
    public const int SessionsPerPeriod = 5;
    private static readonly DateTimeOffset Start = new(2024, 1, 2, 16, 0, 0, TimeSpan.Zero);
    public static readonly WalkForwardConfiguration FixedWindow = new(8, 3, 3, 3, 1);

    private static bool IsWeekend(DateTimeOffset day) =>
        day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static IReadOnlyList<DateTimeOffset> TradingSessions()
    {
        var dates = new List<DateTimeOffset>();
        var date = Start;
        while (dates.Count <= PeriodCount * SessionsPerPeriod)
        {
            if (!IsWeekend(date)) dates.Add(date);
            date = date.AddDays(1);
        }
        return dates;
    }

    /// <summary>Deterministic rules using only closes at indices &lt;= asOfIndex.</summary>
    public static (string Single, string Second, string Fusion, double? Momentum5, double? Momentum15)
        PredictAt(IReadOnlyList<double> closes, int asOfIndex)
    {
        ArgumentNullException.ThrowIfNull(closes);
        if (asOfIndex < 0 || asOfIndex >= closes.Count)
            throw new ArgumentException("Invalid observation cutoff.");
        for (int i = Math.Max(0, asOfIndex - 15); i <= asOfIndex; i++)
            if (!double.IsFinite(closes[i]) || closes[i] <= 0)
                throw new ArgumentException("Historical prices must be finite and positive.");

        double? ret5 = asOfIndex >= 5 ? closes[asOfIndex] / closes[asOfIndex - 5] - 1 : null;
        double? ret15 = asOfIndex >= 15 ? closes[asOfIndex] / closes[asOfIndex - 15] - 1 : null;
        string first = ret5 is double x ? (x >= 0 ? "UP" : "DOWN") : "ABSTAIN";
        string second = ret15 is double y ? (y >= 0 ? "UP" : "DOWN") : "ABSTAIN";
        // Two identical binary votes required; disagreement is not resolved
        // with future labels. This fusion is deliberately simplistic.
        string fusion = first == second ? first : "ABSTAIN";
        return (first, second, fusion, ret5, ret15);
    }

    public static StockExampleReport Run(StockExampleRequest? request = null, CancellationToken ct = default)
    {
        request ??= new();
        if (!double.IsFinite(request.InitialCapital) || request.InitialCapital <= 0 ||
            !double.IsFinite(request.FeeRate) || request.FeeRate is < 0 or > 0.5 ||
            !double.IsFinite(request.SlippageRate) || request.SlippageRate is < 0 or > 0.5 ||
            !double.IsFinite(request.MaxAbsolutePosition) || request.MaxAbsolutePosition is <= 0 or > 1)
            throw new ArgumentException("Invalid example capital, costs or bounded exposure.");

        // Generate the *entire* fixed synthetic universe and period once, with a
        // fixed split configuration. No security/window is chosen by test results.
        var rng = new StableRandom(unchecked((ulong)(uint)request.Seed));
        var dates = TradingSessions();
        var closes = new double[dates.Count];
        closes[0] = 100;
        for (int i = 1; i < closes.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            // Artificial daily signed movement, deterministic for seed.
            double movement = .0006 + (rng.NextDouble() - 0.5) * .032;
            closes[i] = closes[i - 1] * (1 + movement);
        }
        const string model1 = "mock-momentum5-v1";
        const string model2 = "mock-momentum15-v1";
        const string fusion = "mock-unanimous-v1";
        const string versionPrefix = "synth-5session-fixed32-v1-seed-";
        string dataVersion = versionPrefix + request.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var bars = new List<BacktestBar>();
        var predictions = new List<(string First, string Second, string Fused, double? Mom5, double? Mom15)>();
        for (int p = 0; p < PeriodCount; p++)
        {
            ct.ThrowIfCancellationRequested();
            int start = p * SessionsPerPeriod;
            int end = start + SessionsPerPeriod;
            // Execute at today's synthetic close; the signal was decided using
            // *yesterday's* close. Thus even the execution-period closing
            // price cannot leak into the predictions.
            var pred = start == 0
                ? (Single: "ABSTAIN", Second: "ABSTAIN", Fusion: "ABSTAIN",
                    Momentum5: (double?)null, Momentum15: (double?)null)
                : PredictAt(closes.AsSpan(0, start).ToArray(), start - 1);
            predictions.Add(pred);
            double Position(string label) => label == "ABSTAIN" ? 0 : label == "UP" ? request.MaxAbsolutePosition : -request.MaxAbsolutePosition;
            var signalTime = dates[start];
            var evidenceAt = start == 0 ? signalTime.AddDays(-1) : dates[start - 1];
            var signals = new List<BacktestSignal>
            {
                new("mock-single", Position(pred.Single), evidenceAt, evidenceAt),
                new("mock-second", Position(pred.Second), evidenceAt, evidenceAt),
                new("mock-fusion", Position(pred.Fusion), evidenceAt, evidenceAt)
            };
            bars.Add(new($"fixed-{p:D2}", signalTime, dates[end], closes[start], closes[end], signals));
        }

        var policy = new BacktestCostPolicy(request.InitialCapital, request.FeeRate, request.SlippageRate, request.MaxAbsolutePosition);
        var strategies = new BacktestStrategy[]
        {
            new("mock-single", "single"), new("mock-second", "single"),
            new("mock-fusion", "fusion"), new("random-seeded", "random", request.Seed),
            new("buy-hold", "buy-hold"), new("no-trade", "abstain")
        };
        BacktestReport underlying = WalkForwardBacktester.Run(
            new BacktestRequest(bars, strategies, FixedWindow, policy, true, dataVersion), ct);
        var testSet = underlying.Windows.SelectMany(w => w.TestIds).ToHashSet(StringComparer.Ordinal);
        var observations = new List<StockExampleObservation>();
        for (int p = 0; p < bars.Count; p++)
        {
            var b = bars[p];
            var pred = predictions[p];
            observations.Add(new(b.ObservationId, Symbol, b.SignalAt, b.LabelEndAt,
                b.EntryPrice, b.ExitPrice,
                b.ExitPrice > b.EntryPrice ? "UP" : "DOWN", pred.Mom5, pred.Mom15,
                $"{model1}/{model2}", pred.First, pred.Second, pred.Fused,
                testSet.Contains(b.ObservationId)));
        }

        // SHA256 of deterministic full input series identifies the exact
        // seeded synthetic observation path; not a real-data provenance seal.
        using var stream = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            foreach (double close in closes) writer.Write(close);
            writer.Flush();
        }
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray())).ToLowerInvariant();

        var byId = observations.ToDictionary(o => o.ObservationId, StringComparer.Ordinal);
        var reports = new List<StockStrategySummary>();
        foreach (var strategy in underlying.Strategies)
        {
            var cases = strategy.Curve.Count;
            int attempts = 0, correct = 0;
            double meanExposure = 0;
            foreach (var pt in strategy.Curve)
            {
                ct.ThrowIfCancellationRequested();
                meanExposure += Math.Abs(pt.Position);
                if (pt.Position == 0) continue;
                attempts++;
                bool up = byId[pt.ObservationId].TrueLabel == "UP";
                if (up == (pt.Position > 0)) correct++;
            }
            // No Trade / ABSTAIN has no accuracy (0 denominators are never
            // presented as "0% accuracy"). Wilson intervals are descriptive
            // binomial uncertainty, not time-series-valid inferential CIs.
            double? accuracy = attempts > 0 ? (double)correct / attempts : null;
            (double? lo, double? hi) = Wilson95(correct, attempts);
            var ret = strategy.Curve.Select(x => x.NetReturn).ToArray();
            double mean = ret.Average();
            double v = ret.Length > 1 ? ret.Sum(x => (x - mean) * (x - mean)) / (ret.Length - 1) : 0;
            double barsPerYear = 252.0 / SessionsPerPeriod;
            double? annual = strategy.FinalCapital > 0
                ? Math.Pow(strategy.FinalCapital / policy.InitialCapital, barsPerYear / cases) - 1 : -1;
            if (annual is double a && !double.IsFinite(a)) annual = null;
            double? sharpe = v > 0 ? Math.Sqrt(barsPerYear) * mean / Math.Sqrt(v) : null;
            if (sharpe is double s && !double.IsFinite(s)) sharpe = null;
            reports.Add(new(strategy.StrategyId, strategy.Kind, cases, attempts, correct, accuracy,
                (double)attempts / cases, lo, hi, strategy.FinalCapital, strategy.CumulativeReturn,
                strategy.MaxDrawdown, annual, sharpe, strategy.ExecutedTrades, strategy.TotalTurnover,
                strategy.TotalCostsPaid, meanExposure / cases, strategy.Curve));
        }
        return new(Version, dataVersion, hash, request.Seed, true, Symbol,
            "At a fixed decision close, classify price movement over the NEXT FIVE TRADING SESSIONS as UP (>0) or DOWN (<=0).",
            PeriodCount, SessionsPerPeriod, bars[0].SignalAt, bars[^1].LabelEndAt,
            testSet.Count, FixedWindow, policy,
            new Dictionary<string, string> {
                ["mock-single"] = model1, ["mock-second"] = model2, ["mock-fusion"] = fusion,
                ["random-seeded"] = "splitmix64-v1"
            },
            observations, underlying.Windows, reports,
            "ALL inputs and all mock predictions are synthetic simulation; no real Jev/Laya integration, "
            + "no real stocks, no live trading. One fixed synthetic symbol and entire 32-period sequence; "
            + "5 non-overlapping trading sessions per label. Train/validation outcomes obey purge/embargo. "
            + "Every signal is decided on the previous trading session's close, strictly "
            + "before execution time. No tuning from future held-out returns. Single/second are fixed momentum heuristics, fusion "
            + "requires agreement and may abstain; none is an empirically trained AI model. Buy-hold holds "
            + "continuously only over contiguous held-out intervals and pays just one open/close. "
            + "Other strategies close each 5-session position and pay both sides. Random shares full exposure "
            + "and case pool with single/buy-hold, but fusion can abstain: exposure differences must not be "
            + "misread as strategy skill. Wilson95 is a descriptive IID binomial approximation, not valid "
            + "coverage for correlated time-series labels. 252/5 annualization is illustrative; no statistically "
            + "established edge, no survivorship representativeness of real stock universes.");
    }

    private static (double?, double?) Wilson95(int successes, int total)
    {
        if (total == 0) return (null, null);
        const double z = 1.959963984540054;
        double p = (double)successes / total;
        double z2 = z * z;
        double d = 1 + z2 / total;
        double center = (p + z2 / (2 * total)) / d;
        double error = z / d * Math.Sqrt(p * (1 - p) / total + z2 / (4.0 * total * total));
        return (Math.Max(0, center - error), Math.Min(1, center + error));
    }
}

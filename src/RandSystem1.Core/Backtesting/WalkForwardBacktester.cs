using RandSystem1.Core.Random;

namespace RandSystem1.Core.Backtesting;

/// <summary>One historical decision point. LabelEndAt identifies when its outcome became knowable.
/// Entry/Exit prices and outcomes are for scoring only, never for fitting or choosing actions.</summary>
public sealed record BacktestBar(
    string ObservationId,
    DateTimeOffset SignalAt,
    DateTimeOffset LabelEndAt,
    double EntryPrice,
    double ExitPrice,
    IReadOnlyList<BacktestSignal>? Signals = null);

/// <summary>Externally frozen prediction; available at or before SignalAt, not fitted on this test label.</summary>
public sealed record BacktestSignal(string StrategyId, double Position, DateTimeOffset DecidedAt, DateTimeOffset FittedUntil);

/// <summary>Random is seeded; ABSTAIN is zero; buy-hold holds continuously; other strategies need frozen signals.</summary>
public sealed record BacktestStrategy(string StrategyId, string Kind, int Seed = 42);

public sealed record WalkForwardConfiguration(
    int MinimumTrainBars = 5,
    int ValidationBars = 2,
    int TestBars = 2,
    int StepBars = 2,
    int EmbargoBars = 0,
    bool ExpandingTrain = true);

public sealed record BacktestCostPolicy(
    double InitialCapital = 10000,
    double FeeRate = 0.001,
    double SlippageRate = 0.001,
    double MaxAbsolutePosition = 1);

public sealed record BacktestRequest(
    IReadOnlyList<BacktestBar> Bars,
    IReadOnlyList<BacktestStrategy> Strategies,
    WalkForwardConfiguration? Window = null,
    BacktestCostPolicy? Costs = null,
    bool IsSimulation = true,
    string? DatasetVersion = null);

public sealed record WalkForwardWindow(
    int Index,
    IReadOnlyList<string> TrainIds,
    IReadOnlyList<string> ValidationIds,
    IReadOnlyList<string> PurgedTrainIds,
    IReadOnlyList<string> PurgedValidationIds,
    IReadOnlyList<string> EmbargoedIds,
    IReadOnlyList<string> TestIds,
    DateTimeOffset TestStartsAt);

public sealed record BacktestPoint(
    string ObservationId, int WindowIndex, double Position, double GrossReturn,
    double CostFraction, double NetReturn, double Equity, double Turnover, bool Abstained);

public sealed record BacktestStrategyReport(
    string StrategyId, string Kind, int Seed, double InitialCapital, double FinalCapital,
    double CumulativeReturn, double MaxDrawdown, double? AnnualizedReturn,
    double? AnnualizedSharpe, int ExecutedTrades, int WinningTrades,
    double WinRate, double TotalTurnover, double TotalCostsPaid,
    IReadOnlyList<BacktestPoint> Curve);

public sealed record BacktestReport(
    string EngineVersion, string? DatasetVersion, bool IsSimulation,
    WalkForwardConfiguration Window, BacktestCostPolicy Costs,
    IReadOnlyList<WalkForwardWindow> Windows,
    IReadOnlyList<BacktestStrategyReport> Strategies,
    string Interpretation);

public static class WalkForwardBacktester
{
    public const string Version = "0.7.0";
    public static BacktestReport Run(BacktestRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = request.Window ?? new WalkForwardConfiguration();
        var costs = request.Costs ?? new BacktestCostPolicy();
        if (!request.IsSimulation) throw new ArgumentException("Paper backtest only: IsSimulation must be true.");
        if (string.IsNullOrWhiteSpace(request.DatasetVersion)) throw new ArgumentException("DatasetVersion is required.");
        var bars = request.Bars;
        var strategies = request.Strategies;
        if (bars is null || bars.Count is < 5 or > 20000 ||
            strategies is null || strategies.Count is < 1 or > 12 ||
            strategies.Any(s => s is null || string.IsNullOrWhiteSpace(s.StrategyId) ||
                !ValidKind(s.Kind)) ||
            strategies.Select(s => s.StrategyId).Distinct(StringComparer.Ordinal).Count() != strategies.Count)
            throw new ArgumentException("Require 5..20000 bars and 1..12 unique known strategy IDs.");
        if (config.MinimumTrainBars is < 2 or > 20000 || config.ValidationBars is < 1 or > 20000 ||
            config.TestBars is < 1 or > 20000 || config.StepBars < config.TestBars || config.StepBars > 20000 ||
            config.EmbargoBars < 0 || config.EmbargoBars > 1000)
            throw new ArgumentException("Invalid window lengths or overlapping test step.");
        if (!double.IsFinite(costs.InitialCapital) || costs.InitialCapital <= 0 ||
            !double.IsFinite(costs.FeeRate) || costs.FeeRate is < 0 or > 0.5 ||
            !double.IsFinite(costs.SlippageRate) || costs.SlippageRate is < 0 or > 0.5 ||
            !double.IsFinite(costs.MaxAbsolutePosition) || costs.MaxAbsolutePosition is <= 0 or > 1)
            throw new ArgumentException("Invalid capital, execution costs or maximum absolute exposure.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        DateTimeOffset lastTime = default;
        foreach (var bar in bars)
        {
            ct.ThrowIfCancellationRequested();
            if (bar is null || string.IsNullOrWhiteSpace(bar.ObservationId) ||
                !ids.Add(bar.ObservationId) || bar.SignalAt == default ||
                bar.SignalAt <= lastTime || bar.LabelEndAt <= bar.SignalAt ||
                !double.IsFinite(bar.EntryPrice) || !double.IsFinite(bar.ExitPrice) ||
                bar.EntryPrice <= 0 || bar.ExitPrice <= 0)
                throw new ArgumentException("Bars need unique IDs, strict chronology, future label-end, and positive finite prices.");
            lastTime = bar.SignalAt;
        }

        int firstTest = checked(config.MinimumTrainBars + config.ValidationBars + 2 * config.EmbargoBars);
        if (firstTest + config.TestBars > bars.Count)
            throw new ArgumentException("Insufficient chronological history for the first full walk-forward window.");

        var windows = new List<WalkForwardWindow>();
        var testIndexes = new List<(int BarIndex, int WindowIndex)>();
        for (int start = firstTest, win = 0; start + config.TestBars <= bars.Count;
             start += config.StepBars, win++)
        {
            ct.ThrowIfCancellationRequested();
            int valStart = start - config.EmbargoBars - config.ValidationBars;
            int valEnd = start - config.EmbargoBars;
            int trainEnd = valStart - config.EmbargoBars;
            int trainStart = config.ExpandingTrain ? 0 : Math.Max(0, trainEnd - config.MinimumTrainBars);
            var train = new List<string>();
            var val = new List<string>();
            var purgeTrain = new List<string>();
            var purgeValidation = new List<string>();
            for (int i = trainStart; i < trainEnd; i++)
            {
                // No training outcome may become known after validation starts.
                if (bars[i].LabelEndAt <= bars[valStart].SignalAt) train.Add(bars[i].ObservationId);
                else purgeTrain.Add(bars[i].ObservationId);
            }
            for (int i = valStart; i < valEnd; i++)
            {
                // No validation outcome may extend into the held-out test.
                if (bars[i].LabelEndAt <= bars[start].SignalAt) val.Add(bars[i].ObservationId);
                else purgeValidation.Add(bars[i].ObservationId);
            }
            if (train.Count == 0 || val.Count == 0)
                throw new ArgumentException("Purging left no eligible training or validation cases.");
            var embargo = Enumerable.Range(trainEnd, config.EmbargoBars)
                .Concat(Enumerable.Range(valEnd, config.EmbargoBars))
                .Select(i => bars[i].ObservationId).ToArray();
            var testIds = Enumerable.Range(start, config.TestBars).Select(i => bars[i].ObservationId).ToArray();
            windows.Add(new(win, train, val, purgeTrain, purgeValidation, embargo, testIds, bars[start].SignalAt));
            for (int i = start; i < start + config.TestBars; i++)
                testIndexes.Add((i, win));
        }

        // This implementation trades one bar at a time, closing before the next
        // entry. Overlapping label windows cannot be counted as independent trades.
        for (int i = 1; i < testIndexes.Count; i++)
        {
            if (bars[testIndexes[i - 1].BarIndex].LabelEndAt > bars[testIndexes[i].BarIndex].SignalAt)
                throw new ArgumentException("Overlapping test holding intervals are not supported.");
        }

        if (strategies.Any(s => s.Kind.Equals("buy-hold", StringComparison.OrdinalIgnoreCase)) &&
            costs.MaxAbsolutePosition != 1.0)
            throw new ArgumentException("Buy-hold assumes full capital investment; its max absolute position must be 1.");
        if (strategies.Any(s => s.Kind.Equals("buy-hold", StringComparison.OrdinalIgnoreCase)) &&
            testIndexes.Zip(testIndexes.Skip(1)).Any(pair =>
                bars[pair.First.BarIndex].LabelEndAt != bars[pair.Second.BarIndex].SignalAt))
            throw new ArgumentException("Continuous buy-hold requires contiguous test holding intervals without gaps.");

        var results = new List<BacktestStrategyReport>();
        foreach (var strategy in strategies)
        {
            ct.ThrowIfCancellationRequested();
            var rng = new StableRandom(unchecked((ulong)(uint)strategy.Seed));
            double equity = costs.InitialCapital, peak = equity, maxDrawdown = 0, turnover = 0, costPaid = 0;
            int trades = 0, wins = 0;
            bool continuousHold = strategy.Kind.Equals("buy-hold", StringComparison.OrdinalIgnoreCase);
            var curve = new List<BacktestPoint>();
            var returns = new List<double>();
            foreach (var (index, window) in testIndexes)
            {
                ct.ThrowIfCancellationRequested();
                var bar = bars[index];
                double position;
                if (strategy.Kind.Equals("abstain", StringComparison.OrdinalIgnoreCase))
                    position = 0;
                else if (continuousHold)
                    position = costs.MaxAbsolutePosition;
                else if (strategy.Kind.Equals("random", StringComparison.OrdinalIgnoreCase))
                    position = rng.NextDouble() < 0.5 ? -costs.MaxAbsolutePosition : costs.MaxAbsolutePosition;
                else
                {
                    var matches = bar.Signals?.Where(s => s is not null && s.StrategyId == strategy.StrategyId).ToArray();
                    if (matches is not { Length: 1 })
                        throw new ArgumentException($"Missing or duplicate frozen signal for strategy '{strategy.StrategyId}' on '{bar.ObservationId}'.");
                    var signal = matches[0];
                    if (!double.IsFinite(signal.Position) || Math.Abs(signal.Position) > costs.MaxAbsolutePosition ||
                        signal.DecidedAt == default || signal.FittedUntil == default ||
                        signal.FittedUntil > signal.DecidedAt || signal.DecidedAt > bar.SignalAt)
                        throw new ArgumentException("Signals must be finite, within risk limits and based only on already available data.");
                    position = signal.Position;
                }
                double change = bar.ExitPrice / bar.EntryPrice - 1;
                if (!double.IsFinite(change)) throw new ArgumentException("Price ratio overflow.");
                // Round trip on every tested interval: opening + closing fees/slippage;
                // no assumption of continuous holding or free liquidation.
                // Buy-and-hold enters once on the first test bar and exits once
                // on the last contiguous test bar; unlike daily round-trip strategies.
                int testOrdinal = curve.Count;
                double barTurnover = continuousHold
                    ? (testOrdinal == 0 ? Math.Abs(position) : 0)
                      + (testOrdinal == testIndexes.Count - 1 ? Math.Abs(position) : 0)
                    : 2 * Math.Abs(position);
                double cost = barTurnover * (costs.FeeRate + costs.SlippageRate);
                double gross = position * change;
                double net = gross - cost;
                if (!double.IsFinite(net)) throw new ArgumentException("Arithmetic overflow in net returns.");
                double startingEquity = equity;
                double multiplier = Math.Max(0, 1 + net);
                equity *= multiplier;
                if (!double.IsFinite(equity)) throw new ArgumentException("Capital overflow.");
                if (!continuousHold && position != 0 && startingEquity > 0)
                {
                    trades++;
                    if (net > 0) wins++;
                }
                costPaid += startingEquity * cost;
                turnover += barTurnover;
                peak = Math.Max(peak, equity);
                maxDrawdown = Math.Max(maxDrawdown, (peak - equity) / peak);
                double realizedReturn = startingEquity > 0 ? multiplier - 1 : 0;
                returns.Add(realizedReturn);
                curve.Add(new(bar.ObservationId, window, position, gross, cost,
                    realizedReturn, equity, barTurnover, position == 0));
            }
            if (continuousHold)
            {
                trades = 1;
                wins = equity > costs.InitialCapital ? 1 : 0;
            }
            int n = returns.Count;
            double average = returns.Average();
            double variance = n > 1 ? returns.Sum(x => (x - average) * (x - average)) / (n - 1) : 0;
            double? sharpe = variance > 0 ? Math.Sqrt(252) * average / Math.Sqrt(variance) : null;
            if (sharpe is double s && !double.IsFinite(s)) sharpe = null;
            double? annualized = equity > 0 ? Math.Pow(equity / costs.InitialCapital, 252.0 / n) - 1 : -1;
            if (annualized is double a && !double.IsFinite(a)) annualized = null;
            results.Add(new(strategy.StrategyId, strategy.Kind, strategy.Seed,
                costs.InitialCapital, equity, equity / costs.InitialCapital - 1,
                maxDrawdown, annualized, sharpe, trades, wins,
                trades == 0 ? 0 : (double)wins / trades, turnover, costPaid, curve));
        }
        return new(Version, request.DatasetVersion, request.IsSimulation, config, costs, windows, results,
            "Simulation only: all strategies share test bars, capital and costs. Other than buy-hold, each interval opens and closes its position; buy-hold remains invested across contiguous test bars and pays entry/exit costs only at the two outer boundaries. " +
            "Training/validation indices exclude labels unavailable at the next boundary and respect embargo bars. " +
            "Model/rule/fusion signals MUST be externally frozen as of the decision time; input provenance and actual historical model fitting cannot be independently authenticated. " +
            "Random is deterministic seeded simulation, not a real model; ABSTAIN carries zero exposure. " +
            "252-bar annualization/Sharpe are descriptive IID-style conventions, not reliable inference for overlapping or correlated observations. " +
            "No live execution, no promise of future returns.");
    }

    private static bool ValidKind(string? kind) => kind is not null &&
        (kind.Equals("single", StringComparison.OrdinalIgnoreCase) ||
         kind.Equals("rule", StringComparison.OrdinalIgnoreCase) ||
         kind.Equals("fusion", StringComparison.OrdinalIgnoreCase) ||
         kind.Equals("random", StringComparison.OrdinalIgnoreCase) ||
         kind.Equals("abstain", StringComparison.OrdinalIgnoreCase) ||
         kind.Equals("buy-hold", StringComparison.OrdinalIgnoreCase));
}

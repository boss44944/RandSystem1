using RandSystem1.Core.Backtesting;

namespace RandSystem1.UnitTests;

public sealed class SimulatedStockExampleTests
{
    [Fact]
    public void DefaultRunIsFixedFullyLabeledSimulatedFiveSessionExperiment()
    {
        var run = SimulatedStockExample.Run();
        Assert.Equal("0.7.0", run.ExampleVersion);
        Assert.True(run.IsSimulation);
        Assert.Equal(32, run.Periods);
        Assert.Equal(5, run.SessionsPerPeriod);
        Assert.Equal(32, run.Observations.Count);
        Assert.Equal(18, run.TestPeriods);
        Assert.Equal("SYNTHETIC-ONE", run.Instrument);
        Assert.Equal(64, run.DatasetHashSha256.Length);
        Assert.Equal("41f8ca8e0d9cb83414a6c2dc9fdcabe6a5b2a9218b28c04b1f38a89e70b06f0d", run.DatasetHashSha256);
        Assert.All(run.Observations, x =>
        {
            Assert.True(x.ExitPrice > 0);
            Assert.True(x.EntryPrice > 0);
            Assert.Equal(run.Instrument, x.Symbol);
            Assert.Contains(x.TrueLabel, new[] { "UP", "DOWN" });
            Assert.True(x.LabelEndAt > x.SignalAt);
        });
        Assert.Contains("simulation", run.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jev:", run.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatesAreFiveTradingSessionsWithNoWeekendOrOverlappingLabels()
    {
        var data = SimulatedStockExample.Run().Observations;
        var tradingDates = SimulatedStockExample.TradingSessions();
        Assert.Equal(161, tradingDates.Count);
        for (int i = 0; i < data.Count; i++)
        {
            Assert.Equal(tradingDates[5 * i], data[i].SignalAt);
            Assert.Equal(tradingDates[5 * i + 5], data[i].LabelEndAt);
            if (i > 0) Assert.Equal(data[i - 1].LabelEndAt, data[i].SignalAt);
            Assert.NotEqual(DayOfWeek.Saturday, data[i].SignalAt.DayOfWeek);
            Assert.NotEqual(DayOfWeek.Sunday, data[i].SignalAt.DayOfWeek);
        }
    }

    [Fact]
    public void IdenticalSeedReplaysPricesSignalsSplitsAndReports()
    {
        var a = SimulatedStockExample.Run(new(43));
        var b = SimulatedStockExample.Run(new(43));
        Assert.Equal(a.DatasetHashSha256, b.DatasetHashSha256);
        Assert.Equal(a.DatasetVersion, b.DatasetVersion);
        Assert.Equal(a.Observations, b.Observations);
        Assert.Equal(a.Comparison.Select(x => x.FinalCapital),
            b.Comparison.Select(x => x.FinalCapital));
        Assert.Equal(a.Comparison.SelectMany(x => x.EquityCurve).Select(x => x.Position),
            b.Comparison.SelectMany(x => x.EquityCurve).Select(x => x.Position));
    }

    [Fact]
    public void DifferentSeedsMakeDifferentSyntheticDatasetsButKeepTestPoolAndInstrument()
    {
        var a = SimulatedStockExample.Run(new(42));
        var b = SimulatedStockExample.Run(new(43));
        Assert.NotEqual(a.DatasetHashSha256, b.DatasetHashSha256);
        Assert.NotEqual(a.DatasetVersion, b.DatasetVersion);
        Assert.Equal(a.Observations.Select(x => x.ObservationId), b.Observations.Select(x => x.ObservationId));
        Assert.Equal(a.Observations.Select(x => x.SignalAt), b.Observations.Select(x => x.SignalAt));
        Assert.Equal(a.Windows.SelectMany(x => x.TestIds),
            b.Windows.SelectMany(x => x.TestIds));
    }

    [Fact]
    public void SixCategoriesHaveExactlyIdenticalHeldOutIdOrderAndCostPolicy()
    {
        var r = SimulatedStockExample.Run();
        Assert.Equal(6, r.Comparison.Count);
        Assert.Equal(["mock-single","mock-second","mock-fusion","random-seeded","buy-hold","no-trade"],
            r.Comparison.Select(x => x.StrategyId));
        var ids = r.Windows.SelectMany(x => x.TestIds);
        Assert.All(r.Comparison, x => Assert.Equal(ids, x.EquityCurve.Select(y => y.ObservationId)));
        Assert.Equal(10000, r.Costs.InitialCapital);
        Assert.Equal(.001, r.Costs.FeeRate);
        Assert.Equal(.001, r.Costs.SlippageRate);
        Assert.Equal(1, r.Costs.MaxAbsolutePosition);
        Assert.All(r.Comparison, x => Assert.Equal(r.TestPeriods, x.TestCases));
    }

    [Fact]
    public void NoTradeHasNoAccuracyCostsTradesOrExposure()
    {
        var flat = SimulatedStockExample.Run().Comparison.Single(x => x.StrategyId == "no-trade");
        Assert.Null(flat.Accuracy);
        Assert.Null(flat.AccuracyWilsonLower95);
        Assert.Equal(0, flat.Predictions);
        Assert.Equal(0, flat.Trades);
        Assert.Equal(0, flat.Coverage);
        Assert.Equal(0, flat.Turnover);
        Assert.Equal(0, flat.TotalCostsPaid);
        Assert.Equal(0, flat.MaxDrawdown);
        Assert.Equal(10000, flat.FinalCapital);
        Assert.Equal(0, flat.MeanAbsoluteExposure);
    }

    [Fact]
    public void BuyAndHoldEntersOnceAndExitsOnceWithoutArtificialFrequentCosts()
    {
        var r = SimulatedStockExample.Run();
        var hold = r.Comparison.Single(x => x.StrategyId == "buy-hold");
        Assert.Equal(1, hold.Trades);
        Assert.Equal(2, hold.Turnover);
        Assert.Equal(1, hold.MeanAbsoluteExposure);
        Assert.Equal(.002, hold.EquityCurve[0].CostFraction, 12);
        Assert.All(hold.EquityCurve.Skip(1).SkipLast(1), x => Assert.Equal(0, x.CostFraction));
        Assert.Equal(.002, hold.EquityCurve[^1].CostFraction, 12);
        Assert.True(hold.TotalCostsPaid > 0);
        Assert.NotEqual(r.Comparison.Single(x => x.StrategyId == "mock-single").Turnover, hold.Turnover);
    }

    [Fact]
    public void SingleAndSeededRandomMatchAbsoluteExposureAndTradingFrequency()
    {
        var r = SimulatedStockExample.Run();
        var single = r.Comparison.Single(x => x.StrategyId == "mock-single");
        var random = r.Comparison.Single(x => x.StrategyId == "random-seeded");
        Assert.Equal(r.TestPeriods, single.Trades);
        Assert.Equal(r.TestPeriods, random.Trades);
        Assert.Equal(single.MeanAbsoluteExposure, random.MeanAbsoluteExposure);
        Assert.Equal(single.Turnover, random.Turnover);
        Assert.Equal(1, single.Coverage);
        Assert.Equal(1, random.Coverage);
        Assert.True(single.TotalCostsPaid > 0);
        Assert.True(random.TotalCostsPaid > 0);
    }

    [Fact]
    public void PredictAtCannotObserveModifiedFutureQuotes()
    {
        double[] history = Enumerable.Range(1, 25).Select(i => (double)(100 + i)).ToArray();
        var before = SimulatedStockExample.PredictAt(history, 19);
        for (int i = 20; i < 25; i++) history[i] = (i % 2 == 0 ? 1 : 10000);
        var after = SimulatedStockExample.PredictAt(history, 19);
        Assert.Equal(before, after);
        Assert.Equal("UP", after.Single);
        Assert.Equal("UP", after.Second);
        Assert.Equal("UP", after.Fusion);
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.PredictAt(history, 25));
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.PredictAt(history, -1));
    }

    [Fact]
    public void MomentumAndFusionCanDisagreeOrAbstainWithoutPeeking()
    {
        double[] history = [100,110,120,130,140,150,160,170,180,190,200,180,160,140,120,100];
        var (a,b,f,ret5,ret15) = SimulatedStockExample.PredictAt(history, 15);
        Assert.Equal("DOWN", a);
        Assert.Equal("UP", b);
        Assert.Equal("ABSTAIN", f);
        Assert.True(ret5 < 0);
        Assert.Equal(0, ret15);
        Assert.Equal("ABSTAIN", SimulatedStockExample.PredictAt(history, 4).Fusion);
        history[4] = double.NaN;
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.PredictAt(history, 4));
    }

    [Fact]
    public void PredictionsUsePriorCloseRatherThanPriceAtExecution()
    {
        var r = SimulatedStockExample.Run();
        // At signal t, the model uses close[t-1]. A bar's realized UP/DOWN
        // is never used in that same bar's prediction.
        Assert.Equal("ABSTAIN", r.Observations[0].SinglePrediction);
        Assert.Equal("ABSTAIN", r.Observations[1].SinglePrediction);
        Assert.Null(r.Observations[1].Momentum5);
        Assert.NotNull(r.Observations[2].Momentum5);
        Assert.Null(r.Observations[3].Momentum15);
        Assert.NotNull(r.Observations[4].Momentum15);
    }

    [Fact]
    public void AccuracyOnlyScoresActualPredictionsOnFrozenTestPool()
    {
        var r = SimulatedStockExample.Run();
        foreach (var s in r.Comparison)
        {
            Assert.Equal(s.Predictions + (s.TestCases - s.Predictions), s.TestCases);
            Assert.True(s.Correct <= s.Predictions);
            if (s.Predictions == 0)
            {
                Assert.Null(s.Accuracy);
                Assert.Null(s.AccuracyWilsonLower95);
            }
            else
            {
                Assert.Equal((double)s.Correct / s.Predictions, s.Accuracy!.Value, 12);
                Assert.True(s.AccuracyWilsonLower95 <= s.Accuracy);
                Assert.True(s.AccuracyWilsonUpper95 >= s.Accuracy);
                Assert.InRange(s.AccuracyWilsonLower95!.Value, 0, 1);
                Assert.InRange(s.AccuracyWilsonUpper95!.Value, 0, 1);
            }
        }
    }

    [Fact]
    public void TradingCostsAreAppliedToAllStrategiesWithExposure()
    {
        var free = SimulatedStockExample.Run(new(FeeRate: 0, SlippageRate: 0));
        var paid = SimulatedStockExample.Run(new(FeeRate: .01, SlippageRate: .01));
        Assert.Equal(free.DatasetHashSha256, paid.DatasetHashSha256);
        foreach (var id in new[] {"mock-single","mock-second","mock-fusion","random-seeded","buy-hold"})
        {
            var a = free.Comparison.Single(x => x.StrategyId == id);
            var b = paid.Comparison.Single(x => x.StrategyId == id);
            Assert.True(a.FinalCapital > b.FinalCapital, id);
            Assert.Equal(0, a.TotalCostsPaid);
            Assert.True(b.TotalCostsPaid > 0);
        }
        Assert.Equal(free.Comparison.Single(x => x.StrategyId == "no-trade").FinalCapital,
            paid.Comparison.Single(x => x.StrategyId == "no-trade").FinalCapital);
    }

    [Fact]
    public void InvalidFinancialAssumptionsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.Run(new(InitialCapital: 0)));
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.Run(new(FeeRate: -0.01)));
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.Run(new(SlippageRate: double.NaN)));
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.Run(new(MaxAbsolutePosition: 2)));
        Assert.Throws<ArgumentException>(() => SimulatedStockExample.Run(new(MaxAbsolutePosition: double.PositiveInfinity)));
    }

    [Fact]
    public void CancellationStopsBeforeGeneratingReport()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => SimulatedStockExample.Run(ct: cts.Token));
    }
}

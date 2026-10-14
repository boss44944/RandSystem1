using RandSystem1.Core.Backtesting;

namespace RandSystem1.UnitTests;

public sealed class WalkForwardBacktesterTests
{
    private static readonly DateTimeOffset Day0 = new(2026, 01, 01, 12, 0, 0, TimeSpan.Zero);
    private static readonly WalkForwardConfiguration Window = new(3, 2, 2, 2, 1);
    private static BacktestStrategy[] Strategies() =>
    [
        new("single", "single"),
        new("fusion", "fusion"),
        new("rule", "rule"),
        new("random", "random", 1234),
        new("hold", "abstain")
    ];
    private static BacktestBar Bar(int i, double entry = 100, double exit = 110, double singlePosition = 1,
        DateTimeOffset? labelEnd = null, bool withSignals = true)
    {
        var time = Day0.AddDays(i);
        return new($"case-{i}", time, labelEnd ?? time.AddHours(6), entry, exit,
            withSignals ?
            [
                new BacktestSignal("single", singlePosition, time.AddMinutes(-1), time.AddDays(-1)),
                new BacktestSignal("fusion", 0.5, time.AddMinutes(-1), time.AddDays(-1)),
                new BacktestSignal("rule", -1, time.AddMinutes(-1), time.AddDays(-1))
            ] : null);
    }
    private static BacktestRequest Fixture(
        int n = 10,
        WalkForwardConfiguration? window = null,
        BacktestCostPolicy? costs = null,
        IReadOnlyList<BacktestStrategy>? strategies = null) =>
        new(Enumerable.Range(0, n).Select(i => Bar(i)).ToArray(),
            strategies ?? Strategies(), window ?? Window, costs ?? new BacktestCostPolicy(1000, .01, .01),
            DatasetVersion: "fixture-006-v1");

    [Fact]
    public void FixedSplitSeparatesTrainValidationEmbargoAndTest()
    {
        var report = WalkForwardBacktester.Run(Fixture());
        Assert.Equal("0.7.0", report.EngineVersion);
        var win = Assert.Single(report.Windows);
        Assert.Equal(["case-0", "case-1", "case-2"], win.TrainIds);
        Assert.Equal(["case-4", "case-5"], win.ValidationIds);
        Assert.Equal(["case-3", "case-6"], win.EmbargoedIds);
        Assert.Equal(["case-7", "case-8"], win.TestIds);
        Assert.Empty(win.PurgedTrainIds);
        Assert.Empty(win.PurgedValidationIds);
        Assert.Equal(Day0.AddDays(7), win.TestStartsAt);
    }

    [Fact]
    public void MultipleWalkForwardWindowsHaveDisjointTestsAndChronologicalTraining()
    {
        var report = WalkForwardBacktester.Run(Fixture(14));
        Assert.Equal(3, report.Windows.Count);
        Assert.Equal(["case-7", "case-8"], report.Windows[0].TestIds);
        Assert.Equal(["case-9", "case-10"], report.Windows[1].TestIds);
        Assert.Equal(["case-11", "case-12"], report.Windows[2].TestIds);
        Assert.Equal(5, report.Windows[1].TrainIds.Count);
        Assert.True(report.Windows[2].TestStartsAt > report.Windows[1].TestStartsAt);
        Assert.Equal(6, report.Strategies[0].Curve.Count);
    }

    [Fact]
    public void RollingWindowDoesNotExpandTrainingHistory()
    {
        var rolling = Fixture(14, Window with { ExpandingTrain = false });
        var result = WalkForwardBacktester.Run(rolling);
        Assert.All(result.Windows, x => Assert.Equal(3, x.TrainIds.Count));
        Assert.Equal(["case-2", "case-3", "case-4"], result.Windows[1].TrainIds);
    }

    [Fact]
    public void PurgeRemovesLabelsUnknownAtNextWindowBoundary()
    {
        var sample = Fixture().Bars.ToArray();
        sample[1] = Bar(1, labelEnd: Day0.AddDays(5)); // train label overlaps validation start 4
        sample[4] = Bar(4, labelEnd: Day0.AddDays(8)); // validation label overlaps test start 7
        var result = WalkForwardBacktester.Run(Fixture() with { Bars = sample });
        var win = Assert.Single(result.Windows);
        Assert.Equal(["case-1"], win.PurgedTrainIds);
        Assert.Equal(["case-4"], win.PurgedValidationIds);
        Assert.Equal(["case-0", "case-2"], win.TrainIds);
        Assert.Equal(["case-5"], win.ValidationIds);
    }

    [Fact]
    public void InsufficientHistoryOrPurgeDepletingTrainingFailsClosed()
    {
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(Fixture(8)));
        var sample = Fixture().Bars.ToArray();
        for (int i = 0; i < 3; i++) sample[i] = Bar(i, labelEnd: Day0.AddDays(6));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(Fixture() with { Bars = sample }));
    }

    [Fact]
    public void RoundTripChargesFeesAndSlippageForBothSides()
    {
        var result = WalkForwardBacktester.Run(Fixture());
        var s = result.Strategies.Single(x => x.StrategyId == "single");
        Assert.Equal(2, s.ExecutedTrades);
        Assert.Equal(2, s.WinningTrades);
        Assert.Equal(1, s.WinRate);
        Assert.Equal(4, s.TotalTurnover);
        Assert.Equal(.1, s.Curve[0].GrossReturn, 12);
        Assert.Equal(.04, s.Curve[0].CostFraction, 12);
        Assert.Equal(.06, s.Curve[0].NetReturn, 12);
        Assert.Equal(1060, s.Curve[0].Equity, 8);
        Assert.Equal(1123.6, s.FinalCapital, 8);
        Assert.Equal(82.4, s.TotalCostsPaid, 8);
    }

    [Fact]
    public void ZeroExposureNeverTradesOrLosesCapitalToFees()
    {
        var r = WalkForwardBacktester.Run(Fixture());
        var z = r.Strategies.Single(s => s.Kind == "abstain");
        Assert.Equal(0, z.ExecutedTrades);
        Assert.Equal(0, z.WinningTrades);
        Assert.Equal(0, z.TotalTurnover);
        Assert.Equal(0, z.TotalCostsPaid);
        Assert.Equal(0, z.CumulativeReturn);
        Assert.Equal(0, z.MaxDrawdown);
        Assert.Null(z.AnnualizedSharpe);
        Assert.All(z.Curve, x => Assert.True(x.Abstained));
    }

    [Fact]
    public void ComparisonUsesIdenticalTestCasesAndCostsAcrossStrategies()
    {
        var r = WalkForwardBacktester.Run(Fixture());
        Assert.Equal(5, r.Strategies.Count);
        Assert.All(r.Strategies, x => Assert.Equal(["case-7", "case-8"],
            x.Curve.Select(p => p.ObservationId)));
        Assert.True(r.Strategies.Single(x => x.StrategyId == "single").FinalCapital >
                    r.Strategies.Single(x => x.StrategyId == "fusion").FinalCapital);
        Assert.True(r.Strategies.Single(x => x.StrategyId == "rule").FinalCapital < 1000);
        Assert.True(r.Strategies.Single(x => x.StrategyId == "hold").FinalCapital == 1000);
        Assert.Contains("Simulation only", r.Interpretation);
    }

    [Fact]
    public void ASeededRandomBaselineIsExactlyReproducible()
    {
        var req = Fixture();
        var a = WalkForwardBacktester.Run(req).Strategies.Single(x => x.Kind == "random");
        var b = WalkForwardBacktester.Run(req).Strategies.Single(x => x.Kind == "random");
        Assert.Equal(a.Curve.Select(x => x.Position), b.Curve.Select(x => x.Position));
        Assert.Equal(a.FinalCapital, b.FinalCapital);
        Assert.True(WalkForwardBacktester.Run(req with
        { Strategies = [new BacktestStrategy("random", "random", 5678)] }).Strategies.Count == 1);
    }

    [Fact]
    public void MissingOrDuplicateModelSignalsRejectEntireComparison()
    {
        var req = Fixture();
        var bars = req.Bars.ToArray();
        bars[7] = Bar(7, withSignals: false);
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        bars[7] = Bar(7) with { Signals = [Bar(7).Signals![0], Bar(7).Signals![0], .. Bar(7).Signals!.Skip(1)] };
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
    }

    [Fact]
    public void FutureSignalOrFutureFittedModelIsRejected()
    {
        var req = Fixture();
        var bars = req.Bars.ToArray();
        var late = bars[7].Signals!.ToArray();
        late[0] = late[0] with { DecidedAt = bars[7].SignalAt.AddMinutes(1) };
        bars[7] = bars[7] with { Signals = late };
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        late[0] = late[0] with { DecidedAt = bars[7].SignalAt, FittedUntil = bars[7].SignalAt.AddDays(1) };
        bars[7] = bars[7] with { Signals = late };
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
    }

    [Fact]
    public void MissingPricesOrInvalidSignalsAreRejected()
    {
        var req = Fixture();
        var bars = req.Bars.ToArray();
        bars[7] = Bar(7, entry: double.NaN);
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        bars[7] = Bar(7, entry: 0);
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        bars[7] = Bar(7, singlePosition: double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        bars[7] = Bar(7, singlePosition: 1.1);
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
    }

    [Fact]
    public void DatasetIdsDatesAndLabelWindowMustBeWellFormed()
    {
        var req = Fixture();
        var bars = req.Bars.ToArray();
        bars[7] = Bar(7) with { ObservationId = bars[6].ObservationId };
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        bars[7] = Bar(7) with { SignalAt = bars[6].SignalAt };
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        bars[7] = Bar(7) with { LabelEndAt = Day0.AddDays(7) };
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
    }

    [Fact]
    public void TestHoldingIntervalsCannotOverlapOrBeCountedTwice()
    {
        var req = Fixture();
        var bars = req.Bars.ToArray();
        bars[7] = Bar(7, labelEnd: Day0.AddDays(9));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with { Bars = bars }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Window = Window with { StepBars = 1 } }));
    }

    [Fact]
    public void BacktestCannotClaimRealTradingOrOmitDatasetIdentity()
    {
        var q = Fixture();
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(q with { IsSimulation = false }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(q with { DatasetVersion = null }));
    }

    [Fact]
    public void InvalidCostAndWindowConfigurationFail()
    {
        var req = Fixture();
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Costs = new BacktestCostPolicy(FeeRate: -.01) }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Costs = new BacktestCostPolicy(MaxAbsolutePosition: 2) }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Costs = new BacktestCostPolicy(InitialCapital: double.NaN) }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Window = Window with { MinimumTrainBars = 0 } }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Window = Window with { EmbargoBars = -1 } }));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Window = Window with { MinimumTrainBars = int.MaxValue } }));
    }

    [Fact]
    public void ExtremeShortLossCausesBankruptcyNotNegativeEquity()
    {
        var req = Fixture();
        var bars = req.Bars.ToArray();
        bars[7] = Bar(7, entry: 1, exit: 100000);
        var r = WalkForwardBacktester.Run(req with { Bars = bars });
        var shortSide = r.Strategies.Single(s => s.StrategyId == "rule");
        Assert.Equal(0, shortSide.FinalCapital);
        Assert.Equal(-1, shortSide.CumulativeReturn);
        Assert.Equal(1, shortSide.MaxDrawdown);
        Assert.All(shortSide.Curve, p => Assert.True(p.Equity >= 0));
    }

    [Fact]
    public void CancelledRequestDoesNotReturnPartialPerformance()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => WalkForwardBacktester.Run(Fixture(), cancellation.Token));
    }

    [Fact]
    public void ContinuousBuyHoldChargesEntryAndExitOnlyAndCountsOneTrade()
    {
        var fixture = Fixture();
        var output = WalkForwardBacktester.Run(fixture with
        {
            Strategies = [new("long", "buy-hold")],
            Bars = fixture.Bars.Select(x => x with
            {
                ExitPrice = 101,
                LabelEndAt = x.SignalAt.AddDays(1)
            }).ToArray()
        });
        var hold = Assert.Single(output.Strategies);
        Assert.Equal(1, hold.ExecutedTrades);
        Assert.Equal(2, hold.TotalTurnover);
        Assert.Equal(.02, hold.Curve[0].CostFraction, 12);
        Assert.Equal(.02, hold.Curve[^1].CostFraction, 12);
        Assert.All(hold.Curve.Skip(1).SkipLast(1), x => Assert.Equal(0, x.CostFraction));
    }

    [Fact]
    public void ContinuousBuyHoldRejectsGapsBetweenTestPositions()
    {
        var req = Fixture();
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(req with
        { Strategies = [new("long", "buy-hold")] }));
    }

    [Fact]
    public void BuyHoldRequiresFullInvestedExposureWithoutImplicitRebalance()
    {
        var input = Fixture(costs: new BacktestCostPolicy(MaxAbsolutePosition: .5));
        Assert.Throws<ArgumentException>(() => WalkForwardBacktester.Run(input with
        { Strategies = [new("hold", "buy-hold")] }));
    }

    [Fact]
    public void RandomOnlyAndAbstainOnlyNeedNoExternalPredictions()
    {
        var sample = Fixture();
        var bars = sample.Bars.Select(x => x with { Signals = null }).ToArray();
        var result = WalkForwardBacktester.Run(sample with
        { Bars = bars, Strategies = [new("random", "random"), new("abstain", "abstain")] });
        Assert.Equal(2, result.Strategies.Count);
        Assert.Equal(0, result.Strategies[1].ExecutedTrades);
    }
}

# Iteration 007 - Synthetic stock example

## Objective
An executable CPU-only seeded fake-stock demonstration, NOT investment
evidence. At an as-of decision time, classify movement over the NEXT
FIVE TRADING SESSIONS as UP (>0) or DOWN (<=0), score subsequent labels
and compare trading outcomes on identical non-overlapping holdout windows.

## Implemented
- Exactly one predefined fictitious instrument SYNTHETIC-ONE and 32
  five-session horizons (161 synthetic weekday closes). Fixed source and
  predeclared 8/3/3 train/validation/test expanding windows with one bar
  embargo each boundary; labels are purged by availability. No window
  or surviving real stock is picked after seeing results.
- Two explicitly named untrained mock momentum5 and momentum15 rules
  and mock unanimous fusion, decided from PRIOR SESSION closes,
  no real Jev/Laya request or fake claim of model accuracy.
- Seeded Random, mock Single, mock Second, mock Fusion, continuous
  Buy & Hold and No Trade/ABSTAIN; same test observations, initial
  capital, fee and slippage rates, exposure bound. Random vs single
  has matched full-magnitude trade cadence; exposure mismatch for
  abstaining fusion is reported rather than hidden.
- True continuous buy-hold (one opening and one closing transaction,
  not interval-based round trips). All other non-zero strategies
  roundtrip each five-session interval and pay both sides.
- Detailed accuracy, abstention coverage, descriptive IID Wilson95
  bounds, 252/5 annualized descriptive returns, drawdown, trade count,
  turnover, costs and capital curves.
- GET /api/examples/stock/compare?seed=42; OpenAPI 0.7.0;
  examples/stock-demo-007-seed42.json exact version/seed fixture.

## Testing and replay
dotnet restore RandSystem1.slnx
dotnet build RandSystem1.slnx -c Release --warnaserror
dotnet test RandSystem1.slnx -c Release
dotnet run --project src/RandSystem1.Api --urls http://127.0.0.1:5078
curl 'http://127.0.0.1:5078/api/examples/stock/compare?seed=42'

Validate reproducibility, predicted signals unchanged if future prices
change, no same-entry-close dependency, nonoverlapping five-session
labels, alignment of holdout IDs, fees and continuous hold accounting,
invalid cost rejection and no trade costs. Test all previous iterations.

## Safety/statistical limitations
Everything, including labels and prices, is SIMULATION. These are mock
heuristics, NOT empirically evaluated AI models. No live trading,
brokerage credentials, ticker scraping, real Jev/Laya calls or order
submission. This fixed synthetic stock universe cannot establish
survivorship-bias-free results for real investment studies. Time
correlation invalidates naive binomial Wilson confidence coverage.
Seed search or optimizing on reported holdout introduces selection
bias. Different exposure/turnover makes direct rankings confounded.

## Verification record (2026-10-14)
- Dependency restore and Release build with warning-as-error: 0 warnings/0 errors.
- Full xUnit suite: 135/135 tests passed, 0 skipped (18 new tests).
- Actual ASP.NET HTTP smoke: 13/13 cases, including fixed seed replay,
  alternate seed, cost assumptions, invalid fee/seed values, valid
  Buy & Hold accounting and OpenAPI 0.7.0.
- Fixed-seed synthetic 18-test-case capital results: single 8261.67,
  second 8433.21, fusion 8569.93, random 8881.15,
  buy-hold 11819.58 and no-trade 10000 (all start at 10000).
  These are NOT real portfolio performance or evidence of predictive skill.
- Full synthetic JSON fixture is available in
  examples/stock-demo-007-seed42.json.

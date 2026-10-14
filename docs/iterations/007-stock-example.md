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

## Acceptance state
Actual local verification observed on 2026-10-14 in Hub-isolated worktree:
- dotnet restore RandSystem1.slnx successful.
- Release build --warnaserror: 0 errors and 0 warnings.
- Full xUnit suite: 135/135 green, 0 skipped (original 117 + 18 new tests).
- Real ASP.NET HTTP: 13/13 cases, including default seed, identical replay,
  different datasets on changed seed, costs, invalid seed/negative/NaN fee,
  no-trade, accurate continuous buy-hold, OpenAPI 0.7.0 (13 paths).
- Seed42 has 18 heldout decisions. Sample final capital on fully SYNTHETIC
  data: single 8261.67; second 8433.21; fusion 8569.93; random 8881.15;
  buy-hold 11819.58; no-trade 10000. This is intentionally not interpreted
  as investment outperformance. Exact full generated JSON is committed.
- GitHub CI, actual commit, artifacts and Hub review are separate later gates.

Never mark Done or merge before independent Verification, Review and human
final confirmation.

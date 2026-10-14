# Iteration 006 - Walk-forward backtesting

## Goal
Deliver a reproducible chronological paper backtesting increment.

## Implemented scope
- Strict time-series validation (unique ID, ascending signal time, future
  label-end, valid prices), chronological expanding/rolling windows,
  configurable embargo gaps and label-horizon purge.
- Explicit train/validation/purged/embargo/test memberships and no overlapping
  test positions. Missing data or signals fail, never silently imputed.
- Unified single/rule/fusion strategy positions (externally frozen as-of
  decision), seed-replay random simulation and true zero ABSTAIN.
- Two-sided simulated fees/slippage, round-trip per test bar, bounded
  exposure, compounded cash with bankruptcy floor, win rate/trades, turnover,
  cumulative returns, peak drawdown, illustrative Sharpe and annualization.
- Pure C# Core algorithm, HTTP endpoint /api/backtest/run, OpenAPI 0.6.0.

## Files
- src/RandSystem1.Core/Backtesting/WalkForwardBacktester.cs
- tests/RandSystem1.UnitTests/WalkForwardBacktesterTests.cs
- src/RandSystem1.Api/Program.cs and openapi.json
- docs/API.md, docs/STATISTICAL_METHODS.md, docs/ARCHITECTURE.md

## Verification approach
dotnet restore RandSystem1.slnx
dotnet build RandSystem1.slnx -c Release --warnaserror
dotnet test RandSystem1.slnx -c Release
Run real HTTP smoke on demo data for successful multi-strategy cases and
HTTP400 future/duplicate/invalid time, prices and parameters.

## Caveats
This is PAPER simulation only. No actual money trading or order APIs.
External strategy signals cannot be authenticated as truly point-in-time;
fittedUntil/decidedAt are only declared timestamps. Price series must handle
corporate actions externally. Every test interval is closed; no rolling
positions or market microstructure. Time-series uncertainty, daily 252-bar
Sharpe assumptions and execution costs need independent validation.
Data splits are based on observation bars rather than calendar durations.
Random strategy exposure/trade count may differ from other policies.

## Verified local evidence (2026-10-14)
- dotnet restore RandSystem1.slnx: completed successfully.
- dotnet build RandSystem1.slnx -c Release --warnaserror: 0 errors and 0 warnings.
- dotnet test RandSystem1.slnx -c Release: 117/117 passed, 0 skipped (19 new).
- Real Development ASP.NET HTTP smoke: 13/13, including health, OpenAPI
  0.6.0 12 paths, valid 4-strategy run, invalid futures/missing/duplicate IDs,
  bad price, overlapping tests, negative fees, falsely non-simulated output,
  missing dataset version, and a genuine purged historical label.
- Deterministic accounting: capital 1000, two 6% net intervals, final 1123.60.
- These are reproducible local engineering checks, not statistical proof of financial performance.

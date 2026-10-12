# Iteration 003 — Statistical evaluation

## Goal
Deliver statistical evaluation as a runnable, independently tested increment.

## Scope
Accuracy, Brier, log loss, skill score, Monte Carlo, interval estimation and valid matched/paired significance tests.

## Technical approach
Use .NET 10 modular-monolith architecture; keep algorithm dependencies separate from HTTP and persistence. Pin versions, seeds and dataset splits where relevant; never use future information. Ensure the previous iteration stays green.

## Key code locations
- src/RandSystem1.Core/Statistics/ClassificationMetrics.cs
- src/RandSystem1.Core/Statistics/MonteCarloEvaluation.cs
- src/RandSystem1.Core/Statistics/StatisticalInference.cs
- src/RandSystem1.Api/Program.cs and openapi.json; docs/API.md
- tests/RandSystem1.UnitTests/StatisticalEvaluationTests.cs

## Test method
Analytic score fixtures, chance-model false-positive checks, Monte Carlo reference, fixed-seed reproducibility. Run dotnet build and dotnet test, plus endpoint/CLI integration testing when introduced.

## Acceptance results
Core implementation complete in Hub Run Worktree. Initial tests passed 63/63 including Iterations 001-002, full classification metrics and probability losses, stable zero-probability log loss, aligned baseline skill, ECE/reliability, Monte Carlo null distribution and deterministic replay, analytic binomial tests, paired IID bootstrap/exact or randomized swap permutation, explicit-time-series moving block bootstrap, invalid data and NaN guards. Final local verification: Release build with --warnaserror succeeded with 0 warnings and 0 errors; 63/63 tests passed, no skips. Live HTTP smoke verified GET health and OpenAPI 0.3.0, POST /api/evaluation/run (perfect binary accuracy and Brier skill), seeded /monte-carlo 1000 draws with exact replay and null distribution, paired IID and correlated timeseries comparisons, exact 10-of-10 binomial tail, and seven invalid requests returning HTTP 400. Prior /api/random/generate also passed regression smoke. Remote GitHub CI and independent Hub Verification/Review and human final confirmation remain separate gates.

## Known issues
002 was verified, reviewed, confirmed and its Worktree merged into main before this Run. Implementation computes statistics ONLY from caller-supplied held-out labels. The caller must enforce time splits, chronology and data provenance, calibrate models out-of-sample, and explicitly state IID or time-series assumptions. No population generalization is established by simulated Monte Carlo null percentiles. Exact binomial inference is disabled without explicit IID confirmation. Repeated-test best-of-many selection and optional Bonferroni comparisons must be handled honestly.

## Next stage
Run full Release build and actual HTTP smoke, update final evidence, commit and push the Run-level Worktree, verify GitHub Actions and register artifacts; submit for independent Hub Verification/Review and human final confirmation. Iteration 004 must not start before this gate.

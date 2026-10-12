# Iteration 002 — Random baselines

## Goal
Deliver random baselines as a runnable, independently tested increment.

## Scope
Uniform probability and independent sampling; Dirichlet concentration and seed; training-only majority/prior; multi-seed random ensembles.

## Technical approach
Use .NET 10 modular-monolith architecture; keep algorithm dependencies separate from HTTP and persistence. Pin versions, seeds and dataset splits where relevant; never use future information. Ensure the previous iteration stays green.

## Key code locations
- Pure C# core and fixed PRNG: src/RandSystem1.Core/Random/BaselineEngine.cs
- HTTP: src/RandSystem1.Api/Program.cs (POST /api/random/generate)
- API schema and guide: src/RandSystem1.Api/openapi.json, docs/API.md
- Tests: tests/RandSystem1.UnitTests/RandomBaselineTests.cs

## Test method
Exact uniform sums, seed reproducibility, valid Dirichlet probabilities, majority on imbalanced training labels. Run dotnet build and dotnet test, plus endpoint/CLI integration testing when introduced.

## Acceptance results
Implementation complete in Run Worktree; 43/43 local unit tests passed (including 001 regressions). Covers seed replay, categorical draws, independent member seeds, uniform long-run frequency, Dirichlet numerical boundaries, majority/prior on training-only data, batch validation and cancellation. Final local verification: Release build succeeded with 0 warnings and 0 errors; 43/43 tests passed, 0 skipped. Live HTTP integration test generated 70 draws for each of uniform, dirichlet, majority and prior (280 draws total), verified identical seed replay for each type, independent member seeds, probabilities normalized to 1, simulation metadata, and correct majority/prior. Seven bad inputs returned HTTP 400. Health and OpenAPI routes succeeded and schema declared version 0.2.0. Remote GitHub CI, independent Hub review and human confirmation remain separate gates.

## Known issues
001 was Verified/Reviewed/Done in Hub and merged into main before this Run. Caller must enforce chronological training/test split; the API cannot infer it from labels. Engine version 0.2.0 pins internal SplitMix64 seed replay. No result should be mistaken for real-world AI accuracy.

## Next stage
Finish build and API smoke, commit in Run Worktree, verify remote CI, register evidence and submit to Hub. Only after independent verification and human confirmation start 003.

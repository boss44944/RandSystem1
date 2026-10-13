# Iteration 004 — Model complementarity

## Goal
Deliver model complementarity as a runnable, independently tested increment.

## Scope
Joint correctness/error contingency, disagreement, error association and oracle upper bound.

## Technical approach
Use .NET 10 modular-monolith architecture; keep algorithm dependencies separate from HTTP and persistence. Pin versions, seeds and dataset splits where relevant; never use future information. Ensure the previous iteration stays green.

## Key code locations
- Core: src/RandSystem1.Core/Statistics/ModelComplementarity.cs
- HTTP: POST /api/evaluation/complementarity in src/RandSystem1.Api/Program.cs
- Schema/docs: src/RandSystem1.Api/openapi.json and docs/API.md
- Tests: tests/RandSystem1.UnitTests/ModelComplementarityTests.cs

## Test method
Identical models show no complementary correct cases; contrasting errors and oracle formula validated. Run dotnet build and dotnet test, plus endpoint/CLI integration testing when introduced.

## Acceptance results
Initial local implementation and tests: 77/77 pass, including all previous iterations. A 10-case analytical fixture proves 5 both right, 2 A-only, 1 B-only, 2 both wrong, A=70%/B=60%, 30% disagreement and 80% hindsight Oracle. Other unit tests cover identical-model zero gain, anticorrelated errors, undefined zero-variance Phi, multi-model global coverage, unlabeled-only disagreement, strict ID/order matching, bad inputs and cancellation. Final Release build, real HTTP smoke, GitHub CI and independent Hub review remain separate gates.

## Known issues
003 is Done, verified/reviewed, merged and released. This analyzer evaluates common held-out cases, never trains fusion with test labels. No truth -> no accuracy/Oracle claims. Oracle is hindsight upper bound, not an achievable selection strategy. Error Phi is null for zero variance. Request limits cap at 20 models, 20000 cases, 4 million pairwise comparisons.

## Next stage
Finish Release build, HTTP smoke, source commit/push, CI evidence, Hub artifacts and Submit; independent Verification/Review and human final confirmation must follow before Iteration 005.

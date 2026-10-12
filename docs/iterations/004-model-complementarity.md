# Iteration 004 — Model complementarity

## Goal
Deliver model complementarity as a runnable, independently tested increment.

## Scope
Joint correctness/error contingency, disagreement, error association and oracle upper bound.

## Technical approach
Use .NET 10 modular-monolith architecture; keep algorithm dependencies separate from HTTP and persistence. Pin versions, seeds and dataset splits where relevant; never use future information. Ensure the previous iteration stays green.

## Key code locations
Planned: src/RandSystem1.* components corresponding to this iteration and tests/RandSystem1.UnitTests (additional statistical/integration tests as needed). Actual filenames will be updated when implemented.

## Test method
Identical models show no complementary correct cases; contrasting errors and oracle formula validated. Run dotnet build and dotnet test, plus endpoint/CLI integration testing when introduced.

## Acceptance results
NOT STARTED. No build/test claim for this iteration.

## Known issues
Dependent on successful verification and review of preceding iterations. Technical decisions remain open until implementation.

## Next stage
Complete acceptance evidence, commit and submit through Agent Hub; then begin the next numbered iteration.

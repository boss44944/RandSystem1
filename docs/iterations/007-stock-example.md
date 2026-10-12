# Iteration 007 — Stock example

## Goal
Deliver stock example as a runnable, independently tested increment.

## Scope
Simulated five-day UP/DOWN workflow and controlled comparison with random, buy/hold and no-trade.

## Technical approach
Use .NET 10 modular-monolith architecture; keep algorithm dependencies separate from HTTP and persistence. Pin versions, seeds and dataset splits where relevant; never use future information. Ensure the previous iteration stays green.

## Key code locations
Planned: src/RandSystem1.* components corresponding to this iteration and tests/RandSystem1.UnitTests (additional statistical/integration tests as needed). Actual filenames will be updated when implemented.

## Test method
Matched stock pool, period, costs and risk exposure; labeled simulated dataset; reproducible report. Run dotnet build and dotnet test, plus endpoint/CLI integration testing when introduced.

## Acceptance results
NOT STARTED. No build/test claim for this iteration.

## Known issues
Dependent on successful verification and review of preceding iterations. Technical decisions remain open until implementation.

## Next stage
Complete acceptance evidence, commit and submit through Agent Hub; then begin the next numbered iteration.

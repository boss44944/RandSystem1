# Iteration 008 — External model adapters

## Goal
Deliver external model adapters as a runnable, independently tested increment.

## Scope
Generic HTTP integration and verified Jev/Laya interfaces, model version logs and explicit failure/timeout semantics.

## Technical approach
Use .NET 10 modular-monolith architecture; keep algorithm dependencies separate from HTTP and persistence. Pin versions, seeds and dataset splits where relevant; never use future information. Ensure the previous iteration stays green.

## Key code locations
Planned: src/RandSystem1.* components corresponding to this iteration and tests/RandSystem1.UnitTests (additional statistical/integration tests as needed). Actual filenames will be updated when implemented.

## Test method
Contract tests against documented adapter behavior, no fabricated model API or performance. Run dotnet build and dotnet test, plus endpoint/CLI integration testing when introduced.

## Acceptance results
NOT STARTED. No build/test claim for this iteration.

## Status
Planned. This specification describes intended functionality, not
an already implemented or verified capability. New components must
be independently tested against the existing .NET 10 library and API.

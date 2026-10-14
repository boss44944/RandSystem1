# Iteration 002 - Random baselines

## Goal
Deliver reproducible model-independent random and training-fitted baselines.

## Scope
- Uniform probabilities vs actual categorical sampling.
- Dirichlet random probabilities with configurable concentration.
- Majority and Prior fitted only on caller-provided training labels.
- Independent seeded random ensemble members using a stable SplitMix64 PRNG.

## Implementation
- src/RandSystem1.Core/Random/BaselineEngine.cs
- POST /api/random/generate in src/RandSystem1.Api/Program.cs
- Tests: tests/RandSystem1.UnitTests/RandomBaselineTests.cs
- OpenAPI and docs/API.md

## Verification record
Release build: 0 warnings/0 errors; 43/43 xUnit tests passed, including
existing tests. Fixed-seed replay, independent ensemble member seeds,
Dirichlet numerical cases, probabilities summing to one, training-only
majority/prior, invalid inputs and cancellation were tested. Real HTTP
smoke generated 70 draws for each of four baseline types, matched seed
replays, normalized probabilities, checked simulation metadata and saw
seven invalid requests return HTTP 400.

## Limitations
The caller is responsible for clean chronological training/evaluation
splits. Simulated draws are not measurements of real model accuracy.

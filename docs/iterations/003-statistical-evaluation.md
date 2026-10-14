# Iteration 003 - Statistical evaluation

## Goal
Deliver classification/probability scoring, simulated null distributions
and statistically appropriate comparisons.

## Scope
- Classification and probability metrics, accuracy, balanced accuracy,
  precision/recall/F1, Brier, log loss, skill and ECE/calibration.
- Fixed-seed Monte Carlo comparisons against random baselines.
- Paired bootstrap, conditional permutation comparison, IID exact binomial,
  and block bootstrap when time-series assumptions are explicitly declared.

## Implementation
- src/RandSystem1.Core/Statistics/ClassificationMetrics.cs
- src/RandSystem1.Core/Statistics/MonteCarloEvaluation.cs
- src/RandSystem1.Core/Statistics/StatisticalInference.cs
- src/RandSystem1.Api/Program.cs, docs/API.md, OpenAPI
- tests/RandSystem1.UnitTests/StatisticalEvaluationTests.cs

## Verification record
Release build: 0 warnings/0 errors; 63/63 xUnit tests passed with prior
regressions. HTTP smoke covered health and OpenAPI, evaluation scoring,
seeded Monte Carlo 1000-draw replay, paired IID and time-series cases,
a known exact binomial probability and seven bad inputs returning HTTP 400.

## Statistical limitations
Scoring requires actual aligned labels and dataset provenance.
IID significance assumptions cannot be silently applied to correlated
time-series data. Approximate Monte Carlo extreme values, selective
comparison and repeated tests cannot establish independent model skill.

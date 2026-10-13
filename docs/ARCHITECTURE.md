# Architecture / 架构

## Scope and principles

RandSystem1 is a modular monolith for **Random → Evaluate → Decide**. The decision algorithms will remain independent of ASP.NET and persistence, so they can eventually be packaged as a standalone NuGet library. No frontend, Kubernetes, queue, or live stock trading is required in v1.

## Iteration 001 implemented structure

- RandSystem1.Abstractions: versioned decision request/result and async/cancellable IDecisionModel.
- RandSystem1.Core: contract/prediction validation, without web or database dependencies.
- RandSystem1.Infrastructure: a deliberately **simulated** mock adapter; later external adapters belong here.
- RandSystem1.Api: health, model discovery and prediction endpoints.
- RandSystem1.UnitTests: deterministic validation and mock-contract tests.

## Planned components (not yet implemented)

- RandSystem1.Random: uniform, Dirichlet, majority, prior and random ensembles.
- RandSystem1.Statistics / Evaluation: scores, Monte Carlo, appropriate paired hypothesis tests, calibration and complementarity.
- RandSystem1.Fusion: equal/static/performance/dynamic weights, decision gate, abstention and risk policy.
- RandSystem1.Backtesting: walk-forward, purging/embargo, costs and realistic risk reporting.
- RandSystem1.Cli: reproducible experiments and command-line access.
- PostgreSQL and Serilog integration: later production iteration.

## Reliability and reproducibility

Models must report their ID, version, timestamp, task type and an exact probability for each candidate. Reject NaN/Infinity, invalid sums, and extra/missing labels. Adapters must surface failure, timeout and missing outputs explicitly. Experimental and real results must be distinguishable. Future benchmark seeds, dataset hashes, split rules and model versions must be recorded.

## Layering

Abstractions <- Core <- Infrastructure; API composes the layers. No core references to API, PostgreSQL, or ASP.NET. No real model endpoints should be invented. Tests must not require GPU hardware.

## Iteration 005: decision fusion

Statistical fusion and labeled holdout comparisons reside in `RandSystem1.Core.Statistics`, independent of HTTP, persistence and models. The API only maps requests and reports bounded validation errors. Model predictions remain externally supplied; no Jev/Laya adapter is simulated. A strategy must be frozen before later evaluation, and business execution gates default to ABSTAIN unless earlier validation evidence and explicit risk thresholds pass. API-level validation of caller-declared split/time is **not** independent proof of data provenance.

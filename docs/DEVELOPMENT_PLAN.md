# Development roadmap / 开发路线图

RandSystem1 is a model-independent .NET 10 framework for reproducible random
baselines, statistical evaluation, risk-aware model fusion and simulated
decision backtesting. The source repository records algorithms, APIs, tests,
statistical assumptions and public project milestones.

## Iterations

| Iteration | Scope | Implementation status |
|---|---|---|
| 001 | .NET 10 solution, model contract, mock adapter, API, tests, multilingual docs | Implemented |
| 002 | Uniform/Dirichlet, majority/prior, ensembles, seeded replay | Implemented |
| 003 | Classification/probability scores, calibration, Monte Carlo, statistical tests | Implemented |
| 004 | Joint model errors, disagreement, error association, Oracle upper bound | Implemented |
| 005 | Equal/static/validation-weighted fusion, ABSTAIN, risk policy | Implemented |
| 006 | Chronological Walk-Forward, purge/embargo, costs, simulated returns and risk | Implemented |
| 007 | Fully synthetic five-session UP/DOWN stock strategy comparison | Implemented |
| 008 | Model-independent HTTP adapter, only then verified Jev/Laya integrations | Planned |
| 009 | Docker deployment, persistence, asynchronous jobs, logs and performance | Planned |

These statuses describe **code functionality**, not proof of real model
predictive skill or production readiness. The simulated stock example is
not a real-money trading system. Never substitute randomly generated
predictions for genuine labeled evaluation. Tests and experiment metadata
must clearly separate training, validation and future evaluation data.

## Contributing and validation

Before changing algorithms, read [Architecture](ARCHITECTURE.md),
[Statistical methods](STATISTICAL_METHODS.md), [Model protocol](MODEL_PROTOCOL.md)
and the corresponding [iteration specification](iterations/).
Preserve the library/API dependency boundary and fixed-seed replay.
Do not publish model-accuracy claims without true labels, aligned held-out
examples and suitable statistical inference.

Run the public validation commands:

    dotnet restore RandSystem1.slnx
    dotnet build RandSystem1.slnx -c Release --warnaserror
    dotnet test RandSystem1.slnx -c Release

See [API.md](API.md) for endpoints and a reproducible artificial demonstration.

# API / 接口

Endpoints implemented through Iteration 002:

| Method | Path | Behavior |
|---|---|---|
| GET | /api/health | Returns service health |
| GET | /api/models | Lists available model adapters and simulation flag |
| POST | /api/models/predict?modelId=mock-uniform | Accepts DecisionRequest JSON and returns DecisionPrediction |
| POST | /api/random/generate | Generate reproducible Uniform, Dirichlet, training-fitted Majority/Prior baselines and independent ensemble members |

Invalid request returns HTTP 400; an unknown model returns 404. A faulty model output is rejected with HTTP 502. An OpenAPI JSON endpoint is exposed at /openapi/v1.json in development mode.

The following routes are **planned, not implemented**: /api/evaluation/run, /api/evaluation/monte-carlo, /api/evaluation/complementarity, /api/fusion/predict, /api/backtest/run, /api/backtest/{id}. Long-running jobs, database persistence and CLI are later iterations.

## Iteration 002: random baseline simulation

Endpoint: POST /api/random/generate

Dirichlet ensemble example, 10 independent models and 3 samples per model:

    curl -sS -X POST http://127.0.0.1:5078/api/random/generate -H 'Content-Type: application/json' -d '{"baseline":"dirichlet","options":["UP","DOWN","FLAT"],"alpha":1.0,"seed":42,"count":10,"samplesPerModel":3}'

Training-only prior example (do not include validation/test/future labels):

    {"baseline":"prior","options":["UP","DOWN"],"trainingLabels":["UP","UP","UP","DOWN"],"seed":42,"count":1,"samplesPerModel":5}

Allowed baseline types: uniform, dirichlet, majority, prior (case-insensitive).
Alpha must be finite and positive for Dirichlet. Count range is 1..200;
samplesPerModel range is 1..2000, with at most 20,000 draws overall.
Options must contain 1..256 distinct nonempty strings. Fitted baselines
require 1..100000 training labels which must belong to the option set;
random baselines reject training labels altogether. Invalid arguments yield HTTP 400.

The response records engineVersion, seed, count, samplesPerModel, optional alpha,
trainingLabelCount, isSimulation=true, probabilityMeaning, and members.
Each indexed member exposes a stable hexadecimal seed, modelId, and draws.
Each draw separates the probability vector from the categorical sampledOption.
Majority is a deterministic one-hot decision; ties use input option order.
Prior samples from empirical training proportions.

Results reproduce for identical seed, input and engine version. PRNG is
SplitMix64, not cryptographically secure. Random baseline probabilities do
not represent calibrated AI confidence or empirical prediction accuracy.
The caller, not this endpoint, must enforce chronological training/test
splits; the endpoint never accepts ground-truth test labels. Monte Carlo
assessment, hypothesis testing, and comparisons are deferred to Iteration 003.
Do not select only the best random member as an honest baseline.

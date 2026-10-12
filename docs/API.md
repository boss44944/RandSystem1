# API / 接口

Only the following endpoints are implemented in Iteration 001:

| Method | Path | Behavior |
|---|---|---|
| GET | /api/health | Returns service health |
| GET | /api/models | Lists available model adapters and simulation flag |
| POST | /api/models/predict?modelId=mock-uniform | Accepts DecisionRequest JSON and returns DecisionPrediction |

Invalid request returns HTTP 400; an unknown model returns 404. A faulty model output is rejected with HTTP 502. An OpenAPI JSON endpoint is exposed at /openapi/v1.json in development mode.

The following routes are **planned, not implemented**: /api/random/generate, /api/evaluation/run, /api/evaluation/monte-carlo, /api/evaluation/complementarity, /api/fusion/predict, /api/backtest/run, /api/backtest/{id}. Long-running jobs, database persistence and CLI are later iterations.

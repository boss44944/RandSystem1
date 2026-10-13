# API / 接口

Endpoints implemented through Iteration 004:

| Method | Path | Behavior |
|---|---|---|
| GET | /api/health | Returns service health |
| GET | /api/models | Lists available model adapters and simulation flag |
| POST | /api/models/predict?modelId=mock-uniform | Accepts DecisionRequest JSON and returns DecisionPrediction |
| POST | /api/random/generate | Generate reproducible Uniform, Dirichlet, training-fitted Majority/Prior baselines and independent ensemble members |
| POST | /api/evaluation/run | Evaluate labeled test observations (classification, probability, calibration and skill) |
| POST | /api/evaluation/monte-carlo | Generate fixed-seed random accuracy null distribution and conditional upper-tail estimate |
| POST | /api/evaluation/paired | Paired model-versus-baseline loss comparison, bootstrap and guarded significance |
| POST | /api/evaluation/binomial | Exact binomial upper-tail under explicitly confirmed IID Bernoulli assumptions |
| POST | /api/evaluation/complementarity | Strictly aligned joint errors, prediction disagreement, error Phi and Oracle upper bound |

Invalid request returns HTTP 400; an unknown model returns 404. A faulty model output is rejected with HTTP 502. An OpenAPI JSON endpoint is exposed at /openapi/v1.json in development mode.

The following routes are **planned, not implemented**: /api/fusion/predict, /api/backtest/run, /api/backtest/{id}. Long-running jobs, database persistence and CLI are later iterations.

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
splits; the endpoint never accepts ground-truth test labels. Monte Carlo assessment, hypothesis testing, and comparisons are available via the Iteration 003 evaluation endpoints.
Do not select only the best random member as an honest baseline.

## Iteration 003: evaluation, simulation, and valid statistical inference

All evaluation requests MUST contain held-out ground-truth labels. A model
probability vector is not by itself a measured confidence or a proof of accuracy.

### POST /api/evaluation/run

Example with a binary balanced evaluation and aligned random baseline:

    {
      "options":["UP","DOWN"],
      "calibrationBins":10,
      "observations":[
        {"observationId":"t1","actual":"UP","predicted":"UP","probabilities":{"UP":0.9,"DOWN":0.1}},
        {"observationId":"t2","actual":"DOWN","predicted":"DOWN","probabilities":{"UP":0.2,"DOWN":0.8}}
      ],
      "baselineObservations":[
        {"observationId":"t1","actual":"UP","predicted":"UP","probabilities":{"UP":0.5,"DOWN":0.5}},
        {"observationId":"t2","actual":"DOWN","predicted":"UP","probabilities":{"UP":0.5,"DOWN":0.5}}
      ]
    }

Results: accuracy, balancedAccuracy, macroPrecision, macroRecall, macroF1,
perClass (precision/recall/F1 and support), confusionMatrix actual-to-predicted,
multiclass Brier (sum of squared class errors per observation), logLoss (clamp
true-class probability to 1e-15 for finite loss), ece, reliability bins,
and optional skill scores. Macros average classes with positive truth support;
zero predicted counts yield precision=0, absent truth support has null recall.
The ECE bins use reported-choice confidence, rather than assuming it has been
calibrated. Bins with zero observations have count=0, not meaningful calibration.
Brier/log-loss skill is 1-modelLoss/baselineLoss on the same test cases; zero
baseline loss returns null skill. Every observationId must be unique, matched,
ordered and ground truth identical when a baseline is supplied.

### POST /api/evaluation/monte-carlo

    {
      "options":["UP","DOWN"],
      "testLabels":["UP","DOWN","UP","UP","DOWN"],
      "baseline":"uniform","iterations":1000,"seed":42,
      "observedPredictions":["UP","DOWN","UP","DOWN","DOWN"]
    }

Options can be 1..256, test labels 1..2000, iteration count 20..10000, and
iterations * sample count <= 1,000,000. Majority/prior require separate
trainingLabels and cannot access test labels for fitting. Each simulated
member answers the SAME test questions. Output contains meanAccuracy,
standardDeviation, normal-approximation 95% Monte Carlo mean error interval,
nullPercentile05, nullMedian, nullPercentile95, bestObservedRandomAccuracy,
optional modelObservedAccuracy and empiricalUpperTailP
((number random runs >= observed + 1)/(iterations + 1)), plus seed,
engineVersion and reproducible randomAccuracies. The best observed random
run is NOT the expected baseline performance. Null percentiles are NOT a
confidence interval on a real-world population accuracy.

### POST /api/evaluation/paired

    {
      "options":["UP","DOWN"], "loss":"brier",
      "sampleStructure":"iid", "replications":2000,
      "seed":42, "comparisons":3,
      "model":[{"observationId":"t1","actual":"UP","predicted":"UP","probabilities":{"UP":0.9,"DOWN":0.1}}],
      "baseline":[{"observationId":"t1","actual":"UP","predicted":"UP","probabilities":{"UP":0.5,"DOWN":0.5}}]
    }

The samples MUST match one-to-one by ID, truth and order. Improvement is the
positive reduction in average loss (baseline minus model). IID requires
explicit sampleStructure="iid": paired bootstrap percentile interval and
two-sided model-label-swap permutation p-value (exact for <=15 pairs, otherwise
seeded randomized with add-one correction); the Bonferroni-adjusted p-value
requires the caller to state the number of simultaneous comparisons.
Paired label swapping requires exchangeability under the null, and IID pairs.
For ordered correlated observations choose sampleStructure="timeseries",
chronologicalOrderConfirmed=true and blockLength >= 2. The latter uses
a circular moving-block bootstrap confidence interval and returns NULL
permutation p-values. The caller must prevent temporal leakage; selecting
blockLength and checking dependence are analyst responsibilities. Both modes
report sample count, effect size and methodological assumptions.

### POST /api/evaluation/binomial

    {"successes":9,"trials":10,
     "fixedNullSuccessProbability":0.5,
     "independentTrialsConfirmed":true}

Reports the exact one-sided upper tail P(X >= successes), using stable
log-space binomial summation. **Rejects** absent independence confirmation.
Only valid if trials are iid Bernoulli with a known fixed null probability;
do not use this directly for time-series, unequal choice counts, selected
best-of-many models or unlabeled predictions.

**Security / honesty:** these endpoints do NOT train a classifier, verify
chronological splits, prove calibration, trade stocks or produce real
Jev/Laya predictions. Use controlled held-out evaluation and independently
verifiable data provenance.

## Iteration 004: POST /api/evaluation/complementarity

Pass 2-20 named models with predictions on exactly the SAME held-out samples.
Every model and optional groundTruth must have identical observationId/order
and valid candidate labels. At most 20000 observations, 256 choices and
4 million pairwise observation comparisons; invalid/misaligned requests
return HTTP 400.

Labeled example:

    {
      "options": ["UP", "DOWN"],
      "models": [
        {"modelId":"A","predictions":[{"observationId":"1","predictedOption":"UP"},{"observationId":"2","predictedOption":"DOWN"}]},
        {"modelId":"B","predictions":[{"observationId":"1","predictedOption":"DOWN"},{"observationId":"2","predictedOption":"DOWN"}]}
      ],
      "groundTruth":[{"observationId":"1","actual":"UP"},{"observationId":"2","actual":"DOWN"}]
    }

Labeled report: per-model accuracy / prediction class counts; each unordered
pair's bothCorrect, bothWrong, onlyACorrect, onlyBCorrect, agreement/
disagreement counts and rate, jointCorrectCoverage / oracleUpperBound,
errorPhiCorrelation and hindsight gain over the best single model. Error Phi
is null for zero-variance error indicators (always-right/always-wrong).
The global oracleUpperBound includes all model choices.

Oracle selects the correct model's answer AFTER observing the ground truth.
It is a theoretical hindsight ceiling, NOT deployable fusion accuracy. It is
invalid to tune fusion weights using these held-out labels.

Unlabeled mode (omit groundTruth): returns only per-model prediction counts,
model-pair agreement and disagreement; every accuracy/error/oracle field is
null. Disagreement is NOT evidence of superiority to random guessing.

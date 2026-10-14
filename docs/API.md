# API / 接口

Endpoints implemented through Iteration 007:

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
| POST | /api/fusion/predict | Equal/static/performance/dynamic fusion with ABSTAIN and business gates |
| POST | /api/fusion/holdout | Descriptive evaluation of frozen fusion weights on later labeled cases vs single models/uniform baseline |
| POST | /api/backtest/run | Time-ordered paper backtest, purged/embargoed split, fixed costs, strategy/risk comparison |
| GET | /api/examples/stock/compare?seed=42 | Seed-replayable synthetic 5-session UP/DOWN investment comparison |

Invalid request returns HTTP 400; an unknown model returns 404. A faulty model output is rejected with HTTP 502. An OpenAPI JSON endpoint is exposed at /openapi/v1.json in development mode.

The following routes are **planned, not implemented**: /api/backtest/{id}. Long-running jobs, database persistence and CLI are later iterations.

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

## Iteration 005: decision fusion

### POST /api/fusion/predict

Request `options` contains 1..256 unique categories, and `models` contains
1..20 distinct model IDs. Every model must state `status`: `success`,
`missing`, `failed`, `timeout`, `invalid` or `cancelled`.
A `success` requires a finite, normalized probability vector; corrupt vectors
are exposed as invalid and force ABSTAIN. Partial ensemble execution is NOT
silently allowed. Supported `strategy` names: `equal`, `static`,
`performance`, `dynamic`. Static weights cover every model and are
normalized over currently usable models, but a failure still blocks execution.

`performance` weights are proportional to positive Brier Skill
`max(0, 1 - brierLoss/baselineBrierLoss)` computed solely from caller-supplied
past `validation` data, with minimum samples and a positive conservative
`lowerSkillBound`. `dynamic` additionally requires a matching `segment`.
Neither accepts training/test evidence as weight-selection data.

The decision gate requires **independent, earlier validation of the frozen
fusion strategy**, not just validation of its constituent models:
`strategyEvidence` names the exact strategy, segment, model IDs and
normalized `frozenWeights`, past `validationEndedAt`, sample count and
a conservative positive `lowerBrierSkillBound`. Inconsistent, missing or
future evidence causes ABSTAIN. Probabilities remain *model-reported*, not
measured skill or calibrated business odds. Every evidence row is supplied
by the caller and its real provenance MUST be audited externally; an API
field alone does not certify that results are genuinely out-of-sample.

Example deterministic equal-weight request (illustrative reported predictions,
NOT measured real-model performance; replace synthetic evidence with genuinely
independently established validation evidence before production decisions):

```json
{
  "requestId": "decision-01",
  "taskType": "direction",
  "options": ["UP", "DOWN"],
  "models": [
    {"modelId":"A","status":"success","probabilities":{"UP":0.9,"DOWN":0.1}},
    {"modelId":"B","status":"success","probabilities":{"UP":0.7,"DOWN":0.3}}
  ],
  "strategy": "equal",
  "decisionAt": "2026-10-13T12:00:00Z",
  "strategyEvidence": {
    "strategy":"equal","taskType":"direction","segment":null,
    "split":"validation","sampleCount":100,"lowerBrierSkillBound":0.03,
    "validationEndedAt":"2026-10-10T12:00:00Z",
    "modelIds":["A","B"],"frozenWeights":{"A":0.5,"B":0.5}
  },
  "risk":{"minConfidence":0.6,"minValidationSamples":30}
}
```

The response contains `decision` (`EXECUTE` or `ABSTAIN`),
`predictedOption`, `probabilities`, `confidence`, and per-model
`status/reason/weight/isSimulation`. ABSTAIN may still report a
descriptive probability vector, but **must not** be treated as authorization
to act. Simulated predictions default to ABSTAIN. Risk thresholds support
`minExpectedBenefit`, `maxPotentialLoss`, `maxExposure`; when configured,
corresponding request values are mandatory and violating limits blocks
execution. No actual orders or external side effects are performed.

### POST /api/fusion/holdout

This endpoint scores a fixed, **already selected** `frozenWeights` vector
with `weightSourceSplit="validation"` and `weightsFrozenAt` earlier than
every labeled observation `observedAt`. Each later test case has a unique
`observationId`, a true `actual` option, and exactly aligned
`modelProbabilities` by model ID. It computes the fixed fusion's accuracy
and multiclass Brier loss alongside every single model and the theoretical
uniform random-distribution baseline. `bestSingleInHindsight` is selected
*after seeing holdout labels only for a descriptive comparator*, **never**
for assigning weights or live predictions. The report allows negative
`fusionAccuracyMinusBestSingle`; no improvement or significance is assumed.
This first increment does not perform temporal purge/embargo or block bootstrap;
the full walk-forward engine is planned in Iteration 006.

Validation on the same sample as weight fitting, even if called
`validation`, overestimates strategy advantage. Pin data source, strategy,
model version, validation time cutoffs, calibration protocol, experiment seed
and holdout split independently when using these endpoints.


## Iteration 006: POST /api/backtest/run

Pass a versioned historical dataset: datasetVersion, isSimulation=true,
strictly time-ordered bars with unique observationId, signalAt, labelEndAt
(outcome availability, strictly after signalAt), positive entryPrice/exitPrice
and optional strategy signals. All strategies use the same test windows.
API can validate caller-reported timing, not independently verify provenance.

Window fields: minimumTrainBars, validationBars, testBars, stepBars (at least
testBars), embargoBars (observation-count gaps), expandingTrain. Expanding
and rolling windows move chronologically. Training labels unavailable before
validation are purged; validation labels unavailable before test are purged.
Empty eligible partitions, duplicate IDs and overlapping test holding
intervals fail with HTTP 400. No implicit missing-value imputation.

Strategies: {strategyId,kind,seed}; kind is single, fusion, rule, random or
abstain. For the first three each held-out test bar MUST contain exactly one
corresponding externally supplied signal:
{strategyId,position,decidedAt,fittedUntil}. Require
fittedUntil <= decidedAt <= signalAt; position is finite with absolute
exposure <= configured maxAbsolutePosition. A random strategy uses seeded
SplitMix64 to generate +/- exposure; ABSTAIN has zero position and no trades.
There is no internal model fitting and no real order execution.

Each held-out bar represents one closed round-trip simulated position,
regardless of the previous bar. Gross return is
position * (exitPrice / entryPrice - 1). Both entry AND closing fees plus
slippage are charged: 2*abs(position)*(feeRate+slippageRate). Cash compounds
by max(0,1+netReturn); bankrupt portfolios stay at zero. The API returns
per-strategy equity curves, turnover, fees/slippage cost, win rate, trade
count, max drawdown, cumulative return, descriptive 252-bar annualized
return/Sharpe (null if not estimable). Trade counts represent round trips.

Minimal deterministic example: request using 10 historical daily bars
with contiguous dates and valid entry/exit prices, window settings
{minimumTrainBars:3,validationBars:2,testBars:2,stepBars:2,embargoBars:1},
costs {initialCapital:1000,feeRate:0.01,slippageRate:0.01},
and strategies [{strategyId:"r",kind:"random",seed:42},
{strategyId:"flat",kind:"abstain"}] tests bars at indexes 7 and 8.
Every bar must have signalAt, labelEndAt, entryPrice, exitPrice and
observationId, even if no model signals are used.

Limits: 5..20000 bars, 1..12 strategies, no overlapping test windows.
Calendar-irregular or highly dependent observations make the 252-bar
annualization invalid; price data should account for corporate actions and
real-world trading constraints before interpretation. Simulated random trades
may have different frequency/exposure from nonrandom strategies, so equal
fees alone do not establish fair risk-adjusted skill or causal improvement.
No actual trades occur and no historic profit guarantees future returns.


## Iteration 007 - Synthetic stock comparison

GET /api/examples/stock/compare?seed=42 (optional feeRate and slippageRate
default to 0.001 for EACH SIDE). Exactly 32 non-overlapping five-weekday
forecast periods, 161 deterministic artificial closing prices, one fixed
symbol SYNTHETIC-ONE, no actual equity prices, no Jev/Laya calls, and
no financial brokerage/execution. All dates are artificial weekdays; holidays
are not modeled.

Task: five subsequent trading sessions UP (>0) or DOWN (<=0). Every mock
model gets only prior-session price history, not current entry/exit prices.
Versions: mock-momentum5-v1, mock-momentum15-v1 and mock-unanimous-v1
(fusion requires agreement or abstains). Additional seeded random, actual
continuous buy-hold, and no-trade complete six strategies. Rules are
untrained mock heuristics, not empirically validated AI.

All modes share predeclared expanding chronological train/validation/test
windows: 8 initial training observations, 3 validation, 3 test, 3 step,
1 embargo per boundary. Each uses the identical 18 held-out observation
IDs, friction and initial capital (10000); exposure cap is 1. Single
and random both take one full-magnitude position each eligible period.
Fusion has reduced coverage when models disagree. Buy-hold stays long
continuously through contiguous test cases with ONLY one entry and exit,
while other strategies close every five-session period and pay round trip.
The latter exposure/frequency distinctions are reported, NOT mistaken
for equal risk budget or an advantage.

Response is marked isSimulation=true and includes every generated
observation, labels, predicted directions, model versions, fixed seed,
datasetVersion, SHA256 over artificial doubles, all split IDs and
all strategy equity curves. Statistics: accuracy/coverage, descriptive
Wilson binomial 95% intervals (null if no predictions), final capital,
returns, annualization scaled for five-session periods (252/5),
drawdown, turnover, transaction costs and trade count.

Wilson IID accuracy bounds are NOT reliable 95% uncertainty intervals
for serially correlated observations. This is a software example, not
evidence that any AI beats chance, fusion helps, or market returns are
repeatable. Seed-searching to pick a profitable synthetic past is
selection bias. Synthetic fixed symbol is not a survivorship-bias-free
real market universe. Execution uses simple fixed fees/slippage and
excludes market microstructure. No live trading.

The full HTTP-generated fixture is checked in as:
examples/stock-demo-007-seed42.json.

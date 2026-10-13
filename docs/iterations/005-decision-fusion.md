# Iteration 005 — Decision fusion

## Goal
Deliver auditable probability fusion, fail-closed decision gates and a
strictly later labeled holdout comparison in .NET 10.

## Implemented scope
- Equal, normalized static, validation-performance and segment-specific
  dynamic weights. Performance uses positive Brier Skill of each model from
  earlier, caller-supplied validation evidence.
- For each model, explicit success/failure/timeout/missing/invalid/cancelled
  status, chosen weight and provenance explanation. Invalid success output is
  marked invalid, never normalized or silently treated as success.
- Require separately supplied, previously validated **whole-strategy**
  lower Brier skill bound, sample size, segment, model list and exact frozen
  weights before issuing EXECUTE. Confidence is not a calibrated win rate.
- Conservative ABSTAIN for insufficient evidence, confidence, model failure,
  simulated output (unless explicitly allowed), or violated optional exposure,
  loss and benefit gates. No external trading, order placement or persistent
  data side effects.
- Separate fixed-weight holdout evaluator compares fusion accuracy/Brier to
  each single model and the uniform baseline on later uniquely aligned cases,
  without fitting any parameters on labels. Best-single is explicitly
  hindsight-only. Negative fusion gain is a valid result.

## Key code
- src/RandSystem1.Core/Statistics/DecisionFusion.cs
- src/RandSystem1.Core/Statistics/FusionHoldoutComparison.cs
- src/RandSystem1.Api/Program.cs
- src/RandSystem1.Api/openapi.json and docs/API.md
- tests/RandSystem1.UnitTests/DecisionFusionTests.cs

## Test method
Run `dotnet restore RandSystem1.slnx`,
`dotnet build RandSystem1.slnx -c Release --warnaserror`,
`dotnet test RandSystem1.slnx -c Release`, then actual HTTP smoke
for valid equal/static/dynamic, missing model, no skill, corrupt probability,
future/test evidence, risk gate, later holdout and HTTP 400 errors.

## Validation limitation
The service can verify caller-declared dataset split, time cutoff,
sample count, evidence/model/weight alignment, but cannot authenticate
self-reported evidence provenance. External data governance and genuine
out-of-sample validation are essential. The holdout's best-single is not an
oracle fusion strategy. Temporal purge, embargo, robust uncertainty and
true model adapters are later iterations.

## Acceptance results
Local final Release build passed with 0 warnings/errors after restore;
98/98 automated tests passed, including 21 new fusion and holdout tests.
Real ASP.NET HTTP smoke passed 13/13 scenarios: equal/static/dynamic
EXECUTE with supplied evidence, default ABSTAIN for no evidence,
timeouts, malformed success, future proof, and low risk confidence;
HTTP 400 for invalid configuration/holdout chronology; and a fixed
4-case holdout where fusion accuracy 50% trails best single 100%.
OpenAPI 0.5.0 route returned HTTP 200 and 11 documented paths.
Remote CI, Git evidence and independent Agent Hub verification/review
are separate gates pending submission.

## Next stage
After successful HTTP and CI verification, record real evidence and submit
the Run through Agent Hub. Do not merge/mark Done before human confirmation.

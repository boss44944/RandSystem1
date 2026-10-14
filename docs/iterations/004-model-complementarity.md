# Iteration 004 - Model complementarity

## Goal
Measure prediction disagreement and error overlap without confusing a
hindsight Oracle bound with an executable ensemble.

## Scope
- Joint correctness/error contingency and paired model disagreement.
- Error Phi correlation, undefined when error variance is zero.
- Multi-model Oracle upper bound on observed aligned test examples.
- Unlabeled-only diagnostics never claim accuracy.

## Implementation
- src/RandSystem1.Core/Statistics/ModelComplementarity.cs
- POST /api/evaluation/complementarity
- docs/API.md and src/RandSystem1.Api/openapi.json
- tests/RandSystem1.UnitTests/ModelComplementarityTests.cs

## Verification record
77/77 unit tests passed with earlier iteration regressions, including
an analytical fixture with 5 both-right, 2 A-only, 1 B-only and 2
both-wrong cases (Oracle 80% as an upper bound). Other tests cover
identical and complementary errors, zero-variance Phi, model alignment,
unlabeled evaluation and invalid requests.

## Limitations
Oracle is a hindsight upper bound, not a deployable strategy; tests
require exactly aligned IDs and observations. Input limits are
20 models, 20,000 cases and 4 million pairwise comparisons.

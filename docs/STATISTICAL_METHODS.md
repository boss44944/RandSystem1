# Statistical Methods / 统计方法

**Status:** Iterations 001-005: statistical evaluation, complementarity, frozen-weight fusion, fail-closed decision gates and later holdout comparisons executable.

## Baselines

For an N-choice task, uniform baseline assigns p_i=1/N; random sampling is a separate operation from emitting probabilities. Compare against training-only majority and class-prior baselines. A repeated-experiment maximum is not equivalent to a random expected result: selecting the best random run induces selection bias.

## Supervised metrics

Compute accuracy, balanced accuracy, confusion matrix, precision/recall/F1; probability-aware Brier score and log loss; calibration ECE and reliability diagrams. Skill = 1 - model_loss / baseline_loss only when baseline loss is nonzero and measured on identical held-out observations.

## Statistical inference

Use matched tests when the same examples are evaluated, and paired bootstrap to compare prediction loss. Use exact binomial tests only where assumptions genuinely apply. For serial correlation use block bootstrap or appropriate time-aware sampling, and control false positives when comparing multiple models. Report effect sizes, uncertainty, sample size, and confidence intervals; avoid p-values as a substitute for practical relevance.

## Complementarity and fusion

For any two models report both-correct, both-wrong, model-A-only and model-B-only; the union-correct fraction is an **oracle upper bound**, not achievable prediction accuracy. Train fusion weights solely on earlier data and validate out-of-sample. High confidence is not the same as calibration or business reliability. With inadequate validated advantage, output ABSTAIN.

## Unlabeled data

Output entropy, Jensen-Shannon divergence, perturbation stability, order sensitivity and disagreement can be computed without ground truth. **None alone establish that predictive accuracy exceeds chance.**

## Time-series safeguards

Use chronological walk-forward splits, purge overlapping forecast horizons, embargo where necessary, and include transaction fees, slippage, exposure and survivorship bias in trading demonstrations. No optimization may use future information; historical returns do not guarantee future returns.

## Implemented in Iteration 003

- Classification: support per label; confusion truth rows / prediction columns; macro
  recall (balanced accuracy) averages supported labels and does not give an
  artificial zero recall for a test-absent label. Macro precision/F1 likewise
  average labels observed in ground truth.
- Brier: multiclass sum of squared per-class errors, averaged by observation
  (not divided by class count). Log Loss: mean negative log true class
  probability with 1e-15 lower clamp. Null skill when baseline loss is zero.
- Calibration: reported-choice-confidence ECE with fixed-width bins and
  reliability counts/confidence/correctness. This is a descriptive diagnostic;
  ECE near zero on a small sample does not prove calibration.
- Monte Carlo: seeded baselines solve the same fixed held-out examples repeatedly.
  Report mean, SD, null percentiles, selection extreme, approximate Monte Carlo
  mean error interval and an add-one tail estimate if observed choices supplied.
- IID paired model differences: aligned observations, bootstrap uncertainty
  interval, within-pair label swaps (exact for at most 15 pairs, seeded
  approximation beyond), Bonferroni correction supplied comparison count.
  Permutation label-swap validity requires exchangeability under the null.
- Time series: only if caller explicitly confirms chronological order and
  block length >= 2, a circular moving-block bootstrap is used to retain
  short-range dependence. No IID permutation p-value is shown.
- Binomial one-sided upper tail: only available with explicit confirmation
  of IID Bernoulli sampling and a known common null success probability.
- Implementation is a pure .NET 10 Core library; the API has explicit bounded
  CPU/data request limits and does not handle production-size async jobs yet.

## Implemented in Iteration 004

The same exact observation IDs and ordering are required across all models
and optional out-of-sample ground truth. Pairwise contingency counts are
both-correct, A-only, B-only and both-wrong. Error-Phi measures correlation of
two binary error indicators, and is null when either indicator has zero
variance. Prediction disagreement and complementary correctness are NOT
interchangeable.

For 2+ models, Oracle counts cases with at least one right model. This is a
HINDSIGHT upper ceiling, not a deployable rule. The gain over best individual
accuracy is only a theoretical gap. Unlabeled analysis includes just
prediction counts/disagreements, with all skill/error/oracle fields null.

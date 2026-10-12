# Statistical Methods / 统计方法

**Status:** methodological contract and future implementation plan; Iteration 001 does not claim these analyses are executable.

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

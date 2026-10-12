# Development plan / 开发进度

Source of product requirements: [complete original RandSystem1 taskbook](PROJECT_REQUIREMENTS.md) (2026-10-12). Scope: model-independent random baseline and evaluation framework targeting .NET 10. Iterations are sequential; do not claim later features until tested.

| Iteration | Objective | Status |
|---|---|---|
| 001 | Solution, contract, mock, API, tests and docs | Accepted in Agent Hub; Done / merged to main (4f6e5fb) |
| 002 | Uniform/Dirichlet, majority/prior, independent ensemble and reproducible seeds | Accepted in Hub; Done, Worktree merged to main (e0873ec) |
| 003 | Metrics, skill, Monte Carlo and valid statistical inference | Implementation and local HTTP verified (63/63 tests); remote CI and Hub independent verification pending |
| 004 | Model error overlap, disagreement and oracle upper bound | Planned |
| 005 | Fusion strategies, risk policy, abstention | Planned |
| 006 | Walk-forward backtest, trading cost and risk metrics | Planned |
| 007 | Simulated stock investment comparison (never live trading) | Planned |
| 008 | Real HTTP/Jev/Laya adapters subject to verified interfaces | Planned |
| 009 | Docker, persistence, async jobs, operations and performance | Planned |

## Development workflow

1. Register/update the project in Agent Hub, create and approve Requirements, generate WorkItems and claim a Run.
2. Read task acceptance criteria, code, worktree policy, git status and project documents.
3. Implement the smallest accepted increment, build/test, record evidence and submit for independent review.
4. Only an authorized human or independent reviewer completes the final confirmation.
5. Update iteration docs and Git after every increment.

## Initial setup caveat

The project has since been registered in Agent Hub as ProjectId \`73d9c019dfeb4f1fb9f139d46fc1bb3a\`. Approved Requirement \`RANDS1-001\` is linked to WorkItem \`114de40fab38403181bdd06db91a2b30\` and SaaS Run \`6389f3fe7a7741c4a8c9c44e29e19fc0\`. The developer uses a Hub-allocated isolated Git Worktree; source-level completion must not be confused with independent Verification/Review or human final confirmation. Future iterations remain gated by formal acceptance.

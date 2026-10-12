# Development plan / 开发进度

Source of product requirements: user-provided RandSystem1 project taskbook (2026-10-12). Scope: model-independent random baseline and evaluation framework targeting .NET 10. Iterations are sequential; do not claim later features until tested.

| Iteration | Objective | Status |
|---|---|---|
| 001 | Solution, contract, mock, API, tests and docs | Build, 10/10 tests and API smoke verified; Hub registration/review pending |
| 002 | Uniform/Dirichlet, majority/prior, independent ensemble and reproducible seeds | Planned |
| 003 | Metrics, skill, Monte Carlo and valid statistical inference | Planned |
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

The connected AgentHub MCP does not expose project/requirement creation. The corresponding Hub HTTP endpoints returned 401 to an unauthenticated server request at initialization. No Hub Requirement, WorkItem or claimed Run can be asserted until an authorized operation succeeds. This does not prevent local isolated scaffold and source tests.

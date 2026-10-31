# Tasks

Self-contained specs sized for a single agent session. Each lists goal, scope, acceptance criteria, key files and dependencies. Do them roughly in order; check `Depends on` first. Status lives in the `Status` line of each file (`todo` / `in-progress` / `done`).

| # | Task | Depends on |
| --- | --- | --- |
| 01 | Repo scaffold | - |
| 02 | .NET solution + tag versioning | 01 |
| 03 | CI workflow | 02 |
| 04 | Release workflow | 02, 03 |
| 05 | Docs site + Pages | 01 |
| 06 | libghostty-vt binding | 02 |
| 07 | ConPTY session | 06 |
| 08 | Renderer MVP | 06, 07 |
| 09 | Input, clipboard, selection | 08 |
| 10 | Tabs | 09 |
| 11 | Splits | 10 |
| 12 | Config + hot reload | 09 |
| 13 | Polish + packaging | all |
| 14 | Architecture + quality gates (coverage, docs, single libghostty gateway) | 02, alongside 06 |

Commits for every task use Conventional Commits.

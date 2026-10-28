# SafyaClinic — Assessment Set

Read-only assessment of `MostafaSIbrahim/clinic-MS` (ASP.NET Core 8 MVC + EF Core 8 + SQL Server). No source code was changed to produce these documents. Phase 3 created clearly labelled test data in the local dev database (see `10-…`, *Clean-up*).

| # | Document | Phase | Contents |
|---|---|---|---|
| 01 | [Current-state technical assessment](01-current-state-technical-assessment.md) | 1 Discover | Projects, hosting, pipeline, auth, data, integrations, frontend, tests; runtime + DB verification (§15); decisions (§13) |
| 02 | [Solution map](02-solution-map.md) | 2 Document | Repository layout, architecture diagram, environments, entry points, size indicators |
| 03 | [Project responsibility and dependency map](03-project-responsibility-and-dependency-map.md) | 2 | What each project owns, references, DI graph, controller→service dependencies, packages |
| 04 | [Business capability map](04-business-capability-map.md) | 2 | 12 capabilities, visit state machine, clinical/lab/nutrition/billing rules, capability × role matrix |
| 05 | [Route and screen inventory](05-route-and-screen-inventory.md) | 2 | All 158 actions with effective roles and views; all screens; navigation |
| 06 | [Data model summary](06-data-model-summary.md) | 2 | 31 tables, relationships and delete rules, indexes, data-quality observations |
| 07 | [Authentication and authorization summary](07-authn-authz-summary.md) | 2 | Cookie auth, password handling, roles, policies, data-level rules, gaps |
| 08 | [External integration inventory](08-external-integrations.md) | 2 | SQL Server, DwaPrices, file storage, CDNs, hosting, printing |
| 09 | [Risks, unknowns and questions](09-risks-unknowns-questions.md) | 2 | Consolidated risk register (input to Phase 3), unknowns, owner questions, decisions |
| 10 | [Evaluation findings](10-evaluation-findings.md) | 3 Evaluate | 32 findings with evidence, impact, recommendation, priority and confidence; runtime + accessibility results |
| 11 | [Target-state proposal](11-target-state-proposal.md) | 4 Propose | Architecture changes, permission-group model, frontend options, shell, IA, design-system structure, accessibility, responsive, localisation, migration stages, decisions D1–D12, success criteria |
| 12 | [Prioritised roadmap](12-prioritized-roadmap.md) | 5 Prioritize | 33 planned items in 4 groups plus 6 deferred opportunities, each with value, risk, dependencies, effort band and acceptance criteria; sequence, exit gates, finding traceability |
| 13 | [Design-system specification](13-design-system-spec.md) | 6 Design | UI rule, tokens (`../design/tokens.css`), interaction states, components, accessibility, responsive and RTL rules |

Evidence: raw build/test/DB outputs were collected locally into `.assessment/out/` (git-ignored, not committed).

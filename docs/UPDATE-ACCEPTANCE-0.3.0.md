# Updater 0.3.0 — acceptance record

Status: local Windows acceptance passed; production distribution not yet validated.

| Case | SpriteLab | MarkdownLab |
| --- | --- | --- |
| Signed metadata, SHA-256, retained packages | PASS | PASS |
| Tracked installation and confirmation | 0.6.10 → 0.6.11, Completed | 1.7.1 → 1.7.3, Completed |
| Restore and confirm | 0.6.12 → 0.6.11, RolledBack | 1.7.2 → 1.7.1, RolledBack |
| Cancel before file copying; resolve in application | 0.6.11 → 0.6.12, Aborted | 1.7.3 → 1.7.4, Aborted |

Evidence reported by maintainer on 2026-11-28:
- SpriteLab Completed: a3510aa4-3460-4757-b60c-50c1c3755554; RolledBack: a5ba6968-3704-42df-81b6-09dd3c893012; Aborted: 1a354565-f2f6-40c9-a161-ac1e9e38f535.
- MarkdownLab Completed: cba4e39d-7be9-47f0-81d5-a9c4e13e42ab; RolledBack: 531811fe-65dd-4656-96ae-8afa48c2833a; Aborted: 02b9bdc6-c51e-4866-a5d6-19f613ee42e5.
- LocalUpdateLab uses a local transport with signed production-shaped metadata; it does not prove production download availability.

Limits: no automatic rollback; explicit user approval, compatible backed-up data and retained signed baseline required. Missing receipt is not evidence of cancellation. Only resolve cancellation when user confirms no file copying occurred, installer exited and old version is healthy. Process-name checks are advisory. Partial installation and unknown state require manual diagnosis. First update may not have a retained baseline. Never delete the journal to bypass a block.

Remaining gates: review final package and CI, owner approval to merge/publish 0.3.0, anonymous NuGet restore in both consumers, design refresh, then controlled real GitHub channel validation. Publication automation belongs to ReleaseInfrastructure; preserve current manifest and key contracts until compatible integration is approved.

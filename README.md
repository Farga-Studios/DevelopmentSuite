# Farga Studios Development Suite

Shared foundation (currently private; planned open source) for independent game development tools. .NET 10; each tool owns its release lifecycle.

## Components
| Component | Scope | State |
|---|---|---|
| Suite.Updater | Signed manifest and verified package download | Initial extraction |
| Suite.Core | Cross-tool contracts | Reserved |
| Suite.UI | Dark visual system, dialogs, icons | Design contract |
| Suite.Localization | Stable keys, ES/EN resources | Design contract |
| Suite.Settings | Atomic user-local preferences | Design contract |
| Suite.Diagnostics | Diagnostic contracts and logging | Reserved |

SpriteLab is the first consumer. Shared code starts as a reviewed source snapshot; no private feed or cross-repository credentials required yet. Versioned package consumption follows after CI and integration validation. See docs/ARCHITECTURE.md and docs/DISTRIBUTION.md.

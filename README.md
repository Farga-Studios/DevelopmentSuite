# Farga Studios Development Suite

Shared foundation (currently private; planned open source) for independent game development tools. .NET 8 and .NET 10; each tool owns its release lifecycle.

## Components
| Component | Scope | State |
|---|---|---|
| Suite.Updater | Signed manifest and verified package download | 0.2.0 candidate; CI and consumer acceptance pending |
| Suite.Core | Cross-tool contracts | Reserved |
| Suite.UI | Dark visual system, dialogs, icons | Design contract |
| Suite.Localization | Stable keys, ES/EN resources | Design contract |
| Suite.Settings | Atomic user-local preferences | Design contract |
| Suite.Diagnostics | Diagnostic contracts and logging | Reserved |

SpriteLab currently uses a source snapshot; MarkdownLab retains its existing updater. The 0.2.0 candidate builds the versioned NuGet package `FargaStudios.Suite.Updater` for both frameworks. Neither application consumes the package yet. See [package integration](docs/PACKAGES.md). See docs/ARCHITECTURE.md and docs/DISTRIBUTION.md.

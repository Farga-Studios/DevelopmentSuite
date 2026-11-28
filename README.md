# Farga Development Suite

Shared infrastructure for independent desktop tools by Farga Studios.

[![Build](https://github.com/Farga-Studios/DevelopmentSuite/actions/workflows/build.yml/badge.svg)](https://github.com/Farga-Studios/DevelopmentSuite/actions/workflows/build.yml)

## Components

| Component | Purpose | Status |
| --- | --- | --- |
| Suite.Updater | Signed update metadata, verified downloads, installer handoff and recovery | 0.3.0 release candidate |
| Suite.Core | Cross-tool contracts | Planned |
| Suite.UI | Shared design principles | Design contract |
| Suite.Localization | Language resources | Design contract |
| Suite.Settings | Local preferences | Design contract |
| Suite.Diagnostics | Logging and diagnostics | Planned |

SpriteLab and MarkdownLab have passed local Windows acceptance using 0.3.0-rc.2. The stable 0.3.0 package is being prepared for NuGet.org; production channel validation and stable consumer migration remain pending. See [acceptance results](docs/UPDATE-ACCEPTANCE-0.3.0.md). The [DevelopmentSuite Design System](docs/DESIGN-SYSTEM.md) defines the proposed shared visual contract for suite applications only.

## Development

Use the .NET 10 SDK with the .NET 8 and .NET 10 runtimes.

```powershell
dotnet test tests/Suite.Updater.Tests -c Release -f net8.0
dotnet test tests/Suite.Updater.Tests -c Release -f net10.0
dotnet pack src/Suite.Updater -c Release -o packages
```

See [Architecture](docs/ARCHITECTURE.md), [Distribution](docs/DISTRIBUTION.md) and [Package integration](docs/PACKAGES.md).

## License

Original project code: [MIT](LICENSE). Third-party components retain their licenses. Repository publication remains subject to review; no trademark rights are granted.

# Suite.Updater 0.2.0 candidate

The package is UI-independent and targets net8.0 and net10.0. Build using the .NET 10 SDK; execute tests with both runtimes.

```powershell
dotnet test tests/Suite.Updater.Tests -c Release -f net8.0
dotnet test tests/Suite.Updater.Tests -c Release -f net10.0
dotnet pack src/Suite.Updater -c Release -o packages
```

CI retains a versioned candidate .nupkg as a workflow artifact. For local integration, copy it to a local NuGet feed outside the source tree and add that source to NuGet configuration. Pin `<PackageReference Include="FargaStudios.Suite.Updater" Version="0.2.0" />`. Do not commit credentials or binaries. A durable public feed (or approved public package distribution) is pending the open-source audit; do not introduce a remote private-feed dependency into public consumers.

SpriteLab: remove its updater source snapshot when replacing it with the package; pass the application-specific installer switch explicitly if needed. MarkdownLab: preserve AppId, data paths, existing releases and 1.7 upgrade path; integrate in a separate 1.8 candidate. Its future signed installer filename must match the shared manifest contract or an explicitly reviewed contract extension. Both apps own their UI, trust key, channel, release repository and installer lifecycle.

Windows handoff requires a compatible installer implementing /WAITPID and /NOCLOSEAPPLICATIONS. The library verifies the staged package again; the caller is responsible for saving documents and closing the application. Optional application-specific switch is passed as one argument. No automatic rollback is implemented.

Acceptance: both runtime tests green, both package assemblies present, clean consumer restore and build, Windows installer lifecycle and actual signed updates verified. No release publication, signing key provisioning, or public visibility change is implied.

## Public distribution migration
SpriteLab is now public. GitHub Packages still requires authentication even when the NuGet package is public. Keep the existing 0.2.0 private-feed dependency working until an anonymous package is verified on nuget.org. The manual Publish updater to NuGet.org workflow is prepared but deliberately blocked while the suite repository is private or lacks license metadata or a new version. After source/history/dependency audit, add MIT license and package metadata, increment version, update CI version checks, make repository public with owner approval, configure the nuget-public environment and scoped publishing credential, and run the workflow from main. Verify clean anonymous restore in a SpriteLab PR before changing its source mapping; no tokens in consumer repositories. No publication performed by adding this workflow.

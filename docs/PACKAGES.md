# Suite.Updater 0.3.0

Package ID: `FargaStudios.Suite.Updater`. UI-independent, targets net8.0 and net10.0. MIT. The 0.3.0 release is prepared, not yet published; 0.3.0-rc.2 was used for local consumer acceptance.

## Build and test

```powershell
dotnet test tests/Suite.Updater.Tests -c Release -f net8.0
dotnet test tests/Suite.Updater.Tests -c Release -f net10.0
dotnet pack src/Suite.Updater -c Release -o packages
```

CI verifies both framework assemblies in the package. Consumer candidate feeds pin an exact reviewed source commit; replace these with an exact stable NuGet reference only after successful public publication and anonymous restore validation.

## Integration

SpriteLab and MarkdownLab own their UI, pinned public key, stable channel, installer identity, project data and shutdown lifecycle. Use the documented manifest contract without changing signed bytes or trust roots. Windows installers implement /WAITPID, /NOCLOSEAPPLICATIONS and transaction-specific completion receipts. Save user work before handoff. Recovery is explicit, not automatic; signed retained baseline, installer exit, compatible backed-up data and approval are required for rollback. No receipt or unknown installation state requires conservative handling. See [local acceptance](UPDATE-ACCEPTANCE-0.3.0.md).

## Publication

The existing manual publish-nuget-org.yml workflow runs only on public main, with the nuget-public environment and OIDC Trusted Publishing scoped to the package, repository and workflow. No persistent key is required. Review CI and obtain owner approval before merging and dispatching. Verify the actual published version and both clean consumer restores. Package publication is separate from application release approval. Future application publication automation is owned by ReleaseInfrastructure, not this library.

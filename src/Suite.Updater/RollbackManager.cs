namespace Suite.Updater;

/// <summary>Prepares recovery only. Host owns consent, data compatibility, locking and installer handoff.</summary>
public sealed class RollbackManager(UpdateRecovery recovery, VerifiedPackageStore store)
{
    public async Task<PreparedUpdate> PrepareAsync(Guid transactionId, Version runningVersion,
        bool userApproved, bool dataCompatible, string stagingRoot, CancellationToken token = default)
    {
        if (!userApproved || !dataCompatible)
            throw new InvalidOperationException("Rollback requires consent and compatible user data.");
        var assessment = recovery.Assess(runningVersion);
        if (assessment.Transaction is not { } t || t.Id != transactionId ||
            assessment.Status != RecoveryStatus.RecoveryRequired ||
            runningVersion.Revision > 0 ||
            new Version(runningVersion.Major,runningVersion.Minor,Math.Max(0,runningVersion.Build)) != Version.Parse(t.TargetVersion))
            throw new InvalidOperationException("Rollback requires the failed target version and matching transaction.");
        var retained = await store.OpenAsync(t.PreviousVersion, token);
        if (retained.Manifest.Product != t.Product || retained.Manifest.Version != t.PreviousVersion)
            throw new InvalidDataException("Rollback package mismatch.");
        UpdatePathSafety.EnsureDirectory(stagingRoot);
        UpdatePathSafety.RejectLinks(stagingRoot);
        var path = Path.Combine(Path.GetFullPath(stagingRoot), Guid.NewGuid().ToString("N") + "-" + retained.Manifest.FileName);
        try
        {
            await using (var input = File.OpenRead(retained.Path))
            await using (var output = new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var buffer = new byte[65536];
                long total = 0;
                while (true)
                {
                    int n = await input.ReadAsync(buffer,token);
                    if (n == 0) break;
                    total += n;
                    if (total > retained.Manifest.Bytes) throw new InvalidDataException("Rollback package exceeds signed size.");
                    await output.WriteAsync(buffer.AsMemory(0,n),token);
                }
                output.Flush(true);
            }
            await UpdateClient.VerifyPackageAsync(path,retained.Manifest,token);
            token.ThrowIfCancellationRequested();
            return new PreparedUpdate(retained.Manifest,path);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }
}

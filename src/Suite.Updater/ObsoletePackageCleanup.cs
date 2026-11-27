namespace Suite.Updater;

/// <summary>Requires exclusive host lock and no active installer or download.</summary>
public static class ObsoletePackageCleanup
{
    public static async Task<CleanupResult> RunAsync(string cacheRoot, VerifiedPackageStore store,
        string currentVersion, string? previousHealthyVersion, UpdateJournal journal,
        CancellationToken token = default)
    {
        var root = Path.GetFullPath(cacheRoot);
        if (!Directory.Exists(root)) return new(0,0);
        UpdatePathSafety.RejectLinks(root);
        var candidates = PackageRetention.Plan(Directory.EnumerateDirectories(root).Select(Path.GetFileName).OfType<string>(),
            currentVersion,previousHealthyVersion,journal.Read());
        int removed = 0, skipped = 0;
        foreach (var version in candidates)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var folder = Path.Combine(root,version);
                var package = await store.OpenAsync(version,token);
                if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(package.Path)),folder,
                    OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal) ||
                    (File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0 ||
                    Directory.EnumerateDirectories(folder).Any())
                { skipped++; continue; }
                var expected = new HashSet<string>(StringComparer.Ordinal) { "update.json","update.json.sig",package.Manifest.FileName };
                var files = Directory.GetFiles(folder);
                if (files.Length != 3 || files.Any(p => !expected.Contains(Path.GetFileName(p)) ||
                    (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0))
                { skipped++; continue; }
                token.ThrowIfCancellationRequested();
                if (!PackageRetention.Plan(new[]{version},currentVersion,previousHealthyVersion,journal.Read()).Contains(version))
                { skipped++; continue; }
                Directory.Delete(folder,true);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or System.Text.Json.JsonException)
            { skipped++; }
        }
        return new(removed,skipped);
    }
}

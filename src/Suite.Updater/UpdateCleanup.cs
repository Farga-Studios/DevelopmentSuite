namespace Suite.Updater;

public sealed record CleanupResult(int Removed, int Skipped);

/// <summary>Run under the host single-writer lock, with no active installer or download.
/// Only removes abandoned pending directories in the private package cache.</summary>
public static class UpdateCleanup
{
    public static CleanupResult RemoveAbandonedPending(string cacheRoot, TimeSpan minimumAge, DateTimeOffset now)
    {
        if (minimumAge < TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(minimumAge));
        var root = Path.GetFullPath(cacheRoot);
        if (!Directory.Exists(root)) return new(0,0);
        UpdatePathSafety.RejectLinks(root);
        int removed = 0, skipped = 0;
        foreach (var directory in Directory.EnumerateDirectories(root,".pending-*",SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(directory);
            if (!Guid.TryParseExact(name[".pending-".Length..],"N",out _) ||
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                now - Directory.GetLastWriteTimeUtc(directory) < minimumAge)
            {
                skipped++;
                continue;
            }
            try
            {
                // Pending package directories are flat. Unknown structures are preserved.
                if (Directory.EnumerateDirectories(directory).Any() ||
                    Directory.EnumerateFiles(directory).Any(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0))
                {
                    skipped++;
                    continue;
                }
                Directory.Delete(directory,true);
                removed++;
            }
            catch (IOException) { skipped++; }
            catch (UnauthorizedAccessException) { skipped++; }
        }
        return new(removed,skipped);
    }
}

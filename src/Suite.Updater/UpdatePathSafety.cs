namespace Suite.Updater;

internal static class UpdatePathSafety
{
    // Cooperative private storage: this check does not eliminate filesystem races.
    internal static void RejectLinks(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update paths must not contain reparse points.");
            current = Path.GetDirectoryName(current);
        }
    }
}

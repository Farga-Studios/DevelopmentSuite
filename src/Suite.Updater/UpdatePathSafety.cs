namespace Suite.Updater;

internal static class UpdatePathSafety
{
    // Cooperative private storage: this check does not eliminate filesystem races.
    internal static void RejectExistingLinks(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Update paths must not contain reparse points.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = Path.GetDirectoryName(current);
        }
    }

    internal static void EnsureDirectory(string path)
    {
        RejectExistingLinks(path);
        Directory.CreateDirectory(path);
        RejectLinks(path);
    }

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

namespace Suite.Updater;

/// <summary>Read-only retention policy. Host supplies the last known healthy previous version.
/// Unknown directories and invalid journal states are never deletion candidates.</summary>
public static class PackageRetention
{
    public static IReadOnlyList<string> Plan(IEnumerable<string> cachedVersions,
        string currentVersion, string? previousHealthyVersion, JournalReadResult journal)
    {
        static bool Canonical(string? value) =>
            Version.TryParse(value,out var v) && v.Build >= 0 && v.Revision == -1 && v.ToString(3) == value;
        if (!Canonical(currentVersion) || !Canonical(previousHealthyVersion))
            return Array.Empty<string>();
        if (journal.Status != JournalStatus.Missing && journal.Status != JournalStatus.Valid)
            return Array.Empty<string>();
        var keep = new HashSet<string>(StringComparer.Ordinal) { currentVersion, previousHealthyVersion! };
        if (journal.Status == JournalStatus.Valid)
        {
            if (journal.Transaction is not { } t || !Canonical(t.PreviousVersion) || !Canonical(t.TargetVersion))
                return Array.Empty<string>();
            keep.Add(t.PreviousVersion);
            keep.Add(t.TargetVersion);
        }
        return cachedVersions.Where(v => Canonical(v) && !keep.Contains(v) &&
            Version.Parse(v) < Version.Parse(currentVersion)).Distinct(StringComparer.Ordinal).ToArray();
    }
}

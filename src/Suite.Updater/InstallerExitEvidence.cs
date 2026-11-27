using System.Text;

namespace Suite.Updater;

/// <summary>Advisory completion receipt from the trusted installer, not package authorization.
/// Caller holds the update session lock. Missing or malformed receipts fail closed.</summary>
public sealed class InstallerExitEvidence
{
    private readonly string root;
    public InstallerExitEvidence(string directory) => root = Path.GetFullPath(directory);

    public string Prepare(Guid transactionId)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Invalid transaction.");
        UpdatePathSafety.EnsureDirectory(root);
        var path = ReceiptPath(transactionId);
        UpdatePathSafety.RejectExistingLinks(path);
        // Reset any prior attempt, including rollback retries.
        if (File.Exists(path)) File.Delete(path);
        return path;
    }

    public bool HasCompletion(Guid transactionId)
    {
        if (transactionId == Guid.Empty) return false;
        try
        {
            var path = ReceiptPath(transactionId);
            UpdatePathSafety.RejectLinks(path);
            using var stream = File.OpenRead(path);
            var expected = Encoding.ASCII.GetBytes("completed:" + transactionId.ToString("N"));
            var actual = new byte[expected.Length + 1];
            var count = stream.ReadAtLeast(actual, actual.Length, false);
            return count == expected.Length && actual.AsSpan(0,count).SequenceEqual(expected);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private string ReceiptPath(Guid id) => Path.Combine(root, "installer-" + id.ToString("N") + ".receipt");
}

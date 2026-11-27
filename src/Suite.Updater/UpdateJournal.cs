using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suite.Updater;

public enum UpdatePhase { Prepared, HandoffStarted, AwaitingHealthCheck, Completed, RecoveryRequired, RollbackStarted, RolledBack, Aborted }
public enum JournalStatus { Missing, Valid, Invalid }
public sealed record UpdateTransaction(Guid Id, string Product, string PreviousVersion, string TargetVersion, UpdatePhase Phase);
public sealed record JournalReadResult(JournalStatus Status, UpdateTransaction? Transaction);

/// <summary>Advisory transaction state. Caller must hold the application single-writer lock.</summary>
public sealed class UpdateJournal
{
    private const int MaxBytes = 8192;
    private static readonly JsonSerializerOptions Json = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private readonly string path;
    private readonly string product;

    public UpdateJournal(string directory, string product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        if (product.Length > 100 || !product.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
            throw new ArgumentException("Invalid product.", nameof(product));
        this.product = product;
        path = Path.Combine(Path.GetFullPath(directory), product + ".journal.json");
    }

    public JournalReadResult Read()
    {
        try
        {
            UpdatePathSafety.RejectLinks(path);
            using var stream = File.OpenRead(path);
            if (stream.Length > MaxBytes) return new(JournalStatus.Invalid, null);
            var bytes = new byte[MaxBytes + 1];
            int count = stream.ReadAtLeast(bytes, bytes.Length, false);
            if (count > MaxBytes) return new(JournalStatus.Invalid, null);
            using var document = JsonDocument.Parse(bytes.AsMemory(0,count));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(JournalStatus.Invalid,null);
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!fields.Add(property.Name)) return new(JournalStatus.Invalid,null);
            if (!fields.SetEquals(new[]{"Id","Product","PreviousVersion","TargetVersion","Phase"}))
                return new(JournalStatus.Invalid,null);
            var transaction = document.RootElement.Deserialize<UpdateTransaction>(Json);
            return IsValid(transaction) ? new(JournalStatus.Valid, transaction) : new(JournalStatus.Invalid, null);
        }
        catch (FileNotFoundException) { return new(JournalStatus.Missing, null); }
        catch (DirectoryNotFoundException) { return new(JournalStatus.Missing, null); }
        catch (JsonException) { return new(JournalStatus.Invalid, null); }
    }

    public UpdateTransaction Begin(string previousVersion, string targetVersion)
    {
        var current = Read();
        if (current.Status != JournalStatus.Missing && current.Transaction?.Phase is not (UpdatePhase.Completed or UpdatePhase.RolledBack or UpdatePhase.Aborted))
            throw new InvalidOperationException("Unresolved update journal.");
        var transaction = new UpdateTransaction(Guid.NewGuid(), product, previousVersion, targetVersion, UpdatePhase.Prepared);
        Write(transaction);
        return transaction;
    }

    public UpdateTransaction Advance(Guid id, UpdatePhase next)
    {
        var current = Read().Transaction;
        if (current is null || current.Id != id || !Allowed(current.Phase, next))
            throw new InvalidOperationException("Invalid update transition.");
        var updated = current with { Phase = next };
        Write(updated);
        return updated;
    }

    private static bool Allowed(UpdatePhase from, UpdatePhase to) => (from, to) switch
    {
        (UpdatePhase.Prepared, UpdatePhase.HandoffStarted or UpdatePhase.RecoveryRequired or UpdatePhase.Aborted) => true,
        (UpdatePhase.HandoffStarted, UpdatePhase.AwaitingHealthCheck or UpdatePhase.RecoveryRequired or UpdatePhase.Aborted) => true,
        (UpdatePhase.AwaitingHealthCheck, UpdatePhase.Completed or UpdatePhase.RecoveryRequired or UpdatePhase.Aborted) => true,
        (UpdatePhase.RecoveryRequired, UpdatePhase.RollbackStarted) => true,
        (UpdatePhase.RollbackStarted, UpdatePhase.RolledBack or UpdatePhase.RecoveryRequired) => true,
        _ => false
    };

    private bool IsValid(UpdateTransaction? t) =>
        t is not null && t.Id != Guid.Empty && t.Product == product &&
        Canonical(t.PreviousVersion) && Canonical(t.TargetVersion) &&
        Version.Parse(t.TargetVersion) > Version.Parse(t.PreviousVersion) && Enum.IsDefined(t.Phase);

    private static bool Canonical(string? value) =>
        Version.TryParse(value, out var v) && v.Build >= 0 && v.Revision < 0 && value == v.ToString(3);

    private void Write(UpdateTransaction transaction)
    {
        if (!IsValid(transaction)) throw new InvalidDataException("Invalid update transaction.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(transaction, Json);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Journal too large.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        UpdatePathSafety.RejectLinks(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) UpdatePathSafety.RejectLinks(path);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

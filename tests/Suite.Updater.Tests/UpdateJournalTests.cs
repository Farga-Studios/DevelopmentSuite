using Suite.Updater;
using Xunit;

public sealed class UpdateJournalTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "suite-journal-" + Guid.NewGuid().ToString("N"));
    private UpdateJournal Journal => new(root, "SpriteLab");
    private string JournalPath => Path.Combine(root, "SpriteLab.journal.json");

    [Fact]
    public void MissingJournalIsDistinct()
    {
        Assert.Equal(JournalStatus.Missing, Journal.Read().Status);
    }

    [Fact]
    public void PersistsFullLifecycleAcrossInstances()
    {
        var t = Journal.Begin("0.6.8", "0.6.9");
        Assert.Equal(t, Journal.Read().Transaction);
        Journal.Advance(t.Id, UpdatePhase.HandoffStarted);
        Journal.Advance(t.Id, UpdatePhase.AwaitingHealthCheck);
        var completed = Journal.Advance(t.Id, UpdatePhase.Completed);
        Assert.Equal(completed, Journal.Read().Transaction);
        Assert.NotEqual(t.Id, Journal.Begin("0.6.9", "0.7.0").Id);
    }

    [Theory]
    [InlineData("1.0", "1.1.0")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.1.0", "1.0.0")]
    [InlineData("1.0.0", "01.1.0")]
    public void RejectsInvalidVersions(string previous, string target)
    {
        Assert.Throws<InvalidDataException>(() => Journal.Begin(previous, target));
        Assert.Equal(JournalStatus.Missing, Journal.Read().Status);
    }

    [Fact]
    public void RejectsWrongIdSkippedPhaseAndOverlappingTransaction()
    {
        var t = Journal.Begin("1.0.0", "1.1.0");
        Assert.Throws<InvalidOperationException>(() => Journal.Advance(Guid.NewGuid(), UpdatePhase.HandoffStarted));
        Assert.Throws<InvalidOperationException>(() => Journal.Advance(t.Id, UpdatePhase.Completed));
        Assert.Throws<InvalidOperationException>(() => Journal.Begin("1.0.0", "1.2.0"));
        Assert.Equal(t, Journal.Read().Transaction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("null")]
    public void CorruptionBlocksNewTransaction(string content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(JournalPath, content);
        Assert.Equal(JournalStatus.Invalid, Journal.Read().Status);
        Assert.Throws<InvalidOperationException>(() => Journal.Begin("1.0.0", "1.1.0"));
        Assert.Equal(content, File.ReadAllText(JournalPath));
    }

    [Fact]
    public void RejectsOversizedJournal()
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(JournalPath, new byte[8193]);
        Assert.Equal(JournalStatus.Invalid, Journal.Read().Status);
    }

    [Fact]
    public void IgnoresInterruptedTemporaryWriteAndSeparatesProducts()
    {
        var t = Journal.Begin("1.0.0", "1.1.0");
        File.WriteAllText(JournalPath + ".interrupted.tmp", "{");
        Assert.Equal(t, Journal.Read().Transaction);
        Assert.Equal(JournalStatus.Missing, new UpdateJournal(root, "MarkdownLab").Read().Status);
    }

    [Fact]
    public void RecoveryRequiredCannotBeSilentlyOverwritten()
    {
        var t = Journal.Begin("1.0.0", "1.1.0");
        Journal.Advance(t.Id, UpdatePhase.RecoveryRequired);
        Assert.Throws<InvalidOperationException>(() => Journal.Begin("1.0.0", "1.2.0"));
        Assert.Throws<InvalidOperationException>(() => Journal.Advance(t.Id, UpdatePhase.Completed));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}

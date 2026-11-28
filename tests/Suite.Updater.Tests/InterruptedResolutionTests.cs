using Suite.Updater;
using Xunit;

public sealed class InterruptedResolutionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiresAllConfirmationsAndPreviousVersion(bool awaiting)
    {
        var journal = new UpdateJournal(root,"SpriteLab");
        var recovery = new UpdateRecovery(journal);
        var t = journal.Begin("1.0.0","1.1.0");
        journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        if (awaiting) journal.Advance(t.Id,UpdatePhase.AwaitingHealthCheck);
        var previous = new Version("1.0.0");
        Assert.Throws<InvalidOperationException>(() => recovery.ResolveInterrupted(t.Id,previous,false,true,true));
        Assert.Throws<InvalidOperationException>(() => recovery.ResolveInterrupted(t.Id,previous,true,false,true));
        Assert.Throws<InvalidOperationException>(() => recovery.ResolveInterrupted(t.Id,previous,true,true,false));
        Assert.Throws<InvalidOperationException>(() => recovery.ResolveInterrupted(Guid.NewGuid(),previous,true,true,true));
        Assert.Throws<InvalidOperationException>(() => recovery.ResolveInterrupted(t.Id,new Version("1.1.0"),true,true,true));
        Assert.Equal(awaiting ? UpdatePhase.AwaitingHealthCheck : UpdatePhase.HandoffStarted,journal.Read().Transaction!.Phase);
        Assert.Equal(UpdatePhase.Aborted,recovery.ResolveInterrupted(t.Id,previous,true,true,true).Phase);
        Assert.Equal(RecoveryStatus.Aborted,new UpdateRecovery(new UpdateJournal(root,"SpriteLab")).Assess(previous).Status);
        journal.Begin("1.0.0","1.2.0");
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

using Suite.Updater;
using Xunit;

public sealed class RollbackLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "suite-rollback-" + Guid.NewGuid().ToString("N"));
    private UpdateJournal Journal => new(root,"SpriteLab");
    private UpdateRecovery Recovery => new(Journal);
    private UpdateTransaction Failed()
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        return Recovery.RequireRecovery(t.Id);
    }

    [Fact]
    public void RestoredVersionRequiresHealthConfirmation()
    {
        var t = Failed();
        Recovery.MarkRollbackStarted(t.Id,new Version("1.1.0"));
        Assert.Equal(RecoveryStatus.RollbackInterrupted,Recovery.Assess(new Version("1.1.0")).Status);
        Assert.Equal(RecoveryStatus.RollbackHealthCheckRequired,Recovery.Assess(new Version("1.0.0")).Status);
        Assert.Throws<InvalidOperationException>(() => Recovery.ConfirmRollbackHealthy(t.Id,new Version("1.1.0")));
        Assert.Throws<InvalidOperationException>(() => Recovery.ConfirmRollbackHealthy(Guid.NewGuid(),new Version("1.0.0")));
        Assert.Equal(UpdatePhase.RolledBack,Recovery.ConfirmRollbackHealthy(t.Id,new Version("1.0.0")).Phase);
        Assert.Equal(RecoveryStatus.RolledBack,Recovery.Assess(new Version("1.0.0")).Status);
        Journal.Begin("1.0.0","1.2.0");
    }

    [Fact]
    public void FailedRollbackCanReturnToRecovery()
    {
        var t = Failed();
        Assert.Throws<InvalidOperationException>(() => Recovery.MarkRollbackStarted(t.Id,new Version("1.0.0")));
        Recovery.MarkRollbackStarted(t.Id,new Version("1.1.0"));
        Assert.Equal(UpdatePhase.RecoveryRequired,Recovery.RequireRecovery(t.Id).Phase);
        Assert.Equal(UpdatePhase.RollbackStarted,Recovery.MarkRollbackStarted(t.Id,new Version("1.1.0")).Phase);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

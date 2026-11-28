using Suite.Updater;
using Xunit;

public sealed class UpdateRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "suite-recovery-" + Guid.NewGuid().ToString("N"));
    private UpdateJournal Journal => new(root, "SpriteLab");
    private UpdateRecovery Recovery => new(Journal);

    [Fact]
    public void MissingAndCorruptJournalAreDistinct()
    {
        Assert.Equal(RecoveryStatus.NoTransaction, Recovery.Assess(new Version("1.0.0")).Status);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"SpriteLab.journal.json"), "{");
        Assert.Equal(RecoveryStatus.InvalidJournal, Recovery.Assess(new Version("1.0.0")).Status);
    }

    [Theory]
    [InlineData(UpdatePhase.Prepared,"1.0.0",RecoveryStatus.Interrupted)]
    [InlineData(UpdatePhase.Prepared,"1.1.0",RecoveryStatus.UnexpectedVersion)]
    [InlineData(UpdatePhase.HandoffStarted,"1.0.0",RecoveryStatus.Interrupted)]
    [InlineData(UpdatePhase.HandoffStarted,"1.1.0",RecoveryStatus.HealthCheckRequired)]
    [InlineData(UpdatePhase.AwaitingHealthCheck,"1.0.0",RecoveryStatus.Interrupted)]
    [InlineData(UpdatePhase.AwaitingHealthCheck,"1.1.0",RecoveryStatus.HealthCheckRequired)]
    [InlineData(UpdatePhase.Completed,"1.1.0",RecoveryStatus.Healthy)]
    [InlineData(UpdatePhase.Completed,"1.0.0",RecoveryStatus.UnexpectedVersion)]
    [InlineData(UpdatePhase.RecoveryRequired,"1.0.0",RecoveryStatus.RecoveryRequired)]
    [InlineData(UpdatePhase.RecoveryRequired,"1.1.0",RecoveryStatus.RecoveryRequired)]
    [InlineData(UpdatePhase.HandoffStarted,"2.0.0",RecoveryStatus.UnexpectedVersion)]
    [InlineData(UpdatePhase.HandoffStarted,"1.1.0.1",RecoveryStatus.UnexpectedVersion)]
    public void AssessesWithoutMutatingJournal(UpdatePhase phase,string version,RecoveryStatus expected)
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        if (phase == UpdatePhase.RecoveryRequired)
            Journal.Advance(t.Id,phase);
        else
        {
            if (phase != UpdatePhase.Prepared) Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
            if (phase is UpdatePhase.AwaitingHealthCheck or UpdatePhase.Completed)
                Journal.Advance(t.Id,UpdatePhase.AwaitingHealthCheck);
            if (phase == UpdatePhase.Completed) Journal.Advance(t.Id,phase);
        }
        var before = Journal.Read();
        Assert.Equal(expected,Recovery.Assess(Version.Parse(version)).Status);
        Assert.Equal(before,Journal.Read());
    }

    [Fact]
    public void OnlyExpectedVersionAndTransactionCanConfirmHealth()
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        Assert.Throws<InvalidOperationException>(() => Recovery.ConfirmHealthy(t.Id,new Version("1.0.0")));
        Assert.Throws<InvalidOperationException>(() => Recovery.ConfirmHealthy(Guid.NewGuid(),new Version("1.1.0")));
        Assert.Equal(UpdatePhase.HandoffStarted,Journal.Read().Transaction!.Phase);
        Assert.Equal(UpdatePhase.Completed,Recovery.ConfirmHealthy(t.Id,new Version("1.1.0.0")).Phase);
        Assert.Equal(RecoveryStatus.Healthy,Recovery.Assess(new Version("1.1.0")).Status);
    }

    [Fact]
    public void FailedHealthCheckRequiresExplicitRecovery()
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        Assert.Throws<InvalidOperationException>(() => Recovery.RequireRecovery(Guid.NewGuid()));
        var failed = Recovery.RequireRecovery(t.Id);
        Assert.Equal(UpdatePhase.RecoveryRequired,failed.Phase);
        Assert.Equal(failed,Recovery.RequireRecovery(t.Id));
        Assert.Throws<InvalidOperationException>(() => Recovery.ConfirmHealthy(t.Id,new Version("1.1.0")));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

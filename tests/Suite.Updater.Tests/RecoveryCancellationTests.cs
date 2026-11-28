using Suite.Updater;
using Xunit;

public sealed class RecoveryCancellationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"suite-cancel-"+Guid.NewGuid().ToString("N"));
    private UpdateJournal Journal => new(root,"SpriteLab");
    private UpdateRecovery Recovery => new(Journal);

    [Fact]
    public void PreparedCancellationAllowsNewUpdate()
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        Assert.Throws<InvalidOperationException>(() => Recovery.CancelPrepared(Guid.NewGuid(),new Version("1.0.0")));
        Assert.Throws<InvalidOperationException>(() => Recovery.CancelPrepared(t.Id,new Version("1.1.0")));
        Assert.Equal(UpdatePhase.Aborted,Recovery.CancelPrepared(t.Id,new Version("1.0.0")).Phase);
        Assert.Equal(RecoveryStatus.Aborted,Recovery.Assess(new Version("1.0.0")).Status);
        Assert.Equal(RecoveryStatus.UnexpectedVersion,Recovery.Assess(new Version("1.1.0")).Status);
        Journal.Begin("1.0.0","1.2.0");
    }

    [Fact]
    public void CannotCancelAfterHandoff()
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        Assert.Throws<InvalidOperationException>(() => Recovery.CancelPrepared(t.Id,new Version("1.0.0")));
        Assert.Equal(UpdatePhase.HandoffStarted,Journal.Read().Transaction!.Phase);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

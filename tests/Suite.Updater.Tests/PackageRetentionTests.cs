using Suite.Updater;
using Xunit;

public sealed class PackageRetentionTests
{
    private static readonly string[] Versions = ["0.8.0","0.9.0","1.0.0","1.1.0","1.2.0","unknown","../outside"];

    [Fact]
    public void PreservesCurrentPreviousAndFutureVersions()
    {
        Assert.Equal(new[]{"0.8.0","0.9.0"},PackageRetention.Plan(Versions,"1.1.0","1.0.0",new(JournalStatus.Missing,null)));
    }

    [Fact]
    public void ProtectsBothTransactionVersions()
    {
        var t = new UpdateTransaction(Guid.NewGuid(),"SpriteLab","0.9.0","1.0.0",UpdatePhase.RecoveryRequired);
        Assert.Equal(new[]{"0.8.0"},PackageRetention.Plan(Versions,"1.1.0","1.0.0",new(JournalStatus.Valid,t)));
    }

    [Fact]
    public void InvalidOrIncompleteStatePreventsDeletion()
    {
        Assert.Empty(PackageRetention.Plan(Versions,"1.1.0","1.0.0",new(JournalStatus.Invalid,null)));
        Assert.Empty(PackageRetention.Plan(Versions,"1.1.0",null,new(JournalStatus.Missing,null)));
        Assert.Empty(PackageRetention.Plan(Versions,"1.1.0","1.0.0",new(JournalStatus.Valid,null)));
    }
}

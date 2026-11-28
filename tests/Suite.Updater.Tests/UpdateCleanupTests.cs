using Suite.Updater;
using Xunit;

public sealed class UpdateCleanupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"suite-cleanup-"+Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private string Make(string name, int age)
    {
        var path = Directory.CreateDirectory(Path.Combine(root,name)).FullName;
        File.WriteAllText(Path.Combine(path,"test.tmp"),"test");
        Directory.SetLastWriteTimeUtc(path,now.UtcDateTime.AddHours(-age));
        return path;
    }

    [Fact]
    public void RemovesOnlyOldFlatPendingDirectories()
    {
        var old = Make(".pending-"+Guid.NewGuid().ToString("N"),48);
        var recent = Make(".pending-"+Guid.NewGuid().ToString("N"),1);
        var unknown = Make(".pending-unknown",48);
        var version = Make("1.0.0",48);
        var nested = Make(".pending-"+Guid.NewGuid().ToString("N"),48);
        Directory.CreateDirectory(Path.Combine(nested,"nested"));
        Directory.SetLastWriteTimeUtc(nested,now.UtcDateTime.AddHours(-48));
        var result = UpdateCleanup.RemoveAbandonedPending(root,TimeSpan.FromHours(24),now);
        Assert.Equal(1,result.Removed);
        Assert.False(Directory.Exists(old));
        foreach(var path in new[]{recent,unknown,version,nested}) Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void RejectsAggressiveCleanup()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UpdateCleanup.RemoveAbandonedPending(root,TimeSpan.FromHours(1),now));
        Assert.Equal(0,UpdateCleanup.RemoveAbandonedPending(root,TimeSpan.FromHours(24),now).Removed);
    }

    public void Dispose()
    {
        if(Directory.Exists(root)) Directory.Delete(root,true);
    }
}

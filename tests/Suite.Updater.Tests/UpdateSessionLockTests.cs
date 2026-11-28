using Suite.Updater;
using Xunit;

public sealed class UpdateSessionLockTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"suite-lock-"+Guid.NewGuid().ToString("N"));

    [Fact]
    public void SecondSessionCannotAcquireUntilFirstReleases()
    {
        using (UpdateSessionLock.Acquire(root))
        {
            Assert.ThrowsAny<IOException>(() => { using var second = UpdateSessionLock.Acquire(root); });
        }
        using var next = UpdateSessionLock.Acquire(root);
        Assert.True(File.Exists(Path.Combine(root,"update-session.lock")));
    }

    [Fact]
    public void IndependentProductDirectoriesCanBeLockedTogether()
    {
        using var first = UpdateSessionLock.Acquire(Path.Combine(root,"SpriteLab"));
        using var second = UpdateSessionLock.Acquire(Path.Combine(root,"MarkdownLab"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

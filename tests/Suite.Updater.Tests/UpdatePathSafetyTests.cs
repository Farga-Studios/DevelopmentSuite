using Suite.Updater;
using Xunit;

public sealed class UpdatePathSafetyTests
{
    [Fact]
    public void RejectsLinkedAncestorForSessionLockAndCleanup()
    {
        if (OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var real = Directory.CreateDirectory(Path.Combine(root,"real")).FullName;
            Directory.CreateDirectory(Path.Combine(real,"child"));
            var link = Path.Combine(root,"link");
            Directory.CreateSymbolicLink(link,real);
            var child = Path.Combine(link,"child");
            Assert.ThrowsAny<IOException>(() => { using var session = UpdateSessionLock.Acquire(child); });
            Assert.ThrowsAny<IOException>(() => UpdateCleanup.RemoveAbandonedPending(child,TimeSpan.FromHours(24),DateTimeOffset.UtcNow));
            Assert.False(File.Exists(Path.Combine(real,"child","update-session.lock")));
            var missing = Path.Combine(link,"missing","nested");
            Assert.ThrowsAny<IOException>(() => { using var session = UpdateSessionLock.Acquire(missing); });
            Assert.False(Directory.Exists(Path.Combine(real,"missing")));
            var journal = new UpdateJournal(missing,"SpriteLab");
            Assert.ThrowsAny<IOException>(() => journal.Begin("1.0.0","1.1.0"));
            Assert.False(Directory.Exists(Path.Combine(real,"missing")));
            var safe = Path.Combine(root,"safe","nested");
            using (UpdateSessionLock.Acquire(safe))
                Assert.True(Directory.Exists(safe));
        }
        finally { Directory.Delete(root,true); }
    }
}

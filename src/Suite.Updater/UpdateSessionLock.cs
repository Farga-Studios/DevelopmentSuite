namespace Suite.Updater;

/// <summary>Exclusive cooperative lock for a private per-product update directory.
/// All participating hosts must use the same directory. Never delete the lock file.</summary>
public sealed class UpdateSessionLock : IDisposable
{
    private readonly FileStream stream;
    private UpdateSessionLock(FileStream stream) => this.stream = stream;

    public static UpdateSessionLock Acquire(string directory)
    {
        var root = Path.GetFullPath(directory);
        UpdatePathSafety.EnsureDirectory(root);
        UpdatePathSafety.RejectLinks(root);
        var path = Path.Combine(root,"update-session.lock");
        UpdatePathSafety.RejectExistingLinks(path);
        return new UpdateSessionLock(new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None));
    }

    public void Dispose() => stream.Dispose();
}

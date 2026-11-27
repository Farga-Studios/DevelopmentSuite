namespace Suite.Updater;

/// <summary>Offline package retention. Use a private directory and serialize operations with the host lock.</summary>
public sealed class VerifiedPackageStore(string directory, string publicKeyPem, string product, string channel)
{
    private readonly string root = Path.GetFullPath(directory);

    private string VersionDirectory(string version)
    {
        if (!Version.TryParse(version, out var v) || v.Build < 0 || v.Revision >= 0 || v.ToString(3) != version)
            throw new ArgumentException("Invalid version.", nameof(version));
        return Path.Combine(root, version);
    }

    private static void RejectLink(string path) => UpdatePathSafety.RejectLinks(path);

    private static async Task<byte[]> ReadBoundedAsync(string path, int limit, CancellationToken token)
    {
        RejectLink(path);
        await using var stream = File.OpenRead(path);
        var bytes = new byte[limit + 1];
        int count = await stream.ReadAtLeastAsync(bytes, bytes.Length, false, token);
        if (count > limit) throw new InvalidDataException("Metadata too large.");
        return bytes[..count];
    }

    public async Task<PreparedUpdate> RetainAsync(string installer, byte[] metadata, byte[] signature,
        CancellationToken token = default)
    {
        if (metadata.Length > 65536 || signature.Length > 8192)
            throw new InvalidDataException("Metadata too large.");
        metadata = (byte[])metadata.Clone();
        signature = (byte[])signature.Clone();
        var manifest = SignedPackageMetadata.Authenticate(metadata, signature, publicKeyPem, product, channel);
        var destination = VersionDirectory(manifest.Version);
        Directory.CreateDirectory(root);
        RejectLink(root);
        if (Directory.Exists(destination))
        {
            var existing = await OpenAsync(manifest.Version, token);
            if (existing.Manifest != manifest) throw new InvalidDataException("Conflicting retained version.");
            return existing;
        }
        var staging = Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var package = Path.Combine(staging, manifest.FileName);
            RejectLink(installer);
            await using (var input = File.OpenRead(installer))
            await using (var output = new FileStream(package, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[65536];
                long total = 0;
                while (true)
                {
                    int n = await input.ReadAsync(buffer, token);
                    if (n == 0) break;
                    total += n;
                    if (total > manifest.Bytes) throw new InvalidDataException("Package exceeds signed size.");
                    await output.WriteAsync(buffer.AsMemory(0,n), token);
                }
                output.Flush(true);
            }
            await UpdateClient.VerifyPackageAsync(package, manifest, token);
            await File.WriteAllBytesAsync(Path.Combine(staging,"update.json"), metadata, token);
            await File.WriteAllBytesAsync(Path.Combine(staging,"update.json.sig"), signature, token);
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            return await OpenAsync(manifest.Version, token);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    public async Task<PreparedUpdate> OpenAsync(string version, CancellationToken token = default)
    {
        var folder = VersionDirectory(version);
        RejectLink(root);
        RejectLink(folder);
        var metadata = await ReadBoundedAsync(Path.Combine(folder,"update.json"),65536,token);
        var signature = await ReadBoundedAsync(Path.Combine(folder,"update.json.sig"),8192,token);
        var manifest = SignedPackageMetadata.Authenticate(metadata,signature,publicKeyPem,product,channel);
        if (manifest.Version != version) throw new InvalidDataException("Retained version mismatch.");
        var package = Path.Combine(folder,manifest.FileName);
        RejectLink(package);
        await UpdateClient.VerifyPackageAsync(package,manifest,token);
        return new PreparedUpdate(manifest,package);
    }
}

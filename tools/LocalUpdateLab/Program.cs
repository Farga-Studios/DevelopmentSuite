using System.Net;
using Suite.Updater;

if (args.Length != 4 || args[0] is not ("SpriteLab" or "MarkdownLab") || !Version.TryParse(args[1], out var installed))
{
    Console.Error.WriteLine("Usage: LocalUpdateLab <SpriteLab|MarkdownLab> <installed-version> <candidate-directory> <public-key.pem>");
    return 2;
}
var product = args[0];
var folder = Path.GetFullPath(args[2]);
var key = File.ReadAllText(args[3]);
var root = Path.Combine(Path.GetTempPath(), "FargaUpdateLab-" + Guid.NewGuid().ToString("N"));
try
{
    using var http = new HttpClient(new LocalTransport(folder, product));
    var client = new UpdateClient(http, new Uri($"https://raw.githubusercontent.com/Farga-Studios/{product}/main/channels/stable/update.json"), key, product, "stable");
    var manifest = await client.CheckAsync(installed) ?? throw new InvalidOperationException("No authenticated newer version.");
    using var session = UpdateSessionLock.Acquire(root);
    var path = await client.DownloadAsync(manifest, root, null);
    var store = new VerifiedPackageStore(Path.Combine(root, "packages"), key, product, "stable");
    await client.RetainDownloadedAsync(manifest, path, store);
    var retained = await store.OpenAsync(manifest.Version);
    if (retained.Manifest != manifest) throw new InvalidDataException("Retained package mismatch.");
    var journal = new UpdateJournal(root, product);
    var transaction = journal.Begin(new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build)).ToString(3), manifest.Version);
    journal.Advance(transaction.Id, UpdatePhase.HandoffStarted);
    var recovery = new UpdateRecovery(journal);
    if (recovery.Assess(Version.Parse(manifest.Version)).Status != RecoveryStatus.HealthCheckRequired)
        throw new InvalidOperationException("Unexpected recovery state.");
    var evidence = new InstallerExitEvidence(root);
    evidence.Prepare(transaction.Id);
    if (evidence.HasCompletion(transaction.Id)) throw new InvalidOperationException("Unexpected completion receipt.");
    Console.WriteLine($"PASS: {product} {installed} -> {manifest.Version}; signature, download hash, retention and journal verified.");
    Console.WriteLine("No installer executed. No production files or endpoints changed. Completion and real rollback remain Windows acceptance tests.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("FAIL: " + ex.Message);
    return 1;
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, true);
}

sealed class LocalTransport(string folder, string product) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var uri = request.RequestUri ?? throw new InvalidOperationException("Missing URI.");
        var metadata = $"https://raw.githubusercontent.com/Farga-Studios/{product}/main/channels/stable/update.json";
        string path;
        if (uri.AbsoluteUri == metadata || uri.AbsoluteUri == metadata + ".sig")
            path = Path.Combine(folder, "metadata", uri.AbsoluteUri.EndsWith(".sig", StringComparison.Ordinal) ? "update.json.sig" : "update.json");
        else
        {
            var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (uri.Scheme != "https" || uri.Host != "github.com" || parts.Length != 6 ||
                parts[0] != "Farga-Studios" || parts[1] != product || parts[2] != "releases" || parts[3] != "download" ||
                !parts[4].StartsWith('v') || !Version.TryParse(parts[4][1..], out var version) ||
                version.Build < 0 || version.Revision >= 0 || parts[4] != "v" + version.ToString(3) ||
                parts[5] != $"{product}-{version.ToString(3)}-win-x64-setup.exe")
                throw new InvalidOperationException("Unexpected test request: " + uri);
            path = Path.Combine(folder, parts[5]);
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(File.OpenRead(path)) });
    }
}

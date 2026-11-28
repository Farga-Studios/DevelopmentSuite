using System.Net;
using System.Diagnostics;
using Suite.Updater;

if (args.Length == 4 && args[0] == "--resolve-cancelled")
    return ResolveCancelled(args[1], args[2], args[3]);

if ((args.Length != 4 && (args.Length != 7 || args[4] != "--install")) || args[0] is not ("SpriteLab" or "MarkdownLab") || !Version.TryParse(args[1], out var installed))
{
    Console.Error.WriteLine("Usage: LocalUpdateLab <SpriteLab|MarkdownLab> <installed-version> <candidate-directory> <public-key.pem>");
    Console.Error.WriteLine("Optional Windows acceptance: --install <previous-signed-directory> <installed-app-directory>");
    return 2;
}
var product = args[0];
var folder = Path.GetFullPath(args[2]);
var key = File.ReadAllText(args[3]);
var install = args.Length == 7;
var root = install
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), product,
        product == "SpriteLab" ? "Updates" : Path.Combine("Workspace", "updates"))
    : Path.Combine(Path.GetTempPath(), "FargaUpdateLab-" + Guid.NewGuid().ToString("N"));
try
{
    using var http = new HttpClient(new LocalTransport(folder, product));
    var client = new UpdateClient(http, new Uri($"https://raw.githubusercontent.com/Farga-Studios/{product}/main/channels/stable/update.json"), key, product, "stable");
    var manifest = await client.CheckAsync(installed) ?? throw new InvalidOperationException("No authenticated newer version.");
    if (install)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var appDirectory = Path.GetFullPath(args[6]);
        var executable = product == "SpriteLab" ? "SpriteLab.App" : "MarkdownLab";
        var uninstallId = product == "SpriteLab" ? "{B637E3EE-3A57-474F-9F87-C70E3F12AE3B}" : "Sealsworn.MarkdownLab";
        if (!InstallerHandoff.IsInstalled(appDirectory, uninstallId))
            throw new InvalidOperationException("Installed application location does not match registry.");
        var file = FileVersionInfo.GetVersionInfo(Path.Combine(appDirectory, executable + ".exe"));
        if (new Version(file.FileMajorPart,file.FileMinorPart,file.FileBuildPart) != new Version(installed.Major,installed.Minor,Math.Max(0,installed.Build)))
            throw new InvalidOperationException("Installed executable version differs from requested baseline.");
        var processes = Process.GetProcessesByName(executable);
        try { if (processes.Length != 0) throw new InvalidOperationException("Close the application before continuing."); }
        finally { foreach (var process in processes) process.Dispose(); }
        Console.WriteLine($"REAL INSTALLATION: {product} {installed} -> {manifest.Version}.");
        Console.WriteLine("Use only a backed-up test installation. Close all setup processes. Type INSTALL to proceed:");
        if (Console.ReadLine() != "INSTALL") return 2;
    }
    using var session = UpdateSessionLock.Acquire(root);
    var journal = new UpdateJournal(root, product);
    if (install && new UpdateRecovery(journal).Assess(installed).Status is not (RecoveryStatus.NoTransaction or RecoveryStatus.Healthy or RecoveryStatus.Aborted or RecoveryStatus.RolledBack))
        throw new InvalidOperationException("Unresolved update journal. Do not overwrite it.");
    var path = await client.DownloadAsync(manifest, root, null);
    var store = new VerifiedPackageStore(Path.Combine(root, "packages"), key, product, "stable");
    await client.RetainDownloadedAsync(manifest, path, store);
    var retained = await store.OpenAsync(manifest.Version);
    if (retained.Manifest != manifest) throw new InvalidDataException("Retained package mismatch.");
    if (install)
    {
        using var previousHttp = new HttpClient(new LocalTransport(Path.GetFullPath(args[5]), product));
        var previousClient = new UpdateClient(previousHttp, new Uri($"https://raw.githubusercontent.com/Farga-Studios/{product}/main/channels/stable/update.json"), key, product, "stable");
        var previous = await previousClient.CheckAsync(new Version(0,0,0)) ?? throw new InvalidDataException("Missing authenticated baseline.");
        if (Version.Parse(previous.Version) != new Version(installed.Major, installed.Minor, Math.Max(0,installed.Build)))
            throw new InvalidDataException("Baseline package version mismatch.");
        var previousPath = await previousClient.DownloadAsync(previous, root, null);
        await previousClient.RetainDownloadedAsync(previous, previousPath, store);
    }
    var transaction = journal.Begin(new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build)).ToString(3), manifest.Version);
    journal.Advance(transaction.Id, UpdatePhase.HandoffStarted);
    var recovery = new UpdateRecovery(journal);
    if (recovery.Assess(Version.Parse(manifest.Version)).Status != RecoveryStatus.HealthCheckRequired)
        throw new InvalidOperationException("Unexpected recovery state.");
    var evidence = new InstallerExitEvidence(root);
    evidence.Prepare(transaction.Id);
    if (evidence.HasCompletion(transaction.Id)) throw new InvalidOperationException("Unexpected completion receipt.");
    if (install)
    {
        await InstallerHandoff.LaunchAsync(new PreparedUpdate(manifest,path), root,
            applicationUpdateSwitch: product == "MarkdownLab" ? "/MARKDOWNLABUPDATE" : null,
            transactionId: transaction.Id);
        Console.WriteLine("Installer launched. Complete setup, then open the application and use its recovery menu. Journal and signed packages preserved.");
        return 0;
    }
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
    if (!install && Directory.Exists(root)) Directory.Delete(root, true);
}

static int ResolveCancelled(string product, string versionText, string appDirectory)
{
    try
    {
        if (!OperatingSystem.IsWindows() || product is not ("SpriteLab" or "MarkdownLab") ||
            !Version.TryParse(versionText, out var version) || version.Build < 0 || version.Revision >= 0)
            throw new InvalidOperationException("Invalid recovery arguments.");
        var executable = product == "SpriteLab" ? "SpriteLab.App" : "MarkdownLab";
        var uninstallId = product == "SpriteLab" ? "{B637E3EE-3A57-474F-9F87-C70E3F12AE3B}" : "Sealsworn.MarkdownLab";
        if (!InstallerHandoff.IsInstalled(appDirectory, uninstallId))
            throw new InvalidOperationException("Installation location mismatch.");
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), product,
            product == "SpriteLab" ? "Updates" : Path.Combine("Workspace", "updates"));
        using var session = UpdateSessionLock.Acquire(root);
        var journal = new UpdateJournal(root, product);
        var recovery = new UpdateRecovery(journal);
        var assessment = recovery.Assess(version);
        if (assessment.Status != RecoveryStatus.Interrupted || assessment.Transaction is not { } transaction ||
            transaction.Phase is not (UpdatePhase.HandoffStarted or UpdatePhase.AwaitingHealthCheck))
            throw new InvalidOperationException("No matching interrupted installation.");
        if (new InstallerExitEvidence(root).HasCompletion(transaction.Id))
            throw new InvalidOperationException("Completion receipt exists; manual diagnosis required.");
        var file = FileVersionInfo.GetVersionInfo(Path.Combine(appDirectory, executable + ".exe"));
        if (new Version(file.FileMajorPart,file.FileMinorPart,file.FileBuildPart) != version)
            throw new InvalidOperationException("Previous executable version mismatch.");
        Console.WriteLine("Only for a cancelled setup before file copying. Verify all installer processes have exited and the previous application opens, edits and saves successfully. Close the application again.");
        Console.WriteLine("Type ABORT to acknowledge these checks, or press Enter to leave the journal unchanged.");
        if (Console.ReadLine() != "ABORT") return 2;
        var processes = Process.GetProcesses();
        try
        {
            if (processes.Any(p => p.ProcessName.Equals(executable,StringComparison.OrdinalIgnoreCase) ||
                p.ProcessName.Contains("-win-x64-setup",StringComparison.OrdinalIgnoreCase) ||
                p.ProcessName.Contains("MarkdownLab-Setup",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Application or possible installer still running.");
        }
        finally { foreach (var p in processes) p.Dispose(); }
        recovery.ResolveInterrupted(transaction.Id, version, true, true, true);
        Console.WriteLine("PASS: interrupted transaction closed as Aborted. Signed packages preserved.");
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); return 1; }
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

using System.Security.Cryptography;
using System.Text.Json;
using Suite.Updater;
using Xunit;

public sealed class RecoveryIntegrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"suite-integration-"+Guid.NewGuid().ToString("N"));
    private readonly RSA rsa = RSA.Create(2048);
    private string Cache => Path.Combine(root,"cache");
    private UpdateJournal Journal => new(root,"SpriteLab");
    private VerifiedPackageStore Store => new(Cache,rsa.ExportSubjectPublicKeyInfoPem(),"SpriteLab","stable");

    private async Task Retain(string version)
    {
        Directory.CreateDirectory(root);
        byte[] bytes = [1,2,3,4];
        var source = Path.Combine(root,"source.exe");
        File.WriteAllBytes(source,bytes);
        var manifest = new UpdateManifest("SpriteLab","stable",version,$"SpriteLab-{version}-win-x64-setup.exe",bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)),"test");
        var data = JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        await Store.RetainAsync(source,data,rsa.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateLifecycleSurvivesComponentRecreation(bool healthy)
    {
        await Retain("1.0.0");
        await Retain("1.1.0");
        var t = Journal.Begin("1.0.0","1.1.0");
        Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        var recovery = new UpdateRecovery(Journal);
        Assert.Equal(RecoveryStatus.HealthCheckRequired,recovery.Assess(new Version("1.1.0")).Status);
        if (healthy)
        {
            recovery.ConfirmHealthy(t.Id,new Version("1.1.0"));
            Assert.Equal(RecoveryStatus.Healthy,new UpdateRecovery(Journal).Assess(new Version("1.1.0")).Status);
        }
        else
        {
            recovery.RequireRecovery(t.Id);
            var result = await ObsoletePackageCleanup.RunAsync(Cache,Store,"1.1.0","1.0.0",Journal);
            Assert.Equal(0,result.Removed);
            var prepared = await new RollbackManager(recovery,Store).PrepareAsync(t.Id,new Version("1.1.0"),true,true,Path.Combine(root,"staging"));
            Assert.Equal("1.0.0",prepared.Manifest.Version);
            recovery.MarkRollbackStarted(t.Id,new Version("1.1.0"));
            var restarted = new UpdateRecovery(Journal);
            Assert.Equal(RecoveryStatus.RollbackHealthCheckRequired,restarted.Assess(new Version("1.0.0")).Status);
            restarted.ConfirmRollbackHealthy(t.Id,new Version("1.0.0"));
            Assert.Equal(RecoveryStatus.RolledBack,new UpdateRecovery(Journal).Assess(new Version("1.0.0")).Status);
        }
        Assert.Equal(0,(await ObsoletePackageCleanup.RunAsync(Cache,Store,"1.1.0","1.0.0",Journal)).Removed);
    }

    public void Dispose()
    {
        rsa.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

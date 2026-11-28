using System.Security.Cryptography;
using System.Text.Json;
using Suite.Updater;
using Xunit;

public sealed class RollbackManagerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "suite-rollback-manager-" + Guid.NewGuid().ToString("N"));
    private readonly RSA rsa = RSA.Create(2048);
    private readonly byte[] payload = [1,2,3,4];
    private UpdateJournal Journal => new(root,"SpriteLab");
    private UpdateRecovery Recovery => new(Journal);
    private VerifiedPackageStore Store => new(Path.Combine(root,"cache"),rsa.ExportSubjectPublicKeyInfoPem(),"SpriteLab","stable");
    private RollbackManager Manager => new(Recovery,Store);
    private string Staging => Path.Combine(root,"staging");

    private UpdateTransaction Failed()
    {
        var t = Journal.Begin("1.0.0","1.1.0");
        Journal.Advance(t.Id,UpdatePhase.HandoffStarted);
        return Recovery.RequireRecovery(t.Id);
    }

    private async Task<PreparedUpdate> Retain()
    {
        Directory.CreateDirectory(root);
        var installer = Path.Combine(root,"source.exe");
        File.WriteAllBytes(installer,payload);
        var manifest = new UpdateManifest("SpriteLab","stable","1.0.0","SpriteLab-1.0.0-win-x64-setup.exe",payload.Length,Convert.ToHexString(SHA256.HashData(payload)),"test");
        var data = JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        var signature = rsa.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        return await Store.RetainAsync(installer,data,signature);
    }

    [Fact]
    public async Task StagesVerifiedPreviousInstallerWithoutLaunching()
    {
        var t = Failed();
        var retained = await Retain();
        var prepared = await Manager.PrepareAsync(t.Id,new Version("1.1.0"),true,true,Staging);
        Assert.Equal(retained.Manifest,prepared.Manifest);
        Assert.Equal(Staging,Path.GetDirectoryName(prepared.Path));
        Assert.EndsWith("-"+retained.Manifest.FileName,prepared.Path);
        Assert.Equal(payload,File.ReadAllBytes(prepared.Path));
        Assert.Equal(UpdatePhase.RecoveryRequired,Journal.Read().Transaction!.Phase);
    }

    [Theory]
    [InlineData(false,true)]
    [InlineData(true,false)]
    [InlineData(false,false)]
    public async Task RequiresConsentAndCompatibility(bool approved,bool compatible)
    {
        var t = Failed();
        await Retain();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Manager.PrepareAsync(t.Id,new Version("1.1.0"),approved,compatible,Staging));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task RejectsWrongTransactionOrRunningVersion()
    {
        var t = Failed();
        await Retain();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Manager.PrepareAsync(Guid.NewGuid(),new Version("1.1.0"),true,true,Staging));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Manager.PrepareAsync(t.Id,new Version("1.0.0"),true,true,Staging));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task RejectsTamperedRetainedPackage()
    {
        var t = Failed();
        var retained = await Retain();
        File.WriteAllBytes(retained.Path,[4,3,2,1]);
        await Assert.ThrowsAsync<CryptographicException>(() => Manager.PrepareAsync(t.Id,new Version("1.1.0"),true,true,Staging));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public async Task MissingPackageCannotPrepareRollback()
    {
        var t = Failed();
        await Assert.ThrowsAnyAsync<IOException>(() => Manager.PrepareAsync(t.Id,new Version("1.1.0"),true,true,Staging));
        Assert.False(Directory.Exists(Staging));
    }

    public void Dispose()
    {
        rsa.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

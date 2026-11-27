using System.Security.Cryptography;
using System.Text.Json;
using Suite.Updater;
using Xunit;

public sealed class ObsoletePackageCleanupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"suite-obsolete-"+Guid.NewGuid().ToString("N"));
    private readonly RSA rsa = RSA.Create(2048);
    private string Cache => Path.Combine(root,"cache");
    private UpdateJournal Journal => new(root,"SpriteLab");
    private VerifiedPackageStore Store => new(Cache,rsa.ExportSubjectPublicKeyInfoPem(),"SpriteLab","stable");

    private async Task<PreparedUpdate> Retain(string version)
    {
        Directory.CreateDirectory(root);
        byte[] bytes = [1,2,3,4];
        var source = Path.Combine(root,"source.exe");
        File.WriteAllBytes(source,bytes);
        var manifest = new UpdateManifest("SpriteLab","stable",version,$"SpriteLab-{version}-win-x64-setup.exe",bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)),"test");
        var data = JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return await Store.RetainAsync(source,data,rsa.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task RemovesOnlyVerifiedObsoleteVersion()
    {
        var old = await Retain("0.8.0");
        var previous = await Retain("1.0.0");
        var current = await Retain("1.1.0");
        var future = await Retain("1.2.0");
        var result = await ObsoletePackageCleanup.RunAsync(Cache,Store,"1.1.0","1.0.0",Journal);
        Assert.Equal(1,result.Removed);
        Assert.False(File.Exists(old.Path));
        foreach(var package in new[]{previous,current,future}) Assert.True(File.Exists(package.Path));
    }

    [Fact]
    public async Task PreservesTamperedAndUnexpectedContent()
    {
        var tampered = await Retain("0.8.0");
        var extra = await Retain("0.9.0");
        File.WriteAllBytes(tampered.Path,[4,3,2,1]);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(extra.Path)!,"unknown.txt"),"preserve");
        var result = await ObsoletePackageCleanup.RunAsync(Cache,Store,"1.1.0","1.0.0",Journal);
        Assert.Equal(0,result.Removed);
        Assert.Equal(2,result.Skipped);
        Assert.True(File.Exists(tampered.Path));
        Assert.True(File.Exists(extra.Path));
    }

    [Fact]
    public async Task ProtectsTransactionAndInvalidJournal()
    {
        var old = await Retain("0.8.0");
        var t = Journal.Begin("0.8.0","1.1.0");
        Assert.Equal(0,(await ObsoletePackageCleanup.RunAsync(Cache,Store,"1.1.0","1.0.0",Journal)).Removed);
        File.WriteAllText(Path.Combine(root,"SpriteLab.journal.json"),"{");
        Assert.Equal(0,(await ObsoletePackageCleanup.RunAsync(Cache,Store,"1.1.0","1.0.0",Journal)).Removed);
        Assert.True(File.Exists(old.Path));
    }

    public void Dispose()
    {
        rsa.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

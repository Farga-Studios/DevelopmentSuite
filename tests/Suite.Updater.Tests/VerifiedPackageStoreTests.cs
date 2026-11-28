using System.Security.Cryptography;
using System.Text.Json;
using Suite.Updater;
using Xunit;

public sealed class VerifiedPackageStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "suite-store-" + Guid.NewGuid().ToString("N"));
    private readonly RSA rsa = RSA.Create(2048);
    private readonly byte[] payload = [1,2,3,4];
    private VerifiedPackageStore Store => new(Path.Combine(root,"cache"), rsa.ExportSubjectPublicKeyInfoPem(),"SpriteLab","stable");
    private string Installer => Path.Combine(root,"installer.exe");

    private (byte[] Data, byte[] Signature) Metadata(string product = "SpriteLab")
    {
        var m = new UpdateManifest(product,"stable","1.2.3",$"{product}-1.2.3-win-x64-setup.exe",payload.Length,Convert.ToHexString(SHA256.HashData(payload)),"test");
        var data = JsonSerializer.SerializeToUtf8Bytes(m,new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return (data,rsa.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
    }

    private void Prepare()
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Installer,payload);
    }

    [Fact]
    public async Task RetainsAndReopensOffline()
    {
        Prepare();
        var (data,sig) = Metadata();
        var saved = await Store.RetainAsync(Installer,data,sig);
        File.Delete(Installer);
        var reopened = await Store.OpenAsync("1.2.3");
        Assert.Equal(saved,reopened);
        Assert.Equal(payload,File.ReadAllBytes(reopened.Path));
        Assert.Equal(data,File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(reopened.Path)!,"update.json")));
        Assert.Equal(sig,File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(reopened.Path)!,"update.json.sig")));
    }

    [Fact]
    public async Task RejectsTamperedInstaller()
    {
        Prepare();
        var (data,sig) = Metadata();
        var saved = await Store.RetainAsync(Installer,data,sig);
        File.WriteAllBytes(saved.Path,[4,3,2,1]);
        await Assert.ThrowsAsync<CryptographicException>(() => Store.OpenAsync("1.2.3"));
    }

    [Fact]
    public async Task RejectsTamperedMetadata()
    {
        Prepare();
        var (data,sig) = Metadata();
        data[0] ^= 1;
        await Assert.ThrowsAsync<CryptographicException>(() => Store.RetainAsync(Installer,data,sig));
    }

    [Fact]
    public async Task RejectsWrongProduct()
    {
        Prepare();
        var (data,sig) = Metadata("MarkdownLab");
        await Assert.ThrowsAsync<InvalidDataException>(() => Store.RetainAsync(Installer,data,sig));
    }

    [Fact]
    public async Task RejectsOversizedInstallerWithoutPublishing()
    {
        Prepare();
        var (data,sig) = Metadata();
        File.WriteAllBytes(Installer,[1,2,3,4,5]);
        await Assert.ThrowsAsync<InvalidDataException>(() => Store.RetainAsync(Installer,data,sig));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root,"cache")));
    }

    [Fact]
    public async Task RejectsTraversal()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.OpenAsync("../1.2.3"));
    }

    public void Dispose()
    {
        rsa.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }
}

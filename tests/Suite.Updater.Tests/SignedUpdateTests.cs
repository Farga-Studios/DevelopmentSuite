using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Suite.Updater;
using Xunit;

namespace Suite.Updater.Tests;

public sealed class SignedUpdateTests
{
    private static readonly Uri Metadata = new("https://example.invalid/update.json");
    private const string PackageUrl = "https://github.com/Farga-Studios/SpriteLab/releases/download/v0.6.9/SpriteLab-0.6.9-win-x64-setup.exe";
    private static readonly byte[] Payload = [1, 2, 3, 4];

    private sealed class Fixture : IDisposable
    {
        private readonly RSA rsa = RSA.Create(2048);
        public UpdateManifest Manifest = new("SpriteLab", "stable", "0.6.9",
            "SpriteLab-0.6.9-win-x64-setup.exe", Payload.Length,
            Convert.ToHexString(SHA256.HashData(Payload)), "Test");
        public bool CorruptSignature;
        public byte[] Package = Payload;
        public readonly List<string> Requests = [];
        public string Root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        private readonly HttpClient http;

        public Fixture() => http = new HttpClient(new Handler(this));

        public UpdateClient Client(string owner = "Farga-Studios", string? repository = null) =>
            new(http, Metadata, rsa.ExportSubjectPublicKeyInfoPem(), "SpriteLab", "stable", owner, repository);

        private sealed class Handler(Fixture f) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var url = request.RequestUri!.AbsoluteUri;
                f.Requests.Add(url);
                var data = JsonSerializer.SerializeToUtf8Bytes(f.Manifest,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                byte[] bytes;
                if (url == Metadata.AbsoluteUri) bytes = data;
                else if (url == Metadata.AbsoluteUri + ".sig")
                {
                    bytes = f.rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    if (f.CorruptSignature) bytes[0] ^= 1;
                }
                else if (url == PackageUrl || url == PackageUrl.Replace("Farga-Studios/SpriteLab", "OtherOwner/OtherRepo")) bytes = f.Package;
                else return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }
        }
        public void Dispose()
        {
            http.Dispose();
            rsa.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    [Theory]
    [InlineData("0.6.8", true)]
    [InlineData("0.6.9", false)]
    [InlineData("0.6.9.0", false)]
    [InlineData("0.7.0", false)]
    public async Task OnlyNewerSignedVersionIsOffered(string installed, bool available)
    {
        using var f = new Fixture();
        Assert.Equal(available, await f.Client().CheckAsync(Version.Parse(installed)) is not null);
    }

    [Fact]
    public async Task InvalidSignatureIsRejected()
    {
        using var f = new Fixture { CorruptSignature = true };
        await Assert.ThrowsAsync<CryptographicException>(() => f.Client().CheckAsync(new Version(0, 6, 8)));
        Assert.DoesNotContain(PackageUrl, f.Requests);
    }

    [Theory]
    [InlineData("wrong-product")]
    [InlineData("wrong-channel")]
    [InlineData("wrong-filename")]
    [InlineData("invalid-version")]
    public async Task SignedButInvalidManifestIsRejected(string kind)
    {
        using var f = new Fixture();
        f.Manifest = kind switch
        {
            "wrong-product" => f.Manifest with { Product = "Other" },
            "wrong-channel" => f.Manifest with { Channel = "beta" },
            "wrong-filename" => f.Manifest with { FileName = "../setup.exe" },
            _ => f.Manifest with { Version = "0.6.9.1" }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Client().CheckAsync(new Version(0, 6, 8)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifiedPackageUsesConfiguredRepository(bool custom)
    {
        using var f = new Fixture();
        var client = custom ? f.Client("OtherOwner", "OtherRepo") : f.Client();
        var m = await client.CheckAsync(new Version(0, 6, 8));
        Assert.NotNull(m);
        var path = await client.DownloadAsync(m!, f.Root, null);
        Assert.Equal(Payload, await File.ReadAllBytesAsync(path));
        Assert.Contains(custom ? PackageUrl.Replace("Farga-Studios/SpriteLab", "OtherOwner/OtherRepo") : PackageUrl, f.Requests);
        await UpdateClient.VerifyPackageAsync(path, m!);
    }

    [Fact]
    public async Task ChangedMetadataCannotAuthorizeDownload()
    {
        using var f = new Fixture();
        var client = f.Client();
        var m = await client.CheckAsync(new Version(0, 6, 8));
        f.Manifest = f.Manifest with { Notes = "Changed" };
        await Assert.ThrowsAsync<CryptographicException>(() => client.DownloadAsync(m!, f.Root, null));
        Assert.DoesNotContain(PackageUrl, f.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BadPayloadIsRemoved(bool oversized)
    {
        using var f = new Fixture { Package = oversized ? [1, 2, 3, 4, 5] : [4, 3, 2, 1] };
        var client = f.Client();
        var m = await client.CheckAsync(new Version(0, 6, 8));
        if (oversized)
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(m!, f.Root, null));
        else
            await Assert.ThrowsAsync<CryptographicException>(() => client.DownloadAsync(m!, f.Root, null));
        Assert.Empty(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task CancelledDownloadDoesNotCreateInstaller()
    {
        using var f = new Fixture();
        var client = f.Client();
        var m = await client.CheckAsync(new Version(0, 6, 8));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync(m!, f.Root, null, cts.Token));
        Assert.False(Directory.Exists(f.Root));
    }
}

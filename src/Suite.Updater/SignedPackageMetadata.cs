using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suite.Updater;

/// <summary>Authenticates retained metadata without consulting the current release channel.</summary>
public static class SignedPackageMetadata
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static UpdateManifest Authenticate(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature,
        string publicKeyPem, string product, string channel)
    {
        if (data.Length is < 1 or > 65536 || signature.Length is < 1 or > 8192)
            throw new InvalidDataException("Invalid metadata size.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        if (rsa.KeySize < 2048 || !rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new CryptographicException("Update signature invalid.");
        var m = JsonSerializer.Deserialize<UpdateManifest>(data, Json);
        if (m is null || string.IsNullOrEmpty(product) || !product.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_') ||
            string.IsNullOrWhiteSpace(channel) || m.Product != product || m.Channel != channel ||
            !Version.TryParse(m.Version, out var version) || version.Build < 0 || version.Revision >= 0 ||
            m.Version != version.ToString(3) || version <= new Version(0,0,0) ||
            m.FileName != $"{product}-{m.Version}-win-x64-setup.exe" ||
            m.Bytes is < 1 or > 536870912 || m.Sha256 is null || m.Sha256.Length != 64 ||
            !m.Sha256.All(Uri.IsHexDigit) || m.Notes is null || m.Notes.Length > 20000)
            throw new InvalidDataException("Invalid update manifest.");
        return m;
    }
}

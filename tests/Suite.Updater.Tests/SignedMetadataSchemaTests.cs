using System.Security.Cryptography;
using System.Text;
using Suite.Updater;
using Xunit;

public sealed class SignedMetadataSchemaTests
{
    [Theory]
    [InlineData("")]
    [InlineData(",\"notes\":\"first\",\"notes\":\"second\"")]
    [InlineData(",\"notes\":\"valid\",\"unknown\":true")]
    public void RejectsAmbiguousSchemaEvenWithValidSignature(string suffix)
    {
        using var rsa = RSA.Create(2048);
        var json = "{\"product\":\"SpriteLab\",\"channel\":\"stable\",\"version\":\"1.0.0\",\"file_name\":\"SpriteLab-1.0.0-win-x64-setup.exe\",\"bytes\":4,\"sha256\":\""+new string('a',64)+"\""+suffix+"}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var signature = rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        Assert.Throws<InvalidDataException>(() => SignedPackageMetadata.Authenticate(bytes,signature,rsa.ExportSubjectPublicKeyInfoPem(),"SpriteLab","stable"));
    }
}

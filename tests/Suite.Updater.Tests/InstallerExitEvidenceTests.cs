using System.Text;
using Suite.Updater;
using Xunit;

namespace Suite.Updater.Tests;

public sealed class InstallerExitEvidenceTests
{
    [Fact]
    public void CompletionMustMatchTransactionAndBeResetForNewAttempt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var evidence = new InstallerExitEvidence(root);
            var id = Guid.NewGuid();
            var path = evidence.Prepare(id);
            Assert.False(evidence.HasCompletion(id));
            File.WriteAllText(path, "completed:" + Guid.NewGuid().ToString("N"), Encoding.ASCII);
            Assert.False(evidence.HasCompletion(id));
            File.WriteAllText(path, "completed:" + id.ToString("N") + "\n", Encoding.ASCII);
            Assert.False(evidence.HasCompletion(id));
            File.WriteAllText(path, "completed:" + id.ToString("N"), Encoding.ASCII);
            Assert.True(evidence.HasCompletion(id));
            Assert.False(evidence.HasCompletion(Guid.NewGuid()));
            evidence.Prepare(id);
            Assert.False(evidence.HasCompletion(id));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

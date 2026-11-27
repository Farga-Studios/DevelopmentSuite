using Suite.Updater;
using Xunit;

public sealed class JournalSchemaTests
{
    [Theory]
    [InlineData("")]
    [InlineData(",\"Phase\":\"Prepared\",\"Phase\":\"Completed\"")]
    public void RejectsMissingOrDuplicatePhase(string suffix)
    {
        var root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root,"SpriteLab.journal.json"),
                "{\"Id\":\""+Guid.NewGuid()+"\",\"Product\":\"SpriteLab\",\"PreviousVersion\":\"1.0.0\",\"TargetVersion\":\"1.1.0\""+suffix+"}");
            var journal = new UpdateJournal(root,"SpriteLab");
            Assert.Equal(JournalStatus.Invalid,journal.Read().Status);
            Assert.Throws<InvalidOperationException>(() => journal.Begin("1.0.0","1.2.0"));
        }
        finally { Directory.Delete(root,true); }
    }
}

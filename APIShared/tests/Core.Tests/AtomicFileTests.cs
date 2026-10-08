using APIShared.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace APIShared.Core.Tests;
[TestClass]
public class AtomicFileTests
{
    [TestMethod] public void ReplacementPublishesCompleteContentAndConsumesCandidate()
    {
        string directory=Path.Combine(Path.GetTempPath(),"APIShared-Atomic-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source=Path.Combine(directory,"candidate"), target=Path.Combine(directory,"settings");
            File.WriteAllText(source,"new"); File.WriteAllText(target,"previous");
            AtomicFileReplacement.Replace(source,target);
            Assert.AreEqual("new",File.ReadAllText(target)); Assert.IsFalse(File.Exists(source));
            Assert.ThrowsExactly<IOException>(() => AtomicFileReplacement.Replace(source,target));
            Assert.AreEqual("new",File.ReadAllText(target),"Failed publication preserves the previous complete file.");
        }
        finally { Directory.Delete(directory,true); }
    }
}

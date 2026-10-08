from pathlib import Path
root=Path.cwd().parent/'SHCDE-APIShared'
def write(p,t):
    p.parent.mkdir(parents=True,exist_ok=True)
    data=t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'); p.write_bytes(data); assert p.read_bytes()==data
for p in [root/'Directory.Build.props',root/'Directory.Build.targets']:
    text=p.read_text(encoding='utf-8').replace('plugins\x00shcdese','plugins\\000shcdese')
    assert '\x00' not in text
    write(p,text)
write(root/'tests/Core.Tests/JsonTests.cs',r'''using APIShared.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
namespace APIShared.Core.Tests;
[TestClass]
public class JsonTests
{
    [TestMethod]
    public void NumbersEscapesAndRoundTripAreCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var payload = (Dictionary<string, object>)DependencyFreeJson.Parse("{\"number\":1.25,\"max\":18446744073709551615,\"text\":\"line\\n\\\"\\\\ä\",\"array\":[null,true,false,-2]}");
            Assert.AreEqual(1.25, payload["number"]);
            Assert.AreEqual(ulong.MaxValue, payload["max"]);
            Assert.AreEqual("line\n\"\\ä", payload["text"]);
            var encoded = DependencyFreeJson.Serialize(payload);
            Assert.IsTrue(encoded.EndsWith("\r\n", StringComparison.Ordinal));
            var decoded = (Dictionary<string, object>)DependencyFreeJson.Parse(encoded);
            Assert.AreEqual(payload["number"], decoded["number"]);
            Assert.AreEqual(payload["text"], decoded["text"]);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [DataTestMethod]
    [DataRow("{\"x\":1,\"x\":2}")]
    [DataRow("{\"x\":1,}")]
    [DataRow("[1,]")]
    [DataRow("01")]
    [DataRow("1e999")]
    [DataRow("true false")]
    [DataRow("\"bad\ntext\"")]
    [DataRow("{")]
    [DataRow("[")]
    [DataRow("")]
    public void InvalidInputIsRejected(string input) =>
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Parse(input));
    [TestMethod] public void TrailingCommaIsExplicitlyOptIn()
    {
        var parsed = (List<object>)DependencyFreeJson.Parse("[1,]", true);
        Assert.HasCount(1, parsed);
    }
    [TestMethod] public void DepthNonFiniteNumbersAndCyclesAreRejected()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Parse(new string('[',70)+"0"+new string(']',70)));
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Serialize(double.NaN));
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Serialize(double.PositiveInfinity));
        var cycle = new List<object>(); cycle.Add(cycle);
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Serialize(cycle));
    }
}
''')
write(root/'tests/Core.Tests/PatternTests.cs',r'''using APIShared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace APIShared.Core.Tests;
[TestClass]
public class PatternTests
{
    [TestMethod] public void UniqueMissingAndAmbiguousMatchesAreDistinct()
    {
        var pattern = CompiledBytePattern.Parse("AB ? CD");
        Assert.AreEqual(1, pattern.FindUnique(new byte[] { 0,0xAB,3,0xCD,0 }));
        Assert.AreEqual(-1, pattern.FindUnique(new byte[] { 0xAB,3 }));
        Assert.AreEqual(-2, pattern.FindUnique(new byte[] { 0xAB,3,0xCD,0xAB,4,0xCD }));
    }
    [TestMethod] public void BoundariesAndAllWildcardsAreHandled()
    {
        Assert.AreEqual(0, CompiledBytePattern.Parse("01 02").FindUnique(new byte[] {1,2}));
        Assert.AreEqual(0, CompiledBytePattern.Parse("? ?").FindUnique(new byte[] {1,2}));
        Assert.AreEqual(-2, CompiledBytePattern.Parse("? ?").FindUnique(new byte[] {1,2,3}));
        Assert.AreEqual(-1, CompiledBytePattern.Parse("? ?").FindUnique(new byte[] {1}));
    }
    [TestMethod] public void CompiledSearchAgreesWithIndependentReferenceSearch()
    {
        var random = new Random(0x53E2);
        for (int trial=0; trial<2000; trial++)
        {
            byte[] memory = new byte[random.Next(0,128)]; random.NextBytes(memory);
            string[] tokens = Enumerable.Range(0,random.Next(1,8)).Select(_ => random.Next(3)==0 ? "?" : random.Next(8).ToString("X2")).ToArray();
            int expected=-1;
            for (int start=0; start<=memory.Length-tokens.Length; start++)
            {
                bool match=true;
                for (int i=0;i<tokens.Length;i++)
                    if (tokens[i]!="?" && memory[start+i]!=Convert.ToByte(tokens[i],16)) { match=false; break; }
                if (match) { if (expected>=0) { expected=-2; break; } expected=start; }
            }
            Assert.AreEqual(expected, CompiledBytePattern.Parse(string.Join(" ",tokens)).FindUnique(memory), $"trial {trial}");
        }
    }
}
''')
write(root/'tests/Core.Tests/AtomicFileTests.cs',r'''using APIShared.Internal;
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
            Assert.ThrowsExactly<System.ComponentModel.Win32Exception>(() => AtomicFileReplacement.Replace(source,target));
            Assert.AreEqual("new",File.ReadAllText(target),"Failed publication preserves the previous complete file.");
        }
        finally { Directory.Delete(directory,true); }
    }
}
''')
print('Core behavioral tests written; common path configuration validated for NUL bytes.')

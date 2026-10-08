using APIShared;
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

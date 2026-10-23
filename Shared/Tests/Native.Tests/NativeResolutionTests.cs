using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared;
using System;
using System.IO;

[TestClass]
public sealed class NativeResolutionTests
{
    private const int CodeRva = 0x1000, DataRva = 0x2000, ReadOnlyRva = 0x2800;
    private const int RipLoad = CodeRva + 0x10, FieldLoad = CodeRva + 0x20, StrideMul = CodeRva + 0x30;
    private const string Pattern = "8B 05 ?? ?? ?? ?? 90 90";

    // Minimal PE image: .text (execute) at 0x1000, .data (write) at 0x2000..0x27FF, .rdata at 0x2800.
    private static byte[] Image()
    {
        var image = new byte[0x3000];
        image[0] = 0x4D; image[1] = 0x5A;
        WriteInt(image, 0x3C, 0x80);
        image[0x80] = 0x50; image[0x81] = 0x45;
        image[0x86] = 3;
        image[0x94] = 0xF0;
        int table = 0x80 + 24 + 0xF0;
        Section(image, table, CodeRva, 0x1000, 0x60000020);
        Section(image, table + 40, DataRva, 0x800, 0xC0000040);
        Section(image, table + 80, ReadOnlyRva, 0x800, 0x40000040);
        // mov eax,[rip+disp] -> DataRva + 0x10; nop; nop
        image[RipLoad] = 0x8B; image[RipLoad + 1] = 0x05;
        WriteInt(image, RipLoad + 2, DataRva + 0x10 - (RipLoad + 6));
        image[RipLoad + 6] = 0x90; image[RipLoad + 7] = 0x90;
        // mov eax,[rcx+0x5DC]
        byte[] field = { 0x8B, 0x81, 0xDC, 0x05, 0x00, 0x00 };
        Array.Copy(field, 0, image, FieldLoad, field.Length);
        // imul rdx,rax,0x583C
        byte[] stride = { 0x48, 0x69, 0xD0, 0x3C, 0x58, 0x00, 0x00 };
        Array.Copy(stride, 0, image, StrideMul, stride.Length);
        return image;
    }

    private static void Section(byte[] image, int header, int rva, int size, uint flags)
    {
        WriteInt(image, header + 8, size); WriteInt(image, header + 12, rva);
        WriteInt(image, header + 16, size); WriteInt(image, header + 36, unchecked((int)flags));
    }

    private static void WriteInt(byte[] image, int offset, int value) => BitConverter.GetBytes(value).CopyTo(image, offset);

    private static string NewDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "serp-native-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [TestMethod]
    public void OperandsDecodeRipTargetsDisplacementsAndImmediates()
    {
        byte[] image = Image();
        Assert.AreEqual(DataRva + 0x10, NativeInstructionOperands.RipDataTarget(image, RipLoad, writable: true));
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeInstructionOperands.RipDataTarget(image, RipLoad, writable: false));
        Assert.AreEqual(0x5DC, NativeInstructionOperands.MemoryDisplacement(image, FieldLoad, Register.RCX));
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeInstructionOperands.MemoryDisplacement(image, FieldLoad, Register.RDX));
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeInstructionOperands.MemoryDisplacement(image, RipLoad));
        Assert.AreEqual(0x583CL, NativeInstructionOperands.Immediate(image, StrideMul, 2));
        Assert.IsTrue(NativeInstructionOperands.InDataSection(image, ReadOnlyRva + 4, writable: false));
        Assert.IsFalse(NativeInstructionOperands.InDataSection(image, CodeRva + 4, writable: false));

        // A target in the code section is rejected even though it is inside the image.
        WriteInt(image, RipLoad + 2, CodeRva + 0x100 - (RipLoad + 6));
        Assert.ThrowsExactly<InvalidOperationException>(() => NativeInstructionOperands.RipDataTarget(image, RipLoad, writable: true));
    }

    [TestMethod]
    public void KnownReferenceHashNeitherScansNorWritesACache()
    {
        string directory = NewDirectory();
        try
        {
            byte[] image = Image();
            var cache = NativeResolutionCache.Open("owner", "1.0", "hash", referenceHashMatches: true, directory);
            NativeResolution resolved = cache.ResolveUnique(image, Pattern, RipLoad, "load");
            Assert.AreEqual("reference-rva", resolved.Method);
            Assert.IsFalse(cache.Save());
            Assert.AreEqual(0, Directory.GetFiles(directory).Length);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void UnknownHashScansOnceAndLaterStartsUseTheConfirmedCache()
    {
        string directory = NewDirectory();
        try
        {
            byte[] image = Image();
            var first = NativeResolutionCache.Open("owner", "1.0", "newhash", referenceHashMatches: false, directory);
            Assert.AreEqual("signature-fallback", first.ResolveUnique(image, Pattern, 0x1234, "load").Method);
            Assert.IsTrue(first.Save());

            var second = NativeResolutionCache.Open("owner", "1.0", "newhash", referenceHashMatches: false, directory);
            NativeResolution cached = second.ResolveUnique(image, Pattern, 0x1234, "load");
            Assert.AreEqual("cache", cached.Method);
            Assert.AreEqual(RipLoad, cached.Rva);
            Assert.IsFalse(second.Save());

            // Another native build or mod version ignores the old entries.
            Assert.AreEqual("signature-fallback", NativeResolutionCache.Open("owner", "1.1", "newhash", false, directory)
                .ResolveUnique(image, Pattern, 0x1234, "load").Method);
            Assert.AreEqual("signature-fallback", NativeResolutionCache.Open("owner", "1.0", "otherhash", false, directory)
                .ResolveUnique(image, Pattern, 0x1234, "load").Method);

            // A cached RVA whose bytes no longer match is never used.
            byte[] moved = Image();
            Array.Clear(moved, RipLoad, 8);
            Array.Copy(image, RipLoad, moved, RipLoad + 0x40, 8);
            NativeResolution rescanned = NativeResolutionCache.Open("owner", "1.0", "newhash", false, directory)
                .ResolveUnique(moved, Pattern, 0x1234, "load");
            Assert.AreEqual("signature-fallback", rescanned.Method);
            Assert.AreEqual(RipLoad + 0x40, rescanned.Rva);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void DamagedCacheFileOnlyCausesANewDerivation()
    {
        string directory = NewDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "owner.native-cache.tsv"), "not a cache\r\nload\tZZZ");
            int derivations = 0;
            var cache = NativeResolutionCache.Open("owner", "1.0", "newhash", referenceHashMatches: false, directory);
            NativeResolution value = cache.ResolveValue("offset", 0x10, v => v == 0x5DC, () => { derivations++; return 0x5DC; });
            Assert.AreEqual("derived", value.Method);
            Assert.AreEqual(1, derivations);
            Assert.IsTrue(cache.Save());
            var reopened = NativeResolutionCache.Open("owner", "1.0", "newhash", referenceHashMatches: false, directory);
            Assert.AreEqual("cache", reopened.ResolveValue("offset", 0x10, v => v == 0x5DC, () => { derivations++; return 0x5DC; }).Method);
            Assert.AreEqual(1, derivations);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void DerivedValueMustPassItsConfirmation()
    {
        var cache = NativeResolutionCache.Open("owner", "1.0", "hash", referenceHashMatches: true, Path.GetTempPath());
        Assert.AreEqual("reference-value", cache.ResolveValue("ok", 4, v => v == 4, () => throw new InvalidOperationException()).Method);
        Assert.ThrowsExactly<InvalidOperationException>(() => cache.ResolveValue("bad", 4, v => false, () => 8));
    }
}

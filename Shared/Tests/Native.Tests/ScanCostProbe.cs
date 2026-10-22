using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shared;
using System;
using System.Diagnostics;
using System.IO;

[TestClass]
public sealed class ScanCostProbe
{
    // Measurement only (prints timings); maps the installed CrusaderDE.dll like the loader.
    [TestMethod]
    public void MeasureUniquePatternScan()
    {
        string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        if (!File.Exists(dll)) { Assert.Inconclusive("Game DLL unavailable."); return; }
        byte[] raw = File.ReadAllBytes(dll);
        int pe = BitConverter.ToInt32(raw, 0x3C);
        int count = BitConverter.ToUInt16(raw, pe + 6), optional = BitConverter.ToUInt16(raw, pe + 20);
        int sizeOfImage = BitConverter.ToInt32(raw, pe + 24 + 56), headers = BitConverter.ToInt32(raw, pe + 24 + 60);
        var image = new byte[sizeOfImage];
        Array.Copy(raw, image, headers);
        for (int i = 0; i < count; i++)
        {
            int h = pe + 24 + optional + i * 40;
            int va = BitConverter.ToInt32(raw, h + 12), size = BitConverter.ToInt32(raw, h + 16), ptr = BitConverter.ToInt32(raw, h + 20);
            Array.Copy(raw, ptr, image, va, Math.Min(size, image.Length - va));
        }
        const string pattern = "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 FF 15 ? ? ? ? 48 8D 0D ? ? ? ? 8B F8 E8 ? ? ? ? 85 C0 74 77";
        NativePatternResolver.FindUniquePattern(image, pattern, "warmup");
        var watch = Stopwatch.StartNew();
        for (int n = 0; n < 10; n++) NativePatternResolver.FindUniquePattern(image, pattern, "probe");
        watch.Stop();
        Console.WriteLine("FindUniquePattern average: {0:F2} ms (image {1} MB)", watch.Elapsed.TotalMilliseconds / 10, image.Length / 1048576);
        watch.Restart();
        for (int n = 0; n < 10; n++) NativePatternResolver.MatchesPatternAt(image, 0xFE570, pattern);
        watch.Stop();
        Console.WriteLine("MatchesPatternAt average: {0:F4} ms", watch.Elapsed.TotalMilliseconds / 10);
        watch.Restart();
        using (var sha = System.Security.Cryptography.SHA256.Create()) sha.ComputeHash(raw);
        Console.WriteLine("SHA-256 of DLL: {0:F2} ms", watch.Elapsed.TotalMilliseconds);
    }
}

using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Iced.Intel;

namespace GameMemReader;

/// <summary>
/// One native global, resolved with the same AOB pattern the Script Extender uses.
/// Kind "rip": RIP-relative operand, Rva is the module-relative target (exact).
/// Kind "disp": plain displacement of a [reg+disp] operand. The Extender adds the module
/// base for most of these (table RVAs), but some are offsets relative to another register.
/// </summary>
internal sealed record ResolvedGlobal(string Name, string Kind, ulong Rva, int MatchCount, int SourceLine);

/// <summary>
/// Resolves the Extender's native globals without running the Extender: the AOB patterns are
/// parsed from the local Extender source (GameGlobalsManager.cs), scanned in the unpatched DLL
/// file on disk and decoded with Iced. Results are cached per native SHA-256.
/// </summary>
internal static partial class GlobalResolver
{
    // .Scan("PATTERN") followed by .TryReadDisplacement(out X[, operandIndex: N]) or .TryReadRelativeAddress(out X)
    [GeneratedRegex("""\.Scan\("(?<pat>[^"\\]*(?:\\.[^"\\]*)*)"\)\s*\.TryRead(?:Displacement|RelativeAddress)\(\s*out\s+(?<name>\w+)""", RegexOptions.Singleline)]
    private static partial Regex ScanChain();

    public static List<ResolvedGlobal> Resolve(string dllPath, string extenderGlobalsSource, string cacheDir, out string nativeHash)
    {
        var dllBytes = File.ReadAllBytes(dllPath);
        nativeHash = Convert.ToHexString(SHA256.HashData(dllBytes));
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extenderGlobalsSource)))[..16];
        var cacheFile = Path.Combine(cacheDir, $"globals-{nativeHash[..16]}-{sourceHash}.json");
        if (File.Exists(cacheFile))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<List<ResolvedGlobal>>(File.ReadAllText(cacheFile));
                if (cached is { Count: > 0 })
                    return cached;
            }
            catch
            {
                // A broken cache only causes a rescan.
            }
        }

        var image = BuildImage(dllBytes, out var execRanges);
        var source = File.ReadAllText(extenderGlobalsSource);
        var results = new List<ResolvedGlobal>();
        foreach (Match m in ScanChain().Matches(source))
        {
            var name = m.Groups["name"].Value;
            var line = source.AsSpan(0, m.Index).Count('\n') + 1;
            var pattern = ParsePattern(m.Groups["pat"].Value);
            if (pattern.Length == 0)
                continue;

            int count = 0;
            int first = -1;
            foreach (var (start, end) in execRanges)
            {
                for (int i = FindNext(image, pattern, start, end); i >= 0; i = FindNext(image, pattern, i + 1, end))
                {
                    if (first < 0) first = i;
                    count++;
                }
            }
            if (first < 0)
            {
                results.Add(new ResolvedGlobal(name, "notfound", 0, 0, line));
                continue;
            }

            var reader = new ByteArrayCodeReader(image, first, Math.Min(15, image.Length - first));
            var decoder = Iced.Intel.Decoder.Create(64, reader, (ulong)first);
            var instr = decoder.Decode();
            if (instr.IsInvalid || !HasMemoryOperand(instr))
            {
                results.Add(new ResolvedGlobal(name, "undecodable", 0, count, line));
                continue;
            }
            results.Add(instr.IsIPRelativeMemoryOperand
                ? new ResolvedGlobal(name, "rip", instr.IPRelativeMemoryAddress, count, line)
                : new ResolvedGlobal(name, "disp", instr.MemoryDisplacement64, count, line));
        }

        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(cacheFile, JsonSerializer.Serialize(results));
        return results;
    }

    private static bool HasMemoryOperand(in Instruction instr)
    {
        for (int i = 0; i < instr.OpCount; i++)
            if (instr.GetOpKind(i) == OpKind.Memory)
                return true;
        return false;
    }

    /// <summary>Maps the PE file into an RVA-addressed image (unpatched bytes, no relocations needed for RIP math).</summary>
    private static byte[] BuildImage(byte[] dll, out List<(int Start, int End)> execRanges)
    {
        using var pe = new PEReader(new MemoryStream(dll));
        var headers = pe.PEHeaders;
        var image = new byte[headers.PEHeader!.SizeOfImage];
        execRanges = new List<(int, int)>();
        foreach (var s in headers.SectionHeaders)
        {
            var len = Math.Min(s.SizeOfRawData, s.VirtualSize == 0 ? s.SizeOfRawData : s.VirtualSize);
            Buffer.BlockCopy(dll, s.PointerToRawData, image, s.VirtualAddress, Math.Min(len, dll.Length - s.PointerToRawData));
            if ((s.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0)
                execRanges.Add((s.VirtualAddress, s.VirtualAddress + len));
        }
        return image;
    }

    private static short[] ParsePattern(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var list = new List<short>();
        foreach (var t in tokens)
        {
            if (t.StartsWith('?'))
                list.Add(-1);
            else if (t.Length == 2 && byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var b))
                list.Add(b);
            // Anything else (e.g. a stray escaped quote in the source) is ignored, matching a tolerant scanner.
        }
        return list.ToArray();
    }

    private static int FindNext(byte[] image, short[] pattern, int from, int end)
    {
        int anchor = Array.FindIndex(pattern, p => p >= 0);
        if (anchor < 0) return -1;
        byte anchorByte = (byte)pattern[anchor];
        int last = end - pattern.Length;
        for (int i = from; i <= last; i++)
        {
            int idx = Array.IndexOf(image, anchorByte, i + anchor, last - i + 1);
            if (idx < 0) return -1;
            i = idx - anchor;
            bool ok = true;
            for (int k = 0; k < pattern.Length; k++)
            {
                var p = pattern[k];
                if (p >= 0 && image[i + k] != p) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }
}

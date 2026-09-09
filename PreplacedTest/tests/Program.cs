using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PreplacedTest.Tests
{
    internal static class Program
    {
        private static int checks;

        private static int Main()
        {
            try
            {
                TestCountersHaveNoCap();
                TestChunkingIsLossless();
                TestSchedulerModels();
                TestFirstBuildingWindow();
                TestStaticNativeContracts();
                TestNativeSignaturesAgainstCanonicalDll();
                Console.WriteLine($"PreplacedTest tests passed: {checks} checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void TestCountersHaveNoCap()
        {
            DiagnosticCounterSet counters = new DiagnosticCounterSet();
            for (int index = 0; index < 250000; index++) counters.Add("attempt");
            Check(counters.TotalFor("attempt") == 250000, "counter capped events");
            Check(counters.DrainInterval().Single().Value == 250000, "interval sum differs");
            Check(counters.SnapshotTotal().Single().Value == 250000, "total lost on drain");
            counters.Stop(); counters.Add("attempt");
            Check(counters.TotalFor("attempt") == 250000, "terminal observation window kept accumulating");
            counters.Clear();
            Check(counters.SnapshotTotal().Length == 0, "session reset retained totals");
        }

        private static void TestChunkingIsLossless()
        {
            string input = string.Concat(Enumerable.Range(0, 5000).Select(i => (char)('A' + i % 26)));
            string[] chunks = DiagnosticChunker.Split(input, 137);
            Check(chunks.Length > 1, "long group was not split");
            Check(string.Concat(chunks) == input, "split output was truncated or reordered");
            Check(chunks.All(c => c.Length <= 137), "chunk exceeded limit");
        }

        private static void TestSchedulerModels()
        {
            Check(Classify(0, 0, 0, 10000, 0, 5, 0, 1, 2) == "inactive-aiv-slot", "inactive AIV model");
            Check(Classify(1, 1, 10, 10000, 9, 5, 0, 1, 2) == "crushed-building-delay", "crushed delay model");
            Check(Classify(1, 0, 10, 1000, 0, 5, 0, 1, 2) == "build-rate", "build rate model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 3, 1, 2) == "aiv-pause-countdown", "pause model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 0, 1, 0) == "no-prepared-frames", "prepared frame model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 0, 0, 2) == "step-goal-not-released", "step goal model");
            Check(Classify(1, 0, 10, 10000, 5, 5, 0, 1, 2) == "scheduler-work-eligible", "eligible model");
        }

        private static string Classify(int a, int c, int cd, int g, int bc, int br, int p, int goal, int high) =>
            SchedulerGateClassifier.ClassifyBeforeCall(new SchedulerGateState(a, c, cd, g, bc, br, p, goal, high));

        private static void TestFirstBuildingWindow()
        {
            DateTime start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            FirstBuildingWindow window = new FirstBuildingWindow(TimeSpan.FromSeconds(10));
            Check(!window.TryConfirm(start, true, false, false), "nonbuilding command confirmed");
            Check(!window.TryConfirm(start, true, false, true), "wall/moat-style frame transition confirmed");
            Check(!window.TryConfirm(start, false, true, true), "failed execute confirmed");
            Check(window.TryConfirm(start, true, true, false), "spawn success not confirmed");
            Check(!window.FollowUpComplete(start.AddMilliseconds(9999)), "follow-up ended early");
            Check(window.FollowUpComplete(start.AddSeconds(10)), "follow-up did not end");
        }

        private static void TestStaticNativeContracts()
        {
            string source = File.ReadAllText(Path.Combine("src", "PreplacedTestRuntime.cs"));
            string assemblyInfo = File.ReadAllText(Path.Combine("src", "AssemblyInfo.cs"));
            string plugin = File.ReadAllText(Path.Combine("src", "PreplacedTestPlugin.cs"));
            string manifest = File.ReadAllText("info.json");
            string helper = File.ReadAllText(Path.Combine("..", "Shared", "DebugLogHelper.cs"));
            Check(helper.Contains("FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"), "native hash contract missing");
            foreach (string rva in new[] { "0x50680", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060" })
                Check(source.Contains(rva), "RVA missing: " + rva);
            foreach (string contract in new[] { "AivSpecStride = 0x6D98", "PlayerRuntimeStateStride = 0x583C", "PreparedLayoutFrameCount = 0x922", "PreparedEntrySize = 0x0C", "UnmanagedFunctionPointer(CallingConvention.Cdecl)" })
                Check(source.Contains(contract), "native ABI/offset contract missing: " + contract);
            Check(source.Contains("players.Clear()"), "map transition does not reset sessions");
            Check(!source.Contains("MaximumCapture") && !source.Contains("Take(100"), "fixed event cap found");
            Check(assemblyInfo.Contains("AssemblyVersion(\"0.1.0.0\")") &&
                assemblyInfo.Contains("AssemblyFileVersion(\"0.1.0.0\")") &&
                assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.0\")") &&
                plugin.Contains("PluginVersion = \"0.1.0\"") && manifest.Contains("\"Version\": \"0.1.0\""),
                "active version declarations are inconsistent");
        }

        private static void TestNativeSignaturesAgainstCanonicalDll()
        {
            const string expectedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
            string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
            byte[] file = File.ReadAllBytes(dll);
            using (SHA256 sha = SHA256.Create())
                Check(BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "") == expectedHash, "canonical native hash changed");

            byte[] image = file;
            string source = File.ReadAllText(Path.Combine("src", "PreplacedTestRuntime.cs"));
            MatchCollection definitions = Regex.Matches(source,
                @"private const string (?<name>\w+Pattern)\s*=\s*(?<body>.*?);", RegexOptions.Singleline);
            Check(definitions.Count >= 22, "not all native signatures were discovered by the static test");
            foreach (Match definition in definitions)
            {
                string name = definition.Groups["name"].Value;
                string stem = name.Substring(0, name.Length - "Pattern".Length);
                string pattern = string.Concat(Regex.Matches(definition.Groups["body"].Value, "\"(?<s>[^\"]*)\"")
                    .Cast<Match>().Select(m => m.Groups["s"].Value));
                Match rvaMatch = Regex.Match(source, @"private const int " + Regex.Escape(stem) + @"Rva\s*=\s*0x(?<rva>[0-9A-Fa-f]+)");
                Check(rvaMatch.Success, "reference RVA missing for " + name);
                int rva = Convert.ToInt32(rvaMatch.Groups["rva"].Value, 16);
                PatternByte[] parsed = ParsePattern(pattern);
                int rawOffset = RvaToRaw(file, rva);
                Check(Matches(image, rawOffset, parsed), name + " does not match its reference RVA");
                int matches = 0;
                for (int offset = 0; offset <= image.Length - parsed.Length; offset++)
                    if (Matches(image, offset, parsed)) matches++;
                Check(matches == 1, name + " is not unique: " + matches);
            }

            string functions = File.ReadAllText(Path.Combine("..", "_inspect", "CrusaderDE-Native-Baseline", "sem", "FBCB9319", "exports", "semantic-functions.jsonl"));
            foreach (string rva in new[] { "0x50680", "0x54EC0", "0x54F60", "0x54DE0", "0x55320", "0x56670", "0x57080", "0x53D00", "0x539B0", "0x51790", "0x52270", "0x5CD90", "0x7B060", "0xCC420", "0x414A0", "0x41230", "0x41380", "0x41280", "0x3B1D0", "0x50340", "0x504F0" })
                Check(functions.Contains("\"rva\":\"" + rva + "\""), "baseline function boundary missing: " + rva);
        }

        private static int RvaToRaw(byte[] file, int rva)
        {
            int pe = BitConverter.ToInt32(file, 0x3C);
            int sections = BitConverter.ToUInt16(file, pe + 6);
            int optionalSize = BitConverter.ToUInt16(file, pe + 20);
            int optional = pe + 24;
            int sectionTable = optional + optionalSize;
            for (int index = 0; index < sections; index++)
            {
                int header = sectionTable + index * 40;
                int virtualAddress = BitConverter.ToInt32(file, header + 12);
                int virtualSize = BitConverter.ToInt32(file, header + 8);
                int rawSize = BitConverter.ToInt32(file, header + 16);
                int rawOffset = BitConverter.ToInt32(file, header + 20);
                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                    return rawOffset + rva - virtualAddress;
            }
            throw new InvalidOperationException("RVA outside PE sections: 0x" + rva.ToString("X"));
        }

        private static PatternByte[] ParsePattern(string pattern) => pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token == "??" ? new PatternByte(0, true) : new PatternByte(Convert.ToByte(token, 16), false)).ToArray();

        private static bool Matches(byte[] image, int offset, PatternByte[] pattern)
        {
            if (offset < 0 || offset > image.Length - pattern.Length) return false;
            for (int index = 0; index < pattern.Length; index++)
                if (!pattern[index].Wildcard && image[offset + index] != pattern[index].Value) return false;
            return true;
        }

        private readonly struct PatternByte
        {
            public PatternByte(byte value, bool wildcard) { Value = value; Wildcard = wildcard; }
            public byte Value { get; }
            public bool Wildcard { get; }
        }

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("Check failed: " + message);
        }
    }
}

using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIAttackTest
{
    internal static class Program
    {
        private const string NativeDllPath =
            @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private static int assertions;

        private static int Main()
        {
            TestPolicy();
            TestManagedLayout();
            TestNativeContract();
            TestSourceAndPackageContracts();
            Console.WriteLine($"AIAttackTest tests: {assertions} assertions passed.");
            return 0;
        }

        private static void TestPolicy()
        {
            Check(AIAttackPolicy.CalculateNormalWaveMultiplier(10, 50) == 5,
                "10 trigger at 50 percent");
            Check(AIAttackPolicy.CalculateNormalWaveMultiplier(50, 50) == 25,
                "50 trigger at 50 percent");
            Check(AIAttackPolicy.CalculateNormalWaveMultiplier(9, 50) == 5,
                "normal multiplier uses half-up rounding");
            Check(AIAttackPolicy.CalculateHighGoldWaveMultiplier(5) == 7,
                "high-gold 7:5 relation");
            Check(AIAttackPolicy.CalculateHighGoldWaveMultiplier(25) == 35,
                "high-gold 7:5 relation for larger lord");
            Check(AIAttackPolicy.CalculateNormalWaveMultiplier(100, 999) == 300,
                "growth percent is clamped");
            Check(AIAttackPolicy.CalculateInitialDefenseTicks(-1) == 0,
                "negative defense months are clamped");
            Check(AIAttackPolicy.CalculateInitialDefenseTicks(6) == 4800,
                "Vanilla defense months");
            Check(AIAttackPolicy.CalculateInitialDefenseTicks(99) == 24000,
                "defense months are clamped to 30");

            int[] unique = AIAttackPolicy.ResolveUniqueAicIndices(
                new[] { 4, 2, 4, 0, -1, 9, 1 },
                9);
            Check(unique.SequenceEqual(new[] { 1, 2, 4 }),
                "active AIC indices are valid, sorted, and deduplicated");

            InternalAIC lord = new InternalAIC
            {
                siege_trigger_level = 50,
                siege_max_troops = 300,
                siege_normal_wave_multiplier = 4,
                siege_high_gold_wave_multiplier = 12,
            };
            InternalAIC updated = lord;
            updated.siege_normal_wave_multiplier =
                AIAttackPolicy.CalculateNormalWaveMultiplier(lord.siege_trigger_level, 50);
            updated.siege_high_gold_wave_multiplier =
                AIAttackPolicy.CalculateHighGoldWaveMultiplier(updated.siege_normal_wave_multiplier);
            Check(updated.siege_max_troops == 300,
                "lord-specific siege_max_troops remains unchanged");
            Check(updated.siege_trigger_level == 50,
                "lord-specific trigger remains unchanged");
        }

        private static void TestManagedLayout()
        {
            Check(Marshal.SizeOf(typeof(InternalAIC)) == 0x5E4, "InternalAIC size");
            Check(Offset<InternalAIC>(nameof(InternalAIC.siege_trigger_level)) == 0x1F8,
                "siege_trigger_level offset");
            Check(Offset<InternalAIC>(nameof(InternalAIC.siege_max_troops)) == 0x454,
                "siege_max_troops offset");
            Check(Offset<InternalAIC>(nameof(InternalAIC.siege_normal_wave_multiplier)) == 0x458,
                "normal wave multiplier offset");
            Check(Offset<InternalAIC>(nameof(InternalAIC.siege_high_gold_wave_multiplier)) == 0x45C,
                "high-gold wave multiplier offset");
            Check(Offset<GamePlayerResources>(nameof(GamePlayerResources.r_AITargetAttackForceSize)) == 0x38A0,
                "attack-force diagnostic offset");
        }

        private static void TestNativeContract()
        {
            AIAttackNativeContract.ValidateLayout();
            Check(File.Exists(NativeDllPath), "canonical native DLL exists");
            byte[] file = File.ReadAllBytes(NativeDllPath);
            Check(Hash(file) == AIAttackNativeContract.ReferenceSha256,
                "canonical native DLL hash");
            PeImage image = PeImage.Load(file);

            List<int> recruitMatches = image.FindExecutableMatches(
                ParsePattern(AIAttackNativeContract.RecruitContextPattern));
            Check(recruitMatches.Count == 1, "recruitment context is unique");
            Check(recruitMatches[0] == AIAttackNativeContract.RecruitContextRva,
                "recruitment context RVA");
            Check(image.ReadBytes(
                    AIAttackNativeContract.RecruitImmediateRva,
                    AIAttackNativeContract.VanillaRecruitTicks.Length)
                .SequenceEqual(AIAttackNativeContract.VanillaRecruitTicks),
                "recruitment immediate bytes");
            Check(image.ReadByte(AIAttackNativeContract.RecruitImmediateRva - 6) == 0x81 &&
                  image.ReadByte(AIAttackNativeContract.RecruitImmediateRva - 5) == 0x3D &&
                  image.ReadByte(AIAttackNativeContract.RecruitImmediateRva + 4) == 0x7C,
                "recruitment patch is the immediate of cmp dword [rip+disp32], imm32 followed by jl");

            List<int> lordMatches = image.FindExecutableMatches(
                ParsePattern(AIAttackNativeContract.LordContextPattern));
            Check(lordMatches.Count == 1, "lord limiter context is unique");
            Check(lordMatches[0] == AIAttackNativeContract.LordContextRva,
                "lord limiter context RVA");
            Check(image.ReadBytes(
                    AIAttackNativeContract.LordBranchRva,
                    AIAttackNativeContract.VanillaLordBranch.Length)
                .SequenceEqual(AIAttackNativeContract.VanillaLordBranch),
                "lord limiter branch bytes");
            Check(image.ReadByte(AIAttackNativeContract.LordBranchRva - 2) == 0x85 &&
                  image.ReadByte(AIAttackNativeContract.LordBranchRva - 1) == 0xED,
                "lord patch follows test ebp, ebp on an instruction boundary");
            Check(AIAttackNativeContract.LordBranchRva + 2 <
                  AIAttackNativeContract.AiWallTargetingFixRva,
                "lord patch is disjoint from AiWallTargetingFix");
        }

        private static void TestSourceAndPackageContracts()
        {
            string root = Directory.GetCurrentDirectory();
            string plugin = Read(root, "src", "AIAttackTestPlugin.cs");
            string settings = Read(root, "src", "AIAttackTestSettings.cs");
            string runtime = Read(root, "src", "AIAttackTestRuntime.cs");
            string nativeOverrides = Read(root, "src", "AIAttackPermanentNativeOverrides.cs");
            string project = Read(root, "AIAttackTest.csproj");
            string manifest = Read(root, "info.json");
            string assemblyInfo = Read(root, "Properties", "AssemblyInfo.cs");
            string future = Read(root, "FUTURE_WORK.md");
            string build = Read(root, "build.bat");
            string combinedRuntime = plugin + settings + runtime + nativeOverrides + project;

            Check(Count(settings, "[SyncHostOnly]") == 5,
                "all five gameplay settings are host synchronized");
            Check(plugin.Contains("BepInDependency(\"BugfixesAndQoL_Serp\"") &&
                  plugin.Contains("SoftDependency"),
                "BugfixesAndQoL is a soft dependency");
            Check(manifest.Contains("\"NetworkMode\": 1") &&
                  manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.6.0\""),
                "synchronized manifest contract");
            Check(plugin.Contains("PluginVersion = \"0.1.0\"") &&
                  manifest.Contains("\"Version\": \"0.1.0\"") &&
                  assemblyInfo.Contains("AssemblyVersion(\"0.1.0.0\")") &&
                  assemblyInfo.Contains("AssemblyInformationalVersion(\"0.1.0\")"),
                "active version metadata is consistent");
            Check(runtime.Contains("SetAICFromBytes") &&
                  runtime.Contains("new ReadOnlySpan<byte>(&value, sizeof(InternalAIC))") &&
                  runtime.Contains("lordCap={current.siege_max_troops} (preserved)"),
                "full AIC writes preserve the lord-specific cap");
            Check(runtime.Contains("Only unchanged owned fields will be restored") &&
                  runtime.Contains("nativeOverrides.RestoreVanilla()") &&
                  nativeOverrides.Contains("RecruitDisplacedBytes = 15") &&
                  nativeOverrides.Contains("LordDisplacedBytes = 14") &&
                  nativeOverrides.Contains("MarkPublished()") &&
                  nativeOverrides.Contains("RollbackUnpublished()") &&
                  !nativeOverrides.Contains("CodePatch.Write("),
                "cooperative AIC restoration and permanent native logical restoration are present");
            Check(runtime.Contains("GamePlayerManagerAPI.Instance.GetAILord(playerId)") &&
                  runtime.Contains("ResolveUniqueAicIndices"),
                "active AI AIC resolution and deduplication are present");
            Check(runtime.Contains("ChangesLogged >= 4") &&
                  runtime.Contains("r_AITargetAttackForceSize"),
                "first-four attack-force diagnostics are bounded");
            int retainedTargetIndex = runtime.IndexOf(
                "diagnostics[playerId] = diagnostic;", StringComparison.Ordinal);
            int zeroTargetIndex = runtime.IndexOf("if (target == 0)", StringComparison.Ordinal);
            Check(retainedTargetIndex >= 0 && zeroTargetIndex >= 0 &&
                  retainedTargetIndex < zeroTargetIndex,
                "zero target resets are retained before the next attack-force sample");
            Check(future.Contains("vier Angriffsgruppen") && future.Contains("4/2/1") &&
                  future.Contains("AiWallTargetingFix"),
                "future UCP target rotation is documented and separated");
            Check(build.Contains("System\\.Text\\.Json") &&
                  build.Contains("OnDestroy|OnDisable|OnApplicationQuit"),
                "build-time JSON and lifecycle preflight exists");
            Check(!ContainsForbiddenJson(combinedRuntime),
                "runtime project has no forbidden JSON serializer dependency");
            Check(!combinedRuntime.Contains("OnDestroy(") &&
                  !combinedRuntime.Contains("OnDisable(") &&
                  !combinedRuntime.Contains("OnApplicationQuit("),
                "runtime has no Unity teardown lifecycle method");
        }

        private static bool ContainsForbiddenJson(string source) =>
            new[]
            {
                "System.Text.Json",
                "Newtonsoft.Json",
                "JavaScriptSerializer",
                "System.Web.Extensions",
                "DataContractJsonSerializer",
                "JsonUtility",
            }.Any(source.Contains);

        private static int Offset<T>(string field) =>
            Marshal.OffsetOf(typeof(T), field).ToInt32();

        private static string Read(string root, params string[] parts) =>
            File.ReadAllText(parts.Aggregate(root, (path, part) => Path.Combine(path, part)));

        private static int Count(string source, string value)
        {
            int count = 0;
            for (int index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0;
                 index += value.Length)
            {
                count++;
            }
            return count;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static PatternToken[] ParsePattern(string pattern) =>
            pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token == "??"
                    ? new PatternToken(0, true)
                    : new PatternToken(Convert.ToByte(token, 16), false))
                .ToArray();

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct PatternToken
        {
            internal PatternToken(byte value, bool wildcard)
            {
                Value = value;
                Wildcard = wildcard;
            }
            internal byte Value { get; }
            internal bool Wildcard { get; }
        }

        private sealed class PeImage
        {
            private readonly byte[] image;
            private readonly List<Section> executableSections;

            private PeImage(byte[] image, List<Section> executableSections)
            {
                this.image = image;
                this.executableSections = executableSections;
            }

            internal static PeImage Load(byte[] file)
            {
                int peOffset = BitConverter.ToInt32(file, 0x3C);
                ushort sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
                ushort optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
                int optionalHeader = peOffset + 24;
                byte[] image = new byte[BitConverter.ToInt32(file, optionalHeader + 56)];
                int headerSize = BitConverter.ToInt32(file, optionalHeader + 60);
                Buffer.BlockCopy(file, 0, image, 0, Math.Min(headerSize, file.Length));
                int sectionTable = optionalHeader + optionalHeaderSize;
                var executable = new List<Section>();
                for (int index = 0; index < sectionCount; index++)
                {
                    int entry = sectionTable + index * 40;
                    int virtualSize = BitConverter.ToInt32(file, entry + 8);
                    int virtualAddress = BitConverter.ToInt32(file, entry + 12);
                    int rawSize = BitConverter.ToInt32(file, entry + 16);
                    int rawOffset = BitConverter.ToInt32(file, entry + 20);
                    int copyLength = Math.Min(rawSize,
                        Math.Min(file.Length - rawOffset, image.Length - virtualAddress));
                    if (copyLength > 0)
                        Buffer.BlockCopy(file, rawOffset, image, virtualAddress, copyLength);
                    if ((BitConverter.ToUInt32(file, entry + 36) & 0x20000000u) != 0)
                        executable.Add(new Section(virtualAddress, Math.Max(virtualSize, rawSize)));
                }
                return new PeImage(image, executable);
            }

            internal byte ReadByte(int rva) => image[rva];

            internal byte[] ReadBytes(int rva, int length)
            {
                byte[] result = new byte[length];
                Buffer.BlockCopy(image, rva, result, 0, length);
                return result;
            }

            internal List<int> FindExecutableMatches(PatternToken[] pattern)
            {
                var matches = new List<int>();
                foreach (Section section in executableSections)
                {
                    int end = Math.Min(image.Length, section.Start + section.Length) - pattern.Length;
                    for (int offset = section.Start; offset <= end; offset++)
                    {
                        bool match = true;
                        for (int index = 0; index < pattern.Length; index++)
                        {
                            if (!pattern[index].Wildcard && image[offset + index] != pattern[index].Value)
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                            matches.Add(offset);
                    }
                }
                return matches;
            }

            private readonly struct Section
            {
                internal Section(int start, int length)
                {
                    Start = start;
                    Length = length;
                }
                internal int Start { get; }
                internal int Length { get; }
            }
        }
    }
}

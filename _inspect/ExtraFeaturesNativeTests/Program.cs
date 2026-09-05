using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Shared
{
    internal static class DebugLogHelper
    {
        public static void LogInfo(ManualLogSource log, string message) { }
    }
}

namespace ExtraFeatures
{
    internal static class Program
    {
        private const string DllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        private const long Base = 0x180000000;
        private static int assertions;

        private static int Main()
        {
            try
            {
                byte[] file = File.ReadAllBytes(DllPath);
                Check(Hash(file) == GatehouseTimingPatch.SupportedBuildHash, "canonical DLL hash");
                byte[] image = MapPeImage(file);
                TestP7aRedBirdMigration(FindWorkspace());
                TestNativeTargetMap(image);
                TestCatalogAndApply(image);
                TestValidation(image);
                TestAdjacentHookCompatibility(image);
                TestRollbackAndCleanup(image);
                Console.WriteLine($"PASS: ExtraFeatures native tests ({assertions} assertions).");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static void TestP7aRedBirdMigration(string workspace)
        {
            string sourceDirectory = Path.Combine(workspace, "ExtraFeatures", "src");
            string[] sourcePaths = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.TopDirectoryOnly);
            string production = string.Join("\n", sourcePaths.Select(File.ReadAllText));
            string project = File.ReadAllText(Path.Combine(workspace, "ExtraFeatures", "ExtraFeatures.csproj"));
            string plugin = File.ReadAllText(Path.Combine(sourceDirectory, "ExtraFeaturesPlugin.cs"));
            string runtime = File.ReadAllText(Path.Combine(sourceDirectory, "ExtraFeaturesRuntime.cs"));
            string plague = File.ReadAllText(Path.Combine(sourceDirectory, "PlagueDurationPatch.cs"));
            string monk = File.ReadAllText(Path.Combine(sourceDirectory, "MonkAlwaysRunPatch.cs"));
            string gatehouseTiming = File.ReadAllText(Path.Combine(sourceDirectory, "GatehouseTimingPatch.cs"));

            Check(!production.Contains("Zhuqiaomon") && !project.Contains("Zhuqiaomon"),
                "P7a removed Zhuqiaomon source and project references");
            Check(!production.Contains("HookRef<") && !production.Contains(".Unload()") &&
                  !production.Contains("Value.Hook.Trampoline"),
                "P7a removed obsolete handles, Unload calls, and trampolines");
            Check(project.Contains("<Reference Include=\"RedBird.Abstractions\"") &&
                  project.Contains("<Reference Include=\"RedBird.Core\"") &&
                  project.Contains("<Reference Include=\"RedBird.X64\"") &&
                  !project.Contains("PolyHook2.NET"),
                "P7a project references the Script Extender RedBird assemblies without PolyHook2.NET");
            Check(plugin.Contains("[BepInDependency(ScriptExtenderGuid, \"2.0.2\")]") &&
                  plugin.Contains("OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)"),
                "P7a declares an exact 2.0.2 dependency and consumes the load context");
            Check(runtime.Contains("context.ModuleHandle") && runtime.Contains("context.Memory") &&
                  runtime.Contains("context.Region") && !production.Contains("nativeRegion.Dispose()") &&
                  !production.Contains("context.Region.Dispose()"),
                "P7a borrows all native load-context values without disposing the ScanRegion");
            Check(Regex.Matches(production, @"new\s+(?:DetourHandle|HookHandle)<").Count == 8,
                "P7a owns the audited eight RedBird hook handles");
            Check(Regex.Matches(production, @"CommitResult\s+commitResult\s*=\s*[^;]+\.Commit\(\)").Count == 6 &&
                  Regex.Matches(production, @"!commitResult\.IsCompleteSuccess").Count == 6,
                "P7a checks all six aggregate transaction results");
            Check(plague.Contains("CodePatch.Write(") && !plague.Contains("VirtualProtect") &&
                  !plague.Contains("FlushInstructionCache") &&
                  plague.IndexOf("expectedLifetime = desiredLifetime", StringComparison.Ordinal) >
                  plague.IndexOf("verifiedLifetime != desiredLifetime", StringComparison.Ordinal),
                "P7a plague immediate uses verified RedBird CodePatch ownership");
            Check(monk.Contains("using RedBird.X64.Extensions;") &&
                  monk.Contains("assembler.AddUnrestrictedJmp(") &&
                  monk.Contains("hookSize: HookSize"),
                "P7a Monk generator retains the audited unrestricted jumps and hook boundary");
            Check(gatehouseTiming.Contains("VirtualProtect") && gatehouseTiming.Contains("FlushInstructionCache"),
                "P7a leaves the separately owned Gatehouse timing writer unchanged");
        }

        private static string FindWorkspace()
        {
            DirectoryInfo directory = new DirectoryInfo(Environment.CurrentDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "UpdatePlan-SHCDESE-2.0.2.md")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Workspace root was not found.");
        }

        private static void TestNativeTargetMap(byte[] image)
        {
            CheckFunction(image, 0x504F0, 304, "2D6CB2745E0E6619C9D40DDD4F07C70CFF494FE8DECCC6175658C395EBD00393", "AI flag disease handler");
            CheckFunction(image, 0x51790, 2774, "69731F77776995C9FC452A7A9A41408385B757B461F0E7FAB76E291BE64C3ECF", "AIV build-step handler");
            CheckFunction(image, 0x5CD90, 1077, "099D5E8B4AB0B93EB2BE39501D06AE0FC38F481035AF50650654F6F233B23A17", "AIV placement handler");
            CheckFunction(image, 0x9A080, 410, "902372F40007B9FBE5F14FB7C48366F4090A261E2DE21463698B15FFDC7F704B", "Disease update handler");
            CheckFunction(image, 0x9F700, 525, "D4C059E5AED1B7FFCFA334E0A361EDA4DC7B49EF1FBAE9F8972E231FC4A0BC6A", "apothecary Disease search handler");
            CheckFunction(image, 0xCEB10, 31, "5B45784D8B227D4BEB1AA822E6B12523BD9A0825EFA17764909A037E613C6C6A", "AI buy-price helper");
            CheckFunction(image, 0xCEB90, 31, "D428FAE5C2A3BED0B48195B2661F56550E5B53F6E8EE9A603FADA56DAEE8F670", "AI sell-price helper");
            CheckFunction(image, 0x151090, 3969, "785E5FB37D378726A55C84609FFD307CDC81865B964BB631EB98A3EBE5B1CB58", "Monk handler");

            CheckPattern(image, 0x51790, "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2", "AIV build-step ABI/prologue");
            CheckPattern(image, 0x5CD90, "44 89 4C 24 20 44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 48", "AIV placement stack arguments/prologue");
            CheckPattern(image, 0xCEB10, "49 63 C0 8B 8C C1 B8 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3", "AI buy-price complete function");
            CheckPattern(image, 0xCEB90, "49 63 C0 8B 8C C1 BC 17 18 00 B8 67 66 66 66 F7 E9 D1 FA 8B C2 C1 E8 1F 03 C2 41 0F AF C1 C3", "AI sell-price complete function");
            CheckPattern(image, 0x504F0, "4C 8B DC 55 41 56 41 57 48 83 EC 60 4C 8D 3D ?? ?? ?? ?? 48 63 EA 48 69 D5 3C 58 00 00", "AI flag ABI/prologue");
            CheckPattern(image, 0x9A164, "41 0F BF 44 18 18 03 D0 B8 ?? ?? ?? ?? 41 89 54 18 14 66 41 39 84 18 D0 00 00 00 7C 06", "plague lifetime and comparison span");
            CheckPattern(image, 0x9F86B, "83 3D ?? ?? ?? ?? 1E 7F ?? 0F BF 4B 1C 48 8D 15 ?? ?? ?? ?? 44 0F BF 4B 1A", "apothecary distance hook span");
            CheckPattern(image, 0x151436, "66 46 39 B4 2B 14 09 00 00 75 22 66 46 39 B4 2B 9E 09 00 00 74 17", "Monk movement hook and following branch");

            Check(ReadInt32(image, 0x9A16D) == 800, "plague lifetime immediate");
            Check(image[0x9F871] == 30, "apothecary Vanilla distance immediate");
            CheckWorkerTablePattern(image, 0x2E5E58);
        }

        private static void CheckFunction(byte[] image, int rva, int size, string expectedHash, string label)
        {
            byte[] bytes = new byte[size];
            Buffer.BlockCopy(image, rva, bytes, 0, size);
            Check(Hash(bytes) == expectedHash, label + " function hash");
        }

        private static void CheckPattern(byte[] image, int rva, string pattern, string label)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Check(rva >= 0 && rva <= image.Length - tokens.Length, label + " bounds");
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "??")
                    continue;
                Check(image[rva + i] == Convert.ToByte(tokens[i], 16), label + $" byte +0x{i:X}");
            }
        }

        private static void CheckWorkerTablePattern(byte[] image, int rva)
        {
            int[] expected = { 1, 1, 1, 1, 3, 0, 1, 1, 1, 0 };
            for (int i = 0; i < expected.Length; i++)
                Check(ReadInt32(image, rva + i * sizeof(int)) == expected[i], "worker table pattern value " + i);
        }

        private static void TestCatalogAndApply(byte[] image)
        {
            var memory = new FakeMemory(Base, image);
            var patch = new GatehouseTimingPatch(Base, image, true, memory);
            patch.Apply(1.0, 5.0, 10.0, 15.0, true);
            Check(memory.Int32(GatehouseTimingPatch.HumanDelayRva) == 40, "human delay conversion");
            Check(memory.Int32(GatehouseTimingPatch.AiDelayRva) == 200, "AI delay conversion");
            Check(memory.Int32(GatehouseTimingPatch.HumanDistanceRva) == 80, "human distance conversion");
            Check(memory.Int32(GatehouseTimingPatch.AiDistanceRva) == 120, "AI distance conversion");
            int writes = memory.WriteCount;
            patch.Apply(1.0, 5.0, 10.0, 15.0, true);
            Check(memory.WriteCount == writes, "idempotent apply");
            patch.Apply(double.NaN, double.NaN, double.NaN, double.NaN, false);
            Check(memory.Int32(GatehouseTimingPatch.HumanDelayRva) == GatehouseTimingPatch.VanillaHumanDelay, "disable restores Vanilla");
            Check(memory.MakeWritableCount == memory.RestoreCount, "page protections balanced");
            Check(memory.FlushCount >= 8, "each immediate flushed");
            patch.Dispose();
        }

        private static void TestValidation(byte[] image)
        {
            Expect<InvalidOperationException>(() => new GatehouseTimingPatch(Base, image, false, new FakeMemory(Base, image)), "unknown hash fails closed");
            byte[] changed = (byte[])image.Clone();
            changed[GatehouseTimingPatch.FunctionRva] ^= 1;
            Expect<InvalidOperationException>(() => new GatehouseTimingPatch(Base, changed, true, new FakeMemory(Base, changed)), "function hash mismatch");
            Expect<ArgumentOutOfRangeException>(() => GatehouseTimingPatch.SecondsToTicks(double.NaN), "NaN rejected");
            Expect<ArgumentOutOfRangeException>(() => GatehouseTimingPatch.TilesToNativeUnits(8192), "UInt16 overflow rejected");

            var memory = new FakeMemory(Base, image);
            var patch = new GatehouseTimingPatch(Base, image, true, memory);
            memory.SetByte(GatehouseTimingPatch.DecisionBlockRva, 0x90);
            Expect<InvalidOperationException>(() => patch.Apply(1, 5, 10, 15, true), "external opcode mutation rejected");
            Check(memory.WriteCount == 0, "mutation rejected before write");
        }

        private static void TestRollbackAndCleanup(byte[] image)
        {
            var memory = new FakeMemory(Base, image) { FailWriteNumber = 2 };
            var patch = new GatehouseTimingPatch(Base, image, true, memory);
            Expect<InvalidOperationException>(() => patch.Apply(1, 5, 10, 15, true), "partial write fails");
            Check(memory.Int32(GatehouseTimingPatch.AiDistanceRva) == GatehouseTimingPatch.VanillaAiDistance &&
                memory.Int32(GatehouseTimingPatch.AiDelayRva) == GatehouseTimingPatch.VanillaAiDelay, "partial write rolled back");
            Check(memory.MakeWritableCount == memory.RestoreCount, "rollback restores protection");

            memory = new FakeMemory(Base, image) { FailWriteNumber = 2, FailWriteNumber2 = 3 };
            patch = new GatehouseTimingPatch(Base, image, true, memory);
            Expect<AggregateException>(() => patch.Apply(1, 5, 10, 15, true), "write and rollback failure are combined");
            Check(memory.MakeWritableCount == memory.RestoreCount, "rollback failure still restores protection");

            memory = new FakeMemory(Base, image) { FailMakeWritable = true };
            patch = new GatehouseTimingPatch(Base, image, true, memory);
            Expect<InvalidOperationException>(() => patch.Apply(1, 5, 10, 15, true), "protection acquisition fails");
            Check(memory.WriteCount == 0, "protection failure writes nothing");

            memory = new FakeMemory(Base, image) { FailRestoreNumber = 1 };
            patch = new GatehouseTimingPatch(Base, image, true, memory);
            Expect<InvalidOperationException>(() => patch.Apply(1, 5, 10, 15, true), "cleanup failure reported");
            Check(memory.Int32(GatehouseTimingPatch.HumanDelayRva) == 40, "committed state retained after cleanup failure");
            memory.FailRestoreNumber = 0;
            patch.Apply(0, 0, 0, 0, false);
            Check(memory.Int32(GatehouseTimingPatch.HumanDelayRva) == GatehouseTimingPatch.VanillaHumanDelay, "cleanup failure remains recoverable");

            memory = new FakeMemory(Base, image) { FailFlushNumber = 1 };
            patch = new GatehouseTimingPatch(Base, image, true, memory);
            Expect<InvalidOperationException>(() => patch.Apply(1, 5, 10, 15, true), "flush failure reported");
            memory.FailFlushNumber = 0;
            patch.Apply(0, 0, 0, 0, false);
            Check(memory.Int32(GatehouseTimingPatch.AiDistanceRva) == GatehouseTimingPatch.VanillaAiDistance, "flush failure remains recoverable");
        }

        private static void TestAdjacentHookCompatibility(byte[] image)
        {
            Check(GatehouseTimingPatch.OwnedTimingRegionEndRva == 0xB7C39,
                "gatehouse timing ownership ends before the adjacent Fixes hook");

            // Simulate Fixes loading first: the immutable source image remains Vanilla,
            // while the live process byte at its independent hook start is already patched.
            var memory = new FakeMemory(Base, image);
            memory.SetByte(GatehouseTimingPatch.OwnedTimingRegionEndRva, 0xE9);
            var patch = new GatehouseTimingPatch(Base, image, true, memory);
            patch.Apply(1.0, 5.0, 10.0, 15.0, true);
            Check(memory.Byte(GatehouseTimingPatch.OwnedTimingRegionEndRva) == 0xE9,
                "preinstalled adjacent hook is preserved while applying timing values");
            patch.Dispose();
            Check(memory.Byte(GatehouseTimingPatch.OwnedTimingRegionEndRva) == 0xE9,
                "preinstalled adjacent hook is preserved while restoring Vanilla values");

            // Simulate Extra Features loading first and Fixes installing afterwards.
            memory = new FakeMemory(Base, image);
            patch = new GatehouseTimingPatch(Base, image, true, memory);
            memory.SetByte(GatehouseTimingPatch.OwnedTimingRegionEndRva, 0xE9);
            patch.Apply(1.0, 5.0, 10.0, 15.0, true);
            Check(memory.Byte(GatehouseTimingPatch.OwnedTimingRegionEndRva) == 0xE9,
                "later adjacent hook is accepted and preserved");

            memory.SetByte(GatehouseTimingPatch.HumanDelayRva - 1, 0x90);
            Expect<InvalidOperationException>(() => patch.Apply(2.0, 6.0, 11.0, 16.0, true),
                "mutation of the owned human-delay opcode remains fail-closed");
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int pe = ReadInt32(file, 0x3C), count = ReadUInt16(file, pe + 6), optionalSize = ReadUInt16(file, pe + 20), optional = pe + 24;
            int imageSize = ReadInt32(file, optional + 56), headers = ReadInt32(file, optional + 60);
            var image = new byte[imageSize];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(headers, file.Length));
            int table = optional + optionalSize;
            for (int i = 0; i < count; i++)
            {
                int h = table + i * 40, virtualAddress = ReadInt32(file, h + 12), rawSize = ReadInt32(file, h + 16), raw = ReadInt32(file, h + 20);
                if (rawSize > 0) Buffer.BlockCopy(file, raw, image, virtualAddress, Math.Min(rawSize, file.Length - raw));
            }
            return image;
        }

        private static int ReadInt32(byte[] value, int offset) => value[offset] | value[offset + 1] << 8 | value[offset + 2] << 16 | value[offset + 3] << 24;
        private static int ReadUInt16(byte[] value, int offset) => value[offset] | value[offset + 1] << 8;
        private static string Hash(byte[] value) { using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", ""); }
        private static void Check(bool condition, string message) { assertions++; if (!condition) throw new InvalidOperationException(message); }
        private static void Expect<T>(Action action, string message) where T : Exception
        {
            assertions++;
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException(message);
        }

        private sealed class FakeMemory : IGatehouseTimingMemory
        {
            private readonly long baseAddress;
            private readonly byte[] bytes;
            private int writeAttempts, restoreAttempts, flushAttempts;
            public FakeMemory(long baseAddress, byte[] image) { this.baseAddress = baseAddress; bytes = (byte[])image.Clone(); }
            public int PageSize => 4096;
            public int WriteCount { get; private set; }
            public int MakeWritableCount { get; private set; }
            public int RestoreCount { get; private set; }
            public int FlushCount { get; private set; }
            public int FailWriteNumber { get; set; }
            public int FailWriteNumber2 { get; set; }
            public int FailRestoreNumber { get; set; }
            public int FailFlushNumber { get; set; }
            public bool FailMakeWritable { get; set; }
            public byte ReadByte(long address) => bytes[Index(address)];
            public int ReadInt32(long address) => Program.ReadInt32(bytes, Index(address));
            public void WriteInt32(long address, int value)
            {
                writeAttempts++;
                if (FailWriteNumber == writeAttempts || FailWriteNumber2 == writeAttempts) throw new InvalidOperationException("injected write failure");
                int i = Index(address); bytes[i] = (byte)value; bytes[i + 1] = (byte)(value >> 8); bytes[i + 2] = (byte)(value >> 16); bytes[i + 3] = (byte)(value >> 24); WriteCount++;
            }
            public uint MakeWritable(long address, int length) { if (FailMakeWritable) throw new InvalidOperationException("injected protection failure"); MakeWritableCount++; return 0x20; }
            public void RestoreProtection(long address, int length, uint protection) { restoreAttempts++; if (FailRestoreNumber == restoreAttempts) throw new InvalidOperationException("injected restore failure"); RestoreCount++; }
            public void Flush(long address, int length) { flushAttempts++; if (FailFlushNumber == flushAttempts) throw new InvalidOperationException("injected flush failure"); FlushCount++; }
            public int Int32(int rva) => Program.ReadInt32(bytes, rva);
            public byte Byte(int rva) => bytes[rva];
            public void SetByte(int rva, byte value) => bytes[rva] = value;
            private int Index(long address) => checked((int)(address - baseAddress));
        }
    }
}

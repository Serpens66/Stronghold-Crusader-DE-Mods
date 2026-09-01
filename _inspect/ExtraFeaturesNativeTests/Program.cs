using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

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
                TestCatalogAndApply(image);
                TestValidation(image);
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
            public void SetByte(int rva, byte value) => bytes[rva] = value;
            private int Index(long address) => checked((int)(address - baseAddress));
        }
    }
}

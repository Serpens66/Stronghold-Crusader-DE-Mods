// Feature: Configure Vanilla gatehouse enemy distances and reopening delays.
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ExtraFeatures
{
    internal sealed class GatehouseTimingPatch : IDisposable
    {
        public const double MinimumHumanDelaySeconds = 0.0, MaximumHumanDelaySeconds = 30.0;
        public const double MinimumAiDelaySeconds = 0.0, MaximumAiDelaySeconds = 120.0;
        public const double MinimumDistanceTiles = 5.0, MaximumDistanceTiles = 50.0;
        public const double VanillaHumanDelaySeconds = 2.5, VanillaAiDelaySeconds = 30.0;
        public const double VanillaHumanDistanceTiles = 17.5, VanillaAiDistanceTiles = 25.0;
        public const int TicksPerReferenceSecond = 40, NativeUnitsPerTile = 8;

        internal const string SupportedBuildHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int FunctionRva = 0xB73D0, FunctionSize = 2325;
        internal const string FunctionHash = "F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8";
        internal const int DecisionBlockRva = 0xB7BBB, HumanDelayBlockRva = 0xB7C32;
        internal const int AiDistanceRva = 0xB7BC3, AiDelayRva = 0xB7BCA;
        internal const int HumanDistanceRva = 0xB7BD3, HumanDelayRva = 0xB7C35;
        internal const int VanillaAiDistance = 200, VanillaAiDelay = 1200;
        internal const int VanillaHumanDistance = 140, VanillaHumanDelay = 100;

        private static readonly byte[] DecisionBlockBytes = Hex(
            "40 84 F6 75 10 41 81 F8 C8 00 00 00 7D 10 B8 B0 04 00 00 EB 69 " +
            "41 81 F8 8C 00 00 00 7C 5B");
        private static readonly byte[] HumanDelayBlockBytes = Hex(
            "EB 50 B8 64 00 00 00 48 8D 2D C0 83 F4 FF");

        private readonly IGatehouseTimingMemory memory;
        private readonly GatehouseTimingTarget target;
        private readonly object sync = new object();
        private int expectedAiDistance = VanillaAiDistance, expectedAiDelay = VanillaAiDelay;
        private int expectedHumanDistance = VanillaHumanDistance, expectedHumanDelay = VanillaHumanDelay;
        private bool disposed;

        public GatehouseTimingPatch(ManualLogSource log, IntPtr libraryHandle, ReadOnlySpan<byte> image, bool referenceHashMatches)
            : this(libraryHandle.ToInt64(), image, referenceHashMatches, new ProcessGatehouseTimingMemory(), log) { }

        internal GatehouseTimingPatch(long moduleBase, ReadOnlySpan<byte> image, bool referenceHashMatches, IGatehouseTimingMemory memory)
            : this(moduleBase, image, referenceHashMatches, memory, null) { }

        private GatehouseTimingPatch(long moduleBase, ReadOnlySpan<byte> image, bool referenceHashMatches, IGatehouseTimingMemory memory, ManualLogSource log)
        {
            if (!referenceHashMatches)
                throw new InvalidOperationException("The installed CrusaderDE.dll hash is not present in the gatehouse timing target catalog.");
            if (moduleBase == 0 || image.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            target = ResolveTarget(moduleBase, image);
            VerifyExpected();
            Shared.DebugLogHelper.LogInfo(log,
                $"Extra Features gatehouse native values initialized: build={SupportedBuildHash}, " +
                $"functionRva=0x{FunctionRva:X}-0x{FunctionRva + FunctionSize:X}, functionHash={FunctionHash}, " +
                $"aiDistanceRva=0x{AiDistanceRva:X}, aiDelayRva=0x{AiDelayRva:X}, " +
                $"humanDistanceRva=0x{HumanDistanceRva:X}, humanDelayRva=0x{HumanDelayRva:X}.");
        }

        public void Apply(double humanDelaySeconds, double aiDelaySeconds, double humanDistanceTiles, double aiDistanceTiles, bool enabled)
        {
            ThrowIfDisposed();
            int hd, ad, hdist, adist;
            if (enabled)
            {
                ValidateRange(humanDelaySeconds, MinimumHumanDelaySeconds, MaximumHumanDelaySeconds, nameof(humanDelaySeconds));
                ValidateRange(aiDelaySeconds, MinimumAiDelaySeconds, MaximumAiDelaySeconds, nameof(aiDelaySeconds));
                ValidateRange(humanDistanceTiles, MinimumDistanceTiles, MaximumDistanceTiles, nameof(humanDistanceTiles));
                ValidateRange(aiDistanceTiles, MinimumDistanceTiles, MaximumDistanceTiles, nameof(aiDistanceTiles));
                hd = ConvertNativeUInt16(humanDelaySeconds, TicksPerReferenceSecond, nameof(humanDelaySeconds));
                ad = ConvertNativeUInt16(aiDelaySeconds, TicksPerReferenceSecond, nameof(aiDelaySeconds));
                hdist = ConvertNativeUInt16(humanDistanceTiles, NativeUnitsPerTile, nameof(humanDistanceTiles));
                adist = ConvertNativeUInt16(aiDistanceTiles, NativeUnitsPerTile, nameof(aiDistanceTiles));
            }
            else
            {
                hd = VanillaHumanDelay; ad = VanillaAiDelay;
                hdist = VanillaHumanDistance; adist = VanillaAiDistance;
            }
            SetValues(adist, ad, hdist, hd);
        }

        public void RestoreVanilla()
        {
            ThrowIfDisposed();
            SetValues(VanillaAiDistance, VanillaAiDelay, VanillaHumanDistance, VanillaHumanDelay);
        }

        public void Dispose()
        {
            if (disposed) return;
            RestoreVanilla();
            disposed = true;
        }

        public static int SecondsToTicks(double value) => ConvertNativeUInt16(value, TicksPerReferenceSecond, nameof(value));
        public static int TilesToNativeUnits(double value) => ConvertNativeUInt16(value, NativeUnitsPerTile, nameof(value));

        internal static int ConvertNativeUInt16(double value, int multiplier, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, name + " must be finite.");
            int result = checked((int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));
            if (result < ushort.MinValue || result > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(name, $"{name} converts to {result}, outside the native UInt16 range.");
            return result;
        }

        private void SetValues(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
        {
            lock (sync)
            {
                VerifyExpected();
                if (ValuesEqual(aiDistance, aiDelay, humanDistance, humanDelay)) return;
                int oldAdist = expectedAiDistance, oldAd = expectedAiDelay;
                int oldHdist = expectedHumanDistance, oldHd = expectedHumanDelay;
                try
                {
                    GatehouseTimingTransaction.Execute(memory, target.Intervals, VerifyExpected,
                        () => WriteAndVerify(aiDistance, aiDelay, humanDistance, humanDelay, ""),
                        () => WriteAndVerify(oldAdist, oldAd, oldHdist, oldHd, "rolled-back "));
                }
                catch
                {
                    // Cleanup may fail after all writes succeeded. Retain a fully verified new
                    // state so disable/dispose can still restore Vanilla on a later attempt.
                    if (ValuesEqual(aiDistance, aiDelay, humanDistance, humanDelay))
                    {
                        expectedAiDistance = aiDistance; expectedAiDelay = aiDelay;
                        expectedHumanDistance = humanDistance; expectedHumanDelay = humanDelay;
                    }
                    throw;
                }
                expectedAiDistance = aiDistance; expectedAiDelay = aiDelay;
                expectedHumanDistance = humanDistance; expectedHumanDelay = humanDelay;
                VerifyExpected();
            }
        }

        private void WriteAndVerify(int aiDistance, int aiDelay, int humanDistance, int humanDelay, string prefix)
        {
            memory.WriteInt32(target.AiDistance, aiDistance);
            memory.WriteInt32(target.AiDelay, aiDelay);
            memory.WriteInt32(target.HumanDistance, humanDistance);
            memory.WriteInt32(target.HumanDelay, humanDelay);
            VerifyValue(target.AiDistance, aiDistance, prefix + "AI distance");
            VerifyValue(target.AiDelay, aiDelay, prefix + "AI delay");
            VerifyValue(target.HumanDistance, humanDistance, prefix + "human distance");
            VerifyValue(target.HumanDelay, humanDelay, prefix + "human delay");
        }

        private void VerifyExpected()
        {
            foreach (GatehouseInstructionInvariant item in target.Invariants)
                if (memory.ReadByte(item.Address) != item.Value)
                    throw new InvalidOperationException($"A gatehouse instruction byte changed unexpectedly at 0x{item.Address:X}.");
            VerifyValue(target.AiDistance, expectedAiDistance, "AI distance");
            VerifyValue(target.AiDelay, expectedAiDelay, "AI delay");
            VerifyValue(target.HumanDistance, expectedHumanDistance, "human distance");
            VerifyValue(target.HumanDelay, expectedHumanDelay, "human delay");
        }

        private bool ValuesEqual(int adist, int ad, int hdist, int hd) =>
            memory.ReadInt32(target.AiDistance) == adist && memory.ReadInt32(target.AiDelay) == ad &&
            memory.ReadInt32(target.HumanDistance) == hdist && memory.ReadInt32(target.HumanDelay) == hd;

        private void VerifyValue(long address, int expected, string name)
        {
            int actual = memory.ReadInt32(address);
            if (actual != expected)
                throw new InvalidOperationException($"The gatehouse {name} changed unexpectedly: expected={expected}, actual={actual}.");
        }

        private static GatehouseTimingTarget ResolveTarget(long moduleBase, ReadOnlySpan<byte> image)
        {
            GatehousePeSection section = GatehousePeImage.Parse(image).RequireExecutableRange(FunctionRva, FunctionSize, "gatehouse handler");
            string actualHash = ComputeSha256(image.Slice(FunctionRva, FunctionSize));
            if (!string.Equals(actualHash, FunctionHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"The gatehouse handler function hash changed: expected={FunctionHash}, actual={actualHash}.");

            RequireRange(section, DecisionBlockRva, DecisionBlockBytes.Length, "decision block");
            RequireRange(section, HumanDelayBlockRva, HumanDelayBlockBytes.Length, "human delay block");
            RequireBytes(image, DecisionBlockRva, DecisionBlockBytes, "decision block");
            RequireBytes(image, HumanDelayBlockRva, HumanDelayBlockBytes, "human delay block");
            RequireInt32(image, AiDistanceRva, VanillaAiDistance, "AI close distance");
            RequireInt32(image, AiDelayRva, VanillaAiDelay, "AI reopen delay");
            RequireInt32(image, HumanDistanceRva, VanillaHumanDistance, "human close distance");
            RequireInt32(image, HumanDelayRva, VanillaHumanDelay, "human reopen delay");

            var invariants = new List<GatehouseInstructionInvariant>();
            AddInvariants(invariants, moduleBase, DecisionBlockRva, DecisionBlockBytes, AiDistanceRva, AiDelayRva, HumanDistanceRva);
            AddInvariants(invariants, moduleBase, HumanDelayBlockRva, HumanDelayBlockBytes, HumanDelayRva);
            return new GatehouseTimingTarget(moduleBase + AiDistanceRva, moduleBase + AiDelayRva,
                moduleBase + HumanDistanceRva, moduleBase + HumanDelayRva, invariants);
        }

        private static void AddInvariants(List<GatehouseInstructionInvariant> result, long moduleBase, int blockRva, byte[] bytes, params int[] mutableRvas)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                int rva = checked(blockRva + i); bool mutable = false;
                foreach (int start in mutableRvas)
                    if (rva >= start && rva < start + sizeof(int)) mutable = true;
                if (!mutable) result.Add(new GatehouseInstructionInvariant(moduleBase + rva, bytes[i]));
            }
        }

        private static void RequireRange(GatehousePeSection section, int rva, int length, string name)
        {
            int end = checked(FunctionRva + FunctionSize);
            if (rva < FunctionRva || length <= 0 || rva > end - length || !section.Contains(rva, length))
                throw new InvalidOperationException("The gatehouse " + name + " lies outside its catalogued executable function.");
        }

        private static void RequireBytes(ReadOnlySpan<byte> image, int rva, byte[] expected, string name)
        {
            if (rva < 0 || rva > image.Length - expected.Length) throw new InvalidOperationException(name + " lies outside the image.");
            for (int i = 0; i < expected.Length; i++)
                if (image[rva + i] != expected[i]) throw new InvalidOperationException($"The gatehouse {name} changed at +0x{i:X}.");
        }

        private static void RequireInt32(ReadOnlySpan<byte> image, int rva, int expected, string name)
        {
            int actual = GatehousePeImage.ReadInt32(image, rva);
            if (actual != expected) throw new InvalidOperationException($"The gatehouse {name} changed: expected={expected}, actual={actual}.");
        }

        private static void ValidateRange(double value, double min, double max, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name, $"{name} must be finite and between {min} and {max}.");
        }

        private static string ComputeSha256(ReadOnlySpan<byte> bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "");
        }

        private static byte[] Hex(string text)
        {
            string[] parts = text.Split(' '); var result = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++) result[i] = Convert.ToByte(parts[i], 16);
            return result;
        }

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GatehouseTimingPatch)); }
    }

    internal interface IGatehouseTimingMemory
    {
        int PageSize { get; }
        byte ReadByte(long address);
        int ReadInt32(long address);
        void WriteInt32(long address, int value);
        uint MakeWritable(long address, int length);
        void RestoreProtection(long address, int length, uint protection);
        void Flush(long address, int length);
    }

    internal sealed class ProcessGatehouseTimingMemory : IGatehouseTimingMemory
    {
        public int PageSize => Environment.SystemPageSize;
        public byte ReadByte(long address) => Marshal.ReadByte(new IntPtr(address));
        public int ReadInt32(long address) => Marshal.ReadInt32(new IntPtr(address));
        public void WriteInt32(long address, int value) => Marshal.WriteInt32(new IntPtr(address), value);
        public uint MakeWritable(long address, int length)
        {
            if (!VirtualProtect(new IntPtr(address), (UIntPtr)(uint)length, 0x40, out uint old))
                throw new InvalidOperationException($"VirtualProtect failed with Win32 error {Marshal.GetLastWin32Error()}.");
            return old;
        }
        public void RestoreProtection(long address, int length, uint protection)
        {
            if (!VirtualProtect(new IntPtr(address), (UIntPtr)(uint)length, protection, out _))
                throw new InvalidOperationException($"Restoring memory protection failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }
        public void Flush(long address, int length)
        {
            if (!FlushInstructionCache(GetCurrentProcess(), new IntPtr(address), (UIntPtr)(uint)length))
                throw new InvalidOperationException($"FlushInstructionCache failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protection, out uint old);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }

    internal readonly struct GatehouseNativeInterval
    {
        public GatehouseNativeInterval(long start, long end) { if (start < 0 || end <= start) throw new ArgumentOutOfRangeException(nameof(start)); Start = start; End = end; }
        public long Start { get; }
        public long End { get; }
    }

    internal readonly struct GatehouseInstructionInvariant
    {
        public GatehouseInstructionInvariant(long address, byte value) { Address = address; Value = value; }
        public long Address { get; }
        public byte Value { get; }
    }

    internal sealed class GatehouseTimingTarget
    {
        public GatehouseTimingTarget(long adist, long ad, long hdist, long hd, IReadOnlyList<GatehouseInstructionInvariant> invariants)
        {
            AiDistance = adist; AiDelay = ad; HumanDistance = hdist; HumanDelay = hd; Invariants = invariants;
            Intervals = new[] { new GatehouseNativeInterval(adist, adist + 4), new GatehouseNativeInterval(ad, ad + 4), new GatehouseNativeInterval(hdist, hdist + 4), new GatehouseNativeInterval(hd, hd + 4) };
        }
        public long AiDistance { get; }
        public long AiDelay { get; }
        public long HumanDistance { get; }
        public long HumanDelay { get; }
        public IReadOnlyList<GatehouseInstructionInvariant> Invariants { get; }
        public IReadOnlyList<GatehouseNativeInterval> Intervals { get; }
    }

    internal static class GatehouseTimingTransaction
    {
        public static void Execute(IGatehouseTimingMemory memory, IReadOnlyList<GatehouseNativeInterval> intervals, Action verify, Action write, Action rollback)
        {
            List<PageProtection> pages = Acquire(memory, intervals); Exception primary = null, cleanup = null; bool started = false;
            try
            {
                try { verify(); started = true; write(); }
                catch (Exception ex)
                {
                    primary = ex;
                    if (started) try { rollback(); } catch (Exception rb) { primary = new AggregateException("The native write and rollback both failed.", ex, rb); }
                }
            }
            finally
            {
                for (int i = pages.Count - 1; i >= 0; i--)
                    try { memory.RestoreProtection(pages[i].Address, memory.PageSize, pages[i].Protection); } catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                foreach (GatehouseNativeInterval interval in intervals)
                    try { memory.Flush(interval.Start, checked((int)(interval.End - interval.Start))); } catch (Exception ex) { cleanup = Combine(cleanup, ex); }
            }
            if (primary != null && cleanup != null) throw new AggregateException("The gatehouse transaction and cleanup both failed.", primary, cleanup);
            if (primary != null) throw primary;
            if (cleanup != null) throw cleanup;
        }

        private static List<PageProtection> Acquire(IGatehouseTimingMemory memory, IReadOnlyList<GatehouseNativeInterval> intervals)
        {
            if (memory.PageSize <= 0) throw new InvalidOperationException("Invalid native page size.");
            var unique = new SortedSet<long>();
            foreach (GatehouseNativeInterval interval in intervals)
                for (long page = Page(interval.Start, memory.PageSize), last = Page(interval.End - 1, memory.PageSize); page <= last; page = checked(page + memory.PageSize)) unique.Add(page);
            var result = new List<PageProtection>();
            try { foreach (long page in unique) result.Add(new PageProtection(page, memory.MakeWritable(page, memory.PageSize))); return result; }
            catch (Exception primary)
            {
                Exception cleanup = null;
                for (int i = result.Count - 1; i >= 0; i--) try { memory.RestoreProtection(result[i].Address, memory.PageSize, result[i].Protection); } catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                if (cleanup != null) throw new AggregateException("Acquiring writable pages and cleanup both failed.", primary, cleanup);
                throw;
            }
        }
        private static long Page(long address, int size) => address - address % size;
        private static Exception Combine(Exception a, Exception b) => a == null ? b : new AggregateException(a, b);
        private readonly struct PageProtection { public PageProtection(long address, uint protection) { Address = address; Protection = protection; } public long Address { get; } public uint Protection { get; } }
    }

    internal readonly struct GatehousePeSection
    {
        public GatehousePeSection(int start, int length, uint flags) { Start = start; Length = length; Flags = flags; }
        public int Start { get; }
        public int Length { get; }
        public uint Flags { get; }
        public bool Executable => (Flags & 0x20000000u) != 0;
        public bool Contains(int start, int length) => start >= Start && length >= 0 && start <= checked(Start + Length) - length;
    }

    internal sealed class GatehousePeImage
    {
        private readonly GatehousePeSection[] sections;
        private GatehousePeImage(GatehousePeSection[] sections) { this.sections = sections; }
        public static GatehousePeImage Parse(ReadOnlySpan<byte> image)
        {
            if (image.Length < 0x100 || image[0] != 0x4D || image[1] != 0x5A) throw new InvalidOperationException("Invalid DOS header.");
            int pe = ReadInt32(image, 0x3C);
            if (pe < 0 || pe > image.Length - 0x80 || ReadInt32(image, pe) != 0x4550) throw new InvalidOperationException("Invalid PE header.");
            int count = ReadUInt16(image, pe + 6), optionalSize = ReadUInt16(image, pe + 20), optional = checked(pe + 24);
            if (optionalSize < 60 || ReadUInt16(image, optional) != 0x20B) throw new InvalidOperationException("The module is not PE32+.");
            int imageSize = checked((int)ReadUInt32(image, optional + 56));
            if (count <= 0 || count > 96 || imageSize <= 0 || imageSize > image.Length) throw new InvalidOperationException("Invalid PE image size or section count.");
            int table = checked(optional + optionalSize);
            if (table > image.Length - count * 40) throw new InvalidOperationException("PE section table outside image.");
            var sections = new GatehousePeSection[count];
            for (int i = 0; i < count; i++)
            {
                int h = table + i * 40, size = Math.Max(checked((int)ReadUInt32(image, h + 8)), checked((int)ReadUInt32(image, h + 16)));
                int rva = checked((int)ReadUInt32(image, h + 12));
                if (rva < 0 || size <= 0 || rva > imageSize - size) throw new InvalidOperationException("PE section outside image.");
                sections[i] = new GatehousePeSection(rva, size, ReadUInt32(image, h + 36));
            }
            return new GatehousePeImage(sections);
        }
        public GatehousePeSection RequireExecutableRange(int start, int length, string name)
        {
            foreach (GatehousePeSection section in sections) if (section.Executable && section.Contains(start, length)) return section;
            throw new InvalidOperationException(name + " lies outside executable PE sections.");
        }
        internal static int ReadInt32(ReadOnlySpan<byte> image, int offset)
        {
            if (offset < 0 || offset > image.Length - 4) throw new InvalidOperationException("Int32 read outside image.");
            return image[offset] | image[offset + 1] << 8 | image[offset + 2] << 16 | image[offset + 3] << 24;
        }
        private static int ReadUInt16(ReadOnlySpan<byte> image, int offset) { if (offset < 0 || offset > image.Length - 2) throw new InvalidOperationException("UInt16 read outside image."); return image[offset] | image[offset + 1] << 8; }
        private static uint ReadUInt32(ReadOnlySpan<byte> image, int offset) => unchecked((uint)ReadInt32(image, offset));
    }
}

using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SerpNativeAPI
{
    internal enum NativeReservationMode
    {
        Exclusive
    }

    internal readonly struct NativeInterval
    {
        public NativeInterval(long start, long end)
        {
            if (start < 0 || end <= start)
                throw new ArgumentOutOfRangeException(nameof(start));
            Start = start;
            End = end;
        }

        public long Start { get; }
        public long End { get; }
        public bool Overlaps(NativeInterval other) => Start < other.End && other.Start < End;
    }

    internal sealed class NativeOwnershipRegistry
    {
        private readonly object sync = new object();
        private readonly List<Reservation> reservations = new List<Reservation>();

        public bool TryReserve(
            string ownerGuid,
            string capabilityId,
            NativeReservationMode mode,
            IReadOnlyList<NativeInterval> intervals,
            out string conflictOwner)
        {
            conflictOwner = null;
            lock (sync)
            {
                foreach (Reservation existing in reservations)
                {
                    if (string.Equals(existing.OwnerGuid, ownerGuid, StringComparison.Ordinal) &&
                        string.Equals(existing.CapabilityId, capabilityId, StringComparison.Ordinal) &&
                        existing.Mode == mode && IntervalsEqual(existing.Intervals, intervals))
                    {
                        return true;
                    }

                    if (AnyOverlap(existing.Intervals, intervals))
                    {
                        conflictOwner = existing.OwnerGuid;
                        return false;
                    }
                }

                reservations.Add(new Reservation(ownerGuid, capabilityId, mode, Copy(intervals)));
                return true;
            }
        }

        private static bool AnyOverlap(IReadOnlyList<NativeInterval> first, IReadOnlyList<NativeInterval> second)
        {
            for (int left = 0; left < first.Count; left++)
                for (int right = 0; right < second.Count; right++)
                    if (first[left].Overlaps(second[right]))
                        return true;
            return false;
        }

        private static bool IntervalsEqual(IReadOnlyList<NativeInterval> first, IReadOnlyList<NativeInterval> second)
        {
            if (first.Count != second.Count)
                return false;
            for (int index = 0; index < first.Count; index++)
                if (first[index].Start != second[index].Start || first[index].End != second[index].End)
                    return false;
            return true;
        }

        private static NativeInterval[] Copy(IReadOnlyList<NativeInterval> source)
        {
            var result = new NativeInterval[source.Count];
            for (int index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        private sealed class Reservation
        {
            public Reservation(string ownerGuid, string capabilityId, NativeReservationMode mode, NativeInterval[] intervals)
            {
                OwnerGuid = ownerGuid;
                CapabilityId = capabilityId;
                Mode = mode;
                Intervals = intervals;
            }

            public string OwnerGuid { get; }
            public string CapabilityId { get; }
            public NativeReservationMode Mode { get; }
            public NativeInterval[] Intervals { get; }
        }
    }

    internal interface INativeMemory
    {
        int PageSize { get; }
        byte ReadByte(long address);
        int ReadInt32(long address);
        void WriteByte(long address, byte value);
        void WriteInt32(long address, int value);
        uint MakeWritable(long address, int length);
        void RestoreProtection(long address, int length, uint protection);
        void Flush(long address, int length);
    }

    internal sealed class ProcessNativeMemory : INativeMemory
    {
        private const uint PageExecuteReadWrite = 0x40;

        public int PageSize => Environment.SystemPageSize;
        public byte ReadByte(long address) => Marshal.ReadByte(new IntPtr(address));
        public int ReadInt32(long address) => Marshal.ReadInt32(new IntPtr(address));
        public void WriteByte(long address, byte value) => Marshal.WriteByte(new IntPtr(address), value);
        public void WriteInt32(long address, int value) => Marshal.WriteInt32(new IntPtr(address), value);

        public uint MakeWritable(long address, int length)
        {
            if (!VirtualProtect(new IntPtr(address), (UIntPtr)(uint)length, PageExecuteReadWrite, out uint oldProtection))
                throw new InvalidOperationException($"VirtualProtect failed with Win32 error {Marshal.GetLastWin32Error()}.");
            return oldProtection;
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtection, out uint oldProtection);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
    }

    internal sealed class NativeResolutionException : Exception
    {
        public NativeResolutionException(NativeCapabilityState state, string message) : base(message) => State = state;
        public NativeCapabilityState State { get; }
    }

    internal readonly struct NativeSection
    {
        public NativeSection(int start, int length, uint characteristics)
        {
            Start = start;
            Length = length;
            Characteristics = characteristics;
        }

        public int Start { get; }
        public int Length { get; }
        public uint Characteristics { get; }
        public int End => checked(Start + Length);
        public bool Executable => (Characteristics & 0x20000000u) != 0;
        public bool Contains(int start, int length) => start >= Start && length >= 0 && start <= End - length;
    }

    internal sealed class NativePeImage
    {
        private NativePeImage(int imageSize, NativeSection[] sections)
        {
            ImageSize = imageSize;
            Sections = sections;
        }

        public int ImageSize { get; }
        public NativeSection[] Sections { get; }

        public static NativePeImage Parse(ReadOnlySpan<byte> memory)
        {
            if (memory.Length < 0x100 || memory[0] != 0x4D || memory[1] != 0x5A)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native module has no valid DOS header.");
            int pe = ReadInt32(memory, 0x3C);
            if (pe < 0 || pe > memory.Length - 0x80 || ReadInt32(memory, pe) != 0x4550)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native module has no valid PE header.");
            int sectionCount = ReadUInt16(memory, pe + 6);
            int optionalSize = ReadUInt16(memory, pe + 20);
            int optional = pe + 24;
            if (optionalSize < 60 || ReadUInt16(memory, optional) != 0x20B)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native module is not a valid PE32+ image.");
            int imageSize = checked((int)ReadUInt32(memory, optional + 56));
            if (sectionCount <= 0 || sectionCount > 96 || imageSize <= 0 || imageSize > memory.Length)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native PE image size or section count is invalid.");
            int table = checked(optional + optionalSize);
            if (table < 0 || table > memory.Length - sectionCount * 40)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native PE section table lies outside the module.");
            var sections = new NativeSection[sectionCount];
            for (int index = 0; index < sectionCount; index++)
            {
                int header = table + index * 40;
                int virtualSize = checked((int)ReadUInt32(memory, header + 8));
                int virtualAddress = checked((int)ReadUInt32(memory, header + 12));
                int rawSize = checked((int)ReadUInt32(memory, header + 16));
                int length = Math.Max(virtualSize, rawSize);
                if (virtualAddress < 0 || length <= 0 || virtualAddress > imageSize - length)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "A native PE section lies outside the image.");
                sections[index] = new NativeSection(virtualAddress, length, ReadUInt32(memory, header + 36));
            }
            return new NativePeImage(imageSize, sections);
        }

        public NativeSection RequireExecutableRange(int start, int length, string target)
        {
            foreach (NativeSection section in Sections)
                if (section.Executable && section.Contains(start, length))
                    return section;
            throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, target + " lies outside executable PE sections.");
        }

        public NativeSection RequireMappedRange(int start, int length, string target)
        {
            foreach (NativeSection section in Sections)
                if (section.Contains(start, length))
                    return section;
            throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, target + " lies outside mapped PE sections.");
        }

        internal static int ReadInt32(ReadOnlySpan<byte> memory, int offset)
        {
            if (offset < 0 || offset > memory.Length - 4)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "A native Int32 read lies outside the module.");
            return memory[offset] | memory[offset + 1] << 8 | memory[offset + 2] << 16 | memory[offset + 3] << 24;
        }

        private static int ReadUInt16(ReadOnlySpan<byte> memory, int offset) => memory[offset] | memory[offset + 1] << 8;
        private static uint ReadUInt32(ReadOnlySpan<byte> memory, int offset) => unchecked((uint)ReadInt32(memory, offset));
    }

    internal static class NativeApiLog
    {
        public static void Info(ManualLogSource log, string message) => log?.LogInfo(Stamp(message));
        public static void Warning(ManualLogSource log, string message) => log?.LogWarning(Stamp(message));
        public static void Error(ManualLogSource log, string message) => log?.LogError(Stamp(message));
        private static string Stamp(string message) => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
    }
}

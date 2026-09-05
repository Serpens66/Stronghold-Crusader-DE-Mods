// Feature: Permit barracks and keep rally points despite native reachability rejection.
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using RedBird.Core.Memory;

namespace BugfixesAndQoL
{
    internal sealed class AssemblyPointPlacementPatch : IDisposable
    {
        // Every assembly-point group uses this status selection for its preview.
        private const string ConstructingFailureStatusPattern =
            "45 84 ED 74 3D 85 C9 BA AC 00 00 00 B8 0D 00 18 00 BB 0D 00 00 00 0F 44 D8";

        // Actual clicks have one reachability rejection per assembly-point group.
        private const string EuropeanPlacementRejectPattern =
            "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 B4 FE FF FF";
        private const string MercenaryPlacementRejectPattern =
            "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 A2 FE FF FF";
        private const string EngineerPlacementRejectPattern =
            "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 A5 FE FF FF";
        private const string TunnelerPlacementRejectPattern =
            "85 C9 0F 84 ?? ?? ?? ?? C7 05 ?? ?? ?? ?? 1E 00 00 00 E9";
        private const string KnightPlacementRejectPattern =
            "85 D2 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 89 05";
        private const string BedouinPlacementRejectPattern =
            "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 AB FE FF FF";

        private const int ConstructingFailureStatusRva = 0x9129E;
        private const int EuropeanPlacementRejectRva = 0x929D3;
        private const int MercenaryPlacementRejectRva = 0x928E0;
        private const int EngineerPlacementRejectRva = 0x926FA;
        private const int TunnelerPlacementRejectRva = 0x912E0;
        private const int KnightPlacementRejectRva = 0x913CF;
        private const int BedouinPlacementRejectRva = 0x927ED;

        private static readonly byte[] ThreeNops = { 0x90, 0x90, 0x90 };
        private static readonly byte[] SixNops =
            { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

        private readonly ManualLogSource log;
        private readonly List<NativeCodePatch> patches =
            new List<NativeCodePatch>();
        private bool disposed;

        public AssemblyPointPlacementPatch(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));

            // Resolve and validate every site before changing executable memory.
            patches.Add(CreatePatch(
                memory,
                libraryBase,
                ConstructingFailureStatusPattern,
                ConstructingFailureStatusRva,
                referenceHashMatches,
                patternOffset: 22,
                expectedOpcode: new byte[] { 0x0F, 0x44, 0xD8 },
                replacement: ThreeNops,
                label: "shared preview failure status"));
            AddPlacementPatch(
                memory,
                libraryBase,
                EuropeanPlacementRejectPattern,
                EuropeanPlacementRejectRva,
                referenceHashMatches,
                "European troop placement rejection");
            AddPlacementPatch(
                memory,
                libraryBase,
                MercenaryPlacementRejectPattern,
                MercenaryPlacementRejectRva,
                referenceHashMatches,
                "mercenary troop placement rejection");
            AddPlacementPatch(
                memory,
                libraryBase,
                EngineerPlacementRejectPattern,
                EngineerPlacementRejectRva,
                referenceHashMatches,
                "engineer placement rejection");
            AddPlacementPatch(
                memory,
                libraryBase,
                TunnelerPlacementRejectPattern,
                TunnelerPlacementRejectRva,
                referenceHashMatches,
                "tunneler placement rejection");
            AddPlacementPatch(
                memory,
                libraryBase,
                KnightPlacementRejectPattern,
                KnightPlacementRejectRva,
                referenceHashMatches,
                "knight placement rejection");
            AddPlacementPatch(
                memory,
                libraryBase,
                BedouinPlacementRejectPattern,
                BedouinPlacementRejectRva,
                referenceHashMatches,
                "Bedouin troop placement rejection");

            int appliedCount = 0;
            try
            {
                foreach (NativeCodePatch patch in patches)
                {
                    patch.Apply();
                    appliedCount++;
                    LogPatchState("applied", patch, libraryBase);
                }
            }
            catch
            {
                for (int i = appliedCount - 1; i >= 0; i--)
                    patches[i].Restore();

                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL assembly-point placement byte patch installed; " +
                $"sites={patches.Count}.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                patches[i].Restore();
                LogPatchState("restored", patches[i], patches[i].LibraryBase);
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL assembly-point placement byte patch disposed.");
        }

        private void AddPlacementPatch(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            string label)
        {
            patches.Add(CreatePatch(
                memory,
                libraryBase,
                pattern,
                referenceRva,
                referenceHashMatches,
                patternOffset: 2,
                expectedOpcode: new byte[] { 0x0F, 0x84 },
                replacement: SixNops,
                label: label));
        }

        private void LogPatchState(
            string action,
            NativeCodePatch patch,
            ulong libraryBase)
        {
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL assembly-point placement byte patch {action}: " +
                $"site={patch.Label}, address=0x{patch.Address:X}, " +
                $"rva=0x{patch.Address - libraryBase:X}, " +
                $"original={ToHex(patch.OriginalBytes)}, " +
                $"replacement={ToHex(patch.ReplacementBytes)}.");
        }

        private NativeCodePatch CreatePatch(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            int patternOffset,
            byte[] expectedOpcode,
            byte[] replacement,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                referenceRva,
                referenceHashMatches,
                label,
                log).Rva;
            ulong address = libraryBase + unchecked((ulong)(resolvedRva + patternOffset));
            int memoryOffset = checked((int)(address - libraryBase));
            if (memoryOffset < 0 ||
                memoryOffset + replacement.Length > memory.Length)
            {
                throw new InvalidOperationException(
                    "The native " + label + " patch lies outside the game module.");
            }

            ReadOnlySpan<byte> current =
                memory.Slice(memoryOffset, replacement.Length);
            if (!current.Slice(0, expectedOpcode.Length).SequenceEqual(expectedOpcode))
            {
                throw new InvalidOperationException(
                    "The native " + label + " opcode did not match expectations.");
            }

            return new NativeCodePatch(
                label,
                libraryBase,
                address,
                current.ToArray(),
                replacement);
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        private sealed class NativeCodePatch
        {
            public NativeCodePatch(
                string label,
                ulong libraryBase,
                ulong address,
                byte[] originalBytes,
                byte[] replacementBytes)
            {
                Label = label;
                LibraryBase = libraryBase;
                Address = address;
                OriginalBytes = originalBytes;
                ReplacementBytes = replacementBytes;
            }

            public string Label { get; }
            public ulong LibraryBase { get; }
            public ulong Address { get; }
            public byte[] OriginalBytes { get; }
            public byte[] ReplacementBytes { get; }

            public void Apply()
            {
                VerifyCurrentBytes(OriginalBytes, "apply");
                WriteBytes(ReplacementBytes);
            }

            public void Restore()
            {
                byte[] current = ReadBytes(ReplacementBytes.Length);
                if (current.AsSpan().SequenceEqual(OriginalBytes))
                    return;

                if (!current.AsSpan().SequenceEqual(ReplacementBytes))
                {
                    throw new InvalidOperationException(
                        $"Cannot restore native patch '{Label}' because its bytes " +
                        "were changed by another patch.");
                }

                WriteBytes(OriginalBytes);
            }

            private void VerifyCurrentBytes(byte[] expected, string operation)
            {
                byte[] current = ReadBytes(expected.Length);
                if (!current.AsSpan().SequenceEqual(expected))
                {
                    throw new InvalidOperationException(
                        $"Cannot {operation} native patch '{Label}' because the " +
                        "current bytes do not match the validated bytes.");
                }
            }

            private byte[] ReadBytes(int length)
            {
                byte[] bytes = new byte[length];
                Marshal.Copy(unchecked((IntPtr)(long)Address), bytes, 0, length);
                return bytes;
            }

            private void WriteBytes(byte[] bytes)
            {
                CodePatch.Write(Address, bytes);

                VerifyCurrentBytes(bytes, "verify");
            }
        }
    }
}

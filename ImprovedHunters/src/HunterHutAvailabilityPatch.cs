using BepInEx.Logging;
using RedBird.Core.Memory;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Keeps the Hunter's hut in the build menu on maps that started without deer and goats.
    /// Vanilla's availability check 0xFFC70 hides it there in skirmish and multiplayer. Only the
    /// menu asks this check for the hut, so nothing in the simulation depends on it.
    /// </summary>
    internal sealed class HunterHutAvailabilityPatch : IDisposable
    {
        private const int HutCheckRva = 0xFFDA6;
        private const string HutCheckPattern =
            "83 FA 4E 75 11 45 85 D2 74 1B 83 3D ?? ?? ?? ?? 00 75 12 33 C0 C3";
        private const int SiteOffset = 5;
        private const int DisplacedBytes = 14;
        private const int HasPreyOperandOffset = 12;
        private const int HasPreyInstructionEnd = 17;
        private const int AvailableBranchOperandOffset = 9;

        private readonly PermanentNativeGate gate;

        public HunterHutAvailabilityPatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || imageBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int check = Shared.NativePatternResolver.ResolveUnique(
                memory, HutCheckPattern, HutCheckRva, referenceHashMatches, "Hunter's hut availability", log).Rva;

            // This compare ends at a fixed length, so its RIP base is not the end of the operand.
            ulong hasPrey = imageBase + (ulong)(check + HasPreyInstructionEnd +
                Shared.NativePatternResolver.ReadInt32(memory, check + HasPreyOperandOffset));
            ulong available = imageBase + (ulong)(check + AvailableBranchOperandOffset + 1 +
                (sbyte)memory[check + AvailableBranchOperandOffset]);

            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                candidate.AddSite(memory, check + SiteOffset, DisplacedBytes,
                    (a, i) => HunterNativeStubs.EmitHutAlwaysAvailable(a, i, flag, hasPrey, available));
                candidate.Commit();
                gate = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters Hunter's hut availability installed at 0x{check + SiteOffset:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void SetEnabled(bool enabled) => gate?.SetEnabled(enabled);

        public void Dispose() => gate?.Dispose();
    }
}

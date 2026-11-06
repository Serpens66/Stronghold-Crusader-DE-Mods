using BepInEx.Logging;
using Iced.Intel;
using RedBird.Core.Memory;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace ImprovedHunters
{
    /// <summary>
    /// Vanilla only ever hunts deer and goats, whose update counts the prey reservation
    /// down. Rabbits, camels and chickens never do, so their reservation would stay set
    /// after a Hunter gave up or died. This adds the same countdown to their updates.
    /// </summary>
    internal sealed class PreyReservationHeartbeatPatch : IDisposable
    {
        private const int RabbitUpdateEntryRva = 0x157810;
        private const string RabbitUpdateEntryPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 30 " +
            "4C 63 15 ?? ?? ?? ?? 48 8D 35 ?? ?? ?? ?? FF 05 ?? ?? ?? ?? 41 BF 03 00";
        private const int RabbitCurrentUnitOffset = 27;
        private const int RabbitManagerOffset = 34;

        private const int CamelUpdateEntryRva = 0x158020;
        private const string CamelUpdateEntryPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 " +
            "48 63 3D ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? FF 05 ?? ?? ?? ?? 45 33";
        private const int CamelCurrentUnitOffset = 29;
        private const int CamelManagerOffset = 36;

        private const int ChickenUpdateEntryRva = 0x162CB0;
        private const string ChickenUpdateEntryPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 30 " +
            "48 63 35 ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? 48 69 DE 90 04 00 00 45 33 F6";
        private const int ChickenCurrentUnitOffset = 27;
        private const int ChickenManagerOffset = 34;

        // The two RIP-relative loads directly behind each prologue (three bytes of opcode, then
        // the operand). The function entries themselves belong to the Fixes mod's wildlife hook.
        private const int LoadsToOperand = 3;
        private const int DisplacedBytes = 14;
        private const int RipOperandLength = 4;

        private readonly PermanentNativeGate gate;

        public PreyReservationHeartbeatPatch(
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

            int rabbit = Shared.NativePatternResolver.ResolveUnique(
                memory, RabbitUpdateEntryPattern, RabbitUpdateEntryRva, referenceHashMatches, "rabbit update entry", log).Rva;
            int camel = Shared.NativePatternResolver.ResolveUnique(
                memory, CamelUpdateEntryPattern, CamelUpdateEntryRva, referenceHashMatches, "camel update entry", log).Rva;
            int chicken = Shared.NativePatternResolver.ResolveUnique(
                memory, ChickenUpdateEntryPattern, ChickenUpdateEntryRva, referenceHashMatches, "chicken update entry", log).Rva;

            // All three updates must agree on both globals; that is the independent proof.
            int currentUnit = ReadRipTarget(memory, rabbit + RabbitCurrentUnitOffset);
            int manager = ReadRipTarget(memory, rabbit + RabbitManagerOffset);
            if (currentUnit != ReadRipTarget(memory, camel + CamelCurrentUnitOffset) ||
                currentUnit != ReadRipTarget(memory, chicken + ChickenCurrentUnitOffset) ||
                manager != ReadRipTarget(memory, camel + CamelManagerOffset) ||
                manager != ReadRipTarget(memory, chicken + ChickenManagerOffset))
            {
                throw new InvalidOperationException("The prey updates disagree about the unit manager globals.");
            }

            ulong currentUnitAddress = imageBase + (ulong)currentUnit;
            ulong managerAddress = imageBase + (ulong)manager;
            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                // Each update keeps these two values in its own registers.
                candidate.AddSite(memory, rabbit + RabbitCurrentUnitOffset - LoadsToOperand, DisplacedBytes,
                    (a, i) => HunterNativeStubs.EmitReservationHeartbeat(a, i, flag, currentUnitAddress, managerAddress, r10, rsi));
                candidate.AddSite(memory, camel + CamelCurrentUnitOffset - LoadsToOperand, DisplacedBytes,
                    (a, i) => HunterNativeStubs.EmitReservationHeartbeat(a, i, flag, currentUnitAddress, managerAddress, rdi, rbp));
                candidate.AddSite(memory, chicken + ChickenCurrentUnitOffset - LoadsToOperand, DisplacedBytes,
                    (a, i) => HunterNativeStubs.EmitReservationHeartbeat(a, i, flag, currentUnitAddress, managerAddress, rsi, rbp));
                candidate.Commit();
                gate = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters prey reservation countdown installed: rabbit=0x{rabbit:X}, camel=0x{camel:X}, " +
                $"chicken=0x{chicken:X}, currentUnit=0x{currentUnit:X}, manager=0x{manager:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void SetEnabled(bool enabled) => gate?.SetEnabled(enabled);

        public void Dispose() => gate?.Dispose();

        private static int ReadRipTarget(ReadOnlySpan<byte> memory, int displacementRva) =>
            Shared.NativePatternResolver.ResolveRelativeTarget(memory, displacementRva, displacementRva + RipOperandLength);
    }
}

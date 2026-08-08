using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace RandomEvents
{
    internal sealed class NativeBanditEventSupport
    {
        private const int ReferencePenaltyWriteRva = 0x104C1A;
        private const short VanillaBanditPenaltyState = 16;
        private const string PenaltyWritePattern =
            "48 69 C8 3C 58 00 00 FF C2 B8 10 00 00 00 83 FA 32 41 0F 4D D7 45 33 C0 " +
            "66 42 89 84 31 ? ? ? ? 48 8B CB";

        private readonly ManualLogSource log;
        private int playerStride;
        private int penaltyStateBaseOffset;
        private bool penaltyAvailable;

        public NativeBanditEventSupport(ManualLogSource log)
        {
            this.log = log;
        }

        public void InitializeNative(ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            try
            {
                NativeResolution write = NativePatternResolver.ResolveUnique(
                    memory,
                    PenaltyWritePattern,
                    ReferencePenaltyWriteRva,
                    referenceHashMatches,
                    "Vanilla bandit popularity-state write");

                playerStride = NativePatternResolver.ReadInt32(memory, write.Rva + 3);
                penaltyStateBaseOffset = NativePatternResolver.ReadInt32(memory, write.Rva + 29);
                int exposedResourceStride = Marshal.SizeOf<GamePlayerResources>();
                if (playerStride != exposedResourceStride ||
                    penaltyStateBaseOffset <= 0)
                {
                    throw new InvalidOperationException(
                        $"unexpected layout: nativeStride=0x{playerStride:X}, " +
                        $"resourceStride=0x{exposedResourceStride:X}, stateBaseOffset=0x{penaltyStateBaseOffset:X}.");
                }

                penaltyAvailable = true;
                LogInfo(
                    $"Vanilla bandit popularity penalty ready: strategy={write.Strategy}, " +
                    $"writeRva=0x{write.Rva:X}, playerStride=0x{playerStride:X}, " +
                    $"stateBaseOffset=0x{penaltyStateBaseOffset:X}, state={VanillaBanditPenaltyState}.");
            }
            catch (Exception ex)
            {
                penaltyAvailable = false;
                LogError(
                    "Vanilla bandit popularity penalty is disabled; manual bandit spawning remains available: " + ex);
            }
        }

        public unsafe bool TryApplyPopularityPenalty(int targetPlayerId, out string detail)
        {
            detail = string.Empty;
            if (!penaltyAvailable)
            {
                detail = "native Vanilla bandit popularity-state write is unavailable.";
                return false;
            }

            try
            {
                if (targetPlayerId < 1 || targetPlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                {
                    detail = $"target player ID {targetPlayerId} is outside Vanilla's player range.";
                    return false;
                }

                IntPtr playerManager = GamePlayerManagerAPI.Instance.GetPlayerManager();
                if (playerManager == IntPtr.Zero)
                {
                    detail = "native player manager is unavailable.";
                    return false;
                }

                int playerOffset = checked(penaltyStateBaseOffset + targetPlayerId * playerStride);
                IntPtr stateAddress = IntPtr.Add(playerManager, playerOffset);
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                        targetPlayerId,
                        out GamePlayerResources* resources) ||
                    resources == null)
                {
                    detail = $"player resources for target {targetPlayerId} are unavailable.";
                    return false;
                }

                long fieldOffset = stateAddress.ToInt64() - (long)resources;
                if (fieldOffset < 0 || fieldOffset > playerStride - sizeof(short))
                {
                    throw new InvalidOperationException(
                        $"resolved state field offset 0x{fieldOffset:X} is outside player resources.");
                }

                Marshal.WriteInt16(stateAddress, VanillaBanditPenaltyState);
                short actual = Marshal.ReadInt16(stateAddress);
                if (actual != VanillaBanditPenaltyState)
                {
                    detail = $"Vanilla state verification returned {actual} instead of {VanillaBanditPenaltyState}.";
                    return false;
                }

                detail =
                    $"targetPlayerId={targetPlayerId}, state={actual}, " +
                    $"resourceFieldOffset=0x{fieldOffset:X}.";
                return true;
            }
            catch (Exception ex)
            {
                penaltyAvailable = false;
                detail = $"native Vanilla bandit popularity-state write failed ({ex.Message}).";
                LogError(
                    "Vanilla bandit popularity penalty was disabled after a runtime failure; " +
                    "manual bandit spawning remains available: " + ex);
                return false;
            }
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}

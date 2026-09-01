using BepInEx.Logging;
using SerpNativeAPI;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace APITest
{
    internal sealed unsafe class AssassinClimbCancellationTest
    {
        private const int SelectionBitmapWordCount = 625;
        private const int UnitIdBitsPerWord = 16;
        private const int ThrowingHookState = 126;
        private const int ClimbingDownState = 129;
        private const ushort NormalMovementState = 101;
        private const ushort StoppedMovementState = 8;
        private const int AssassinClimbVisualActiveOffset = 0x40F;
        private const int AssassinClimbProgressOffset = 0x414;
        private const int AssassinFacingOffset = 0x416;
        private const ushort CompletedClimbFacing = 0x20;

        private readonly ManualLogSource log;
        private bool firstStopLogged;
        private bool invalidTribeLogged;
        private long stopCallbacks;
        private long selectedUnitsInspected;
        private long climbingAssassinsCompleted;

        public AssassinClimbCancellationTest(ManualLogSource log) =>
            this.log = log ?? throw new ArgumentNullException(nameof(log));

        public void OnSelectedUnitCommand(SelectedUnitCommandContext context)
        {
            if ((uint)context.Command != (uint)TribeAICommand.UnitStop)
                return;
            stopCallbacks++;
            if (!firstStopLogged)
            {
                firstStopLogged = true;
                Log($"ASSASSIN_HOOK_CONFIRMED: stopCallbacks={stopCallbacks}, tribeId={context.TribeId}.");
            }

            try
            {
                CancelClimbingAssassins(context.TribeId);
            }
            catch (Exception ex)
            {
                Log($"ASSASSIN_CALLBACK_ERROR: tribeId={context.TribeId}, stopCallbacks={stopCallbacks}, error={ex}");
            }
        }

        private void CancelClimbingAssassins(int tribeId)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            if (!tribeApi.IsValidId(tribeId) ||
                !tribeApi.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
            {
                if (!invalidTribeLogged)
                {
                    invalidTribeLogged = true;
                    Log($"ASSASSIN_INVALID_TRIBE: tribeId={tribeId}; Vanilla continues unchanged.");
                }
                return;
            }

            ushort* bitmap = &tribe->r_UnitIdsInGroupBitfield;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int wordIndex = 0; wordIndex < SelectionBitmapWordCount; wordIndex++)
            {
                ushort word = bitmap[wordIndex];
                if (word == 0)
                    continue;
                for (int bitIndex = 0; bitIndex < UnitIdBitsPerWord; bitIndex++)
                {
                    if ((word & (1 << bitIndex)) == 0)
                        continue;
                    int localUnitId = wordIndex * UnitIdBitsPerWord + bitIndex;
                    if (localUnitId <= 0 || !unitApi.TryGetUnitById(localUnitId, out GameUnit* unit) || unit == null)
                        continue;
                    selectedUnitsInspected++;
                    if (unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                        unit->r_AIState < ThrowingHookState || unit->r_AIState > ClimbingDownState)
                    {
                        continue;
                    }

                    ApplyCancellation(unit);
                    climbingAssassinsCompleted++;
                    Log($"ASSASSIN_CLIMB_COMPLETED: tribeId={tribeId}, unitId={localUnitId}, stopCallbacks={stopCallbacks}, inspected={selectedUnitsInspected}, completed={climbingAssassinsCompleted}.");
                }
            }
        }

        private static void ApplyCancellation(GameUnit* unit)
        {
            // Preserve Vanilla's current tile and occupancy registration; only complete the
            // same transition fields that Vanilla clears at the end of climbing states.
            unit->r_MovingRelevant = StoppedMovementState;
            unit->r_AIState = NormalMovementState;
            unit->r_AnimationTimer = 0;
            unit->r_CurrentSpriteAnimationFrame = 0;
            unit->N00000061 = 0;
            byte* raw = (byte*)unit;
            *(raw + AssassinClimbVisualActiveOffset) = 0;
            *(ushort*)(raw + AssassinClimbProgressOffset) = 0;
            *(ushort*)(raw + AssassinFacingOffset) = CompletedClimbFacing;
        }

        private void Log(string message) =>
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}

// Feature: Cancels active Assassin climbing through Vanilla's synchronized Stop command.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AssassinClimbCancellationRuntime : IDisposable
    {
        private const int SelectionBitmapWordCount = 625;
        private const int UnitIdBitsPerWord = 16;
        private const ushort NormalMovementState = 101;
        private const ushort StoppedMovementState = 8;
        private const int AssassinClimbVisualActiveOffset = 0x40F;
        private const int AssassinClimbProgressOffset = 0x414;
        private const int AssassinFacingOffset = 0x416;
        private const ushort CompletedClimbFacing = 0x20;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private IDisposable selectedUnitCommandSubscription;
        private bool fixedUnitLayoutValidated;
        private bool invalidTribeLogged;

        public AssassinClimbCancellationRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (selectedUnitCommandSubscription != null)
                return;
            selectedUnitCommandSubscription =
                TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(OnSelectedUnitCommand);
            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Assassin climb cancellation subscribed to the Script Extender selected-unit Pre event.");
        }

        public void SetFixedUnitLayoutValidated(bool value)
        {
            fixedUnitLayoutValidated = value;
        }

        public void Dispose()
        {
            selectedUnitCommandSubscription?.Dispose();
            selectedUnitCommandSubscription = null;
            fixedUnitLayoutValidated = false;
        }

        private void OnSelectedUnitCommand(TribeIssueOrderWithTargetEventArgs args)
        {
            // Vanilla ignores Stop in states 126-129. Complete only that transition during
            // the Script Extender's Pre phase; its shared hook then runs Vanilla unchanged.
            if (AssassinClimbCancellationPolicy.ShouldHandleCommand(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    selectedUnitCommandSubscription != null,
                    fixedUnitLayoutValidated,
                    args.Phase,
                    (uint)args.AICommand))
            {
                try
                {
                    CancelClimbingAssassins(args.TribeId);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL Assassin climb cancellation failed before Vanilla Stop: {ex}");
                }
            }
        }

        private void CancelClimbingAssassins(int tribeId)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            if (!tribeApi.IsValidId(tribeId) ||
                !tribeApi.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
            {
                LogInvalidTribeOnce(tribeId);
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
                    if (unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                        !AssassinClimbCancellationPolicy.IsClimbingState(unit->r_AIState))
                        continue;

                    ApplyCancellation(unit);
                }
            }
        }

        private static void ApplyCancellation(GameUnit* unit)
        {
            // Keep Vanilla's current tile and occupancy registration together. During state 129
            // Current may already be the lower tile; teleporting it back desynchronizes the tile list.
            unit->r_MovingRelevant = StoppedMovementState;
            unit->r_AIState = NormalMovementState;
            unit->r_AnimationTimer = 0;
            unit->r_CurrentSpriteAnimationFrame = 0;
            unit->N00000061 = 0;

            byte* raw = (byte*)unit;
            // These are the exact fields Vanilla clears when states 127/129 finish naturally.
            *(raw + AssassinClimbVisualActiveOffset) = 0;
            *(ushort*)(raw + AssassinClimbProgressOffset) = 0;
            *(ushort*)(raw + AssassinFacingOffset) = CompletedClimbFacing;
        }

        private void LogInvalidTribeOnce(int tribeId)
        {
            if (invalidTribeLogged)
                return;
            invalidTribeLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Bugfixes and QoL could not safely resolve Assassin Stop tribe {tribeId}; Vanilla behavior remains active.");
        }

    }
}

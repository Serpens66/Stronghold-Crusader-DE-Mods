// Feature: Stop-command diagnostics for Assassin climbing without polling or position history.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace ExtraFeatures
{
    internal sealed unsafe class AssassinClimbCancellationRuntime : IDisposable
    {
        private const int TribeBitmapWordCount = 625;
        private const int UnitIdBitsPerWord = 16;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private IDisposable orderSubscription;
        private bool fixedLayoutValidated;
        private bool invalidTribeLogged;

        public AssassinClimbCancellationRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize(bool isFixedLayoutValidated)
        {
            fixedLayoutValidated = isFixedLayoutValidated;
            if (!fixedLayoutValidated || orderSubscription != null)
                return;

            orderSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTribeOrder);
            Shared.DebugLogHelper.LogInfo(
                log,
                "Extra Features Assassin climb-stop diagnostics installed on Vanilla's synchronized UnitStop path.");
        }

        public void Dispose()
        {
            orderSubscription?.Dispose();
            orderSubscription = null;
        }

        private void OnTribeOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (args == null || !AssassinClimbCancellationPolicy.ShouldInspectOrder(
                    settings.EnableMod,
                    settings.EnableImprovedAssassinPathfinding,
                    fixedLayoutValidated,
                    (uint)args.AICommand,
                    args.a6))
            {
                return;
            }

            try
            {
                if (!GameTribeManagerAPI.Instance.TryGetTribeById(args.TribeId, out GameTribe* tribe) || tribe == null)
                {
                    if (!invalidTribeLogged)
                    {
                        invalidTribeLogged = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Extra Features could not inspect Assassin climb-stop positions because tribe {args.TribeId} is invalid.");
                    }
                    return;
                }

                int inspectedMembers = 0;
                int climbingAssassins = 0;
                ushort* bitmap = &tribe->r_UnitIdsInGroupBitfield;
                int expectedMembers = tribe->r_UnitsInGroup;
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

                for (int wordIndex = 0; wordIndex < TribeBitmapWordCount && inspectedMembers < expectedMembers; wordIndex++)
                {
                    ushort word = bitmap[wordIndex];
                    if (word == 0)
                        continue;

                    for (int bitIndex = 0; bitIndex < UnitIdBitsPerWord && inspectedMembers < expectedMembers; bitIndex++)
                    {
                        if ((word & (1 << bitIndex)) == 0)
                            continue;

                        inspectedMembers++;
                        int unitId = wordIndex * UnitIdBitsPerWord + bitIndex;
                        if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                            unit == null || unit->r_AliveState != AliveState.IsAlive ||
                            unit->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                            !AssassinClimbCancellationPolicy.IsClimbingState(unit->r_AIState))
                        {
                            continue;
                        }

                        climbingAssassins++;
                        LogSnapshot(args.TribeId, unitId, unit);
                    }
                }

                if (climbingAssassins > 0)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Extra Features Assassin climb-stop diagnostic summary: tribeId={args.TribeId}, " +
                        $"members={inspectedMembers}/{expectedMembers}, climbingAssassins={climbingAssassins}; no state was changed.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features Assassin climb-stop diagnostics failed without changing Vanilla behavior: {ex}");
            }
        }

        private void LogSnapshot(int tribeId, int unitId, GameUnit* unit)
        {
            byte* raw = (byte*)unit;
            byte ownerOfScaledObject = *(raw + 0x412);
            byte hasAssignedTarget = *(raw + 0x413);
            short heightDifference = *(short*)(raw + 0x414);
            ushort previousFacing = *(ushort*)(raw + 0x416);
            short decayCounter = *(short*)(raw + 0x418);
            ushort assassinTimer = *(ushort*)(raw + 0x41A);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features Assassin climb-stop snapshot: tribeId={tribeId}, unitId={unitId}, " +
                $"globalId={unit->r_GlobalId}, state={unit->r_AIState}, " +
                $"current={FormatTile(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY, unit->r_CurrentPositionTileId)}, " +
                $"previous={FormatTile(unit->r_PreviousTilePositionX, unit->r_PreviousTilePositionY, unit->r_PreviousPositionTileId)}, " +
                $"next={FormatTile(unit->r_NextTilePositionX2, unit->r_NextTilePositionY2, unit->r_NextPositionTileId2)}, " +
                $"target={FormatTile(unit->r_TargetTilePositionX, unit->r_TargetTilePositionY, unit->r_TargetPositionTileId)}, " +
                $"world=({unit->r_CurrentWorldPositionX},{unit->r_CurrentWorldPositionY}), elevation={unit->r_HeightElevation}, " +
                $"animationTimer={unit->r_AnimationTimer}, animationFrame={unit->r_CurrentSpriteAnimationFrame}, " +
                $"animationField70={unit->N00000061}, scaledObjectOwner={ownerOfScaledObject}, assignedTarget={hasAssignedTarget}, " +
                $"heightDifference={heightDifference}, previousFacing={previousFacing}, decayCounter={decayCounter}, assassinTimer={assassinTimer}.");
        }

        private static string FormatTile(ushort x, ushort y, uint storedTileId)
        {
            int derivedTileId = -1;
            int height = -1;
            if (x < GameTileManagerAPI.MAX_WIDTH && y < GameTileManagerAPI.MAX_HEIGHT)
            {
                derivedTileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (derivedTileId >= 0 && derivedTileId < AssassinClimbCancellationPolicy.TileCount)
                    height = GameTileManagerAPI.Instance.GetTileHeight(derivedTileId);
            }

            int storedHeight = AssassinClimbCancellationPolicy.IsValidTileId(storedTileId)
                ? GameTileManagerAPI.Instance.GetTileHeight((int)storedTileId)
                : -1;
            return $"({x},{y};storedId={storedTileId};derivedId={derivedTileId};storedHeight={storedHeight};derivedHeight={height})";
        }
    }
}

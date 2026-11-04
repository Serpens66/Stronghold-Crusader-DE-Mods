// Feature: AI defense repair, enemy-proximity adapter: classifies proximity queries as human placement,
// AI wall repair or AIV rebuild and replaces only configured category radii.
using APIShared.Buildings;
using SHCDESE.API;
using SHCDESE.EventAPI.AI;
using SHCDESE.Interop.Enums;
using System;

namespace ExtraFeatures
{
    internal sealed partial class AIDefenseRepairRuntime
    {
        private EnemyProximityDecision OnRepairProximity(EnemyProximityContext args)
        {
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || !mapActive)
                return EnemyProximityDecision.None;

            try
            {
                bool isAi = IsAI(args.PlayerId);
                if (!isAi)
                {
                    return ApplyHumanPlacementProximity(args);
                }

                // AI wall repair is not a GameBuilding repair. ExecuteBuildStep calls the
                // dedicated wall writer at RVA 0x6CB20, whose validator RVA 0x77CF0 asks
                // this helper with radius 5 immediately before restoring Height/DamageGrid.
                // Bind only that synchronous, coordinate-identical call. This preserves all
                // first placements while avoiding the old tower-spawn-history heuristic.
                bool isWallRepair = pendingWallRepair.Matches(
                    args.PlayerId, args.TileX, args.TileY, args.OriginalRadius);
                if (pendingWallRepair.IsSet)
                    pendingWallRepair = default;
                if (isWallRepair)
                {
                    if (!TryGetConfiguredAIRadii(out int wallCivilian, out int wallMilitary))
                        return EnemyProximityDecision.None;

                    return CategoryProximityOverride(wallCivilian, wallMilitary);
                }

                BuildStepContext context = activeContext;
                if (context != null && context.PlayerId == args.PlayerId && context.DelayBlocked)
                {
                    // The measured ExecuteBuildStep path asks this native question after the
                    // placement helper and before spawning. Returning Vanilla's blocked value
                    // leaves the frame scheduler intact, so other frame targets still run.
                    return EnemyProximityDecision.Blocked;
                }

                // 0xEE640 is a general placement helper, not a standing-building repair API.
                // Calls outside the explicitly classified wall/rebuild contexts remain Vanilla.
                if (context == null || context.History == null ||
                    !context.History.EverSpawnedDefense ||
                    !TryGetConfiguredAIRadii(out int rebuildCivilian, out int rebuildMilitary))
                    return EnemyProximityDecision.None;

                // Live AI calls supplied context-specific Vanilla radii 3, 5 and 15. A custom
                // mode value replaces them only after the call was safely classified as rebuild.
                return CategoryProximityOverride(rebuildCivilian, rebuildMilitary);
            }
            catch (Exception ex)
            {
                LogFailure("repair proximity", ex);
                return EnemyProximityDecision.None;
            }
        }

        private void OnAIBuildWall(AIBuildWallEventArgs args)
        {
            pendingWallRepair = default;
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) || !mapActive || !TryGetConfiguredAIRadii(out _, out _) ||
                !IsAI(args.PlayerId))
            {
                return;
            }

            try
            {
                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                int tileId = tileApi.GetTileId(args.TileX, args.TileY);
                if (!tileApi.IsValidTileId(tileId) ||
                    !tileApi.HasTilePropertyFlag(tileId, TilePropertyFlag.IsWall))
                {
                    return;
                }

                // IsWall is the stable distinction we need: a first placement has no wall bit,
                // while Vanilla may route several damaged/breached wall states through different
                // 0x77CF0 branches before they converge on the radius-5 query. Do not duplicate
                // those evolving health/state rules here. If Vanilla rejects earlier, no matching
                // proximity callback occurs and this one-shot context cannot change game state.
                pendingWallRepair = new PendingWallRepair(args.PlayerId, args.TileX, args.TileY);
            }
            catch (Exception ex)
            {
                pendingWallRepair = default;
                LogFailure("AI damaged-wall repair classification", ex);
            }
        }

        private EnemyProximityDecision ApplyHumanPlacementProximity(EnemyProximityContext args)
        {
            if (args.PlayerId < 1 || args.PlayerId > GamePlayerManagerAPI.MAX_PLAYERS || !gameModeKnown)
                return EnemyProximityDecision.None;

            if (!EnemyProximityPolicy.IsOrdinaryHumanRadius(args.OriginalRadius)) return EnemyProximityDecision.None;
            return CategoryProximityOverride(
                EnemyProximityPolicy.SelectConfiguredRadius(realMultiplayer,
                    settings.HumanCivilianEnemyProximitySingleplayer, settings.HumanCivilianEnemyProximityMultiplayer),
                EnemyProximityPolicy.SelectConfiguredRadius(realMultiplayer,
                    settings.HumanMilitaryEnemyProximitySingleplayer, settings.HumanMilitaryEnemyProximityMultiplayer));
        }

        private bool TryGetConfiguredAIRadii(out int civilian, out int military)
        {
            civilian = military = -1;
            if (!gameModeKnown) return false;
            civilian = EnemyProximityPolicy.SelectConfiguredRadius(realMultiplayer,
                settings.AICivilianEnemyProximitySingleplayer, settings.AICivilianEnemyProximityMultiplayer);
            military = EnemyProximityPolicy.SelectConfiguredRadius(realMultiplayer,
                settings.AIMilitaryEnemyProximitySingleplayer, settings.AIMilitaryEnemyProximityMultiplayer);
            return civilian >= 0 || military >= 0;
        }

        // A Vanilla-mode category contributes nothing (null), as the Override contract requires: only
        // configured categories are replaced, so earlier participants keep theirs.
        private static EnemyProximityDecision CategoryProximityOverride(int civilian, int military)
        {
            if (civilian < 0 && military < 0) return EnemyProximityDecision.None;
            return new EnemyProximityDecision(
                civilianRadius: EnemyProximityPolicy.ConfiguredContribution(civilian),
                militaryRadius: EnemyProximityPolicy.ConfiguredContribution(military));
        }

        private readonly struct PendingWallRepair
        {
            internal PendingWallRepair(int playerId, int x, int y)
            {
                PlayerId = playerId;
                X = x;
                Y = y;
                IsSet = true;
            }

            internal int PlayerId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal bool IsSet { get; }

            internal bool Matches(int playerId, int x, int y, int vanillaRadius) =>
                IsSet && PlayerId == playerId && X == x && Y == y && vanillaRadius == 5;
        }
    }
}

using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.API.Components.Timer;
using SHCDESE.Interop;
using System;

namespace MPTest
{
    internal static unsafe class UnitSpawnDiagnostics
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        public static void Log(
            ManualLogSource log,
            int unitId,
            string source,
            WoodcutterSwordsmanSpawnPacket packet,
            int spawnHeight)
        {
            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"MPTest spawn diagnostics could not resolve unitId={unitId}.");
                    return;
                }

                GameTimeStamp timeStamp = GameTimeManagerAPI.Instance.CaptureTimeStamp();
                int mapTicks = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
                int? deterministicSeed = DeterministicRandom.GetCurrentSeed();
                ulong structHash = ComputeStructHash(unit);

                string context =
                    $"source={source}, playerId={packet.SourcePlayerId}, requestId={packet.RequestId}, unitId={unitId}";

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MPTest spawn snapshot timing: {context}, gameTick={timeStamp.CapturedGameTick}, " +
                    $"gameTimeUnits={timeStamp.CapturedGameTimeUnits}, elapsedMapTicks={mapTicks}, " +
                    $"deterministicSeed={deterministicSeed?.ToString() ?? "null"}, " +
                    $"structSize={sizeof(GameUnit)}, structFnv1a64=0x{structHash:X16}.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MPTest spawn snapshot state: {context}, globalId={unit->r_GlobalId}, " +
                    $"aliveState={unit->r_AliveState}, chimp={unit->r_UnitChimp}, " +
                    $"owner={unit->r_ControllableForPlayerId}, color={unit->r_SpritePlayerColorId}, " +
                    $"spawnedForPlayerIndex={unit->r_SpawnedForPlayerIndex}, linkedProductionBuildingId={unit->r_LinkedProductionBuildingId}, " +
                    $"health={unit->r_CurrentHealth}/{unit->r_MaxHealth}, healthPercent={unit->r_CurrentHealthPercentage}, " +
                    $"ticksAlive={unit->r_TicksAlive1}/{unit->r_TicksAlive2}, aliveTicks={unit->r_AliveTicks1}/{unit->r_AliveTicks2}.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MPTest spawn snapshot visual: {context}, material={unit->r_GameMaterialIndex}, " +
                    $"animationFrame={unit->r_AnimationFrame}, spriteAnimationFrame={unit->r_CurrentSpriteAnimationFrame}, " +
                    $"animationTimer={unit->r_AnimationTimer}, direction={unit->r_Direction}, " +
                    $"attackFacing={unit->r_AttackTargetFacing}, invisible={unit->r_IsInvisible}.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MPTest spawn snapshot position: {context}, requestedTile={packet.TargetTileX},{packet.TargetTileY},{spawnHeight}, " +
                    $"world={unit->r_CurrentWorldPositionX},{unit->r_CurrentWorldPositionY},{unit->r_HeightElevation}, " +
                    $"tile={unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}, " +
                    $"target={unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}, " +
                    $"previous={unit->r_PreviousTilePositionX},{unit->r_PreviousTilePositionY}, " +
                    $"tileIds={unit->r_CurrentPositionTileId}/{unit->r_TargetPositionTileId}/{unit->r_PreviousPositionTileId}, " +
                    $"next={unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2},{unit->r_NextPositionTileId2}, " +
                    $"target2={unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2}, " +
                    $"pathFlags={unit->r_PathPlanStateBitFlags}, moving={unit->r_MovingRelevant}, " +
                    $"pathPosition={unit->p_CurrentPathPlanPosition}, pathSize={unit->p_PathPlanSize}, " +
                    $"aiState={unit->r_AIState}, speed={unit->r_CurrentSpeed}/{unit->r_CurrentSpeed2}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"MPTest spawn diagnostics failed: {ex}");
            }
        }

        private static ulong ComputeStructHash(GameUnit* unit)
        {
            byte* bytes = (byte*)unit;
            ulong hash = FnvOffsetBasis;
            for (int index = 0; index < sizeof(GameUnit); index++)
            {
                hash ^= bytes[index];
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}

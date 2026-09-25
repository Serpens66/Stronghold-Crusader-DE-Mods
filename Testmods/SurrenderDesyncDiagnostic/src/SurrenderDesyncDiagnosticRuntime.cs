using APIShared;
using BepInEx.Logging;
using BugfixesAndQoL;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SurrenderDesyncDiagnostic
{
    internal sealed unsafe class SurrenderDesyncDiagnosticRuntime
    {
        private const int HistoryCapacity = 128;
        private const int CaptureAfterDeathTicks = 200;
        private const int HashChunkSize = 256;
        private readonly ManualLogSource log;
        private readonly Queue<string> stateHistory = new Queue<string>(HistoryCapacity);
        private readonly ResyncDiagnosticHistory choreHistory = new ResyncDiagnosticHistory();
        private readonly int[] lastKnownLordUnitIds = new int[9];
        private long generation;
        private int captureUntilTick = -1;
        private int lastObservedTick = -1;
        private int lastSurrenderTick = -1;
        private string lastSurrender = "none";
        private string lastSpectator = "none";
        private bool postCleanupMarkerLogged;
        private bool resyncDumped;

        internal SurrenderDesyncDiagnosticRuntime(ManualLogSource logger)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal void Initialize()
        {
            SurrenderDiagnosticBridge.SurrenderExecuted += OnSurrender;
            SurrenderDiagnosticBridge.SpectatorExecuted += OnSpectator;
            SurrenderDiagnosticBridge.ChoresSent += OnChores;
            SurrenderDiagnosticBridge.ResyncStateChanged += OnResyncState;
            GameTimeManagerAPI.Instance.OnTick += OnTick;

            if (ApiShared.Current.TryGetPlayerDefeat(
                    SurrenderDesyncDiagnosticPlugin.PluginGuid,
                    out IPlayerDefeatCapability capability,
                    out NativeCapabilityDiagnostic diagnostic))
            {
                if (!capability.TryRegisterObserver("lord-death-diagnostic", OnLordDied,
                        OnPlayerDefeated, out diagnostic))
                    Log("PLAYER_DEFEAT_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            else
            {
                Log("PLAYER_DEFEAT_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            Log("READY: event subscribers installed; awaiting post-startup simulation tick.");
        }

        private void OnTick(int simulationTick)
        {
            try
            {
                if (lastObservedTick >= 0 && simulationTick < lastObservedTick)
                    ResetMapDiagnostics();
                lastObservedTick = simulationTick;
                if (!postCleanupMarkerLogged)
                {
                    postCleanupMarkerLogged = true;
                    Log("POST_STARTUP_RUNTIME_TICK: simulationTick=" + simulationTick);
                }

                bool detailed = simulationTick <= captureUntilTick;
                string snapshot = CaptureState(simulationTick, detailed);
                if (stateHistory.Count == HistoryCapacity)
                    stateHistory.Dequeue();
                stateHistory.Enqueue(snapshot);
                if (detailed)
                    Log("STATE: " + snapshot);

                if (lastSurrenderTick >= 0)
                {
                    foreach (int offset in choreHistory.TakeDueCheckpointOffsets(simulationTick))
                        Log($"RESYNC_CHECKPOINT: surrenderTick={lastSurrenderTick},offset={offset}," + snapshot);
                }
            }
            catch (Exception ex) { Log("TICK_CAPTURE_ERROR: " + ex); }
        }

        private void ResetMapDiagnostics()
        {
            stateHistory.Clear();
            choreHistory.Reset();
            Array.Clear(lastKnownLordUnitIds, 0, lastKnownLordUnitIds.Length);
            captureUntilTick = -1;
            lastSurrenderTick = -1;
            lastSurrender = "none";
            lastSpectator = "none";
            resyncDumped = false;
            Log("MAP_DIAGNOSTICS_RESET: simulation tick restarted.");
        }

        private void OnSurrender(int playerId, int unitId, int globalId, int tick)
        {
            try
            {
                lastSurrenderTick = tick;
                lastSurrender = $"player={playerId},unit={unitId},global={globalId},tick={tick}";
                Arm("SURRENDER", tick);
            }
            catch (Exception ex) { Log("SURRENDER_CAPTURE_ERROR: " + ex); }
        }

        private void OnSpectator(long sessionId, int playerId, int deathTick, int executionTick, int localPlayerId)
        {
            lastSpectator = $"session={sessionId},player={playerId},deathTick={deathTick}," +
                            $"executionTick={executionTick},localPlayer={localPlayerId}";
            Log("SPECTATOR_CHORE: " + lastSpectator);
        }

        private void OnLordDied(PlayerLordDeathNotification notification)
        {
            if (notification == null)
                return;
            try
            {
                Log($"LORD_DEATH: session={notification.SessionId},player={notification.PlayerId}," +
                    $"unit={notification.LordUnitId},global={notification.LordGlobalId}," +
                    $"observerTick={notification.SimulationTick},lastSurrender=[{lastSurrender}]");
                if (lastSurrenderTick < 0 || Math.Abs(notification.SimulationTick - lastSurrenderTick) > 4)
                    Arm("NATURAL_LORD_DEATH", notification.SimulationTick);
            }
            catch (Exception ex) { Log("LORD_DEATH_CAPTURE_ERROR: " + ex); }
        }

        private void OnPlayerDefeated(PlayerDefeatNotification notification)
        {
            if (notification != null)
                Log($"OFFICIAL_DEFEAT: session={notification.SessionId},player={notification.PlayerId}," +
                    $"observerTick={notification.SimulationTick}");
        }

        private void Arm(string reason, int tick)
        {
            captureUntilTick = Math.Max(captureUntilTick, tick + CaptureAfterDeathTicks);
            generation++;
            resyncDumped = false;
            choreHistory.ObserveAnchor(generation, tick);
            Log($"CAPTURE_ARMED: reason={reason},tick={tick},throughTick={captureUntilTick}," +
                $"lastSurrender=[{lastSurrender}],lastSpectator=[{lastSpectator}]");
            foreach (string snapshot in stateHistory)
                Log("STATE_HISTORY: " + snapshot);
            Log("STATE_IMMEDIATE: " + CaptureState(tick, detailed: true));
        }

        private void OnChores(byte[] buffer)
        {
            try
            {
                int tick = GameTimeManagerAPI.Instance?.GetElapsedMapTicks() ?? -1;
                string description = ResyncDiagnosticHistory.DescribeBuffer(
                    buffer, tick, out bool containsStart, out bool containsEnd);
                choreHistory.AddBuffer(buffer, tick, out _, out _);
                if (tick <= captureUntilTick)
                    Log("CHORE_OUTGOING: " + description);
                if (containsStart || containsEnd)
                    Log("RESYNC_CHORE_OUTGOING: " + description);
                if (containsStart && !resyncDumped)
                {
                    resyncDumped = true;
                    DumpResync(tick);
                }
            }
            catch (Exception ex) { Log("CHORE_CAPTURE_ERROR: " + ex); }
        }

        private void OnResyncState(bool previous, bool current, int tick, int section, int layer)
        {
            try
            {
                Log($"RESYNC_STATE_CHANGED: previous={previous},current={current}," +
                    $"tick={tick},section={section},layer={layer}," +
                    $"lastSurrender=[{lastSurrender}],lastSpectator=[{lastSpectator}]");
                if (current && !resyncDumped)
                {
                    resyncDumped = true;
                    DumpResync(tick);
                }
            }
            catch (Exception ex) { Log("RESYNC_CAPTURE_ERROR: " + ex); }
        }

        private void DumpResync(int tick)
        {
            Log("RESYNC_DIAGNOSTIC_DUMP_BEGIN: tick=" + tick);
            foreach (string entry in choreHistory.GetBuffers())
                Log("RESYNC_CHORE_HISTORY: " + entry);
            foreach (string entry in stateHistory)
                Log("RESYNC_STATE_HISTORY: " + entry);
            Log("RESYNC_DIAGNOSTIC_DUMP_END");
        }

        private string CaptureState(int tick, bool detailed)
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            var playerRows = new List<string>(8);
            var formerLordRows = new List<string>(8);
            for (int playerId = 1; playerId <= 8; playerId++)
            {
                if (players == null || !players.TryGetPlayerResourcesById(playerId,
                        out GamePlayerResources* resources) || resources == null)
                {
                    playerRows.Add(playerId + ":missing");
                    continue;
                }
                int lordUnitId = checked((int)resources->r_LordUnitId);
                if (lordUnitId > 0)
                    lastKnownLordUnitIds[playerId] = lordUnitId;
                int observedLordUnitId = lordUnitId > 0
                    ? lordUnitId : lastKnownLordUnitIds[playerId];
                string lord = "missing";
                if (lordUnitId > 0 && unitApi != null &&
                    unitApi.TryGetUnitById(lordUnitId, out GameUnit* unit) && unit != null)
                {
                    lord = $"global={unit->r_GlobalId},owner={unit->r_ControllableForPlayerId}," +
                           $"alive={(int)unit->r_AliveState},hp={unit->r_CurrentHealth}," +
                           $"maxHp={unit->r_MaxHealth},hpPct={unit->r_CurrentHealthPercentage}," +
                           $"bar={unit->r_HealthBarBlocks},type={(int)unit->r_UnitChimp}," +
                           $"x={unit->r_CurrentWorldPositionX},y={unit->r_CurrentWorldPositionY}";
                }
                if (lordUnitId == 0 && observedLordUnitId > 0 && unitApi != null &&
                    unitApi.TryGetUnitById(observedLordUnitId, out GameUnit* former) && former != null)
                {
                    formerLordRows.Add($"{playerId}:unit={observedLordUnitId},global={former->r_GlobalId}," +
                        $"alive={(int)former->r_AliveState},hp={former->r_CurrentHealth}," +
                        $"maxHp={former->r_MaxHealth},owner={former->r_ControllableForPlayerId}");
                }
                playerRows.Add($"{playerId}:win={(int)resources->r_WinLossState},paused={resources->r_IsPaused}," +
                    $"population={resources->r_TotalPopulation},gold={resources->r_TotalGoodsGold}," +
                    $"lordId={lordUnitId}," +
                    $"storedGlobal={resources->r_LordUnitGlobalId},[{lord}]");
            }
            string playersState = string.Join(";", playerRows);
            string units = detailed ? HashUnits() : "deferred";
            string buildings = detailed ? HashBuildings() : "deferred";
            string peer = $"players={ResyncDiagnosticHistory.ComputeSha256(playersState)}," +
                          $"units={units},buildings={buildings}";
            GameData gameData = GameData.Instance;
            string localUi = $"localPlayer={players?.GetLocalPlayerId() ?? -1}," +
                $"spectator={gameData?.lastGameState?.spectatorMode ?? -1}";
            return $"tick={tick},peerSha256={ResyncDiagnosticHistory.ComputeSha256(peer)}," +
                   $"{peer},playerState=[{playersState}],formerLord=[{string.Join(";", formerLordRows)}]," +
                   $"localUi=[{localUi}]";
        }

        private static string HashUnits()
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            var hashes = new List<string>();
            var row = new StringBuilder(HashChunkSize * 35);
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                if (spanIndex % HashChunkSize == 0)
                    row.Clear();
                ref GameUnit unit = ref units[spanIndex];
                row.Append(spanIndex).Append(':').Append(unit.r_GlobalId).Append(',')
                   .Append((int)unit.r_AliveState).Append(',').Append((int)unit.r_UnitChimp)
                   .Append(',').Append(unit.r_ControllableForPlayerId).Append(',')
                   .Append(unit.r_CurrentHealth).Append(',').Append(unit.r_MaxHealth)
                   .Append(',').Append(unit.r_CurrentWorldPositionX).Append(',')
                   .Append(unit.r_CurrentWorldPositionY).Append(';');
                if (spanIndex % HashChunkSize == HashChunkSize - 1 || spanIndex == units.Length - 1)
                    hashes.Add(ResyncDiagnosticHistory.ComputeSha256(row.ToString()));
            }
            return "[" + string.Join(",", hashes) + "]";
        }

        private static string HashBuildings()
        {
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            var hashes = new List<string>();
            var row = new StringBuilder(HashChunkSize * 30);
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                if (spanIndex % HashChunkSize == 0)
                    row.Clear();
                ref GameBuilding building = ref buildings[spanIndex];
                row.Append(spanIndex).Append(':').Append(building.r_GlobalId).Append(',')
                   .Append((int)building.r_AliveState).Append(',')
                   .Append((int)building.r_BuildingType).Append(',')
                   .Append(building.r_PlayerIdOwner).Append(',')
                   .Append(building.r_CurrentHealth).Append(',')
                   .Append(building.r_WorldPositionX).Append(',')
                   .Append(building.r_WorldPositionY).Append(';');
                if (spanIndex % HashChunkSize == HashChunkSize - 1 || spanIndex == buildings.Length - 1)
                    hashes.Add(ResyncDiagnosticHistory.ComputeSha256(row.ToString()));
            }
            return "[" + string.Join(",", hashes) + "]";
        }

        private void Log(string message) =>
            Shared.DebugLogHelper.LogInfo(log, "[SurrenderDiag] " + message);
    }
}

using Shared;
// Capture completed Vanilla Lord health once; retain that basis across save/load cycles.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed unsafe class LordHealthRuntime
    {
        internal const string SaveDataIdentifier = "ExtraFeatures.LordHealth.v1";
        private bool simulationTickRegistered;
        // Bytes of the last archive load, with the archive object they came from. Every load
        // creates a new MapArchive, so BeginMap applies them only while that same archive is
        // still the loaded one: exactly what reading the archive there would have returned.
        // Load and session-start callbacks may run on different threads.
        private readonly object archivedStateSync = new object();
        private byte[] archivedState;
        private object archivedStateSource;
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = GamePlayerManagerAPI.MAX_PLAYERS;
        // Bit per player ID 1..MAX_PLAYERS (bit 0 unused), as used by pendingPlayersMask.
        private const int AllPlayersMask = ((1 << (LastPlayerId + 1)) - 1) & ~1;
        private const int ScanTickInterval = 10;
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        // Indexed by player ID; slot 0 stays unused.
        private readonly LordHealthBasis[] bases = new LordHealthBasis[LastPlayerId + 1];
        private readonly uint[] appliedGlobalIds = new uint[LastPlayerId + 1];
        private readonly uint[] createdGlobalIds = new uint[LastPlayerId + 1];
        private IDisposable unitCreatedSubscription;
        private int pendingPlayersMask;
        private int warnedPlayersMask;
        private long sessionId;
        private bool initialized;
        private bool mapActive;
        private bool loadedSave;
        private int humanPercent = LordHealthMultiplierPolicy.DefaultPercent;
        private int aiPercent = LordHealthMultiplierPolicy.DefaultPercent;

        public LordHealthRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized) return;
            unitCreatedSubscription = UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(OnUnitCreated);
            // Archive callbacks run in Pre and Post, before managed initialization is
            // complete. They only buffer the bytes; BeginMap applies them for that file.
            // ClearArchivedState is only a courtesy unload callback; correctness relies on the
            // archive identity check, not on the Script Extender raising it.
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SaveDataIdentifier, SaveState, BufferArchivedState, ClearArchivedState))
            {
                unitCreatedSubscription.Dispose();
                unitCreatedSubscription = null;
                throw new InvalidOperationException("Lord health save-data registration failed.");
            }
            if (!simulationTickRegistered)
            {
                if (!APIShared.Simulation.SimulationTickEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid, "lord-health", OnGameTick, out string tickReason, 300))
                    throw new InvalidOperationException("Native simulation ticks unavailable: " + tickReason);
                simulationTickRegistered = true;
            }
            initialized = true;
        }

        // Called during the archive load, only when it has a non-empty entry for SaveDataIdentifier.
        private void BufferArchivedState(byte[] bytes, LoadContext context)
        {
            object source = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            lock (archivedStateSync)
            {
                archivedState = bytes;
                archivedStateSource = source;
            }
        }

        private void ClearArchivedState()
        {
            lock (archivedStateSync)
            {
                archivedState = null;
                archivedStateSource = null;
            }
        }

        // The buffered bytes, only if they came from the archive that is loaded now; always clears.
        private byte[] TakeArchivedStateForCurrentArchive()
        {
            object current = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            lock (archivedStateSync)
            {
                byte[] bytes = current != null && ReferenceEquals(archivedStateSource, current)
                    ? archivedState
                    : null;
                archivedState = null;
                archivedStateSource = null;
                return bytes;
            }
        }

        public void BeginMap(long newSessionId, bool isLoadedSave)
        {
            if (sessionId == newSessionId) return;
            ResetMapState();
            sessionId = newSessionId;
            byte[] archivedBytes = TakeArchivedStateForCurrentArchive();
            if (!IsFeatureModeAllowed()) return;
            loadedSave = isLoadedSave;
            if (loadedSave)
            {
                try
                {
                    byte[] bytes = archivedBytes;
                    if (bytes == null || bytes.Length == 0)
                    {
                        log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Extra Features Lord health inactive: this save has no recorded Vanilla HP basis. Legacy saves are not supported.");
                        return;
                    }
                    LordHealthSaveState state = LordHealthSaveState.Decode(bytes);
                    foreach (LordHealthBasis basis in state.Records) bases[basis.PlayerId] = basis;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/src/LordHealthRuntime.cs:85", $"Extra Features Lord health inactive: invalid saved HP basis: {ex}");
                    return;
                }
            }
            humanPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.HumanLordHealthPercent);
            aiPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.AILordHealthPercent);
            pendingPlayersMask = AllPlayersMask;
            mapActive = true;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebug(log,
                    $"Extra Features Lord health initialized: session={sessionId}, savedBasis={loadedSave}, humans={humanPercent}%, AI={aiPercent}%.");
            ApplyAvailableLords();
        }

        public void ResetMapState()
        {
            mapActive = false;
            pendingPlayersMask = 0;
            warnedPlayersMask = 0;
            sessionId = 0;
            loadedSave = false;
            Array.Clear(bases, 0, bases.Length);
            Array.Clear(appliedGlobalIds, 0, appliedGlobalIds.Length);
            Array.Clear(createdGlobalIds, 0, createdGlobalIds.Length);
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || pendingPlayersMask == 0 || tick % ScanTickInterval != 0) return;
            if (!IsFeatureModeAllowed()) { ResetMapState(); return; }
            try { ApplyAvailableLords(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/src/LordHealthRuntime.cs:130", $"Extra Features Lord health scan failed: {ex}");
            }
        }

        private static bool IsFeatureModeAllowed() =>
            Shared.GameplayModActivationGate.IsFeatureAllowed(ExtraFeaturesPlugin.PluginGuid, Shared.GameplayFeatureId.LordHealthMultipliers);

        private void OnUnitCreated(UnitCreateEventArgs args)
        {
            if (!mapActive || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
            // Post arguments retain original inputs even when Pre subscribers change them.
            // Read the returned unit's actual type/owner; do not write HP here.
            if (!APIShared.UnitAccess.TryGetById((int)args.ReturnValue, out GameUnit* unit, out _) ||
                unit == null || unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD) return;
            int playerId = unit->r_ControllableForPlayerId;
            if (playerId < FirstPlayerId || playerId > LastPlayerId || unit->r_GlobalId == 0) return;
            createdGlobalIds[playerId] = unit->r_GlobalId;
            pendingPlayersMask |= 1 << playerId;
            // The caller publishes Player.LordUnitId and finishes AI HP scaling AFTER Post.
        }

        private void ApplyAvailableLords()
        {
            int pending = pendingPlayersMask;
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
            {
                int bit = 1 << playerId;
                if ((pending & bit) == 0) continue;
                pendingPlayersMask &= ~bit;
                try { if (!TryApplyPlayerLord(playerId)) pendingPlayersMask |= bit; }
                catch { pendingPlayersMask |= bit; throw; }
            }
        }

        private bool TryGetPlayerLord(int playerId, out GameUnit* lord, out bool retry, bool capturePending = false)
        {
            lord = null;
            retry = false;
            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0) return false;
            int expectedGlobalId = GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(playerId);
            if (expectedGlobalId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out lord, out _) ||
                lord == null || lord->r_GlobalId != (uint)expectedGlobalId ||
                lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
            {
                retry = true;
                return false;
            }
            // The spawn helper initializes health while the slot still has NeedsInit.
            // Wait before applying; saving may record its completed spawn maximum.
            // Never revive a dying Lord whose alive flag has not yet changed.
            if (lord->r_CurrentHealth == 0) return false;
            retry = lord->r_AliveState == AliveState.NeedsInit;
            return APIShared.UnitAccess.IsReallyAlive(lord) || (capturePending && retry);
        }

        private bool TryGetBasis(int playerId, GameUnit* lord, out LordHealthBasis basis)
        {
            basis = bases[playerId];
            if (basis.GlobalId == lord->r_GlobalId) return true;
            if (loadedSave && createdGlobalIds[playerId] != lord->r_GlobalId)
            {
                LogPlayerWarningOnce(playerId, "saved Lord identity has no matching Vanilla HP basis");
                return false;
            }
            if (lord->r_MaxHealth == 0) return false;
            basis = new LordHealthBasis(playerId, lord->r_GlobalId, lord->r_MaxHealth);
            bases[playerId] = basis;
            return true;
        }

        private bool TryApplyPlayerLord(int playerId)
        {
            if (!TryGetPlayerLord(playerId, out GameUnit* lord, out bool retry)) return !retry;
            if (appliedGlobalIds[playerId] == lord->r_GlobalId) return true;
            if (!TryGetBasis(playerId, lord, out LordHealthBasis basis))
                return lord->r_MaxHealth != 0; // Zero can become ready; missing saved identity is terminal.
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            int selectedPercent = isAI ? aiPercent : humanPercent;
            uint oldMaximum = lord->r_MaxHealth;
            uint oldCurrent = lord->r_CurrentHealth;
            uint newMaximum = LordHealthMultiplierPolicy.CalculateMaximum(basis.VanillaMaximum, selectedPercent);
            uint newCurrent = LordHealthMultiplierPolicy.CalculateCurrent(oldCurrent, oldMaximum, newMaximum);
            ushort healthPercent = LordHealthMultiplierPolicy.CalculateHealthPercent(newCurrent, newMaximum);
            lord->r_MaxHealth = newMaximum;
            lord->r_CurrentHealth = newCurrent;
            lord->r_CurrentHealthPercentage = healthPercent;
            lord->r_HealthBarBlocks = (uint)(healthPercent / 10);
            appliedGlobalIds[playerId] = lord->r_GlobalId;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/src/LordHealthRuntime.cs:220", $"Extra Features applied Lord health: player={playerId}, globalId={lord->r_GlobalId}, " +
                    $"multiplier={selectedPercent}%, health={oldCurrent}/{oldMaximum}->{newCurrent}/{newMaximum}, vanillaMax={basis.VanillaMaximum}.");
            return true;
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!mapActive || !context.IsSaveFile || context.IsMapEditorSave || !IsFeatureModeAllowed()) return null;
            var records = new List<LordHealthBasis>(LastPlayerId);
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
            {
                if (TryGetPlayerLord(playerId, out GameUnit* lord, out _, capturePending: true) &&
                    TryGetBasis(playerId, lord, out LordHealthBasis basis)) records.Add(basis);
            }
            // Capture a new Lord saved before its next tick without mutating native save data.
            return LordHealthSaveState.Encode(records.ToArray());
        }

        private void LogPlayerWarningOnce(int playerId, string reason)
        {
            int bit = 1 << playerId;
            if ((warnedPlayersMask & bit) != 0) return;
            warnedPlayersMask |= bit;
            Shared.DebugLogHelper.LogWarningLimited(log, "ExtraFeatures/src/LordHealthRuntime.cs:244", $"Extra Features skipped Lord health for player {playerId}: {reason}.");
        }
    }
}

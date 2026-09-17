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
    internal sealed unsafe class LordHealthRuntime : IDisposable
    {
        internal const string SaveDataIdentifier = "ExtraFeatures.LordHealth.v1";
        private const string ArchiveFileName = "_SE_ModData_" + SaveDataIdentifier + ".msgpack";
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private const int ScanTickInterval = 10;
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly LordHealthBasis[] bases = new LordHealthBasis[9];
        private readonly uint[] appliedGlobalIds = new uint[9];
        private readonly uint[] createdGlobalIds = new uint[9];
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
            // complete. BeginMap reads the final archive instead of applying early data.
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SaveDataIdentifier, SaveState, IgnoreEarlyLoad))
            {
                unitCreatedSubscription.Dispose();
                unitCreatedSubscription = null;
                throw new InvalidOperationException("Lord health save-data registration failed.");
            }
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            initialized = true;
        }

        private static void IgnoreEarlyLoad(byte[] bytes, LoadContext context) { }

        public void BeginMap(long newSessionId, bool isLoadedSave)
        {
            if (sessionId == newSessionId) return;
            ResetMapState();
            sessionId = newSessionId;
            if (!IsFeatureModeAllowed()) return;
            loadedSave = isLoadedSave;
            if (loadedSave)
            {
                try
                {
                    byte[] bytes = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(ArchiveFileName, ignoreCase: true);
                    if (bytes == null || bytes.Length == 0)
                    {
                        Shared.DebugLogHelper.LogWarning(log,
                            "Extra Features Lord health inactive: this save has no recorded Vanilla HP basis. Legacy saves are not supported.");
                        return;
                    }
                    LordHealthSaveState state = LordHealthSaveState.Decode(bytes);
                    foreach (LordHealthBasis basis in state.Records) bases[basis.PlayerId] = basis;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(log, $"Extra Features Lord health inactive: invalid saved HP basis: {ex}");
                    return;
                }
            }
            humanPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.HumanLordHealthPercent);
            aiPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.AILordHealthPercent);
            pendingPlayersMask = 0x1FE;
            mapActive = true;
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

        // Only an unpublished initialization candidate may be disposed.
        // Ordinary sessions retain these process-rooted registrations.
        public void Dispose()
        {
            ResetMapState();
            if (!initialized) return;
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
            unitCreatedSubscription?.Dispose();
            unitCreatedSubscription = null;
            initialized = false;
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || pendingPlayersMask == 0 || tick % ScanTickInterval != 0) return;
            if (!IsFeatureModeAllowed()) { ResetMapState(); return; }
            try { ApplyAvailableLords(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features Lord health scan failed: {ex}");
            }
        }

        private static bool IsFeatureModeAllowed() =>
            Shared.GameplayFeatureModePolicy.IsAllowed(ExtraFeaturesPlugin.PluginGuid,
                Shared.GameplayFeatureId.LordHealthMultipliers, Shared.GameplayModActivationGate.Snapshot);

        private void OnUnitCreated(UnitCreateEventArgs args)
        {
            if (!mapActive || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
            // Post arguments retain original inputs even when Pre subscribers change them.
            // Read the returned unit's actual type/owner; do not write HP here.
            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)args.ReturnValue, out GameUnit* unit) ||
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
            if (expectedGlobalId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out lord) ||
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
            return lord->r_AliveState == AliveState.IsAlive || (capturePending && retry);
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
            Shared.DebugLogHelper.LogDebug(log,
                $"Extra Features applied Lord health: player={playerId}, globalId={lord->r_GlobalId}, " +
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
            Shared.DebugLogHelper.LogWarning(log, $"Extra Features skipped Lord health for player {playerId}: {reason}.");
        }
    }
}

using MessagePack;
using MessagePack.Formatters;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace StartConditions
{
    public sealed unsafe partial class StartConditionsRuntime
    {
        private const string AIStartTroopSaveDataIdentifier = "StartConditions_Serp.AIStartTroops";
        private const int AIStartTroopSaveSchemaVersion = 1;
        private const int AIStartTroopValidationIntervalTicks = 250;
        private const short ProtectedAIBehaviourTypeValue = -1;
        private const ushort ProtectedAIBehaviourType = ushort.MaxValue;
        private const ushort ProtectedAIBehaviourRelatedValue = 0;

        // These registrations are process-lifetime state. They are deliberately separate from
        // the toggleable Start Conditions subscriptions and are never disposed during normal
        // plugin, map, or settings lifecycles.
        private readonly List<IDisposable> aiStartTroopLifetimeSubscriptions =
            new List<IDisposable>();
        private readonly Dictionary<int, ProtectedAIStartTroop> protectedAIStartTroopsByUnitId =
            new Dictionary<int, ProtectedAIStartTroop>();
        private readonly Dictionary<int, ProtectedAIStartTroop> protectedAIStartTroopsByTribeId =
            new Dictionary<int, ProtectedAIStartTroop>();

        private bool aiStartTroopIsolationInitialized;
        private bool aiStartTroopMapActive;
        private int nextAIStartTroopValidationTick;
        private int permittedAIStartTroopAssignmentUnitId;
        private int permittedAIStartTroopAssignmentTribeId;
        private AIStartTroopIsolationSaveState pendingAIStartTroopSaveState;

        private void InitializeAIStartTroopIsolation()
        {
            if (aiStartTroopIsolationInitialized)
                return;

            var candidates = new List<IDisposable>();
            bool saveHandlerRegistered = false;
            try
            {
                saveHandlerRegistered = ModSaveDataAPI.Instance.RegisterModDataHandler(
                    AIStartTroopSaveDataIdentifier,
                    SaveAIStartTroopIsolationState,
                    CacheLoadedAIStartTroopIsolationState,
                    ClearAIStartTroopIsolationState);
                if (!saveHandlerRegistered)
                    throw new InvalidOperationException("AI start-troop save-data registration failed.");

                candidates.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(
                    log,
                    context =>
                    {
                        if (context.IsEditor)
                            ClearAIStartTroopIsolationState();
                        else if (context.IsLoadedSave)
                            BeginLoadedAIStartTroopIsolationMap();
                        else
                            BeginNewAIStartTroopIsolationMap();
                    }));

                candidates.Add(Shared.MissionEvents.Ended.Subscribe(_ => ClearAIStartTroopIsolationState()));
                candidates.Add(TribeR3EventHooks.OnTribeAssignUnit.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnAIStartTroopTribeAssign));
                candidates.Add(TribeR3EventHooks.OnTribeDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnAIStartTroopTribeDelete));
                candidates.Add(UnitR3EventHooks.OnUnitDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnProtectedAIStartTroopDelete));

                foreach (IDisposable candidate in candidates)
                    aiStartTroopLifetimeSubscriptions.Add(candidate);

                GameTimeManagerAPI.Instance.OnTick += OnAIStartTroopValidationTick;
                aiStartTroopIsolationInitialized = true;
                LogDebug(
                    "AI start-troop isolation initialized; validation interval ticks",
                    AIStartTroopValidationIntervalTicks,
                    "protected behaviour type",
                    ProtectedAIBehaviourTypeValue,
                    "stance",
                    TribeStance.Aggressive);
            }
            catch
            {
                // Disposal is permitted here because initialization has not been published.
                foreach (IDisposable candidate in candidates)
                    candidate.Dispose();
                if (saveHandlerRegistered)
                    ModSaveDataAPI.Instance.UnregisterModDataHandler(AIStartTroopSaveDataIdentifier);
                throw;
            }
        }

        private void BeginNewAIStartTroopIsolationMap()
        {
            ClearAIStartTroopTracking();
            pendingAIStartTroopSaveState = null;
            aiStartTroopMapActive = true;
            nextAIStartTroopValidationTick = GetCurrentGameTick() + AIStartTroopValidationIntervalTicks;
        }

        private void BeginLoadedAIStartTroopIsolationMap()
        {
            ClearAIStartTroopTracking();
            aiStartTroopMapActive = true;
            nextAIStartTroopValidationTick = GetCurrentGameTick() + AIStartTroopValidationIntervalTicks;

            AIStartTroopIsolationSaveState state = pendingAIStartTroopSaveState;
            pendingAIStartTroopSaveState = null;
            if (state == null)
            {
                LogDebug("Loaded save has no Start Conditions AI start-troop isolation data; no legacy migration is attempted.");
                return;
            }

            if (state.SchemaVersion != AIStartTroopSaveSchemaVersion || state.Records == null)
            {
                LogError(
                    "Ignoring unsupported AI start-troop save data schema",
                    state.SchemaVersion,
                    "expected",
                    AIStartTroopSaveSchemaVersion);
                return;
            }

            int restored = 0;
            int rejected = 0;
            foreach (AIStartTroopIsolationSaveRecord saved in state.Records)
            {
                if (!TryRestoreProtectedAIStartTroop(saved, out string failureReason))
                {
                    rejected++;
                    LogError(
                        "Could not restore one protected AI start troop:",
                        failureReason,
                        "unitGlobalId",
                        saved.UnitGlobalId);
                    continue;
                }

                restored++;
            }

            LogDebug("Restored protected AI start troops from save; restored", restored, "rejected", rejected);
        }

        private int GetCurrentGameTick()
        {
            return GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;
        }

        private void ClearAIStartTroopIsolationState()
        {
            aiStartTroopMapActive = false;
            pendingAIStartTroopSaveState = null;
            ClearAIStartTroopTracking();
        }

        private void ClearAIStartTroopTracking()
        {
            protectedAIStartTroopsByUnitId.Clear();
            protectedAIStartTroopsByTribeId.Clear();
            permittedAIStartTroopAssignmentUnitId = 0;
            permittedAIStartTroopAssignmentTribeId = 0;
        }

        private byte[] SaveAIStartTroopIsolationState(SaveContext context)
        {
            if (!context.IsSaveFile || !aiStartTroopMapActive)
                return null;

            var records = new List<AIStartTroopIsolationSaveRecord>();
            foreach (ProtectedAIStartTroop protectedTroop in protectedAIStartTroopsByUnitId.Values)
            {
                if (!TryGetExactProtectedAIStartTroop(protectedTroop, out GameUnit* unit) ||
                    unit->r_ControllableForPlayerId != protectedTroop.OwnerPlayerId)
                {
                    continue;
                }

                records.Add(new AIStartTroopIsolationSaveRecord
                {
                    UnitGlobalId = protectedTroop.UnitGlobalId,
                    OwnerPlayerId = protectedTroop.OwnerPlayerId,
                    PrivateTribeGlobalId = protectedTroop.PrivateTribeGlobalId,
                });
            }

            if (records.Count == 0)
                return null;

            return AIStartTroopIsolationSaveState.Encode(records.ToArray());
        }

        private void CacheLoadedAIStartTroopIsolationState(byte[] bytes, LoadContext context)
        {
            pendingAIStartTroopSaveState = null;
            if (!context.IsSaveFile || bytes == null || bytes.Length == 0)
                return;

            try
            {
                pendingAIStartTroopSaveState = AIStartTroopIsolationSaveState.Decode(bytes);
            }
            catch (Exception ex)
            {
                LogError("Invalid AI start-troop isolation save data was ignored:", ex);
            }
        }

        private bool TryRestoreProtectedAIStartTroop(
            AIStartTroopIsolationSaveRecord saved,
            out string failureReason)
        {
            failureReason = null;
            if (saved.UnitGlobalId == 0 || saved.UnitGlobalId > int.MaxValue)
            {
                failureReason = "invalid saved unit global id";
                return false;
            }

            int unitId = GameUnitManagerAPI.Instance.GetByGlobalId((int)saved.UnitGlobalId);
            if (unitId <= 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null ||
                unit->r_GlobalId != saved.UnitGlobalId ||
                !IsActiveAIStartTroopUnit(unit->r_AliveState) ||
                unit->r_ControllableForPlayerId != saved.OwnerPlayerId ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(saved.OwnerPlayerId))
            {
                failureReason = $"saved unit {saved.UnitGlobalId} is absent, inactive, has a different owner, or is no longer AI-owned";
                return false;
            }

            var protectedTroop = new ProtectedAIStartTroop(
                unitId,
                saved.UnitGlobalId,
                saved.OwnerPlayerId);

            if (saved.PrivateTribeGlobalId != 0 && saved.PrivateTribeGlobalId <= int.MaxValue)
            {
                int tribeId = GameTribeManagerAPI.Instance.GetByGlobalId((int)saved.PrivateTribeGlobalId);
                if (tribeId > 0 &&
                    GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) &&
                    tribe != null &&
                    tribe->r_GlobalId == saved.PrivateTribeGlobalId)
                {
                    protectedTroop.PrivateTribeId = tribeId;
                    protectedTroop.PrivateTribeGlobalId = saved.PrivateTribeGlobalId;
                    protectedAIStartTroopsByTribeId[tribeId] = protectedTroop;
                }
            }

            protectedAIStartTroopsByUnitId[unitId] = protectedTroop;
            if (!TryEnsureProtectedAIStartTroopState(protectedTroop, unit, "save restoration", out failureReason))
            {
                // Retain the record. The periodic event-based repair may succeed after native
                // post-load state has fully settled.
                return false;
            }

            return true;
        }

        private void TryProtectSpawnedAIStartTroop(long createdId, int ownerPlayerId, eChimps unitType)
        {
            int unitId = 0;
            try
            {
                if (createdId <= 0 || createdId > int.MaxValue)
                    throw new InvalidOperationException($"CreateUnitLocal returned invalid id {createdId}.");

                unitId = (int)createdId;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    !IsActiveAIStartTroopUnit(unit->r_AliveState) ||
                    unit->r_GlobalId == 0 ||
                    unit->r_ControllableForPlayerId != ownerPlayerId ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(ownerPlayerId))
                {
                    throw new InvalidOperationException($"spawned unit {unitId} failed identity, owner, AI, or alive-state validation.");
                }

                var protectedTroop = new ProtectedAIStartTroop(unitId, unit->r_GlobalId, ownerPlayerId);
                protectedAIStartTroopsByUnitId[unitId] = protectedTroop;

                if (!TryEnsureProtectedAIStartTroopState(
                        protectedTroop,
                        unit,
                        "spawn initialization",
                        out string failureReason))
                {
                    throw new InvalidOperationException(failureReason);
                }

                LogDebug(
                    "Protected spawned AI start troop",
                    "unitId", unitId,
                    "unitGlobalId", protectedTroop.UnitGlobalId,
                    "owner", ownerPlayerId,
                    "type", unitType,
                    "tribeId", protectedTroop.PrivateTribeId,
                    "tribeGlobalId", protectedTroop.PrivateTribeGlobalId,
                    "role", (short)unit->r_AITribeRole,
                    "stance", TribeStance.Aggressive);
            }
            catch (Exception ex)
            {
                if (unitId > 0 && protectedAIStartTroopsByUnitId.TryGetValue(unitId, out ProtectedAIStartTroop failed))
                {
                    CleanupFailedAIStartTroopProtection(failed);
                    RemoveProtectedAIStartTroop(failed);
                }

                bool deleteMarked = unitId > 0 && GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId);
                LogError(
                    "AI start troop isolation failed; spawned unit was rejected fail-closed.",
                    "unitId", unitId,
                    "owner", ownerPlayerId,
                    "type", unitType,
                    "deleteMarked", deleteMarked,
                    ex);
            }
        }

        private void OnAIStartTroopTribeAssign(TribeAssignUnitEventArgs args)
        {
            if (!aiStartTroopMapActive ||
                (args.UnitId == permittedAIStartTroopAssignmentUnitId &&
                 args.TribeId == permittedAIStartTroopAssignmentTribeId))
            {
                return;
            }

            try
            {
                if (!TryGetProtectedAIStartTroop(args.UnitId, out ProtectedAIStartTroop protectedTroop, out GameUnit* unit))
                    return;

                EnsureProtectedAIStartTroopBehaviour(unit);
                args.SkipOriginalFunction = true;
                args.ReturnValue = 0;
                LogDebug(
                    "Blocked unexpected AI start-troop tribe assignment",
                    "unitId", args.UnitId,
                    "unitGlobalId", protectedTroop.UnitGlobalId,
                    "requestedTribeId", args.TribeId,
                    "privateTribeId", protectedTroop.PrivateTribeId);
            }
            catch (Exception ex)
            {
                LogError("AI start-troop tribe-assignment protection failed:", ex);
            }
        }

        private void OnAIStartTroopTribeDelete(TribeDeleteEventArgs args)
        {
            if (!aiStartTroopMapActive ||
                !protectedAIStartTroopsByTribeId.TryGetValue(args.TribeId, out ProtectedAIStartTroop protectedTroop))
            {
                return;
            }

            ClearProtectedAIStartTroopTribeTracking(protectedTroop);
            nextAIStartTroopValidationTick = GetCurrentGameTick();
        }

        private void OnProtectedAIStartTroopDelete(UnitDeleteEventArgs args)
        {
            if (!aiStartTroopMapActive)
                return;

            int unitId = unchecked((int)args.UnitId);
            if (protectedAIStartTroopsByUnitId.TryGetValue(unitId, out ProtectedAIStartTroop protectedTroop))
                RemoveProtectedAIStartTroop(protectedTroop);
        }

        private void OnAIStartTroopValidationTick(int tick)
        {
            if (!aiStartTroopMapActive || tick < nextAIStartTroopValidationTick)
                return;

            nextAIStartTroopValidationTick = tick + AIStartTroopValidationIntervalTicks;
            var snapshot = new List<ProtectedAIStartTroop>(protectedAIStartTroopsByUnitId.Values);
            foreach (ProtectedAIStartTroop protectedTroop in snapshot)
            {
                try
                {
                    if (!TryGetProtectedAIStartTroop(
                            protectedTroop.UnitId,
                            out ProtectedAIStartTroop current,
                            out GameUnit* unit) ||
                        !ReferenceEquals(current, protectedTroop))
                    {
                        continue;
                    }

                    if (unit->r_ControllableForPlayerId != protectedTroop.OwnerPlayerId ||
                        !GamePlayerManagerAPI.Instance.IsAIPlayer(protectedTroop.OwnerPlayerId))
                    {
                        RemoveProtectedAIStartTroop(protectedTroop);
                        continue;
                    }

                    if (!TryEnsureProtectedAIStartTroopState(
                            protectedTroop,
                            unit,
                            "periodic validation",
                            out string failureReason))
                    {
                        LogError(
                            "Could not repair protected AI start troop",
                            protectedTroop.UnitId,
                            protectedTroop.UnitGlobalId,
                            failureReason);
                    }
                }
                catch (Exception ex)
                {
                    LogError(
                        "AI start-troop validation failed for",
                        protectedTroop.UnitId,
                        protectedTroop.UnitGlobalId,
                        ex);
                }
            }
        }

        private bool TryEnsureProtectedAIStartTroopState(
            ProtectedAIStartTroop protectedTroop,
            GameUnit* unit,
            string reason,
            out string failureReason)
        {
            failureReason = null;
            EnsureProtectedAIStartTroopBehaviour(unit);

            if (IsProtectedAIStartTroopTribeValid(protectedTroop, unit, out GameTribe* validTribe))
            {
                if (validTribe->r_TribeStance != TribeStance.Aggressive &&
                    !GameTribeManagerAPI.Instance.SetStance(protectedTroop.PrivateTribeId, TribeStance.Aggressive))
                {
                    failureReason = $"could not restore aggressive stance on tribe {protectedTroop.PrivateTribeId}";
                    return false;
                }

                EnsureProtectedAIStartTroopBehaviour(unit);
                return true;
            }

            if (protectedTroop.PrivateTribeId != 0)
                CleanupStaleAIStartTroopTribe(protectedTroop, unit);

            if (unit->r_TribeId != 0)
            {
                int unexpectedTribeId = unit->r_TribeId;
                if (!TryRemoveUnitFromTribeOrClearStaleBackReference(unexpectedTribeId, protectedTroop.UnitId, unit))
                {
                    failureReason = $"could not leave unexpected tribe {unexpectedTribeId}";
                    return false;
                }
            }

            EnsureProtectedAIStartTroopBehaviour(unit);
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            long createdId = tribeApi.Create(protectedTroop.OwnerPlayerId, false);
            if (createdId <= 0 || createdId > int.MaxValue)
            {
                failureReason = $"Create returned invalid tribe id {createdId}";
                return false;
            }

            int privateTribeId = (int)createdId;
            if (!tribeApi.TryGetTribeById(privateTribeId, out GameTribe* privateTribe) ||
                privateTribe == null ||
                !IsActiveAIStartTroopTribe(privateTribe->r_AliveState) ||
                privateTribe->r_GlobalId == 0 ||
                privateTribe->r_PlayerIdOwner != protectedTroop.OwnerPlayerId)
            {
                tribeApi.DeleteTribeSafe(privateTribeId);
                failureReason = $"created tribe {privateTribeId} failed validation";
                return false;
            }

            protectedTroop.PrivateTribeId = privateTribeId;
            protectedTroop.PrivateTribeGlobalId = privateTribe->r_GlobalId;
            protectedAIStartTroopsByTribeId[privateTribeId] = protectedTroop;

            bool assignmentIssued;
            permittedAIStartTroopAssignmentUnitId = protectedTroop.UnitId;
            permittedAIStartTroopAssignmentTribeId = privateTribeId;
            try
            {
                assignmentIssued = tribeApi.AssignUnit(privateTribeId, protectedTroop.UnitId);
            }
            finally
            {
                permittedAIStartTroopAssignmentUnitId = 0;
                permittedAIStartTroopAssignmentTribeId = 0;
            }

            bool stanceSet = assignmentIssued &&
                unit->r_TribeId == privateTribeId &&
                tribeApi.SetStance(privateTribeId, TribeStance.Aggressive);
            EnsureProtectedAIStartTroopBehaviour(unit);

            if (!assignmentIssued || unit->r_TribeId != privateTribeId || !stanceSet ||
                privateTribe->r_TribeStance != TribeStance.Aggressive)
            {
                CleanupFailedAIStartTroopProtection(protectedTroop);
                failureReason =
                    $"private tribe setup failed for tribe {privateTribeId} " +
                    $"(assignment={assignmentIssued}, unitTribe={unit->r_TribeId}, stanceSet={stanceSet}, stance={privateTribe->r_TribeStance})";
                return false;
            }

            LogDebug(
                "Created private aggressive AI start-troop tribe",
                "unitId", protectedTroop.UnitId,
                "unitGlobalId", protectedTroop.UnitGlobalId,
                "owner", protectedTroop.OwnerPlayerId,
                "tribeId", privateTribeId,
                "tribeGlobalId", protectedTroop.PrivateTribeGlobalId,
                "reason", reason);
            return true;
        }

        private bool IsProtectedAIStartTroopTribeValid(
            ProtectedAIStartTroop protectedTroop,
            GameUnit* unit,
            out GameTribe* tribe)
        {
            tribe = null;
            return protectedTroop.PrivateTribeId > 0 &&
                protectedTroop.PrivateTribeGlobalId != 0 &&
                unit->r_TribeId == protectedTroop.PrivateTribeId &&
                GameTribeManagerAPI.Instance.TryGetTribeById(protectedTroop.PrivateTribeId, out tribe) &&
                tribe != null &&
                tribe->r_GlobalId == protectedTroop.PrivateTribeGlobalId &&
                IsActiveAIStartTroopTribe(tribe->r_AliveState) &&
                tribe->r_PlayerIdOwner == protectedTroop.OwnerPlayerId;
        }

        private bool TryRemoveUnitFromTribeOrClearStaleBackReference(
            int tribeId,
            int unitId,
            GameUnit* unit)
        {
            if (GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) &&
                tribe != null &&
                IsActiveAIStartTroopTribe(tribe->r_AliveState))
            {
                return TryUnassignAIStartTroop(tribeId, unitId, unit);
            }

            unit->r_TribeId = 0;
            unit->r_TribeLeaderUnitId = 0;
            return true;
        }

        private bool TryUnassignAIStartTroop(int tribeId, int unitId, GameUnit* unit)
        {
            if (unit == null || unit->r_TribeId != tribeId)
                return false;

            bool issued = GameTribeManagerAPI.Instance.UnassignUnit(tribeId, unitId);
            return issued && unit->r_TribeId != tribeId;
        }

        private void CleanupStaleAIStartTroopTribe(ProtectedAIStartTroop protectedTroop, GameUnit* unit)
        {
            int staleTribeId = protectedTroop.PrivateTribeId;
            bool exactTribeFound = TryGetExactProtectedAIStartTroopTribe(protectedTroop, out GameTribe* staleTribe);
            if (unit->r_TribeId == staleTribeId)
            {
                if (exactTribeFound && IsActiveAIStartTroopTribe(staleTribe->r_AliveState))
                    TryUnassignAIStartTroop(staleTribeId, protectedTroop.UnitId, unit);
                else
                {
                    unit->r_TribeId = 0;
                    unit->r_TribeLeaderUnitId = 0;
                }
            }

            if (exactTribeFound && IsActiveAIStartTroopTribe(staleTribe->r_AliveState))
                GameTribeManagerAPI.Instance.DeleteTribeSafe(staleTribeId);

            ClearProtectedAIStartTroopTribeTracking(protectedTroop);
        }

        private void CleanupFailedAIStartTroopProtection(ProtectedAIStartTroop protectedTroop)
        {
            if (!TryGetExactProtectedAIStartTroop(protectedTroop, out GameUnit* unit))
            {
                ClearProtectedAIStartTroopTribeTracking(protectedTroop);
                return;
            }

            int tribeId = protectedTroop.PrivateTribeId;
            bool exactTribeFound = TryGetExactProtectedAIStartTroopTribe(protectedTroop, out GameTribe* tribe);
            if (tribeId > 0 && unit->r_TribeId == tribeId &&
                exactTribeFound && IsActiveAIStartTroopTribe(tribe->r_AliveState))
            {
                TryUnassignAIStartTroop(tribeId, protectedTroop.UnitId, unit);
            }

            if (exactTribeFound && IsActiveAIStartTroopTribe(tribe->r_AliveState))
                GameTribeManagerAPI.Instance.DeleteTribeSafe(tribeId);

            ClearProtectedAIStartTroopTribeTracking(protectedTroop);
        }

        private bool TryGetProtectedAIStartTroop(
            int unitId,
            out ProtectedAIStartTroop protectedTroop,
            out GameUnit* unit)
        {
            unit = null;
            if (!protectedAIStartTroopsByUnitId.TryGetValue(unitId, out protectedTroop))
                return false;

            if (!TryGetExactProtectedAIStartTroop(protectedTroop, out unit))
            {
                RemoveProtectedAIStartTroop(protectedTroop);
                protectedTroop = null;
                return false;
            }

            return true;
        }

        private bool TryGetExactProtectedAIStartTroop(
            ProtectedAIStartTroop protectedTroop,
            out GameUnit* unit)
        {
            unit = null;
            return protectedTroop != null &&
                GameUnitManagerAPI.Instance.TryGetUnitById(protectedTroop.UnitId, out unit) &&
                unit != null &&
                unit->r_GlobalId == protectedTroop.UnitGlobalId &&
                IsActiveAIStartTroopUnit(unit->r_AliveState);
        }

        private bool TryGetExactProtectedAIStartTroopTribe(
            ProtectedAIStartTroop protectedTroop,
            out GameTribe* tribe)
        {
            tribe = null;
            return protectedTroop.PrivateTribeId > 0 &&
                protectedTroop.PrivateTribeGlobalId != 0 &&
                GameTribeManagerAPI.Instance.TryGetTribeById(protectedTroop.PrivateTribeId, out tribe) &&
                tribe != null &&
                tribe->r_GlobalId == protectedTroop.PrivateTribeGlobalId;
        }

        private static void EnsureProtectedAIStartTroopBehaviour(GameUnit* unit)
        {
            unit->r_AITribeRoleRelatedUnknown = ProtectedAIBehaviourRelatedValue;
            unit->r_AITribeRole = ProtectedAIBehaviourType;
        }

        private void RemoveProtectedAIStartTroop(ProtectedAIStartTroop protectedTroop)
        {
            if (protectedTroop == null)
                return;

            if (protectedAIStartTroopsByUnitId.TryGetValue(
                    protectedTroop.UnitId,
                    out ProtectedAIStartTroop currentUnit) &&
                ReferenceEquals(currentUnit, protectedTroop))
            {
                protectedAIStartTroopsByUnitId.Remove(protectedTroop.UnitId);
            }

            ClearProtectedAIStartTroopTribeTracking(protectedTroop);
        }

        private void ClearProtectedAIStartTroopTribeTracking(ProtectedAIStartTroop protectedTroop)
        {
            if (protectedTroop.PrivateTribeId > 0 &&
                protectedAIStartTroopsByTribeId.TryGetValue(
                    protectedTroop.PrivateTribeId,
                    out ProtectedAIStartTroop currentTribe) &&
                ReferenceEquals(currentTribe, protectedTroop))
            {
                protectedAIStartTroopsByTribeId.Remove(protectedTroop.PrivateTribeId);
            }

            protectedTroop.PrivateTribeId = 0;
            protectedTroop.PrivateTribeGlobalId = 0;
        }

        private static bool IsActiveAIStartTroopUnit(AliveState state)
        {
            return state == AliveState.NeedsInit || state == AliveState.IsAlive;
        }

        private static bool IsActiveAIStartTroopTribe(AliveState state)
        {
            return state == AliveState.NeedsInit || state == AliveState.IsAlive;
        }

        private sealed class ProtectedAIStartTroop
        {
            public ProtectedAIStartTroop(int unitId, uint unitGlobalId, int ownerPlayerId)
            {
                UnitId = unitId;
                UnitGlobalId = unitGlobalId;
                OwnerPlayerId = ownerPlayerId;
            }

            public int UnitId { get; }
            public uint UnitGlobalId { get; }
            public int OwnerPlayerId { get; }
            public int PrivateTribeId { get; set; }
            public uint PrivateTribeGlobalId { get; set; }
        }

    }

    internal readonly struct AIStartTroopIsolationSaveRecord
    {
        internal AIStartTroopIsolationSaveRecord(
            uint unitGlobalId,
            int ownerPlayerId,
            uint privateTribeGlobalId)
        {
            UnitGlobalId = unitGlobalId;
            OwnerPlayerId = ownerPlayerId;
            PrivateTribeGlobalId = privateTribeGlobalId;
        }

        internal uint UnitGlobalId { get; }
        internal int OwnerPlayerId { get; }
        internal uint PrivateTribeGlobalId { get; }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(AIStartTroopIsolationSaveStateFormatter))]
    internal sealed class AIStartTroopIsolationSaveState
    {
        internal const int MaximumRecords = 10000;
        internal const int MaximumPayloadBytes = 262144;

        [IgnoreMember]
        internal int SchemaVersion;

        [IgnoreMember]
        internal AIStartTroopIsolationSaveRecord[] Records;

        internal static byte[] Encode(AIStartTroopIsolationSaveRecord[] records)
        {
            return MessagePackSerializer.Serialize(new AIStartTroopIsolationSaveState
            {
                SchemaVersion = 1,
                Records = records,
            });
        }

        internal static AIStartTroopIsolationSaveState Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumPayloadBytes)
                throw new InvalidOperationException("AI start-troop payload has an invalid length.");

            var reader = new MessagePackReader(bytes);
            AIStartTroopIsolationSaveState state = new AIStartTroopIsolationSaveStateFormatter()
                .Deserialize(ref reader, MessagePackSerializerOptions.Standard);
            if (!reader.End)
                throw new InvalidOperationException("Trailing AI start-troop save data.");
            return state;
        }
    }

    internal sealed class AIStartTroopIsolationSaveStateFormatter :
        IMessagePackFormatter<AIStartTroopIsolationSaveState>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            AIStartTroopIsolationSaveState value,
            MessagePackSerializerOptions options)
        {
            AIStartTroopIsolationSaveRecord[] records = value?.Records;
            if (value == null ||
                value.SchemaVersion != 1 ||
                records == null ||
                records.Length > AIStartTroopIsolationSaveState.MaximumRecords)
            {
                throw new MessagePackSerializationException("Invalid AI start-troop save state.");
            }

            var unitGlobalIds = new HashSet<uint>();
            writer.WriteArrayHeader(1 + records.Length * 3);
            writer.Write(value.SchemaVersion);
            foreach (AIStartTroopIsolationSaveRecord record in records)
            {
                Validate(record, unitGlobalIds);
                writer.Write(record.UnitGlobalId);
                writer.Write(record.OwnerPlayerId);
                writer.Write(record.PrivateTribeGlobalId);
            }
        }

        public AIStartTroopIsolationSaveState Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count < 1 ||
                count > 1 + AIStartTroopIsolationSaveState.MaximumRecords * 3 ||
                (count - 1) % 3 != 0)
            {
                throw new MessagePackSerializationException("Invalid AI start-troop save-data field count.");
            }

            int schemaVersion = reader.ReadInt32();
            if (schemaVersion != 1)
                throw new MessagePackSerializationException("Unsupported AI start-troop save-data schema.");

            var records = new AIStartTroopIsolationSaveRecord[(count - 1) / 3];
            var unitGlobalIds = new HashSet<uint>();
            for (int index = 0; index < records.Length; index++)
            {
                var record = new AIStartTroopIsolationSaveRecord(
                    reader.ReadUInt32(),
                    reader.ReadInt32(),
                    reader.ReadUInt32());
                Validate(record, unitGlobalIds);
                records[index] = record;
            }

            return new AIStartTroopIsolationSaveState
            {
                SchemaVersion = schemaVersion,
                Records = records,
            };
        }

        private static void Validate(
            AIStartTroopIsolationSaveRecord record,
            HashSet<uint> unitGlobalIds)
        {
            if (record.UnitGlobalId == 0 ||
                record.UnitGlobalId > int.MaxValue ||
                record.OwnerPlayerId < 1 ||
                record.OwnerPlayerId > 8 ||
                record.PrivateTribeGlobalId > int.MaxValue ||
                !unitGlobalIds.Add(record.UnitGlobalId))
            {
                throw new MessagePackSerializationException("Invalid or duplicate AI start-troop identity.");
            }
        }
    }
}

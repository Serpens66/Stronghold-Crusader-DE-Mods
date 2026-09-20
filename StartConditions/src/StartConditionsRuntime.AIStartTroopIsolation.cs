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
        private const int AIStartTroopSaveSchemaVersion = AIStartTroopIsolationSaveState.SchemaVersionCurrent;
        private const int AIStartTroopValidationIntervalTicks = 250;
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
            bool tickHandlerSubscribed = false;
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
                tickHandlerSubscribed = true;
                aiStartTroopIsolationInitialized = true;
            }
            catch
            {
                // Disposal is permitted here because initialization has not been published.
                if (tickHandlerSubscribed)
                    GameTimeManagerAPI.Instance.OnTick -= OnAIStartTroopValidationTick;
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
                return;

            if (state.SchemaVersion != AIStartTroopSaveSchemaVersion || state.Records == null)
            {
                LogError(
                    "Ignoring unsupported AI start-troop save data schema",
                    state.SchemaVersion,
                    "expected",
                    AIStartTroopSaveSchemaVersion);
                return;
            }

            foreach (AIStartTroopIsolationSaveRecord saved in state.Records)
            {
                if (!TryRestoreProtectedAIStartTroop(saved, out string failureReason))
                {
                    LogError(
                        "Could not restore one protected AI start troop:",
                        failureReason,
                        "unitGlobalId",
                        saved.UnitGlobalId);
                    continue;
                }
            }

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
                if (protectedTroop.PendingDeletion ||
                    !TryGetExactProtectedAIStartTroop(protectedTroop, out GameUnit* unit) ||
                    unit->r_ControllableForPlayerId != protectedTroop.OwnerPlayerId ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(protectedTroop.OwnerPlayerId))
                {
                    continue;
                }

                EnsureProtectedAIStartTroopBehaviour(unit);
                records.Add(new AIStartTroopIsolationSaveRecord(
                    protectedTroop.UnitGlobalId,
                    protectedTroop.OwnerPlayerId,
                    protectedTroop.PrivateTribeGlobalId));
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
                !GamePlayerManagerAPI.Instance.IsAIPlayer(saved.OwnerPlayerId) ||
                unit->r_AITribeRole != ProtectedAIBehaviourType ||
                unit->r_AITribeRoleRelatedUnknown != ProtectedAIBehaviourRelatedValue)
            {
                failureReason =
                    $"saved unit {saved.UnitGlobalId} is absent, inactive, has a different owner, " +
                    "is no longer AI-owned, or lacks the persisted isolation marker";
                return false;
            }

            var protectedTroop = new ProtectedAIStartTroop(
                unitId,
                saved.UnitGlobalId,
                saved.OwnerPlayerId);

            if (saved.PrivateTribeGlobalId != 0 && saved.PrivateTribeGlobalId <= int.MaxValue)
            {
                int tribeId = GameTribeManagerAPI.Instance.GetByGlobalId((int)saved.PrivateTribeGlobalId);
                if (tribeId > 0)
                {
                    if (!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                        tribe == null ||
                        tribe->r_GlobalId != saved.PrivateTribeGlobalId ||
                        !IsActiveAIStartTroopTribe(tribe->r_AliveState) ||
                        tribe->r_PlayerIdOwner != saved.OwnerPlayerId ||
                        unit->r_TribeId != tribeId ||
                        !TribeContainsOnlyProtectedUnit(tribe, unitId))
                    {
                        failureReason = $"saved private tribe {saved.PrivateTribeGlobalId} has conflicting live state";
                        return false;
                    }

                    protectedTroop.PrivateTribeId = tribeId;
                    protectedTroop.PrivateTribeGlobalId = saved.PrivateTribeGlobalId;
                    protectedAIStartTroopsByTribeId[tribeId] = protectedTroop;
                }
            }

            if (protectedTroop.PrivateTribeId == 0 &&
                unit->r_TribeId != 0 &&
                GameTribeManagerAPI.Instance.TryGetTribeById(unit->r_TribeId, out GameTribe* currentTribe) &&
                currentTribe != null &&
                IsActiveAIStartTroopTribe(currentTribe->r_AliveState))
            {
                failureReason = $"saved unit {saved.UnitGlobalId} belongs to conflicting live tribe {unit->r_TribeId}";
                return false;
            }

            protectedAIStartTroopsByUnitId[unitId] = protectedTroop;
            if (!TryEnsureProtectedAIStartTroopState(protectedTroop, unit, out failureReason))
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
                    unit->r_GlobalId > int.MaxValue ||
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
                        out string failureReason))
                {
                    throw new InvalidOperationException(failureReason);
                }

            }
            catch (Exception ex)
            {
                Exception cleanupException = null;
                protectedAIStartTroopsByUnitId.TryGetValue(unitId, out ProtectedAIStartTroop failed);
                if (failed != null)
                {
                    try
                    {
                        CleanupFailedAIStartTroopProtection(failed);
                    }
                    catch (Exception cleanupEx)
                    {
                        cleanupException = cleanupEx;
                    }
                }

                bool deleteMarked = false;
                Exception deleteException = null;
                if (unitId > 0)
                {
                    try
                    {
                        deleteMarked = GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId);
                    }
                    catch (Exception deleteEx)
                    {
                        deleteException = deleteEx;
                    }
                }

                if (failed != null)
                {
                    if (deleteMarked || !TryGetExactProtectedAIStartTroop(failed, out GameUnit* failedUnit))
                    {
                        RemoveProtectedAIStartTroop(failed);
                    }
                    else
                    {
                        EnsureProtectedAIStartTroopBehaviour(failedUnit);
                        failed.PendingDeletion = true;
                        nextAIStartTroopValidationTick = 0;
                    }
                }

                LogError(
                    "AI start troop isolation failed; spawned unit was rejected fail-closed.",
                    "unitId", unitId,
                    "owner", ownerPlayerId,
                    "type", unitType,
                    "deleteMarked", deleteMarked,
                    "cleanupException", cleanupException,
                    "deleteException", deleteException,
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
                bool targetsPrivateTribe = protectedAIStartTroopsByTribeId.TryGetValue(
                    args.TribeId,
                    out _);
                if (!TryGetProtectedAIStartTroop(args.UnitId, out ProtectedAIStartTroop protectedTroop, out GameUnit* unit))
                {
                    if (targetsPrivateTribe)
                    {
                        args.SkipOriginalFunction = true;
                        args.ReturnValue = 0;
                    }

                    return;
                }

                EnsureProtectedAIStartTroopBehaviour(unit);
                args.SkipOriginalFunction = true;
                args.ReturnValue = 0;
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

            try
            {
                ClearProtectedAIStartTroopTribeTracking(protectedTroop);
                nextAIStartTroopValidationTick = GetCurrentGameTick();
            }
            catch (Exception ex)
            {
                LogError("AI start-troop tribe-delete tracking failed:", ex);
            }
        }

        private void OnProtectedAIStartTroopDelete(UnitDeleteEventArgs args)
        {
            if (!aiStartTroopMapActive)
                return;

            try
            {
                int unitId = unchecked((int)args.UnitId);
                if (protectedAIStartTroopsByUnitId.TryGetValue(unitId, out ProtectedAIStartTroop protectedTroop))
                {
                    if (TryGetExactProtectedAIStartTroopTribe(protectedTroop, out GameTribe* tribe) &&
                        IsActiveAIStartTroopTribe(tribe->r_AliveState))
                    {
                        GameTribeManagerAPI.Instance.DeleteTribeSafe(protectedTroop.PrivateTribeId);
                    }

                    RemoveProtectedAIStartTroop(protectedTroop);
                }
            }
            catch (Exception ex)
            {
                LogError("AI start-troop unit-delete tracking failed:", ex);
            }
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

                    if (protectedTroop.PendingDeletion)
                    {
                        EnsureProtectedAIStartTroopBehaviour(unit);
                        if (GameUnitManagerAPI.Instance.DeleteUnitSafe(protectedTroop.UnitId))
                            RemoveProtectedAIStartTroop(protectedTroop);
                        continue;
                    }

                    if (unit->r_ControllableForPlayerId != protectedTroop.OwnerPlayerId ||
                        !GamePlayerManagerAPI.Instance.IsAIPlayer(protectedTroop.OwnerPlayerId))
                    {
                        CleanupFailedAIStartTroopProtection(protectedTroop);
                        unit->r_AITribeRoleRelatedUnknown = 0;
                        unit->r_AITribeRole = 0;
                        RemoveProtectedAIStartTroop(protectedTroop);
                        continue;
                    }

                    if (!TryEnsureProtectedAIStartTroopState(
                            protectedTroop,
                            unit,
                            out string failureReason))
                    {
                        LogProtectedAIStartTroopRepairFailure(protectedTroop, failureReason, null);
                    }
                    else
                        protectedTroop.RepairFailureLog.MarkRecovered();
                }
                catch (Exception ex)
                {
                    string failureSignature = $"{ex.GetType().FullName}: {ex.Message}";
                    LogProtectedAIStartTroopRepairFailure(protectedTroop, failureSignature, ex);
                }
            }
        }

        private void LogProtectedAIStartTroopRepairFailure(
            ProtectedAIStartTroop protectedTroop,
            string failureSignature,
            Exception exception)
        {
            failureSignature = failureSignature ?? "unknown repair failure";
            if (!protectedTroop.RepairFailureLog.ShouldLog(failureSignature))
                return;

            if (exception == null)
            {
                LogError(
                    "Could not repair protected AI start troop",
                    protectedTroop.UnitId,
                    protectedTroop.UnitGlobalId,
                    failureSignature);
            }
            else
            {
                LogError(
                    "AI start-troop validation failed for",
                    protectedTroop.UnitId,
                    protectedTroop.UnitGlobalId,
                    exception);
            }
        }

        private bool TryEnsureProtectedAIStartTroopState(
            ProtectedAIStartTroop protectedTroop,
            GameUnit* unit,
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
                privateTribe->r_GlobalId > int.MaxValue ||
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

            bool membershipValid = assignmentIssued &&
                unit->r_TribeId == privateTribeId &&
                TribeContainsOnlyProtectedUnit(privateTribe, protectedTroop.UnitId);
            bool stanceSet = membershipValid &&
                tribeApi.SetStance(privateTribeId, TribeStance.Aggressive);
            EnsureProtectedAIStartTroopBehaviour(unit);

            if (!membershipValid || !stanceSet ||
                privateTribe->r_TribeStance != TribeStance.Aggressive)
            {
                CleanupFailedAIStartTroopProtection(protectedTroop);
                failureReason =
                    $"private tribe setup failed for tribe {privateTribeId} " +
                    $"(assignment={assignmentIssued}, membership={membershipValid}, unitTribe={unit->r_TribeId}, " +
                    $"stanceSet={stanceSet}, stance={privateTribe->r_TribeStance})";
                return false;
            }

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
                tribe->r_PlayerIdOwner == protectedTroop.OwnerPlayerId &&
                TribeContainsOnlyProtectedUnit(tribe, protectedTroop.UnitId);
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
                return TryUnassignAIStartTroop(tribeId, unitId, unit, tribe);
            }

            unit->r_TribeId = 0;
            unit->r_TribeLeaderUnitId = 0;
            return true;
        }

        private bool TryUnassignAIStartTroop(
            int tribeId,
            int unitId,
            GameUnit* unit,
            GameTribe* tribe)
        {
            if (unit == null ||
                tribe == null ||
                unit->r_TribeId != tribeId ||
                tribe->r_UnitsInGroup == 0 ||
                !DoesTribeMembershipIncludeUnit(tribe, unitId))
            {
                return false;
            }

            ushort membersBefore = tribe->r_UnitsInGroup;
            bool issued = GameTribeManagerAPI.Instance.UnassignUnit(tribeId, unitId);
            return issued &&
                unit->r_TribeId != tribeId &&
                !DoesTribeMembershipIncludeUnit(tribe, unitId) &&
                tribe->r_UnitsInGroup < membersBefore;
        }

        private void CleanupStaleAIStartTroopTribe(ProtectedAIStartTroop protectedTroop, GameUnit* unit)
        {
            int staleTribeId = protectedTroop.PrivateTribeId;
            bool exactTribeFound = TryGetExactProtectedAIStartTroopTribe(protectedTroop, out GameTribe* staleTribe);
            if (unit->r_TribeId == staleTribeId)
            {
                if (exactTribeFound && IsActiveAIStartTroopTribe(staleTribe->r_AliveState))
                {
                    if (!TryUnassignAIStartTroop(
                            staleTribeId,
                            protectedTroop.UnitId,
                            unit,
                            staleTribe))
                    {
                        if (GameTribeManagerAPI.Instance.DeleteTribeSafe(staleTribeId))
                        {
                            unit->r_TribeId = 0;
                            unit->r_TribeLeaderUnitId = 0;
                        }
                    }
                }
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
                TryUnassignAIStartTroop(tribeId, protectedTroop.UnitId, unit, tribe);
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
                DeleteExactProtectedAIStartTroopTribe(protectedTroop);
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

        private static bool DoesTribeMembershipIncludeUnit(GameTribe* tribe, int unitId)
        {
            if (tribe == null || unitId <= 0 || unitId >= 10000)
                return false;

            ushort* membershipWords = &tribe->r_UnitIdsInGroupBitfield;
            int wordIndex = unitId >> 4;
            int bitIndex = unitId & 15;
            return (membershipWords[wordIndex] & (1 << bitIndex)) != 0;
        }

        private static bool TribeContainsOnlyProtectedUnit(GameTribe* tribe, int unitId)
        {
            if (tribe == null ||
                tribe->r_UnitsInGroup != 1 ||
                tribe->r_LeaderUnitId != unitId ||
                !DoesTribeMembershipIncludeUnit(tribe, unitId))
            {
                return false;
            }

            ushort* membershipWords = &tribe->r_UnitIdsInGroupBitfield;
            int expectedWordIndex = unitId >> 4;
            ushort expectedWord = (ushort)(1 << (unitId & 15));
            for (int wordIndex = 0; wordIndex < 625; wordIndex++)
            {
                ushort expected = wordIndex == expectedWordIndex ? expectedWord : (ushort)0;
                if (membershipWords[wordIndex] != expected)
                    return false;
            }

            return true;
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

        private void DeleteExactProtectedAIStartTroopTribe(ProtectedAIStartTroop protectedTroop)
        {
            if (TryGetExactProtectedAIStartTroopTribe(protectedTroop, out GameTribe* tribe) &&
                IsActiveAIStartTroopTribe(tribe->r_AliveState) &&
                tribe->r_PlayerIdOwner == protectedTroop.OwnerPlayerId)
            {
                GameTribeManagerAPI.Instance.DeleteTribeSafe(protectedTroop.PrivateTribeId);
            }
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
            public bool PendingDeletion { get; set; }
            public RepairFailureLogState RepairFailureLog { get; } = new RepairFailureLogState();
        }

    }
}

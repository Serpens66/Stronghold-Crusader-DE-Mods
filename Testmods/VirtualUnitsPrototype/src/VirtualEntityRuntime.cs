using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VirtualUnitsPrototype.API;

namespace VirtualUnitsPrototype
{
    internal sealed unsafe class VirtualEntityRuntime
    {
        private const byte UnitKind = (byte)VirtualEntityKind.Unit;
        private const byte BuildingKind = (byte)VirtualEntityKind.Building;
        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly Dictionary<string, VirtualUnitDefinition> unitDefinitions = new Dictionary<string, VirtualUnitDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, VirtualBuildingDefinition> buildingDefinitions = new Dictionary<string, VirtualBuildingDefinition>(StringComparer.Ordinal);
        private readonly IdentityRegistry<StoredInstance> instances = new IdentityRegistry<StoredInstance>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, PendingSpawn> pendingUnits = new Dictionary<int, PendingSpawn>();
        private readonly Dictionary<int, PendingSpawn> pendingBuildings = new Dictionary<int, PendingSpawn>();
        private readonly ConcurrentQueue<OperationRequest> operationQueue = new ConcurrentQueue<OperationRequest>();
        private readonly ConcurrentQueue<OperationCompletion> completionQueue = new ConcurrentQueue<OperationCompletion>();
        private readonly ConcurrentQueue<bool> availabilityQueue = new ConcurrentQueue<bool>();
        private readonly ConcurrentQueue<bool> visualResetQueue = new ConcurrentQueue<bool>();
        private readonly ConcurrentQueue<int> unitTintRestoreQueue = new ConcurrentQueue<int>();
        private readonly ConcurrentQueue<RecruitmentTransition> recruitmentTransitions = new ConcurrentQueue<RecruitmentTransition>();
        private List<SaveRecord> pendingRestore;
        private PendingBuildingSpawn pendingBuilding;
        private VisualRuntime visuals;
        private IUnitHudPresentationCapability unitHudPresentation;
        private PendingRecruitment pendingRecruitment;
        private VirtualSpawnController spawnController;
        private bool definitionsSealed;
        private bool initialized;
        private bool mapActive;
        private bool modeAllowed;
        private long nextOperationId;
        private int currentSimulationTick;
        private int simulationThreadId;
        private bool unrepresentableSpeedLogged;
        private const int SpawnInitializationTickBudget = 180;
        private const int RecruitmentCorrelationTickBudget = 8;
        private const int RecruitmentQuietTickBudget = 1;

        internal VirtualEntityRuntime(ManualLogSource log) { this.log = log ?? throw new ArgumentNullException(nameof(log)); }
        internal static VirtualEntityRuntime Current { get; set; }
        internal bool CanMutate { get { lock (sync) return initialized && mapActive && modeAllowed; } }

        internal void Initialize(VirtualUnitsPlugin plugin)
        {
            lock (sync) definitionsSealed = true;
            try
            {
                visuals = new VisualRuntime(this, log);
                visuals.Install();
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Where(x => x.Phase == EventHookPhase.Post).Subscribe(OnStartMap));
                subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable.Where(x => x.Phase == EventHookPhase.Post).Subscribe(OnLoadSave));
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(_ => ClearMapState()));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualSpawn.Observable.Subscribe(visuals.OnUnitVisualSpawn));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable.Subscribe(visuals.OnUnitVisualInterpolate));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualRemove.Observable.Subscribe(visuals.OnUnitVisualRemove));
                subscriptions.Add(UnitR3EventHooks.OnUnitTransition.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(OnUnitTransition));
                subscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(x => Forget(VirtualEntityKind.Unit, checked((int)x.UnitId))));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(x => Forget(VirtualEntityKind.Building, x.BuildingId)));
                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(VirtualUnitsPlugin.PluginGuid, Save, Load, ClearMapState))
                    throw new InvalidOperationException("Mod save-data identifier is already registered.");
                spawnController = new VirtualSpawnController(this, log);
                spawnController.Initialize();
                GameXAMLManagerAPI.Instance.RegisterBinding("VirtualUnitsPrototypeHud", spawnController.Hud);
                ApiShared.WhenReady(RegisterUnitHudPresentation);
                GameTimeManagerAPI.Instance.OnTick += OnSimulationTick;
                lock (sync) initialized = true;
                Shared.DebugLogHelper.LogInfo(log, $"Runtime initialized; unitDefinitions={unitDefinitions.Count}, buildingDefinitions={buildingDefinitions.Count}, definitions sealed.");
            }
            catch (Exception ex)
            {
                visuals?.Dispose();
                Shared.DebugLogHelper.LogError(log, $"Runtime initialization failed closed: {ex}");
                throw;
            }
        }

        internal VirtualApiResult Register(VirtualUnitDefinition definition)
        {
            lock (sync)
            {
                if (definitionsSealed) return Result(VirtualApiResultCode.DefinitionsSealed, "Definitions are sealed.");
                VirtualApiResult validation = Validate(definition);
                if (!validation.Succeeded) return validation;
                if (unitDefinitions.ContainsKey(definition.TypeId) || buildingDefinitions.ContainsKey(definition.TypeId)) return Result(VirtualApiResultCode.DuplicateTypeId, "Type ID is already registered.");
                unitDefinitions.Add(definition.TypeId, definition); return VirtualApiResult.Success("Unit definition registered.");
            }
        }

        internal VirtualApiResult Register(VirtualBuildingDefinition definition)
        {
            lock (sync)
            {
                if (definitionsSealed) return Result(VirtualApiResultCode.DefinitionsSealed, "Definitions are sealed.");
                VirtualApiResult validation = Validate(definition);
                if (!validation.Succeeded) return validation;
                if (unitDefinitions.ContainsKey(definition.TypeId) || buildingDefinitions.ContainsKey(definition.TypeId)) return Result(VirtualApiResultCode.DuplicateTypeId, "Type ID is already registered.");
                buildingDefinitions.Add(definition.TypeId, definition); return VirtualApiResult.Success("Building definition registered.");
            }
        }

        internal VirtualApiResult TryGetDefinition(string typeId, out VirtualUnitDefinition definition)
        { lock (sync) { if (unitDefinitions.TryGetValue(typeId ?? string.Empty, out definition)) return VirtualApiResult.Success(); } return Result(VirtualApiResultCode.UnknownTypeId, "Unknown unit type ID."); }
        internal VirtualApiResult TryGetDefinition(string typeId, out VirtualBuildingDefinition definition)
        { lock (sync) { if (buildingDefinitions.TryGetValue(typeId ?? string.Empty, out definition)) return VirtualApiResult.Success(); } return Result(VirtualApiResultCode.UnknownTypeId, "Unknown building type ID."); }

        internal VirtualApiResult QueueOperation(VirtualOperationKind kind, string typeId, int gameId, int tileX, int tileY, out VirtualOperationTicket ticket)
        {
            ticket = default(VirtualOperationTicket);
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Mutations are allowed only in a loaded singleplayer skirmish.");
            if ((kind == VirtualOperationKind.AssignUnit || kind == VirtualOperationKind.AssignBuilding || kind == VirtualOperationKind.RemoveUnitAssignment || kind == VirtualOperationKind.RemoveBuildingAssignment) && gameId <= 0)
                return Result(VirtualApiResultCode.InvalidGameId, "Game ID must be 1-based and positive.");
            if ((kind == VirtualOperationKind.SpawnUnit || kind == VirtualOperationKind.AssignUnit) && !unitDefinitions.ContainsKey(typeId ?? string.Empty))
                return Result(VirtualApiResultCode.UnknownTypeId, "Unknown unit type ID.");
            if ((kind == VirtualOperationKind.SpawnBuilding || kind == VirtualOperationKind.AssignBuilding) && !buildingDefinitions.ContainsKey(typeId ?? string.Empty))
                return Result(VirtualApiResultCode.UnknownTypeId, "Unknown building type ID.");
            long requestId = Interlocked.Increment(ref nextOperationId);
            ticket = new VirtualOperationTicket(requestId, kind);
            operationQueue.Enqueue(new OperationRequest(ticket, typeId, gameId, tileX, tileY));
            Shared.DebugLogHelper.LogInfo(log, $"Queued virtual operation: ticket={ticket}, gameId={gameId}, tile={tileX},{tileY}, callerThread={Thread.CurrentThread.ManagedThreadId}.");
            return Result(VirtualApiResultCode.InitializationPending, $"Operation {ticket} is queued for the next simulation tick.");
        }

        private VirtualApiResult ExecuteAssignUnit(int unitId, string typeId, VirtualOperationTicket ticket, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Assignments are allowed only in a loaded singleplayer skirmish.");
            if (!unitDefinitions.TryGetValue(typeId ?? string.Empty, out VirtualUnitDefinition definition)) return Result(VirtualApiResultCode.UnknownTypeId, "Unknown unit type ID.");
            if (!TryReadUnit(unitId, definition.BaseType, out uint globalId, out int maxHealth, out int currentHealth, out int speed, out VirtualApiResult failure)) return failure;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* assignedUnit) || assignedUnit == null || assignedUnit->r_AliveState != AliveState.IsAlive)
                return Result(VirtualApiResultCode.EntityNotFound, "Unit is not fully initialized.");
            PendingSpawn pending;
            lock (sync)
            {
                pendingUnits.Remove(unitId);
                if (instances.TryGet(UnitKind, unitId, globalId, out StoredInstance existing) && existing.TypeId == typeId) { snapshot = existing.Snapshot; return VirtualApiResult.Success("Unit is already assigned."); }
                RestoreExistingSlot(VirtualEntityKind.Unit, unitId);
                pending = PendingSpawn.ForUnit(unitId, globalId, typeId, definition.DefinitionVersion, assignedUnit->r_ControllableForPlayerId,
                    assignedUnit->r_CurrentTilePositionX, assignedUnit->r_CurrentTilePositionY, maxHealth, currentHealth, speed,
                    assignedUnit->r_AliveState, currentSimulationTick + SpawnInitializationTickBudget, ticket);
                pendingUnits[unitId] = pending; snapshot = pending.Snapshot;
            }
            LogPending(pending, "existing live unit queued for visual validation");
            return Result(VirtualApiResultCode.InitializationPending, $"Unit {unitId}/{globalId} is waiting for visual validation.");
        }

        private VirtualApiResult ExecuteAssignBuilding(int buildingId, string typeId, VirtualOperationTicket ticket, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Assignments are allowed only in a loaded singleplayer skirmish.");
            if (!buildingDefinitions.TryGetValue(typeId ?? string.Empty, out VirtualBuildingDefinition definition)) return Result(VirtualApiResultCode.UnknownTypeId, "Unknown building type ID.");
            if (!TryReadBuilding(buildingId, definition.BaseType, out uint globalId, out int maxHealth, out int currentHealth, out VirtualApiResult failure)) return failure;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* assignedBuilding) || assignedBuilding == null || assignedBuilding->r_AliveState != AliveState.IsAlive)
                return Result(VirtualApiResultCode.EntityNotFound, "Building is not fully initialized.");
            PendingSpawn pending;
            lock (sync)
            {
                pendingBuildings.Remove(buildingId);
                if (instances.TryGet(BuildingKind, buildingId, globalId, out StoredInstance existing) && existing.TypeId == typeId) { snapshot = existing.Snapshot; return VirtualApiResult.Success("Building is already assigned."); }
                RestoreExistingSlot(VirtualEntityKind.Building, buildingId);
                pending = PendingSpawn.ForBuilding(buildingId, globalId, typeId, definition.DefinitionVersion, assignedBuilding->r_PlayerIdOwner,
                    assignedBuilding->r_TilePositionXBegin, assignedBuilding->r_TilePositionYBegin, maxHealth, currentHealth,
                    assignedBuilding->r_AliveState, currentSimulationTick + SpawnInitializationTickBudget, ticket);
                pendingBuildings[buildingId] = pending; snapshot = pending.Snapshot;
            }
            RefreshBuilding(buildingId);
            LogPending(pending, "existing live building queued for visual validation");
            return Result(VirtualApiResultCode.InitializationPending, $"Building {buildingId}/{globalId} is waiting for visual validation.");
        }

        private VirtualApiResult ExecuteRemove(VirtualEntityKind kind, int gameId, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Removal is allowed only in a loaded singleplayer skirmish.");
            if (gameId <= 0) return Result(VirtualApiResultCode.InvalidGameId, "Game ID must be 1-based and positive.");
            StoredInstance stored;
            lock (sync)
            {
                if (!instances.TryGetSlot((byte)kind, gameId, out uint globalId, out stored)) return Result(VirtualApiResultCode.EntityNotFound, "No assignment exists for this slot.");
                if (!IdentityMatches(stored)) { instances.Remove((byte)kind, gameId); return Result(VirtualApiResultCode.GlobalIdMismatch, "The native slot was reused; stale assignment removed."); }
                Restore(stored); instances.Remove((byte)kind, gameId);
            }
            snapshot = stored.Snapshot;
            if (kind == VirtualEntityKind.Building) RefreshBuilding(gameId);
            else unitTintRestoreQueue.Enqueue(gameId);
            VirtualApiResult success = VirtualApiResult.Success("Assignment removed."); VirtualEntityApi.RaiseRemoved(stored.Snapshot, success); return success;
        }

        internal VirtualApiResult TryGetInstance(VirtualEntityKind kind, int gameId, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (gameId <= 0) return Result(VirtualApiResultCode.InvalidGameId, "Game ID must be 1-based and positive.");
            lock (sync)
            {
                if (!instances.TryGetSlot((byte)kind, gameId, out uint ignored, out StoredInstance stored)) return Result(VirtualApiResultCode.EntityNotFound, "No assignment exists for this slot.");
                if (!IdentityMatches(stored)) { instances.Remove((byte)kind, gameId); return Result(VirtualApiResultCode.GlobalIdMismatch, "The native slot was reused; stale assignment removed."); }
                snapshot = stored.Snapshot; return VirtualApiResult.Success();
            }
        }

        internal VirtualApiResult GetSelectedVirtualUnits(out IReadOnlyList<VirtualUnitSelectionSnapshot> selection)
        {
            selection = Array.Empty<VirtualUnitSelectionSnapshot>();
            try
            {
                SelectedUnitInfo[] selected = GamePlayerManagerAPI.Instance?.GetSelectedChimps() ?? Array.Empty<SelectedUnitInfo>();
                var grouped = new Dictionary<string, List<VirtualEntityInstance>>(StringComparer.Ordinal);
                foreach (SelectedUnitInfo item in selected)
                {
                    if (!TryGetValidatedUnit(item.UnitId, out VirtualEntityInstance instance, out VirtualUnitDefinition definition) ||
                        !definition.PresentationProfile.ShowAsDistinctCategory) continue;
                    if (!grouped.TryGetValue(definition.TypeId, out List<VirtualEntityInstance> list))
                        grouped.Add(definition.TypeId, list = new List<VirtualEntityInstance>());
                    list.Add(instance);
                }
                selection = grouped.OrderBy(x => x.Key).Select(x => new VirtualUnitSelectionSnapshot(
                    x.Key, unitDefinitions[x.Key].DisplayName, x.Value.ToArray())).ToArray();
                return VirtualApiResult.Success();
            }
            catch (Exception ex) { return Result(VirtualApiResultCode.InternalError, $"Selection query failed: {ex.Message}"); }
        }

        internal bool TryGetValidatedUnit(int unitId, out VirtualEntityInstance instance, out VirtualUnitDefinition definition)
        {
            instance = null; definition = null;
            if (unitId <= 0) return false;
            lock (sync)
            {
                if (!instances.TryGetSlot(UnitKind, unitId, out uint globalId, out StoredInstance stored) || !stored.VisualValidated ||
                    !unitDefinitions.TryGetValue(stored.TypeId, out definition)) return false;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                    unit->r_GlobalId != globalId || unit->r_UnitChimp != definition.BaseType || unit->r_AliveState != AliveState.IsAlive)
                { instances.Remove(UnitKind, unitId); instance = null; definition = null; return false; }
                instance = stored.Snapshot; return true;
            }
        }

        private void RegisterUnitHudPresentation(IApiShared api)
        {
            if (!api.TryGetUnitHudPresentation(VirtualUnitsPlugin.PluginGuid, out IUnitHudPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                Shared.DebugLogHelper.LogError(log, $"Central unit-HUD capability unavailable: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            var category = new UnitHudCategoryDefinition(
                VirtualUnitsPlugin.DesertArcherId,
                "Desert Archer",
                (int)eChimps.CHIMP_TYPE_ARCHER,
                UnitHudSurface.All,
                null,
                new UnitHudTint(64, 128, byte.MaxValue, 115),
                0,
                new UnitHudTextProfile("Desert Archer", "DA", "A tougher variant of the European archer.", ResolveDesertArcherText));
            if (!capability.TryRegisterCategory(category, snapshot =>
            {
                if (!TryGetValidatedUnit(snapshot.GameId, out VirtualEntityInstance instance, out VirtualUnitDefinition definition))
                    return false;
                return instance.Key.GlobalId == snapshot.GlobalId &&
                    definition.TypeId == VirtualUnitsPlugin.DesertArcherId &&
                    (int)definition.BaseType == snapshot.VanillaType;
            }, out diagnostic))
            {
                Shared.DebugLogHelper.LogError(log, $"Desert Archer HUD registration failed: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            if (!capability.TryRegisterRecruitment(VirtualUnitsPlugin.DesertArcherId, BeginRecruitment, out diagnostic))
            {
                Shared.DebugLogHelper.LogError(log, $"Desert Archer recruitment registration failed: state={diagnostic?.State}, reason={diagnostic?.Reason}");
                return;
            }
            unitHudPresentation = capability;
            capability.RequestRefresh();
            Shared.DebugLogHelper.LogInfo(log, "Desert Archer registered with APIShared unit-HUD presentation.");
        }

        private static string ResolveDesertArcherText(UnitHudTextKind kind)
        {
            string locale = string.Empty;
            try
            {
                Type assetApiType = Type.GetType("SHCDESE.API.GameAssetManagerAPI, SHCDESE", false);
                object assets = assetApiType?.GetProperty("Instance")?.GetValue(null, null);
                locale = assetApiType?.GetProperty("CurrentLanguage")?.GetValue(assets, null) as string ?? string.Empty;
            }
            catch { }
            bool german = locale.StartsWith("de", StringComparison.OrdinalIgnoreCase);
            if (kind == UnitHudTextKind.ShortLabel) return "DA";
            if (kind == UnitHudTextKind.Description) return german
                ? "Eine widerstandsfähigere Variante des europäischen Bogenschützen."
                : "A tougher variant of the European archer.";
            return german ? "Wüstenbogenschütze" : "Desert Archer";
        }

        private bool BeginRecruitment(UnitHudRecruitmentTicket ticket)
        {
            if (ticket == null || !CanMutate || ticket.CategoryId != VirtualUnitsPlugin.DesertArcherId ||
                ticket.BaseUnitType != (int)eChimps.CHIMP_TYPE_ARCHER || ticket.PlayerId <= 0 || ticket.RequestedAmount <= 0)
                return false;
            lock (sync)
            {
                if (pendingRecruitment != null) return false;
                pendingRecruitment = new PendingRecruitment(ticket, currentSimulationTick + RecruitmentCorrelationTickBudget);
            }
            Shared.DebugLogHelper.LogInfo(log, $"Desert Archer recruitment armed: ticket={ticket.TicketId}, player={ticket.PlayerId}, requested={ticket.RequestedAmount}, tick={currentSimulationTick}.");
            return true;
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            PendingRecruitment pending;
            lock (sync) pending = pendingRecruitment;
            if (pending == null || args.Source != UnitTransitionSource.EuropeanBarracks ||
                args.PlayerOwnerId != pending.Ticket.PlayerId || args.NextUnitType != eChimps.CHIMP_TYPE_ARCHER || args.UnitId <= 0)
                return;
            recruitmentTransitions.Enqueue(new RecruitmentTransition(pending.Ticket.TicketId, args.UnitId));
        }

        internal VirtualEntityInstance[] GetValidatedUnitsForPlayer(int playerId)
        {
            var result = new List<VirtualEntityInstance>();
            StoredInstance[] candidates;
            lock (sync) candidates = instances.Values.Where(x => x.Kind == VirtualEntityKind.Unit).ToArray();
            foreach (StoredInstance candidate in candidates)
            {
                if (!TryGetValidatedUnit(candidate.GameId, out VirtualEntityInstance instance, out VirtualUnitDefinition ignored)) continue;
                if (GameUnitManagerAPI.Instance.TryGetUnitById(candidate.GameId, out GameUnit* unit) && unit != null && unit->r_ControllableForPlayerId == playerId)
                    result.Add(instance);
            }
            return result.ToArray();
        }

        internal bool TryGetUnitDefinitionForInstance(VirtualEntityInstance instance, out VirtualUnitDefinition definition)
            => TryGetDefinition(instance?.TypeId, out definition).Succeeded;

        private VirtualApiResult ExecuteSpawnUnit(string typeId, int tileX, int tileY, VirtualOperationTicket ticket, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Spawning is allowed only in a loaded singleplayer skirmish.");
            if (!unitDefinitions.TryGetValue(typeId ?? string.Empty, out VirtualUnitDefinition definition) || !definition.SpawnOptions.AllowDiagnosticSpawn) return Result(VirtualApiResultCode.UnknownTypeId, "Unknown or non-spawnable unit type.");
            if (!TryValidateTile(tileX, tileY, true, out int tileId)) return Result(VirtualApiResultCode.InvalidPlacement, "Target tile is outside the map, occupied, or not walkable.");
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (playerId <= 0) return Result(VirtualApiResultCode.UnsupportedGameMode, "No unambiguous local player is available.");
            try
            {
                long created = GameUnitManagerAPI.Instance.CreateUnitLocal(playerId, playerId, tileX, tileY, GameTileManagerAPI.Instance.GetTileHeight(tileId), definition.BaseType);
                if (created <= 0 || created > int.MaxValue) return Result(VirtualApiResultCode.SpawnFailed, $"CreateUnitLocal returned invalid ID {created}.");
                int unitId = (int)created;
                if (!TryReadUnit(unitId, definition.BaseType, out uint globalId, out int maxHealth, out int currentHealth, out int speed, out VirtualApiResult failure))
                {
                    GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId);
                    return failure;
                }
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null || unit->r_ControllableForPlayerId != playerId)
                {
                    GameUnitManagerAPI.Instance.DeleteUnitSafe(unitId);
                    return Result(VirtualApiResultCode.SpawnFailed, $"Spawned unit {unitId} has an unexpected owner.");
                }
                var pending = PendingSpawn.ForUnit(unitId, globalId, typeId, definition.DefinitionVersion, playerId, tileX, tileY, maxHealth, currentHealth, speed, unit->r_AliveState, currentSimulationTick + SpawnInitializationTickBudget, ticket);
                lock (sync) pendingUnits[unitId] = pending;
                snapshot = pending.Snapshot;
                LogPending(pending, $"CreateUnitLocal={created}, native=[{DescribeUnit(unit)}]");
                return Result(VirtualApiResultCode.InitializationPending, $"Unit {unitId}/{globalId} is waiting for Vanilla initialization.");
            }
            catch (Exception ex) { LogError($"Unit spawn failed: {ex}"); return Result(VirtualApiResultCode.InternalError, "Unit spawn raised an internal error."); }
        }

        private VirtualApiResult ExecuteSpawnBuilding(string typeId, int tileX, int tileY, VirtualOperationTicket ticket, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Spawning is allowed only in a loaded singleplayer skirmish.");
            if (!buildingDefinitions.TryGetValue(typeId ?? string.Empty, out VirtualBuildingDefinition definition) || !definition.SpawnOptions.AllowDiagnosticSpawn) return Result(VirtualApiResultCode.UnknownTypeId, "Unknown or non-spawnable building type.");
            if (!TryValidateTile(tileX, tileY, false, out int ignoredTileId)) return Result(VirtualApiResultCode.InvalidPlacement, "Target tile is outside the map.");
            int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (playerId <= 0) return Result(VirtualApiResultCode.UnsupportedGameMode, "No unambiguous local player is available.");
            try
            {
                pendingBuilding = new PendingBuildingSpawn(playerId, tileX, tileY, definition.BaseType);
                long result;
                try { result = GameBuildingManagerAPI.Instance.CreatePrefab(playerId, tileX, tileY, definition.Mapper, definition.BuildingScale, 0, true, false); }
                finally { pendingBuilding.Active = false; }
                int buildingId = pendingBuilding.CapturedBuildingId;
                pendingBuilding = default(PendingBuildingSpawn);
                string resultDiagnostic = FormatBuildingResult(result);
                if (buildingId <= 0) return Result(VirtualApiResultCode.SpawnFailed, $"CreatePrefab did not produce a correlated building ({resultDiagnostic}, eventId={buildingId}).");
                if (!TryReadBuilding(buildingId, definition.BaseType, out uint globalId, out int maxHealth, out int currentHealth, out VirtualApiResult failure))
                {
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(buildingId);
                    return failure;
                }
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null || building->r_PlayerIdOwner != playerId)
                {
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(buildingId);
                    return Result(VirtualApiResultCode.SpawnFailed, $"Spawned building {buildingId} has an unexpected owner.");
                }
                var pending = PendingSpawn.ForBuilding(buildingId, globalId, typeId, definition.DefinitionVersion, playerId, tileX, tileY, maxHealth, currentHealth, building->r_AliveState, currentSimulationTick + SpawnInitializationTickBudget, ticket);
                lock (sync) pendingBuildings[buildingId] = pending;
                snapshot = pending.Snapshot;
                LogPending(pending, $"{resultDiagnostic}, native=[{DescribeBuilding(building)}], linkedTiles={CountLinkedFootprintTiles(buildingId, building)}");
                return Result(VirtualApiResultCode.InitializationPending, $"Building {buildingId}/{globalId} is waiting for Vanilla initialization.");
            }
            catch (Exception ex) { pendingBuilding = default(PendingBuildingSpawn); LogError($"Building spawn failed: {ex}"); return Result(VirtualApiResultCode.InternalError, "Building spawn raised an internal error."); }
        }

        internal bool TryResolveUnitVisual(int unitId, out VirtualUnitDefinition definition)
        {
            definition = null;
            if (unitId <= 0) return false;
            lock (sync)
            {
                if (pendingUnits.TryGetValue(unitId, out PendingSpawn pending))
                    return unitDefinitions.TryGetValue(pending.TypeId, out definition);
                if (!instances.TryGetSlot(UnitKind, unitId, out uint ignored, out StoredInstance stored) || !stored.VisualValidated) return false;
                return unitDefinitions.TryGetValue(stored.TypeId, out definition);
            }
        }

        internal bool TryResolveBuildingVisual(int buildingId, out VirtualBuildingDefinition definition)
        {
            definition = null;
            if (buildingId <= 0) return false;
            lock (sync)
            {
                if (pendingBuildings.TryGetValue(buildingId, out PendingSpawn pending))
                    return buildingDefinitions.TryGetValue(pending.TypeId, out definition);
                if (!instances.TryGetSlot(BuildingKind, buildingId, out uint ignored, out StoredInstance stored) || !stored.VisualValidated) return false;
                return buildingDefinitions.TryGetValue(stored.TypeId, out definition);
            }
        }

        internal bool MayHaveUnitVisual(int unitId)
        {
            lock (sync) return pendingUnits.ContainsKey(unitId) || instances.TryGetSlot(UnitKind, unitId, out uint ignored, out StoredInstance stored) && stored.VisualValidated;
        }

        internal uint GetKnownVisualGlobalId(VirtualEntityKind kind, int gameId)
        {
            lock (sync)
            {
                Dictionary<int, PendingSpawn> pending = kind == VirtualEntityKind.Unit ? pendingUnits : pendingBuildings;
                if (pending.TryGetValue(gameId, out PendingSpawn pendingSpawn)) return pendingSpawn.GlobalId;
                return instances.TryGetSlot((byte)kind, gameId, out uint globalId, out StoredInstance ignored) ? globalId : 0;
            }
        }

        private void OnSimulationTick(int simulationTick)
        {
            currentSimulationTick = simulationTick;
            if (Interlocked.CompareExchange(ref simulationThreadId, Thread.CurrentThread.ManagedThreadId, 0) == 0)
                Shared.DebugLogHelper.LogInfo(log, $"Simulation mutation context established: thread={simulationThreadId}, tick={simulationTick}.");
            if (pendingRestore != null && CanMutate) RestorePending();
            if (!CanMutate) return;
            while (operationQueue.TryDequeue(out OperationRequest request)) ExecuteOperation(request);
            ProcessRecruitmentTransitions();
            ValidateActiveInstances();
            ProcessPendingUnits();
            ProcessPendingBuildings();
        }

        private void ProcessRecruitmentTransitions()
        {
            PendingRecruitment active;
            lock (sync)
            {
                active = pendingRecruitment;
                while (recruitmentTransitions.TryDequeue(out RecruitmentTransition transition))
                {
                    if (active == null || transition.TicketId != active.Ticket.TicketId) continue;
                    active.Candidates.Add(transition.UnitId);
                    active.LastTransitionTick = currentSimulationTick;
                }
            }
            if (active == null) return;

            foreach (int unitId in active.Candidates.ToArray())
            {
                if (!TryReadUnit(unitId, eChimps.CHIMP_TYPE_ARCHER, out uint globalId, out int maxHealth,
                    out int currentHealth, out int speed, out VirtualApiResult ignored)) continue;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                    unit->r_ControllableForPlayerId != active.Ticket.PlayerId) continue;
                lock (sync)
                {
                    if (!ReferenceEquals(active, pendingRecruitment) || pendingUnits.ContainsKey(unitId) ||
                        instances.TryGetSlot(UnitKind, unitId, out uint ignoredGlobal, out StoredInstance ignoredStored))
                        continue;
                    var pending = PendingSpawn.ForUnit(unitId, globalId, VirtualUnitsPlugin.DesertArcherId, 1,
                        active.Ticket.PlayerId, unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY,
                        maxHealth, currentHealth, speed, unit->r_AliveState,
                        currentSimulationTick + SpawnInitializationTickBudget, default(VirtualOperationTicket), true);
                    pendingUnits[unitId] = pending;
                    active.Candidates.Remove(unitId);
                    active.MatchedUnitIds.Add(unitId);
                    LogPending(pending, "correlated Vanilla EuropeanBarracks transition");
                }
            }

            bool complete;
            lock (sync)
            {
                complete = ReferenceEquals(active, pendingRecruitment) && RecruitmentCorrelationPolicy.ShouldComplete(
                    active.MatchedUnitIds.Count, active.Ticket.RequestedAmount, active.Candidates.Count,
                    currentSimulationTick, active.LastTransitionTick, active.DeadlineTick, RecruitmentQuietTickBudget);
                if (complete) pendingRecruitment = null;
            }
            if (!complete) return;
            string reason = active.MatchedUnitIds.Count >= active.Ticket.RequestedAmount
                ? "expected transition count reached"
                : currentSimulationTick >= active.DeadlineTick ? "bounded correlation window elapsed" : "transition batch became quiet";
            if (unitHudPresentation != null && !unitHudPresentation.TryCompleteRecruitment(active.Ticket,
                active.MatchedUnitIds.Count, reason, out NativeCapabilityDiagnostic diagnostic))
                LogWarning($"Recruitment ticket completion was rejected: state={diagnostic?.State}, reason={diagnostic?.Reason}");
            Shared.DebugLogHelper.LogInfo(log, $"Desert Archer recruitment completed: ticket={active.Ticket.TicketId}, requested={active.Ticket.RequestedAmount}, matched={active.MatchedUnitIds.Count}, reason={reason}.");
        }

        internal void DrainMainThreadWork()
        {
            while (visualResetQueue.TryDequeue(out bool ignoredReset)) { visuals?.ClearBindings(); spawnController?.ResetForMapLifecycle(); unitHudPresentation?.RequestRefresh(); }
            while (unitTintRestoreQueue.TryDequeue(out int unitId)) visuals?.RestoreUnitTint(unitId);
            while (availabilityQueue.TryDequeue(out bool available)) spawnController?.ApplyAvailability(available);
            while (completionQueue.TryDequeue(out OperationCompletion completion))
            {
                spawnController?.ReportRuntimeResult(completion.Result);
                VirtualEntityApi.RaiseOperationCompleted(completion.Ticket, completion.Result, completion.Instance);
            }
        }

        private void ExecuteOperation(OperationRequest request)
        {
            Shared.DebugLogHelper.LogInfo(log, $"Executing virtual operation: ticket={request.Ticket}, simulationThread={Thread.CurrentThread.ManagedThreadId}, tick={currentSimulationTick}.");
            VirtualEntityInstance instance = null;
            VirtualApiResult result;
            try
            {
                switch (request.Ticket.Kind)
                {
                    case VirtualOperationKind.SpawnUnit: result = ExecuteSpawnUnit(request.TypeId, request.TileX, request.TileY, request.Ticket, out instance); break;
                    case VirtualOperationKind.SpawnBuilding: result = ExecuteSpawnBuilding(request.TypeId, request.TileX, request.TileY, request.Ticket, out instance); break;
                    case VirtualOperationKind.AssignUnit: result = ExecuteAssignUnit(request.GameId, request.TypeId, request.Ticket, out instance); break;
                    case VirtualOperationKind.AssignBuilding: result = ExecuteAssignBuilding(request.GameId, request.TypeId, request.Ticket, out instance); break;
                    case VirtualOperationKind.RemoveUnitAssignment: result = ExecuteRemove(VirtualEntityKind.Unit, request.GameId, out instance); break;
                    case VirtualOperationKind.RemoveBuildingAssignment: result = ExecuteRemove(VirtualEntityKind.Building, request.GameId, out instance); break;
                    default: result = Result(VirtualApiResultCode.InternalError, "Unknown virtual operation."); break;
                }
            }
            catch (Exception ex)
            {
                LogError($"Virtual operation {request.Ticket} failed closed: {ex}");
                result = Result(VirtualApiResultCode.InternalError, $"Operation {request.Ticket} raised an internal error.");
            }
            if (result.Code != VirtualApiResultCode.InitializationPending) CompleteOperation(request.Ticket, result, instance);
        }

        private void CompleteOperation(VirtualOperationTicket ticket, VirtualApiResult result, VirtualEntityInstance instance)
        {
            if (ticket.RequestId > 0) completionQueue.Enqueue(new OperationCompletion(ticket, result, instance));
        }

        internal void RecordUnitRendererBinding(int unitId)
        {
            if (unitId <= 0) return;
            lock (sync)
            {
                if (!pendingUnits.TryGetValue(unitId, out PendingSpawn pending)) return;
                pending.RendererSeen = true;
                if (!pending.RendererLogged)
                {
                    pending.RendererLogged = true;
                    Shared.DebugLogHelper.LogInfo(log, $"Pending unit renderer bound: unitId={unitId}, globalId={pending.GlobalId}, type={pending.TypeId}.");
                }
            }
        }

        internal void RecordUnitVisualHook(int unitId, bool tintApplied)
        {
            lock (sync)
            {
                if (!pendingUnits.TryGetValue(unitId, out PendingSpawn pending)) return;
                pending.UnitHookSeen |= tintApplied;
            }
        }

        internal void RecordBuildingVisualHook(int buildingId, bool vanillaSpriteUsable)
        {
            lock (sync)
            {
                if (!pendingBuildings.TryGetValue(buildingId, out PendingSpawn pending)) return;
                pending.BuildingHookSeen |= vanillaSpriteUsable;
            }
        }

        internal void RecordBuildingTintHook(int buildingId)
        {
            lock (sync)
            {
                if (pendingBuildings.TryGetValue(buildingId, out PendingSpawn pending)) pending.BuildingTintSeen = true;
            }
        }

        private void ProcessPendingUnits()
        {
            PendingSpawn[] candidates;
            lock (sync) candidates = pendingUnits.Values.ToArray();
            foreach (PendingSpawn pending in candidates)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(pending.GameId, out GameUnit* unit) || unit == null)
                {
                    FailPending(pending, "native unit slot is no longer available", false);
                    continue;
                }
                bool identityMatches = unit->r_GlobalId == pending.GlobalId;
                if (!identityMatches || unit->r_UnitChimp != unitDefinitions[pending.TypeId].BaseType || unit->r_ControllableForPlayerId != pending.PlayerId)
                {
                    FailPending(pending, $"identity changed: global={unit->r_GlobalId}, type={unit->r_UnitChimp}, owner={unit->r_ControllableForPlayerId}", identityMatches);
                    continue;
                }
                LogStateTransition(pending, unit->r_AliveState, DescribeUnit(unit));
                if (unit->r_AliveState == AliveState.IsAlive)
                {
                    int currentTileId = unchecked((int)unit->r_CurrentPositionTileId);
                    bool positionValid = GameTileManagerAPI.Instance.IsTileInsideMapBounds(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY) &&
                        currentTileId == GameTileManagerAPI.Instance.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                    if (SpawnInitializationPolicy.CanFinalize(identityMatches, true, positionValid, true, pending.RendererSeen && pending.UnitHookSeen))
                    {
                        FinalizeUnit(pending, unit);
                        continue;
                    }
                }
                else if (unit->r_AliveState != AliveState.NeedsInit)
                {
                    FailPending(pending, $"unexpected alive state {unit->r_AliveState}", true);
                    continue;
                }
                if (SpawnInitializationPolicy.HasTimedOut(currentSimulationTick, pending.DeadlineTick))
                {
                    FailPending(pending, $"initialization timeout: {DescribeUnit(unit)}, rendererSeen={pending.RendererSeen}, bodyHookSeen={pending.UnitHookSeen}", true);
                }
            }
        }

        private void ProcessPendingBuildings()
        {
            PendingSpawn[] candidates;
            lock (sync) candidates = pendingBuildings.Values.ToArray();
            foreach (PendingSpawn pending in candidates)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(pending.GameId, out GameBuilding* building) || building == null)
                {
                    FailPending(pending, "native building slot is no longer available", false);
                    continue;
                }
                bool identityMatches = building->r_GlobalId == pending.GlobalId;
                if (!identityMatches || building->r_BuildingType != buildingDefinitions[pending.TypeId].BaseType || building->r_PlayerIdOwner != pending.PlayerId)
                {
                    FailPending(pending, $"identity changed: global={building->r_GlobalId}, type={building->r_BuildingType}, owner={building->r_PlayerIdOwner}", identityMatches);
                    continue;
                }
                LogStateTransition(pending, building->r_AliveState, $"{DescribeBuilding(building)}, linkedTiles={CountLinkedFootprintTiles(pending.GameId, building)}");
                if (building->r_AliveState == AliveState.IsAlive)
                {
                    int linkedTiles = CountLinkedFootprintTiles(pending.GameId, building);
                    if (SpawnInitializationPolicy.CanFinalize(identityMatches, true, linkedTiles > 0, true, pending.BuildingHookSeen && pending.BuildingTintSeen))
                    {
                        FinalizeBuilding(pending, building, linkedTiles);
                        continue;
                    }
                }
                else if (building->r_AliveState != AliveState.NeedsInit)
                {
                    FailPending(pending, $"unexpected alive state {building->r_AliveState}", true);
                    continue;
                }
                if (SpawnInitializationPolicy.HasTimedOut(currentSimulationTick, pending.DeadlineTick))
                {
                    FailPending(pending, $"initialization timeout: {DescribeBuilding(building)}, linkedTiles={CountLinkedFootprintTiles(pending.GameId, building)}, tileHookSeen={pending.BuildingHookSeen}, tintSeen={pending.BuildingTintSeen}", true);
                }
            }
        }

        private void FinalizeUnit(PendingSpawn pending, GameUnit* unit)
        {
            VirtualUnitDefinition definition = unitDefinitions[pending.TypeId];
            int currentHealth = GameUnitManagerAPI.Instance.GetCurrentHealth(pending.GameId);
            var stored = new StoredInstance(VirtualEntityKind.Unit, pending.GameId, pending.GlobalId, pending.TypeId, pending.DefinitionVersion, pending.OriginalMaxHealth, pending.OriginalSpeed);
            ApplyUnitStats(pending.GameId, pending.OriginalMaxHealth, currentHealth, pending.OriginalSpeed, definition);
            lock (sync) { pendingUnits.Remove(pending.GameId); instances.Set(UnitKind, pending.GameId, pending.GlobalId, stored); }
            var success = VirtualApiResult.Success("Unit assignment finalized after Vanilla initialization.");
            Shared.DebugLogHelper.LogInfo(log, $"Pending unit finalized: {DescribeUnit(unit)}, rendererSeen={pending.RendererSeen}, bodyHookSeen={pending.UnitHookSeen}, maxHealth={GameUnitManagerAPI.Instance.GetMaxHealth(pending.GameId)}, currentHealth={GameUnitManagerAPI.Instance.GetCurrentHealth(pending.GameId)}, speed={GameUnitManagerAPI.Instance.GetSpeed(pending.GameId)}.");
            VirtualEntityApi.RaiseAssigned(stored.Snapshot, success);
            CompleteOperation(pending.Ticket, success, stored.Snapshot);
        }

        private void FinalizeBuilding(PendingSpawn pending, GameBuilding* building, int linkedTiles)
        {
            VirtualBuildingDefinition definition = buildingDefinitions[pending.TypeId];
            int currentHealth = GameBuildingManagerAPI.Instance.GetCurrentHealth(pending.GameId);
            var stored = new StoredInstance(VirtualEntityKind.Building, pending.GameId, pending.GlobalId, pending.TypeId, pending.DefinitionVersion, pending.OriginalMaxHealth, 0);
            ApplyBuildingStats(pending.GameId, pending.OriginalMaxHealth, currentHealth, definition);
            lock (sync) { pendingBuildings.Remove(pending.GameId); instances.Set(BuildingKind, pending.GameId, pending.GlobalId, stored); }
            RefreshBuilding(pending.GameId);
            var success = VirtualApiResult.Success("Building assignment finalized after Vanilla initialization.");
            Shared.DebugLogHelper.LogInfo(log, $"Pending building finalized: {DescribeBuilding(building)}, linkedTiles={linkedTiles}, tileHookSeen={pending.BuildingHookSeen}, tintSeen={pending.BuildingTintSeen}, maxHealth={GameBuildingManagerAPI.Instance.GetMaxHealth(pending.GameId)}, currentHealth={GameBuildingManagerAPI.Instance.GetCurrentHealth(pending.GameId)}.");
            VirtualEntityApi.RaiseAssigned(stored.Snapshot, success);
            CompleteOperation(pending.Ticket, success, stored.Snapshot);
        }

        private void FailPending(PendingSpawn pending, string reason, bool deleteIfIdentityStillMatches)
        {
            lock (sync) (pending.Kind == VirtualEntityKind.Unit ? pendingUnits : pendingBuildings).Remove(pending.GameId);
            bool deleteIssued = false;
            if (!pending.PreserveOnFailure && deleteIfIdentityStillMatches && PendingIdentityMatches(pending))
                deleteIssued = pending.Kind == VirtualEntityKind.Unit ? GameUnitManagerAPI.Instance.DeleteUnitSafe(pending.GameId) : GameBuildingManagerAPI.Instance.DeleteBuildingSafe(pending.GameId);
            var failure = Result(VirtualApiResultCode.SpawnFailed, $"{pending.Kind} {pending.GameId}/{pending.GlobalId} failed initialization: {reason}; safeDeleteIssued={deleteIssued}.");
            LogWarning(failure.Message);
            CompleteOperation(pending.Ticket, failure, null);
        }

        private bool PendingIdentityMatches(PendingSpawn pending) => pending.Kind == VirtualEntityKind.Unit
            ? GameUnitManagerAPI.Instance.GetGlobalId(pending.GameId) == unchecked((int)pending.GlobalId)
            : GameBuildingManagerAPI.Instance.GetGlobalId(pending.GameId) == unchecked((int)pending.GlobalId);

        private int CountLinkedFootprintTiles(int buildingId, GameBuilding* building)
        {
            if (building->r_TilePositionXBegin > building->r_TilePositionXEnd || building->r_TilePositionYBegin > building->r_TilePositionYEnd) return 0;
            int linked = 0;
            for (int y = building->r_TilePositionYBegin; y <= building->r_TilePositionYEnd; y++)
            for (int x = building->r_TilePositionXBegin; x <= building->r_TilePositionXEnd; x++)
            {
                if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y)) return 0;
                if (GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(x, y)) == buildingId) linked++;
            }
            return linked;
        }

        private void LogStateTransition(PendingSpawn pending, AliveState state, string details)
        {
            if (pending.LastState == state) return;
            pending.LastState = state;
            Shared.DebugLogHelper.LogInfo(log, $"Pending {pending.Kind} state transition: gameId={pending.GameId}, globalId={pending.GlobalId}, state={state}, simulationTick={currentSimulationTick}, {details}.");
        }

        private void LogPending(PendingSpawn pending, string creationResult) => Shared.DebugLogHelper.LogInfo(log,
            $"Pending {pending.Kind} created: ticket={pending.Ticket}, gameId={pending.GameId}, globalId={pending.GlobalId}, owner={pending.PlayerId}, type={pending.TypeId}, requestedTile={pending.RequestedX},{pending.RequestedY}, originalMaxHealth={pending.OriginalMaxHealth}, originalCurrentHealth={pending.OriginalCurrentHealth}, originalSpeed={pending.OriginalSpeed}, state={pending.LastState}, deadlineTick={pending.DeadlineTick}, rendererSeen={pending.RendererSeen}, {creationResult}.");

        private static string DescribeUnit(GameUnit* unit) => $"idGlobal={unit->r_GlobalId}, state={unit->r_AliveState}, type={unit->r_UnitChimp}, owner={unit->r_ControllableForPlayerId}, tile={unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}, tileId={unit->r_CurrentPositionTileId}, invisible={unit->r_IsInvisible}";
        private static string DescribeBuilding(GameBuilding* building) => $"idGlobal={building->r_GlobalId}, state={building->r_AliveState}, type={building->r_BuildingType}, owner={building->r_PlayerIdOwner}, footprint={building->r_TilePositionXBegin},{building->r_TilePositionYBegin}-{building->r_TilePositionXEnd},{building->r_TilePositionYEnd}, originTileId={building->r_TileIdBegin}";
        internal static string FormatBuildingResult(long result) => $"result={result}, low32={VirtualMath.HexLow32(result)}, signedLow32={VirtualMath.SignedLow32(result)}";
        internal IEnumerable<VirtualUnitDefinition> VisibleUnits() => unitDefinitions.Values.Where(x => x.SpawnOptions.ShowInDiagnosticMenu).OrderBy(x => x.TypeId).ToArray();
        internal IEnumerable<VirtualBuildingDefinition> VisibleBuildings() => buildingDefinitions.Values.Where(x => x.SpawnOptions.ShowInDiagnosticMenu).OrderBy(x => x.TypeId).ToArray();

        private void OnStartMap(MapStartEventArgs args) { SetMapMode(Shared.GameModeHelper.Capture(args)); }
        private void OnLoadSave(LoadSaveGameEventArgs args) { SetMapMode(Shared.GameModeHelper.Capture(args)); }
        private void SetMapMode(Shared.GameModeSnapshot mode)
        {
            lock (sync) { mapActive = true; modeAllowed = mode.IsSingleplayerSkirmish && !mode.IsRealMultiplayer && !mode.IsMapEditor; }
            availabilityQueue.Enqueue(modeAllowed);
            Shared.DebugLogHelper.LogInfo(log, $"Map lifecycle: diagnosticMutationAllowed={modeAllowed}; {mode.ToDiagnosticString()}.");
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!pendingBuilding.Active || args.Phase != EventHookPhase.Post || args.PlayerId != pendingBuilding.PlayerId || args.TileX != pendingBuilding.TileX || args.TileY != pendingBuilding.TileY || args.Building != pendingBuilding.Type || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
            pendingBuilding.CapturedBuildingId = (int)args.ReturnValue;
        }

        private byte[] Save(SaveContext context)
        {
            if (!context.IsSaveFile || context.IsMapEditorSave) return null;
            SaveRecord[] records;
            lock (sync)
            {
                records = instances.Values.Select(x => new SaveRecord { Kind = (byte)x.Kind, GameId = x.GameId, GlobalId = x.GlobalId, TypeId = x.TypeId, DefinitionVersion = x.DefinitionVersion, OriginalMaxHealth = x.OriginalMaxHealth, OriginalSpeed = x.OriginalSpeed }).ToArray();
            }
            return SaveCodec.Encode(records);
        }
        private void Load(byte[] data, LoadContext context)
        {
            if (!context.IsSaveFile) return;
            try { SavePayload payload = SaveCodec.DecodePayload(data); pendingRestore = payload.Records; Shared.DebugLogHelper.LogInfo(log, $"Loaded {pendingRestore.Count} pending virtual-entity records; ignored {payload.ControlGroups.Count} legacy control-group shadow records."); }
            catch (Exception ex) { pendingRestore = null; LogError($"Save data rejected: {ex}"); }
        }
        private void RestorePending()
        {
            List<SaveRecord> records = pendingRestore; pendingRestore = null;
            foreach (SaveRecord record in records)
            {
                VirtualEntityKind kind = record.Kind == UnitKind ? VirtualEntityKind.Unit : record.Kind == BuildingKind ? VirtualEntityKind.Building : 0;
                VirtualApiResult failure = ValidateRestore(record, kind);
                if (!failure.Succeeded)
                {
                    var rejected = new VirtualEntityInstance(new VirtualEntityKey(kind, record.GameId, record.GlobalId), record.TypeId, record.DefinitionVersion, record.OriginalMaxHealth, record.OriginalSpeed);
                    VirtualEntityApi.RaiseRestoreRejected(rejected, failure); LogWarning($"Restore rejected for {record.TypeId}/{record.GameId}/{record.GlobalId}: {failure}"); continue;
                }
                var stored = new StoredInstance(kind, record.GameId, record.GlobalId, record.TypeId, record.DefinitionVersion, record.OriginalMaxHealth, record.OriginalSpeed);
                lock (sync) instances.Set(record.Kind, record.GameId, record.GlobalId, stored);
                if (kind == VirtualEntityKind.Building) RefreshBuilding(record.GameId);
                VirtualEntityApi.RaiseAssigned(stored.Snapshot, VirtualApiResult.Success("Assignment restored without reapplying factors."));
            }
            unitHudPresentation?.RequestRefresh();
        }

        private void ValidateActiveInstances()
        {
            StoredInstance[] active;
            lock (sync) active = instances.Values.ToArray();
            foreach (StoredInstance stored in active)
            {
                bool valid;
                if (stored.Kind == VirtualEntityKind.Unit)
                {
                    valid = unitDefinitions.TryGetValue(stored.TypeId, out VirtualUnitDefinition definition) &&
                        GameUnitManagerAPI.Instance.TryGetUnitById(stored.GameId, out GameUnit* unit) && unit != null &&
                        unit->r_GlobalId == stored.GlobalId && unit->r_UnitChimp == definition.BaseType && unit->r_AliveState == AliveState.IsAlive;
                }
                else
                {
                    valid = buildingDefinitions.TryGetValue(stored.TypeId, out VirtualBuildingDefinition definition) &&
                        GameBuildingManagerAPI.Instance.TryGetBuildingById(stored.GameId, out GameBuilding* building) && building != null &&
                        building->r_GlobalId == stored.GlobalId && building->r_BuildingType == definition.BaseType && building->r_AliveState == AliveState.IsAlive;
                }
                lock (sync)
                {
                    stored.VisualValidated = valid;
                    if (!valid) instances.Remove((byte)stored.Kind, stored.GameId);
                }
            }
        }

        private VirtualApiResult ValidateRestore(SaveRecord record, VirtualEntityKind kind)
        {
            if (kind == 0 || record.GameId <= 0 || record.GlobalId == 0 || string.IsNullOrWhiteSpace(record.TypeId)) return Result(VirtualApiResultCode.SaveDefinitionIncompatible, "Malformed save record.");
            if (kind == VirtualEntityKind.Unit)
            {
                if (!unitDefinitions.TryGetValue(record.TypeId, out VirtualUnitDefinition definition) || definition.DefinitionVersion != record.DefinitionVersion) return Result(VirtualApiResultCode.SaveDefinitionIncompatible, "Unit definition is missing or version-incompatible.");
                if (!TryReadUnit(record.GameId, definition.BaseType, out uint global, out int a, out int b, out int c, out VirtualApiResult failure)) return failure;
                return global == record.GlobalId ? VirtualApiResult.Success() : Result(VirtualApiResultCode.GlobalIdMismatch, "Unit global ID differs.");
            }
            if (!buildingDefinitions.TryGetValue(record.TypeId, out VirtualBuildingDefinition building) || building.DefinitionVersion != record.DefinitionVersion) return Result(VirtualApiResultCode.SaveDefinitionIncompatible, "Building definition is missing or version-incompatible.");
            if (!TryReadBuilding(record.GameId, building.BaseType, out uint buildingGlobal, out int d, out int e, out VirtualApiResult buildingFailure)) return buildingFailure;
            return buildingGlobal == record.GlobalId ? VirtualApiResult.Success() : Result(VirtualApiResultCode.GlobalIdMismatch, "Building global ID differs.");
        }

        private bool TryReadUnit(int id, eChimps expected, out uint global, out int max, out int current, out int speed, out VirtualApiResult failure)
        {
            global = 0; max = current = speed = 0;
            if (id <= 0) { failure = Result(VirtualApiResultCode.InvalidGameId, "Unit ID must be 1-based and positive."); return false; }
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) || unit == null || (unit->r_AliveState != AliveState.NeedsInit && unit->r_AliveState != AliveState.IsAlive)) { failure = Result(VirtualApiResultCode.EntityNotFound, "Unit is not active."); return false; }
            if (unit->r_UnitChimp != expected) { failure = Result(VirtualApiResultCode.BaseTypeMismatch, "Unit base type differs from the definition."); return false; }
            global = unit->r_GlobalId; max = GameUnitManagerAPI.Instance.GetMaxHealth(id); current = GameUnitManagerAPI.Instance.GetCurrentHealth(id); speed = GameUnitManagerAPI.Instance.GetSpeed(id);
            if (global == 0 || max <= 0 || speed < 0) { failure = Result(VirtualApiResultCode.EntityNotFound, "Unit identity or base values are unavailable."); return false; }
            failure = VirtualApiResult.Success(); return true;
        }

        private bool TryReadBuilding(int id, eStructs expected, out uint global, out int max, out int current, out VirtualApiResult failure)
        {
            global = 0; max = current = 0;
            if (id <= 0) { failure = Result(VirtualApiResultCode.InvalidGameId, "Building ID must be 1-based and positive."); return false; }
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) || building == null || (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive)) { failure = Result(VirtualApiResultCode.EntityNotFound, "Building is not active."); return false; }
            if (building->r_BuildingType != expected) { failure = Result(VirtualApiResultCode.BaseTypeMismatch, "Building base type differs from the definition."); return false; }
            global = building->r_GlobalId; max = GameBuildingManagerAPI.Instance.GetMaxHealth(id); current = GameBuildingManagerAPI.Instance.GetCurrentHealth(id);
            if (global == 0 || max <= 0) { failure = Result(VirtualApiResultCode.EntityNotFound, "Building identity or base values are unavailable."); return false; }
            failure = VirtualApiResult.Success(); return true;
        }

        private void ApplyUnitStats(int id, int max, int current, int speed, VirtualUnitDefinition definition)
        {
            int newMax = VirtualMath.ScalePositive(max, definition.Stats.HealthFactor.Numerator, definition.Stats.HealthFactor.Denominator, int.MaxValue);
            int newCurrent = VirtualMath.ScaleHealth(current, max, newMax);
            int newSpeed = VirtualMath.ScaleMovementSpeed(speed, definition.Stats.SpeedFactor.Numerator, definition.Stats.SpeedFactor.Denominator, ushort.MaxValue);
            if (speed > 0 && newSpeed == speed && definition.Stats.SpeedFactor.Numerator != definition.Stats.SpeedFactor.Denominator && !unrepresentableSpeedLogged)
            {
                unrepresentableSpeedLogged = true;
                LogWarning($"Unit speed factor {definition.Stats.SpeedFactor.Numerator}/{definition.Stats.SpeedFactor.Denominator} is not representable for encoded Vanilla speed {speed}; preserving {speed}.");
            }
            GameUnitManagerAPI.Instance.SetMaxHealth(id, newMax); GameUnitManagerAPI.Instance.SetCurrentHealth(id, newCurrent); GameUnitManagerAPI.Instance.SetSpeed(id, (ushort)newSpeed);
        }
        private void ApplyBuildingStats(int id, int max, int current, VirtualBuildingDefinition definition)
        {
            int newMax = VirtualMath.ScalePositive(max, definition.Stats.HealthFactor.Numerator, definition.Stats.HealthFactor.Denominator, short.MaxValue);
            int newCurrent = VirtualMath.ScaleHealth(current, max, newMax);
            GameBuildingManagerAPI.Instance.SetMaxHealth(id, (ushort)newMax); GameBuildingManagerAPI.Instance.SetCurrentHealth(id, (short)newCurrent);
        }
        private void Restore(StoredInstance stored)
        {
            if (stored.Kind == VirtualEntityKind.Unit && TryReadUnit(stored.GameId, unitDefinitions[stored.TypeId].BaseType, out uint g, out int max, out int current, out int speed, out VirtualApiResult f) && g == stored.GlobalId)
            {
                int restoredCurrent = VirtualMath.ScaleHealth(current, max, stored.OriginalMaxHealth);
                GameUnitManagerAPI.Instance.SetMaxHealth(stored.GameId, stored.OriginalMaxHealth); GameUnitManagerAPI.Instance.SetCurrentHealth(stored.GameId, restoredCurrent); GameUnitManagerAPI.Instance.SetSpeed(stored.GameId, (ushort)Math.Min(ushort.MaxValue, stored.OriginalSpeed));
            }
            else if (stored.Kind == VirtualEntityKind.Building && TryReadBuilding(stored.GameId, buildingDefinitions[stored.TypeId].BaseType, out uint bg, out int bmax, out int bcurrent, out VirtualApiResult bf) && bg == stored.GlobalId)
            {
                int restoredCurrent = VirtualMath.ScaleHealth(bcurrent, bmax, Math.Min(short.MaxValue, stored.OriginalMaxHealth));
                GameBuildingManagerAPI.Instance.SetMaxHealth(stored.GameId, (ushort)Math.Min(ushort.MaxValue, stored.OriginalMaxHealth)); GameBuildingManagerAPI.Instance.SetCurrentHealth(stored.GameId, (short)restoredCurrent);
            }
        }
        private void RestoreExistingSlot(VirtualEntityKind kind, int id) { if (instances.TryGetSlot((byte)kind, id, out uint g, out StoredInstance old)) { if (IdentityMatches(old)) Restore(old); instances.Remove((byte)kind, id); } }
        private bool IdentityMatches(StoredInstance stored) => stored.Kind == VirtualEntityKind.Unit ? GameUnitManagerAPI.Instance.GetGlobalId(stored.GameId) == unchecked((int)stored.GlobalId) : GameBuildingManagerAPI.Instance.GetGlobalId(stored.GameId) == unchecked((int)stored.GlobalId);
        private void Forget(VirtualEntityKind kind, int id) { lock (sync) { instances.Remove((byte)kind, id); (kind == VirtualEntityKind.Unit ? pendingUnits : pendingBuildings).Remove(id); } }
        private void ClearMapState()
        {
            lock (sync)
            {
                instances.Clear(); pendingUnits.Clear(); pendingBuildings.Clear(); pendingRestore = null;
                pendingBuilding = default(PendingBuildingSpawn); pendingRecruitment = null; mapActive = false; modeAllowed = false;
            }
            while (operationQueue.TryDequeue(out OperationRequest ignoredOperation)) { }
            while (completionQueue.TryDequeue(out OperationCompletion ignoredCompletion)) { }
            while (unitTintRestoreQueue.TryDequeue(out int ignoredUnitTint)) { }
            while (recruitmentTransitions.TryDequeue(out RecruitmentTransition ignoredTransition)) { }
            visualResetQueue.Enqueue(true);
            availabilityQueue.Enqueue(false);
        }

        private bool TryValidateTile(int x, int y, bool requireFreeWalkable, out int tileId)
        {
            tileId = -1; GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tiles == null || !tiles.IsTileInsideMapBounds(x, y)) return false;
            tileId = tiles.GetTileId(x, y); return !requireFreeWalkable || tiles.IsTileWalkableAndUnoccupied(tileId);
        }
        private void RefreshBuilding(int id)
        {
            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) || building == null) return;
                for (int y = building->r_TilePositionYBegin; y <= building->r_TilePositionYEnd; y++) for (int x = building->r_TilePositionXBegin; x <= building->r_TilePositionXEnd; x++) GameTileManagerAPI.Instance.RefreshTileAreaVisuals(x, y);
            }
            catch (Exception ex) { LogWarning($"Building visual refresh failed for ID {id}: {ex.Message}"); }
        }

        private static VirtualApiResult Validate(VirtualUnitDefinition d)
        {
            if (d == null || !ValidId(d.TypeId) || string.IsNullOrWhiteSpace(d.DisplayName) || d.DefinitionVersion <= 0 || d.BaseType == eChimps.CHIMP_TYPE_NULL || !ValidTint(d.SpriteProfile) || d.PresentationProfile == null || !ValidTint(d.PresentationProfile.IconTint) || d.Stats == null || d.SpawnOptions == null || !d.Stats.HealthFactor.IsValid || !d.Stats.SpeedFactor.IsValid) return Result(VirtualApiResultCode.InvalidDefinition, "Invalid unit definition.");
            return VirtualApiResult.Success();
        }
        private static VirtualApiResult Validate(VirtualBuildingDefinition d)
        {
            if (d == null || !ValidId(d.TypeId) || string.IsNullOrWhiteSpace(d.DisplayName) || d.DefinitionVersion <= 0 || d.BaseType == eStructs.STRUCT_NULL || !ValidTint(d.VisualProfile) || d.Stats == null || d.SpawnOptions == null || !d.Stats.HealthFactor.IsValid || !d.Stats.SpeedFactor.IsValid || d.Mapper.ConvertToEStructs() != d.BaseType || BuildingScales.GetScale(d.Mapper) != d.BuildingScale || d.BuildingScale < 0) return Result(VirtualApiResultCode.InvalidDefinition, "Invalid building definition or mapper/struct/scale pairing.");
            return VirtualApiResult.Success();
        }
        private static bool ValidTint(VirtualSpriteTintProfile tint) => tint != null && tint.Alpha == byte.MaxValue && (tint.Red != byte.MaxValue || tint.Green != byte.MaxValue || tint.Blue != byte.MaxValue);
        private static bool ValidId(string id) { if (string.IsNullOrWhiteSpace(id) || id.IndexOf(':') <= 0) return false; foreach (char c in id) if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == ':' || c == '_')) return false; return true; }
        private static VirtualApiResult Result(VirtualApiResultCode code, string message) => new VirtualApiResult(code, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private sealed class StoredInstance
        {
            public StoredInstance(VirtualEntityKind kind, int gameId, uint globalId, string typeId, int definitionVersion, int originalMaxHealth, int originalSpeed)
            { Kind = kind; GameId = gameId; GlobalId = globalId; TypeId = typeId; DefinitionVersion = definitionVersion; OriginalMaxHealth = originalMaxHealth; OriginalSpeed = originalSpeed; VisualValidated = true; Snapshot = new VirtualEntityInstance(new VirtualEntityKey(kind, gameId, globalId), typeId, definitionVersion, originalMaxHealth, originalSpeed); }
            public VirtualEntityKind Kind; public int GameId; public uint GlobalId; public string TypeId; public int DefinitionVersion; public int OriginalMaxHealth; public int OriginalSpeed; public bool VisualValidated; public VirtualEntityInstance Snapshot;
        }

        private sealed class PendingSpawn
        {
            private PendingSpawn(VirtualEntityKind kind, int gameId, uint globalId, string typeId, int definitionVersion, int playerId, int requestedX, int requestedY, int originalMaxHealth, int originalCurrentHealth, int originalSpeed, AliveState initialState, int deadlineTick, VirtualOperationTicket ticket, bool preserveOnFailure)
            {
                Kind = kind; GameId = gameId; GlobalId = globalId; TypeId = typeId; DefinitionVersion = definitionVersion; PlayerId = playerId;
                RequestedX = requestedX; RequestedY = requestedY; OriginalMaxHealth = originalMaxHealth; OriginalCurrentHealth = originalCurrentHealth;
                OriginalSpeed = originalSpeed; LastState = initialState; DeadlineTick = deadlineTick; Ticket = ticket; PreserveOnFailure = preserveOnFailure;
                Snapshot = new VirtualEntityInstance(new VirtualEntityKey(kind, gameId, globalId), typeId, definitionVersion, originalMaxHealth, originalSpeed);
            }
            public static PendingSpawn ForUnit(int gameId, uint globalId, string typeId, int definitionVersion, int playerId, int x, int y, int maxHealth, int currentHealth, int speed, AliveState state, int deadlineTick, VirtualOperationTicket ticket, bool preserveOnFailure = false)
                => new PendingSpawn(VirtualEntityKind.Unit, gameId, globalId, typeId, definitionVersion, playerId, x, y, maxHealth, currentHealth, speed, state, deadlineTick, ticket, preserveOnFailure);
            public static PendingSpawn ForBuilding(int gameId, uint globalId, string typeId, int definitionVersion, int playerId, int x, int y, int maxHealth, int currentHealth, AliveState state, int deadlineTick, VirtualOperationTicket ticket)
                => new PendingSpawn(VirtualEntityKind.Building, gameId, globalId, typeId, definitionVersion, playerId, x, y, maxHealth, currentHealth, 0, state, deadlineTick, ticket, false);
            public VirtualEntityKind Kind; public int GameId; public uint GlobalId; public string TypeId; public int DefinitionVersion; public int PlayerId;
            public int RequestedX; public int RequestedY; public int OriginalMaxHealth; public int OriginalCurrentHealth; public int OriginalSpeed;
            public AliveState LastState; public int DeadlineTick; public bool RendererSeen; public bool RendererLogged; public bool UnitHookSeen; public bool BuildingHookSeen; public bool BuildingTintSeen; public bool PreserveOnFailure; public VirtualOperationTicket Ticket; public VirtualEntityInstance Snapshot;
        }

        private sealed class PendingRecruitment
        {
            internal PendingRecruitment(UnitHudRecruitmentTicket ticket, int deadlineTick)
            {
                Ticket = ticket; DeadlineTick = deadlineTick; LastTransitionTick = deadlineTick - RecruitmentCorrelationTickBudget;
            }
            internal UnitHudRecruitmentTicket Ticket { get; }
            internal int DeadlineTick { get; }
            internal int LastTransitionTick { get; set; }
            internal HashSet<int> Candidates { get; } = new HashSet<int>();
            internal HashSet<int> MatchedUnitIds { get; } = new HashSet<int>();
        }

        private readonly struct RecruitmentTransition
        {
            internal RecruitmentTransition(long ticketId, int unitId) { TicketId = ticketId; UnitId = unitId; }
            internal long TicketId { get; }
            internal int UnitId { get; }
        }

        private sealed class OperationRequest
        {
            public OperationRequest(VirtualOperationTicket ticket, string typeId, int gameId, int tileX, int tileY)
            { Ticket = ticket; TypeId = typeId; GameId = gameId; TileX = tileX; TileY = tileY; }
            public VirtualOperationTicket Ticket { get; }
            public string TypeId { get; }
            public int GameId { get; }
            public int TileX { get; }
            public int TileY { get; }
        }

        private sealed class OperationCompletion
        {
            public OperationCompletion(VirtualOperationTicket ticket, VirtualApiResult result, VirtualEntityInstance instance)
            { Ticket = ticket; Result = result; Instance = instance; }
            public VirtualOperationTicket Ticket { get; }
            public VirtualApiResult Result { get; }
            public VirtualEntityInstance Instance { get; }
        }
        private struct PendingBuildingSpawn
        {
            public PendingBuildingSpawn(int playerId, int x, int y, eStructs type) { Active = true; PlayerId = playerId; TileX = x; TileY = y; Type = type; CapturedBuildingId = 0; }
            public bool Active; public int PlayerId; public int TileX; public int TileY; public eStructs Type; public int CapturedBuildingId;
        }
    }
}

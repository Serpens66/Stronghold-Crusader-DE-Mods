using BepInEx.Logging;
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        private List<SaveRecord> pendingRestore;
        private PendingBuildingSpawn pendingBuilding;
        private VisualRuntime visuals;
        private VirtualSpawnController spawnController;
        private bool definitionsSealed;
        private bool initialized;
        private bool mapActive;
        private bool modeAllowed;

        internal VirtualEntityRuntime(ManualLogSource log) { this.log = log ?? throw new ArgumentNullException(nameof(log)); }
        internal static VirtualEntityRuntime Current { get; set; }
        internal bool CanMutate => initialized && mapActive && modeAllowed;

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
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualRemove.Observable.Subscribe(visuals.OnUnitVisualRemove));
                subscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(x => Forget(VirtualEntityKind.Unit, checked((int)x.UnitId))));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Where(x => x.Phase == EventHookPhase.Pre).Subscribe(x => Forget(VirtualEntityKind.Building, x.BuildingId)));
                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(VirtualUnitsPlugin.PluginGuid, Save, Load, ClearMapState))
                    throw new InvalidOperationException("Mod save-data identifier is already registered.");
                spawnController = new VirtualSpawnController(this, log);
                spawnController.Initialize();
                GameXAMLManagerAPI.Instance.RegisterBinding("VirtualUnitsPrototypeHud", spawnController.Hud);
                initialized = true;
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

        internal VirtualApiResult AssignUnit(int unitId, string typeId, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Assignments are allowed only in a loaded singleplayer skirmish.");
            if (!unitDefinitions.TryGetValue(typeId ?? string.Empty, out VirtualUnitDefinition definition)) return Result(VirtualApiResultCode.UnknownTypeId, "Unknown unit type ID.");
            if (!TryReadUnit(unitId, definition.BaseType, out uint globalId, out int maxHealth, out int currentHealth, out int speed, out VirtualApiResult failure)) return failure;
            lock (sync)
            {
                if (instances.TryGet(UnitKind, unitId, globalId, out StoredInstance existing) && existing.TypeId == typeId) { snapshot = existing.Snapshot; return VirtualApiResult.Success("Unit is already assigned."); }
                RestoreExistingSlot(VirtualEntityKind.Unit, unitId);
                var stored = new StoredInstance(VirtualEntityKind.Unit, unitId, globalId, typeId, definition.DefinitionVersion, maxHealth, speed);
                ApplyUnitStats(unitId, maxHealth, currentHealth, speed, definition);
                instances.Set(UnitKind, unitId, globalId, stored); snapshot = stored.Snapshot;
            }
            VirtualApiResult success = VirtualApiResult.Success("Unit assignment created."); VirtualEntityApi.RaiseAssigned(snapshot, success); return success;
        }

        internal VirtualApiResult AssignBuilding(int buildingId, string typeId, out VirtualEntityInstance snapshot)
        {
            snapshot = null;
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Assignments are allowed only in a loaded singleplayer skirmish.");
            if (!buildingDefinitions.TryGetValue(typeId ?? string.Empty, out VirtualBuildingDefinition definition)) return Result(VirtualApiResultCode.UnknownTypeId, "Unknown building type ID.");
            if (!TryReadBuilding(buildingId, definition.BaseType, out uint globalId, out int maxHealth, out int currentHealth, out VirtualApiResult failure)) return failure;
            lock (sync)
            {
                if (instances.TryGet(BuildingKind, buildingId, globalId, out StoredInstance existing) && existing.TypeId == typeId) { snapshot = existing.Snapshot; return VirtualApiResult.Success("Building is already assigned."); }
                RestoreExistingSlot(VirtualEntityKind.Building, buildingId);
                var stored = new StoredInstance(VirtualEntityKind.Building, buildingId, globalId, typeId, definition.DefinitionVersion, maxHealth, 0);
                ApplyBuildingStats(buildingId, maxHealth, currentHealth, definition);
                instances.Set(BuildingKind, buildingId, globalId, stored); snapshot = stored.Snapshot;
            }
            RefreshBuilding(buildingId); VirtualApiResult success = VirtualApiResult.Success("Building assignment created."); VirtualEntityApi.RaiseAssigned(snapshot, success); return success;
        }

        internal VirtualApiResult Remove(VirtualEntityKind kind, int gameId)
        {
            if (!CanMutate) return Result(VirtualApiResultCode.UnsupportedGameMode, "Removal is allowed only in a loaded singleplayer skirmish.");
            if (gameId <= 0) return Result(VirtualApiResultCode.InvalidGameId, "Game ID must be 1-based and positive.");
            StoredInstance stored;
            lock (sync)
            {
                if (!instances.TryGetSlot((byte)kind, gameId, out uint globalId, out stored)) return Result(VirtualApiResultCode.EntityNotFound, "No assignment exists for this slot.");
                if (!IdentityMatches(stored)) { instances.Remove((byte)kind, gameId); return Result(VirtualApiResultCode.GlobalIdMismatch, "The native slot was reused; stale assignment removed."); }
                Restore(stored); instances.Remove((byte)kind, gameId);
            }
            if (kind == VirtualEntityKind.Building) RefreshBuilding(gameId);
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

        internal VirtualApiResult SpawnUnit(string typeId, int tileX, int tileY, out VirtualEntityInstance snapshot)
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
                return AssignUnit((int)created, typeId, out snapshot);
            }
            catch (Exception ex) { LogError($"Unit spawn failed: {ex}"); return Result(VirtualApiResultCode.InternalError, "Unit spawn raised an internal error."); }
        }

        internal VirtualApiResult SpawnBuilding(string typeId, int tileX, int tileY, out VirtualEntityInstance snapshot)
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
                if (buildingId <= 0 || result <= 0) return Result(VirtualApiResultCode.SpawnFailed, $"CreatePrefab did not produce a correlated building (result={result}, eventId={buildingId}).");
                return AssignBuilding(buildingId, typeId, out snapshot);
            }
            catch (Exception ex) { pendingBuilding = default(PendingBuildingSpawn); LogError($"Building spawn failed: {ex}"); return Result(VirtualApiResultCode.InternalError, "Building spawn raised an internal error."); }
        }

        internal bool TryResolveUnitVisual(int unitId, out VirtualUnitDefinition definition)
        {
            definition = null;
            if (unitId <= 0) return false;
            int global = GameUnitManagerAPI.Instance.GetGlobalId(unitId);
            lock (sync)
            {
                if (global <= 0 || !instances.TryGet(UnitKind, unitId, unchecked((uint)global), out StoredInstance stored)) return false;
                return unitDefinitions.TryGetValue(stored.TypeId, out definition) && GameUnitManagerAPI.Instance.GetType(unitId) == definition.BaseType;
            }
        }

        internal bool TryResolveBuildingVisual(int buildingId, out VirtualBuildingDefinition definition)
        {
            definition = null;
            if (buildingId <= 0) return false;
            int global = GameBuildingManagerAPI.Instance.GetGlobalId(buildingId);
            lock (sync)
            {
                if (global <= 0 || !instances.TryGet(BuildingKind, buildingId, unchecked((uint)global), out StoredInstance stored)) return false;
                return buildingDefinitions.TryGetValue(stored.TypeId, out definition) && GameBuildingManagerAPI.Instance.GetType(buildingId) == definition.BaseType;
            }
        }

        internal void Tick() { if (pendingRestore != null && CanMutate) RestorePending(); }
        internal IEnumerable<VirtualUnitDefinition> VisibleUnits() => unitDefinitions.Values.Where(x => x.SpawnOptions.ShowInDiagnosticMenu).OrderBy(x => x.TypeId).ToArray();
        internal IEnumerable<VirtualBuildingDefinition> VisibleBuildings() => buildingDefinitions.Values.Where(x => x.SpawnOptions.ShowInDiagnosticMenu).OrderBy(x => x.TypeId).ToArray();

        private void OnStartMap(MapStartEventArgs args) { SetMapMode(Shared.GameModeHelper.Capture(args)); }
        private void OnLoadSave(LoadSaveGameEventArgs args) { SetMapMode(Shared.GameModeHelper.Capture(args)); }
        private void SetMapMode(Shared.GameModeSnapshot mode)
        {
            mapActive = true; modeAllowed = mode.IsSingleplayerSkirmish && !mode.IsRealMultiplayer && !mode.IsMapEditor;
            spawnController?.SetAvailability(modeAllowed);
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
            lock (sync)
            {
                return SaveCodec.Encode(instances.Values.Select(x => new SaveRecord { Kind = (byte)x.Kind, GameId = x.GameId, GlobalId = x.GlobalId, TypeId = x.TypeId, DefinitionVersion = x.DefinitionVersion, OriginalMaxHealth = x.OriginalMaxHealth, OriginalSpeed = x.OriginalSpeed }));
            }
        }
        private void Load(byte[] data, LoadContext context)
        {
            if (!context.IsSaveFile) return;
            try { pendingRestore = SaveCodec.Decode(data); Shared.DebugLogHelper.LogInfo(log, $"Loaded {pendingRestore.Count} pending virtual-entity records."); }
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
            int newSpeed = VirtualMath.ScalePositive(speed, definition.Stats.SpeedFactor.Numerator, definition.Stats.SpeedFactor.Denominator, ushort.MaxValue);
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
        private void Forget(VirtualEntityKind kind, int id) { lock (sync) instances.Remove((byte)kind, id); }
        private void ClearMapState() { lock (sync) { instances.Clear(); pendingRestore = null; pendingBuilding = default(PendingBuildingSpawn); } visuals?.ClearBindings(); mapActive = false; modeAllowed = false; spawnController?.SetAvailability(false); }

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
            if (d == null || !ValidId(d.TypeId) || string.IsNullOrWhiteSpace(d.DisplayName) || d.DefinitionVersion <= 0 || d.BaseType == eChimps.CHIMP_TYPE_NULL || d.SpriteProfile == null || d.Stats == null || d.SpawnOptions == null || !d.Stats.HealthFactor.IsValid || !d.Stats.SpeedFactor.IsValid || !Enum.IsDefined(typeof(Enums.GM), d.SpriteProfile.TargetGm)) return Result(VirtualApiResultCode.InvalidDefinition, "Invalid unit definition.");
            return VirtualApiResult.Success();
        }
        private static VirtualApiResult Validate(VirtualBuildingDefinition d)
        {
            if (d == null || !ValidId(d.TypeId) || string.IsNullOrWhiteSpace(d.DisplayName) || d.DefinitionVersion <= 0 || d.BaseType == eStructs.STRUCT_NULL || d.VisualProfile == null || d.Stats == null || d.SpawnOptions == null || !d.Stats.HealthFactor.IsValid || !d.Stats.SpeedFactor.IsValid || d.Mapper.ConvertToEStructs() != d.BaseType || BuildingScales.GetScale(d.Mapper) != d.BuildingScale || d.BuildingScale < 0 || !Enum.IsDefined(typeof(Enums.GM), d.VisualProfile.FirstGm) || !Enum.IsDefined(typeof(Enums.GM), d.VisualProfile.SecondGm)) return Result(VirtualApiResultCode.InvalidDefinition, "Invalid building definition or mapper/struct/scale pairing.");
            return VirtualApiResult.Success();
        }
        private static bool ValidId(string id) { if (string.IsNullOrWhiteSpace(id) || id.IndexOf(':') <= 0) return false; foreach (char c in id) if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == ':' || c == '_')) return false; return true; }
        private static VirtualApiResult Result(VirtualApiResultCode code, string message) => new VirtualApiResult(code, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private sealed class StoredInstance
        {
            public StoredInstance(VirtualEntityKind kind, int gameId, uint globalId, string typeId, int definitionVersion, int originalMaxHealth, int originalSpeed)
            { Kind = kind; GameId = gameId; GlobalId = globalId; TypeId = typeId; DefinitionVersion = definitionVersion; OriginalMaxHealth = originalMaxHealth; OriginalSpeed = originalSpeed; Snapshot = new VirtualEntityInstance(new VirtualEntityKey(kind, gameId, globalId), typeId, definitionVersion, originalMaxHealth, originalSpeed); }
            public VirtualEntityKind Kind; public int GameId; public uint GlobalId; public string TypeId; public int DefinitionVersion; public int OriginalMaxHealth; public int OriginalSpeed; public VirtualEntityInstance Snapshot;
        }
        private struct PendingBuildingSpawn
        {
            public PendingBuildingSpawn(int playerId, int x, int y, eStructs type) { Active = true; PlayerId = playerId; TileX = x; TileY = y; Type = type; CapturedBuildingId = 0; }
            public bool Active; public int PlayerId; public int TileX; public int TileY; public eStructs Type; public int CapturedBuildingId;
        }
    }
}

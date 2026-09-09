using SHCDESE.Interop;
using System;

namespace VirtualUnitsPrototype.API
{
    public enum VirtualEntityKind : byte { Unit = 1, Building = 2 }
    public enum VirtualApiResultCode
    {
        Success, NotInitialized, DefinitionsSealed, InvalidDefinition, DuplicateTypeId,
        UnknownTypeId, InvalidGameId, EntityNotFound, GlobalIdMismatch, BaseTypeMismatch,
        UnsupportedGameMode, InvalidPlacement, SpawnFailed, VisualFeatureUnavailable,
        SaveDefinitionIncompatible, InternalError
    }

    public readonly struct VirtualApiResult
    {
        public VirtualApiResult(VirtualApiResultCode code, string message) { Code = code; Message = message ?? string.Empty; }
        public VirtualApiResultCode Code { get; }
        public string Message { get; }
        public bool Succeeded => Code == VirtualApiResultCode.Success;
        public static VirtualApiResult Success(string message = "Success") => new VirtualApiResult(VirtualApiResultCode.Success, message);
        public override string ToString() => $"{Code}: {Message}";
    }

    public readonly struct RationalFactor
    {
        public RationalFactor(int numerator, int denominator) { Numerator = numerator; Denominator = denominator; }
        public int Numerator { get; }
        public int Denominator { get; }
        public bool IsValid => Numerator > 0 && Denominator > 0;
    }

    public sealed class VirtualStatProfile
    {
        public VirtualStatProfile(RationalFactor healthFactor, RationalFactor speedFactor)
        { HealthFactor = healthFactor; SpeedFactor = speedFactor; }
        public RationalFactor HealthFactor { get; }
        public RationalFactor SpeedFactor { get; }
    }

    public sealed class UnitSpriteProfile
    {
        public UnitSpriteProfile(Enums.GM targetGm) { TargetGm = targetGm; }
        public Enums.GM TargetGm { get; }
    }

    public sealed class BuildingTileVisualProfile
    {
        public BuildingTileVisualProfile(Enums.GM firstGm, Enums.GM secondGm) { FirstGm = firstGm; SecondGm = secondGm; }
        public Enums.GM FirstGm { get; }
        public Enums.GM SecondGm { get; }
        public bool TryMap(Enums.GM source, out Enums.GM target)
        {
            if (source == FirstGm) { target = SecondGm; return true; }
            if (source == SecondGm) { target = FirstGm; return true; }
            target = source; return false;
        }
    }

    public sealed class VirtualSpawnOptions
    {
        public VirtualSpawnOptions(bool showInDiagnosticMenu, bool allowDiagnosticSpawn)
        { ShowInDiagnosticMenu = showInDiagnosticMenu; AllowDiagnosticSpawn = allowDiagnosticSpawn; }
        public bool ShowInDiagnosticMenu { get; }
        public bool AllowDiagnosticSpawn { get; }
    }

    public sealed class VirtualUnitDefinition
    {
        public VirtualUnitDefinition(string typeId, int definitionVersion, string displayName, eChimps baseType,
            UnitSpriteProfile spriteProfile, VirtualStatProfile stats, VirtualSpawnOptions spawnOptions)
        { TypeId = typeId; DefinitionVersion = definitionVersion; DisplayName = displayName; BaseType = baseType; SpriteProfile = spriteProfile; Stats = stats; SpawnOptions = spawnOptions; }
        public string TypeId { get; }
        public int DefinitionVersion { get; }
        public string DisplayName { get; }
        public eChimps BaseType { get; }
        public UnitSpriteProfile SpriteProfile { get; }
        public VirtualStatProfile Stats { get; }
        public VirtualSpawnOptions SpawnOptions { get; }
    }

    public sealed class VirtualBuildingDefinition
    {
        public VirtualBuildingDefinition(string typeId, int definitionVersion, string displayName, eStructs baseType,
            eMappers mapper, int buildingScale, BuildingTileVisualProfile visualProfile,
            VirtualStatProfile stats, VirtualSpawnOptions spawnOptions)
        { TypeId = typeId; DefinitionVersion = definitionVersion; DisplayName = displayName; BaseType = baseType; Mapper = mapper; BuildingScale = buildingScale; VisualProfile = visualProfile; Stats = stats; SpawnOptions = spawnOptions; }
        public string TypeId { get; }
        public int DefinitionVersion { get; }
        public string DisplayName { get; }
        public eStructs BaseType { get; }
        public eMappers Mapper { get; }
        public int BuildingScale { get; }
        public BuildingTileVisualProfile VisualProfile { get; }
        public VirtualStatProfile Stats { get; }
        public VirtualSpawnOptions SpawnOptions { get; }
    }

    public readonly struct VirtualEntityKey
    {
        public VirtualEntityKey(VirtualEntityKind kind, int gameId, uint globalId) { Kind = kind; GameId = gameId; GlobalId = globalId; }
        public VirtualEntityKind Kind { get; }
        public int GameId { get; }
        public uint GlobalId { get; }
    }

    public sealed class VirtualEntityInstance
    {
        internal VirtualEntityInstance(VirtualEntityKey key, string typeId, int definitionVersion, int originalMaxHealth, int originalSpeed)
        { Key = key; TypeId = typeId; DefinitionVersion = definitionVersion; OriginalMaxHealth = originalMaxHealth; OriginalSpeed = originalSpeed; }
        public VirtualEntityKey Key { get; }
        public string TypeId { get; }
        public int DefinitionVersion { get; }
        public int OriginalMaxHealth { get; }
        public int OriginalSpeed { get; }
    }

    public sealed class VirtualEntityEventArgs : EventArgs
    {
        public VirtualEntityEventArgs(VirtualEntityInstance instance, VirtualApiResult result) { Instance = instance; Result = result; }
        public VirtualEntityInstance Instance { get; }
        public VirtualApiResult Result { get; }
    }

    public static class VirtualEntityApi
    {
        public static event EventHandler<VirtualEntityEventArgs> AssignmentCreated;
        public static event EventHandler<VirtualEntityEventArgs> AssignmentRemoved;
        public static event EventHandler<VirtualEntityEventArgs> RestoreRejected;

        public static VirtualApiResult RegisterUnitDefinition(VirtualUnitDefinition definition) => RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.Register(definition) : error;
        public static VirtualApiResult RegisterBuildingDefinition(VirtualBuildingDefinition definition) => RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.Register(definition) : error;
        public static VirtualApiResult TryGetUnitDefinition(string typeId, out VirtualUnitDefinition definition) { definition = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetDefinition(typeId, out definition) : error; }
        public static VirtualApiResult TryGetBuildingDefinition(string typeId, out VirtualBuildingDefinition definition) { definition = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetDefinition(typeId, out definition) : error; }
        public static VirtualApiResult TryAssignUnit(int unitId, string typeId, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.AssignUnit(unitId, typeId, out instance) : error; }
        public static VirtualApiResult TryAssignBuilding(int buildingId, string typeId, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.AssignBuilding(buildingId, typeId, out instance) : error; }
        public static VirtualApiResult TryRemoveUnitAssignment(int unitId) => RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.Remove(VirtualEntityKind.Unit, unitId) : error;
        public static VirtualApiResult TryRemoveBuildingAssignment(int buildingId) => RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.Remove(VirtualEntityKind.Building, buildingId) : error;
        public static VirtualApiResult TryGetUnitInstance(int unitId, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetInstance(VirtualEntityKind.Unit, unitId, out instance) : error; }
        public static VirtualApiResult TryGetBuildingInstance(int buildingId, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetInstance(VirtualEntityKind.Building, buildingId, out instance) : error; }
        public static VirtualApiResult SpawnVirtualUnit(string typeId, int tileX, int tileY, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.SpawnUnit(typeId, tileX, tileY, out instance) : error; }
        public static VirtualApiResult SpawnVirtualBuilding(string typeId, int tileX, int tileY, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.SpawnBuilding(typeId, tileX, tileY, out instance) : error; }

        private static bool RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error)
        { runtime = VirtualEntityRuntime.Current; error = runtime == null ? new VirtualApiResult(VirtualApiResultCode.NotInitialized, "VirtualUnitsPrototype is not initialized.") : default(VirtualApiResult); return runtime != null; }
        internal static void RaiseAssigned(VirtualEntityInstance instance, VirtualApiResult result) => AssignmentCreated?.Invoke(null, new VirtualEntityEventArgs(instance, result));
        internal static void RaiseRemoved(VirtualEntityInstance instance, VirtualApiResult result) => AssignmentRemoved?.Invoke(null, new VirtualEntityEventArgs(instance, result));
        internal static void RaiseRestoreRejected(VirtualEntityInstance instance, VirtualApiResult result) => RestoreRejected?.Invoke(null, new VirtualEntityEventArgs(instance, result));
    }
}

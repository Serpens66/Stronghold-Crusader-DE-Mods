using SHCDESE.Interop;
using System;

namespace VirtualUnitsPrototype.API
{
    public enum VirtualEntityKind : byte { Unit = 1, Building = 2 }
    public enum VirtualOperationKind : byte
    {
        SpawnUnit = 1, SpawnBuilding = 2, AssignUnit = 3,
        AssignBuilding = 4, RemoveUnitAssignment = 5, RemoveBuildingAssignment = 6
    }
    public enum VirtualApiResultCode
    {
        Success, NotInitialized, DefinitionsSealed, InvalidDefinition, DuplicateTypeId,
        UnknownTypeId, InvalidGameId, EntityNotFound, GlobalIdMismatch, BaseTypeMismatch,
        UnsupportedGameMode, InvalidPlacement, SpawnFailed, VisualFeatureUnavailable,
        SaveDefinitionIncompatible, InternalError, InitializationPending
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

    public sealed class VirtualSpriteTintProfile
    {
        public VirtualSpriteTintProfile(byte red, byte green, byte blue, byte alpha)
        { Red = red; Green = green; Blue = blue; Alpha = alpha; }
        public byte Red { get; }
        public byte Green { get; }
        public byte Blue { get; }
        public byte Alpha { get; }
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
            VirtualSpriteTintProfile spriteProfile, VirtualStatProfile stats, VirtualSpawnOptions spawnOptions)
        { TypeId = typeId; DefinitionVersion = definitionVersion; DisplayName = displayName; BaseType = baseType; SpriteProfile = spriteProfile; Stats = stats; SpawnOptions = spawnOptions; }
        public string TypeId { get; }
        public int DefinitionVersion { get; }
        public string DisplayName { get; }
        public eChimps BaseType { get; }
        public VirtualSpriteTintProfile SpriteProfile { get; }
        public VirtualStatProfile Stats { get; }
        public VirtualSpawnOptions SpawnOptions { get; }
    }

    public sealed class VirtualBuildingDefinition
    {
        public VirtualBuildingDefinition(string typeId, int definitionVersion, string displayName, eStructs baseType,
            eMappers mapper, int buildingScale, VirtualSpriteTintProfile visualProfile,
            VirtualStatProfile stats, VirtualSpawnOptions spawnOptions)
        { TypeId = typeId; DefinitionVersion = definitionVersion; DisplayName = displayName; BaseType = baseType; Mapper = mapper; BuildingScale = buildingScale; VisualProfile = visualProfile; Stats = stats; SpawnOptions = spawnOptions; }
        public string TypeId { get; }
        public int DefinitionVersion { get; }
        public string DisplayName { get; }
        public eStructs BaseType { get; }
        public eMappers Mapper { get; }
        public int BuildingScale { get; }
        public VirtualSpriteTintProfile VisualProfile { get; }
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

    public readonly struct VirtualOperationTicket
    {
        public VirtualOperationTicket(long requestId, VirtualOperationKind kind) { RequestId = requestId; Kind = kind; }
        public long RequestId { get; }
        public VirtualOperationKind Kind { get; }
        public override string ToString() => $"{Kind}#{RequestId}";
    }

    public sealed class VirtualOperationCompletedEventArgs : EventArgs
    {
        public VirtualOperationCompletedEventArgs(VirtualOperationTicket ticket, VirtualApiResult result, VirtualEntityInstance instance)
        { Ticket = ticket; Result = result; Instance = instance; }
        public VirtualOperationTicket Ticket { get; }
        public VirtualApiResult Result { get; }
        public VirtualEntityInstance Instance { get; }
    }

    public static class VirtualEntityApi
    {
        public static event EventHandler<VirtualEntityEventArgs> AssignmentCreated;
        public static event EventHandler<VirtualEntityEventArgs> AssignmentRemoved;
        public static event EventHandler<VirtualEntityEventArgs> RestoreRejected;
        public static event EventHandler<VirtualOperationCompletedEventArgs> OperationCompleted;

        public static VirtualApiResult RegisterUnitDefinition(VirtualUnitDefinition definition) => RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.Register(definition) : error;
        public static VirtualApiResult RegisterBuildingDefinition(VirtualBuildingDefinition definition) => RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.Register(definition) : error;
        public static VirtualApiResult TryGetUnitDefinition(string typeId, out VirtualUnitDefinition definition) { definition = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetDefinition(typeId, out definition) : error; }
        public static VirtualApiResult TryGetBuildingDefinition(string typeId, out VirtualBuildingDefinition definition) { definition = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetDefinition(typeId, out definition) : error; }
        public static VirtualApiResult QueueAssignUnit(int unitId, string typeId, out VirtualOperationTicket ticket) => Queue(VirtualOperationKind.AssignUnit, typeId, unitId, 0, 0, out ticket);
        public static VirtualApiResult QueueAssignBuilding(int buildingId, string typeId, out VirtualOperationTicket ticket) => Queue(VirtualOperationKind.AssignBuilding, typeId, buildingId, 0, 0, out ticket);
        public static VirtualApiResult QueueRemoveUnitAssignment(int unitId, out VirtualOperationTicket ticket) => Queue(VirtualOperationKind.RemoveUnitAssignment, null, unitId, 0, 0, out ticket);
        public static VirtualApiResult QueueRemoveBuildingAssignment(int buildingId, out VirtualOperationTicket ticket) => Queue(VirtualOperationKind.RemoveBuildingAssignment, null, buildingId, 0, 0, out ticket);
        public static VirtualApiResult QueueVirtualUnitSpawn(string typeId, int tileX, int tileY, out VirtualOperationTicket ticket) => Queue(VirtualOperationKind.SpawnUnit, typeId, 0, tileX, tileY, out ticket);
        public static VirtualApiResult QueueVirtualBuildingSpawn(string typeId, int tileX, int tileY, out VirtualOperationTicket ticket) => Queue(VirtualOperationKind.SpawnBuilding, typeId, 0, tileX, tileY, out ticket);
        public static VirtualApiResult TryAssignUnit(int unitId, string typeId, out VirtualEntityInstance instance) { instance = null; VirtualApiResult result = QueueAssignUnit(unitId, typeId, out _); return result; }
        public static VirtualApiResult TryAssignBuilding(int buildingId, string typeId, out VirtualEntityInstance instance) { instance = null; VirtualApiResult result = QueueAssignBuilding(buildingId, typeId, out _); return result; }
        public static VirtualApiResult TryRemoveUnitAssignment(int unitId) => QueueRemoveUnitAssignment(unitId, out _);
        public static VirtualApiResult TryRemoveBuildingAssignment(int buildingId) => QueueRemoveBuildingAssignment(buildingId, out _);
        public static VirtualApiResult TryGetUnitInstance(int unitId, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetInstance(VirtualEntityKind.Unit, unitId, out instance) : error; }
        public static VirtualApiResult TryGetBuildingInstance(int buildingId, out VirtualEntityInstance instance) { instance = null; return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error) ? runtime.TryGetInstance(VirtualEntityKind.Building, buildingId, out instance) : error; }
        public static VirtualApiResult SpawnVirtualUnit(string typeId, int tileX, int tileY, out VirtualEntityInstance instance) { instance = null; return QueueVirtualUnitSpawn(typeId, tileX, tileY, out _); }
        public static VirtualApiResult SpawnVirtualBuilding(string typeId, int tileX, int tileY, out VirtualEntityInstance instance) { instance = null; return QueueVirtualBuildingSpawn(typeId, tileX, tileY, out _); }

        private static VirtualApiResult Queue(VirtualOperationKind kind, string typeId, int gameId, int tileX, int tileY, out VirtualOperationTicket ticket)
        {
            ticket = default(VirtualOperationTicket);
            return RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error)
                ? runtime.QueueOperation(kind, typeId, gameId, tileX, tileY, out ticket)
                : error;
        }

        private static bool RuntimeOrError(out VirtualEntityRuntime runtime, out VirtualApiResult error)
        { runtime = VirtualEntityRuntime.Current; error = runtime == null ? new VirtualApiResult(VirtualApiResultCode.NotInitialized, "VirtualUnitsPrototype is not initialized.") : default(VirtualApiResult); return runtime != null; }
        internal static void RaiseAssigned(VirtualEntityInstance instance, VirtualApiResult result) => AssignmentCreated?.Invoke(null, new VirtualEntityEventArgs(instance, result));
        internal static void RaiseRemoved(VirtualEntityInstance instance, VirtualApiResult result) => AssignmentRemoved?.Invoke(null, new VirtualEntityEventArgs(instance, result));
        internal static void RaiseRestoreRejected(VirtualEntityInstance instance, VirtualApiResult result) => RestoreRejected?.Invoke(null, new VirtualEntityEventArgs(instance, result));
        internal static void RaiseOperationCompleted(VirtualOperationTicket ticket, VirtualApiResult result, VirtualEntityInstance instance) => OperationCompleted?.Invoke(null, new VirtualOperationCompletedEventArgs(ticket, result, instance));
    }
}

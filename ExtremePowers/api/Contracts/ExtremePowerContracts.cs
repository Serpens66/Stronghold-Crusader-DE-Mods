using System;

namespace ExtremePowers.API
{
    public enum ExtremePowerId { ArrowVolley = 0, Heal = 1, Spearmen = 2, Engineers = 3, Macemen = 4, Gold = 5, Knights = 6, RockVolley = 7 }
    public enum ExtremePowerTargetKind { None = 0, MapPoint = 1, Unit = 2 }

    public readonly struct ExtremePowerTarget : IEquatable<ExtremePowerTarget>
    {
        private ExtremePowerTarget(ExtremePowerTargetKind kind, int tileIndex, int unitId) { Kind = kind; TileIndex = tileIndex; UnitId = unitId; }
        public ExtremePowerTargetKind Kind { get; }
        public int TileIndex { get; }
        public int UnitId { get; }
        public static ExtremePowerTarget None => new ExtremePowerTarget(ExtremePowerTargetKind.None, -1, -1);
        public static ExtremePowerTarget MapPoint(int tileIndex) => new ExtremePowerTarget(ExtremePowerTargetKind.MapPoint, tileIndex, -1);
        public static ExtremePowerTarget Unit(int globalUnitId) => new ExtremePowerTarget(ExtremePowerTargetKind.Unit, -1, globalUnitId);
        public bool Equals(ExtremePowerTarget other) => Kind == other.Kind && TileIndex == other.TileIndex && UnitId == other.UnitId;
        public override bool Equals(object obj) => obj is ExtremePowerTarget other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ TileIndex ^ UnitId;
    }

    public readonly struct ExtremePowerExecutionContext
    {
        public ExtremePowerExecutionContext(ExtremePowerId power, int playerId, ExtremePowerTarget target, ulong operationId, int simulationTick)
        { Power = power; PlayerId = playerId; Target = target; OperationId = operationId; SimulationTick = simulationTick; }
        public ExtremePowerId Power { get; }
        public int PlayerId { get; }
        public ExtremePowerTarget Target { get; }
        public ulong OperationId { get; }
        public int SimulationTick { get; }
    }

    /// <summary>Result of one native unit-group spawn performed inside an already synchronized callback.</summary>
    public readonly struct ExtremePowerSpawnResult
    {
        public ExtremePowerSpawnResult(int ownerPlayerId, int groupUnitId, int requestedCount, int spawnedUnitCount)
        {
            OwnerPlayerId = ownerPlayerId;
            GroupUnitId = groupUnitId;
            RequestedCount = requestedCount;
            SpawnedUnitCount = spawnedUnitCount;
        }

        public int OwnerPlayerId { get; }
        public int GroupUnitId { get; }
        public int RequestedCount { get; }
        public int SpawnedUnitCount { get; }
        public bool CreatedGroup => GroupUnitId > 0;
    }

    public delegate bool ExtremePowerCanExecute(in ExtremePowerExecutionContext context, out string rejectionReason);
    public delegate void ExtremePowerExecute(in ExtremePowerExecutionContext context);

    public readonly struct ExtremePowersReadiness
    {
        public ExtremePowersReadiness(bool ready, string reason)
        {
            Ready = ready;
            Reason = reason ?? string.Empty;
        }

        public bool Ready { get; }
        public string Reason { get; }
        public static ExtremePowersReadiness Available => new ExtremePowersReadiness(true, string.Empty);
        public static ExtremePowersReadiness Unavailable(string reason) => new ExtremePowersReadiness(false, reason);
    }

    public sealed class ExtremePowerReplacement
    {
        public ExtremePowerReplacement(string name, string tooltip, string sprite, ExtremePowerTargetKind targetKind, ExtremePowerCanExecute canExecute, ExtremePowerExecute execute)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A replacement name is required.", nameof(name));
            if ((int)targetKind < 0 || (int)targetKind > 2) throw new ArgumentOutOfRangeException(nameof(targetKind));
            Name = name; Tooltip = tooltip ?? string.Empty; Sprite = sprite ?? string.Empty; TargetKind = targetKind;
            CanExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute)); Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }
        public string Name { get; }
        public string Tooltip { get; }
        public string Sprite { get; }
        public ExtremePowerTargetKind TargetKind { get; }
        public ExtremePowerCanExecute CanExecute { get; }
        public ExtremePowerExecute Execute { get; }
    }

    public sealed class ExtremePowersBootstrapOptions
    {
        public Func<string, ExtremePowersReadiness> GetSessionReadiness { get; set; }
        public Action<string> Diagnostic { get; set; }
    }

    public interface IExtremePowersApi
    {
        string ProtocolVersion { get; }
        string CompatibilityToken { get; }
        bool NativeBackendAvailable { get; }
        string NativeBackendStatus { get; }
        bool SupportsUnitTargeting { get; }
        VanillaExtremePowersConfiguration Vanilla { get; }
        ExtremePowersTuning Current { get; }
        void Apply(ExtremePowersTuning tuning);
        void RestoreVanilla();
        IDisposable RegisterReplacement(ExtremePowerId power, ExtremePowerReplacement replacement);
        bool TryGetReplacement(ExtremePowerId power, out ExtremePowerReplacement replacement);
        bool TryExecuteReplacement(ExtremePowerExecutionContext context, out string rejectionReason);
        bool QueueReplacement(ExtremePowerId power, int playerId, ExtremePowerTarget target, out string rejectionReason);
        /// <summary>
        /// Spawns one native unit group for the explicit owner. Owner 0 is the neutral nature player; owners 1-8
        /// are normal player slots. This method does not send a network message; callers must invoke it only from
        /// a callback that is already executed deterministically on every peer.
        /// </summary>
        ExtremePowerSpawnResult SpawnUnitGroup(int ownerPlayerId, int targetTileId, int unitType, int count);
    }
}

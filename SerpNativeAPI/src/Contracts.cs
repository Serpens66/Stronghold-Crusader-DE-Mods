using System;

namespace SerpNativeAPI
{
    public enum NativeApiState
    {
        Pending,
        Ready,
        Unavailable
    }

    public enum NativeCapabilityState
    {
        Pending,
        Available,
        UnsupportedBuild,
        PatternMissing,
        Ambiguous,
        ValidationFailed,
        Conflict,
        Faulted
    }

    public static class NativeCapabilityIds
    {
        public const string GatehouseTiming = "gatehouse-timing";
        public const string SelectedUnitCommand = "selected-unit-command";
    }

    public sealed class NativeCapabilityDiagnostic
    {
        public NativeCapabilityDiagnostic(
            string capabilityId,
            NativeCapabilityState state,
            string binaryHash,
            string reason,
            string conflictOwnerGuid = null)
        {
            CapabilityId = capabilityId ?? string.Empty;
            State = state;
            BinaryHash = binaryHash ?? string.Empty;
            Reason = reason ?? string.Empty;
            ConflictOwnerGuid = conflictOwnerGuid;
        }

        public string CapabilityId { get; }
        public NativeCapabilityState State { get; }
        public string BinaryHash { get; }
        public string Reason { get; }
        public string ConflictOwnerGuid { get; }
    }

    public readonly struct GatehouseTimingSettings
    {
        public GatehouseTimingSettings(
            bool enabled,
            double humanDelaySeconds,
            double aiDelaySeconds,
            double humanDistanceTiles,
            double aiDistanceTiles)
        {
            Enabled = enabled;
            HumanDelaySeconds = humanDelaySeconds;
            AiDelaySeconds = aiDelaySeconds;
            HumanDistanceTiles = humanDistanceTiles;
            AiDistanceTiles = aiDistanceTiles;
        }

        public bool Enabled { get; }
        public double HumanDelaySeconds { get; }
        public double AiDelaySeconds { get; }
        public double HumanDistanceTiles { get; }
        public double AiDistanceTiles { get; }
    }

    public readonly struct SelectedUnitCommandContext
    {
        public SelectedUnitCommandContext(int tribeId, int command, int argument1, int argument2, int argument3)
        {
            TribeId = tribeId;
            Command = command;
            Argument1 = argument1;
            Argument2 = argument2;
            Argument3 = argument3;
        }

        public int TribeId { get; }
        public int Command { get; }
        public int Argument1 { get; }
        public int Argument2 { get; }
        public int Argument3 { get; }
    }

    public interface IGatehouseTimingCapability
    {
        bool TryApply(GatehouseTimingSettings settings, out NativeCapabilityDiagnostic diagnostic);
    }

    public interface ISelectedUnitCommandRegistration : IDisposable
    {
        bool IsEnabled { get; }
        void Enable();
        void Disable();
    }

    public interface ISelectedUnitCommandCapability
    {
        bool TryRegisterBefore(
            Action<SelectedUnitCommandContext> callback,
            out ISelectedUnitCommandRegistration registration,
            out NativeCapabilityDiagnostic diagnostic);
    }

    public interface ISerpNativeApi
    {
        NativeApiState State { get; }
        bool TryGetGatehouseTiming(
            string ownerGuid,
            out IGatehouseTimingCapability capability,
            out NativeCapabilityDiagnostic diagnostic);
        bool TryGetSelectedUnitCommand(
            string ownerGuid,
            out ISelectedUnitCommandCapability capability,
            out NativeCapabilityDiagnostic diagnostic);
    }

    public static class SerpNativeApi
    {
        public static ISerpNativeApi Current => SerpNativeApiRuntime.ProcessInstance;

        public static void WhenReady(Action<ISerpNativeApi> callback) =>
            SerpNativeApiRuntime.ProcessInstance.WhenReady(callback);
    }
}

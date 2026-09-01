using System;
using SHCDESE.Interop.Enums;

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
            double humanReopenDelaySeconds,
            double aiReopenDelaySeconds,
            double humanCloseDistanceTiles,
            double aiCloseDistanceTiles)
        {
            Enabled = enabled;
            HumanReopenDelaySeconds = humanReopenDelaySeconds;
            AiReopenDelaySeconds = aiReopenDelaySeconds;
            HumanCloseDistanceTiles = humanCloseDistanceTiles;
            AiCloseDistanceTiles = aiCloseDistanceTiles;
        }

        public bool Enabled { get; }
        public double HumanReopenDelaySeconds { get; }
        public double AiReopenDelaySeconds { get; }
        public double HumanCloseDistanceTiles { get; }
        public double AiCloseDistanceTiles { get; }
    }

    public readonly struct SelectedUnitCommandContext
    {
        public SelectedUnitCommandContext(
            int tribeId,
            TribeAICommand command,
            int targetValue1,
            int targetValue2,
            int argument6)
        {
            TribeId = tribeId;
            Command = command;
            TargetValue1 = targetValue1;
            TargetValue2 = targetValue2;
            Argument6 = argument6;
        }

        public int TribeId { get; }
        public TribeAICommand Command { get; }
        public int TargetValue1 { get; }
        public int TargetValue2 { get; }
        public int Argument6 { get; }
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

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

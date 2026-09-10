using System;

namespace APIShared
{
    /// <summary>Describes whether the process-wide API has completed initialization.</summary>
    public enum NativeApiState
    {
        /// <summary>Initialization has not reached a terminal state.</summary>
        Pending,
        /// <summary>The API is published; individual capabilities may still be unavailable.</summary>
        Ready,
        /// <summary>The API could not be published safely.</summary>
        Unavailable
    }

    /// <summary>Describes the availability or failure state of one independent capability.</summary>
    public enum NativeCapabilityState
    {
        /// <summary>The API has not resolved the capability yet.</summary>
        Pending,
        /// <summary>The capability is ready for use.</summary>
        Available,
        /// <summary>The installed native game build is not catalogued.</summary>
        UnsupportedBuild,
        /// <summary>A required native target could not be found.</summary>
        PatternMissing,
        /// <summary>A native target could not be identified uniquely.</summary>
        Ambiguous,
        /// <summary>A target or runtime invariant failed validation.</summary>
        ValidationFailed,
        /// <summary>Another owner already controls an overlapping native target.</summary>
        Conflict,
        /// <summary>An unexpected capability error occurred.</summary>
        Faulted
    }

    /// <summary>Stable identifiers for capabilities exposed by this API version.</summary>
    public static class NativeCapabilityIds
    {
        /// <summary>Capability for selecting the native gatehouse distance origin.</summary>
        public const string GatehouseDistanceOrigin = "gatehouse-distance-origin";
        /// <summary>Capability for configuring gatehouse timing and closing distances.</summary>
        public const string GatehouseTiming = "gatehouse-timing";
        /// <summary>Capability for shared unit HUD categories and image overrides.</summary>
        public const string UnitHudPresentation = "unit-hud-presentation";
        /// <summary>Capability for observing the process-wide AIV build-step function.</summary>
        public const string AivBuildStep = "aiv-build-step";
    }

    /// <summary>Immutable diagnostic information returned by capability acquisition and mutation.</summary>
    public sealed class NativeCapabilityDiagnostic
    {
        /// <summary>Creates a capability diagnostic.</summary>
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

        /// <summary>Gets the stable capability identifier.</summary>
        public string CapabilityId { get; }
        /// <summary>Gets the capability state.</summary>
        public NativeCapabilityState State { get; }
        /// <summary>Gets the complete SHA-256 of the installed native game library, when available.</summary>
        public string BinaryHash { get; }
        /// <summary>Gets a human-readable explanation.</summary>
        public string Reason { get; }
        /// <summary>Gets the BepInEx GUID owning a conflicting native interval, when applicable.</summary>
        public string ConflictOwnerGuid { get; }
    }

    /// <summary>Public process-wide entry point for typed shared native capabilities.</summary>
    public interface IApiShared
    {
        /// <summary>Gets the global initialization state.</summary>
        NativeApiState State { get; }
        /// <summary>Attempts to acquire the gatehouse distance-origin capability for a stable owner GUID.</summary>
        bool TryGetGatehouseDistanceOrigin(
            string ownerGuid,
            out IGatehouseDistanceOriginCapability capability,
            out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Attempts to acquire the gatehouse timing capability for a stable owner GUID.</summary>
        bool TryGetGatehouseTiming(
            string ownerGuid,
            out IGatehouseTimingCapability capability,
            out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Attempts to acquire the process-wide unit HUD presentation capability.</summary>
        bool TryGetUnitHudPresentation(
            string ownerGuid,
            out IUnitHudPresentationCapability capability,
            out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Attempts to acquire the process-wide AIV build-step observer capability.</summary>
        bool TryGetAivBuildStep(
            string ownerGuid,
            out IAivBuildStepCapability capability,
            out NativeCapabilityDiagnostic diagnostic);
    }

    /// <summary>Static access to the process-wide API and its readiness notification.</summary>
    public static class ApiShared
    {
        /// <summary>Gets the process-wide API instance. Inspect <see cref="IApiShared.State"/> before use.</summary>
        public static IApiShared Current => ApiSharedRuntime.ProcessInstance;

        /// <summary>
        /// Registers a callback that runs when initialization reaches a terminal state. Late
        /// registrations run synchronously.
        /// </summary>
        public static void WhenReady(Action<IApiShared> callback) =>
            ApiSharedRuntime.ProcessInstance.WhenReady(callback);
    }
}

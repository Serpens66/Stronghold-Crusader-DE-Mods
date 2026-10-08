using System;

namespace APIShared
{
    /// <summary>Owner-bound access to shared capabilities. Keep this client in your process-owned runtime; it does not own or dispose shared hooks.</summary>
    public sealed class ModApiClient
    {
        private readonly ApiSharedRuntime api;

        internal ModApiClient(string ownerGuid, ApiSharedRuntime api)
        {
            if (string.IsNullOrWhiteSpace(ownerGuid))
                throw new ArgumentException("A non-empty BepInEx plugin GUID is required.", nameof(ownerGuid));
            OwnerGuid = ownerGuid;
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        /// <summary>The exact GUID used for acquisitions and owner-local registrations; never a display name.</summary>
        public string OwnerGuid { get; }
        /// <summary>Global publication state, independent of individual capability availability.</summary>
        public NativeApiState State => api.State;

        /// <summary>Runs after global initialization reaches a terminal state. Late calls run synchronously on the caller's thread; early calls use the initialization publisher thread. Exceptions are isolated; no dispatch occurs.</summary>
        public void WhenReady(Action<ModApiClient> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            api.WhenReady(_ => callback(this));
        }

        /// <summary>Acquires IMissionLifecycleCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetMissionLifecycle(out IMissionLifecycleCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetMissionLifecycle(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IGatehouseDistanceOriginCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetGatehouseDistanceOrigin(out IGatehouseDistanceOriginCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetGatehouseDistanceOrigin(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IGatehouseTimingCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetGatehouseTiming(out IGatehouseTimingCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetGatehouseTiming(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IUnitHudPresentationCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetUnitHudPresentation(out IUnitHudPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetUnitHudPresentation(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IAivBuildStepCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetAivBuildStep(out IAivBuildStepCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetAivBuildStep(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires ILobbyStateCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetLobbyState(out ILobbyStateCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetLobbyState(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IPlayerDefeatCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetPlayerDefeat(out IPlayerDefeatCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetPlayerDefeat(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IBriefingGoldPresentationCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetBriefingGoldPresentation(out IBriefingGoldPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetBriefingGoldPresentation(OwnerGuid, out capability, out diagnostic);

        /// <summary>Acquires IBuildingRepairCapability for this owner. Inspect the returned diagnostic on failure; independent capabilities remain usable.</summary>
        public bool TryGetBuildingRepair(out IBuildingRepairCapability capability, out NativeCapabilityDiagnostic diagnostic) =>
            api.TryGetBuildingRepair(OwnerGuid, out capability, out diagnostic);
    }
}

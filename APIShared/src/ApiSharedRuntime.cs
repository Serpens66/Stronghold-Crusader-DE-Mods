using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace APIShared
{
    internal sealed class ApiSharedRuntime : IApiShared
    {
        internal const string SupportedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        private readonly object sync = new object();
        private readonly List<Action<IApiShared>> readyCallbacks = new List<Action<IApiShared>>();
        private NativeApiState state;
        private string binaryHash = string.Empty;
        private GatehouseDistanceOriginService gatehouseDistanceOrigin;
        private GatehouseTimingService gatehouse;
        private UnitHudPresentationService unitHudPresentation;
        private NativeCapabilityDiagnostic gatehouseDistanceOriginDiagnostic = Pending(NativeCapabilityIds.GatehouseDistanceOrigin);
        private NativeCapabilityDiagnostic gatehouseDiagnostic = Pending(NativeCapabilityIds.GatehouseTiming);
        private NativeCapabilityDiagnostic unitHudDiagnostic = Pending(NativeCapabilityIds.UnitHudPresentation);
        private ManualLogSource log;

        internal static ApiSharedRuntime ProcessInstance { get; } = new ApiSharedRuntime();

        public NativeApiState State { get { lock (sync) return state; } }

        public void WhenReady(Action<IApiShared> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            lock (sync)
            {
                if (state == NativeApiState.Pending)
                {
                    readyCallbacks.Add(callback);
                    return;
                }
            }
            callback(this);
        }

        internal void Initialize(
            long moduleBase,
            ReadOnlySpan<byte> memory,
            string hash,
            INativeMemory nativeMemory,
            ManualLogSource logger,
            GatehouseBuildTarget gateTarget = null,
            bool installUnitHudPresentation = true)
        {
            lock (sync)
            {
                if (state != NativeApiState.Pending)
                    return;
                binaryHash = hash ?? string.Empty;
                log = logger;
            }

            NativeApiState terminalState = NativeApiState.Ready;
            try
            {
                if (installUnitHudPresentation)
                {
                    UnitHudPresentationService.TryCreate(
                        binaryHash,
                        moduleBase,
                        memory,
                        log,
                        out unitHudPresentation,
                        out unitHudDiagnostic);
                }
                else
                {
                    unitHudPresentation = null;
                    unitHudDiagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.UnitHudPresentation,
                        NativeCapabilityState.UnsupportedBuild,
                        binaryHash,
                        "Managed HUD hooks are intentionally disabled in the isolated native test harness.");
                }
                var gatehouseOwnership = new NativeOwnershipRegistry();
                var gatehouseMutationSync = new object();
                GatehouseCapabilityResolver.Resolve(
                    binaryHash,
                    moduleBase,
                    memory,
                    nativeMemory,
                    gatehouseOwnership,
                    gatehouseMutationSync,
                    log,
                    gateTarget ?? GatehouseBuildTarget.Supported,
                    out gatehouseDistanceOrigin,
                    out gatehouseDistanceOriginDiagnostic,
                    out gatehouse,
                    out gatehouseDiagnostic);
            }
            catch (Exception ex)
            {
                // Capability resolvers contain their own error boundaries. Reaching this catch
                // means publication itself failed and the API cannot be trusted globally.
                terminalState = NativeApiState.Unavailable;
                gatehouseDistanceOrigin = null;
                gatehouse = null;
                unitHudPresentation = null;
                gatehouseDistanceOriginDiagnostic = Faulted(NativeCapabilityIds.GatehouseDistanceOrigin, ex.Message);
                gatehouseDiagnostic = Faulted(NativeCapabilityIds.GatehouseTiming, ex.Message);
                unitHudDiagnostic = Faulted(NativeCapabilityIds.UnitHudPresentation, ex.Message);
                NativeApiLog.Error(log, $"APIShared initialization failed globally: build={binaryHash}, error={ex}");
            }

            Action<IApiShared>[] callbacks;
            lock (sync)
            {
                state = terminalState;
                callbacks = readyCallbacks.ToArray();
                readyCallbacks.Clear();
            }
            NativeApiLog.Info(log, $"APIShared initialized: state={terminalState}, build={binaryHash}, gatehouseDistanceOrigin={gatehouseDistanceOriginDiagnostic.State}, gatehouseTiming={gatehouseDiagnostic.State}, unitHudPresentation={unitHudDiagnostic.State}.");
            foreach (Action<IApiShared> callback in callbacks)
            {
                try { callback(this); }
                catch (Exception ex) { NativeApiLog.Error(log, $"APIShared readiness callback failed: build={binaryHash}, error={ex}"); }
            }
        }

        public bool TryGetGatehouseDistanceOrigin(
            string ownerGuid,
            out IGatehouseDistanceOriginCapability capability,
            out NativeCapabilityDiagnostic diagnostic)
        {
            capability = null;
            if (!ValidateOwner(ownerGuid, NativeCapabilityIds.GatehouseDistanceOrigin, out diagnostic))
                return false;
            lock (sync)
            {
                if (gatehouseDistanceOrigin == null)
                {
                    diagnostic = gatehouseDistanceOriginDiagnostic;
                    return false;
                }
                capability = gatehouseDistanceOrigin.Bind(ownerGuid);
                diagnostic = gatehouseDistanceOriginDiagnostic;
                return true;
            }
        }

        public bool TryGetGatehouseTiming(string ownerGuid, out IGatehouseTimingCapability capability, out NativeCapabilityDiagnostic diagnostic)
        {
            capability = null;
            if (!ValidateOwner(ownerGuid, NativeCapabilityIds.GatehouseTiming, out diagnostic))
                return false;
            lock (sync)
            {
                if (gatehouse == null)
                {
                    diagnostic = gatehouseDiagnostic;
                    return false;
                }
                capability = gatehouse.Bind(ownerGuid);
                diagnostic = gatehouseDiagnostic;
                return true;
            }
        }

        public bool TryGetUnitHudPresentation(string ownerGuid, out IUnitHudPresentationCapability capability, out NativeCapabilityDiagnostic diagnostic)
        {
            capability = null;
            if (!ValidateOwner(ownerGuid, NativeCapabilityIds.UnitHudPresentation, out diagnostic))
                return false;
            lock (sync)
            {
                if (unitHudPresentation == null)
                {
                    diagnostic = unitHudDiagnostic;
                    return false;
                }
                capability = unitHudPresentation.Bind(ownerGuid);
                diagnostic = unitHudDiagnostic;
                return true;
            }
        }

        private bool ValidateOwner(string ownerGuid, string capabilityId, out NativeCapabilityDiagnostic diagnostic)
        {
            lock (sync)
            {
                if (state == NativeApiState.Pending)
                {
                    diagnostic = Pending(capabilityId);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(ownerGuid))
                {
                    diagnostic = new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.ValidationFailed, binaryHash, "A non-empty BepInEx owner GUID is required.");
                    return false;
                }
            }
            diagnostic = null;
            return true;
        }

        internal static string ComputeSha256(ReadOnlySpan<byte> bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", string.Empty);
        }

        private NativeCapabilityDiagnostic Faulted(string capabilityId, string reason) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Faulted, binaryHash, reason);
        private static NativeCapabilityDiagnostic Pending(string capabilityId) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Pending, string.Empty, "APIShared has not completed native initialization.");
    }
}

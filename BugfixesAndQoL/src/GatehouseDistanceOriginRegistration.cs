// Feature: Own the shared gatehouse distance-origin correction through APIShared.
using APIShared;
using BepInEx.Logging;
using System;

namespace BugfixesAndQoL
{
    internal sealed class GatehouseDistanceOriginRegistration
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private IGatehouseDistanceOriginCapability capability;
        private string lastFailure;

        internal GatehouseDistanceOriginRegistration(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.SettingChanged += OnSettingChanged;
            ApiShared.WhenReady(Register);
        }

        private void Register(IApiShared api)
        {
            NativeCapabilityDiagnostic diagnostic = null;
            if (api == null || !api.TryGetGatehouseDistanceOrigin(
                    BugfixesAndQoLPlugin.PluginGuid,
                    out IGatehouseDistanceOriginCapability sharedCapability,
                    out diagnostic))
            {
                ReportFailure(diagnostic, "APIShared did not publish the gatehouse distance-origin capability");
                return;
            }

            capability = sharedCapability;
            lastFailure = null;
            Apply();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Gatehouse distance-origin correction is owned by APIShared; no local native patch is installed.");
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableMod) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableCenteredGatehouseDistanceFix))
            {
                Apply();
            }
        }

        private void Apply()
        {
            IGatehouseDistanceOriginCapability sharedCapability = capability;
            if (sharedCapability == null)
                return;

            GatehouseDistanceOrigin desired =
                settings.EnableMod && settings.EnableCenteredGatehouseDistanceFix
                    ? GatehouseDistanceOrigin.BuildingBoundsCenter
                    : GatehouseDistanceOrigin.VanillaBuildingBegin;
            if (!sharedCapability.TryApply(desired, out NativeCapabilityDiagnostic diagnostic))
                ReportFailure(diagnostic, "APIShared rejected the requested gatehouse distance origin");
            else
                lastFailure = null;
        }

        private void ReportFailure(NativeCapabilityDiagnostic diagnostic, string fallback)
        {
            string failure = diagnostic == null
                ? fallback
                : $"{fallback}: state={diagnostic.State}, reason={diagnostic.Reason}, conflictOwner={diagnostic.ConflictOwnerGuid ?? "none"}";
            if (string.Equals(lastFailure, failure, StringComparison.Ordinal))
                return;
            lastFailure = failure;
            Shared.DebugLogHelper.LogError(
                log,
                failure + ". Vanilla's gatehouse distance origin remains authoritative; independent fixes continue.");
        }
    }
}

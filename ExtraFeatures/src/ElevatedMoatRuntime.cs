using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using APIShared;

namespace ExtraFeatures
{
    internal sealed class ElevatedMoatRuntime
    {
        private const string RetiredTestPluginGuid = "ElevatedMoatTest_Serp";
        private static readonly object ProcessPatchSync = new object();
        private static ElevatedMoatPatch processPatch;
        private static bool processPatchUnavailable;
        // Reported once, and only when the feature is enabled; a disabled feature stays silent.
        private static Exception installFailure;

        private readonly ManualLogSource log;
        private CrusaderLibraryLoadContext context;

        internal ElevatedMoatRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void InitializeNative(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));

            // Installed in the LibraryLoaded window, where it costs almost nothing, instead of
            // during the first map load with the feature enabled. The hooks start logically
            // disabled; Reconcile only switches their native and managed flags.
            lock (ProcessPatchSync)
            {
                if (processPatch != null || processPatchUnavailable)
                    return;
                try
                {
                    if (Chainloader.PluginInfos.ContainsKey(RetiredTestPluginGuid))
                        throw new InvalidOperationException(
                            "ElevatedMoatTest is still loaded. Remove the retired test mod and restart to avoid overlapping native hooks.");
                    processPatch = new ElevatedMoatPatch(log, context, referenceHashMatches);
                }
                catch (Exception ex)
                {
                    processPatchUnavailable = true;
                    installFailure = ex;
                }
            }
        }

        // The shared publisher roots this callback beyond plugin startup cleanup.
        internal void RegisterTooltip(ExtraFeaturesViewModel settings)
        {
            string ownedText = null;
            if (!APIShared.Presentation.PresentationEvents.TryRegister(
                APIShared.Presentation.PresentationOperation.BuildingRollover,
                ExtraFeaturesPlugin.PluginGuid, "ElevatedMoatDescription", null, args =>
                {
                    if (!args.OriginalCompleted || !CrusaderDE.MainViewModel.viewModelLoaded) return;
                    var main = CrusaderDE.MainViewModel.Instance;
                    if (!ReferenceEquals(main.HUDmain, args.BuildingHud) || !main.RolloverBuilding_TooltipVis) return;
                    // Vanilla caches the body while hovering the same button. Recognize only
                    // the original or our own last value; never overwrite another mod's text.
                    string original = CrusaderDE.Translate.Instance.lookUpText(
                        Enums.eTextSections.TEXT_BUILDING_DESCRIPTIONS, 54);
                    string current = main.RolloverBuilding_TooltipBody;
                    if (current != original && (ownedText == null || current != ownedText)) return;
                    bool enabled = processPatch != null && !processPatchUnavailable &&
                        Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                        (APIShared.GameModes.GameModeHelper.IsMapEditor()
                            ? settings.AllowElevatedMoatAI || settings.AllowElevatedMoatHuman
                            : settings.AllowElevatedMoatHuman);
                    string next = enabled ? SerpLocalization.Get("ExtraFeatures.ElevatedMoatTooltip") : original;
                    if (current != next) main.RolloverBuilding_TooltipBody = next;
                    ownedText = enabled ? next : null;
                }, out string reason, order: int.MaxValue - 2))
                throw new InvalidOperationException(reason);
        }

        internal void Reconcile(bool allowAIPlacement, bool allowHumanPlacement)
        {
            if (context == null)
            {
                ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Unknown);
                return;
            }

            lock (ProcessPatchSync)
            {
                if (processPatch != null)
                {
                    processPatch.UpdateSettings(allowAIPlacement, allowHumanPlacement);
                    ElevatedMoatAiCapability.Publish(processPatch.IsAiEnabled
                        ? ElevatedMoatAiState.Enabled : ElevatedMoatAiState.Disabled);
                    return;
                }

                ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
                if ((allowAIPlacement || allowHumanPlacement) && installFailure != null)
                {
                    Exception failure = installFailure;
                    installFailure = null;
                    throw new InvalidOperationException(
                        "Elevated moat is unavailable for this process; its native hooks were not installed at startup.",
                        failure);
                }
            }
        }
    }
}

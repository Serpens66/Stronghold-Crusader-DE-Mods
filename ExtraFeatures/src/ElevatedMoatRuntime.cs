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

        internal void Deactivate()
        {
            lock (ProcessPatchSync)
                processPatch?.UpdateSettings(false, false);
            ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
            context = null;
        }
    }
}

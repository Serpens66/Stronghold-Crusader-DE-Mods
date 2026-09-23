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

        private readonly ManualLogSource log;
        private CrusaderLibraryLoadContext context;
        private bool referenceHashMatches;

        internal ElevatedMoatRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void InitializeNative(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.referenceHashMatches = referenceHashMatches;
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
                if (processPatchUnavailable)
                {
                    ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
                    return;
                }
                if (processPatch != null)
                {
                    processPatch.UpdateSettings(allowAIPlacement, allowHumanPlacement);
                    ElevatedMoatAiCapability.Publish(processPatch.IsAiEnabled
                        ? ElevatedMoatAiState.Enabled : ElevatedMoatAiState.Disabled);
                    return;
                }
                if (!allowAIPlacement && !allowHumanPlacement)
                {
                    ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
                    return;
                }

                if (Chainloader.PluginInfos.ContainsKey(RetiredTestPluginGuid))
                {
                    processPatchUnavailable = true;
                    throw new InvalidOperationException(
                        "ElevatedMoatTest is still loaded. Remove the retired test mod and restart to avoid overlapping native hooks.");
                }

                try
                {
                    var candidate = new ElevatedMoatPatch(
                        log,
                        context,
                        referenceHashMatches,
                        allowAIPlacement,
                        allowHumanPlacement);
                    processPatch = candidate;
                }
                catch
                {
                    processPatchUnavailable = true;
                    ElevatedMoatAiCapability.Publish(ElevatedMoatAiState.Disabled);
                    throw;
                }
                ElevatedMoatAiCapability.Publish(processPatch.IsAiEnabled
                    ? ElevatedMoatAiState.Enabled : ElevatedMoatAiState.Disabled);
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

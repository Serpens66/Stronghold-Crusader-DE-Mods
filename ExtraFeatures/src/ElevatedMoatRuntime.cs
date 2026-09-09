using BepInEx.Bootstrap;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace ExtraFeatures
{
    internal sealed class ElevatedMoatRuntime : IDisposable
    {
        private const string RetiredTestPluginGuid = "ElevatedMoatTest_Serp";

        private readonly ManualLogSource log;
        private CrusaderLibraryLoadContext context;
        private ElevatedMoatPatch patch;
        private bool referenceHashMatches;
        private bool unavailable;
        private bool activeAI;
        private bool activeHuman;

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
            if (unavailable || context == null)
                return;
            if (patch != null && activeAI == allowAIPlacement && activeHuman == allowHumanPlacement)
                return;

            DisposePatch();
            if (!allowAIPlacement && !allowHumanPlacement)
                return;

            if (Chainloader.PluginInfos.ContainsKey(RetiredTestPluginGuid))
            {
                unavailable = true;
                throw new InvalidOperationException(
                    "ElevatedMoatTest is still loaded. Remove the retired test mod and restart to avoid overlapping native hooks.");
            }

            try
            {
                patch = new ElevatedMoatPatch(
                    log,
                    context,
                    referenceHashMatches,
                    allowAIPlacement,
                    allowHumanPlacement);
                activeAI = allowAIPlacement;
                activeHuman = allowHumanPlacement;
            }
            catch
            {
                DisposePatch();
                unavailable = true;
                throw;
            }
        }

        public void Dispose()
        {
            DisposePatch();
            context = null;
        }

        private void DisposePatch()
        {
            patch?.Dispose();
            patch = null;
            activeAI = false;
            activeHuman = false;
        }
    }
}

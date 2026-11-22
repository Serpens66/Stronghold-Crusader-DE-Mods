using System;
using System.Threading;
using APIShared;
using APIShared.Multiplayer;
using BepInEx.Logging;
using R3;
using SHCDESE.API.LowLevel;
using Shared;

namespace BugfixesAndQoL.AreaConstruction
{
    internal static class AreaConstructionFeature
    {
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel model;
        private static ModApiClient api;
        private static PlacementInput runtime;
        private static StorageEmptyLimitPatch storageLimitPatch;
        private static bool registered, installed, agreed;

        internal static bool LocalEnabled => model != null && model.EnableClientFeatures && model.EnableAreaConstruction;

        internal static void Configure(ManualLogSource logger, BugfixesAndQoLViewModel settings)
        {
            if (log != null) return;
            log = logger;
            model = settings;
            api = ApiShared.ForMod(BugfixesAndQoLPlugin.PluginGuid);
            model.SettingChanged += SettingChanged;
        }

        internal static void RegisterAgreement() => SimulationAgreement.Register(BugfixesAndQoLPlugin.PluginGuid,
            StorageEmptyLimitPatch.Feature, false, () => Volatile.Read(ref storageLimitPatch)?.CheckIntegrity() == true,
            value => Volatile.Write(ref agreed, value));

        internal static void RegisterNetwork()
        {
            if (registered) return;
            registered = true;
            try
            {
                runtime = PlacementInput.Create(log);
                runtime.RegisterLifecycle(api);
            }
            catch (Exception error) { log.LogError(Stamp("Area construction backend unavailable: " + error)); }
        }

        internal static void Install(CrusaderLibraryLoadContext context)
        {
            if (installed) return;
            installed = true;
            try { NativePlacement.RequireSupportedImage(context); }
            catch (Exception error)
            {
                log.LogError(Stamp("Area construction requires the audited CrusaderDE.dll: " + error.Message));
                return;
            }

            // The single-click host rule is independent of the local drag and network adapter.
            var patch = StorageEmptyLimitPatch.TryInstall(context, log);
            if (patch != null)
            {
                MissionEvents.Ended.Subscribe(_ => patch.ClearLimits());
                MissionEvents.Started.Subscribe(note =>
                {
                    bool multiplayer = note.Context.Mode.IsRealMultiplayer;
                    bool shared = Volatile.Read(ref agreed);
                    int stockpile = model.EmptyStockpileLimit;
                    int granaryArmoury = model.EmptyGranaryArmouryLimit;
                    bool requested = model.EnableMod &&
                        (stockpile != StorageEmptyLimitPolicy.Vanilla || granaryArmoury != StorageEmptyLimitPolicy.Vanilla);
                    bool ready = patch.CheckIntegrity();
                    if (requested && multiplayer && shared && !ready)
                        SimulationAgreement.ReportFault("The agreed storage placement limit patch changed during map start.");
                    if (requested && ready && (!multiplayer || shared))
                        patch.SetLimits(stockpile, granaryArmoury);
                    else
                        patch.ClearLimits();
                });
                Volatile.Write(ref storageLimitPatch, patch);
            }

            runtime?.Install(context, patch);
        }

        private static void SettingChanged(string property)
        {
            if ((property == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                 property == nameof(BugfixesAndQoLViewModel.EnableAreaConstruction)) && !LocalEnabled)
                runtime?.CancelLocalWork();
        }

        internal static string Stamp(string message) => $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
    }
}

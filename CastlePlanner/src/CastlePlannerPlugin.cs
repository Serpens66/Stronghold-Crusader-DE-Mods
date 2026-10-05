using BepInEx;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CastlePlanner
{
    [BepInDependency(ScriptExtenderGuid, "2.10.4")]
    [BepInDependency("APIShared_Serp", "0.4.6")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CastlePlannerPlugin : BaseUnityPlugin
    {
        private delegate void SteamAwakeDelegate(SteamManager manager);
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "CastlePlanner_Serp";
        public const string PluginName = "CastlePlanner";
        public const string PluginVersion = "0.8.35";

        // The BepInEx component is destroyed during startup, so runtime state remains static.
        private static CastlePlannerRuntime runtime;
        private static FreeCastlePreviewRuntime previewRuntime;
        private static BlueprintRuntimeController blueprintRuntime;
        private static CastlePlanner.AIVPlacement.AivPlacementRuntime aivPlacementRuntime;
        private static bool libraryLoadedHandled;
        private static Hook steamAwakeHook;
        private static SteamAwakeDelegate steamAwakeOriginal;
        private static CastlePlannerSettingsViewModel processSettings;

        public CastlePlannerSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            if (runtime != null)
                return;

            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Settings = new CastlePlannerSettingsViewModel(Logger, Info.Location);
            processSettings = Settings;
            InstallSteamReadyHook();
            Shared.GameplayModActivationGate.Initialize(Logger, PluginGuid, PluginName, () => Settings.EnableMod);
            previewRuntime = new FreeCastlePreviewRuntime(Logger, Settings);
            runtime = new CastlePlannerRuntime(Logger, Settings, previewRuntime);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; the AIVJSON catalog will be cached automatically in the lobby.");
        }

        private static void InstallSteamReadyHook()
        {
            if (steamAwakeHook != null)
                return;
            MethodInfo awake = typeof(SteamManager).GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingMethodException(typeof(SteamManager).FullName, "Awake");
            Hook candidate = null;
            try
            {
                candidate = new Hook(awake, (SteamAwakeDelegate)OnSteamAwake);
                SteamAwakeDelegate original = candidate.GenerateTrampoline<SteamAwakeDelegate>();
                steamAwakeOriginal = original;
                steamAwakeHook = candidate;
            }
            catch
            {
                try { candidate?.Undo(); } catch { }
                try { candidate?.Dispose(); } catch { }
                throw;
            }
        }

        private static void OnSteamAwake(SteamManager manager)
        {
            steamAwakeOriginal(manager);
            try { processSettings?.PumpCastleCatalogLoad(); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("CastlePlanner catalog startup failed after Steam initialization: " + ex);
            }
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(
                Logger,
                "Plugin component destroyed during startup; keeping CastlePlanner lifecycle subscriptions rooted.");
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryLoadedHandled)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            libraryLoadedHandled = true;

            var failedOptionalStages = new List<string>();

            bool currentNativeLayout = false;
            try
            {
                currentNativeLayout = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    PluginName,
                    requireCurrentVersion: true);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"CastlePlanner native version check failed; native Spawn mode remains inactive: {ex}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this, Logger, PluginGuid, Settings, "ScriptExtenderUI/CastlePlannerSettings.xaml");
                Settings.PumpCastleCatalogLoad();
            }
            catch (Exception ex)
            {
                // Host settings cannot safely drive gameplay without the shared authority path.
                Shared.DebugLogHelper.LogError(Logger, $"CastlePlanner settings registration failed; runtime initialization stopped fail-closed: {ex}");
                return;
            }

            TryInitializeStage("Blueprint runtime", () =>
            {
                blueprintRuntime =
                    BlueprintRuntimeController.Create(Logger, Settings, previewRuntime);
            }, failedOptionalStages);
            TryInitializeStage("free-castle preview runtime", () =>
            {
                previewRuntime.Initialize();
            }, failedOptionalStages);
            TryInitializeStage("Blueprint HUD binding", () =>
            {
                if (blueprintRuntime == null)
                    throw new InvalidOperationException("The Blueprint runtime is unavailable.");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerBlueprintHud",
                    blueprintRuntime.Hud);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerBlueprintSettingsButton",
                    blueprintRuntime.Hud);
            }, failedOptionalStages);

            TryInitializeStage("AIV placement runtime", () =>
            {
                CastlePlanner.AIVPlacement.KeepRangeSettingsBridge.NativeCompatible = currentNativeLayout;
                aivPlacementRuntime = new CastlePlanner.AIVPlacement.AivPlacementRuntime(
                    Logger,
                    () => Settings.EnableMod && Settings.EnableAivPlacementLobby);
                Settings.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(CastlePlannerSettingsViewModel.EnableAivPlacementLobby) ||
                        args.PropertyName == nameof(CastlePlannerSettingsViewModel.EnableMod))
                    {
                        if (Settings.EnableMod && Settings.EnableAivPlacementLobby)
                            aivPlacementRuntime?.RequestRefresh();
                        else
                            aivPlacementRuntime?.Deactivate();
                    }
                };
            }, failedOptionalStages);
            TryInitializeStage("AIV selection-list binding", () =>
            {
                if (aivPlacementRuntime == null)
                    throw new InvalidOperationException("The AIV placement runtime is unavailable.");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerAivSelectionListHost",
                    aivPlacementRuntime.SelectionList);
            }, failedOptionalStages);
            TryInitializeStage("AIV placement hooks", () =>
            {
                if (aivPlacementRuntime == null)
                    throw new InvalidOperationException("The AIV placement runtime is unavailable.");
                aivPlacementRuntime.Install();
            }, failedOptionalStages);

            if (currentNativeLayout)
            {
                try
                {
                    runtime.Install(context, currentNativeLayout);
                }
                catch (Exception ex)
                {
                    failedOptionalStages.Add("native Spawn mode");
                    Shared.DebugLogHelper.LogError(
                        Logger,
                        $"CastlePlanner native Spawn mode initialization failed; independent features continue: {ex}");
                }
            }

            if (failedOptionalStages.Count == 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    "Crusader library initialization completed; all optional stages completed successfully.");
            }
            else
            {
                Shared.DebugLogHelper.LogWarning(
                    Logger,
                    $"Crusader library initialization completed with {failedOptionalStages.Count} failed optional " +
                    $"stage(s) [{string.Join(", ", failedOptionalStages)}]; independent CastlePlanner features continue.");
            }
        }

        private void TryInitializeStage(
            string stageName,
            Action initialize,
            ICollection<string> failedOptionalStages)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                failedOptionalStages.Add(stageName);
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"CastlePlanner {stageName} initialization failed; independent features continue: {ex}");
            }
        }
    }
}

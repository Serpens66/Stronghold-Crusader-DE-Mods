using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace CastlePlanner
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CastlePlannerPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "CastlePlanner_Serp";
        public const string PluginName = "CastlePlanner";
        public const string PluginVersion = "0.6.7";

        // The BepInEx component is destroyed during startup, so runtime state remains static.
        private static CastlePlannerRuntime runtime;
        private static BlueprintRuntimeController blueprintRuntime;
        private static CastleDropDownHeightController castleDropDownHeightController;
        private static CastlePlanner.AIVPlacement.AivPlacementRuntime aivPlacementRuntime;
        private static bool libraryLoadedHandled;

        public CastlePlannerSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            if (runtime != null)
                return;

            Settings = new CastlePlannerSettingsViewModel(Logger, Info.Location);
            runtime = new CastlePlannerRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; the AIVJSON catalog will be cached when mod settings are opened.");
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "Plugin component destroyed during startup; keeping CastlePlanner lifecycle subscriptions rooted.");
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryLoadedHandled)
                return;
            libraryLoadedHandled = true;

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
            }
            catch (Exception ex)
            {
                // Host settings cannot safely drive gameplay without the shared authority path.
                Shared.DebugLogHelper.LogError(Logger, $"CastlePlanner settings registration failed; runtime initialization stopped fail-closed: {ex}");
                return;
            }

            TryInitializeStage("dropdown sizing", () =>
            {
                castleDropDownHeightController =
                    CastleDropDownHeightController.Attach(Logger, Settings);
            });

            TryInitializeStage("Blueprint runtime", () =>
            {
                blueprintRuntime =
                    BlueprintRuntimeController.Create(Logger, Settings);
            });
            TryInitializeStage("Blueprint HUD binding", () =>
            {
                if (blueprintRuntime == null)
                    throw new InvalidOperationException("The Blueprint runtime is unavailable.");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerBlueprintHud",
                    blueprintRuntime.Hud);
            });

            TryInitializeStage("AIV placement runtime", () =>
            {
                aivPlacementRuntime = new CastlePlanner.AIVPlacement.AivPlacementRuntime(
                    Logger,
                    () => Settings.EnableAivPlacementLobby);
                Settings.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(CastlePlannerSettingsViewModel.EnableAivPlacementLobby) &&
                        !Settings.EnableAivPlacementLobby)
                    {
                        aivPlacementRuntime?.Deactivate();
                    }
                };
            });
            TryInitializeStage("AIV selection-list binding", () =>
            {
                if (aivPlacementRuntime == null)
                    throw new InvalidOperationException("The AIV placement runtime is unavailable.");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "CastlePlannerAivSelectionListHost",
                    aivPlacementRuntime.SelectionList);
            });
            TryInitializeStage("AIV placement hooks", () =>
            {
                if (aivPlacementRuntime == null)
                    throw new InvalidOperationException("The AIV placement runtime is unavailable.");
                aivPlacementRuntime.Install();
            });

            if (currentNativeLayout)
            {
                try
                {
                    runtime.Install(libraryHandle, memory);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        Logger,
                        $"CastlePlanner native Spawn mode initialization failed; independent features continue: {ex}");
                }
            }

            Shared.DebugLogHelper.LogInfo(
                Logger,
                "Crusader library initialization completed; failed optional stages did not block independent CastlePlanner features.");
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"CastlePlanner {stageName} initialization failed; independent features continue: {ex}");
            }
        }
    }
}

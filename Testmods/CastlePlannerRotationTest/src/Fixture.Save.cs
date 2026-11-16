using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using APIShared.Automation;
using BepInEx.Bootstrap;
using CrusaderDE;
using SHCDESE.API;

namespace CastlePlannerRotationTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> SaveReload(AutomationRun run)
        {
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            string path = Path.Combine(ConfigSettings.GetSavesPath(), "SerpAcceptance-" + runId.ToString("N") + ".sav");
            Require(!File.Exists(path), "owned acceptance save already exists");
            int beforeCount = run.InSimulation(mission, VerifyNativeKeep);
            yield return AutomationWait.Until("safe paused save", () => Director.instance.SafeToSave());
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1);
            int x = -1, y = -1;
            GameMap.instance.getRotationCentre(ref x, ref y);
            // SafeToSave drains before SaveSaveGame takes its own engine monitor.
            Require(EngineInterface.SaveSaveGame(path, screen.x, screen.y, x, y, false, false, false), "normal save writer failed");
            Require(File.Exists(path) && new FileInfo(path).Length > 0, "save missing or empty");
            log("saved path=" + path + " ownedBuildings=" + beforeCount);

            var view = MainViewModel.Instance;
            // Same public quit setup used by the established acceptance runner: otherwise
            // Vanilla reopens the retained Skirmish lobby rather than the ready main menu.
            if (view.HUDIngameMenu.restartSkirmishMapInfo != null)
            {
                view.HUDIngameMenu.restartSkirmishMapInfo = null;
                log("save reload quit cleared retained Skirmish restart info");
            }
            view.HUDmain.InGameOptions(null, null);
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonIngameMenuCommand.Execute("6");
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonConfirmationCommand.Execute("1");
            yield return AutomationWait.Until("quit front end", () => FatControler.currentScene == Enums.SceneIDS.FrontEnd);
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            for (int attempt = 0; attempt < 10 && !MainMenuReady(); attempt++)
            {
                view = MainViewModel.Instance;
                string action;
                if (view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSM || view.Show_HUD_ConfirmationSands)
                { action = "confirm"; view.ButtonConfirmationCommand.Execute("1"); }
                else if (view.FRONTMultiplayer != null && view.FRONTMultiplayer.panelActive && view.Show_MPGameCreation)
                { action = "lobby-back"; view.FRONTMultiplayer.ButtonClicked("Back"); }
                else { action = "back-main"; view.FrontEndMenu.ButtonClicked("BackMain"); }
                log("save reload navigation attempt=" + attempt + " action=" + action + " scene=" + FatControler.currentScene +
                    " main=" + view.Show_Frontend_MainMenu + " intro=" + view.Show_IntroSequence +
                    " lobby=" + (view.FRONTMultiplayer != null && view.FRONTMultiplayer.panelActive) +
                    " creation=" + view.Show_MPGameCreation);
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            }
            log("save reload menu navigation ended ready=" + MainMenuReady() + " scene=" + FatControler.currentScene);
            yield return AutomationWait.Until("ready main menu", MainMenuReady);
            foreach (var wait in run.LoadSave(path)) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
            CloseBriefing();
            Require(!Convert.ToBoolean(Get(preview, "IsPreviewActive")), "save loading incorrectly repeated castle selection");
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            int restored = run.InSimulation(run.CurrentMission, VerifyNativeKeep);
            Require(restored == beforeCount, "save reload duplicated or removed castle buildings: " + beforeCount + " -> " + restored);

            object plugin = Chainloader.PluginInfos["CastlePlanner_Serp"].Instance;
            object blueprint = plugin.GetType().GetField("blueprintRuntime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            MethodInfo directionReader = blueprint.GetType().GetMethod("TryGetPreviewRotation", Instance);
            Type directionType = directionReader.GetParameters()[0].ParameterType.GetElementType();
            object[] args = { Activator.CreateInstance(directionType) };
            Require((bool)directionReader.Invoke(blueprint, args) && Convert.ToInt32(args[0]) == expectedRotation * 45,
                "loaded Blueprint did not recover actual Keep rotation");
            object hud = Get(blueprint, "Hud");
            var toggle = (ICommand)Get(hud, "ToggleCommand");
            Require(toggle.CanExecute(null), "loaded Blueprint toggle unavailable");
            toggle.Execute(null);
            yield return AutomationWait.Until("loaded rotated Blueprint layout", () => Field(blueprint, "layout") != null);
            object layout = Field(blueprint, "layout");
            Require(Convert.ToInt32(Get(layout, "CastleRotation")) == expectedRotation * 45, "loaded Blueprint layout rotation mismatch");
            log("save reload kept direction=" + expectedRotation + " ownedBuildings=" + restored + " BlueprintRotation=" + Get(layout, "CastleRotation"));
        }

        private static bool MainMenuReady() => MainViewModel.viewModelLoaded &&
            FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
            !MainViewModel.Instance.Show_IntroSequence;
    }
}

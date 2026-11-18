using System;
using System.Collections.Generic;
using System.IO;
using APIShared.Automation;
using APIShared.Units.AiMilitary;
using CrusaderDE;

namespace AiGroupAdmissionTest
{
    internal sealed partial class Fixture
    {
        // Save and reload: the second round then joins groups that already have members and whose
        // current order comes from the save (or is unknown), the case the controller handoff covers.
        private IEnumerable<AutomationWait> SaveReload(AutomationRun run)
        {
            // Only this run-owned name is permitted; the runner archives and removes it afterwards.
            string path = Path.Combine(ConfigSettings.GetSavesPath(), "SerpAcceptance-" + runId.ToString("N") + ".sav");
            Require(!File.Exists(path), "Owned acceptance save already exists");
            yield return AutomationWait.Until("safe paused save", () => Director.instance.SafeToSave());
            // SafeToSave opens a pause scope; balance it before SaveSaveGame opens its own.
            Director.instance.FinishedSaving();
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1);
            int cx = -1, cy = -1;
            GameMap.instance.getRotationCentre(ref cx, ref cy);
            Require(EngineInterface.SaveSaveGame(path, screen.x, screen.y, cx, cy, false, false, false), "Native save writer rejected the fixture");
            Require(File.Exists(path) && new FileInfo(path).Length > 0, "Acceptance save missing or empty");
            log("saved path=" + path);

            var view = MainViewModel.Instance;
            if (view.HUDIngameMenu.restartSkirmishMapInfo != null) view.HUDIngameMenu.restartSkirmishMapInfo = null;
            view.HUDmain.InGameOptions(null, null);
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonIngameMenuCommand.Execute("6");
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonConfirmationCommand.Execute("1");
            yield return AutomationWait.Until("quit to front end", () => FatControler.currentScene == Enums.SceneIDS.FrontEnd);
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            for (int attempt = 0; attempt < 10 && !MainMenuReady(); attempt++)
            {
                view = MainViewModel.Instance;
                if (view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSM || view.Show_HUD_ConfirmationSands)
                    view.ButtonConfirmationCommand.Execute("1");
                else if (view.FRONTMultiplayer != null && view.FRONTMultiplayer.panelActive && view.Show_MPGameCreation)
                    view.FRONTMultiplayer.ButtonClicked("Back");
                else view.FrontEndMenu.ButtonClicked("BackMain");
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
            }
            yield return AutomationWait.Until("ready main menu", MainMenuReady);
            foreach (var wait in run.LoadSave(path)) yield return wait;
            foreach (var wait in EnterMission(run)) yield return wait;
            Require(run.InSimulation(run.CurrentMission, FindOwner), "AI owner not available after load");
            run.InSimulation(run.CurrentMission, () => { LogLedger("loaded-start"); return true; });
            int restored = run.InSimulation(run.CurrentMission, () =>
            {
                int count = 0;
                foreach (var probe in Current("fresh"))
                    if (probe.Checked && api.TryGetState(probe.Unit, out var state) && state.Status == AiMilitaryAssignmentStatus.Applied) count++;
                return count;
            });
            log("loaded owner=" + owner + " restoredAppliedStates=" + restored);
        }

        private static bool MainMenuReady() => MainViewModel.viewModelLoaded &&
            FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
            !MainViewModel.Instance.Show_IntroSequence;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using APIShared.Automation;
using APIShared.Buildings;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;

namespace ConstructionCostsTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> SaveReload(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                int before = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                Require(PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out wellId, out wellGlobal, out wellX, out wellY), "No legal paid well for save/load receipt acceptance");
                Require(before - GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) == 32 && ReceiptCount() == 1,
                    "Save fixture requires actual native20 + Human12 charge and one paid receipt");
                paidWellExtra = 12;
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;

            // The shared runner permits exactly this run-owned filename, archives it, verifies
            // unrelated user saves, and removes the one acceptance save after the process exits.
            string path = Path.Combine(ConfigSettings.GetSavesPath(), "SerpAcceptance-" + runId.ToString("N") + ".sav");
            Require(!File.Exists(path), "Owned acceptance save already exists");
            yield return AutomationWait.Until("safe paused construction receipt save", () => Director.instance.SafeToSave());
            // SafeToSave is a stateful pause scope, not a pure readiness predicate.
            // Balance this successful probe before SaveSaveGame opens its own scope,
            // otherwise the nested call retains managed pause after writing the file.
            Director.instance.FinishedSaving();
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1);
            int x = -1, y = -1;
            GameMap.instance.getRotationCentre(ref x, ref y);
            Require(EngineInterface.SaveSaveGame(path, screen.x, screen.y, x, y, false, false, false), "Normal game save writer rejected construction fixture");
            Require(File.Exists(path) && new FileInfo(path).Length > 0, "Construction acceptance save missing or empty");
            log("saved paid well id=" + wellId + ":" + wellGlobal + " receipts=1 path=" + path);

            // Consume the live receipt through real demolition after saving. The later
            // restored receipt must come from disk rather than surviving process memory.
            foreach (var wait in Refund(run)) yield return wait;
            Require(ReceiptCount() == 0, "Pre-load live demolition must consume the saved object's receipt");

            extras.SetCosts(well, new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 99 } });
            extras.SetActive(false);
            Require(!extras.TryGetCosts(well, out _), "Changed future-cost configuration must be disabled before load");
            var view = MainViewModel.Instance;
            if (view.HUDIngameMenu.restartSkirmishMapInfo != null) view.HUDIngameMenu.restartSkirmishMapInfo = null;
            view.HUDmain.InGameOptions(null, null);
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonIngameMenuCommand.Execute("6");
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonConfirmationCommand.Execute("1");
            yield return AutomationWait.Until("construction save quit front end", () => FatControler.currentScene == Enums.SceneIDS.FrontEnd);
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
            yield return AutomationWait.Until("construction save ready main menu", MainMenuReady);
            Require(ReceiptCount() == 0, "No live receipt may mask archive restoration");
            foreach (var wait in run.LoadSave(path)) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("loaded construction briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            run.InSimulation(run.CurrentMission, () => {
                Require(GamePlayerManagerAPI.Instance.GetLocalPlayerId() == owner, "Loaded construction owner changed");
                Require(!Removed(wellId, wellGlobal) && ReceiptCount() == 1, "Archive must restore exactly the real paid building generation and its receipt");
                Require(!extras.TryGetCosts(well, out _), "Load must not reactivate disabled future-cost configuration");
                // The load reapplies player mod settings. Keep the native refund base at20;
                // the saved Human payment must remain12 despite disabled future config99.
                foreach (var good in NativeGoods)
                    Require(ConstructionCosts.TrySetCurrent(well, good, good == eGoods.STORED_GOLD ? 20 : 0, out string why), why);
                log("PASS save-load actual receipt restored=1 futureExtra99 disabled=true originalPaidExtra12; next step verifies one refund");
                return true;
            });
        }

        private static bool MainMenuReady() => MainViewModel.viewModelLoaded &&
            FatControler.currentScene == Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu &&
            !MainViewModel.Instance.Show_IntroSequence;
    }
}

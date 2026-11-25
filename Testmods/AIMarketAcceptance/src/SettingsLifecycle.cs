using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.ModSettings;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketAcceptance
{
    internal static partial class Acceptance
    {
        private const string MainGuid="BugfixesAndQoL_Serp";

        private static PresetLobbyModSettingsViewModel MainSettings()
        {
            Require(ModSettingsApplication.Endpoints.TryGetValue(MainGuid,out var model),"main mod has no public APIShared settings endpoint");
            return model;
        }

        private static void SetMainSettings(AutomationRun run,bool enableMod,bool enableMarket,bool resetDefaults=false)
        {
            Require(FatControler.currentScene==Enums.SceneIDS.FrontEnd&&run.CurrentMission==null,
                "settings may only change in the front end after mission retirement");
            var model=MainSettings();
            if(resetDefaults)
            {
                model.System_LoadModDefaults();
                Require(ReadSetting(model,"EnableAiMarketOverbuyFix"),"integrated market option does not default to enabled");
            }
            WriteSetting(model,"EnableMod",enableMod);
            WriteSetting(model,"EnableAiMarketOverbuyFix",enableMarket);
            Info("SETTINGS_FRONTEND EnableMod="+enableMod+" EnableAiMarketOverbuyFix="+enableMarket+" resetDefaults="+resetDefaults);
        }

        private static void WriteSetting(object model,string name,bool value)
        {
            PropertyInfo property=model.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
            Require(property!=null&&property.PropertyType==typeof(bool)&&property.GetSetMethod()!=null,"public boolean setting unavailable: "+name);
            property.SetValue(model,value,null);
            Require(ReadSetting(model,name)==value,"setting setter rejected "+name);
        }

        private static bool ReadSetting(object model,string name)
        {
            PropertyInfo property=model.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
            Require(property!=null&&property.PropertyType==typeof(bool),"public boolean setting unavailable: "+name);
            return (bool)property.GetValue(model,null);
        }

        private static void CheckMainSettings(bool enableMod,bool enableMarket)
        {
            object model=MainSettings();
            Require(ReadSetting(model,"EnableMod")==enableMod&&ReadSetting(model,"EnableAiMarketOverbuyFix")==enableMarket,
                "save settings overrode the explicit test configuration");
        }

        private static void PrepareCurrentSettingsLoad(string path)
        {
            var choice=SavegameModSettings.GetLoadChoiceState(path);
            Require(choice!=SavegameLoadChoiceState.SavedOnly,"test fixture forbids the current-settings load choice");
            SavegameModSettings.PrepareLoad(path,true);
            Info("LOAD_CURRENT_SETTINGS path="+path+" choice="+choice);
        }

        private static IEnumerable<AutomationWait> LoadInitialFixture(AutomationRun run)
        {
            yield return AutomationWait.Until("ready front end before initial test settings",()=>
                FatControler.currentScene==Enums.SceneIDS.FrontEnd&&run.CurrentMission==null&&MainViewModel.viewModelLoaded&&MainViewModel.Instance.Show_Frontend_MainMenu);
            SetMainSettings(run,true,true,true);
            fixture=Path.GetFullPath(Path.IsPathRooted(fixture)?fixture:Path.Combine(ConfigSettings.GetSavesPath(),fixture));
            PrepareCurrentSettingsLoad(fixture);
            foreach(var wait in run.LoadSave(fixture))yield return wait;
            CheckMainSettings(true,true);
        }

        private static IEnumerable<AutomationWait> LoadSettingsCase(AutomationRun run,bool enableMod,bool enableMarket,int restoreOrdinal)
        {
            Volatile.Write(ref expectRestore,0);Volatile.Write(ref rawVerified,0);Volatile.Write(ref terminalArmed,0);
            marketObserver.SetEnabled(false);
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            yield return AutomationWait.Until("front end and retired mission before settings change",()=>
                FatControler.currentScene==Enums.SceneIDS.FrontEnd&&run.CurrentMission==null&&MainViewModel.viewModelLoaded&&MainViewModel.Instance.Show_Frontend_MainMenu);
            SetMainSettings(run,enableMod,enableMarket);
            Require(SavegameModSettings.CanUseCurrentSettings(save),"run-owned custom-game save does not permit current settings");
            PrepareCurrentSettingsLoad(save);
            if(restoreOrdinal>0)
            {
                Volatile.Write(ref expectRestore,restoreOrdinal);
                marketObserver.SetEnabled(true);
            }
            foreach(var wait in run.LoadSave(save))yield return wait;
            CheckMainSettings(enableMod,enableMarket);
        }

        private static IEnumerable<AutomationWait> CheckDisabled(AutomationRun run,bool enableMod,bool enableMarket)
        {
            var mission=run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            foreach(var wait in run.SetPause(mission,true))yield return wait;
            run.InSimulation(mission,()=>{
                CheckMainSettings(enableMod,enableMarket);
                bool correction=(bool)owner.GetProperty("CorrectionActive",Static).GetValue(null,null);
                Require(!correction&&ReadGate("writerGate")==0&&ReadGate("receiptGate")==0,"disabled feature retained a native mutation gate");
                var players=GamePlayerManagerAPI.Instance;
                State before=Read(player,good);
                Require(before.Pending==4&&before.Stock==0,"disabled test did not load the original partial native state");
                int gold=players.GetPlayerGold(player);
                Require(players.TryAddGood(player,(eGoods)good,1),"disabled test physical delivery failed");
                State delivered=Read(player,good);
                Require(delivered.Pending==4&&delivered.Stock==1&&players.GetPlayerGold(player)==gold,"disabled fix changed the native pending amount on delivery");
                players.RemoveGood(player,(eGoods)good,1);
                State consumed=Read(player,good);
                Require(consumed.Pending==4&&consumed.Stock==0&&players.GetPlayerGold(player)==gold,"disabled fix changed native pending on consumption");
                Info("SETTINGS_DISABLED_PASS EnableMod="+enableMod+" EnableAiMarketOverbuyFix="+enableMarket+
                    " player="+player+" good="+good+" pendingBefore=4 delivered=1 consumed=1 pendingAfter=4 writerGate=0 receiptGate=0 fixturePendingWrites=0");
                return true;
            });
        }
    }
}

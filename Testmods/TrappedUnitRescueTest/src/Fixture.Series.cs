using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Automation;
using CrusaderDE;

namespace TrappedUnitRescueTest
{
    internal static class RescueScenarioSelection
    {
        internal static bool IsMountedKeepPart(string name)=>TryMountedKeepPart(name,out _,out _);
        internal static bool TryMountedKeepPart(string name,out int keep,out int rotation)
        {
            keep=0;rotation=0;
            if(name=="rescue-mountedkeep-controls-on" || name=="rescue-mountedkeep-controls-off")return true;
            foreach(int candidate in new[]{2,3,1})foreach(int direction in new[]{0,2,4,6})
                if(name=="rescue-mountedkeep-k"+candidate+"-r"+direction+"-on" ||
                    name=="rescue-mountedkeep-k"+candidate+"-r"+direction+"-off")
                {keep=candidate;rotation=direction;return true;}
            return false;
        }
        internal static bool TryExpand(string[] selection,out string[] expanded)
        {
            expanded=null;if(selection==null || selection.Length==0 || selection.Length>MaximumCases)return false;
            var result=new List<string>();
            foreach(string name in selection)
            {
                if(string.IsNullOrWhiteSpace(name))return false;
                if(name=="rescue-mountedkeep-on" || name=="rescue-mountedkeep-off")
                {
                    string option=name.EndsWith("-on",StringComparison.Ordinal)?"on":"off";
                    result.Add("rescue-mountedkeep-controls-"+option);
                    foreach(int keep in new[]{2,3,1})foreach(int rotation in new[]{0,2,4,6})
                        result.Add("rescue-mountedkeep-k"+keep+"-r"+rotation+"-"+option);
                }
                else result.Add(name);
                if(result.Count>MaximumCases)return false;
            }
            expanded=result.ToArray();return true;
        }
        internal const string SeriesRequest="rescue-series";
        internal const int MaximumCases=64;
        internal static bool TryParse(string selection,Func<string,bool> owns,out string[] cases)
        {
            cases=null;if(string.IsNullOrWhiteSpace(selection) || owns==null || selection.IndexOfAny(new[]{'\r','\n'})>=0)return false;
            var parts=selection.Split(',');if(parts.Length<1 || parts.Length>MaximumCases)return false;
            for(int i=0;i<parts.Length;i++)
            {parts[i]=parts[i].Trim();if(parts[i].Length==0 || !owns(parts[i]))return false;}
            cases=parts;return true; // Explicit repetitions are useful; step prefixes include their index.
        }
    }
    internal sealed class RescueSeriesCase
    {
        internal readonly string Name;
        internal readonly IEnumerable<AutomationStep> Steps;
        internal readonly Action Select,Stop;
        internal RescueSeriesCase(string name,IEnumerable<AutomationStep> steps,Action select,Action stop)
        {Name=name??throw new ArgumentNullException(nameof(name));Steps=steps??throw new ArgumentNullException(nameof(steps));
            Select=select??throw new ArgumentNullException(nameof(select));Stop=stop??throw new ArgumentNullException(nameof(stop));}
    }
    // SDK0.74 makes step metadata internal. Read that immutable contract rather
    // than copying its runner or every scenario's step routing into this testmod.
    internal static class RescueSeriesStepMetadata
    {
        private static readonly PropertyInfo Name=Property("Name",typeof(string));
        private static readonly PropertyInfo Timeout=Property("Timeout",typeof(TimeSpan));
        private static readonly PropertyInfo Execute=Property("Execute",typeof(Func<AutomationRun,IEnumerable<AutomationWait>>));
        private static readonly PropertyInfo MissionBound=Property("MissionBound",typeof(bool));
        private static PropertyInfo Property(string name,Type type)
        {
            var p=typeof(AutomationStep).GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(p==null || p.PropertyType!=type || p.GetGetMethod(true)==null || p.GetIndexParameters().Length!=0)
                throw new InvalidOperationException("Unsupported AutomationStep metadata contract: "+name);
            return p;
        }
        internal static AutomationStep Prefix(AutomationStep step,string prefix)
        {
            if(step==null || string.IsNullOrEmpty(prefix))throw new ArgumentException("A step and unique series prefix are required.");
            return new AutomationStep(prefix+(string)Name.GetValue(step,null),(TimeSpan)Timeout.GetValue(step,null),
                (Func<AutomationRun,IEnumerable<AutomationWait>>)Execute.GetValue(step,null),(bool)MissionBound.GetValue(step,null));
        }
    }
    internal sealed class RescueScenarioSeries
    {
        private readonly RescueSeriesCase[] cases;
        private readonly Action<string> log;
        private readonly Dictionary<long,RescueSeriesCase> sessions=new Dictionary<long,RescueSeriesCase>();
        private RescueSeriesCase current;
        internal RescueScenarioSeries(RescueSeriesCase[] cases,Action<string> log)
        {
            if(cases==null || cases.Length<1 || cases.Length>RescueScenarioSelection.MaximumCases)throw new ArgumentException("Provide a bounded nonempty series.");
            this.cases=(RescueSeriesCase[])cases.Clone();this.log=log??throw new ArgumentNullException(nameof(log));
            foreach(var item in this.cases)if(item==null)throw new ArgumentException("Null series case.");
        }
        internal void MissionStarted(long session)
        {if(current!=null && session>0)sessions[session]=current;}
        internal void MissionEnded(long session)
        {if(sessions.TryGetValue(session,out var ended)){sessions.Remove(session);ended.Stop();}}
        internal void Stop()
        {foreach(var item in cases)item.Stop();sessions.Clear();current=null;}
        internal IEnumerable<AutomationStep> Steps()
        {
            for(int index=0;index<cases.Length;index++)
            {
                var item=cases[index];string prefix="s"+(index+1).ToString("D2")+"-"+item.Name+"-";
                yield return new AutomationStep(prefix+"select",TimeSpan.FromSeconds(90),run=>Select(run,item));
                foreach(var step in item.Steps)yield return RescueSeriesStepMetadata.Prefix(step,prefix);
                // Not mission-bound: the normal quit path intentionally ends it.
                yield return new AutomationStep(prefix+"return-menu",TimeSpan.FromSeconds(120),ReturnToMenu);
            }
        }
        private IEnumerable<AutomationWait> Select(AutomationRun run,RescueSeriesCase item)
        {
            yield return AutomationWait.Until("series main menu before selecting "+item.Name,()=>MenuReady(run));
            current=item;item.Select();log("series-case-selected scenario="+item.Name);
        }
        // All members below were checked public in the installed non-publicized
        // Assembly-CSharp.dll (26.11.2026). Commands use Vanilla's ordinary UI path.
        private IEnumerable<AutomationWait> ReturnToMenu(AutomationRun run)
        {
            if(run.CurrentMission!=null)
            {
                if(!MainViewModel.viewModelLoaded || MainViewModel.Instance.HUDmain==null || MainViewModel.Instance.HUDIngameMenu==null)
                    throw new InvalidOperationException("Ordinary in-game menu is unavailable.");
                // Vanilla otherwise reopens the remembered Skirmish lobby. This is
                // the documented public menu-return path, not a scene/mission reset.
                if(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo!=null)
                {
                    MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo=null;
                    log("series-quit cleared-skirmish-restart-info=true");
                }
                LogMenuState(run,"before-quit",-1);
                MainViewModel.Instance.HUDmain.InGameOptions(null,null);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
                MainViewModel.Instance.ButtonIngameMenuCommand.Execute("6");
                yield return AutomationWait.Until("ordinary quit confirmation",ConfirmationVisible);
                MainViewModel.Instance.ButtonConfirmationCommand.Execute("1");
                yield return AutomationWait.Until("mission ended in front end",()=>run.CurrentMission==null && FatControler.currentScene==Enums.SceneIDS.FrontEnd);
                LogMenuState(run,"front-end",-1);
            }
            for(int attempt=0;attempt<10 && !MenuReady(run);attempt++)
            {
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
                LogMenuState(run,"navigation-before",attempt);
                if(MenuReady(run))break;
                if(!MainViewModel.viewModelLoaded)continue;
                var view=MainViewModel.Instance;
                string action="waiting-frontend-menu";
                if(ConfirmationVisible()){action="confirm";view.ButtonConfirmationCommand.Execute("1");}
                else if(view.FRONTMultiplayer!=null && view.FRONTMultiplayer.panelActive && view.Show_MPGameCreation)
                {action="lobby-back";view.FRONTMultiplayer.ButtonClicked("Back");}
                else if(view.FrontEndMenu!=null){action="back-main";view.FrontEndMenu.ButtonClicked("BackMain");}
                LogMenuState(run,"navigation-after-"+action,attempt);
            }
            LogMenuState(run,"final-readiness",-1);
            yield return AutomationWait.Until("main menu ready after ordinary quit",()=>MenuReady(run));
            log("series-case-ended scenario="+(current?.Name??"unknown")+" menu=true");
        }
        private void LogMenuState(AutomationRun run,string stage,int attempt)
        {
            bool loaded=MainViewModel.viewModelLoaded;
            var view=loaded?MainViewModel.Instance:null;
            log("series-menu-navigation stage="+stage+" attempt="+attempt+" scene="+FatControler.currentScene+
                " session="+(run.CurrentMission?.SessionId??0)+" viewLoaded="+loaded+
                " mainMenu="+(view!=null && view.Show_Frontend_MainMenu)+" intro="+(view!=null && view.Show_IntroSequence)+
                " lobby="+(view?.FRONTMultiplayer!=null && view.FRONTMultiplayer.panelActive)+" showMP="+(view!=null && view.Show_MPGameCreation)+
                " hud="+(view?.HUDmain!=null)+" ingameMenu="+(view?.HUDIngameMenu!=null)+
                " director="+(Director.instance!=null)+" editor="+(EditorDirector.instance!=null)+" ready="+MenuReady(run));
        }
        private static bool ConfirmationVisible()
        {
            if(!MainViewModel.viewModelLoaded)return false;
            var view=MainViewModel.Instance;
            return view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSM || view.Show_HUD_ConfirmationSands;
        }
        internal static bool MenuReady(AutomationRun run)
        {
            if(run.CurrentMission!=null || !MainViewModel.viewModelLoaded || FatControler.currentScene!=Enums.SceneIDS.FrontEnd)return false;
            var view=MainViewModel.Instance;
            return view.Show_Frontend_MainMenu && !view.Show_IntroSequence && view.HUDmain!=null && view.HUDIngameMenu!=null &&
                Director.instance!=null && EditorDirector.instance!=null;
        }
    }
}

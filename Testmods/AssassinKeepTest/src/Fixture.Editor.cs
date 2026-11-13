using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;

namespace AssassinKeepTest
{
    internal sealed partial class Fixture
    {
        private readonly int unityThread=Thread.CurrentThread.ManagedThreadId;
        private FieldInfo editorMonitorField;
        private IEnumerable<AutomationWait> StartEditor(AutomationRun run)
        {
            yield return AutomationWait.Until("ready main menu for exact editor map",()=>run.CurrentMission==null && MainViewModel.viewModelLoaded &&
                FatControler.currentScene==Enums.SceneIDS.FrontEnd && MainViewModel.Instance.Show_Frontend_MainMenu && !MainViewModel.Instance.Show_IntroSequence &&
                MainViewModel.Instance.HUDmain!=null && EditorDirector.instance!=null && Director.instance!=null);
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","Firefly Studios","Stronghold Crusader Definitive Edition","Maps","test_assassinkeep.map");
            Require(File.Exists(path),"Exact editor map is missing: "+path);
            var header=MapFileManager.Instance.GetFileInfoFromFileName(path,path,0,true);
            Require(header!=null && header.isMapEditable(),"Vanilla rejected the exact editor map header");
            long before=run.CurrentMission?.SessionId??0;
            // Exact public callback sequence from FRONT_EditorSetup's existing Load handler.
            GameData.Instance.SetMissionTextFromHeader(header);
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
            Director.instance.SetPausedState(false);
            Require(EditorDirector.instance.loadMapIntoEditor(header.filePath,header.standAlone_filename),"Public editor load failed");
            yield return AutomationWait.Until("new managed editor mission",()=>run.CurrentMission!=null && run.CurrentMission.SessionId!=before);
            Require(run.CurrentMission.Mode.IsMapEditor && !run.CurrentMission.Mode.IsRealMultiplayer &&
                string.Equals(Path.GetFullPath(run.CurrentMission.FilePath),path,StringComparison.OrdinalIgnoreCase),"Unexpected editor mission provenance");
            log("EDITOR_LOADED session="+run.CurrentMission.SessionId+" mode="+run.CurrentMission.Mode.Kind+" file="+path);
        }
        private T Simulation<T>(AutomationRun run,Func<T> action)
        {
            if(!Editor)return run.InSimulation(run.CurrentMission,action);
            Require(Thread.CurrentThread.ManagedThreadId==unityThread,"Editor monitor adapter requires Unity thread");
            var expected=run.CurrentMission;
            Require(expected!=null && expected.Mode.IsMapEditor && !expected.Mode.IsRealMultiplayer,"Expected current local editor mission");
            if(editorMonitorField==null)
            {
                var candidate=typeof(EngineInterface).GetField("threadLock",BindingFlags.NonPublic|BindingFlags.Static);
                Require(candidate!=null && candidate.IsPrivate && candidate.IsStatic && candidate.FieldType==typeof(object),"Audited private static object EngineInterface.threadLock contract changed");
                editorMonitorField=candidate;
            }
            object monitor=editorMonitorField.GetValue(null);Require(monitor!=null,"Engine simulation monitor unavailable");
            if(!Monitor.TryEnter(monitor,100))throw new InvalidOperationException("Editor simulation is busy; pause before preparing test state");
            try
            {
                Require(run.CurrentMission?.SessionId==expected.SessionId && run.CurrentMission.Mode.IsMapEditor &&
                    ReferenceEquals(editorMonitorField.GetValue(null),monitor) && Director.instance!=null && Director.instance.SimRunning &&
                    GameData.Instance.lastGameState!=null,"Editor mission/monitor/readiness changed before simulation work");
                return action();
            }
            finally{Monitor.Exit(monitor);}
        }
        private IEnumerable<AutomationWait> Pause(AutomationRun run,bool paused)
        {
            if(!Editor){foreach(var wait in run.SetPause(run.CurrentMission,paused))yield return wait;yield break;}
            Require(run.CurrentMission?.Mode.IsMapEditor==true && Thread.CurrentThread.ManagedThreadId==unityThread,"Expected Unity editor pause request");
            Require(!Director.instance.Paused,"Close the editor's managed pause before changing native pause");
            if(Simulation(run,()=>GamePlayerManagerAPI.Instance.IsLocalPaused())!=paused)
            {
                int value=paused?1:0;
                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused,value,value);
            }
            yield return AutomationWait.Until("editor native pause="+paused,()=>Simulation(run,()=>GamePlayerManagerAPI.Instance.IsLocalPaused())==paused);
        }
    }
}

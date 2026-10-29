using BepInEx;
using SHCDESE.API.LowLevel;
using APIShared.ModSettings;
using System;
namespace LordOverviewTest {
 [BepInPlugin("LordOverviewTest_Serp","Lord Overview Test","0.1.0")]
 [BepInDependency("000shcdese","2.14.0")]
 [BepInDependency("APIShared_Serp","0.18.0")]
 public sealed class Plugin:BaseUnityPlugin {
  private static OverviewSettings settings;
  private static bool initialized;
  private void Awake(){var log=Logger;CrusaderLibrary.Instance.LibraryLoaded+=context=>{
   if(initialized)return;initialized=true;
   try{settings=new OverviewSettings();
    LobbyModSettingsPresetRegistration.Register(this,log,"Lord Overview Test",settings,"ScriptExtenderUI/LordOverviewTestSettings.xaml",logRoutineActivity:false);
    OverviewRuntime.Start(log,settings);
   }catch(Exception error){log.LogError("["+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+"] LORD_OVERVIEW_INITIALIZATION: "+error);}
  };}
 }
}

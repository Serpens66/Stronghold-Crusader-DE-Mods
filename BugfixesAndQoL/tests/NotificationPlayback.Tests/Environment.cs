// Isolated adapters. Original HUD/SFX predicates mirror the audited installed assemblies;
// the two feature classes are compiled unmodified from production sources.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Noesis;
using NoesisApp;
using CrusaderDE;

namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace Shared { public static class DebugLogHelper {
 public static readonly List<string> Errors = new List<string>();
 public static void LogDebug(object a,string b) { }
 public static void LogError(object a,string b) { Errors.Add(b); }
 public static void LogErrorLimited(object a,string b,string c) { Errors.Add(c); }
} }
namespace Noesis { public class RoutedEventArgs { } }
namespace NoesisApp { public class MediaElement {
 public Uri Source; public float Opacity=1; public bool Paused; public int Closes;
 public void Pause(){Paused=true;} public void Stop(){Paused=false;}
 public void Close(){Closes++;}
} }
namespace UnityEngine {
 public class AudioClip { public bool Unloaded; public void UnloadAudioData(){Unloaded=true;} }
 public class AudioSource { public float volume; public bool Paused,Playing; public int Plays,Stops;
  public void PlayOneShot(AudioClip clip){Plays++;Playing=true;} public void Pause(){Paused=true;}
  public void Stop(){Stops++;Playing=false;Paused=false;}
 }
 public static class Application { public static string streamingAssetsPath="Streaming",dataPath="Data"; }
}
public static class ConfigSettings { public static bool Settings_EnglishSpeech=true,Settings_ReduceMusicVolumeForSpeech=true;
 public static float Settings_UnitSpeechVolume=1,Settings_SpeechVolume=1; }
public class FatControler { public static bool UsesEnglishSpeechFolder()=>false; }
public class MyAudioManager {
 public static MyAudioManager Instance;
 private UnityEngine.AudioSource speechSource1=new UnityEngine.AudioSource();
 private UnityEngine.AudioClip speechClip1=null;
 private int speechMode1; private bool speechPaused,ignoreSpeechMuting=false;
 public string LastPath; public TaskCompletionSource<UnityEngine.AudioClip> Pending;
 public UnityEngine.AudioSource Source=>speechSource1;
 public int Mode { get=>speechMode1; set=>speechMode1=value; }
 public bool Paused { get=>speechPaused; set {speechPaused=value;speechSource1.Paused=value;} }
 public bool Muting=>ignoreSpeechMuting; public UnityEngine.AudioClip Clip=>speechClip1;
 public bool isSpeechPlaying(int channel)=>channel==1 && speechMode1!=0;
 public static float GetMasterVolume()=>1;
 public void setMusicFadedState(bool value) { }
 public void PlaySpeech(int channel,string folder,string name,bool force=true){
  if(channel==1){if(!force && speechMode1!=0)return;speechMode1=1;}
  LoadClip(channel,folder,name,false,false);
 }
 private void LoadClip(int channel,string folder,string name,bool units,bool ignorePause){
  MonoMod.RuntimeDetour.Hook.Dispatch("speech",this,channel,folder,name,units,ignorePause);
 }
 private Task<UnityEngine.AudioClip> LoadClip(string path){LastPath=path;return Pending.Task;}
 public void OriginalLoad(int channel,string folder,string name,bool units,bool ignorePause){LastPath=folder+"/"+name;}
}
public class SFXManager {
 public static SFXManager instance; public Uri requestBinkPlaybackURI;
 public int requestBinkPlayState; public bool binkIsPlaying,binkWaitForSpeech;
 public void playBink(string name,bool loop,bool wait){MonoMod.RuntimeDetour.Hook.Dispatch("play",this,name,loop,wait);}
}
namespace CrusaderDE {
 public class MainViewModel {public static bool viewModelLoaded=true;public static MainViewModel Instance;
  public object HUDmain=new object();public MainHUD HUDRoot; }
 public class MainHUD {
  public MediaElement RefRadarME=new MediaElement();
  private void RadarME_Ended(object sender,RoutedEventArgs args) { }
  public void EndEvent(){MonoMod.RuntimeDetour.Hook.Dispatch("end",this,RefRadarME,new RoutedEventArgs());}
  public void RadarME_Ended(){RefRadarME.Stop();RefRadarME.Source=null;RefRadarME.Close();
   SFXManager.instance.binkIsPlaying=false;SFXManager.instance.requestBinkPlayState=0;RefRadarME.Opacity=0;}
 }
}
namespace SHCDESE.API { public class GameTimeManagerAPI {
 public static GameTimeManagerAPI Instance=new GameTimeManagerAPI(); public event Action<int> OnTick;
 public void Tick()=>OnTick?.Invoke(1);
} }
namespace APIShared.Presentation {
 public enum PresentationOperation{GuiChecks}
 public class PresentationPostEventArgs{public bool OriginalCompleted=true;}
 public static class PresentationEvents {
  public static Action<PresentationPostEventArgs> Callback;
  public static bool TryRegister(PresentationOperation op,string owner,string id,object pre,Action<PresentationPostEventArgs> post,out string reason){Callback=post;reason=null;return true;}
  public static void Gui(){
   SFXManager s=SFXManager.instance;MainHUD hud=MainViewModel.Instance.HUDRoot;
   if((s.requestBinkPlayState==0 && s.binkIsPlaying) ||
      (s.requestBinkPlayState==3 && s.binkIsPlaying && !MyAudioManager.Instance.isSpeechPlaying(1)))hud.RadarME_Ended();
   Callback(new PresentationPostEventArgs());
  }
 }
}
namespace BugfixesAndQoL {
 internal class BugfixesAndQoLViewModel{public bool EnableMod=true,EnableClientFeatures=true,EnableNotificationLastFrame=true;}
 public static class BugfixesAndQoLPlugin{public const string PluginGuid="BugfixesAndQoL_Serp";}
}
namespace MonoMod.RuntimeDetour {
 public struct HookConfig{public bool ManualApply;public string ID;}
 public sealed class Hook {
  private static readonly Dictionary<string,Delegate> callbacks=new Dictionary<string,Delegate>();
  private readonly string key;private readonly Delegate callback;
  public Hook(MethodInfo method,Delegate callback,HookConfig config){this.callback=callback;
   key=method.DeclaringType==typeof(MyAudioManager)?"speech":method.Name=="playBink"?"play":"end";}
  public void Apply(){callbacks[key]=callback;}
  public void Dispose(){callbacks.Remove(key);}
  public T GenerateTrampoline<T>()=> (T)(object)Delegate.CreateDelegate(typeof(T),typeof(Originals).GetMethod(key));
  public static void Dispatch(string key,params object[] args){callbacks[key].DynamicInvoke(args);}
 }
}
public static class Originals {
 public static bool FailEnd; public static int EndCalls;
 public static void speech(MyAudioManager self,int channel,string folder,string name,bool units,bool ignorePause)=>self.OriginalLoad(channel,folder,name,units,ignorePause);
 public static void play(SFXManager self,string name,bool loop,bool wait){
  if(name=="rejected")return;
  bool wasPlaying=self.binkIsPlaying;self.binkWaitForSpeech=wait && MyAudioManager.Instance.isSpeechPlaying(1);
  self.requestBinkPlaybackURI=new Uri(name,UriKind.Relative);self.requestBinkPlayState=(loop?2:1)*(wasPlaying?-1:1);
 }
 public static void end(MainHUD hud,object sender,RoutedEventArgs args){
  EndCalls++;if(FailEnd)throw new InvalidOperationException("original end failure");
  SFXManager s=SFXManager.instance;if(s.requestBinkPlayState!=1)return;
  if(s.binkWaitForSpeech && MyAudioManager.Instance.isSpeechPlaying(1)){s.requestBinkPlayState=3;hud.RefRadarME.Opacity=0;return;}
  hud.RadarME_Ended();
 }
}
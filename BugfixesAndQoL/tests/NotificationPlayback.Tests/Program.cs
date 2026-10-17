using System;
using System.Threading;
using System.Threading.Tasks;
using BugfixesAndQoL;
using CrusaderDE;
using APIShared.Presentation;
using UnityEngine;

internal static class Program {
 private static int checks;
 private static BugfixesAndQoLViewModel settings=new BugfixesAndQoLViewModel();
 private static NotificationSpeechPlayback speech;
 private static NotificationLastFrameFeature feature;
 private static MyAudioManager audio=>MyAudioManager.Instance;
 private static SFXManager sfx=>SFXManager.instance;
 private static MainHUD hud=>MainViewModel.Instance.HUDRoot;
 private static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
 private static void Setup(){
  feature?.ResetMapState();speech?.Invalidate();speech?.SetNotificationClassifier(null);
  MyAudioManager.Instance=new MyAudioManager();SFXManager.instance=new SFXManager();
  MainViewModel.Instance=new MainViewModel{HUDRoot=new MainHUD()};MainViewModel.viewModelLoaded=true;
  settings.EnableMod=settings.EnableClientFeatures=settings.EnableNotificationLastFrame=true;
 }
 private static void Start(bool wait=true,bool loop=false){
  audio.PlaySpeech(1,"ai","Saladin.wav");sfx.playBink("Saladin.webm",loop,wait);StartRequested();
 }
 private static void StartRequested(){hud.RefRadarME.Source=sfx.requestBinkPlaybackURI;
  sfx.requestBinkPlayState=Math.Abs(sfx.requestBinkPlayState);sfx.binkIsPlaying=true;hud.RefRadarME.Opacity=1;}
 private static void Hold(){Start();hud.EndEvent();Check(sfx.requestBinkPlayState==3 && hud.RefRadarME.Paused && hud.RefRadarME.Opacity==1,"own speech must hold last frame");}
 public static int Main(){try{
  Setup();speech=NotificationSpeechPlayback.Get(new BepInEx.Logging.ManualLogSource());
  feature=new NotificationLastFrameFeature(new BepInEx.Logging.ManualLogSource(),settings);
  Hold();long generation=speech.Capture(audio);
  audio.PlaySpeech(2,"troops","crossbow.wav");PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==3 && speech.Capture(audio)==generation,"channel 2 must not end held message");
  audio.PlaySpeech(1,"ai","ignored.wav",false);PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==3 && speech.Capture(audio)==generation,"rejected request must not change identity");
  audio.PlaySpeech(1,"ai","Saladin.wav");PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==0 && hud.RefRadarME.Source==null && audio.isSpeechPlaying(1),"same filename in new request must free old frame without stopping speech");
  for(int i=0;i<100;i++)audio.PlaySpeech(1,"scribe","next.wav");
  Check(hud.RefRadarME.Opacity==0,"old frame must stay gone across later requests");

  Setup();Start();audio.PlaySpeech(1,"scribe","replacement.wav");hud.EndEvent();
  Check(sfx.requestBinkPlayState==0 && audio.isSpeechPlaying(1),"speech replaced before video ends must not retain video");
  Setup();Start(false);Check(!sfx.requestBinkPlaybackURI.OriginalString.EndsWith("**"),"no wait request must keep original URI");hud.EndEvent();
  Check(sfx.requestBinkPlayState==0,"unrelated occupied channel cannot retain nonwaiting video");
  Setup();Start(true,true);hud.EndEvent();Check(sfx.requestBinkPlayState==2 && !hud.RefRadarME.Paused,"loop must keep original behavior");
  Setup();settings.EnableNotificationLastFrame=false;Start();hud.EndEvent();
  Check(sfx.requestBinkPlayState==3 && hud.RefRadarME.Opacity==0,"disabled feature must preserve Vanilla hidden speech wait");

  Setup();Hold();audio.Mode=2;audio.Paused=true;PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==3 && hud.RefRadarME.Opacity==1,"intentional speech pause must retain frame");
  audio.Paused=false;audio.Mode=0;PresentationEvents.Gui();Check(sfx.requestBinkPlayState==0,"normal speech end closes frame");
  Setup();Hold();settings.EnableNotificationLastFrame=false;PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==0 && audio.isSpeechPlaying(1),"disable closes owned held frame without stopping speech");

  Setup();Hold();MainHUD oldHud=hud;
  MainViewModel.Instance.HUDRoot=new MainHUD();audio.PlaySpeech(1,"ai","next.wav");sfx.playBink("Saladin.webm",false,true);StartRequested();
  oldHud.EndEvent();Check(sfx.requestBinkPlayState==1,"obsolete HUD event must not mutate new video");PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Source!=null,"new video must survive previous ownership cleanup");
  Setup();Hold();oldHud=hud;MainViewModel.Instance.HUDRoot=new MainHUD();sfx.playBink("new.webm",false,false);StartRequested();
  oldHud.EndEvent();Check(sfx.requestBinkPlayState==1,"obsolete HUD must not close a nonwaiting replacement video");
  Setup();Hold();sfx.playBink("Saladin.webm",false,true);StartRequested();PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1,"same-URI replacement must not close new playback");
  Setup();Hold();sfx.playBink("rejected",false,true);PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==3,"rejected video request must preserve current ownership");
  Setup();Hold();speech.Invalidate();feature.ResetMapState();sfx.playBink("new.webm",false,false);StartRequested();PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1,"mission reset must not close later playback");
  Setup();audio.PlaySpeech(1,"ai","Saladin.wav");sfx.playBink("Saladin.webm",false,true);
  PresentationEvents.Gui();StartRequested();hud.EndEvent();Check(sfx.requestBinkPlayState==3 && hud.RefRadarME.Opacity==1,"pending media source must not lose ownership");

  Setup();speech.SetNotificationClassifier(name=>name=="notification.wav");
  var late=new TaskCompletionSource<AudioClip>();audio.Pending=late;audio.PlaySpeech(1,"ai","notification.wav");
  long oldGeneration=speech.Capture(audio);audio.PlaySpeech(1,"scribe","other.wav");AudioClip oldClip=new AudioClip();late.SetResult(oldClip);
  Check(SpinWait.SpinUntil(()=>oldClip.Unloaded,2000) && audio.Source.Plays==0 && audio.Mode==1,"late notification load must unload after unrelated accepted request");
  Check(!speech.IsCurrent(audio,oldGeneration),"all accepted loads invalidate previous token");

  Setup();speech.SetNotificationClassifier(_=>true);audio.Pending=new TaskCompletionSource<AudioClip>();
  audio.PlaySpeech(1,"_SE_","custom/full.wav");Check(audio.LastPath=="custom/full.wav","SE custom path must preserve original loader contract");
  audio.Paused=true;AudioClip ownClip=new AudioClip();audio.Pending.SetResult(ownClip);
  Check(SpinWait.SpinUntil(()=>audio.Mode==2,2000) && audio.Source.Paused,"accepted async load respects pause");
  speech.StopSpeechChannel1(audio);Check(audio.Mode==0 && ownClip.Unloaded && audio.Source.Stops==1,"skip stops paused owned channel and unloads its clip");
  Setup();speech.SetNotificationClassifier(_=>true);audio.Pending=new TaskCompletionSource<AudioClip>();audio.PlaySpeech(1,"ai","pending.wav");
  speech.StopSpeechChannel1(audio);AudioClip skipped=new AudioClip();audio.Pending.SetResult(skipped);
  Check(SpinWait.SpinUntil(()=>skipped.Unloaded,2000) && audio.Source.Plays==0,"skipped loading audio must not replay");
  Setup();speech.SetNotificationClassifier(_=>true);audio.Pending=new TaskCompletionSource<AudioClip>();audio.PlaySpeech(1,"ai","pending.wav");
  speech.Invalidate();AudioClip ended=new AudioClip();audio.Pending.SetResult(ended);
  Check(SpinWait.SpinUntil(()=>ended.Unloaded,2000) && audio.Source.Plays==0,"mission end invalidates pending notification load");
  Setup();speech.SetNotificationClassifier(_=>true);audio.Pending=new TaskCompletionSource<AudioClip>();audio.PlaySpeech(1,"ai","missing.wav");audio.Pending.SetResult(null);
  Check(SpinWait.SpinUntil(()=>audio.Mode==0,2000),"null clip must release loading speech state");
  Setup();Start();Originals.EndCalls=0;Originals.FailEnd=true;
  bool originalFailed=false;try{hud.EndEvent();}catch(System.Reflection.TargetInvocationException){originalFailed=true;}finally{Originals.FailEnd=false;}
  Check(originalFailed && Originals.EndCalls==1,"original failure must propagate without retrying Vanilla");
  Setup();settings.EnableNotificationLastFrame=false;Start(false);oldHud=new MainHUD();Originals.EndCalls=0;oldHud.EndEvent();
  Check(Originals.EndCalls==1,"disabled callback must retain complete Vanilla event dispatch even for an obsolete HUD");
  Setup();speech.SetNotificationClassifier(_=>true);var failed=new TaskCompletionSource<AudioClip>();audio.Pending=failed;audio.PlaySpeech(1,"ai","failed.wav");
  failed.SetException(new InvalidOperationException("deliberate loader failure"));Check(SpinWait.SpinUntil(()=>audio.Mode==0,2000),"faulted current load must release channel");
  Setup();speech.SetNotificationClassifier(name=>name=="failed.wav");failed=new TaskCompletionSource<AudioClip>();audio.Pending=failed;audio.PlaySpeech(1,"ai","failed.wav");
  audio.PlaySpeech(1,"scribe","new.wav");failed.SetException(new InvalidOperationException("deliberate stale failure"));
  Check(audio.Mode==1 && audio.Source.Stops==0,"faulted stale load must not reset or stop newer speech");
  Check(Shared.DebugLogHelper.Errors.Count==3,"only deliberate failed-load tests should log an error");
  Console.WriteLine("PASS: "+checks+" production notification playback checks (isolated Unity/Noesis adapters).");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
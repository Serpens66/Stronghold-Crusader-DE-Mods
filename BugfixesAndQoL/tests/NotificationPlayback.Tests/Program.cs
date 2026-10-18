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
  MyAudioManager.Instance=new MyAudioManager();SFXManager.instance=new SFXManager();OnScreenText.Instance=new OnScreenText();
  MainViewModel.Instance=new MainViewModel{HUDRoot=new MainHUD()};MainViewModel.viewModelLoaded=true;
  settings.EnableMod=settings.EnableClientFeatures=settings.EnableNotificationLastFrame=true;
 }
 private static void Message(){OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_MESSAGE_BAR,221,992);}
 private static void Start(bool wait=true,bool loop=false){
  Message();audio.PlaySpeech(1,"ai","Saladin.wav");sfx.playBink("Saladin.webm",loop,wait);StartRequested();
 }
 private static void StartRequested(){hud.RefRadarME.Source=sfx.requestBinkPlaybackURI;
  sfx.requestBinkPlayState=Math.Abs(sfx.requestBinkPlayState);sfx.binkIsPlaying=true;hud.RefRadarME.Opacity=1;}
 private static void Hold(){Start();OnScreenText.Instance.Flush();hud.EndEvent();Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Paused && hud.RefRadarME.Opacity==1,"visible text must hold last frame in active state 1");}
 public static int Main(){try{
  Setup();speech=NotificationSpeechPlayback.Get(new BepInEx.Logging.ManualLogSource());
  feature=new NotificationLastFrameFeature(new BepInEx.Logging.ManualLogSource(),settings);
  Hold();audio.Mode=0;PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Opacity==1,"audio end must not end visible text's frame");
  for(int i=0;i<100;i++)audio.PlaySpeech(1,"scribe","next.wav");PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Opacity==1,"audio replacements must not control retained picture");
  audio.Paused=true;PresentationEvents.Gui();
  Check(hud.RefRadarME.Opacity==1,"audio pause must not change picture");
  MainViewModel.Instance.OST_Message_Bar_Vis=false;
  Check(sfx.requestBinkPlayState==0 && hud.RefRadarME.Source==null && hud.RefRadarME.Opacity==0 && audio.isSpeechPlaying(1),"text hide must immediately close picture without stopping speech");

  Setup();Start();OnScreenText.Instance.Flush();MainViewModel.Instance.OST_Message_Bar_Vis=false;
  Check(hud.RefRadarME.Source==null && sfx.requestBinkPlayState==0,"text expiry must also close a video still playing");
  Setup();Message();sfx.playBink("silent.webm",false,false);StartRequested();OnScreenText.Instance.Flush();hud.EndEvent();
  Check(hud.RefRadarME.Paused && hud.RefRadarME.Opacity==1 && sfx.requestBinkPlayState==1,"silent notification must retain frame without speech");
  MainViewModel.Instance.OST_Message_Bar_Vis=false;Check(sfx.requestBinkPlayState==0,"silent notification ends with text");

  Setup();Start(true,true);OnScreenText.Instance.Flush();hud.EndEvent();
  Check(sfx.requestBinkPlayState==2 && !hud.RefRadarME.Paused,"loop keeps Vanilla behavior");
  Setup();sfx.playBink("standalone.webm",false,false);StartRequested();hud.EndEvent();
  Check(sfx.requestBinkPlayState==0,"video without text must keep Vanilla end behavior");
  Setup();settings.EnableNotificationLastFrame=false;Start();OnScreenText.Instance.Flush();hud.EndEvent();
  Check(sfx.requestBinkPlayState==3 && hud.RefRadarME.Opacity==0,"disabled feature preserves Vanilla hidden speech wait");
  Setup();Hold();settings.EnableNotificationLastFrame=false;PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==0 && audio.isSpeechPlaying(1),"disable closes picture without stopping speech");

  Setup();Hold();hud.RefRadarME.Opacity=0;PresentationEvents.Gui();hud.EndEvent();
  Check(hud.RefRadarME.Opacity==0,"left-click hide must never be undone");
  Setup();Hold();hud.RadarME_Ended();audio.Mode=0;MainViewModel.Instance.OST_Message_Bar_Vis=false;PresentationEvents.Gui();
  Check(hud.RefRadarME.Opacity==0 && hud.RefRadarME.Source==null,"right-click close stays closed");
  Setup();Hold();hud.RefRadarME.Paused=false;PresentationEvents.Gui();
  Check(hud.RefRadarME.Paused && hud.RefRadarME.Opacity==1,"briefing resume must not restart a held picture");
  PresentationEvents.Gui();Check(sfx.requestBinkPlayState==1,"Vanilla keeping briefing text visible also keeps picture");
  MainViewModel.Instance.OST_Message_Bar_Vis=false;Check(sfx.requestBinkPlayState==0,"Vanilla text expiry after briefing releases picture");

  Setup();Hold();MainHUD oldHud=hud;MainViewModel oldMain=MainViewModel.Instance;
  MainViewModel.Instance=new MainViewModel{HUDRoot=new MainHUD()};Start();OnScreenText.Instance.Flush();
  oldHud.EndEvent();oldMain.OST_Message_Bar_Vis=false;PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Source!=null,"obsolete HUD and ViewModel events cannot close replacement");
  Check(oldHud.RefRadarME.Source==null,"obsolete HUD media is closed without changing replacement flags");
  Setup();Hold();oldHud=hud;MainViewModel.Instance.HUDRoot=new MainHUD();sfx.playBink("new.webm",false,false);StartRequested();
  oldHud.EndEvent();Check(sfx.requestBinkPlayState==1,"old HUD cannot close nonwaiting replacement");
  Setup();Hold();Message();sfx.playBink("Saladin.webm",false,true);StartRequested();OnScreenText.Instance.Flush();PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1 && !hud.RefRadarME.Paused,"identical text and filename must start a fresh playback");
  hud.EndEvent();MainViewModel.Instance.OST_Message_Bar_Vis=false;
  Check(sfx.requestBinkPlayState==0,"identical replacement owns its own text end");
  Setup();Hold();sfx.playBink("rejected",false,true);PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Opacity==1,"rejected request preserves current picture");
  Setup();Message();sfx.playBink("rejected",false,false);sfx.playBink("accepted.webm",false,false);StartRequested();OnScreenText.Instance.Flush();hud.EndEvent();
  Check(hud.RefRadarME.Paused && sfx.requestBinkPlayState==1,"rejected request must not consume text association");
  Setup();Hold();Message();OnScreenText.Instance.Flush();PresentationEvents.Gui();
  Check(hud.RefRadarME.Source==null,"replacement text without video removes old portrait");
  sfx.playBink("unrelated.webm",false,false);StartRequested();hud.EndEvent();
  Check(sfx.requestBinkPlayState==0,"unconsumed text must not attach to a later batch's video");

  Setup();Hold();feature.ResetMapState();PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==0 && hud.RefRadarME.Source==null,"mission end releases held picture on presentation caller");
  Setup();Hold();feature.ResetMapState();sfx.playBink("new.webm",false,false);StartRequested();PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==1,"mission reset must not close subsequent playback");
  Setup();Start();PresentationEvents.Incomplete();
  Check(sfx.requestBinkPlayState==1 && hud.RefRadarME.Source!=null,"initial false must not close before presentation completes");
  PresentationEvents.Gui();hud.EndEvent();Check(hud.RefRadarME.Paused && hud.RefRadarME.Opacity==1,"first text update completes ownership");
  Setup();Start();OnScreenText.Instance.Pending=false;PresentationEvents.Gui();
  Check(sfx.requestBinkPlayState==0,"completed presentation without visible text must not leave a held video");
  Setup();Message();sfx.playBink("waiting.webm",false,false);PresentationEvents.Gui();
  StartRequested();hud.EndEvent();Check(hud.RefRadarME.Paused,"pending source retains its association behind replacement delay");
  Setup();Start();OnScreenText.Instance.Flush();hud.RefRadarME.Source=new Uri("different.webm",UriKind.Relative);hud.EndEvent();
  Check(sfx.requestBinkPlayState==1 && !hud.RefRadarME.Paused,"obsolete source event cannot pause a different source");

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
  Setup();settings.EnableNotificationLastFrame=false;Start();Originals.EndCalls=0;Originals.FailEnd=true;
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
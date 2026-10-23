using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BugfixesAndQoL;
using CrusaderDE;
using APIShared.Presentation;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static long now;
    private static readonly BugfixesAndQoLViewModel settings = new BugfixesAndQoLViewModel();
    private static NotificationSpeechPlayback speech;
    private static NotificationLastFrameFeature feature;
    private static MyAudioManager audio => MyAudioManager.Instance;
    private static SFXManager sfx => SFXManager.instance;
    private static MainHUD hud => MainViewModel.Instance.HUDRoot;
    private static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    private static void At(long milliseconds) => now = milliseconds * Stopwatch.Frequency / 1000;
    private static void Setup()
    {
        feature?.ResetMapState(); speech?.Invalidate(); speech?.SetNotificationClassifier(null);
        MyAudioManager.Instance = new MyAudioManager(); SFXManager.instance = new SFXManager();
        OnScreenText.Instance = new OnScreenText();
        MainViewModel.Instance = new MainViewModel { HUDRoot = new MainHUD() };
        MainViewModel.viewModelLoaded = true;
        settings.EnableMod = settings.EnableClientFeatures = settings.EnableNotificationLastFrame = true;
        // A nonzero epoch catches implementations that compare process uptime instead of elapsed time.
        At(100000);
    }
    private static void Message() => OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_MESSAGE_BAR, 221, 992);
    private static void Start(bool wait = true, bool loop = false)
    {
        Message(); audio.PlaySpeech(1, "ai", "Saladin.wav");
        sfx.playBink("Saladin.webm", loop, wait); StartRequested();
    }
    private static void StartRequested()
    {
        hud.RefRadarME.Source = sfx.requestBinkPlaybackURI;
        sfx.requestBinkPlayState = Math.Abs(sfx.requestBinkPlayState);
        sfx.binkIsPlaying = true; hud.RefRadarME.Opacity = 1; hud.RefRadarME.Play();
    }
    private static void CheckHeld(string message) => Check(sfx.requestBinkPlayState == 1 &&
        sfx.binkIsPlaying && hud.RefRadarME.Paused && hud.RefRadarME.Opacity == 1, message);
    private static void CheckClosed(string message) => Check(sfx.requestBinkPlayState == 0 &&
        !sfx.binkIsPlaying && hud.RefRadarME.Source == null && hud.RefRadarME.Opacity == 0, message);
    private static void Hold()
    {
        Start(); hud.EndEvent();
        CheckHeld("active speech holds the final frame in state 1 without requiring visible text");
    }
    private static void TestHdCompletion()
    {
        Hold(); audio.Mode = 0; PresentationEvents.Gui();
        CheckClosed("speech completion closes a held picture");
        Check(audio.Source.Stops == 0, "picture completion must not stop audio");

        Setup(); Start(); audio.Mode = 0; PresentationEvents.Gui();
        Check(sfx.requestBinkPlayState == 1 && !hud.RefRadarME.Paused,
            "speech ending first must not truncate a running video");
        hud.EndEvent(); CheckClosed("video ending after speech closes without holding");

        Setup(); Start(); OnScreenText.Instance.Flush(); MainViewModel.Instance.OST_Message_Bar_Vis = false;
        hud.EndEvent(); CheckHeld("text expiry does not shorten the speech-owned frame");
        Message(); OnScreenText.Instance.Flush(); PresentationEvents.Gui();
        CheckHeld("new text without video does not replace or close the current picture");
        audio.Mode = 0; PresentationEvents.Gui();
        CheckClosed("visible text cannot extend the picture after speech ends");

        Setup(); sfx.playBink("silent.webm", false, false); StartRequested(); hud.EndEvent();
        CheckClosed("silent video closes at its own end without a retained frame");
        Setup(); sfx.playBink("missing-speech.webm", false, true); StartRequested();
        audio.PlaySpeech(1, "scribe", "later.wav"); hud.EndEvent();
        CheckClosed("speech absent at video request cannot be replaced by later unrelated speech");
        Setup(); Start(false); hud.EndEvent();
        CheckClosed("explicit no-wait closes despite an occupied speech channel");
        Check(audio.isSpeechPlaying(1), "no-wait completion leaves speech running");

        Setup(); Start(); audio.Mode = 0; PresentationEvents.Gui();
        audio.PlaySpeech(1, "scribe", "replacement.wav"); hud.EndEvent();
        CheckClosed("an observed idle channel permanently clears the binding while video still plays");

        Setup(); Hold(); At(119999); PresentationEvents.Gui();
        CheckHeld("19999 ms is inside the HD wait bound");
        At(120000); PresentationEvents.Gui(); CheckHeld("exactly 20000 ms still waits, matching HD's strict greater-than");
        At(120001); PresentationEvents.Gui(); CheckClosed("20001 ms releases a permanently loading channel");
        Check(audio.Mode == 1 && audio.Source.Stops == 0, "deadline closes only the picture, leaving loading speech intact");

        Setup(); Hold(); audio.Mode = 2; At(120001); PresentationEvents.Gui();
        CheckClosed("a long playing voice also releases at the fixed HD deadline");
        Check(audio.Mode == 2 && audio.Source.Stops == 0, "long voice continues after the picture closes");
        Setup(); Start(); At(120001); PresentationEvents.Gui();
        Check(sfx.requestBinkPlayState == 1 && !hud.RefRadarME.Paused,
            "deadline never truncates a video longer than 20 seconds");
        hud.EndEvent(); CheckClosed("long video closes normally at MediaEnded after the wait expired");
        Setup(); Start(); At(120001); hud.EndEvent();
        CheckClosed("MediaEnded enforces deadline even without an intervening GUI callback");

        Setup(); Hold();
        for (int i = 1; i <= 100; i++)
        {
            At(100000 + i * 199); audio.PlaySpeech(1, "scribe", "same.wav"); PresentationEvents.Gui();
        }
        CheckHeld("HD observes the occupied channel across replacements within the original deadline");
        At(120001); PresentationEvents.Gui(); CheckClosed("100 speech replacements cannot renew the original deadline");
        Check(audio.isSpeechPlaying(1), "newer speech must survive old picture expiry");

        Setup(); Hold(); At(119999); sfx.playBink("rejected", false, true);
        PresentationEvents.Gui(); CheckHeld("rejected request preserves current picture");
        At(120001); PresentationEvents.Gui(); CheckClosed("rejected video request cannot renew the deadline");
        Setup(); Hold(); At(119000); sfx.playBink("Saladin.webm", false, true); StartRequested(); hud.EndEvent();
        At(120001); PresentationEvents.Gui(); CheckHeld("accepted identical filename gets its own request deadline");
        At(139001); PresentationEvents.Gui(); CheckClosed("identical replacement expires relative to its own start");

        Setup(); Start(true, true); At(130000); PresentationEvents.Gui(); hud.EndEvent();
        Check(sfx.requestBinkPlayState == 2 && !hud.RefRadarME.Paused,
            "looping video retains complete Vanilla behavior beyond the deadline");
        Setup(); settings.EnableNotificationLastFrame = false; Start(); hud.EndEvent();
        Check(sfx.requestBinkPlayState == 3 && hud.RefRadarME.Opacity == 0,
            "disabled feature preserves Vanilla hidden speech wait");
        Setup(); settings.EnableMod = false; Hold();
        CheckHeld("host activation does not gate this personal feature");
        settings.EnableClientFeatures = false; PresentationEvents.Gui();
        CheckClosed("client disable releases its owned picture");
        Check(audio.isSpeechPlaying(1), "client disable leaves speech running");
        Setup(); Hold(); settings.EnableNotificationLastFrame = false; PresentationEvents.Gui();
        CheckClosed("option disable releases its owned picture");

        Setup(); Hold(); audio.Paused = true; hud.RefRadarME.Play(); PresentationEvents.Gui();
        CheckHeld("briefing resume cannot restart a held picture");
        At(120001); PresentationEvents.Gui(); CheckClosed("speech pause and briefing resume do not extend the deadline");
        Check(audio.Paused && audio.Source.Stops == 0, "deadline leaves paused speech untouched");
        Setup(); Hold(); hud.RefRadarME.Opacity = 0; hud.RefRadarME.Play(); PresentationEvents.Gui(); hud.EndEvent();
        Check(hud.RefRadarME.Paused && hud.RefRadarME.Opacity == 0, "left-click hide is never undone by resume or MediaEnded");
        At(120001); PresentationEvents.Gui(); CheckClosed("hidden retained video also releases at deadline");
        Setup(); Hold(); hud.RadarME_Ended(); PresentationEvents.Gui();
        CheckClosed("explicit right-click video completion stays closed");

        Setup(); Hold(); var oldHud = hud; var oldMain = MainViewModel.Instance;
        MainViewModel.Instance = new MainViewModel { HUDRoot = new MainHUD() }; Start();
        oldHud.EndEvent(); oldMain.OST_Message_Bar_Vis = false; PresentationEvents.Gui();
        Check(sfx.requestBinkPlayState == 1 && hud.RefRadarME.Source != null, "obsolete HUD events cannot close a replacement");
        Check(oldHud.RefRadarME.Source == null, "obsolete media is closed without changing replacement flags");
        Setup(); Hold(); oldHud = hud; MainViewModel.Instance.HUDRoot = new MainHUD();
        sfx.playBink("new.webm", false, false); StartRequested(); oldHud.EndEvent();
        Check(sfx.requestBinkPlayState == 1, "old HUD cannot close a nonwaiting replacement");
        Setup(); Hold(); MyAudioManager.Instance = new MyAudioManager();
        audio.PlaySpeech(1, "scribe", "new-manager.wav"); PresentationEvents.Gui();
        CheckClosed("replacement audio manager cannot extend old picture");
        Check(audio.isSpeechPlaying(1), "replacement manager's speech is untouched");
        Setup(); Hold(); hud.RefRadarME = new NoesisApp.MediaElement(); PresentationEvents.Gui();
        Check(sfx.requestBinkPlayState == 1, "media replacement cleanup cannot alter its global flags");

        Setup(); Hold(); feature.ResetMapState();
        Check(sfx.requestBinkPlayState == 1, "mission invalidation does not access Noesis on the notifying thread");
        PresentationEvents.Gui(); CheckClosed("mission end closes owned video on presentation caller");
        Setup(); Hold(); feature.ResetMapState(); sfx.playBink("new.webm", false, false); StartRequested(); PresentationEvents.Gui();
        Check(sfx.requestBinkPlayState == 1, "deferred mission cleanup cannot close subsequent playback");
        Setup(); Start(); MainViewModel.viewModelLoaded = false; PresentationEvents.Gui();
        Check(hud.RefRadarME.Source == null, "unloaded ViewModel closes only its old owned media");
        Setup(); Start(); PresentationEvents.Incomplete();
        Check(sfx.requestBinkPlayState == 1, "incomplete presentation cannot prematurely close a requested video");
        Setup(); audio.PlaySpeech(1, "ai", "pending.wav"); sfx.playBink("waiting.webm", false, true);
        PresentationEvents.Gui(); At(120001); PresentationEvents.Gui();
        Check(sfx.requestBinkPlayState == 1, "delayed source installation is not treated as video completion");
        StartRequested(); hud.EndEvent(); CheckClosed("source delay does not renew the speech deadline");
        Setup(); Start(); hud.RefRadarME.Source = new Uri("different.webm", UriKind.Relative); hud.EndEvent();
        Check(sfx.requestBinkPlayState == 1 && !hud.RefRadarME.Paused, "obsolete-source event cannot pause another source");
        feature.ResetMapState(); PresentationEvents.Gui();
        Check(hud.RefRadarME.Source.OriginalString == "different.webm" && sfx.requestBinkPlayState == 1,
            "cleanup cannot close a foreign media source or clear its flags");
        Check(Shared.DebugLogHelper.Errors.Count == 0, "normal completion paths must not log errors");
    }
    private static void TestSpeechLoadFailureCompletion()
    {
        foreach (bool faulted in new[] { false, true })
        {
            Setup(); speech.SetNotificationClassifier(_ => true);
            audio.Pending = new TaskCompletionSource<AudioClip>(); Start(); hud.EndEvent();
            CheckHeld("accepted pending audio keeps the frame within the bounded wait");
            if (faulted) audio.Pending.SetException(new InvalidOperationException("deliberate held load failure"));
            else audio.Pending.SetResult(null);
            Check(SpinWait.SpinUntil(() => audio.Mode == 0, 2000), "failed load releases its speech state");
            PresentationEvents.Gui(); CheckClosed("failed or null clip releases a held picture before deadline");
        }
        Check(Shared.DebugLogHelper.Errors.Count == 2, "only deliberate failed loads log errors");
        Shared.DebugLogHelper.Errors.Clear();

        Setup(); Start(); hud.RefRadarME.FailPause = true; Originals.EndCalls = 0; hud.EndEvent();
        Check(Originals.EndCalls == 1 && sfx.requestBinkPlayState == 3,
            "decoder pause failure falls back to the Vanilla end callback exactly once");
        At(120001); PresentationEvents.Gui();
        CheckClosed("the HD deadline also releases Vanilla state 3 after a decoder pause failure");
        Check(Shared.DebugLogHelper.Errors.Count == 1, "only the deliberately failed decoder pause is logged");
        Shared.DebugLogHelper.Errors.Clear();
    }
    private static void TestSpeechOwnership()
    {
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
  Setup();settings.EnableNotificationLastFrame=false;Start(false);var oldHud=new MainHUD();Originals.EndCalls=0;oldHud.EndEvent();
  Check(Originals.EndCalls==1,"disabled callback must retain complete Vanilla event dispatch even for an obsolete HUD");
  Setup();speech.SetNotificationClassifier(_=>true);var failed=new TaskCompletionSource<AudioClip>();audio.Pending=failed;audio.PlaySpeech(1,"ai","failed.wav");
  failed.SetException(new InvalidOperationException("deliberate loader failure"));Check(SpinWait.SpinUntil(()=>audio.Mode==0,2000),"faulted current load must release channel");
  Setup();speech.SetNotificationClassifier(name=>name=="failed.wav");failed=new TaskCompletionSource<AudioClip>();audio.Pending=failed;audio.PlaySpeech(1,"ai","failed.wav");
  audio.PlaySpeech(1,"scribe","new.wav");failed.SetException(new InvalidOperationException("deliberate stale failure"));
  Check(audio.Mode==1 && audio.Source.Stops==0,"faulted stale load must not reset or stop newer speech");
  Check(Shared.DebugLogHelper.Errors.Count==3,"only deliberate failed-load tests should log an error");
    }
    public static int Main()
    {
        try
        {
            Setup(); speech = NotificationSpeechPlayback.Get(new BepInEx.Logging.ManualLogSource());
            feature = new NotificationLastFrameFeature(new BepInEx.Logging.ManualLogSource(), settings, () => now);
            TestHdCompletion(); TestSpeechLoadFailureCompletion(); TestSpeechOwnership();
            Console.WriteLine("PASS: " + checks + " production notification playback checks (isolated Unity/Noesis adapters and monotone test clock).");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

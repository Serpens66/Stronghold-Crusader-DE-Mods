#if SERP_DEVDIAG
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using APIShared.Presentation;
using BepInEx.Logging;
using NoesisApp;
using UnityEngine;

internal static partial class Program
{
    private static bool Logged(params string[] parts) => ManualLogSource.Messages.Any(line => parts.All(line.Contains));
    private static void DiagnosticSetup()
    {
        Setup();
        // Isolate evidence windows; this is not a runtime reset/escape hatch.
        ((IList)typeof(BugfixesAndQoL.NotificationLastFrameFeature).GetField("diagVideos", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Clear();
        ManualLogSource.Messages.Clear();
        Application.Render();
    }
    private static NoesisMediaPlayer Decoder()
    {
        var player = new NoesisMediaPlayer { Media = hud.RefRadarME };
        hud.RefRadarME.Player = player;
        Application.Render();
        return player;
    }
    static partial void TestDiagnostics()
    {
        Check(Logged("stage=READY", "normalDiagnosticSwitch=ignored"), "diagnostic startup must announce readiness independently of preferences");
        DiagnosticSetup();
        Check(Logged("stage=RENDER_READY", "unityThread=confirmed"), "render callback confirms the Unity thread before UI measurements");
        Start(); var player = Decoder();
        audio.Mode = 2; audio.Source.Playing = true; Application.Render();
        audio.Source.Playing = false; Application.Render();
        Check(Logged("speechMode1=2", "actualPlaying=False"), "raw busy-but-silent state is distinguishable from actual audio playback");
        int monitors = Originals.Monitors; audio.Monitor(); Application.Render();
        Check(Originals.Monitors == monitors + 1 && audio.Mode == 0 && Logged("audio-state/monitor-after", "speechMode1=0"), "monitor original executes once and its cleanup is visible");

        DiagnosticSetup(); Start(); player = Decoder(); audio.Mode = 2; audio.Source.Playing = true;
        int playerCalls = Originals.PlayerEnds, mediaCalls = Originals.MediaEnds;
        player.End();
        CheckHeld("diagnostic end hooks preserve last-frame behavior");
        audio.Source.Playing = false; audio.Monitor(); PresentationEvents.Gui(); Application.Render();
        CheckClosed("diagnostic completion preserves production behavior");
        Check(Originals.PlayerEnds == playerCalls + 1 && Originals.MediaEnds == mediaCalls + 1, "each decoder original runs exactly once");
        Check(Logged("stage=SUMMARY", "closed=True", "playerEnds=1", "mediaEnds=1", "hudEnds=1", "closeAttempts=1", "closeReturns=1"), "successful summary joins the entire decoder-to-close chain");
        ManualLogSource.Messages.Clear(); At(121000); Application.Render();
        Check(!Logged("stage=deadline-expired"), "a completed observation cannot report a later timeout against a reused media element");

        DiagnosticSetup(); Start(); player = Decoder(); player.Video.frame = 9; player.Video.isPlaying = false;
        At(125001); Application.Render();
        Check(Logged("stage=SUMMARY", "closed=False", "deadlinePassed=True", "lastFrameObserved=True", "hudEnd=True", "gui=True"), "missing end and GUI callbacks are explicit evidence gaps at 25 seconds");
        Check(sfx.binkIsPlaying && audio.Mode == 1, "independent observer must not repair or stop stalled playback");

        DiagnosticSetup(); Start(); player = Decoder(); player.End(); At(120001); PresentationEvents.Gui(); Application.Render();
        Check(Logged("stage=speech-wait-cleared", "deadlineExceeded=True") && Logged("stage=SUMMARY", "closed=True"), "timeout path is measured through actual close completion");
        Check(audio.Mode == 1 && audio.Source.Stops == 0, "timeout diagnosis never mutates audio");

        DiagnosticSetup(); Start(); Decoder();
        PresentationEvents.Callback(new PresentationPostEventArgs { OriginalCompleted = false, WasSkipped = true });
        PresentationEvents.Callback(new PresentationPostEventArgs { OriginalCompleted = false, WasReplaced = true });
        At(125000); Application.Render();
        Check(Logged("stage=SUMMARY", "skipped=1", "replaced=1", "closed=False"), "skipped and replaced GUI calls count before production completion gate");

        DiagnosticSetup(); Start(); Decoder();
        hud.RefRadarME.Native = new IntPtr(123);
        var wrapper = new MediaElement { Native = new IntPtr(123), Source = hud.RefRadarME.Source };
        MonoMod.RuntimeDetour.Hook.Dispatch("end", hud, wrapper, new Noesis.RoutedEventArgs());
        Check(Logged("stage=reject-identity", "sender=False", "/n0x7B"), "native-equal managed-different wrappers expose the exact rejecting condition");
        At(125000); Application.Render(); Check(Logged("stage=SUMMARY", "rejected=1"), "rejected end callbacks remain visible in summary");

        DiagnosticSetup(); Start(); player = Decoder(); hud.IgnoreClose = true; audio.Mode = 0; player.End(); Application.Render();
        At(125000); Application.Render();
        Check(Logged("stage=SUMMARY", "closed=False", "closeAttempts=1", "closeReturns=1"), "a close return is never mistaken for successful media removal");

        DiagnosticSetup(); Start(); Decoder();
        audio.PlaySpeech(1, "ai", "Saladin.wav"); audio.PlaySpeech(1, "ai", "Saladin.wav");
        var speechIds = ManualLogSource.Messages.Where(l => l.Contains("stage=speech-accepted")).Select(l => l.Split(new[] { "requestSpeech=" }, StringSplitOptions.None)[1].Split(' ')[0]).Distinct().Count();
        Check(speechIds == 3, "same-file accepted speech has distinct diagnostic identities");
        sfx.playBink("rejected", false, true);
        Check(Logged("stage=video-rejected", "request-uri-reference-unchanged"), "rejected requests are separate from accepted ownership");

        DiagnosticSetup(); Start(); player = Decoder();
        Originals.FailMonitor = true; monitors = Originals.Monitors;
        bool threw = false; try { audio.Monitor(); } catch (TargetInvocationException) { threw = true; } finally { Originals.FailMonitor = false; }
        Check(threw && Originals.Monitors == monitors + 1, "monitor errors propagate after exactly one original invocation");
        Originals.FailPlayerEnd = true; playerCalls = Originals.PlayerEnds; threw = false;
        try { player.End(); } catch (TargetInvocationException) { threw = true; } finally { Originals.FailPlayerEnd = false; }
        Check(threw && Originals.PlayerEnds == playerCalls + 1, "decoder errors propagate without original retries");
        Check(!Logged("DIAG_ERROR"), "valid diagnostic fixtures produce no diagnostic failures");
        DiagnosticSetup(); Start(); Decoder();
        int snapshots = ManualLogSource.Messages.Count(l => l.Contains("stage=status") || l.Contains("stage=state-change"));
        for (int i = 0; i < 20; i++) Application.Render();
        Check(ManualLogSource.Messages.Count(l => l.Contains("stage=status") || l.Contains("stage=state-change")) == snapshots,
            "unchanged frames do not flood full status snapshots");
        var modeField = typeof(BugfixesAndQoL.NotificationLastFrameFeature).GetField("diagMode", BindingFlags.Static | BindingFlags.NonPublic);
        object saved = modeField.GetValue(null); modeField.SetValue(null, null); monitors = Originals.Monitors;
        try { audio.Monitor(); } finally { modeField.SetValue(null, saved); }
        Check(Originals.Monitors == monitors + 1 && Logged("DIAG_ERROR"), "diagnostic capture errors cannot suppress the audio original");
        // The 25s watchdog must survive failed per-frame reads and still report a diagnostic gap.
        modeField.SetValue(null, null);
        try { At(125000); Application.Render(); } finally { modeField.SetValue(null, saved); }
        Check(Logged("stage=SUMMARY", "sampleErrors=1", "closed=False", "deadlinePassed=True"), "a failed sampler still emits an accurate deadline summary and its error count");
        Console.WriteLine("PASS: diagnostic timeline, missing callbacks, stale speech, skipped GUI, timeout and failed-close scenarios.");
    }
}
#endif

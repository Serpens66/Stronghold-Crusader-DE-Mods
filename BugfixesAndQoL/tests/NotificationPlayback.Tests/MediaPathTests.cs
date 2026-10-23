using System;
using APIShared.Presentation;
using SHCDESE.API;
using CrusaderDE;

internal static partial class Program
{
    private static void TestNotificationMediaPaths()
    {
        const string wolf = @"Assets\GUI\Video\Characters\wf_taunt2.webm";
        Setup(); audio.PlaySpeech(1, "ai", "wf_taunt_02.wav");
        sfx.playBink(wolf, false, true); StartRequested();
        Check(hud.RefRadarME.Source.OriginalString == "Assets/GUI/Video/Characters/wf_taunt2.webm**" &&
            sfx.requestBinkPlaybackURI.OriginalString.Contains("\\"), "fixture reproduces the logged Noesis separator round-trip");
        At(102767); hud.EndEvent(); CheckHeld("logged Wolf end must be accepted after Noesis normalizes its URI");
        At(104701); audio.Mode = 0; PresentationEvents.Gui(); CheckClosed("logged Wolf picture closes at speech end, before the timeout");
        Check(audio.Source.Stops == 0, "normalized completion never stops speech");

        // Registered mod video overrides may replace the URI kind and extension. Resolve
        // before adding ** so the SE setter cannot drop the nonlooping end-event suffix.
        foreach (string request in new[] { @"Assets\GUI\Video\Characters\mod_message.webm", @"Assets\GUI\Video\_SLA_\example.lord\Video\message.webm" })
        foreach (bool voiced in new[] { false, true })
        foreach (bool wait in new[] { false, true })
        {
            Setup(); GameAssetManagerAPI.Instance.Videos[request.Replace('\\','/').Replace(".webm", ".mp4")] = @"C:\Mods\Example Lord\Video\message.mp4";
            if (voiced) audio.PlaySpeech(1, "_SE_", "_SLA_/example.lord/Audio/message.ogg");
            sfx.playBink(request, false, wait); StartRequested();
            Check(hud.RefRadarME.Source.IsAbsoluteUri && hud.RefRadarME.Source.OriginalString.EndsWith("message.mp4**", StringComparison.Ordinal),
                "registered mod override preserves its resolved physical path, extension and end-event suffix");
            Check(sfx.requestBinkPlaybackURI.IsAbsoluteUri, "mod request identity tracks the resolved source without broadening the source guard");
            hud.EndEvent();
            if (voiced && wait) { CheckHeld("mod speech holds its resolved video"); audio.Mode = 0; PresentationEvents.Gui(); }
            CheckClosed("mod notification completes with or without audio");
            Check(audio.Source.Stops == 0, "mod picture completion leaves the speech channel untouched");
        }

        foreach (string request in new[] { @"C:\Custom Lords\Example Lord\message.webm", "file:///C:/Custom%20Lords/Example%20Lord/message.webm" })
        foreach (bool voiced in new[] { false, true })
        {
            Setup(); if (voiced) audio.PlaySpeech(1, "_SE_", "custom/message.ogg");
            sfx.playBink(request, false, voiced); StartRequested(); hud.EndEvent();
            if (voiced) { CheckHeld("absolute custom-lord video retains speech binding"); audio.Mode = 0; PresentationEvents.Gui(); }
            CheckClosed("absolute custom-lord media closes with and without audio");
        }

        foreach (bool wait in new[] { false, true })
        {
            Setup(); Message(); sfx.playBink(wolf, false, wait); StartRequested();
            PresentationEvents.Gui(); Check(sfx.binkIsPlaying && !hud.RefRadarME.Paused, "silent notification must finish playing its video");
            hud.EndEvent(); CheckClosed("no speech closes a normalized video directly at MediaEnded");
            Check(MainViewModel.Instance.OST_Message_Bar_Vis, "silent picture completion does not shorten visible message text");
        }
        Setup(); audio.PlaySpeech(1, "scribe", "unrelated.wav"); sfx.playBink(wolf, false, false); StartRequested(); hud.EndEvent();
        CheckClosed("explicit no-wait notification closes even with unrelated speech");
        Check(audio.isSpeechPlaying(1), "unrelated speech survives silent/no-wait notification completion");
        Setup(); Message(); PresentationEvents.Gui();
        Check(sfx.requestBinkPlaybackURI == null && !sfx.binkIsPlaying && hud.RefRadarME.Source == null,
            "text-only mod notification must not create or hold a video");

        foreach (string foreign in new[] {
            "Assets/GUI/Video/Other/wf_taunt2.webm**", "Assets/GUI/Video/Characters/wf_taunt2.webm*",
            "Assets/GUI/Video/Characters/WF_TAUNT2.webm**", "Assets/GUI/Video/Characters/wf%5Ftaunt2.webm**",
            "file:///C:/Assets/GUI/Video/Characters/wf_taunt2.webm**" })
        {
            Setup(); audio.PlaySpeech(1, "ai", "voice.wav"); sfx.playBink(wolf, false, true); StartRequested();
            var replacement = new Uri(foreign, UriKind.RelativeOrAbsolute); hud.RefRadarME.Source = replacement; hud.EndEvent();
            Check(!hud.RefRadarME.Paused && sfx.binkIsPlaying, "different folder, suffix, case, escaping or URI kind remains rejected");
            feature.ResetMapState(); PresentationEvents.Gui();
            Check(hud.RefRadarME.Source.OriginalString == replacement.OriginalString && sfx.binkIsPlaying, "foreign source cleanup must not close a replacement or clear its flags");
        }

        Setup(); audio.PlaySpeech(1, "ai", "voice.wav"); sfx.playBink(wolf, false, true); StartRequested(); hud.EndEvent();
        hud.RefRadarME.Play(); PresentationEvents.Gui(); CheckHeld("normalized source is paused again after briefing resume");
        var oldMedia = hud.RefRadarME; MainViewModel.Instance = new MainViewModel { HUDRoot = new MainHUD() };
        feature.ResetMapState(); PresentationEvents.Gui();
        Check(oldMedia.Source == null && oldMedia.Opacity == 0 && sfx.binkIsPlaying, "normalized old HUD media closes without clearing replacement-owned global state");

        Setup(); audio.PlaySpeech(1, "ai", "voice.wav"); sfx.playBink(wolf, false, true); StartRequested(); hud.EndEvent();
        var firstRequest = sfx.requestBinkPlaybackURI;
        sfx.playBink(wolf, false, true); StartRequested(); hud.EndEvent();
        Check(!ReferenceEquals(firstRequest, sfx.requestBinkPlaybackURI), "same filename still creates a distinct request identity");
        At(120001); PresentationEvents.Gui(); CheckClosed("normalized source releases a permanently loading voice at the fixed deadline");
    }
}

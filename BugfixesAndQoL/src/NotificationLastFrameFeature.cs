// Feature: Hold a minimap video's last frame only for its own accepted speech request.
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using NoesisApp;
using SHCDESE.API;
using System;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class NotificationLastFrameFeature
    {
        private delegate void VideoEndedDelegate(MainHUD self, object sender, RoutedEventArgs args);
        private delegate void PlayBinkDelegate(SFXManager self, string binkName, bool loop, bool waitForSpeech);
        private static NotificationLastFrameFeature instance;
        private static Hook videoEndedHook, playBinkHook;
        private static VideoEndedDelegate originalVideoEnded;
        private static PlayBinkDelegate originalPlayBink;
        private static bool postStartupLogged;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly NotificationSpeechPlayback speech;
        private Playback playback;

        internal NotificationLastFrameFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (instance != null)
                return;
            // This managed owner does not require the optional native skip feature.
            speech = NotificationSpeechPlayback.Get(log);
            MethodInfo playMethod = typeof(SFXManager).GetMethod("playBink", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
            MethodInfo endMethod = typeof(MainHUD).GetMethod("RadarME_Ended", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(object), typeof(RoutedEventArgs) }, null);
            if (playMethod == null || playMethod.ReturnType != typeof(void) ||
                endMethod == null || endMethod.ReturnType != typeof(void))
                throw new MissingMethodException("Minimap video playback contract changed.");

            Hook playCandidate = null, endCandidate = null;
            try
            {
                var config = new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.NotificationLastFrame" };
                playCandidate = new Hook(playMethod, (PlayBinkDelegate)OnPlayBink, config);
                endCandidate = new Hook(endMethod, (VideoEndedDelegate)OnVideoEnded, config);
                originalPlayBink = playCandidate.GenerateTrampoline<PlayBinkDelegate>();
                originalVideoEnded = endCandidate.GenerateTrampoline<VideoEndedDelegate>();
                if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, BugfixesAndQoLPlugin.PluginGuid,
                    "NotificationLastFrame", null, OnGuiChecks, out string reason))
                    throw new InvalidOperationException(reason);
                playCandidate.Apply();
                endCandidate.Apply();
                playBinkHook = playCandidate;
                videoEndedHook = endCandidate;
                instance = this;
            }
            catch
            {
                // Only unpublished installation candidates can be undone.
                if (instance == null)
                {
                    endCandidate?.Dispose();
                    playCandidate?.Dispose();
                }
                throw;
            }
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            Shared.DebugLogHelper.LogDebug(log, "NOTIFICATION_LAST_FRAME_INSTALLED: permanent playback and MediaEnded hooks installed.");
        }

        private bool Enabled => settings.EnableMod && settings.EnableClientFeatures && settings.EnableNotificationLastFrame;

        internal void ResetMapState() => Interlocked.Exchange(ref playback, null);

        private static void OnGameTick(int tick)
        {
            if (postStartupLogged || instance == null || videoEndedHook == null || playBinkHook == null)
                return;
            postStartupLogged = true;
            Shared.DebugLogHelper.LogDebug(instance.log, "NOTIFICATION_LAST_FRAME_POST_STARTUP: permanent hooks active after startup cleanup.");
        }

        private static void OnPlayBink(SFXManager self, string binkName, bool loop, bool waitForSpeech)
        {
            Uri before = self?.requestBinkPlaybackURI;
            originalPlayBink(self, binkName, loop, waitForSpeech);
            NotificationLastFrameFeature feature = instance;
            if (feature == null || self == null || ReferenceEquals(before, self.requestBinkPlaybackURI))
                return; // Vanilla rejected the request; the existing playback still owns its state.
            feature.ResetMapState();
            if (!feature.Enabled || loop || !waitForSpeech || !self.binkWaitForSpeech ||
                self.requestBinkPlaybackURI == null || !MainViewModel.viewModelLoaded)
                return;
            try
            {
                MainViewModel main = MainViewModel.Instance;
                MainHUD hud = main.HUDmain != null ? main.HUDRoot : null;
                if (hud?.RefRadarME == null)
                    return;
                MyAudioManager audio = MyAudioManager.Instance;
                long generation = feature.speech.Capture(audio);
                if (generation == 0)
                    return;
                Uri source = self.requestBinkPlaybackURI;
                string value = source.OriginalString;
                if (string.IsNullOrEmpty(value) || value.EndsWith("*", StringComparison.Ordinal))
                    return;
                source = new Uri(value + "**", source.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
                self.requestBinkPlaybackURI = source;
                Volatile.Write(ref feature.playback, new Playback(self, hud, source, audio, generation));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(feature.log, "NOTIFICATION_LAST_FRAME_URI_FAILED: Vanilla playback retained: " + ex);
            }
        }

        private static void OnVideoEnded(MainHUD self, object sender, RoutedEventArgs args)
        {
            NotificationLastFrameFeature feature = instance;
            if (feature == null || !feature.Enabled)
            {
                originalVideoEnded(self, sender, args);
                return;
            }
            Playback current;
            bool previousWait, retain;
            try
            {
                // A replacement without a speech-wait request has no retained-frame record.
                // Validate the live HUD independently so late events from the previous HUD
                // cannot clear the replacement's global playback flags either.
                if (MainViewModel.viewModelLoaded && !ReferenceEquals(MainViewModel.Instance.HUDRoot, self))
                    return;
                current = feature == null ? null : Volatile.Read(ref feature.playback);
                if (current != null && !current.Matches(self, sender))
                    return; // An obsolete HUD/source event must not mutate a replacement playback.
                if (current == null || current.Sfx.requestBinkPlayState != 1)
                {
                    current = null;
                }
                previousWait = current != null && current.Sfx.binkWaitForSpeech;
                retain = current != null && feature.Enabled && feature.IsOwnSpeechActive(current);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(feature?.log, "NOTIFICATION_LAST_FRAME_GUARD_FAILED: " + ex);
                originalVideoEnded(self, sender, args);
                return;
            }
            if (current == null)
            {
                originalVideoEnded(self, sender, args);
                return;
            }
            try
            {
                // Never promote an unrelated channel occupant to the video's speech owner.
                current.Sfx.binkWaitForSpeech = previousWait && retain;
                originalVideoEnded(self, sender, args);
            }
            finally
            {
                if (current.IsCurrentRequest())
                    current.Sfx.binkWaitForSpeech = previousWait;
            }
            try
            {
                if (!retain || !current.IsCurrentRequest() || current.Sfx.requestBinkPlayState != 3 ||
                    !current.Sfx.binkIsPlaying || !feature.IsOwnSpeechActive(current))
                    return;
                MediaElement media = current.Media;
                media.Pause();
                media.Opacity = 1f;
                current.Held = true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(feature.log, "NOTIFICATION_LAST_FRAME_DISPLAY_FAILED: " + ex);
            }
        }

        private bool IsOwnSpeechActive(Playback current) =>
            speech.IsCurrent(current.Audio, current.Generation) && current.Audio != null && current.Audio.isSpeechPlaying(1);

        private static void OnGuiChecks(PresentationPostEventArgs args)
        {
            NotificationLastFrameFeature feature = instance;
            if (feature == null || !args.OriginalCompleted)
                return;
            Playback current = Volatile.Read(ref feature.playback);
            if (current == null)
                return;
            try
            {
                if (!current.IsCurrentRequest() || !MainViewModel.viewModelLoaded ||
                    !ReferenceEquals(MainViewModel.Instance.HUDRoot, current.Hud) ||
                    !ReferenceEquals(current.Hud.RefRadarME, current.Media) ||
                    current.Sfx.requestBinkPlayState == 0)
                {
                    Interlocked.CompareExchange(ref feature.playback, null, current);
                    return;
                }
                if (current.Media.Source != current.Source)
                    return; // The requested source may still be waiting behind a briefing or replacement delay.
                if (current.Held && current.Sfx.requestBinkPlayState == 3 &&
                    (!feature.Enabled || !feature.IsOwnSpeechActive(current)))
                {
                    // Close only our still-current video, never the replacement speech or video.
                    current.Hud.RadarME_Ended();
                    Interlocked.CompareExchange(ref feature.playback, null, current);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(feature.log, "NotificationLastFrame.GuiChecks",
                    "NOTIFICATION_LAST_FRAME_COMPLETION_FAILED: " + ex);
            }
        }

        private sealed class Playback
        {
            internal readonly SFXManager Sfx;
            internal readonly MainHUD Hud;
            internal readonly MediaElement Media;
            internal readonly Uri Source;
            internal readonly MyAudioManager Audio;
            internal readonly long Generation;
            internal bool Held;
            internal Playback(SFXManager sfx, MainHUD hud, Uri source, MyAudioManager audio, long generation)
            { Sfx = sfx; Hud = hud; Media = hud.RefRadarME; Source = source; Audio = audio; Generation = generation; }
            internal bool IsCurrentRequest() => ReferenceEquals(SFXManager.instance, Sfx) &&
                ReferenceEquals(Sfx.requestBinkPlaybackURI, Source);
            internal bool Matches(MainHUD hud, object sender) => IsCurrentRequest() && ReferenceEquals(Hud, hud) &&
                ReferenceEquals(Media, hud.RefRadarME) && ReferenceEquals(sender, Media) && Media.Source == Source;
        }
    }
}
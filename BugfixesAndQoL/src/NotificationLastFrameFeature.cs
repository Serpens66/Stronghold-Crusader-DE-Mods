// Feature: Hold a minimap video's last frame for speech, with HD's fixed 20-second bound.
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using NoesisApp;
using SHCDESE.API;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed partial class NotificationLastFrameFeature
    {
        private delegate void VideoEndedDelegate(MainHUD self, object sender, RoutedEventArgs args);
        private delegate void PlayBinkDelegate(SFXManager self, string binkName, bool loop, bool waitForSpeech);
        private static NotificationLastFrameFeature instance;
        private static Hook videoEndedHook, playBinkHook;
        private static VideoEndedDelegate originalVideoEnded;
        private static PlayBinkDelegate originalPlayBink;
        private static MethodInfo videoPathResolver;
        private static bool postStartupLogged;
        // HD 00409200/00408FC0 expire the speech binding only after elapsed > 20000 ms.
        private static readonly long MaximumSpeechWaitTicks = 20L * Stopwatch.Frequency;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Func<long> timestamp;
        private Playback playback;

        internal NotificationLastFrameFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
            : this(log, settings, Stopwatch.GetTimestamp) { }

        // The internal clock seam exercises the production completion path without real-time sleeps.
        internal NotificationLastFrameFeature(ManualLogSource log, BugfixesAndQoLViewModel settings, Func<long> timestamp)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.timestamp = timestamp ?? throw new ArgumentNullException(nameof(timestamp));
            if (instance != null)
                return;
            // Use the installed asset resolver before adding the Noesis playback suffix.
            videoPathResolver = typeof(GameAssetManagerAPI).GetMethod("TryResolveVideoPath",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string).MakeByRefType() }, null);
            if (videoPathResolver == null || videoPathResolver.ReturnType != typeof(bool))
                throw new MissingMethodException("Script Extender video asset resolution contract changed.");
            MethodInfo playMethod = typeof(SFXManager).GetMethod("playBink", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
            MethodInfo endMethod = typeof(MainHUD).GetMethod("RadarME_Ended", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(object), typeof(RoutedEventArgs) }, null);
            if (playMethod == null || playMethod.ReturnType != typeof(void) ||
                endMethod == null || endMethod.ReturnType != typeof(void))
                throw new MissingMethodException("Minimap video presentation contract changed.");

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
            DiagInitialize();
            Shared.DebugLogHelper.LogDebug(log, "NOTIFICATION_LAST_FRAME_INSTALLED: permanent playback and MediaEnded hooks installed; HD speech wait bound is 20000 ms.");
        }

        private bool Enabled => settings.EnableClientFeatures && settings.EnableNotificationLastFrame;

        // Implemented only by the development build; calls and arguments disappear otherwise.
        partial void DiagInitialize();
        static partial void DiagRequest(SFXManager self, string name, bool loop, bool wait, Uri before);
        partial void DiagAttach(Playback current);
        static partial void DiagEnd(MainHUD hud, object sender, string stage);
        partial void DiagState(Playback current, string stage);
        static partial void DiagGui(PresentationPostEventArgs args);

        // Mission notifications may come from another thread. Close Noesis only on the presentation caller.
        internal void ResetMapState()
        {
            Playback current = Volatile.Read(ref playback);
            if (current != null)
                Volatile.Write(ref current.Invalidated, true);
            DiagState(current, "map-invalidated");
        }

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
            DiagRequest(self, binkName, loop, waitForSpeech, before);
            NotificationLastFrameFeature feature = instance;
            if (feature == null || self == null || ReferenceEquals(before, self.requestBinkPlaybackURI))
                return; // Rejected requests neither replace the owner nor renew its deadline.
            try
            {
                long started = feature.timestamp();
                feature.Finish(Volatile.Read(ref feature.playback));
                if (!feature.Enabled || loop || self.requestBinkPlaybackURI == null || !MainViewModel.viewModelLoaded)
                    return;
                MainViewModel main = MainViewModel.Instance;
                if (main?.HUDmain == null)
                    return;
                MainHUD hud = main.HUDRoot;
                if (hud?.RefRadarME == null)
                    return;
                Uri source = self.requestBinkPlaybackURI;
                string value = source.OriginalString;
                if (string.IsNullOrEmpty(value) || value.EndsWith("*", StringComparison.Ordinal))
                    return;
                // SE's MediaElement setter can redirect to an absolute .webm/.mp4 and
                // discard **. Resolve once here so both identity and end semantics survive.
                source = ResolveVideoSource(source);
                value = source.OriginalString;
                source = new Uri(value + "**", source.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
                self.requestBinkPlaybackURI = source;
                Volatile.Write(ref feature.playback, new Playback(self, main, hud, source,
                    MyAudioManager.Instance, started, waitForSpeech && self.binkWaitForSpeech));
                feature.DiagAttach(Volatile.Read(ref feature.playback));
            }
            catch (Exception ex) { feature.LogFailure("URI", ex); }
        }

        private static void OnVideoEnded(MainHUD self, object sender, RoutedEventArgs args)
        {
            DiagEnd(self, sender, "hud-end-enter");
            NotificationLastFrameFeature feature = instance;
            if (feature != null && feature.Enabled)
            {
                try
                {
                    if (MainViewModel.viewModelLoaded && !ReferenceEquals(MainViewModel.Instance.HUDRoot, self))
                    {
                        DiagEnd(self, sender, "reject-old-hud");
                        return;
                    }
                    Playback current = Volatile.Read(ref feature.playback);
                    if (current != null)
                    {
                        if (!current.Matches(self, sender))
                        {
                            DiagEnd(self, sender, "reject-identity");
                            return; // A late event must not finish its replacement.
                        }
                        if (Volatile.Read(ref current.Invalidated))
                        {
                            feature.Finish(current);
                            return;
                        }
                        if (current.Sfx.requestBinkPlayState == 1)
                        {
                            if (!feature.IsSpeechWaitActive(current))
                            {
                                feature.Finish(current);
                                return;
                            }
                            // State 1 + playing avoids FatControler's speech-dependent state-3 cleanup.
                            // ** is nonlooping; Pause keeps the frame and respects a previous left-click hide.
                            current.Held = true;
                            feature.DiagState(current, "held");
                            current.Media.Pause();
                            return;
                        }
                    }
                }
                catch (Exception ex) { feature.LogFailure("DISPLAY", ex); }
            }
            DiagEnd(self, sender, "vanilla-end-fallback");
            originalVideoEnded(self, sender, args);
            DiagEnd(self, sender, "vanilla-end-return");
        }

        private bool IsSpeechWaitActive(Playback current)
        {
            // Like HD's stream-index clear, expiry/end is latched even while the video is
            // still playing. Later speech on the same channel cannot re-arm this binding.
            if (current.WaitingForSpeech && (timestamp() - current.Started > MaximumSpeechWaitTicks ||
                current.Audio == null || !ReferenceEquals(MyAudioManager.Instance, current.Audio) ||
                !current.Audio.isSpeechPlaying(1)))
            {
                current.WaitingForSpeech = false;
                DiagState(current, "speech-wait-cleared");
            }
            return current.WaitingForSpeech;
        }

        private static void OnGuiChecks(PresentationPostEventArgs args)
        {
            DiagGui(args);
            NotificationLastFrameFeature feature = instance;
            if (feature == null || !args.OriginalCompleted)
                return;
            try
            {
                Playback current = Volatile.Read(ref feature.playback);
                if (current == null)
                    return;
                MainViewModel main = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
                if (Volatile.Read(ref current.Invalidated) || !feature.Enabled ||
                    !ReferenceEquals(main, current.Main) || !ReferenceEquals(main?.HUDRoot, current.Hud) ||
                    !ReferenceEquals(current.Hud.RefRadarME, current.Media) ||
                    !current.IsCurrentRequest() || current.Sfx.requestBinkPlayState == 0)
                {
                    feature.Finish(current);
                    return;
                }
                bool waiting = feature.IsSpeechWaitActive(current);
                if (current.Held && !waiting)
                {
                    feature.Finish(current);
                    return;
                }
                if (current.Held && current.HasMediaSource(current.Media.Source))
                    current.Media.Pause(); // Vanilla may resume the media after closing a briefing.
            }
            catch (Exception ex) { feature.LogFailure("COMPLETION", ex); }
        }

        private void Finish(Playback current)
        {
            if (current == null)
                return;
            DiagState(current, "close-attempt");
            // Never clear global flags belonging to a replacement request or HUD.
            if (current.IsCurrentRequest() && MainViewModel.viewModelLoaded &&
                ReferenceEquals(MainViewModel.Instance.HUDRoot, current.Hud) &&
                ReferenceEquals(current.Hud.RefRadarME, current.Media) &&
                (current.HasMediaSource(current.Media.Source) || current.Media.Source == null))
                current.Hud.RadarME_Ended();
            else if (current.HasMediaSource(current.Media.Source))
            {
                current.Media.Stop();
                current.Media.Source = null;
                current.Media.Close();
                current.Media.Opacity = 0f;
            }
            Interlocked.CompareExchange(ref playback, null, current);
            DiagState(current, "close-return");
        }

        private void LogFailure(string operation, Exception ex) =>
            Shared.DebugLogHelper.LogErrorLimited(log, "NotificationLastFrame." + operation,
                "NOTIFICATION_LAST_FRAME_" + operation + "_FAILED: " + ex);

        private static Uri ResolveVideoSource(Uri source)
        {
            object[] arguments = { source.OriginalString.TrimStart('/', '\\'), null };
            return (bool)videoPathResolver.Invoke(GameAssetManagerAPI.Instance, arguments)
                ? new Uri((string)arguments[1], UriKind.Absolute) : source;
        }

        private static string MediaSourceKey(Uri source)
        {
            string value = source?.OriginalString;
            return value == null || value.IndexOf('\\') < 0 ? value : value.Replace('\\', '/');
        }

        private static bool MediaSourcesMatch(Uri actual, Uri expected, string expectedKey) =>
            actual != null && expected != null && actual.IsAbsoluteUri == expected.IsAbsoluteUri &&
            (actual == expected || string.Equals(MediaSourceKey(actual), expectedKey, StringComparison.Ordinal));

        private sealed class Playback
        {
            internal readonly SFXManager Sfx;
            internal readonly MainViewModel Main;
            internal readonly MainHUD Hud;
            internal readonly MediaElement Media;
            internal readonly Uri Source;
            internal readonly string SourceKey;
            internal readonly MyAudioManager Audio;
            internal readonly long Started;
            internal bool WaitingForSpeech, Held, Invalidated;
            internal Playback(SFXManager sfx, MainViewModel main, MainHUD hud, Uri source,
                MyAudioManager audio, long started, bool waitingForSpeech)
            {
                Sfx = sfx; Main = main; Hud = hud; Media = hud.RefRadarME; Source = source;
                Audio = audio; Started = started; WaitingForSpeech = waitingForSpeech;
                SourceKey = MediaSourceKey(source); // No expected-path normalization/allocation in GUI checks.
            }
            internal bool HasMediaSource(Uri actual) => MediaSourcesMatch(actual, Source, SourceKey);
            internal bool IsCurrentRequest() => ReferenceEquals(SFXManager.instance, Sfx) &&
                ReferenceEquals(Sfx.requestBinkPlaybackURI, Source);
            internal bool Matches(MainHUD hud, object sender) => IsCurrentRequest() && ReferenceEquals(Hud, hud) &&
                ReferenceEquals(Media, hud.RefRadarME) && ReferenceEquals(sender, Media) && HasMediaSource(Media.Source);
        }
    }
}

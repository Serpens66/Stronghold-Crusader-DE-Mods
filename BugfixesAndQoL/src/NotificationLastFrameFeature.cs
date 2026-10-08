// Feature: Keep the last frame of a minimap video visible while its speech continues.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using NoesisApp;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class NotificationLastFrameFeature
    {
        private delegate void VideoEndedDelegate(MainHUD self, object sender, RoutedEventArgs args);
        private delegate void PlayBinkDelegate(SFXManager self, string binkName, bool loop, bool waitForSpeech);

        private static NotificationLastFrameFeature instance;
        private static Hook videoEndedHook;
        private static Hook playBinkHook;
        private static VideoEndedDelegate originalVideoEnded;
        private static PlayBinkDelegate originalPlayBink;
        private static bool postStartupLogged;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;

        internal NotificationLastFrameFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (instance != null)
                return;

            Hook playCandidate = null;
            Hook endCandidate = null;
            bool tickSubscribed = false;
            try
            {
                MethodInfo playMethod = typeof(SFXManager).GetMethod(
                    "playBink", BindingFlags.Instance | BindingFlags.Public, null,
                    new[] { typeof(string), typeof(bool), typeof(bool) }, null);
                MethodInfo endMethod = typeof(MainHUD).GetMethod(
                    "RadarME_Ended", BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new[] { typeof(object), typeof(RoutedEventArgs) }, null);
                if (playMethod == null || playMethod.ReturnType != typeof(void) ||
                    endMethod == null || endMethod.ReturnType != typeof(void))
                    throw new MissingMethodException("Minimap video playback contract changed.");

                playCandidate = new Hook(playMethod, (PlayBinkDelegate)OnPlayBink);
                PlayBinkDelegate playOriginal = playCandidate.GenerateTrampoline<PlayBinkDelegate>();
                endCandidate = new Hook(endMethod, (VideoEndedDelegate)OnVideoEnded);
                VideoEndedDelegate endOriginal = endCandidate.GenerateTrampoline<VideoEndedDelegate>();
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;
                originalPlayBink = playOriginal;
                originalVideoEnded = endOriginal;
                playBinkHook = playCandidate;
                videoEndedHook = endCandidate;
                instance = this;
            }
            catch
            {
                // Only unpublished installation candidates can be undone.
                if (instance == null)
                {
                    if (tickSubscribed)
                        GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
                    if (endCandidate != null)
                    {
                        endCandidate.Undo();
                        endCandidate.Dispose();
                    }
                    if (playCandidate != null)
                    {
                        playCandidate.Undo();
                        playCandidate.Dispose();
                    }
                }
                throw;
            }
            Shared.DebugLogHelper.LogDebug(log, "NOTIFICATION_LAST_FRAME_INSTALLED: permanent playback and MediaEnded hooks installed.");
        }

        private bool Enabled => settings.EnableMod && settings.EnableClientFeatures &&
            settings.EnableNotificationLastFrame;

        private static void OnGameTick(int tick)
        {
            if (postStartupLogged || instance == null || videoEndedHook == null || playBinkHook == null)
                return;
            postStartupLogged = true;
            Shared.DebugLogHelper.LogDebug(instance.log, "NOTIFICATION_LAST_FRAME_POST_STARTUP: permanent hooks active after startup cleanup.");
        }

        private static void OnPlayBink(SFXManager self, string binkName, bool loop, bool waitForSpeech)
        {
            originalPlayBink(self, binkName, loop, waitForSpeech);
            NotificationLastFrameFeature feature = instance;
            if (feature == null || !feature.Enabled || loop || self?.requestBinkPlaybackURI == null)
                return;

            try
            {
                Uri original = self.requestBinkPlaybackURI;
                string value = original.OriginalString;
                if (string.IsNullOrEmpty(value) || value.EndsWith("*", StringComparison.Ordinal))
                    return;
                self.requestBinkPlaybackURI = new Uri(
                    value + "**", original.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
            }
            catch (Exception ex)
            {
                feature.log.LogError("NOTIFICATION_LAST_FRAME_URI_FAILED: Vanilla playback retained: " + ex);
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

            SFXManager sfx = null;
            MediaElement media = null;
            bool retain = false;
            bool previousWaitForSpeech = false;
            try
            {
                sfx = SFXManager.instance;
                media = self?.RefRadarME;
                retain = sfx != null && media != null &&
                    sfx.requestBinkPlayState == 1 && sfx.binkIsPlaying &&
                    MyAudioManager.Instance != null && MyAudioManager.Instance.isSpeechPlaying(1);
                if (retain)
                {
                    previousWaitForSpeech = sfx.binkWaitForSpeech;
                    sfx.binkWaitForSpeech = true;
                }
            }
            catch (Exception ex)
            {
                retain = false;
                feature.log.LogError("NOTIFICATION_LAST_FRAME_GUARD_FAILED: " + ex);
            }

            try
            {
                originalVideoEnded(self, sender, args);
            }
            finally
            {
                if (retain)
                    sfx.binkWaitForSpeech = previousWaitForSpeech;
            }
            if (!retain)
                return;

            try
            {
                if (sfx.requestBinkPlayState != 3 || !sfx.binkIsPlaying ||
                    !MyAudioManager.Instance.isSpeechPlaying(1) || media.Source == null)
                    return;
                media.Pause();
                media.Opacity = 1f;
                Shared.DebugLogHelper.LogDebug(feature.log, "NOTIFICATION_LAST_FRAME_HELD: final frame held while speech continues.");
            }
            catch (Exception ex)
            {
                feature.log.LogError("NOTIFICATION_LAST_FRAME_DISPLAY_FAILED: " + ex);
            }
        }
    }
}

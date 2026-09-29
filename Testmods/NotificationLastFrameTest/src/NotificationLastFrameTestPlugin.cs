using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using NoesisApp;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Reflection;

namespace NotificationLastFrameTest
{
    [BepInDependency("000shcdese", "2.10.4")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class NotificationLastFrameTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "NotificationLastFrameTest_Serp";
        public const string Name = "Notification Last Frame Test";
        public const string Version = "0.1.0";

        private delegate void VideoEndedDelegate(MainHUD self, object sender, RoutedEventArgs args);
        private delegate void PlayBinkDelegate(SFXManager self, string binkName, bool loop, bool waitForSpeech);

        private static ManualLogSource persistentLog;
        private static Hook videoEndedHook;
        private static Hook playBinkHook;
        private static VideoEndedDelegate originalVideoEnded;
        private static PlayBinkDelegate originalPlayBink;
        private static bool librarySubscribed;
        private static bool tickSubscribed;
        private static bool postStartupLogged;

        private void Awake()
        {
            persistentLog = Logger;
            persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_LOADED: waiting for the Script Extender library.");
            if (librarySubscribed)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            librarySubscribed = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (videoEndedHook != null || playBinkHook != null)
                return;

            Hook videoCandidate = null;
            Hook playCandidate = null;
            try
            {
                MethodInfo playMethod = typeof(SFXManager).GetMethod(
                    "playBink",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(string), typeof(bool), typeof(bool) },
                    null);
                if (playMethod == null || playMethod.ReturnType != typeof(void))
                    throw new MissingMethodException(typeof(SFXManager).FullName, "playBink(string, bool, bool)");

                MethodInfo endMethod = typeof(MainHUD).GetMethod(
                    "RadarME_Ended",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(object), typeof(RoutedEventArgs) },
                    null);
                if (endMethod == null || endMethod.ReturnType != typeof(void))
                    throw new MissingMethodException(typeof(MainHUD).FullName, "RadarME_Ended(object, RoutedEventArgs)");

                playCandidate = new Hook(playMethod, (PlayBinkDelegate)OnPlayBink);
                PlayBinkDelegate playTrampoline = playCandidate.GenerateTrampoline<PlayBinkDelegate>();
                videoCandidate = new Hook(endMethod, (VideoEndedDelegate)OnVideoEnded);
                VideoEndedDelegate endTrampoline = videoCandidate.GenerateTrampoline<VideoEndedDelegate>();
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;
                originalPlayBink = playTrampoline;
                originalVideoEnded = endTrampoline;
                playBinkHook = playCandidate;
                videoEndedHook = videoCandidate;
            }
            catch (Exception ex)
            {
                // Only unpublished, failed installation candidates may be rolled back.
                if (videoEndedHook == null && playBinkHook == null)
                {
                    if (videoCandidate != null)
                    {
                        videoCandidate.Undo();
                        videoCandidate.Dispose();
                    }
                    if (playCandidate != null)
                    {
                        playCandidate.Undo();
                        playCandidate.Dispose();
                    }
                }
                persistentLog.LogError("NOTIFICATION_LAST_FRAME_TEST_INSTALL_FAILED: " + ex);
                return;
            }

            persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_INSTALLED: permanent playback and MediaEnded hooks installed.");
        }

        private static void OnGameTick(int tick)
        {
            if (postStartupLogged || !tickSubscribed || videoEndedHook == null || playBinkHook == null)
                return;

            postStartupLogged = true;
            persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_POST_STARTUP: permanent hook active after startup cleanup.");
        }

        private static void OnPlayBink(SFXManager self, string binkName, bool loop, bool waitForSpeech)
        {
            originalPlayBink(self, binkName, loop, waitForSpeech);
            if (loop || self?.requestBinkPlaybackURI == null)
                return;

            try
            {
                Uri original = self.requestBinkPlaybackURI;
                string value = original.OriginalString;
                if (string.IsNullOrEmpty(value) || value.EndsWith("*", StringComparison.Ordinal))
                    return;

                self.requestBinkPlaybackURI = new Uri(
                    value + "**",
                    original.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
                persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_URI: non-looping MediaEnded playback selected.");
            }
            catch (Exception ex)
            {
                persistentLog.LogError("NOTIFICATION_LAST_FRAME_TEST_URI_FAILED: Vanilla playback retained: " + ex);
            }
        }

        private static void OnVideoEnded(MainHUD self, object sender, RoutedEventArgs args)
        {
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
                persistentLog.LogError("NOTIFICATION_LAST_FRAME_TEST_GUARD_FAILED: " + ex);
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
                persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_HELD: playback paused at its end while speech continues.");
            }
            catch (Exception ex)
            {
                persistentLog.LogError("NOTIFICATION_LAST_FRAME_TEST_DISPLAY_FAILED: " + ex);
            }
        }
    }
}

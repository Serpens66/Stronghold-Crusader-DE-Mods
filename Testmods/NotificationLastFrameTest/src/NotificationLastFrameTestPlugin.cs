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

        private static ManualLogSource persistentLog;
        private static Hook videoEndedHook;
        private static VideoEndedDelegate originalVideoEnded;
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
            if (videoEndedHook != null)
                return;

            Hook candidate = null;
            try
            {
                MethodInfo method = typeof(MainHUD).GetMethod(
                    "RadarME_Ended",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(object), typeof(RoutedEventArgs) },
                    null);
                if (method == null || method.ReturnType != typeof(void))
                    throw new MissingMethodException(typeof(MainHUD).FullName, "RadarME_Ended(object, RoutedEventArgs)");

                candidate = new Hook(method, (VideoEndedDelegate)OnVideoEnded);
                VideoEndedDelegate trampoline = candidate.GenerateTrampoline<VideoEndedDelegate>();
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;
                originalVideoEnded = trampoline;
                videoEndedHook = candidate;
                persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_INSTALLED: permanent MediaEnded hook installed.");
            }
            catch (Exception ex)
            {
                // Only an unpublished, failed installation candidate may be rolled back.
                if (videoEndedHook == null && candidate != null)
                {
                    candidate.Undo();
                    candidate.Dispose();
                }
                persistentLog.LogError("NOTIFICATION_LAST_FRAME_TEST_INSTALL_FAILED: " + ex);
            }
        }

        private static void OnGameTick(int tick)
        {
            if (postStartupLogged || !tickSubscribed || videoEndedHook == null)
                return;

            postStartupLogged = true;
            persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_POST_STARTUP: permanent hook active after startup cleanup.");
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

                media.Opacity = 1f;
                persistentLog.LogInfo("NOTIFICATION_LAST_FRAME_TEST_HELD: video ended; last frame shown while speech continues.");
            }
            catch (Exception ex)
            {
                persistentLog.LogError("NOTIFICATION_LAST_FRAME_TEST_DISPLAY_FAILED: " + ex);
            }
        }
    }
}

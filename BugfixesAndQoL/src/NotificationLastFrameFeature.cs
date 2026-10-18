// Feature: Hold a minimap video's last frame until its Vanilla message text disappears.
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using NoesisApp;
using SHCDESE.API;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class NotificationLastFrameFeature
    {
        private delegate void VideoEndedDelegate(MainHUD self, object sender, RoutedEventArgs args);
        private delegate void PlayBinkDelegate(SFXManager self, string binkName, bool loop, bool waitForSpeech);
        private delegate void AddTextDelegate(OnScreenText self, Enums.eOnScreenText id,
            int data1, int data2, int data3, int data4, int data5);
        private static NotificationLastFrameFeature instance;
        private static Hook videoEndedHook, playBinkHook, addTextHook;
        private static VideoEndedDelegate originalVideoEnded;
        private static PlayBinkDelegate originalPlayBink;
        private static AddTextDelegate originalAddText;
        private static bool postStartupLogged;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private MainViewModel observedMain;
        private long textGeneration, pendingTextGeneration;
        private Playback playback;

        internal NotificationLastFrameFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (instance != null)
                return;
            MethodInfo playMethod = typeof(SFXManager).GetMethod("playBink", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
            MethodInfo endMethod = typeof(MainHUD).GetMethod("RadarME_Ended", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(object), typeof(RoutedEventArgs) }, null);
            MethodInfo textMethod = typeof(OnScreenText).GetMethod("addOSTEntry", BindingFlags.Instance | BindingFlags.Public,
                null, new[] { typeof(Enums.eOnScreenText), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            if (playMethod == null || playMethod.ReturnType != typeof(void) ||
                endMethod == null || endMethod.ReturnType != typeof(void) ||
                textMethod == null || textMethod.ReturnType != typeof(void))
                throw new MissingMethodException("Minimap video/message presentation contract changed.");

            Hook playCandidate = null, endCandidate = null, textCandidate = null;
            try
            {
                var config = new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.NotificationLastFrame" };
                playCandidate = new Hook(playMethod, (PlayBinkDelegate)OnPlayBink, config);
                endCandidate = new Hook(endMethod, (VideoEndedDelegate)OnVideoEnded, config);
                textCandidate = new Hook(textMethod, (AddTextDelegate)OnAddText, config);
                originalPlayBink = playCandidate.GenerateTrampoline<PlayBinkDelegate>();
                originalVideoEnded = endCandidate.GenerateTrampoline<VideoEndedDelegate>();
                originalAddText = textCandidate.GenerateTrampoline<AddTextDelegate>();
                if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, BugfixesAndQoLPlugin.PluginGuid,
                    "NotificationLastFrame", null, OnGuiChecks, out string reason))
                    throw new InvalidOperationException(reason);
                playCandidate.Apply();
                endCandidate.Apply();
                textCandidate.Apply();
                playBinkHook = playCandidate;
                videoEndedHook = endCandidate;
                addTextHook = textCandidate;
                instance = this;
            }
            catch
            {
                // Only unpublished installation candidates can be undone.
                if (instance == null)
                {
                    textCandidate?.Dispose();
                    endCandidate?.Dispose();
                    playCandidate?.Dispose();
                }
                throw;
            }
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            Shared.DebugLogHelper.LogDebug(log, "NOTIFICATION_LAST_FRAME_INSTALLED: permanent playback, text and MediaEnded hooks installed.");
        }

        private bool Enabled => settings.EnableMod && settings.EnableClientFeatures && settings.EnableNotificationLastFrame;

        // Mission notifications may come from another thread. Close Noesis only on the presentation caller.
        internal void ResetMapState()
        {
            Interlocked.Exchange(ref pendingTextGeneration, 0);
            Playback current = Volatile.Read(ref playback);
            if (current != null)
                Volatile.Write(ref current.Invalidated, true);
        }

        private static void OnGameTick(int tick)
        {
            if (postStartupLogged || instance == null || videoEndedHook == null || playBinkHook == null || addTextHook == null)
                return;
            postStartupLogged = true;
            Shared.DebugLogHelper.LogDebug(instance.log, "NOTIFICATION_LAST_FRAME_POST_STARTUP: permanent hooks active after startup cleanup.");
        }

        private MainViewModel ObserveMain()
        {
            if (!MainViewModel.viewModelLoaded)
                return null;
            MainViewModel main = MainViewModel.Instance;
            if (!ReferenceEquals(main, observedMain))
            {
                // Rebind a replaceable ViewModel, never uninstall a process-wide hook.
                if (observedMain != null)
                    observedMain.PropertyChanged -= OnMessageVisibilityChanged;
                observedMain = main;
                if (main != null)
                    main.PropertyChanged += OnMessageVisibilityChanged;
            }
            return main;
        }

        private static void OnAddText(OnScreenText self, Enums.eOnScreenText id,
            int data1, int data2, int data3, int data4, int data5)
        {
            originalAddText(self, id, data1, data2, data3, data4, data5);
            NotificationLastFrameFeature feature = instance;
            if (feature == null || id != Enums.eOnScreenText.OST_MESSAGE_BAR)
                return;
            try
            {
                feature.Finish(Volatile.Read(ref feature.playback));
                long generation = Interlocked.Increment(ref feature.textGeneration);
                Interlocked.Exchange(ref feature.pendingTextGeneration,
                    feature.Enabled && feature.ObserveMain() != null ? generation : 0);
            }
            catch (Exception ex) { feature.LogFailure("TEXT", ex); }
        }

        private static void OnPlayBink(SFXManager self, string binkName, bool loop, bool waitForSpeech)
        {
            Uri before = self?.requestBinkPlaybackURI;
            originalPlayBink(self, binkName, loop, waitForSpeech);
            NotificationLastFrameFeature feature = instance;
            if (feature == null || self == null || ReferenceEquals(before, self.requestBinkPlaybackURI))
                return; // Rejected requests do not replace the owner or consume the pending text.
            try
            {
                feature.Finish(Volatile.Read(ref feature.playback));
                long generation = Interlocked.Exchange(ref feature.pendingTextGeneration, 0);
                MainViewModel main = feature.ObserveMain();
                if (!feature.Enabled || loop || generation == 0 || self.requestBinkPlaybackURI == null || main?.HUDmain == null)
                    return;
                MainHUD hud = main.HUDRoot;
                if (hud?.RefRadarME == null)
                    return;
                Uri source = self.requestBinkPlaybackURI;
                string value = source.OriginalString;
                if (string.IsNullOrEmpty(value) || value.EndsWith("*", StringComparison.Ordinal))
                    return;
                source = new Uri(value + "**", source.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
                self.requestBinkPlaybackURI = source;
                Volatile.Write(ref feature.playback, new Playback(self, main, hud, source, generation));
            }
            catch (Exception ex) { feature.LogFailure("URI", ex); }
        }

        private static void OnVideoEnded(MainHUD self, object sender, RoutedEventArgs args)
        {
            NotificationLastFrameFeature feature = instance;
            if (feature != null && feature.Enabled)
            {
                try
                {
                    if (MainViewModel.viewModelLoaded && !ReferenceEquals(MainViewModel.Instance.HUDRoot, self))
                        return;
                    Playback current = Volatile.Read(ref feature.playback);
                    if (current != null)
                    {
                        if (!current.Matches(self, sender))
                            return; // A late event must not finish its replacement.
                        if (Volatile.Read(ref current.Invalidated) || current.Generation != feature.textGeneration)
                        {
                            feature.Finish(current);
                            return;
                        }
                        if (current.Sfx.requestBinkPlayState == 1)
                        {
                            // State 1 + playing avoids FatControler's speech-dependent state-3 cleanup.
                            // ** is nonlooping; Pause keeps the frame and respects a previous left-click hide.
                            current.Media.Pause();
                            current.Held = true;
                            return;
                        }
                    }
                }
                catch (Exception ex) { feature.LogFailure("DISPLAY", ex); }
            }
            originalVideoEnded(self, sender, args);
        }

        private void OnMessageVisibilityChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(MainViewModel.OST_Message_Bar_Vis))
                return;
            try
            {
                Playback current = Volatile.Read(ref playback);
                if (current != null && ReferenceEquals(sender, current.Main) && !current.Main.OST_Message_Bar_Vis)
                    Finish(current);
            }
            catch (Exception ex) { LogFailure("VISIBILITY", ex); }
        }

        private static void OnGuiChecks(PresentationPostEventArgs args)
        {
            NotificationLastFrameFeature feature = instance;
            if (feature == null || !args.OriginalCompleted)
                return;
            try
            {
                // Native text precedes video in the same rendered batch. Unconsumed text must
                // not attach to an unrelated video in a later batch.
                Interlocked.Exchange(ref feature.pendingTextGeneration, 0);
                Playback current = Volatile.Read(ref feature.playback);
                if (current == null)
                    return;
                MainViewModel main = feature.ObserveMain();
                if (Volatile.Read(ref current.Invalidated) || !feature.Enabled ||
                    !ReferenceEquals(main, current.Main) || !ReferenceEquals(main?.HUDRoot, current.Hud) ||
                    !ReferenceEquals(current.Hud.RefRadarME, current.Media) ||
                    !current.IsCurrentRequest() || current.Sfx.requestBinkPlayState == 0 ||
                    !main.OST_Message_Bar_Vis)
                {
                    // Only now has the presentation completed; initial false before the first text update is safe.
                    feature.Finish(current);
                    return;
                }
                if (current.Held && current.Media.Source == current.Source)
                    current.Media.Pause(); // Vanilla may resume the media after closing a briefing.
            }
            catch (Exception ex) { feature.LogFailure("COMPLETION", ex); }
        }

        private void Finish(Playback current)
        {
            if (current == null)
                return;
            // Never clear global flags belonging to a replacement request or HUD.
            if (current.IsCurrentRequest() && MainViewModel.viewModelLoaded &&
                ReferenceEquals(MainViewModel.Instance.HUDRoot, current.Hud) &&
                ReferenceEquals(current.Hud.RefRadarME, current.Media))
                current.Hud.RadarME_Ended();
            else if (current.Media.Source == current.Source)
            {
                current.Media.Stop();
                current.Media.Source = null;
                current.Media.Close();
                current.Media.Opacity = 0f;
            }
            Interlocked.CompareExchange(ref playback, null, current);
        }

        private void LogFailure(string operation, Exception ex) =>
            Shared.DebugLogHelper.LogErrorLimited(log, "NotificationLastFrame." + operation,
                "NOTIFICATION_LAST_FRAME_" + operation + "_FAILED: " + ex);

        private sealed class Playback
        {
            internal readonly SFXManager Sfx;
            internal readonly MainViewModel Main;
            internal readonly MainHUD Hud;
            internal readonly MediaElement Media;
            internal readonly Uri Source;
            internal readonly long Generation;
            internal bool Held, Invalidated;
            internal Playback(SFXManager sfx, MainViewModel main, MainHUD hud, Uri source, long generation)
            { Sfx = sfx; Main = main; Hud = hud; Media = hud.RefRadarME; Source = source; Generation = generation; }
            internal bool IsCurrentRequest() => ReferenceEquals(SFXManager.instance, Sfx) &&
                ReferenceEquals(Sfx.requestBinkPlaybackURI, Source);
            internal bool Matches(MainHUD hud, object sender) => IsCurrentRequest() && ReferenceEquals(Hud, hud) &&
                ReferenceEquals(Media, hud.RefRadarME) && ReferenceEquals(sender, Media) && Media.Source == Source;
        }
    }
}

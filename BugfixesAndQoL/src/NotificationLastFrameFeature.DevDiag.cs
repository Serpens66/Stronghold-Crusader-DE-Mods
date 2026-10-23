#if SERP_DEVDIAG
using APIShared.Presentation;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using NoesisApp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Video;

namespace BugfixesAndQoL
{
    // Read-only evidence collector. No diagnostic exception may enter a playback decision.
    internal sealed partial class NotificationLastFrameFeature
    {
        private delegate void MonitorDelegate(MyAudioManager self);
        private delegate void PlayerEndDelegate(NoesisMediaPlayer self, VideoPlayer player);
        private delegate void MediaEndDelegate(MediaElement self);
        private static Hook diagMonitorHook, diagPlayerEndHook, diagMediaEndHook;
        private static MonitorDelegate diagMonitorOriginal;
        private static PlayerEndDelegate diagPlayerEndOriginal;
        private static MediaEndDelegate diagMediaEndOriginal;
        private static readonly object diagLock = new object();
        private static readonly List<Observation> diagVideos = new List<Observation>();
        private static readonly ConditionalWeakTable<object, ObjectIdentity> diagObjects = new ConditionalWeakTable<object, ObjectIdentity>();
        private static long diagVideoId, diagObjectId, diagSpeechId;
        private static int diagUnityThread, diagFrame = -1, diagErrors;
        private static long diagGui, diagSkipped, diagReplaced, diagGuiFailed, diagMonitor, diagRender;
        private static readonly BindingFlags DiagMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static FieldInfo diagMode, diagPaused, diagSource, diagClip, diagMediaPlayer, diagVideoPlayer;
        private static string diagLastAudio;
        private static string diagLastGui;

        private sealed class ObjectIdentity { internal readonly long Value = Interlocked.Increment(ref diagObjectId); }
        private sealed class Observation
        {
            internal long Id, Speech, Started, LastFull, Gui, Monitor, Render, Skipped, Replaced, Failed;
            internal SFXManager Sfx;
            internal Uri Source;
            internal string SourceKey;
            internal Playback Owner;
            internal MediaElement Media;
            internal object Player;
            internal VideoPlayer Video;
            internal int PlayerEnds, MediaEnds, HudEnds, Rejected, CloseAttempts, CloseReturns, Errors;
            internal bool Summary, Closed, SawLastFrame, SawSilentBusy, SawExpiry;
            internal string State;
        }

        partial void DiagInitialize()
        {
            Safe(() =>
            {
                Shared.DevDiag.Attach(log); // Independent of the normal DiagnosticsEnabled preference.
                diagMode = Field(typeof(MyAudioManager), "speechMode1", typeof(int));
                diagPaused = Field(typeof(MyAudioManager), "speechPaused", typeof(bool));
                diagSource = Field(typeof(MyAudioManager), "speechSource1", typeof(AudioSource));
                diagClip = Field(typeof(MyAudioManager), "speechClip1", typeof(AudioClip));
                diagMediaPlayer = Field(typeof(MediaElement), "_player", typeof(NoesisApp.MediaPlayer));
                diagVideoPlayer = Field(typeof(NoesisMediaPlayer), "_videoPlayer", typeof(VideoPlayer));
                InstallDiagHook(typeof(MyAudioManager), "MonitorLoadedSounds", Type.EmptyTypes,
                    (MonitorDelegate)DiagMonitor, ref diagMonitorHook, out diagMonitorOriginal);
                InstallDiagHook(typeof(NoesisMediaPlayer), "OnMediaEnded", new[] { typeof(VideoPlayer) },
                    (PlayerEndDelegate)DiagPlayerEnd, ref diagPlayerEndHook, out diagPlayerEndOriginal);
                InstallDiagHook(typeof(MediaElement), "OnMediaEnded", Type.EmptyTypes,
                    (MediaEndDelegate)DiagMediaEnd, ref diagMediaEndHook, out diagMediaEndOriginal);
                Application.onBeforeRender += DiagBeforeRender;
                Emit(null, "READY", "hooks=audio-monitor,player-end,media-end observer=onBeforeRender ui=pending-first-render normalDiagnosticSwitch=ignored");
            });
        }

        private static FieldInfo Field(Type type, string name, Type expected)
        {
            var field = type.GetField(name, DiagMembers);
            if (field == null || field.FieldType != expected) throw new MissingFieldException(type.FullName, name);
            return field;
        }

        private static void InstallDiagHook<T>(Type type, string name, Type[] parameters, T callback,
            ref Hook published, out T original) where T : Delegate
        {
            var method = type.GetMethod(name, DiagMembers, null, parameters, null);
            if (method == null || method.ReturnType != typeof(void)) throw new MissingMethodException(type.FullName, name);
            var candidate = new Hook(method, callback, new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.NotificationDiagnosis." + name });
            try { original = candidate.GenerateTrampoline<T>(); candidate.Apply(); published = candidate; }
            catch { candidate.Dispose(); throw; } // Never dispose a published hook.
        }

        private static bool OnUnityThread => Volatile.Read(ref diagUnityThread) == Thread.CurrentThread.ManagedThreadId;
        private static long Clock => instance == null ? Stopwatch.GetTimestamp() : instance.timestamp();
        private static double Age(Observation item) => (Clock - item.Started) * 1000d / Stopwatch.Frequency;
        private static long Id(object value) => value == null ? 0 : diagObjects.GetValue(value, _ => new ObjectIdentity()).Value;
        private static string Identity(object value)
        {
            string result = "m" + Id(value);
            if (value is BaseComponent component)
                result += OnUnityThread ? "/n0x" + BaseComponent.getCPtr(component).Handle.ToInt64().ToString("X") : "/n=unread-off-thread";
            return result;
        }
        private static void Emit(Observation item, string stage, string details)
        {
            Shared.DevDiag.Log(() => "notification t=" + Thread.CurrentThread.ManagedThreadId +
                " tick=" + Clock + " v=" + (item?.Id ?? 0) + " speech=" + Interlocked.Read(ref diagSpeechId) +
                " boundSpeech=" + (item?.Speech ?? 0) + " ageMs=" + (item == null ? "-" : Age(item).ToString("F3", CultureInfo.InvariantCulture)) +
                " stage=" + stage + " " + details);
        }
        private static void Safe(Action action, Observation item = null)
        {
            try { lock (diagLock) action(); }
            catch (Exception ex)
            {
                int count = Interlocked.Increment(ref diagErrors);
                if (item != null) item.Errors++;
                if (count <= 8) Shared.DevDiag.Log(() => "notification DIAG_ERROR count=" + count + " thread=" + Thread.CurrentThread.ManagedThreadId + " " + ex);
            }
        }

        static partial void DiagRequest(SFXManager self, string name, bool loop, bool wait, Uri before)
        {
            Safe(() =>
            {
                var item = new Observation { Id = ++diagVideoId, Speech = diagSpeechId, Started = Clock,
                    Sfx = self, Source = self?.requestBinkPlaybackURI, Gui = diagGui, Monitor = diagMonitor,
                    Render = diagRender, Skipped = diagSkipped, Replaced = diagReplaced, Failed = diagGuiFailed };
                item.SourceKey = MediaSourceKey(item.Source);
                Emit(item, "video-request", "file=" + name + " loop=" + loop + " wait=" + wait + " sfx=" + Identity(self));
                if (self == null || ReferenceEquals(before, item.Source))
                { Emit(item, "video-rejected", "reason=" + (self == null ? "null-sfx" : "request-uri-reference-unchanged")); return; }
                Emit(item, "video-accepted", "uri=" + item.Source + " nativeWait=" + self.binkWaitForSpeech + " state=" + self.requestBinkPlayState +
                    " featureEnabled=" + (instance?.Enabled ?? false) + " viewModelLoaded=" + MainViewModel.viewModelLoaded + " loop=" + loop);
                // Keep a bounded history, including replaced requests, until their 25-second evidence window.
                if (diagVideos.Count == 64) { Summarize(diagVideos[0], "capacity-eviction-gap"); diagVideos.RemoveAt(0); }
                diagVideos.Add(item);
            });
        }

        partial void DiagAttach(Playback current)
        {
            Safe(() =>
            {
                for (int i = diagVideos.Count - 1; i >= 0; --i)
                    if (ReferenceEquals(diagVideos[i].Sfx, current.Sfx) && diagVideos[i].Owner == null)
                    {
                        var item = diagVideos[i]; item.Owner = current; item.Media = current.Media; item.Source = current.Source;
                        item.SourceKey = current.SourceKey;
                        item.Started = current.Started; // Report the production deadline, not time spent logging acceptance.
                        Emit(item, "feature-owner", "media=" + Identity(item.Media) + " hud=" + Identity(current.Hud) + " waiting=" + current.WaitingForSpeech);
                        return;
                    }
                Emit(null, "DIAG_GAP", "feature-owner-without-video-request");
            });
        }

        internal static void ObserveSpeech(MyAudioManager audio, long generation, string stage, string detail)
        {
            Safe(() =>
            {
                if (stage == "speech-accepted")
                {
                    long previous = diagSpeechId; diagSpeechId = generation;
                    Emit(null, "speech-switch", "previous=" + previous + " audio=" + Identity(audio));
                }
                Emit(null, stage, "requestSpeech=" + generation + " audio=" + Identity(audio) + " detail=" + detail);
                if (OnUnityThread) AudioSnapshot(audio, stage);
            });
        }

        private static void DiagMonitor(MyAudioManager self)
        {
            Safe(() => { diagMonitor++; if (diagMonitor == 1) Emit(null, "MONITOR_READY", "audio=" + Identity(self)); if (OnUnityThread && diagVideos.Count > 0) AudioSnapshot(self, "monitor-before"); });
            try { diagMonitorOriginal(self); }
            finally { Safe(() => { if (OnUnityThread && diagVideos.Count > 0) AudioSnapshot(self, "monitor-after"); }); }
        }
        private static void DiagPlayerEnd(NoesisMediaPlayer self, VideoPlayer player)
        {
            Safe(() => EndCallback("player-end-enter", self, player));
            try { diagPlayerEndOriginal(self, player); }
            finally { Safe(() => EndCallback("player-end-return", self, player)); }
        }
        private static void DiagMediaEnd(MediaElement self)
        {
            Safe(() => EndCallback("media-end-enter", self, null));
            try { diagMediaEndOriginal(self); }
            finally { Safe(() => EndCallback("media-end-return", self, null)); }
        }
        private static void EndCallback(string stage, object sender, VideoPlayer video)
        {
            bool matched = false;
            foreach (var item in diagVideos)
            {
                // Resolve the chain at callback time as well: a short video may end before the first sample.
                ResolvePlayer(item);
                if (!SameIdentity(sender, item.Player) && !SameIdentity(sender, item.Media) &&
                    !(video != null && ReferenceEquals(video, item.Video))) continue;
                matched = true;
                if (stage == "player-end-enter") item.PlayerEnds++;
                if (stage == "media-end-enter") item.MediaEnds++;
                Emit(item, stage, "sender=" + Identity(sender) + " video=" + Identity(video));
            }
            if (!matched) Emit(null, stage, "UNMATCHED sender=" + Identity(sender) + " video=" + Identity(video));
        }
        private static bool SameIdentity(object left, object right)
        {
            if (left == null || right == null) return false;
            if (ReferenceEquals(left, right)) return true;
            if (!OnUnityThread || !(left is BaseComponent a) || !(right is BaseComponent b)) return false;
            IntPtr pointer = BaseComponent.getCPtr(a).Handle;
            return pointer != IntPtr.Zero && pointer == BaseComponent.getCPtr(b).Handle;
        }

        static partial void DiagEnd(MainHUD hud, object sender, string stage)
        {
            Safe(() =>
            {
                var current = instance?.playback;
                Observation item = Find(current);
                if (item != null)
                {
                    if (stage == "hud-end-enter") item.HudEnds++;
                    if (stage.StartsWith("reject-", StringComparison.Ordinal)) item.Rejected++;
                }
                string conditions = "ui=unread-off-thread";
                if (OnUnityThread && current != null)
                    conditions = "sfx=" + ReferenceEquals(SFXManager.instance, current.Sfx) +
                        " request=" + ReferenceEquals(current.Sfx.requestBinkPlaybackURI, current.Source) +
                        " hud=" + ReferenceEquals(hud, current.Hud) + " media=" + ReferenceEquals(hud?.RefRadarME, current.Media) +
                        " sender=" + ReferenceEquals(sender, current.Media) + " uri=" + current.HasMediaSource(current.Media.Source) +
                        " rawUriEquals=" + (current.Media.Source == current.Source) +
                        " currentHud=" + (MainViewModel.viewModelLoaded && ReferenceEquals(MainViewModel.Instance.HUDRoot, hud)) +
                        " expectedMedia=" + Identity(current.Media) + " actualMedia=" + Identity(hud?.RefRadarME);
                Emit(item, stage, "sender=" + Identity(sender) + " hud=" + Identity(hud) + " checks[" + conditions + "] false=failed-condition");
            });
        }
        private static Observation Find(Playback current)
        {
            if (current != null) for (int i = diagVideos.Count - 1; i >= 0; --i)
                if (ReferenceEquals(diagVideos[i].Owner, current)) return diagVideos[i];
            return null;
        }
        partial void DiagState(Playback current, string stage)
        {
            Safe(() =>
            {
                var item = Find(current);
                if (item == null) return;
                if (stage == "close-attempt") item.CloseAttempts++;
                if (stage == "close-return") item.CloseReturns++;
                Emit(item, stage, "held=" + current.Held + " waiting=" + current.WaitingForSpeech +
                    " invalidated=" + current.Invalidated + " deadlineExceeded=" + (Age(item) > 20000));
                if (OnUnityThread) Sample(item, true);
            });
        }
        static partial void DiagGui(PresentationPostEventArgs args)
        {
            Safe(() =>
            {
                diagGui++;
                if (args.WasSkipped) diagSkipped++;
                if (args.WasReplaced) diagReplaced++;
                if (args.CompletionException != null) diagGuiFailed++;
                string state = "skipped=" + args.WasSkipped + " replaced=" + args.WasReplaced + " exception=" + args.CompletionException;
                if (!args.OriginalCompleted && state != diagLastGui) Emit(Find(instance?.playback), "gui-incomplete", state);
                diagLastGui = state;
            });
        }

        private static void DiagBeforeRender()
        {
            Safe(() =>
            {
                if (diagUnityThread == 0)
                {
                    diagUnityThread = Thread.CurrentThread.ManagedThreadId;
                    Emit(null, "RENDER_READY", "unityThread=confirmed hooks=" + (diagMonitorHook != null && diagPlayerEndHook != null && diagMediaEndHook != null));
                }
                if (!OnUnityThread) { Emit(null, "DIAG_GAP", "render-thread-changed; UI read refused"); return; }
                if (diagFrame == Time.frameCount) return;
                diagFrame = Time.frameCount; diagRender++;
                if (diagVideos.Count > 0) Safe(() => AudioSnapshot(MyAudioManager.Instance, "render"));
                for (int i = diagVideos.Count - 1; i >= 0; --i)
                {
                    var item = diagVideos[i];
                    if (!item.Summary) Safe(() => Sample(item, false), item);
                    if (!item.Summary && (item.Closed || Age(item) >= 25000)) Summarize(item, item.Closed ? "closed" : "25s");
                    if (Age(item) >= 25000 && item.Summary) diagVideos.RemoveAt(i);
                }
            });
        }

        private static string AudioSnapshot(MyAudioManager audio, string stage)
        {
            if (audio == null) return "audio=null";
            int mode = (int)diagMode.GetValue(audio);
            bool paused = (bool)diagPaused.GetValue(audio);
            var source = (AudioSource)diagSource.GetValue(audio);
            var clip = (AudioClip)diagClip.GetValue(audio);
            string state = "audio=" + Identity(audio) + " speechMode1=" + mode + " pause=" + paused +
                " source=" + Identity(source) + " actualPlaying=" + (source != null && source.isPlaying) +
                " active=" + (source != null && source.gameObject.activeInHierarchy) + " volume=" + (source == null ? "?" : source.volume.ToString("R", CultureInfo.InvariantCulture)) +
                " clip=" + Identity(clip) + " load=" + (clip == null ? "none" : clip.loadState.ToString()) +
                " clipSeconds=" + (clip == null ? "?" : clip.length.ToString("R", CultureInfo.InvariantCulture));
            if (state != diagLastAudio) { diagLastAudio = state; Emit(null, "audio-state/" + stage, state); }
            return state;
        }
        private static void ResolvePlayer(Observation item)
        {
            if (!OnUnityThread) return;
            if (item.Media == null && MainViewModel.viewModelLoaded && MainViewModel.Instance?.HUDmain != null &&
                ReferenceEquals(item.Sfx?.requestBinkPlaybackURI, item.Source)) item.Media = MainViewModel.Instance.HUDRoot?.RefRadarME;
            if (item.Media == null) return;
            object player = diagMediaPlayer.GetValue(item.Media);
            // Once replaced, retain the old decoder identity to recognize delayed callbacks.
            if (item.Player == null || MediaSourcesMatch(item.Media.Source, item.Source, item.SourceKey))
            {
                item.Player = player;
                item.Video = player is NoesisMediaPlayer ? (VideoPlayer)diagVideoPlayer.GetValue(player) : null;
            }
        }
        private static void Sample(Observation item, bool force)
        {
            if (!OnUnityThread) return;
            ResolvePlayer(item);
            var owner = item.Owner;
            var main = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
            var video = item.Video;
            bool last = video != null && video.isPrepared && video.frameCount > 0 && video.frame >= 0 && (ulong)video.frame >= video.frameCount - 1;
            var audio = owner?.Audio ?? MyAudioManager.Instance;
            var audioSource = audio == null ? null : (AudioSource)diagSource.GetValue(audio);
            bool busySilent = audio != null && (int)diagMode.GetValue(audio) != 0 &&
                (audioSource == null || !audioSource.isPlaying);
            item.SawSilentBusy |= busySilent; item.SawLastFrame |= last;
            if (!item.SawExpiry && Age(item) > 20000) { item.SawExpiry = true; Emit(item, "deadline-expired", "held=" + (owner?.Held ?? false) + " waiting=" + (owner?.WaitingForSpeech ?? false)); }
            // Completion must be observed in the media AND the owning global flags, not assumed from a return.
            item.Closed = item.Media != null && item.Media.Source == null && item.Media.Opacity == 0 &&
                (!ReferenceEquals(item.Sfx?.requestBinkPlaybackURI, item.Source) || !item.Sfx.binkIsPlaying && item.Sfx.requestBinkPlayState == 0);
            string state = "held=" + (owner?.Held ?? false) + " waiting=" + (owner?.WaitingForSpeech ?? false) +
                " invalidated=" + (owner?.Invalidated ?? false) + " state=" + item.Sfx?.requestBinkPlayState +
                " binkPlaying=" + item.Sfx?.binkIsPlaying + " currentRequest=" + ReferenceEquals(item.Sfx?.requestBinkPlaybackURI, item.Source) +
                " enabled=" + (instance?.Enabled ?? false) + " mainReady=" + (main?.HUDmain != null) +
                " currentMain=" + (owner != null && ReferenceEquals(main, owner.Main)) +
                " currentHud=" + (owner != null && ReferenceEquals(main?.HUDRoot, owner.Hud)) +
                " currentMedia=" + SameIdentity(main?.HUDRoot?.RefRadarME, item.Media) +
                " currentOwner=" + ReferenceEquals(instance?.playback, owner) + " media=" + Identity(item.Media) + " player=" + Identity(item.Player) +
                " video=" + Identity(video) + " uri=" + item.Media?.Source + " expectedUri=" + item.Source +
                " opacity=" + item.Media?.Opacity + " videoPlaying=" + (video != null && video.isPlaying) +
                " videoPaused=" + (video != null && video.isPaused) + " prepared=" + (video != null && video.isPrepared) +
                " lastFrame=" + last + " silentBusy=" + busySilent + " expired=" + (Age(item) > 20000) + " closed=" + item.Closed;
            if (force || state != item.State || Clock - item.LastFull >= Stopwatch.Frequency)
            {
                bool changed = state != item.State; item.State = state;
                string audioState = AudioSnapshot(audio, "sample");
                Emit(item, changed ? "state-change" : "status", state + " " + audioState +
                    " frame=" + (video == null ? "?" : video.frame.ToString()) + "/" + (video == null ? "?" : video.frameCount.ToString()) +
                    " position=" + (video == null ? "?" : video.time.ToString("R", CultureInfo.InvariantCulture)) +
                    " duration=" + (video == null ? "?" : video.length.ToString("R", CultureInfo.InvariantCulture)) +
                    " mediaPosition=" + item.Media?.Position + " mediaDuration=" + item.Media?.NaturalDuration +
                    " gui=" + (diagGui - item.Gui) + " monitor=" + (diagMonitor - item.Monitor) + " renders=" + (diagRender - item.Render));
                item.LastFull = Clock;
            }
        }
        private static void Summarize(Observation item, string reason)
        {
            item.Summary = true;
            Emit(item, "SUMMARY", "reason=" + reason + " closed=" + item.Closed + " playerEnds=" + item.PlayerEnds +
                " mediaEnds=" + item.MediaEnds + " hudEnds=" + item.HudEnds + " rejected=" + item.Rejected +
                " closeAttempts=" + item.CloseAttempts + " closeReturns=" + item.CloseReturns +
                " gui=" + (diagGui - item.Gui) + " skipped=" + (diagSkipped - item.Skipped) + " replaced=" + (diagReplaced - item.Replaced) +
                " guiErrors=" + (diagGuiFailed - item.Failed) + " monitor=" + (diagMonitor - item.Monitor) + " renders=" + (diagRender - item.Render) +
                " lastFrameObserved=" + item.SawLastFrame + " silentButBusyObserved=" + item.SawSilentBusy + " deadlinePassed=" + (Age(item) > 20000) +
                " diagErrors=" + diagErrors + " sampleErrors=" + item.Errors + " gaps[owner=" + (item.Owner == null) + " video=" + (item.Video == null) +
                " playerEnd=" + (item.PlayerEnds == 0) + " mediaEnd=" + (item.MediaEnds == 0) + " hudEnd=" + (item.HudEnds == 0) +
                " gui=" + (diagGui == item.Gui) + " monitor=" + (diagMonitor == item.Monitor) +
                "] evidenceOnly=true; missing callbacks are gaps, not proof of their cause");
        }
    }
}
#endif

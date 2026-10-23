// Feature: Own accepted channel-1 speech identities independently of native notification skipping.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed partial class NotificationSpeechPlayback
    {
        private delegate void LoadSpeechClipDelegate(MyAudioManager self, int channel, string folder,
            string soundName, bool unitsSpeech, bool ignorePauseState);
        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static NotificationSpeechPlayback instance;
        private readonly ManualLogSource log;
        private readonly Hook loadSpeechClipHook;
        private readonly LoadSpeechClipDelegate loadSpeechClipOriginal;
        private readonly FieldInfo speechSource1Field, speechClip1Field, speechMode1Field;
        private readonly FieldInfo speechPausedField, ignoreSpeechMutingField;
        private readonly MethodInfo loadClipByPathMethod;
        private Func<string, bool> notificationAudioLoad;
        private MyAudioManager currentAudio;
        private long speechChannel1Generation;

        partial void DiagSpeech(MyAudioManager audio, long generation, string stage, string detail);

        // Initialized from the validated Unity-thread LibraryLoaded path; the static owner and
        // permanent delegate survive destruction of the startup BepInEx component.
        internal static NotificationSpeechPlayback Get(ManualLogSource log)
        {
            return instance ?? (instance = new NotificationSpeechPlayback(log));
        }

        private NotificationSpeechPlayback(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            speechSource1Field = FindField("speechSource1", typeof(AudioSource));
            speechClip1Field = FindField("speechClip1", typeof(AudioClip));
            speechMode1Field = FindField("speechMode1", typeof(int));
            speechPausedField = FindField("speechPaused", typeof(bool));
            ignoreSpeechMutingField = FindField("ignoreSpeechMuting", typeof(bool));
            loadClipByPathMethod = FindLoadClipByPathMethod();
            Hook candidate = new Hook(FindLoadSpeechClipMethod(), (LoadSpeechClipDelegate)LoadSpeechClipHook,
                new HookConfig { ManualApply = true, ID = "BugfixesAndQoL.NotificationSpeech" });
            try
            {
                loadSpeechClipOriginal = candidate.GenerateTrampoline<LoadSpeechClipDelegate>();
                candidate.Apply();
                loadSpeechClipHook = candidate;
            }
            catch
            {
                // This candidate was never published to a consumer.
                candidate.Dispose();
                throw;
            }
        }

        internal long Capture(MyAudioManager audio) =>
            ReferenceEquals(audio, Volatile.Read(ref currentAudio)) && audio != null && audio.isSpeechPlaying(1)
                ? Volatile.Read(ref speechChannel1Generation) : 0;

        internal bool IsCurrent(MyAudioManager audio, long generation) => generation != 0 &&
            generation == Volatile.Read(ref speechChannel1Generation) &&
            ReferenceEquals(audio, Volatile.Read(ref currentAudio));

        internal void Invalidate()
        {
            Interlocked.Increment(ref speechChannel1Generation);
            DiagSpeech(currentAudio, Volatile.Read(ref speechChannel1Generation), "speech-invalidated", "pending loads invalidated");
        }

        // Publish only after the optional native skip feature has initialized successfully.
        internal void SetNotificationClassifier(Func<string, bool> classifier) => notificationAudioLoad = classifier;

        private void LoadSpeechClipHook(MyAudioManager self, int channel, string folder,
            string soundName, bool unitsSpeech, bool ignorePauseState)
        {
            if (channel != 1)
            {
                loadSpeechClipOriginal(self, channel, folder, soundName, unitsSpeech, ignorePauseState);
                return;
            }
            // Vanilla invokes this overload only after accepting PlaySpeech. Count every accepted
            // request, including unrelated speech and repeated requests for the same file.
            long generation = Interlocked.Increment(ref speechChannel1Generation);
            Volatile.Write(ref currentAudio, self);
            DiagSpeech(self, generation, "speech-accepted", folder + "/" + soundName);
            if (notificationAudioLoad == null || !notificationAudioLoad(soundName))
            {
                loadSpeechClipOriginal(self, channel, folder, soundName, unitsSpeech, ignorePauseState);
                return;
            }
            LoadSpeechChannel1Async(self, folder, soundName, unitsSpeech, ignorePauseState, generation);
        }

        internal void StopSpeechChannel1(MyAudioManager audio)
        {
            if (audio == null)
                return;

            Invalidate();
            AudioSource source = (AudioSource)speechSource1Field.GetValue(audio);
            AudioClip clip = (AudioClip)speechClip1Field.GetValue(audio);
            source?.Stop();
            if (clip != null)
                clip.UnloadAudioData();
            speechClip1Field.SetValue(audio, null);
            speechMode1Field.SetValue(audio, 0);
            DiagSpeech(audio, Volatile.Read(ref speechChannel1Generation), "speech-stopped", "channel-1 skip");

            bool otherSpeechPlaying = audio.isSpeechPlaying(2) || audio.isSpeechPlaying(3);
            bool ignoreSpeechMuting = (bool)ignoreSpeechMutingField.GetValue(audio);
            audio.setMusicFadedState(
                otherSpeechPlaying &&
                ConfigSettings.Settings_ReduceMusicVolumeForSpeech &&
                !ignoreSpeechMuting);
        }

        private async void LoadSpeechChannel1Async(
            MyAudioManager audio,
            string folder,
            string soundName,
            bool unitsSpeech,
            bool ignorePauseState,
            long generation)
        {
            AudioClip loadedClip = null;
            try
            {
                // A null folder takes Vanilla's Path.Combine branch, which rejects it like the original loader.
                string path = (folder == "*" || folder != null && folder.StartsWith("_SE_", StringComparison.OrdinalIgnoreCase))
                    ? soundName
                    : (ConfigSettings.Settings_EnglishSpeech || FatControler.UsesEnglishSpeechFolder()
                        ? Path.Combine(Application.streamingAssetsPath, "EnglishSpeech", folder, soundName)
                        : Path.Combine(Application.dataPath, "Assets", "GUI", "Speech", folder, soundName));
                Task<AudioClip> loadTask =
                    (Task<AudioClip>)loadClipByPathMethod.Invoke(audio, new object[] { path });
                DiagSpeech(audio, generation, "audio-load-pending", path);
                loadedClip = await loadTask;
                DiagSpeech(audio, generation, "audio-load-return", loadedClip == null ? "null" : "clip");

                if (!IsCurrent(audio, generation))
                {
                    DiagSpeech(audio, generation, "audio-load-stale", "discard");
                    loadedClip?.UnloadAudioData();
                    return;
                }

                if (loadedClip == null)
                    throw new InvalidOperationException("Channel-1 speech loading returned no audio clip.");
                speechClip1Field.SetValue(audio, loadedClip);
                AudioSource source = (AudioSource)speechSource1Field.GetValue(audio);
                float volume = (unitsSpeech
                        ? ConfigSettings.Settings_UnitSpeechVolume
                        : ConfigSettings.Settings_SpeechVolume) *
                    MyAudioManager.GetMasterVolume();
                source.volume = volume;
                source.PlayOneShot(loadedClip);
                speechMode1Field.SetValue(audio, 2);
                if ((bool)speechPausedField.GetValue(audio) && !ignorePauseState)
                    source.Pause();
                else
                    audio.setMusicFadedState(
                        ConfigSettings.Settings_ReduceMusicVolumeForSpeech &&
                        !(bool)ignoreSpeechMutingField.GetValue(audio));
                DiagSpeech(audio, generation, "audio-started", "mode=2");
            }
            catch (Exception ex)
            {
                DiagSpeech(audio, generation, "audio-load-failed", ex.ToString());
                if (IsCurrent(audio, generation))
                {
                    loadedClip?.UnloadAudioData();
                    speechClip1Field.SetValue(audio, null);
                    speechMode1Field.SetValue(audio, 0);
                }
                Shared.DebugLogHelper.LogErrorLimited(log, "NotificationSpeechPlayback.Load", $"Bugfixes and QoL channel-1 speech loading failed: {ex}");
            }
        }

        private static MethodInfo FindLoadSpeechClipMethod()
        {
            MethodInfo method = typeof(MyAudioManager).GetMethod(
                "LoadClip",
                InstanceMembers,
                null,
                new[] { typeof(int), typeof(string), typeof(string), typeof(bool), typeof(bool) },
                null);
            return method ?? throw new MissingMethodException(typeof(MyAudioManager).FullName, "LoadClip(int,...)");
        }

        private static MethodInfo FindLoadClipByPathMethod()
        {
            MethodInfo method = typeof(MyAudioManager).GetMethod(
                "LoadClip",
                InstanceMembers,
                null,
                new[] { typeof(string) },
                null);
            if (method == null || method.ReturnType != typeof(Task<AudioClip>))
                throw new MissingMethodException(typeof(MyAudioManager).FullName, "LoadClip(string)");
            return method;
        }

        private static FieldInfo FindField(string name, Type expectedType)
        {
            FieldInfo field = typeof(MyAudioManager).GetField(name, InstanceMembers);
            if (field == null || field.FieldType != expectedType)
                throw new MissingFieldException(typeof(MyAudioManager).FullName, name);
            return field;
        }

    }
}

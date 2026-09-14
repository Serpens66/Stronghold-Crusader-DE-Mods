// Feature: Fully finish a queued AI/event notification when its minimap video is right-clicked.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using RedBird.Core.Memory;
using SHCDESE.GameGlobals;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class NotificationSkipFeature
    {
        private delegate void LoadSpeechClipDelegate(
            MyAudioManager self,
            int channel,
            string folder,
            string soundName,
            bool unitsSpeech,
            bool ignorePauseState);

        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IntPtr messageManager;
        private readonly Hook loadSpeechClipHook;
        private readonly LoadSpeechClipDelegate loadSpeechClipOriginal;
        private readonly FieldInfo speechSource1Field;
        private readonly FieldInfo speechClip1Field;
        private readonly FieldInfo speechMode1Field;
        private readonly FieldInfo speechPausedField;
        private readonly FieldInfo ignoreSpeechMutingField;
        private readonly MethodInfo loadClipByPathMethod;
        private int speechChannel1Generation;

        public NotificationSkipFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion nativeRegion)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (nativeRegion == null)
                throw new ArgumentNullException(nameof(nativeRegion));
            NotificationQueueNativeContract.Validate(nativeRegion.Span);

            ulong messageManagerAddress = GameGlobalsManager.Instance.MessageManagerVA;
            if (messageManagerAddress == 0 || messageManagerAddress > long.MaxValue)
                throw new InvalidOperationException("The Script Extender did not expose a valid MessageManager address.");
            messageManager = new IntPtr(unchecked((long)messageManagerAddress));

            Hook pendingLoadHook = null;
            try
            {
                speechSource1Field = FindField("speechSource1", typeof(AudioSource));
                speechClip1Field = FindField("speechClip1", typeof(AudioClip));
                speechMode1Field = FindField("speechMode1", typeof(int));
                speechPausedField = FindField("speechPaused", typeof(bool));
                ignoreSpeechMutingField = FindField("ignoreSpeechMuting", typeof(bool));
                loadClipByPathMethod = FindLoadClipByPathMethod();

                pendingLoadHook = new Hook(FindLoadSpeechClipMethod(), (LoadSpeechClipDelegate)LoadSpeechClipHook);
                LoadSpeechClipDelegate pendingLoadOriginal =
                    pendingLoadHook.GenerateTrampoline<LoadSpeechClipDelegate>();

                loadSpeechClipOriginal = pendingLoadOriginal;
                loadSpeechClipHook = pendingLoadHook;
                NotificationSkipBehavior.Configure(this);
            }
            catch (Exception ex)
            {
                RollbackFailedInitialization(pendingLoadHook);
                throw new InvalidOperationException(
                    "The notification channel-1 speech hook could not be initialized.",
                    ex);
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL complete notification-skip hooks installed for the process lifetime.");
        }

        internal void CompleteFromRadarVideoRightClick(Noesis.MouseButtonEventArgs args)
        {
            try
            {
                if (!NotificationSkipPolicy.ShouldCompleteOnRightClick(
                        ShouldArmNotificationSkip(),
                        true,
                        true))
                {
                    return;
                }

                args.Handled = true;
                CompleteCurrentNotification();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL complete notification skip failed after activation; completion may be partial: {ex}");
            }
        }

        private bool ShouldArmNotificationSkip()
        {
            SFXManager sfx = SFXManager.instance;
            bool enabled = settings.EnableMod &&
                settings.EnableClientFeatures &&
                settings.EnableCompleteNotificationSkipOnClick;
            bool queueActive = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.IsQueueActiveOffset) != 0;
            bool hasVideo = Marshal.ReadByte(
                messageManager,
                NotificationQueueNativeContract.ImmediateVideoPathOffset) != 0;
            bool videoVisible = MainViewModel.viewModelLoaded &&
                MainViewModel.Instance?.HUDRoot?.RefRadarME != null &&
                MainViewModel.Instance.HUDRoot.RefRadarME.Opacity != 0f;
            bool videoPlaying = sfx != null &&
                (sfx.binkIsPlaying || sfx.requestBinkPlayState != 0);

            return NotificationSkipPolicy.ShouldArm(
                enabled,
                queueActive,
                hasVideo,
                videoVisible,
                videoPlaying);
        }

        private void CompleteCurrentNotification()
        {
            Exception firstFailure = null;
            try
            {
                MainViewModel.Instance.HUDRoot.RadarME_Ended();
            }
            catch (Exception ex)
            {
                firstFailure = ex;
            }

            try
            {
                StopSpeechChannel1(MyAudioManager.Instance);
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }

            try
            {
                Marshal.WriteInt32(
                    messageManager,
                    NotificationQueueNativeContract.ImmediateCommandIdOffset,
                    0);
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }

            if (firstFailure != null)
            {
                throw new InvalidOperationException(
                    "At least one notification completion step failed.",
                    firstFailure);
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL completed the right-clicked notification; Vanilla will promote the next queued message.");
        }

        private void StopSpeechChannel1(MyAudioManager audio)
        {
            if (audio == null)
                return;

            Interlocked.Increment(ref speechChannel1Generation);
            AudioSource source = (AudioSource)speechSource1Field.GetValue(audio);
            AudioClip clip = (AudioClip)speechClip1Field.GetValue(audio);
            source?.Stop();
            if (clip != null)
                clip.UnloadAudioData();
            speechClip1Field.SetValue(audio, null);
            speechMode1Field.SetValue(audio, 0);

            bool otherSpeechPlaying = audio.isSpeechPlaying(2) || audio.isSpeechPlaying(3);
            bool ignoreSpeechMuting = (bool)ignoreSpeechMutingField.GetValue(audio);
            audio.setMusicFadedState(
                otherSpeechPlaying &&
                ConfigSettings.Settings_ReduceMusicVolumeForSpeech &&
                !ignoreSpeechMuting);
        }

        private void LoadSpeechClipHook(
            MyAudioManager self,
            int channel,
            string folder,
            string soundName,
            bool unitsSpeech,
            bool ignorePauseState)
        {
            if (channel != 1 || !IsActiveVideoNotificationEnabled())
            {
                loadSpeechClipOriginal(self, channel, folder, soundName, unitsSpeech, ignorePauseState);
                return;
            }

            int generation = Interlocked.Increment(ref speechChannel1Generation);
            LoadSpeechChannel1Async(
                self,
                folder,
                soundName,
                unitsSpeech,
                ignorePauseState,
                generation);
        }

        private bool IsActiveVideoNotificationEnabled()
        {
            if (!settings.EnableMod ||
                !settings.EnableClientFeatures ||
                !settings.EnableCompleteNotificationSkipOnClick)
            {
                return false;
            }

            try
            {
                return Marshal.ReadInt32(
                           messageManager,
                           NotificationQueueNativeContract.IsQueueActiveOffset) != 0 &&
                       Marshal.ReadByte(
                           messageManager,
                           NotificationQueueNativeContract.ImmediateVideoPathOffset) != 0;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not classify channel-1 speech; Vanilla loading continues: {ex}");
                return false;
            }
        }

        private async void LoadSpeechChannel1Async(
            MyAudioManager audio,
            string folder,
            string soundName,
            bool unitsSpeech,
            bool ignorePauseState,
            int generation)
        {
            AudioClip loadedClip = null;
            try
            {
                string path = folder == "*"
                    ? soundName
                    : (ConfigSettings.Settings_EnglishSpeech || FatControler.UsesEnglishSpeechFolder()
                        ? Path.Combine(Application.streamingAssetsPath, "EnglishSpeech", folder, soundName)
                        : Path.Combine(Application.dataPath, "Assets", "GUI", "Speech", folder, soundName));
                Task<AudioClip> loadTask =
                    (Task<AudioClip>)loadClipByPathMethod.Invoke(audio, new object[] { path });
                loadedClip = await loadTask;

                if (generation != Volatile.Read(ref speechChannel1Generation))
                {
                    loadedClip?.UnloadAudioData();
                    return;
                }

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
            }
            catch (Exception ex)
            {
                if (generation == Volatile.Read(ref speechChannel1Generation))
                {
                    loadedClip?.UnloadAudioData();
                    speechClip1Field.SetValue(audio, null);
                    speechMode1Field.SetValue(audio, 0);
                }
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL channel-1 speech loading failed: {ex}");
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

        private static void RollbackFailedInitialization(Hook candidate)
        {
            if (candidate == null)
                return;
            candidate.Undo();
            candidate.Dispose();
        }
    }
}

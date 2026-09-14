// Feature: Fully finish a queued AI/event notification when its minimap video is right-clicked.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.GameGlobals;
using System;
using System.Diagnostics;
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
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void NotificationUpdateDelegate(IntPtr manager);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate ulong NotificationFinalizeDelegate(IntPtr manager);

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
        private readonly IntPtr notificationPendingFlag;
        private readonly Hook loadSpeechClipHook;
        private readonly LoadSpeechClipDelegate loadSpeechClipOriginal;
        private readonly FieldInfo speechSource1Field;
        private readonly FieldInfo speechClip1Field;
        private readonly FieldInfo speechMode1Field;
        private readonly FieldInfo speechPausedField;
        private readonly FieldInfo ignoreSpeechMutingField;
        private readonly MethodInfo loadClipByPathMethod;
        private readonly DetourHandle<NotificationUpdateDelegate> notificationUpdateHook =
            new DetourHandle<NotificationUpdateDelegate>();
        private readonly HookTransaction notificationUpdateTransaction;
        private readonly NotificationFinalizeDelegate finalizeNotification;
        private PendingSkipRequest pendingSkipRequest;
        private int skipRequestGeneration;
        private int speechChannel1Generation;

        public NotificationSkipFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion nativeRegion,
            IntPtr libraryHandle)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (nativeRegion == null)
                throw new ArgumentNullException(nameof(nativeRegion));
            if (libraryHandle == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(libraryHandle));

            NotificationQueueNativeResolution nativeContract =
                NotificationQueueNativeContract.Validate(nativeRegion.Span);

            ulong messageManagerAddress = GameGlobalsManager.Instance.MessageManagerVA;
            if (messageManagerAddress == 0 || messageManagerAddress > long.MaxValue)
                throw new InvalidOperationException("The Script Extender did not expose a valid MessageManager address.");
            messageManager = new IntPtr(unchecked((long)messageManagerAddress));

            Hook pendingLoadHook = null;
            HookTransaction pendingNativeTransaction = null;
            string initializationStage = "managed message-bar contract";
            try
            {
                ValidateMessageBarContract();

                initializationStage = "channel-1 speech contract";
                speechSource1Field = FindField("speechSource1", typeof(AudioSource));
                speechClip1Field = FindField("speechClip1", typeof(AudioClip));
                speechMode1Field = FindField("speechMode1", typeof(int));
                speechPausedField = FindField("speechPaused", typeof(bool));
                ignoreSpeechMutingField = FindField("ignoreSpeechMuting", typeof(bool));
                loadClipByPathMethod = FindLoadClipByPathMethod();

                initializationStage = "channel-1 speech hook";
                pendingLoadHook = new Hook(FindLoadSpeechClipMethod(), (LoadSpeechClipDelegate)LoadSpeechClipHook);
                LoadSpeechClipDelegate pendingLoadOriginal =
                    pendingLoadHook.GenerateTrampoline<LoadSpeechClipDelegate>();

                loadSpeechClipOriginal = pendingLoadOriginal;
                loadSpeechClipHook = pendingLoadHook;

                ulong moduleBase = unchecked((ulong)libraryHandle.ToInt64());
                finalizeNotification = Marshal.GetDelegateForFunctionPointer<NotificationFinalizeDelegate>(
                    new IntPtr(unchecked((long)(moduleBase + (ulong)nativeContract.FinalizerRva))));
                notificationPendingFlag = new IntPtr(
                    unchecked((long)(moduleBase + (ulong)nativeContract.PendingFlagRva)));

                initializationStage = "native notification-update detour";
                pendingNativeTransaction = BugfixesHookInfrastructure.CreateOwnedTransaction(nativeRegion);
                pendingNativeTransaction.AddDetour(
                    notificationUpdateHook,
                    HookTarget.FromAddress(moduleBase + (ulong)nativeContract.UpdateRva),
                    UpdateNotificationQueue);
                CommitResult commitResult = pendingNativeTransaction.Commit();
                if (!commitResult.IsCompleteSuccess || !notificationUpdateHook.Success)
                    throw new InvalidOperationException("The native notification-update detour was not installed.");

                notificationUpdateTransaction = pendingNativeTransaction;
                initializationStage = "notification UI behavior publication";
                NotificationSkipBehavior.Configure(this);
            }
            catch (Exception ex)
            {
                RollbackFailedInitialization(pendingLoadHook, pendingNativeTransaction);
                throw new InvalidOperationException(
                    $"The notification {initializationStage} could not be initialized.",
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
            int messageId = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.ImmediateCommandIdOffset);
            int presentationId = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.ImmediatePresentationIdOffset);
            int queuedCount = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.QueuedCountOffset);
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
                HideMessageBar();
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }

            int generation = Interlocked.Increment(ref skipRequestGeneration);
            var request = new PendingSkipRequest(
                generation,
                messageId,
                presentationId,
                queuedCount,
                Stopwatch.GetTimestamp());
            try
            {
                Marshal.WriteInt32(
                    messageManager,
                    NotificationQueueNativeContract.ImmediateCommandIdOffset,
                    0);
                Interlocked.Exchange(ref pendingSkipRequest, request);
            }
            catch (Exception ex)
            {
                if (firstFailure == null)
                    firstFailure = ex;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Bugfixes and QoL requested right-click notification completion: " +
                $"messageId={messageId}, presentationId={presentationId}, " +
                $"queuedCount={queuedCount}, generation={generation}.");

            if (firstFailure != null)
            {
                throw new InvalidOperationException(
                    "At least one notification completion step failed.",
                    firstFailure);
            }

        }

        private void UpdateNotificationQueue(IntPtr manager)
        {
            PendingSkipRequest request = Volatile.Read(ref pendingSkipRequest);
            if (request == null)
            {
                notificationUpdateHook.Original(manager);
                return;
            }

            try
            {
                bool managerMatches = manager == messageManager;
                if (!managerMatches)
                {
                    if (Interlocked.CompareExchange(ref pendingSkipRequest, null, request) == request)
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Bugfixes and QoL discarded notification completion for an unexpected manager: " +
                            $"generation={request.Generation}.");
                    }
                    notificationUpdateHook.Original(manager);
                    return;
                }

                int queueActive = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.IsQueueActiveOffset);
                int messageId = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.ImmediateCommandIdOffset);
                int presentationId = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.ImmediatePresentationIdOffset);
                int queuedBefore = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.QueuedCountOffset);
                bool identityMatches = presentationId == request.PresentationId &&
                    (messageId == request.MessageId || messageId == 0);

                if (queueActive == 0 || !identityMatches)
                {
                    if (Interlocked.CompareExchange(ref pendingSkipRequest, null, request) == request)
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Bugfixes and QoL discarded stale notification completion: " +
                            $"generation={request.Generation}, active={queueActive}, " +
                            $"expectedMessageId={request.MessageId}, " +
                            $"messageId={messageId}, expectedPresentationId={request.PresentationId}, " +
                            $"presentationId={presentationId}.");
                    }
                    notificationUpdateHook.Original(manager);
                    return;
                }

                if (Interlocked.CompareExchange(ref pendingSkipRequest, null, request) != request)
                {
                    notificationUpdateHook.Original(manager);
                    return;
                }

                // This callback is the validated 0xFE570 update reached from DLL_RunTick.
                // Mirror Vanilla's immediately preceding pending-flag clear; a promoted
                // presentation sets it again in 0xFEE50. The output buffer stays valid here.
                Marshal.WriteByte(notificationPendingFlag, 0);
                ulong promoted = finalizeNotification(manager);
                int activeAfter = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.IsQueueActiveOffset);
                int messageAfter = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.ImmediateCommandIdOffset);
                int presentationAfter = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.ImmediatePresentationIdOffset);
                int queuedAfter = Marshal.ReadInt32(
                    manager,
                    NotificationQueueNativeContract.QueuedCountOffset);
                double elapsedMilliseconds =
                    (Stopwatch.GetTimestamp() - request.RequestedTimestamp) * 1000.0 /
                    Stopwatch.Frequency;

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL completed the right-clicked notification centrally: " +
                    $"generation={request.Generation}, elapsedMs={elapsedMilliseconds:F1}, " +
                    $"messageIdBefore={request.MessageId}, presentationIdBefore={request.PresentationId}, " +
                    $"queuedAtClick={request.QueuedCount}, queuedBefore={queuedBefore}, " +
                    $"promoted={promoted != 0}, activeAfter={activeAfter}, " +
                    $"messageIdAfter={messageAfter}, presentationIdAfter={presentationAfter}, " +
                    $"queuedAfter={queuedAfter}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL native notification completion failed; Vanilla update retained where safe: {ex}");
                if (Volatile.Read(ref pendingSkipRequest) != null)
                    notificationUpdateHook.Original(manager);
            }
        }

        private static void HideMessageBar()
        {
            OnScreenText onScreenText = OnScreenText.Instance;
            if (onScreenText != null)
            {
                bool wasTurnedOff = false;
                bool wasTurnedOnOrChanged = false;
                OnScreenText.OST messageBar = onScreenText.getOST(
                    Enums.eOnScreenText.OST_MESSAGE_BAR,
                    ref wasTurnedOff,
                    ref wasTurnedOnOrChanged);
                if (messageBar != null)
                {
                    messageBar.active = false;
                    messageBar.activeThisFrame = false;
                    messageBar.wasTurnedOnOrChanged = false;
                    messageBar.wasTurnedOff = false;
                    messageBar.timedEnd = DateTime.MinValue;
                }
            }

            if (MainViewModel.viewModelLoaded && MainViewModel.Instance != null)
                MainViewModel.Instance.OST_Message_Bar_Vis = false;
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

        private static void ValidateMessageBarContract()
        {
            Type ostType = typeof(OnScreenText.OST);
            ValidatePublicField(ostType, "active", typeof(bool));
            ValidatePublicField(ostType, "activeThisFrame", typeof(bool));
            ValidatePublicField(ostType, "wasTurnedOnOrChanged", typeof(bool));
            ValidatePublicField(ostType, "wasTurnedOff", typeof(bool));
            ValidatePublicField(ostType, "timedEnd", typeof(DateTime));

            MethodInfo getOst = typeof(OnScreenText).GetMethod(
                "getOST",
                InstanceMembers,
                null,
                new[]
                {
                    typeof(Enums.eOnScreenText),
                    typeof(bool).MakeByRefType(),
                    typeof(bool).MakeByRefType(),
                    typeof(bool)
                },
                null);
            if (getOst == null || getOst.ReturnType != ostType)
                throw new MissingMethodException(typeof(OnScreenText).FullName, "getOST");

            PropertyInfo visibility = typeof(MainViewModel).GetProperty(
                "OST_Message_Bar_Vis",
                InstanceMembers);
            if (visibility == null || visibility.PropertyType != typeof(bool) || !visibility.CanWrite)
                throw new MissingMemberException(typeof(MainViewModel).FullName, "OST_Message_Bar_Vis");
        }

        private static void ValidatePublicField(Type declaringType, string name, Type expectedType)
        {
            FieldInfo field = declaringType.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != expectedType)
                throw new MissingFieldException(declaringType.FullName, name);
        }

        private static void RollbackFailedInitialization(
            Hook managedCandidate,
            HookTransaction nativeCandidate)
        {
            nativeCandidate?.Dispose();
            if (managedCandidate == null)
                return;
            managedCandidate.Undo();
            managedCandidate.Dispose();
        }

        private sealed class PendingSkipRequest
        {
            public PendingSkipRequest(
                int generation,
                int messageId,
                int presentationId,
                int queuedCount,
                long requestedTimestamp)
            {
                Generation = generation;
                MessageId = messageId;
                PresentationId = presentationId;
                QueuedCount = queuedCount;
                RequestedTimestamp = requestedTimestamp;
            }

            public int Generation { get; }
            public int MessageId { get; }
            public int PresentationId { get; }
            public int QueuedCount { get; }
            public long RequestedTimestamp { get; }
        }
    }
}

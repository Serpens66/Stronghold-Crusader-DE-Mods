// Feature: Fully finish a queued AI/event notification when its video or videoless minimap is right-clicked.
using BepInEx.Logging;
using CrusaderDE;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.GameGlobals;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class NotificationSkipFeature
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void NotificationUpdateDelegate(IntPtr manager);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate ulong NotificationFinalizeDelegate(IntPtr manager);

        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IntPtr messageManager;
        private readonly IntPtr notificationPendingFlag;
        private readonly NotificationSpeechPlayback speech;
        private readonly DetourHandle<NotificationUpdateDelegate> notificationUpdateHook =
            new DetourHandle<NotificationUpdateDelegate>();
        private readonly HookTransaction notificationUpdateTransaction;
        private readonly NotificationFinalizeDelegate finalizeNotification;
        private PendingSkipRequest pendingSkipRequest;

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
                NotificationQueueNativeContract.Validate(nativeRegion.Span,
                    (image, pattern, referenceRva, name) => BfqolNativeCache.Current.ResolveUnique(image, pattern, referenceRva, name, log).Rva);

            ulong messageManagerAddress = GameGlobalsManager.Instance.MessageManagerVA;
            if (messageManagerAddress == 0 || messageManagerAddress > long.MaxValue)
                throw new InvalidOperationException("The Script Extender did not expose a valid MessageManager address.");
            messageManager = new IntPtr(unchecked((long)messageManagerAddress));

            HookTransaction pendingNativeTransaction = null;
            string initializationStage = "managed message-bar contract";
            try
            {
                ValidateMessageBarContract();

                initializationStage = "shared channel-1 speech owner";
                speech = NotificationSpeechPlayback.Get(log);

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
                speech.SetNotificationClassifier(IsActiveNotificationAudioLoad);
            }
            catch (Exception ex)
            {
                RollbackFailedInitialization(pendingNativeTransaction);
                throw new InvalidOperationException(
                    $"The notification {initializationStage} could not be initialized.",
                    ex);
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL complete notification-skip hooks installed for the process lifetime.");
        }

        internal void CompleteFromNotificationSurfaceRightClick(
            NotificationSkipSurface surface,
            Noesis.MouseButtonEventArgs args)
        {
            try
            {
                bool hasAudio;
                bool hasVideo;
                if (!NotificationSkipPolicy.ShouldCompleteOnRightClick(
                        ShouldArmNotificationSkip(surface, out hasAudio, out hasVideo),
                        true,
                        true))
                {
                    return;
                }

                args.Handled = true;
                CompleteCurrentNotification(hasVideo, hasAudio);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/NotificationSkipFeature.cs:155", $"Bugfixes and QoL complete notification skip failed after activation; completion may be partial: {ex}");
            }
        }

        private bool ShouldArmNotificationSkip(
            NotificationSkipSurface surface,
            out bool hasAudio,
            out bool hasVideo)
        {
            hasAudio = false;
            bool enabled = Enabled;
            bool queueActive = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.IsQueueActiveOffset) != 0;
            hasVideo = Marshal.ReadByte(
                messageManager,
                NotificationQueueNativeContract.ImmediateVideoPathOffset) != 0;
            hasAudio = Marshal.ReadByte(
                messageManager,
                NotificationQueueNativeContract.ImmediateAudioPathOffset) != 0;
            bool briefingVisible = MainViewModel.viewModelLoaded &&
                MainViewModel.Instance != null &&
                MainViewModel.Instance.Show_HUD_Briefing;

            // RadarME is layered above RadarMapImage. Noesis may still hit-test the
            // transparent media element, so a videoless hit on either attached
            // surface is semantically a minimap click.
            if (!hasVideo)
            {
                return NotificationSkipPolicy.ShouldArmMinimap(
                    enabled,
                    queueActive,
                    hasVideo,
                    briefingVisible);
            }

            if (surface == NotificationSkipSurface.Minimap)
                return false;

            SFXManager sfx = SFXManager.instance;
            bool videoVisible = MainViewModel.viewModelLoaded &&
                MainViewModel.Instance?.HUDRoot?.RefRadarME != null &&
                MainViewModel.Instance.HUDRoot.RefRadarME.Opacity != 0f;
            bool videoPlaying = sfx != null &&
                (sfx.binkIsPlaying || sfx.requestBinkPlayState != 0);

            return NotificationSkipPolicy.ShouldArmVideo(
                enabled,
                queueActive,
                hasVideo,
                videoVisible,
                videoPlaying,
                briefingVisible);
        }

        private void CompleteCurrentNotification(bool hasVideo, bool hasAudio)
        {
            int messageId = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.ImmediateCommandIdOffset);
            int presentationId = Marshal.ReadInt32(
                messageManager,
                NotificationQueueNativeContract.ImmediatePresentationIdOffset);
            Exception firstFailure = null;
            if (hasVideo)
            {
                try
                {
                    MainViewModel.Instance.HUDRoot.RadarME_Ended();
                }
                catch (Exception ex)
                {
                    firstFailure = ex;
                }
            }

            if (hasAudio)
            {
                try
                {
                    speech.StopSpeechChannel1(MyAudioManager.Instance);
                }
                catch (Exception ex)
                {
                    if (firstFailure == null)
                        firstFailure = ex;
                }
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

            var request = new PendingSkipRequest(messageId, presentationId);
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
                        Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/NotificationSkipFeature.cs:298", "Bugfixes and QoL discarded notification completion for an unexpected manager.");
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
                bool identityMatches = presentationId == request.PresentationId &&
                    (messageId == request.MessageId || messageId == 0);

                if (queueActive == 0 || !identityMatches)
                {
                    if (Interlocked.CompareExchange(ref pendingSkipRequest, null, request) == request)
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/NotificationSkipFeature.cs:322", $"Bugfixes and QoL discarded stale notification completion: " +
                            $"active={queueActive}, " +
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
                _ = finalizeNotification(manager);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/NotificationSkipFeature.cs:348", $"Bugfixes and QoL native notification completion failed; Vanilla update retained where safe: {ex}");
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

        private bool Enabled => settings.EnableClientFeatures && settings.EnableCompleteNotificationSkipOnClick;

        private bool IsActiveNotificationAudioLoad(string soundName)
        {
            if (!Enabled)
            {
                return false;
            }

            try
            {
                if (Marshal.ReadInt32(
                        messageManager,
                        NotificationQueueNativeContract.IsQueueActiveOffset) == 0)
                {
                    return false;
                }

                string notificationAudioPath = ReadImmediateAudioPath();
                return NotificationSkipPolicy.AudioPathMatches(
                    notificationAudioPath,
                    soundName);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/NotificationSkipFeature.cs:452", $"Bugfixes and QoL could not classify channel-1 speech; Vanilla loading continues: {ex}");
                return false;
            }
        }

        private string ReadImmediateAudioPath()
        {
            IntPtr pathAddress = IntPtr.Add(
                messageManager,
                NotificationQueueNativeContract.ImmediateAudioPathOffset);
            int length = 0;
            while (length < NotificationQueueNativeContract.ImmediateAudioPathCapacity &&
                   Marshal.ReadByte(pathAddress, length) != 0)
            {
                length++;
            }

            if (length == NotificationQueueNativeContract.ImmediateAudioPathCapacity)
                throw new InvalidOperationException("The immediate notification audio path is not null-terminated.");

            return length == 0 ? string.Empty : Marshal.PtrToStringAnsi(pathAddress, length);
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

        private static void RollbackFailedInitialization(HookTransaction nativeCandidate)
        {
            nativeCandidate?.Dispose();
        }

        private sealed class PendingSkipRequest
        {
            public PendingSkipRequest(int messageId, int presentationId)
            {
                MessageId = messageId;
                PresentationId = presentationId;
            }

            public int MessageId { get; }
            public int PresentationId { get; }
        }
    }
}

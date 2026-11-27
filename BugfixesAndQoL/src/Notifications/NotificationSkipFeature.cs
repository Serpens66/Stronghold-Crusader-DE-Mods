// Feature: Finish exactly the presented AI/event notification on a right-click.
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using SHCDESE.GameGlobals;
using System;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed unsafe class NotificationSkipFeature
    {
        private const string Feature = "Notification skip";
        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ulong messageManager;
        private readonly NotificationSpeechPlayback speech;
        private readonly X64InlineHook ageHook;
        private PendingSkipRequest pendingSkipRequest;

        internal NotificationSkipFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion nativeRegion,
            IntPtr libraryHandle,
            bool nativeHashValidated)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (nativeRegion == null)
                throw new ArgumentNullException(nameof(nativeRegion));
            if (libraryHandle == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(libraryHandle));
            if (!nativeHashValidated)
                throw new InvalidOperationException("The notification age branch is only audited on the reference CrusaderDE.dll.");

            int ageRva = NotificationQueueNativeContract.ValidateAgeBranch(nativeRegion.Span);
            messageManager = GameGlobalsManager.Instance.MessageManagerVA;
            if (messageManager == 0 || messageManager > long.MaxValue)
                throw new InvalidOperationException("The Script Extender did not expose a valid MessageManager address.");
            ValidateMessageBarContract();
            speech = NotificationSpeechPlayback.Get(log);

            ulong address = unchecked((ulong)libraryHandle.ToInt64()) + (uint)ageRva;
            Shared.NativePatchGuard.RequireUnpatched(address, NotificationQueueNativeContract.AgeBranchLength, Feature, Feature);
            X64InlineHook candidate = null;
            try
            {
                var logger = SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory.CreateLogger(Feature);
                candidate = new X64InlineHook(address, NotificationQueueNativeContract.AgeBranchLength, logger, Feature);
                if (candidate.State == HookState.Failed || candidate.IsInstalled ||
                    candidate.DisplacedByteCount != NotificationQueueNativeContract.AgeBranchLength)
                    throw new InvalidOperationException("The notification age branch displaced an unexpected span.");

                var options = new ContextHookOptions
                {
                    Registers = X64SmartCPUContextRegs.All,
                    HookSize = NotificationQueueNativeContract.AgeBranchLength,
                    Placement = OverwrittenInstructionPlacement.AfterCallback,
                    ErrorMode = CallbackErrorMode.LogAndContinue
                };
                var generator = ContextStub.CreateGenerator(candidate,
                    ContextStub.Guard(ApplyAgeBypass, options, address, logger, Feature), options);
                candidate.Generate((asm, original, continuation) =>
                {
                    if (continuation != address + NotificationQueueNativeContract.AgeBranchLength)
                        throw new InvalidOperationException("Unexpected notification age-branch continuation.");
                    generator(asm, original, continuation);
                });
                if (candidate.State == HookState.Failed || candidate.StubAddress == IntPtr.Zero)
                    throw new InvalidOperationException("The notification age branch did not produce a context stub.");
                Shared.NativePatchGuard.RequireUnpatched(candidate, Feature);
                candidate.Enable();
                if (!candidate.IsInstalled)
                    throw new InvalidOperationException("The notification age branch was not installed.");
                Shared.NativePatchGuard.Watch(candidate, Feature);
                ageHook = candidate;
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }

            NotificationSkipBehavior.Configure(this);
            speech.SetNotificationSkipEnabled(() => Enabled);
            Shared.DebugLogHelper.LogInfo(log,
                $"Notification skip installed at RVA 0x{ageRva:X}, displaced {ageHook.DisplacedByteCount} bytes.");
        }

        private bool Enabled => settings.EnableClientFeatures && settings.EnableCompleteNotificationSkipOnClick;

        internal void ResetMapState() => Interlocked.Exchange(ref pendingSkipRequest, null);

        internal void CompleteFromNotificationSurfaceRightClick(
            NotificationSkipSurface surface,
            object sender,
            Noesis.MouseButtonEventArgs args)
        {
            try
            {
                if (!Enabled || !ageHook.IsInstalled ||
                    !NotificationPresentations.TryGetRendered(out NotificationPresentationId id, out bool hasVideo) ||
                    !MainViewModel.viewModelLoaded)
                    return;

                MainViewModel main = MainViewModel.Instance;
                MainHUD hud = main?.HUDRoot;
                if (hud == null || main.Show_HUD_Briefing)
                    return;

                if (hasVideo)
                {
                    if (surface != NotificationSkipSurface.Video || hud.RefRadarME == null ||
                        !ReferenceEquals(sender, hud.RefRadarME) || hud.RefRadarME.Opacity == 0f ||
                        !OwnsCurrentVideo(hud, id))
                        return;
                }
                else if (!ReferenceEquals(sender, hud.RefRadarMapImage) && !ReferenceEquals(sender, hud.RefRadarME))
                {
                    return;
                }

                args.Handled = true;
                if (hasVideo && OwnsCurrentHud(main, hud, id) && OwnsCurrentVideo(hud, id))
                    CompleteOwnedUiPart(hud.RadarME_Ended, "video");
                if (OwnsCurrentHud(main, hud, id))
                    CompleteOwnedUiPart(() => speech.TryStopNotification(MyAudioManager.Instance, id), "speech");
                if (OwnsCurrentHud(main, hud, id) && NotificationPresentations.IsRenderedText(id))
                    CompleteOwnedUiPart(() => HideMessageBar(main), "text");

                if (Enabled && OwnsCurrentHud(main, hud, id))
                    Interlocked.Exchange(ref pendingSkipRequest, new PendingSkipRequest(id));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/NotificationSkipFeature/click",
                    $"Notification skip could not process the clicked presentation: {ex}");
            }
        }

        private void CompleteOwnedUiPart(Action operation, string part)
        {
            try { operation(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/NotificationSkipFeature/" + part,
                    $"Notification skip could not complete {part}: {ex}");
            }
        }

        private static bool OwnsCurrentHud(MainViewModel main, MainHUD hud, NotificationPresentationId id) =>
            MainViewModel.viewModelLoaded && ReferenceEquals(MainViewModel.Instance, main) &&
            ReferenceEquals(main.HUDRoot, hud) &&
            NotificationPresentations.TryGetRendered(out NotificationPresentationId current) && current.Equals(id);

        private static bool OwnsCurrentVideo(MainHUD hud, NotificationPresentationId id)
        {
            SFXManager sfx = SFXManager.instance;
            return sfx != null && sfx.binkIsPlaying && sfx.requestBinkPlayState > 0 && hud.RefRadarME?.Source != null &&
                NotificationPresentations.TryGetRenderedVideo(out NotificationPresentationId video, out Uri uri) &&
                video.Equals(id) && ReferenceEquals(sfx.requestBinkPlaybackURI, uri);
        }

        private void ApplyAgeBypass(NativePointer<X64SmartCPUContext> context)
        {
            PendingSkipRequest request = Volatile.Read(ref pendingSkipRequest);
            if (request == null)
                return;

            X64SmartCPUContext* state = context.Pointer;
            if (state->RBX != messageManager)
                return;
            if (!Enabled || !NotificationPresentations.IsNativeCurrent(request.Id))
            {
                Interlocked.CompareExchange(ref pendingSkipRequest, null, request);
                return;
            }

            // Feed the original SUB/CMP/JA a value just past the age threshold. RDI is dead
            // after that branch until the epilogue restores it. Keep the request while Vanilla
            // is still waiting on audio or video for this exact presentation.
            uint startedAt = *(uint*)(messageManager + NotificationQueueNativeContract.StartTimeOffset);
            state->RDI = NotificationSkipPolicy.TimestampPastAgeGate(startedAt);
        }

        private static void HideMessageBar(MainViewModel main)
        {
            OnScreenText onScreenText = OnScreenText.Instance;
            if (onScreenText == null)
                return;

            bool wasTurnedOff = false;
            bool wasTurnedOnOrChanged = false;
            OnScreenText.OST messageBar = onScreenText.getOST(
                Enums.eOnScreenText.OST_MESSAGE_BAR, ref wasTurnedOff, ref wasTurnedOnOrChanged);
            if (messageBar == null)
                return;

            messageBar.active = false;
            messageBar.activeThisFrame = false;
            messageBar.wasTurnedOnOrChanged = false;
            messageBar.wasTurnedOff = false;
            messageBar.timedEnd = DateTime.MinValue;
            main.OST_Message_Bar_Vis = false;
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

        private sealed class PendingSkipRequest
        {
            internal PendingSkipRequest(NotificationPresentationId id) => Id = id;

            internal NotificationPresentationId Id { get; }
        }
    }
}

using System;
using System.Runtime.InteropServices;
using APIShared.Presentation;
using BugfixesAndQoL;
using CrusaderDE;
using Noesis;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Context;
using SHCDESE.GameGlobals;

internal static partial class Program
{
    private static void TestNotificationSkip()
    {
        const int ageRva = 0xFE5C9;
        byte[] instructions = Convert.FromHexString("2BBBD800000081FF102700007706837B0400752F807B0C00");
        byte[] image = new byte[ageRva + instructions.Length];
        instructions.CopyTo(image, ageRva);
        IntPtr manager = Marshal.AllocHGlobal(NotificationQueueNativeContract.StartTimeOffset + sizeof(uint));
        try
        {
            ulong address = unchecked((ulong)manager.ToInt64());
            GameGlobalsManager.Instance.MessageManagerVA = address;
            Marshal.WriteInt32(manager, NotificationQueueNativeContract.StartTimeOffset, -5);
            Marshal.WriteInt32(manager, 4, 221);
            var first = new NotificationPresentationId(1);
            var second = new NotificationPresentationId(2);
            var skip = new NotificationSkipFeature(new BepInEx.Logging.ManualLogSource(),
                settings, new ScanRegion(image), new IntPtr(1), true);

            Setup();
            NotificationPresentations.Reset();
            var click = new MouseButtonEventArgs();
            skip.CompleteFromNotificationSurfaceRightClick(NotificationSkipSurface.Minimap, hud.RefRadarMapImage, click);
            Check(!click.Handled && ContextStub.RunAgeGate(address, 7) == 7,
                "missing observation preserves the original click and native age");

            Setup();
            Start();
            PresentationEvents.Gui();
            NotificationPresentations.RenderedId = NotificationPresentations.VideoId =
                NotificationPresentations.TextId = first;
            NotificationPresentations.HasVideo = true;
            NotificationPresentations.Request = sfx.requestBinkPlaybackURI;
            NotificationPresentations.NativeId = second;
            click = new MouseButtonEventArgs();
            skip.CompleteFromNotificationSurfaceRightClick(NotificationSkipSurface.Video, hud.RefRadarME, click);
            Check(click.Handled && !MainViewModel.Instance.OST_Message_Bar_Vis && !sfx.binkIsPlaying,
                "a valid click still closes rendered A when native already advanced to B");
            Check(ContextStub.RunAgeGate(address, 7) == 7,
                "native B keeps its original age even if it used the same text and files");

            Setup();
            Start();
            NotificationPresentations.RenderedId = NotificationPresentations.VideoId = second;
            NotificationPresentations.HasVideo = true;
            NotificationPresentations.Request = sfx.requestBinkPlaybackURI;
            sfx.requestBinkPlayState = -1;
            click = new MouseButtonEventArgs();
            skip.CompleteFromNotificationSurfaceRightClick(NotificationSkipSurface.Video, hud.RefRadarME, click);
            Check(!click.Handled && sfx.binkIsPlaying,
                "an accepted URI whose replacement is still pending does not own the visible video");

            Setup();
            NotificationPresentations.Reset();
            NotificationPresentations.RenderedId = NotificationPresentations.NativeId = first;
            Message();
            PresentationEvents.Gui();
            click = new MouseButtonEventArgs();
            skip.CompleteFromNotificationSurfaceRightClick(NotificationSkipSurface.Minimap, hud.RefRadarMapImage, click);
            Check(click.Handled && MainViewModel.Instance.OST_Message_Bar_Vis,
                "a valid videoless click preserves a message bar owned by another producer");
            Check(ContextStub.RunAgeGate(address, 7) == 9996 && ContextStub.RunAgeGate(0, 7) == 7,
                "same live native ticket bypasses age with unsigned wrap, and wrong manager stays original");
            Check(ContextStub.RunAgeGate(address, 7) == 9996 && Marshal.ReadInt32(manager, 4) == 221,
                "Vanilla audio/video guards can defer completion while the native message selector remains unchanged");
            skip.ResetMapState();
            Check(ContextStub.RunAgeGate(address, 7) == 7, "mission cleanup clears the pending request");

            settings.EnableCompleteNotificationSkipOnClick = false;
            click = new MouseButtonEventArgs();
            skip.CompleteFromNotificationSurfaceRightClick(NotificationSkipSurface.Minimap, hud.RefRadarMapImage, click);
            Check(!click.Handled && ContextStub.RunAgeGate(address, 7) == 7,
                "disabled skip does not consume a click or change native age");
        }
        finally
        {
            settings.EnableCompleteNotificationSkipOnClick = true;
            NotificationPresentations.Reset();
            GameGlobalsManager.Instance.MessageManagerVA = 0;
            Marshal.FreeHGlobal(manager);
        }
    }
}

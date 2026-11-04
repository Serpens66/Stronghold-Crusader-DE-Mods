using System;
using System.Threading;
using CrusaderDE;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static int observerBuildMainThread;
        private static readonly ObserverBuildHudState observerBuildHud = new ObserverBuildHudState();
        private static readonly Action<object> clearObserverBuildPage = ClearObserverBuildPage;

        // Called once by Initialize on Unity's startup thread; no new frame publisher.
        private static void InitializeObserverBuildHud() =>
            Volatile.Write(ref observerBuildMainThread, Thread.CurrentThread.ManagedThreadId);
        private static bool IsObserverBuildUiThread =>
            Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref observerBuildMainThread);

        private static bool HasGenuineObserverPlacement() =>
            !ObserverBuildPolicy.IsReadOnly(true, IsPlacementSession(), CurrentAiOwner() > 0);
        private static bool IsReadOnlyObserverBuild() => IsObserverBuildUiThread &&
            SpectatorDefeat.IsEliminatedSpectator && !HasGenuineObserverPlacement();

        private static bool BeforeObserverButtonBuild()
        {
            if (!IsObserverBuildUiThread) return true;
            try
            {
                if (!IsReadOnlyObserverBuild()) return true;
                ReconcileObserverBuildHud();
                return false;
            }
            catch (Exception error)
            { LogOnce("SPECTATOR_OBSERVER_BUILD_COMMAND_ERROR: " + error); return false; }
        }

        private static bool BeforeObserverStartMapper(int item, ref int __result)
        {
            // Zero is Vanilla's cancellation path. Never block it, even while retiring
            // an invalid Place target. Background/synchronized callers remain untouched.
            if (!IsObserverBuildUiThread || item == 0) return true;
            try
            {
                if (ObserverBuildPolicy.AllowMapper(true, IsReadOnlyObserverBuild(), item)) return true;
            }
            catch (Exception error) { LogOnce("SPECTATOR_OBSERVER_MAPPER_ERROR: " + error); }
            __result = -1;
            return false;
        }

        // True means this HUD belongs to a read-only observer, even when it was
        // already blank. Call before the lease's native-page observer so a rejected
        // category cannot discard the Tools lease or suppress reopening it.
        private static bool ReconcileObserverBuildHud(HUD_Main hud = null, bool preparingPromotion = false)
        {
            if (!IsObserverBuildUiThread || !MainViewModel.viewModelLoaded || !IsCurrentSession(CurrentSessionId)) return false;
            var view = MainViewModel.Instance;
            if (hud != null && !ReferenceEquals(hud, view.HUDmain)) return false;
            hud = view.HUDmain;
            if (hud == null || !(preparingPromotion || SpectatorDefeat.IsEliminatedSpectator)) return false;
            if (!preparingPromotion && HasGenuineObserverPlacement()) return false;
            if (humanPlacementMode)
            {
                humanPlacementMode = false;
                CancelSelectionActions();
                RestorePlacementLayout(); playerPlacementPage = null;
                activeHud = null; lastOwner = lastScreen = int.MinValue;
                SetRevealHidden(0);
                InvalidateToolbar();
            }
            observerBuildHud.TryClear(hud, CurrentSessionId, view.buildScreenID, true, clearObserverBuildPage);
            return true;
        }

        private static void ClearObserverBuildPage(object owner)
        {
            var hud = (HUD_Main)owner;
            var view = MainViewModel.Instance;
            buildPageMutation++;
            try
            {
                // The negative reset hides old icons immediately. NewBlank alone
                // disables them but can retain their prior visible states.
                hud.SetupNewBuildScreen(-1000);
                hud.NewBuildScreenBlank();
                view.buildControlsFreeze(true);
                RequestToolbarLayout();
            }
            finally { buildPageMutation--; }
        }
    }
}

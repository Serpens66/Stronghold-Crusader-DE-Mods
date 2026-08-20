// Feature: Make minimap dragging follow the mouse cursor directly.
using CrusaderDE;

namespace BugfixesAndQoL
{
    internal sealed partial class MinimapPlacementClickHook
    {
        private void NormalizeIdleRadarOverlay()
        {
            if (!settings.EnableClientFeatures || !settings.EnableMinimapCursorFollowFix ||
                !MainViewModel.viewModelLoaded || MainViewModel.Instance == null ||
                MainViewModel.Instance.HUDRoot == null ||
                MainViewModel.Instance.HUDRoot.RefRadarME == null ||
                FatControler.currentScene != Enums.SceneIDS.ActualMainGame ||
                GameData.Instance?.lastGameState == null ||
                !MainViewModel.Instance.RadarLoaded || !MainViewModel.Instance.MainUILoaded)
            {
                return;
            }

            var radarMedia = MainViewModel.Instance.HUDRoot.RefRadarME;
            SFXManager sfxManager = SFXManager.instance;

            // RadarME starts visible with a stale Source even though Bink is idle. Vanilla
            // consumes the first minimap click to hide it, so normalize that startup state.
            if (radarMedia.Opacity == 0f || sfxManager == null ||
                sfxManager.requestBinkPlayState != 0 || sfxManager.binkIsPlaying)
            {
                return;
            }

            radarMedia.Opacity = 0f;
        }

        private void FollowMinimapCursor(FatControler self)
        {
            if (!settings.EnableClientFeatures || !settings.EnableMinimapCursorFollowFix ||
                self == null || KeyManager.instance == null)
                return;

            if (!self.mouseIsDown || !GetBool(self, RadarScrollTriggeredField) || FatControler.MouseIsDownStroke)
                return;

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            if (IsOutsideRadar(self, mousePoint))
            {
                KeyManager.instance.RadarHeldX = 0f;
                KeyManager.instance.RadarHeldY = 0f;
                return;
            }

            KeyManager.instance.RadarHeldX = 0f;
            KeyManager.instance.RadarHeldY = 0f;
            EngineInterface.GameAction(
                Enums.GameActionCommand.RadarClicked,
                (int)(mousePoint.X * self.SHRadarScalar),
                (int)(mousePoint.Y * self.SHRadarScalar));
        }
    }
}

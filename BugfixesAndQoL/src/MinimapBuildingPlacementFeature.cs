// Feature: Allow minimap camera movement while placing a building.
using CrusaderDE;
using System;

namespace BugfixesAndQoL
{
    internal sealed partial class MinimapPlacementClickHook
    {
        private void TryHandlePlacementMinimap(FatControler self)
        {
            if (!settings.EnableMod || !settings.AllowMinimapWhilePlacingBuilding)
                return;

            if (self == null || MainControls.instance == null || MainControls.instance.CurrentAction != (int)Enums.editorActions.placingBuilding)
                return;

            if (!MainViewModel.viewModelLoaded || MainViewModel.Instance == null || MainViewModel.Instance.Show_HUD_Briefing || !MainControls.instance.IsUIVisible)
                return;

            if (GetBool(self, RadarClickDelayField) && DateTime.UtcNow < GetDateTime(self, RadarClickDelayTimeField))
                return;

            if (GameData.Instance?.lastGameState == null ||
                FatControler.currentScene != Enums.SceneIDS.ActualMainGame ||
                !MainViewModel.Instance.RadarLoaded ||
                !MainViewModel.Instance.MainUILoaded)
            {
                return;
            }

            if (!self.mouseIsDown)
            {
                SetBool(self, RadarScrollTriggeredField, false);
                return;
            }

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            if (FatControler.MouseIsDownStroke && IsOutsideRadar(self, mousePoint))
                return;

            if (FatControler.MouseIsDownStroke)
                HandleClickStroke(self);
            else
                HandleDrag(self);
        }

        private static void HandleClickStroke(FatControler self)
        {
            if (MainViewModel.Instance.HUDRoot.RefRadarME.Opacity != 0f)
            {
                MainViewModel.Instance.HUDRoot.RefRadarME.Opacity = 0f;
                FatControler.MouseIsDownStroke = false;
                SetBool(self, RadarScrollTriggeredField, false);
                return;
            }

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            SetBool(self, RadarScrollTriggeredField, true);
            SetPoint(self, LastNgMousePointField, mousePoint);
            EngineInterface.GameAction(
                Enums.GameActionCommand.RadarClicked,
                (int)(mousePoint.X * self.SHRadarScalar),
                (int)(mousePoint.Y * self.SHRadarScalar));
        }

        private static void HandleDrag(FatControler self)
        {
            if (!GetBool(self, RadarScrollTriggeredField) || KeyManager.instance == null)
                return;

            Noesis.Point mousePoint = GetPoint(self, NgMousePointField);
            Noesis.Point lastMousePoint = GetPoint(self, LastNgMousePointField);
            float deltaX = mousePoint.X - lastMousePoint.X;
            float deltaY = lastMousePoint.Y - mousePoint.Y;

            if (deltaX == 0f && deltaY != 0f)
            {
                KeyManager.instance.RadarHeldY = deltaY > 0f ? 1f : -1f;
                return;
            }
            if (deltaY == 0f && deltaX != 0f)
            {
                KeyManager.instance.RadarHeldX = deltaX > 0f ? 1f : -1f;
                return;
            }

            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                float ratioY = Math.Abs(deltaY / deltaX);
                KeyManager.instance.RadarHeldX = deltaX > 0f ? 1f : -1f;
                KeyManager.instance.RadarHeldY = deltaY > 0f ? ratioY : -ratioY;
            }
            else if (Math.Abs(deltaY) > 0f)
            {
                float ratioX = Math.Abs(deltaX / deltaY);
                KeyManager.instance.RadarHeldX = deltaX > 0f ? ratioX : -ratioX;
                KeyManager.instance.RadarHeldY = deltaY > 0f ? 1f : -1f;
            }
        }

        private static bool IsOutsideRadar(FatControler self, Noesis.Point mousePoint)
        {
            return mousePoint.X < 0f ||
                mousePoint.X >= self.SHRadarRectSize ||
                mousePoint.Y < 0f ||
                mousePoint.Y >= self.SHRadarRectSize;
        }
    }
}

// Feature: Make minimap dragging follow the mouse cursor directly.
using CrusaderDE;

namespace BugfixesAndQoL
{
    internal sealed partial class MinimapPlacementClickHook
    {
        private void FollowMinimapCursor(FatControler self)
        {
            if (!settings.EnableMod || self == null || KeyManager.instance == null)
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

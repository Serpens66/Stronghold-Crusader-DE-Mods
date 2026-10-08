using System;
namespace APIShared.UnitCommands
{
    internal interface IFormationPresentation
    {
        void CloseMenu();
        void RefreshHostState();
        void RefreshPreview();
        void ClearPreview();
        void SetPreviewAuthorization(bool allowed);
        void PublishPreview(FormationPreviewPoint[] points, FormationDirectionIndicator direction);
    }
    internal readonly struct FormationPreviewPoint
    {
        internal FormationPreviewPoint(int x, int y, FormationRole role) { X = x; Y = y; Role = role; }
        internal int X { get; }
        internal int Y { get; }
        internal FormationRole Role { get; }
    }

    internal readonly struct FormationDirectionIndicator
    {
        internal static readonly FormationDirectionIndicator Hidden =
            new FormationDirectionIndicator(false, 0, 0, 0, 0, false);
        internal FormationDirectionIndicator(
            bool visible,
            int startX,
            int startY,
            int endX,
            int endY,
            bool explicitDirection)
        {
            Visible = visible;
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            ExplicitDirection = explicitDirection;
        }
        internal bool Visible { get; }
        internal int StartX { get; }
        internal int StartY { get; }
        internal int EndX { get; }
        internal int EndY { get; }
        internal bool ExplicitDirection { get; }
    }
}

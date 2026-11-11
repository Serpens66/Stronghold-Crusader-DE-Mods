namespace AreaDemolitionTest
{
    internal enum GestureStage { Idle, Pressed, Dragging, Cancelled }
    internal enum GestureRelease { None, Single, Area, Cancelled }
    // This state machine owns the whole press/release pair, including after cancellation.
    internal sealed class SelectionGesture
    {
        internal GestureStage Stage { get; private set; }
        internal bool Owned => Stage != GestureStage.Idle;
        internal SelectionSpec Selection { get; private set; }
        internal AreaRect Area => new AreaRect(startX, startY, endX, endY);
        private int startX, startY, endX, endY;
        private float screenX, screenY;
        internal void Begin(int x, int y, float sx, float sy, SelectionSpec selection)
        { startX=endX=x; startY=endY=y; screenX=sx; screenY=sy; Selection=selection; Stage=GestureStage.Pressed; }
        internal void Move(int x, int y, float sx, float sy, float scale)
        {
            if (Stage != GestureStage.Pressed && Stage != GestureStage.Dragging) return;
            endX=x; endY=y;
            float dx=sx-screenX, dy=sy-screenY, threshold=4f*(scale > 0 ? scale : 1);
            if (dx*dx+dy*dy >= threshold*threshold) Stage=GestureStage.Dragging;
        }
        internal void Cancel() { if (Owned) Stage=GestureStage.Cancelled; }
        internal GestureRelease Release(bool overUi)
        {
            var result = !Owned ? GestureRelease.None : overUi || Stage==GestureStage.Cancelled ? GestureRelease.Cancelled : Stage==GestureStage.Dragging ? GestureRelease.Area : GestureRelease.Single;
            Stage=GestureStage.Idle; return result;
        }
        internal void Reset() { Stage=GestureStage.Idle; }
    }
}

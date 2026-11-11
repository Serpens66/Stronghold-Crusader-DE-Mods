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
        internal ScreenRect Area => new ScreenRect(startX, startY, endX, endY);
        private float startX, startY, endX, endY;
        internal void Begin(float sx, float sy, SelectionSpec selection)
        { startX=endX=sx; startY=endY=sy; Selection=selection; Stage=GestureStage.Pressed; }
        internal void Move(float sx, float sy, bool endpointValid, float scale)
        {
            if (Stage != GestureStage.Pressed && Stage != GestureStage.Dragging) return;
            // HUD crossing freezes the endpoint, but still counts as physical movement.
            if (endpointValid) { endX=sx; endY=sy; }
            float dx=sx-startX, dy=sy-startY, threshold=4f*(scale > 0 ? scale : 1);
            if (dx*dx+dy*dy >= threshold*threshold) Stage=GestureStage.Dragging;
        }
        internal bool SetSameType(bool sameType)
        {
            if (!Owned || Stage==GestureStage.Cancelled || Selection.SameType==sameType) return false;
            Selection=new SelectionSpec(Selection.Filters,sameType,Selection.Start);
            return true;
        }
        internal void Cancel() { if (Owned) Stage=GestureStage.Cancelled; }
        internal GestureRelease Release(bool overUi)
        {
            var result = !Owned ? GestureRelease.None : Stage==GestureStage.Cancelled ? GestureRelease.Cancelled : Stage==GestureStage.Dragging ? GestureRelease.Area : overUi ? GestureRelease.Cancelled : GestureRelease.Single;
            Stage=GestureStage.Idle; return result;
        }
        internal void Reset() { Stage=GestureStage.Idle; }
    }
}

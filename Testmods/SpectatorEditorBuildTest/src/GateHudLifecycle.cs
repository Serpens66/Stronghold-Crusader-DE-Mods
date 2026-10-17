namespace SpectatorEditorBuildTest
{
    internal enum ToolsHudRoute { None, Main, Editor, Building, Troops }

    // Input intentions survive Vanilla's intervening HUD updates, but never cross a session/HUD reset.
    internal sealed class GateHudLifecycle
    {
        private long readyAfter;
        private bool pending;
        private long pendingGeneration;
        internal long Generation { get; private set; }
        internal bool Owned { get; private set; }
        internal void Request(long revision) { readyAfter = revision + 1; pendingGeneration = Generation; pending = true; }
        internal bool TryConsume(long revision, bool ready)
        {
            if (!pending || pendingGeneration != Generation || !ready || revision < readyAfter) return false;
            pending = false; return true;
        }
        internal void Acquire() { Owned = true; }
        internal bool Release() { bool owned = Owned; Owned = false; return owned; }
        internal void CancelPending() { pending = false; }
        internal static ToolsHudRoute Route(int mode, int submode, bool editor)
        {
            // Managed PlayState ABI: mirrors GameData.setGameState, not a native mode write.
            if (mode == 16) return ToolsHudRoute.Building;
            if (mode == 14 && (submode == 61 || submode == 62)) return ToolsHudRoute.Troops;
            if (editor) return ToolsHudRoute.Editor;
            return mode == 14 ? ToolsHudRoute.Main : ToolsHudRoute.None;
        }
        internal bool ObservePresentation(bool sameHud, bool building, bool main, bool troops)
        {
            if (!Owned || (sameHud && building && !main && !troops)) return false;
            Transition(); return true;
        }
        internal void Transition() { Generation++; pending = false; }
        internal static bool IsBuildScreen(int screen) { return screen > 0 && screen < 23 &&
            screen != 15 && screen != 17 && screen != 18 && screen != 21 && screen != 22; }
    }
}

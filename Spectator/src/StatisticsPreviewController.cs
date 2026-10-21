namespace Spectator
{
    // Identity/generation ownership keeps delayed cleanup away from a newer Vanilla screen.
    internal sealed class StatisticsPreviewController
    {
        internal long Generation { get; private set; }
        internal long Session { get; private set; }
        internal object View { get; private set; }
        internal bool VanillaEnded { get; private set; }
        internal bool Active => View != null;
        internal void Start(long session) { Session = session; Generation++; View = null; VanillaEnded = false; }
        internal bool Acquire(object view) { if (Session <= 0 || VanillaEnded || view == null) return false; View = view; Generation++; return true; }
        internal bool Release(long generation, object visibleView)
        {
            if (generation != Generation) return false;
            bool ownsVisible = View != null && ReferenceEquals(View, visibleView);
            View = null; Generation++; return ownsVisible;
        }
        internal void EndVanilla() { VanillaEnded = true; View = null; Generation++; }
    }
}

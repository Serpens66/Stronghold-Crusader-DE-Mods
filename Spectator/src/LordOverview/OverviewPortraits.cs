using Noesis;

namespace Spectator.LordOverview
{
    internal static partial class OverviewRuntime
    {
        private static readonly PlayerPortraits<ImageSource> portraits = new PlayerPortraits<ImageSource>(
            new VanillaPortraitSource(() => main, () => mode));

        private static void SetPortrait(RowVisual view, ImageSource face)
        {
            if (ReferenceEquals(view.Face, face)) return;
            view.Face = face; ((Image)view.Portrait.Children[0]).Source = face;
        }

        private static void RefreshPendingPortraits()
        {
            if (!portraits.HasPending || panel?.IsVisible != true || !panel.IsLoaded || main?.Show_InGameUI != true) return;
            // Keep the per-GUI pending check allocation-free; UiActionContext is a class.
            long portraitSession = System.Threading.Interlocked.Read(ref session);
            int portraitGeneration = System.Threading.Volatile.Read(ref generation);
            var portraitPanel = panel;
            if (portraitSession <= 0 || !settings.EnableMod) return;
            int changed = portraits.Poll(Now);
            if (changed == 0 || portraitSession != System.Threading.Interlocked.Read(ref session) ||
                portraitGeneration != System.Threading.Volatile.Read(ref generation) ||
                !ReferenceEquals(portraitPanel, panel) || panel?.IsVisible != true || !settings.EnableMod) return;
            // Fixed portrait geometry: update the realized Image only, without Render or sampling.
            for (int player = 1; player <= 8; player++)
                if ((changed & (1 << player)) != 0 && rowVisuals.TryGetValue(player, out var view)) SetPortrait(view, portraits.Cached(player));
        }
    }
}

using CrusaderDE;
using Noesis;
using APIShared.GameModes;

namespace Spectator.LordOverview
{
    internal static partial class OverviewRuntime
    {
        private static readonly PlayerPortraits<ImageSource> portraits = new PlayerPortraits<ImageSource>(new VanillaPortraitSource());

        private sealed class VanillaPortraitSource : IPlayerPortraitSource<ImageSource>
        {
            public PortraitIdentity Identify(int player)
            {
                var snapshot = GameData.Instance?.lastGameState;
                if (snapshot?.computer_register == null || player < 1 || player >= snapshot.computer_register.Length) return default;
                bool ai = snapshot.is_skirmish_player(player);
                bool known = mode.HasValue && mode.Value.Kind != GameModeKind.Unknown;
                bool multiplayer = known && mode.Value.IsRealMultiplayer;
                ulong steamId = 0;
                if (!ai && multiplayer && Platform_Multiplayer.Instance != null)
                {
                    var id = Platform_Multiplayer.Instance.getPlayerSteamID(player);
                    if (id.IsValid()) steamId = id.m_SteamID;
                }
                return PortraitIdentity.Choose(ai, snapshot.computer_register[player], known, multiplayer, steamId);
            }
            public ImageSource Read(PortraitIdentity identity)
            {
                if (identity.Kind == PortraitKind.Ai) return main?.getAIFace(identity.Lord, true);
                var platform = Platform_Multiplayer.Instance;
                if (platform == null) return null;
                if (identity.Kind == PortraitKind.Local) return platform.GetLocalAvatar();
                if (identity.Kind == PortraitKind.Steam) return platform.GetUserAvatar(identity.SteamId);
                return null;
            }
        }

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

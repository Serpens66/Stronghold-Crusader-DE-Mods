using System;
using APIShared.GameModes;
using CrusaderDE;
using Noesis;
using Spectator.LordOverview;

namespace Spectator
{
    // Both surfaces borrow Vanilla-owned images, with independent cache/HUD lifetimes.
    internal sealed class VanillaPortraitSource : IPlayerPortraitSource<ImageSource>
    {
        private readonly Func<MainViewModel> view;
        private readonly Func<GameModeSnapshot?> mode;
        internal VanillaPortraitSource(Func<MainViewModel> view, Func<GameModeSnapshot?> mode)
        { this.view = view; this.mode = mode; }

        public PortraitIdentity Identify(int player)
        {
            var snapshot = GameData.Instance?.lastGameState;
            if (snapshot?.computer_register == null || player < 1 || player >= snapshot.computer_register.Length) return default;
            bool ai = snapshot.is_skirmish_player(player);
            var context = mode();
            bool known = context.HasValue && context.Value.Kind != GameModeKind.Unknown;
            bool multiplayer = known && context.Value.IsRealMultiplayer;
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
            if (identity.Kind == PortraitKind.Ai) return view()?.getAIFace(identity.Lord, true);
            var platform = Platform_Multiplayer.Instance;
            if (platform == null) return null;
            if (identity.Kind == PortraitKind.Local) return platform.GetLocalAvatar();
            if (identity.Kind == PortraitKind.Steam) return platform.GetUserAvatar(identity.SteamId);
            return null;
        }
    }
}

using CrusaderDE;
using Noesis;
using System;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static Grid toolbarRoot;
        private static ScrollViewer detailsScroll;
        private static readonly Button[] targetButtons = new Button[9];
        private static readonly TextBlock[] targetLabels = new TextBlock[9];
        private static readonly Image[] ownerFaces = new Image[9], targetFaces = new Image[9];
        private static string toolbarLocale;
        private static bool initialSelectionChosen;
        private static long rosterFingerprint;
        private static EngineInterface.PlayState rosterState;

        private static void CacheToolbarElements(IngameUIScreens screen)
        {
            toolbarRoot = screen.FindName("TestToolsRoot") as Grid;
            statusText = screen.FindName("ToolsStatus") as TextBlock;
            detailsScroll = screen.FindName("SpectatorDetailsScroll") as ScrollViewer;
            if (detailsScroll == null) throw new MissingMemberException("Toolbar header/scroll host missing.");
            MenuToolTipSizing.SetReference(humanPanel, humanCanvas);
            var attackIcon = screen.FindName("AttackActionIcon") as Image;
            if (attackIcon != null) attackIcon.Source = MainViewModel.Instance.getAlliesGoodsIcon(-3);
            currentOrderIcon = screen.FindName("CurrentOrderIcon") as Image;
            currentOrderText = screen.FindName("CurrentOrderText") as TextBlock;
            currentOrderTarget = screen.FindName("CurrentOrderTarget") as TextBlock;
            if (currentOrderIcon == null || currentOrderText == null || currentOrderTarget == null) throw new MissingMemberException("Order presentation missing.");
            for (int player = 1; player <= 8; player++)
            {
                targetButtons[player] = screen.FindName("TestTarget" + player) as Button;
                targetLabels[player] = screen.FindName("TestTargetText" + player) as TextBlock;
                ownerFaces[player] = screen.FindName("OwnerFace" + player) as Image;
                targetFaces[player] = screen.FindName("TargetFace" + player) as Image;
                if (targetButtons[player] == null || targetLabels[player] == null || ownerFaces[player] == null || targetFaces[player] == null)
                    throw new MissingMemberException("Spectator player cards are incomplete.");
            }
            if (toolbarRoot == null) throw new MissingMemberException("Spectator toolbar root is missing.");
        }

        private static void ObserveRoster()
        {
            var state = GameData.Instance?.lastGameState;
            if (state == null || ReferenceEquals(state, rosterState)) return;
            rosterState = state;
            RefreshOrderPresentation();
            long value = 17;
            unchecked
            {
                for (int player = 1; player <= 8; player++)
                {
                    value = value * 31 + state.computer_register[player];
                    value = value * 31 + state.player_register[player];
                    value = value * 31 + state.teams[player];
                    value = value * 31 + (state.computer_names?[player].GetHashCode() ?? 0);
                    value = value * 31 + (SpriteMapping.remapColours?[player] ?? 0);
                    // Defeat can invalidate a recipient without changing its roster slot/name.
                    value = value * 31 + (IsToolsPlayer(player, false) ? 1 : 0);
                }
            }
            if (value != rosterFingerprint) { rosterFingerprint = value; toolbarDirty = true; }
            // Availability can change on defeat while roster membership remains unchanged.
            if (!spectatorSelectionLinked && humanSelectedOwner > 0 && !IsSelectableAi(humanSelectedOwner)) ApplyHumanAi(-1);
            if (toolsModel?.Expanded == true && toolsModel.ActiveViewKey == "Orders") RefreshOrderAvailability();
        }

        private static void RefreshOrderAvailability()
        {
            bool actor = IsSelectableAi(humanSelectedOwner);
            bool target = toolsModel != null && IsToolsPlayer(toolsModel.Target, false);
            bool backend = nativeOrder != null;
            var state = GameData.Instance?.lastGameState;
            bool enemy = actor && target && state != null && humanSelectedOwner != toolsModel.Target &&
                state.teams[humanSelectedOwner] != state.teams[toolsModel.Target];
            toolsModel?.SetOrderAvailability(actor && backend && enemy, actor && backend && target, actor && backend,
                !actor ? "InvalidTarget" : !backend ? "NativeUnavailable" : !target ? "SelectTarget" : !enemy ? "EnemyRequired" : "");
        }
    }
}

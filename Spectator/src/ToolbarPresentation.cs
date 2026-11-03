using CrusaderDE;
using Noesis;
using System;
using Spectator.LordOverview;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static Canvas toolbarRoot;
        private static readonly Button[] targetButtons = new Button[9];
        private static readonly TextBlock[] targetLabels = new TextBlock[9];
        private static readonly Image[] ownerFaces = new Image[9], targetFaces = new Image[9];
        private static readonly PlayerPortraits<ImageSource> toolbarPortraits = new PlayerPortraits<ImageSource>(
            new VanillaPortraitSource(() => MainViewModel.viewModelLoaded ? MainViewModel.Instance : null, () => sessionMode));
        private static string toolbarLocale;
        private static bool initialSelectionChosen;
        private static long rosterFingerprint;
        private static EngineInterface.PlayState rosterState;
        private static readonly PortraitNumberLayout[] portraitNumbers = new PortraitNumberLayout[16];

        private static double PortraitTime => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

        private static void SetToolbarPortrait(int player, ImageSource face)
        {
            if (ownerFaces[player] != null && !ReferenceEquals(ownerFaces[player].Source, face)) ownerFaces[player].Source = face;
            if (targetFaces[player] != null && !ReferenceEquals(targetFaces[player].Source, face)) targetFaces[player].Source = face;
        }

        private static void RefreshToolbarPortraits(int mask)
        {
            toolbarPortraits.SetPlayers(mask);
            double now = PortraitTime;
            for (int player = 1; player <= 8; player++)
                SetToolbarPortrait(player, (mask & (1 << player)) == 0 ? null : toolbarPortraits.Resolve(player, now));
        }

        private static void RefreshPendingToolbarPortraits()
        {
            if (!toolbarPortraits.HasPending || humanPanel?.IsVisible != true || !humanPanel.IsLoaded) return;
            long session = CurrentSessionId;
            var panel = humanPanel;
            if (!IsCurrentSession(session)) return;
            int changed = toolbarPortraits.Poll(PortraitTime);
            if (changed == 0 || !IsCurrentSession(session) || !ReferenceEquals(panel, humanPanel) || panel?.IsVisible != true) return;
            // Updating the existing images must not rebuild the toolbar or acquire statistics.
            for (int player = 1; player <= 8; player++)
                if ((changed & (1 << player)) != 0) SetToolbarPortrait(player, toolbarPortraits.Cached(player));
        }

        private static void RefreshPortraitNumbers()
        { foreach (var number in portraitNumbers) number?.Refresh(); }

        private static void DetachPortraitNumbers()
        { for (int i = 0; i < portraitNumbers.Length; i++) { portraitNumbers[i]?.Detach(); portraitNumbers[i] = null; } }

        private static void CacheToolbarElements(IngameUIScreens screen)
        {
            toolbarRoot = screen.FindName("TestToolsRoot") as Canvas;
            statusText = screen.FindName("ToolsStatus") as TextBlock;
            orderStatusText = screen.FindName("OrdersStatus") as TextBlock;
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
                portraitNumbers[(player - 1) * 2] = new PortraitNumberLayout(
                    screen.FindName("SpectatorEditorHumanNumber" + player) as TextBlock,
                    screen.FindName("OwnerNumberOutline" + player) as TextBlock);
                portraitNumbers[(player - 1) * 2 + 1] = new PortraitNumberLayout(targetLabels[player],
                    screen.FindName("TargetNumberOutline" + player) as TextBlock);
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
            if (!spectatorSelectionLinked) selection?.Revalidate(IsSelectableView(humanSelectedOwner));
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

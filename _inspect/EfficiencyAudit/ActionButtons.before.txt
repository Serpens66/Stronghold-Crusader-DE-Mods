using CrusaderDE;
using Noesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace APIShared
{
    internal sealed unsafe partial class UnitHudPresentationService
    {
        private readonly List<ActionButtonRegistration> actionButtons = new List<ActionButtonRegistration>();
        private readonly Dictionary<ActionButtonRegistration, ActionButtonVisual> actionVisuals =
            new Dictionary<ActionButtonRegistration, ActionButtonVisual>();
        private volatile bool hasActionButtons;
        private HUD_Troops actionPanel;
        private Canvas actionHost;
        private FrameworkElement actionControls;
        private Button actionNext;
        private readonly UnitHudHoverTextPresenter troopHover = new UnitHudHoverTextPresenter();
        private bool actionNextHovered;
        private string actionNextText;
        private int actionPage;
        private int actionCapacity;
        private int actionVisibleCount;

        private bool RegisterActionButton(string owner, UnitHudActionButtonDefinition definition,
            out IUnitHudActionButtonRegistration registration, out NativeCapabilityDiagnostic diagnostic)
        {
            registration = null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.ButtonId) ||
                definition.Command == null || string.IsNullOrWhiteSpace(definition.Tooltip) || definition.ContentFactory == null)
                return Fail("Action button ID, command, tooltip and content factory are required.", out diagnostic);
            lock (sync)
            {
                if (actionButtons.Any(x => x.Owner == owner && x.Definition.ButtonId == definition.ButtonId))
                    return Fail("The owner already registered this action button ID.", out diagnostic);
                var item = new ActionButtonRegistration(this, owner, definition);
                actionButtons.Add(item);
                actionButtons.Sort((a, b) => {
                    int order = a.Definition.Order.CompareTo(b.Definition.Order);
                    return order != 0 ? order : Compare(a.Owner, a.Definition.ButtonId, b.Owner, b.Definition.ButtonId);
                });
                registration = item;
                hasActionButtons = true;
                pendingPresentation = true;
            }
            diagnostic = Available("Own-troop action button registered for the process lifetime.");
            return true;
        }

        private ActionButtonState[] CaptureActionButtons()
        {
            lock (sync)
                return actionButtons.Select(x => new ActionButtonState(x, OwnerActive(x.Owner))).ToArray();
        }

        private bool TryGetActionContext(MainViewModel main, out UnitHudActionButtonContext context)
        {
            context = null;
            if (main?.HUDmain == null || main.HUDTroopPanel == null ||
                FatControler.currentScene != Enums.SceneIDS.ActualMainGame) return false;
            var controls = main.HUDTroopPanel.FindName("TroopSelectionControls") as FrameworkElement;
            if (controls == null || !TryGetTroopSelection(out LocalSelectionSnapshot selection) ||
                !UnitHudActionButtonLayout.OwnContext(main.Show_HUD_Troops,
                    controls.Visibility == Visibility.Visible, controls.IsHitTestVisible, controls.Opacity,
                    selection.PlayerId, selection.Count)) return false;
            int colour = SpriteMapping.RemapMPLoadedColour(selection.PlayerId);
            int[] colours = SpriteMapping.remapColours;
            colour = ResolveActionColour(colour, colours);
            if (colour < 1) return false;

            context = new UnitHudActionButtonContext(main.HUDTroopPanel, selection.PlayerId, colour);
            return true;
        }

        internal static int ResolveActionColour(int loadedColour, int[] colours) =>
            colours != null && loadedColour >= 0 && loadedColour < colours.Length &&
            colours[loadedColour] >= 1 && colours[loadedColour] <= 8 ? colours[loadedColour] : -1;

        private void ApplyActionButtons(MainViewModel main)
        {
            if (!hasActionButtons) { if (!TryGetActionContext(main, out _)) troopHover.Close(); return; }
            ActionButtonState[] states = CaptureActionButtons();
            bool ownContext = TryGetActionContext(main, out UnitHudActionButtonContext context);
            foreach (ActionButtonState state in states)
                NotifyActionContext(state.Registration, ownContext && state.Visible);
            if (!ownContext) { HideActionButtons(true); return; }
            EnsureActionHost(main.HUDTroopPanel);
            var anchor = main.HUDTroopPanel.FindName("ToggleControlGroups") as FrameworkElement;
            if (anchor == null || anchor.ActualWidth <= 0 || actionControls.ActualWidth <= 0)
            { HideActionButtons(); return; }
            Rect anchorRect = ActionRect(anchor);
            float left = 0;
            // Reserve every visible sibling intersecting the button row, including other mods' controls.
            var root = main.HUDTroopPanel.FindName("LayoutRoot") as Panel;
            if (root == null) { HideActionButtons(); return; }
            foreach (UIElement child in root.Children)
            {
                if (!(child is FrameworkElement obstacle) || ReferenceEquals(obstacle, actionControls) ||
                    ReferenceEquals(obstacle, anchor) || obstacle.Visibility != Visibility.Visible ||
                    obstacle.Opacity <= 0 || !obstacle.IsHitTestVisible || obstacle.ActualWidth <= 0 ||
                    string.Equals(obstacle.Tag as string, "Ignore", StringComparison.Ordinal)) continue;
                left = ReserveActionObstacle(obstacle.GetType(), anchorRect, ActionRect(obstacle), left);
            }
            actionCapacity = UnitHudActionButtonLayout.Capacity(anchorRect.X, left);
            var visible = states.Where(x => x.Visible).ToArray();
            actionVisibleCount = visible.Length;
            actionPage = UnitHudActionButtonLayout.ClampPage(actionPage, visible.Length, actionCapacity);
            if (actionCapacity == 0 || visible.Length == 0) { HideActionButtons(); return; }
            actionHost.Visibility = Visibility.Visible;
            var displayed = new HashSet<ActionButtonRegistration>();
            int start = actionPage * actionCapacity;
            int end = Math.Min(start + actionCapacity, visible.Length);
            for (int index = start; index < end; index++)
            {
                ActionButtonState state = visible[index];
                try
                {
                    ActionButtonVisual visual = GetActionVisual(state, context);
                    if (visual.Failed) continue;
                    Button button = visual.Button;
                    button.IsEnabled = state.Enabled;
                    if (!state.Enabled) SetActionHover(state.Registration, false);
                    else if (state.Registration.Hovered) ShowTroopHover(state.Registration, state.Tooltip);
                    Canvas.SetLeft(button, anchorRect.X - UnitHudActionButtonLayout.Pitch * (index - start + 1));
                    Canvas.SetTop(button, anchorRect.Y);
                    button.Visibility = Visibility.Visible;
                    displayed.Add(state.Registration);
                }
                catch (Exception ex) { LogCallbackFailure("action visual " + state.Registration.Key, ex); }
            }
            foreach (var pair in actionVisuals)
                if (!displayed.Contains(pair.Key))
                {
                    pair.Value.Button.Visibility = Visibility.Collapsed;
                    SetActionHover(pair.Key, false);
                }
            bool overflow = UnitHudActionButtonLayout.PageCount(visible.Length, actionCapacity) > 1;
            actionNextText = (actionPage + 1) + " / " +
                UnitHudActionButtonLayout.PageCount(visible.Length, actionCapacity) + " →";
            Canvas.SetLeft(actionNext, anchorRect.X + anchorRect.Width + 4);
            Canvas.SetTop(actionNext, anchorRect.Y + 6.5f);
            actionNext.Visibility = overflow ? Visibility.Visible : Visibility.Collapsed;
            if (actionNextHovered && overflow) ShowTroopHover(actionNext, actionNextText);
            if (!overflow) { actionNextHovered = false; troopHover.Close(actionNext); }
        }

        internal static float ReserveActionObstacle(Type elementType, Rect anchor, Rect obstacle, float leftBoundary)
        {
            // Vanilla's transparent rectangles prevent world clicks; they do not reserve button space.
            if (typeof(TextBlock).IsAssignableFrom(elementType) || typeof(Image).IsAssignableFrom(elementType) ||
                typeof(Rectangle).IsAssignableFrom(elementType)) return leftBoundary;
            return obstacle.Y < anchor.Y + UnitHudActionButtonLayout.Size && obstacle.Y + obstacle.Height > anchor.Y &&
                obstacle.X < anchor.X ? Math.Max(leftBoundary, obstacle.X + obstacle.Width + UnitHudActionButtonLayout.Gap) : leftBoundary;
        }
        private Rect ActionRect(FrameworkElement element)
        {
            Point origin = actionControls.PointToScreen(new Point(0, 0));
            Point unit = actionControls.PointToScreen(new Point(1, 1));
            Point position = element.PointToScreen(new Point(0, 0));
            Point edge = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
            float scaleX = unit.X - origin.X, scaleY = unit.Y - origin.Y;
            if (scaleX <= 0 || scaleY <= 0) throw new InvalidOperationException("Troop action HUD scale is unavailable.");
            return new Rect((position.X - origin.X) / scaleX, (position.Y - origin.Y) / scaleY,
                (edge.X - position.X) / scaleX, (edge.Y - position.Y) / scaleY);
        }

        private void EnsureActionHost(HUD_Troops panel)
        {
            if (ReferenceEquals(actionPanel, panel) && actionHost != null) return;
            HideActionButtons();
            actionVisuals.Clear();
            actionPanel = panel;
            actionHost = panel.FindName("APISharedTroopActionButtonsHost") as Canvas;
            actionControls = panel.FindName("TroopSelectionControls") as FrameworkElement;
            actionNext = panel.FindName("APISharedTroopActionButtonsNext") as Button;
            if (actionHost == null || actionControls == null || actionNext == null)
                throw new MissingMemberException("APIShared own-troop action XAML host is unavailable.");
            ToolTipService.SetIsEnabled(actionNext, false);
            actionNext.MouseEnter += (sender, args) => { actionNextHovered = true; ShowTroopHover(actionNext, actionNextText); };
            actionNext.MouseLeave += (sender, args) => { actionNextHovered = false; troopHover.Close(actionNext); };
            actionNext.Click += (sender, args) => {
                if (!MainViewModel.viewModelLoaded || !TryGetActionContext(MainViewModel.Instance, out _)) return;
                actionPage = UnitHudActionButtonLayout.NextPage(actionPage, actionVisibleCount, actionCapacity);
                pendingPresentation = true;
            };
            actionPage = 0;
        }

        private ActionButtonVisual GetActionVisual(ActionButtonState state, UnitHudActionButtonContext context)
        {
            ActionButtonRegistration registration = state.Registration;
            if (!actionVisuals.TryGetValue(registration, out ActionButtonVisual visual))
            {
                var button = new Button { Width = 35, Height = 35, Padding = new Thickness(0),
                    Visibility = Visibility.Collapsed, Style = actionPanel.TryFindResource("APISharedTroopActionButtonStyle") as Style,
                    Command = new ActionButtonCommand(this, registration) };
                ToolTipService.SetIsEnabled(button, false);
                button.MouseEnter += (_, __) => SetActionHover(registration, true);
                button.MouseLeave += (_, __) => SetActionHover(registration, false);
                visual = new ActionButtonVisual(button);
                actionVisuals.Add(registration, visual);
                actionHost.Children.Add(button);
            }
            if (visual.Version != state.Version || visual.Colour != context.PlayerColour || visual.Player != context.PlayerId)
            {
                visual.Version = state.Version; visual.Colour = context.PlayerColour; visual.Player = context.PlayerId;
                visual.Failed = false;
                visual.Button.Content = null;
                if (TryCreateActionContent(registration.Definition.ContentFactory, context, registration.Key, out FrameworkElement content))
                    visual.Button.Content = content;
                else visual.Failed = true;
            }
            return visual;
        }

        internal bool TryCreateActionContent(Func<UnitHudActionButtonContext, FrameworkElement> factory,
            UnitHudActionButtonContext context, string key, out FrameworkElement content)
        {
            content = null;
            try
            {
                FrameworkElement candidate = factory(context);
                if (candidate == null || candidate.Parent != null)
                    throw new InvalidOperationException("Action factory must return fresh unattached content.");
                candidate.IsHitTestVisible = false;
                content = candidate;
                return true;
            }
            catch (Exception ex) { LogCallbackFailure("action content " + key, ex); return false; }
        }

        private void FailClosedActionButtons()
        {
            HideActionButtons();
            foreach (ActionButtonState state in CaptureActionButtons()) NotifyActionContext(state.Registration, false);
        }

        private void ResetActionButtonPresentation()
        {
            APIShared.Internal.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() => {
                FailClosedActionButtons();
                actionVisuals.Clear();
                actionPanel = null; actionHost = null; actionControls = null; actionNext = null;
                troopHover.Close();
                recruitmentHover.Close();
                actionPage = 0; actionCapacity = 0; actionVisibleCount = 0;
            });
        }

        private void HideActionButtons(bool closeAllHover = false)
        {
            if (actionHost != null) actionHost.Visibility = Visibility.Collapsed;
            if (actionNext != null) actionNext.Visibility = Visibility.Collapsed;
            actionNextHovered = false;
            if (closeAllHover) troopHover.Close(); else if (actionNext != null) troopHover.Close(actionNext);
            foreach (var pair in actionVisuals)
            {
                pair.Value.Button.Visibility = Visibility.Collapsed;
                if (pair.Key.Hovered) SetActionHover(pair.Key, false);
            }
        }

        private void NotifyActionContext(ActionButtonRegistration item, bool available)
        {
            if (item.ContextAvailable == available) return;
            item.ContextAvailable = available;
            try { item.Definition.ContextChanged?.Invoke(available); }
            catch (Exception ex) { LogCallbackFailure("action context " + item.Key, ex); }
        }

        private void SetActionHover(ActionButtonRegistration item, bool hovered)
        {
            if (hovered) lock (sync) if (!item.Visible || !item.Enabled || !OwnerActive(item.Owner)) hovered = false;
            if (item.Hovered == hovered) return;
            item.Hovered = hovered;
            try { if (hovered) ShowTroopHover(item, item.Tooltip); else troopHover.Close(item); }
            catch (Exception ex) { LogCallbackFailure("action rollover " + item.Key, ex); }
            try { item.Definition.HoverChanged?.Invoke(hovered); }
            catch (Exception ex) { LogCallbackFailure("action hover " + item.Key, ex); }
        }

        private void ShowTroopHover(object origin, string text)
        {
            if (!MainViewModel.viewModelLoaded) return;
            MainViewModel main = MainViewModel.Instance;
            if (!TryGetActionContext(main, out _) || main.HUDTroopPanel.RefTroopsPanelRollover == null) return;
            HUD_Troops panel = main.HUDTroopPanel;
            troopHover.Show(panel, origin, text, () => ReferenceEquals(main.HUDTroopPanel, panel) ? main.TroopsPanelRollover : null,
                value => {
                    main.TroopsPanelRollover = value;
                    main.TroopsPanelRollover_AmountReq1 = string.Empty;
                    main.TroopsPanelRollover_AmountGot1 = string.Empty;
                    main.TroopsPanelRollover_GoodsImage1 = null;
                    panel.RefTroopsPanelRollover.Visibility = Visibility.Visible;
                    if (panel.RefTroopsPanelRollover2 != null) panel.RefTroopsPanelRollover2.Visibility = Visibility.Hidden;
                },
                () => {
                    panel.RefTroopsPanelRollover.Visibility = Visibility.Hidden;
                    if (panel.RefTroopsPanelRollover2 != null) panel.RefTroopsPanelRollover2.Visibility = Visibility.Hidden;
                });
        }
        private sealed class ActionButtonCommand : ICommand
        {
            private readonly UnitHudPresentationService service;
            private readonly ActionButtonRegistration item;
            internal ActionButtonCommand(UnitHudPresentationService service, ActionButtonRegistration item)
            { this.service = service; this.item = item; }
            public event System.EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true; // Live authorization is checked in Execute.
            public void Execute(object parameter)
            {
                try
                {
                    lock (service.sync)
                        if (!item.Visible || !item.Enabled || !service.OwnerActive(item.Owner)) return;
                    if (!MainViewModel.viewModelLoaded || !service.TryGetActionContext(MainViewModel.Instance, out _) ||
                        !service.actionVisuals.TryGetValue(item, out ActionButtonVisual visual) || visual.Failed ||
                        visual.Button.Visibility != Visibility.Visible) return;
                    if (item.Definition.Command.CanExecute(parameter)) item.Definition.Command.Execute(parameter);
                }
                catch (Exception ex) { service.LogCallbackFailure("action command " + item.Key, ex); }
            }
        }

        private sealed class ActionButtonVisual
        {
            internal readonly Button Button;
            internal int Version = -1, Colour = -1, Player = -1;
            internal bool Failed;
            internal ActionButtonVisual(Button button) { Button = button; }
        }

        private sealed class ActionButtonState
        {
            internal readonly ActionButtonRegistration Registration;
            internal readonly bool Visible, Enabled;
            internal readonly string Tooltip;
            internal readonly int Version;
            internal ActionButtonState(ActionButtonRegistration item, bool ownerActive)
            { Registration = item; Visible = ownerActive && item.Visible; Enabled = item.Enabled; Tooltip = item.Tooltip; Version = item.Version; }
        }

        private sealed class ActionButtonRegistration : IUnitHudActionButtonRegistration
        {
            private readonly UnitHudPresentationService service;
            internal readonly string Owner, Key;
            internal readonly UnitHudActionButtonDefinition Definition;
            internal bool Visible = true, Enabled = true, Hovered;
            internal bool? ContextAvailable;
            internal string Tooltip;
            internal int Version;
            internal ActionButtonRegistration(UnitHudPresentationService service, string owner, UnitHudActionButtonDefinition definition)
            { this.service = service; Owner = owner; Definition = definition; Key = owner + ":" + definition.ButtonId; Tooltip = definition.Tooltip; }
            public void SetVisible(bool visible) { lock (service.sync) { Visible = visible; service.pendingPresentation = true; } }
            public void SetEnabled(bool enabled) { lock (service.sync) { Enabled = enabled; service.pendingPresentation = true; } }
            public void SetTooltip(string tooltip)
            {
                if (string.IsNullOrWhiteSpace(tooltip)) throw new ArgumentException("A nonempty tooltip is required.", nameof(tooltip));
                lock (service.sync) { Tooltip = tooltip; service.pendingPresentation = true; }
            }
            public void RequestContentRefresh() { lock (service.sync) { Version++; service.pendingPresentation = true; } }
        }
    }
}

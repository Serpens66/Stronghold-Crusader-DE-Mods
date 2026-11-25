using System;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;

namespace BugfixesAndQoL.AutoSend
{
    internal sealed partial class AutoSendView
    {
        private readonly AutoSendDisclosureState disclosure = new AutoSendDisclosureState();
        private readonly AutoSendPendingDisclosureClick pendingDisclosureClick = new AutoSendPendingDisclosureClick();
        private HUD_AlliesPanel pendingDisclosurePanel;
        private AllyGoodsSectionRegistration sectionRegistration;
        private AllyGoodsFooterRegistration footerRegistration;
        private Border disclosureHost;
        private Button disclosureButton;
        private Noesis.Path disclosureArrow;
        private Geometry disclosureArrowRight, disclosureArrowDown;
        private ToolTip disclosureTip;
        private bool sectionVisible;
        private long disclosureGeneration, disclosurePanelGeneration;

        internal void RegisterDisclosure(AllyGoodsSectionRegistration registration)
        {
            sectionRegistration = registration;
            registration.SetVisible(false);
            RegisterFrame();
            // Layout changes also refresh while simulation is paused.
            registration.LayoutAvailabilityChanged += Refresh;
            if (!AllyGoodsPanelExtensions.TryRegisterFooterControl(BugfixesAndQoLPlugin.PluginGuid, "auto-send-disclosure", CreateDisclosure,
                out footerRegistration, out string reason, 100)) throw new InvalidOperationException(reason);
            footerRegistration.SetVisible(false);
            if (!AllyPanelButtonEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "auto-send-goods-click", null,
                OnAllyButtonCompleted, out reason)) throw new InvalidOperationException(reason);
            if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, BugfixesAndQoLPlugin.PluginGuid,
                "auto-send-after-gui", null, OnDisclosureGuiCompleted, out reason, 200)) throw new InvalidOperationException(reason);
        }

        private void OnAllyButtonCompleted(AllyPanelButtonPostEventArgs args)
        {
            if (!runtime.LocalFeatureEnabled || args.WasSkipped || args.CompletionException != null || !args.OriginalCompleted && !args.WasClaimed ||
                !AllyGoodsPanelExtensions.TryGetContext(out var context)) return;
            if (!MainViewModel.viewModelLoaded || !ReferenceEquals(args.Panel, MainViewModel.Instance.HUDAlliesPanel)) return;
            if (args.Button == "Send" || args.Button == "Request" || args.Button == "Back" || args.Button == "Exit")
            {
                pendingDisclosureClick.Cancel();
                pendingDisclosurePanel = null;
                // Observe command boundaries too: closing and reopening within one
                // frame must not preserve an old expanded state between GUI snapshots.
                disclosure.ObserveMenu(0, false, context.PanelGeneration);
                runtime.RequestViewRefresh(); return;
            }
            string button = args.Button;
            if (!context.IsSendMode || button == null || button.Length != 3 || button[0] != 'G' ||
                button[1] < '0' || button[1] > '9' || button[2] < '0' || button[2] > '9') return;
            int good = (button[1] - '0') * 10 + button[2] - '0';
            if (!AutoSendGoods.Contains(good) || !context.Good.HasValue || (int)context.Good.Value != good) return;
            var data = runtime.ReadDisplay(context);
            if (data.SessionId == 0 || !data.Selected || data.SenderPlayerId != context.LocalPlayerId ||
                context.ViewedPlayerId != context.LocalPlayerId) return;
            pendingDisclosureClick.Record(DisclosureTarget(context, data));
            pendingDisclosurePanel = args.Panel;
        }
        private void OnDisclosureGuiCompleted(PresentationPostEventArgs args)
        {
            if (!args.OriginalCompleted || !pendingDisclosureClick.HasPending) return;
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode ||
                !MainViewModel.viewModelLoaded || !ReferenceEquals(pendingDisclosurePanel, MainViewModel.Instance.HUDAlliesPanel))
            { pendingDisclosureClick.Cancel(); pendingDisclosurePanel = null; return; }
            var data = runtime.ReadDisplay(context);
            pendingDisclosurePanel = null;
            if (!pendingDisclosureClick.TryConsume(DisclosureTarget(context, data))) return;
            disclosure.ObserveMenu(data.SessionId, context.IsSendMode, context.PanelGeneration);
            disclosure.ObserveGoodsClick(data.Rule.Enabled);
            runtime.RequestViewRefresh();
        }
        private static AutoSendDisclosureTarget DisclosureTarget(AllyGoodsPanelContext context, AutoSendDisplay data) =>
            new AutoSendDisclosureTarget(data.SessionId, context.PanelGeneration, context.LocalPlayerId,
                context.ViewedPlayerId, context.RecipientPlayerId, (int)context.Good.GetValueOrDefault());

        private FrameworkElement CreateDisclosure(AllyGoodsSectionContext context)
        {
            long generation = ++disclosureGeneration;
            long panelGeneration = disclosurePanelGeneration = context.Selection.PanelGeneration;
            if (tooltipStyle == null) tooltipStyle = CreateTooltipStyle();
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = AutoSendText.Get("Disclosure"), FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 243, 198)) });
            // Draw the indicator: Vanilla's font does not contain triangle glyphs.
            if (disclosureArrowRight == null) disclosureArrowRight = Geometry.Parse("M0,0 L6,3 L0,6 Z");
            if (disclosureArrowDown == null) disclosureArrowDown = Geometry.Parse("M0,0 L6,0 L3,6 Z");
            disclosureArrow = new Noesis.Path { Width = 6, Height = 6, Margin = new Thickness(3, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center, Data = disclosureArrowRight,
                Fill = new SolidColorBrush(Color.FromArgb(255, 239, 243, 198)) };
            content.Children.Add(disclosureArrow);
            var button = new Button { Name = "AutoSendDisclosureButton", Content = content,
                MinWidth = 0, MinHeight = 0, Padding = new Thickness(4, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(120, 29, 23, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(130, 242, 212, 138)), BorderThickness = new Thickness(1),
                Template = GUI.ParseXaml(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='#DD453922'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>") as ControlTemplate };
            disclosureButton = button;
            disclosureTip = AttachTooltip(button, "DisclosureHelp");
            var host = new Border { Name = "AutoSendDisclosureHost", Child = button };
            disclosureHost = host;
            // Parent preview cancels before a focus change could submit a draft.
            // A callback retained by an old HUD never targets its replacement.
            host.PreviewMouseDown += (_, args) => {
                if (IsCurrentDisclosure(generation, panelGeneration) && ReferenceEquals(disclosureHost, host) &&
                    args.ChangedButton == MouseButton.Left && disclosure.Expanded) keyboardCapture?.Release();
            };
            button.Click += (_, __) => {
                if (IsCurrentDisclosure(generation, panelGeneration) && ReferenceEquals(disclosureButton, button)) ToggleDisclosure();
            };
            host.Loaded += (_, __) => {
                if (IsCurrentDisclosure(generation, panelGeneration) && ReferenceEquals(disclosureHost, host)) Refresh();
            };
            // Reparenting into the broker's overflow may raise Unloaded. Keep this
            // control's events and local disclosure state until a new factory generation.
            Refresh();
            return host;
        }

        private bool IsCurrentDisclosure(long generation, long panelGeneration) => generation == disclosureGeneration &&
            panelGeneration == disclosurePanelGeneration && AllyGoodsPanelExtensions.TryGetContext(out var context) &&
            context.PanelGeneration == panelGeneration;

        private void ToggleDisclosure()
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode) return;
            var data = runtime.ReadDisplay(context);
            if (!CanDisclose(context, data, out _)) return;
            pendingDisclosureClick.Cancel();
            pendingDisclosurePanel = null;
            disclosure.ObserveMenu(data.SessionId, context.IsSendMode, context.PanelGeneration);
            disclosure.Toggle(data.SessionId);
            Refresh();
        }

        private bool CanDisclose(AllyGoodsPanelContext context, AutoSendDisplay data, out string reasonKey)
        {
            bool layoutAvailable = sectionRegistration != null && sectionRegistration.TryGetLayoutAvailability(out _);
            bool ownView = data.SenderPlayerId < 1 || context.ViewedPlayerId == data.SenderPlayerId;
            reasonKey = AutoSendDisclosureState.UnavailableReason(data.SenderReady, data.TransferUnavailable, data.IdentityMismatch,
                ownView, data.RecipientReadiness, layoutAvailable);
            return reasonKey == null;
        }

        private bool RefreshDisclosure(AllyGoodsPanelContext context, AutoSendDisplay data)
        {
            disclosure.ObserveMenu(data.SessionId, context.IsSendMode, context.PanelGeneration);
            if (!context.IsSendMode) { pendingDisclosureClick.Cancel(); pendingDisclosurePanel = null; }
            bool available = CanDisclose(context, data, out string reasonKey);
            bool visible = disclosure.IsVisible(context.IsSendMode, available);
            footerRegistration?.SetVisible(context.IsSendMode);
            // Keep the parent enabled so disabled-button tooltips remain reachable.
            if (disclosureButton != null && disclosureButton.IsEnabled != available) disclosureButton.IsEnabled = available;
            var arrow = disclosure.Expanded ? disclosureArrowDown : disclosureArrowRight;
            if (disclosureArrow != null && !ReferenceEquals(disclosureArrow.Data, arrow)) disclosureArrow.Data = arrow;
            string help = AutoSendText.Get(reasonKey ?? "DisclosureHelp");
            if (disclosureTip != null && !Equals(disclosureTip.Content, help)) disclosureTip.Content = help;
            ApplyDisclosure(visible);
            return visible;
        }
        private void ApplyDisclosure(bool visible)
        {
            if (sectionVisible == visible) return;
            if (!visible && sectionVisible) keyboardCapture?.Release();
            sectionVisible = visible;
            sectionRegistration?.SetVisible(visible);
            frameRegistration?.SetEnabled(visible);
        }
    }
}

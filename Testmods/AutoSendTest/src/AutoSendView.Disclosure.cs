using System;
using APIShared.Presentation;
using Noesis;

namespace AutoSendTest
{
    internal sealed partial class AutoSendView
    {
        private readonly AutoSendDisclosureState disclosure = new AutoSendDisclosureState();
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
            if (!AllyGoodsPanelExtensions.TryRegisterFooterControl(AutoSendPlugin.Guid, "auto-send-disclosure", CreateDisclosure,
                out footerRegistration, out string reason, 100)) throw new InvalidOperationException(reason);
        }

        private FrameworkElement CreateDisclosure(AllyGoodsSectionContext context)
        {
            long generation = ++disclosureGeneration;
            long panelGeneration = disclosurePanelGeneration = context.Selection.PanelGeneration;
            AutoSendText.Load();
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
            bool available = CanDisclose(context, data, out string reasonKey);
            bool visible = disclosure.IsVisible(context.IsSendMode, available);
            footerRegistration?.SetVisible(context.IsSendMode);
            // Keep the parent enabled so disabled-button tooltips remain reachable.
            if (disclosureButton != null) disclosureButton.IsEnabled = available;
            if (disclosureArrow != null) disclosureArrow.Data = disclosure.Expanded ? disclosureArrowDown : disclosureArrowRight;
            if (disclosureTip != null) disclosureTip.Content = AutoSendText.Get(reasonKey ?? "DisclosureHelp");
            ApplyDisclosure(visible);
            return visible;
        }
        private void ApplyDisclosure(bool visible)
        {
            if (!visible && sectionVisible) keyboardCapture?.Release();
            sectionVisible = visible;
            sectionRegistration?.SetVisible(visible);
            frameRegistration?.SetEnabled(visible);
        }
    }
}

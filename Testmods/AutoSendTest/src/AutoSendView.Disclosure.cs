using System;
using APIShared.Presentation;
using Noesis;
using SHCDESE.API;
using SHCDESE.NoesisUtil;

namespace AutoSendTest
{
    internal sealed partial class AutoSendView
    {
        private const string DisclosureHostName = "AutoSendDisclosureHost";
        private readonly AutoSendDisclosureState disclosure = new AutoSendDisclosureState();
        private AllyGoodsSectionRegistration sectionRegistration;
        private ContentControl disclosureHost;
        private Button disclosureButton;
        private TextBlock disclosureArrow;
        private bool sectionVisible;

        internal void RegisterDisclosure(AllyGoodsSectionRegistration registration)
        {
            sectionRegistration = registration;
            registration.SetVisible(false);
            GameXAMLManagerAPI.Instance.RegisterBinding(DisclosureHostName, new AutoSendDisclosureBinding(BindDisclosure));
        }
        private void BindDisclosure(FrameworkElement element)
        {
            if (ReferenceEquals(disclosureHost, element)) return;
            DetachDisclosure();
            disclosureHost = element as ContentControl ?? throw new InvalidOperationException("Auto Send disclosure host must be a ContentControl.");
            AutoSendText.Load();
            if (tooltipStyle == null) tooltipStyle = CreateTooltipStyle();
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = AutoSendText.Get("Disclosure"), FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 243, 198)) });
            disclosureArrow = new TextBlock { Width = 12, TextAlignment = TextAlignment.Center, FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 243, 198)) };
            content.Children.Add(disclosureArrow);
            disclosureButton = new Button { Name = "AutoSendDisclosureButton", Content = content,
                MinWidth = 0, MinHeight = 0, Padding = new Thickness(4, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(120, 29, 23, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(130, 242, 212, 138)), BorderThickness = new Thickness(1),
                Template = GUI.ParseXaml(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='#DD453922'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>") as ControlTemplate };
            AttachTooltip(disclosureButton, "DisclosureHelp");
            disclosureButton.Click += ToggleDisclosure;
            disclosureHost.Content = disclosureButton;
            // Cancel before the button can move focus: TextBox.LostFocus would
            // otherwise submit a half-finished draft before the collapse Click.
            disclosureHost.PreviewMouseDown += DisclosurePointerDown;
            disclosureHost.Unloaded += DisclosureUnloaded;
            Refresh();
        }
        private void DisclosureUnloaded(object sender, RoutedEventArgs args) => DetachDisclosure();
        private void DetachDisclosure()
        {
            if (disclosureButton != null) disclosureButton.Click -= ToggleDisclosure;
            if (disclosureHost != null)
            {
                disclosureHost.PreviewMouseDown -= DisclosurePointerDown;
                disclosureHost.Unloaded -= DisclosureUnloaded;
            }
            disclosureHost = null; disclosureButton = null; disclosureArrow = null;
        }
        private void DisclosurePointerDown(object sender, MouseButtonEventArgs args)
        {
            if (args.ChangedButton == MouseButton.Left && disclosure.Expanded) keyboardCapture?.Release();
        }
        private void ToggleDisclosure(object sender, RoutedEventArgs args)
        {
            if (!AllyGoodsPanelExtensions.TryGetContext(out var context) || !context.IsSendMode) return;
            disclosure.Toggle(runtime.ReadDisplay(context).SessionId);
            Refresh();
        }
        private bool RefreshDisclosure(AllyGoodsPanelContext context, long session)
        {
            bool visible = disclosure.IsVisible(context.IsSendMode);
            if (disclosureHost != null) disclosureHost.Visibility = context.IsSendMode ? Visibility.Visible : Visibility.Collapsed;
            if (disclosureButton != null) disclosureButton.IsEnabled = session != 0;
            if (disclosureArrow != null) disclosureArrow.Text = disclosure.Expanded ? "▾" : "▸";
            ApplyDisclosure(visible);
            return visible;
        }
        private void ApplyDisclosure(bool visible)
        {
            if (!visible && sectionVisible) keyboardCapture?.Release();
            sectionVisible = visible;
            sectionRegistration?.SetVisible(visible);
        }
    }

    // Extender rebinding recreates only our own launcher; Vanilla's DataContext
    // and commands are untouched. The binding publisher roots this receiver.
    public sealed class AutoSendDisclosureBinding : INoesisElementBindingAware
    {
        private readonly Action<FrameworkElement> bound;
        private FrameworkElement host;
        internal AutoSendDisclosureBinding(Action<FrameworkElement> bound) { this.bound = bound; }
        void INoesisElementBindingAware.OnNoesisElementBound(FrameworkElement element)
        {
            if (!ReferenceEquals(host, element))
            {
                if (host != null) host.Loaded -= OnLoaded;
                host = element; host.Loaded += OnLoaded;
            }
            bound(element);
        }
        // Reattaching an existing view does not necessarily call Extender's load
        // hook again. Restore our launcher after its Unloaded cleanup as well.
        private void OnLoaded(object sender, RoutedEventArgs args)
        { if (ReferenceEquals(sender, host)) bound(host); }
    }
}

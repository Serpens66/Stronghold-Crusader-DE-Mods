using CrusaderDE;
using Noesis;
using System;

namespace Shared
{
    // Routes lobby hints through the game's existing bottom help panel.
    public static class AiSettingsHelpHover
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(AiSettingsHelpHover),
                new PropertyMetadata(false, OnEnabledChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached(
                "Text",
                typeof(string),
                typeof(AiSettingsHelpHover),
                new PropertyMetadata(string.Empty, OnTextChanged));

        private static FrameworkElement hoveredElement;
        private static FrameworkElement ownerElement;
        private static string ownedText;
        private static FrameworkElement scope;
        private static Popup dropdown;

        public static void RegisterScope(FrameworkElement card, Popup dropdownPopup = null)
        {
            if (ReferenceEquals(scope, card) && ReferenceEquals(dropdown, dropdownPopup))
                return;

            ClearCurrentHelp();
            if (scope != null)
            {
                scope.MouseLeave -= OnScopeMouseLeave;
                scope.IsVisibleChanged -= OnScopeVisibleChanged;
                scope.Unloaded -= OnScopeUnloaded;
            }
            if (dropdown != null)
            {
                dropdown.MouseLeave -= OnDropdownMouseLeave;
                dropdown.Closed -= OnDropdownClosed;
            }

            scope = card;
            dropdown = dropdownPopup;
            if (scope != null)
            {
                scope.MouseLeave += OnScopeMouseLeave;
                scope.IsVisibleChanged += OnScopeVisibleChanged;
                scope.Unloaded += OnScopeUnloaded;
            }
            if (dropdown != null)
            {
                dropdown.MouseLeave += OnDropdownMouseLeave;
                dropdown.Closed += OnDropdownClosed;
            }
        }

        public static bool GetEnabled(DependencyObject element) =>
            element != null && element.GetValue(EnabledProperty) is bool enabled && enabled;

        public static void SetEnabled(DependencyObject element, bool enabled) =>
            element?.SetValue(EnabledProperty, enabled);

        public static string GetText(DependencyObject element) =>
            element?.GetValue(TextProperty) as string ?? string.Empty;

        public static void SetText(DependencyObject element, string value) =>
            element?.SetValue(TextProperty, value ?? string.Empty);

        private static void OnEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is FrameworkElement element))
                return;

            element.MouseEnter -= OnMouseEnter;
            element.MouseLeave -= OnMouseLeave;
            element.IsVisibleChanged -= OnIsVisibleChanged;
            element.Unloaded -= OnUnloaded;
            if (args.NewValue is bool enabled && enabled)
            {
                element.MouseEnter += OnMouseEnter;
                element.MouseLeave += OnMouseLeave;
                element.IsVisibleChanged += OnIsVisibleChanged;
                element.Unloaded += OnUnloaded;
            }
            else
            {
                ClearIfOwned(element);
            }
        }

        private static void OnTextChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is FrameworkElement element) ||
                !ReferenceEquals(hoveredElement, element) ||
                !ReferenceEquals(ownerElement, element))
                return;

            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel == null ||
                !string.Equals(viewModel.AI_Settings_Help, ownedText, StringComparison.Ordinal))
            {
                hoveredElement = null;
                ownerElement = null;
                ownedText = null;
                return;
            }

            string updated = args.NewValue as string ?? string.Empty;
            if (string.IsNullOrEmpty(updated))
                ReleaseHover(element);
            else
                ShowHelp(element, updated);
        }

        private static void OnMouseEnter(object sender, MouseEventArgs args)
        {
            if (!(sender is FrameworkElement element) || !element.IsVisible)
                return;

            string text = GetText(element);
            if (!string.IsNullOrEmpty(text))
                ShowHelp(element, text);
        }

        private static void OnMouseLeave(object sender, MouseEventArgs args) =>
            ReleaseHover(sender as FrameworkElement);

        private static void OnIsVisibleChanged(
            object sender,
            DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue is bool visible && !visible)
                ReleaseHover(sender as FrameworkElement);
        }

        private static void OnUnloaded(object sender, RoutedEventArgs args) =>
            ReleaseHover(sender as FrameworkElement);

        public static void ShowHelp(FrameworkElement element, string text)
        {
            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel == null || element == null || string.IsNullOrEmpty(text))
                return;

            hoveredElement = element;
            ownerElement = element;
            ownedText = text;
            if (viewModel.FRONTMultiplayer != null)
                viewModel.FRONTMultiplayer.hideToolTipTime = DateTime.MinValue;
            viewModel.AI_Settings_Help = text;
            viewModel.Show_AI_Settings_Help = true;
        }

        public static void ReleaseHover(FrameworkElement element)
        {
            if (ReferenceEquals(hoveredElement, element))
                hoveredElement = null;
        }

        public static void ClearIfOwned(FrameworkElement element)
        {
            if (element == null || !ReferenceEquals(ownerElement, element))
                return;

            ClearCurrentHelp();
        }

        public static void ClearIfShowing(string text)
        {
            if (ownerElement != null &&
                string.Equals(ownedText, text, StringComparison.Ordinal))
                ClearCurrentHelp();
        }

        public static void ClearCurrentHelp()
        {
            if (ownerElement == null)
                return;

            string text = ownedText;
            hoveredElement = null;
            ownerElement = null;
            ownedText = null;
            MainViewModel viewModel = MainViewModel.Instance;
            if (viewModel != null &&
                string.Equals(viewModel.AI_Settings_Help, text, StringComparison.Ordinal))
            {
                viewModel.AI_Settings_Help = string.Empty;
                viewModel.Show_AI_Settings_Help = false;
            }
        }

        private static void OnScopeMouseLeave(object sender, MouseEventArgs args)
        {
            if (dropdown?.IsOpen != true)
                ClearCurrentHelp();
        }

        private static void OnScopeVisibleChanged(
            object sender,
            DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue is bool visible && !visible)
                ClearCurrentHelp();
        }

        private static void OnScopeUnloaded(object sender, RoutedEventArgs args) =>
            ClearCurrentHelp();

        private static void OnDropdownMouseLeave(object sender, MouseEventArgs args)
        {
            if (scope?.IsMouseOver != true)
                ClearCurrentHelp();
        }

        private static void OnDropdownClosed(object sender, Noesis.EventArgs args)
        {
            if (scope?.IsMouseOver != true)
                ClearCurrentHelp();
        }
    }
}

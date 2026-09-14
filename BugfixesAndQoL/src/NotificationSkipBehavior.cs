// Feature: Bind complete notification skipping to the real Noesis right-click event on RadarME.
using Noesis;
using NoesisApp;
using System;

namespace BugfixesAndQoL
{
    public static class NotificationSkipBehavior
    {
        private static NotificationSkipFeature feature;

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(NotificationSkipBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject value) =>
            value != null && value.GetValue(IsEnabledProperty) is bool enabled && enabled;

        public static void SetIsEnabled(DependencyObject value, bool enabled) =>
            value?.SetValue(IsEnabledProperty, enabled);

        internal static void Configure(NotificationSkipFeature value) =>
            feature = value ?? throw new ArgumentNullException(nameof(value));

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is MediaElement mediaElement))
                return;

            mediaElement.PreviewMouseDown -= OnPreviewMouseDown;
            if (args.NewValue is bool enabled && enabled)
                mediaElement.PreviewMouseDown += OnPreviewMouseDown;
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (!(sender is MediaElement) ||
                args == null ||
                args.ChangedButton != MouseButton.Right ||
                args.ClickCount != 1)
            {
                return;
            }

            feature?.CompleteFromRadarVideoRightClick(args);
        }
    }
}

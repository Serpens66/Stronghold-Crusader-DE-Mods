// Feature: Bind complete notification skipping to the real Noesis right-click events on the notification surfaces.
using Noesis;
using NoesisApp;
using System;

namespace BugfixesAndQoL
{
    internal enum NotificationSkipSurface
    {
        Video,
        Minimap
    }

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
            if (!(dependencyObject is UIElement element))
                return;

            element.PreviewMouseDown -= OnPreviewMouseDown;
            if (args.NewValue is bool enabled && enabled)
                element.PreviewMouseDown += OnPreviewMouseDown;
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (args == null ||
                args.ChangedButton != MouseButton.Right ||
                args.ClickCount != 1)
            {
                return;
            }

            NotificationSkipSurface surface;
            if (sender is MediaElement)
                surface = NotificationSkipSurface.Video;
            else if (sender is Image)
                surface = NotificationSkipSurface.Minimap;
            else
                return;

            feature?.CompleteFromNotificationSurfaceRightClick(surface, args);
        }
    }
}

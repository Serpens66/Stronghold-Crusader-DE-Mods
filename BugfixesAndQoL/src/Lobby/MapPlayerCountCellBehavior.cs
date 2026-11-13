// Feature: Present the maximum playable map slots in the editor map list.
using CrusaderDE;
using Noesis;

namespace BugfixesAndQoL
{
    public static class MapPlayerCountCellBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(MapPlayerCountCellBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject value) =>
            value != null && value.GetValue(IsEnabledProperty) is bool enabled && enabled;

        public static void SetIsEnabled(DependencyObject value, bool enabled) =>
            value?.SetValue(IsEnabledProperty, enabled);

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is TextBlock textBlock))
                return;

            textBlock.Loaded -= OnLoaded;
            textBlock.DataContextChanged -= OnDataContextChanged;

            if (args.NewValue is bool enabled && enabled)
            {
                textBlock.Loaded += OnLoaded;
                textBlock.DataContextChanged += OnDataContextChanged;
                UpdateText(textBlock);
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs args) =>
            UpdateText(sender as TextBlock);

        private static void OnDataContextChanged(
            object sender,
            DependencyPropertyChangedEventArgs args) =>
            UpdateText(sender as TextBlock);

        private static void UpdateText(TextBlock textBlock)
        {
            if (textBlock == null)
                return;

            FileHeader header = (textBlock.DataContext as FileRow)?.fileHeader;
            textBlock.Text =
                VanillaMapEditorPolicy.FormatPlayerCount(header?.maxPlayers ?? 0);
        }
    }
}

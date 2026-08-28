using Noesis;
using SHCDESE.UI;
using System;

namespace SerpsModsHost
{
    /// <summary>
    /// Builds a second view of an existing setting without bypassing its original binding.
    /// Unsupported binding shapes deliberately remain navigation-only search results.
    /// </summary>
    internal static class ModSettingsSearchEditorFactory
    {
        public static FrameworkElement Create(
            FrameworkElement source,
            string title,
            string toolTip,
            object fallbackDataContext)
        {
            if (source == null)
                return null;

            FrameworkElement editor = null;
            if (source is CheckBox sourceCheckBox)
                editor = CreateCheckBox(sourceCheckBox);
            else if (source is Slider sourceSlider)
                editor = CreateSlider(sourceSlider);
            else if (source is ComboBox sourceComboBox)
                editor = CreateComboBox(sourceComboBox);
            else if (source is TextBox sourceTextBox)
                editor = CreateTextBox(sourceTextBox);
            else if (source is Button sourceButton)
                editor = CreateButton(sourceButton, title);

            if (editor == null)
                return null;

            if (source is Control sourceControl && editor is Control editorControl)
                CopyControlPresentation(sourceControl, editorControl);

            // Unopened tabs have not inherited their root DataContext through a visual tree yet.
            editor.DataContext = source.DataContext ?? fallbackDataContext;
            editor.ToolTip = string.IsNullOrWhiteSpace(toolTip) ? title ?? string.Empty : toolTip;
            ToolTipService.SetShowDuration(editor, 60000);
            BindingOperations.SetBinding(
                editor,
                UIElement.IsEnabledProperty,
                new Binding(nameof(UIElement.IsEnabled), source) { Mode = BindingMode.OneWay });
            if (!IsLogicallyVisible(source))
                editor.Visibility = Visibility.Collapsed;
            return editor;
        }

        private static void CopyControlPresentation(Control source, Control editor)
        {
            // Reuse the resolved original style and its local presentation values. Layout width
            // stays bounded by the result card so a single setting cannot create side scrolling.
            editor.Style = source.Style;
            editor.Foreground = source.Foreground;
            editor.Background = source.Background;
            editor.BorderBrush = source.BorderBrush;
            editor.BorderThickness = source.BorderThickness;
            editor.FontFamily = source.FontFamily;
            editor.FontSize = source.FontSize;
            editor.FontStretch = source.FontStretch;
            editor.FontStyle = source.FontStyle;
            editor.FontWeight = source.FontWeight;
            editor.Padding = source.Padding;
            editor.Opacity = source.Opacity;
            editor.MinWidth = Math.Min(260.0f, Math.Max(0.0f, source.MinWidth));
            editor.MaxWidth = Math.Min(260.0f, ResolveMaximumWidth(source.MaxWidth));
        }

        private static float ResolveMaximumWidth(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f ? 260.0f : value;

        private static float ResolveEditorWidth(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0.0f
                ? fallback
                : Math.Min(260.0f, value);

        private static CheckBox CreateCheckBox(CheckBox source)
        {
            Binding binding = CloneBinding(source, ToggleButton.IsCheckedProperty);
            if (binding == null)
                return null;
            var editor = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsThreeState = source.IsThreeState,
                Content = source.Content is string ? source.Content : null
            };
            BindingOperations.SetBinding(editor, ToggleButton.IsCheckedProperty, binding);
            return editor;
        }

        private static Slider CreateSlider(Slider source)
        {
            Binding binding = CloneBinding(source, RangeBase.ValueProperty);
            if (binding == null)
                return null;
            var editor = new Slider
            {
                Minimum = source.Minimum,
                Maximum = source.Maximum,
                SmallChange = source.SmallChange,
                LargeChange = source.LargeChange,
                TickFrequency = source.TickFrequency,
                IsSnapToTickEnabled = source.IsSnapToTickEnabled,
                Orientation = source.Orientation,
                Width = ResolveEditorWidth(source.Width, 220.0f),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            BindingOperations.SetBinding(editor, RangeBase.ValueProperty, binding);
            return editor;
        }

        private static ComboBox CreateComboBox(ComboBox source)
        {
            DependencyProperty selectionProperty = FindBoundProperty(
                source,
                Selector.SelectedValueProperty,
                Selector.SelectedItemProperty,
                Selector.SelectedIndexProperty);
            Binding selectionBinding = selectionProperty == null
                ? null
                : CloneBinding(source, selectionProperty);
            if (selectionBinding == null)
                return null;

            var editor = new ComboBox
            {
                Width = ResolveEditorWidth(source.Width, 220.0f),
                HorizontalAlignment = HorizontalAlignment.Left,
                DisplayMemberPath = source.DisplayMemberPath,
                SelectedValuePath = source.SelectedValuePath,
                ItemTemplate = source.ItemTemplate,
                MaxDropDownHeight = source.MaxDropDownHeight,
                IsEditable = source.IsEditable,
                IsReadOnly = source.IsReadOnly
            };
            Binding itemsBinding = CloneBinding(source, ItemsControl.ItemsSourceProperty);
            if (itemsBinding != null)
                BindingOperations.SetBinding(editor, ItemsControl.ItemsSourceProperty, itemsBinding);
            else
                editor.ItemsSource = source.ItemsSource;
            BindingOperations.SetBinding(editor, selectionProperty, selectionBinding);
            return editor;
        }

        private static TextBox CreateTextBox(TextBox source)
        {
            Binding binding = CloneBinding(source, TextBox.TextProperty);
            if (binding == null)
                return null;
            var editor = new TextBox
            {
                MinWidth = Math.Min(260.0f, Math.Max(0.0f, source.MinWidth)),
                MaxWidth = Math.Min(220.0f, ResolveMaximumWidth(source.MaxWidth)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(4.0f, 2.0f, 4.0f, 2.0f),
                TextAlignment = source.TextAlignment,
                AcceptsReturn = source.AcceptsReturn,
                MaxLength = source.MaxLength,
                TextWrapping = source.TextWrapping,
                IsReadOnly = source.IsReadOnly
            };
            BindingOperations.SetBinding(editor, TextBox.TextProperty, binding);
            KeyboardCaptureBinding.SetEnabled(editor, true);
            return editor;
        }

        private static Button CreateButton(Button source, string title)
        {
            Binding commandBinding = CloneBinding(source, ButtonBase.CommandProperty);
            if (commandBinding == null)
                return null;
            var editor = new Button
            {
                Content = source.Content is string ? source.Content : title ?? string.Empty,
                CommandParameter = source.CommandParameter,
                Padding = new Thickness(8.0f, 3.0f, 8.0f, 3.0f),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            BindingOperations.SetBinding(editor, ButtonBase.CommandProperty, commandBinding);
            Binding parameterBinding = CloneBinding(source, ButtonBase.CommandParameterProperty);
            if (parameterBinding != null)
                BindingOperations.SetBinding(editor, ButtonBase.CommandParameterProperty, parameterBinding);
            return editor;
        }

        private static DependencyProperty FindBoundProperty(
            DependencyObject source,
            params DependencyProperty[] properties)
        {
            foreach (DependencyProperty property in properties)
            {
                if (BindingOperations.GetBinding(source, property) != null)
                    return property;
            }
            return null;
        }

        private static Binding CloneBinding(DependencyObject source, DependencyProperty property)
        {
            Binding original = BindingOperations.GetBinding(source, property);
            if (original == null || !string.IsNullOrWhiteSpace(original.ElementName) || original.RelativeSource != null)
                return null;

            var clone = new Binding(original.Path?.Path ?? string.Empty)
            {
                Mode = original.Mode,
                UpdateSourceTrigger = original.UpdateSourceTrigger,
                Converter = original.Converter,
                ConverterParameter = original.ConverterParameter,
                Delay = original.Delay,
                StringFormat = original.StringFormat
            };
            object bindingSource = original.Source;
            object targetNullValue = original.TargetNullValue;
            object fallbackValue = original.FallbackValue;
            if (HasConcreteValue(bindingSource))
                clone.Source = bindingSource;
            if (HasConcreteValue(targetNullValue))
                clone.TargetNullValue = targetNullValue;
            if (HasConcreteValue(fallbackValue))
                clone.FallbackValue = fallbackValue;
            return clone;
        }

        private static bool HasConcreteValue(object value) =>
            value != null && !Equals(value, DependencyProperty.UnsetValue);

        private static bool IsLogicallyVisible(FrameworkElement source)
        {
            DependencyObject current = source;
            while (current is FrameworkElement element)
            {
                if (element.Visibility != Visibility.Visible)
                    return false;
                current = VisualTreeHelper.GetParent(current);
            }
            return true;
        }
    }
}

using CrusaderDE;
using Noesis;
using System;
using System.ComponentModel;

namespace ExtendedData
{
    public static class TrailMakerAdvancedHelp
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(TrailMakerAdvancedHelp), new PropertyMetadata(string.Empty, OnTextChanged));

        private static CheckBox owner;
        private static MainViewModel viewModel;
        private static string ownedText;

        public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);
        public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

        private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
        {
            if (!(element is CheckBox checkbox))
                return;

            checkbox.MouseEnter -= OnMouseEnter;
            checkbox.MouseLeave -= OnMouseLeave;
            checkbox.IsVisibleChanged -= OnVisibleChanged;
            checkbox.Unloaded -= OnUnloaded;
            Clear(checkbox);

            if (string.IsNullOrWhiteSpace(args.NewValue as string))
                return;

            checkbox.MouseEnter += OnMouseEnter;
            checkbox.MouseLeave += OnMouseLeave;
            checkbox.IsVisibleChanged += OnVisibleChanged;
            checkbox.Unloaded += OnUnloaded;
        }

        private static void OnMouseEnter(object sender, MouseEventArgs args)
        {
            if (!(sender is CheckBox checkbox) || !checkbox.IsVisible || !MainViewModel.viewModelLoaded)
                return;

            MainViewModel current = MainViewModel.Instance;
            if (current?.FRONTMultiplayer?.RefEnableAdvancedSkirmishCheck != checkbox ||
                !current.MultiplayerTrailMakerMode || !current.Show_MP_SkirmishAdvanced)
                return;

            if (!ReferenceEquals(owner, checkbox))
            {
                Clear(owner);
                owner = checkbox;
                viewModel = current;
                current.PropertyChanged += OnMenuStateChanged;
            }
            ownedText = GetText(checkbox);
            current.MPGame_Type_Description = ownedText;
            // The vanilla setter also cancels the previous control's delayed hide.
            current.Show_MPGame_Type_Description = true;
        }

        private static void OnMouseLeave(object sender, MouseEventArgs args) => Clear(sender as CheckBox);
        private static void OnUnloaded(object sender, RoutedEventArgs args) => Clear(sender as CheckBox);

        private static void OnMenuStateChanged(object sender, PropertyChangedEventArgs args)
        {
            if (sender is MainViewModel current && ReferenceEquals(current, viewModel) &&
                (args.PropertyName == nameof(MainViewModel.Show_MP_SkirmishAdvanced) ||
                 args.PropertyName == nameof(MainViewModel.MultiplayerTrailMakerMode)))
            {
                if (!current.Show_MP_SkirmishAdvanced || !current.MultiplayerTrailMakerMode)
                    Clear(owner);
            }
        }

        private static void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (args.NewValue is bool visible && !visible)
                Clear(sender as CheckBox);
        }

        private static void Clear(CheckBox checkbox)
        {
            if (owner == null || !ReferenceEquals(owner, checkbox))
                return;

            MainViewModel previous = viewModel;
            string text = ownedText;
            previous.PropertyChanged -= OnMenuStateChanged;
            owner = null;
            viewModel = null;
            ownedText = null;
            if (previous.FRONTMultiplayer == null ||
                !string.Equals(previous.MPGame_Type_Description, text, StringComparison.Ordinal))
                return;

            previous.MPGame_Type_Description = string.Empty;
            previous.Show_MPGame_Type_Description = false;
        }
    }
}

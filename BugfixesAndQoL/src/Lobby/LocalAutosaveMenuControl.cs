using APIShared.Presentation;
using Noesis;
using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    // Each menu view owns its editor. The runtime owns the delayed, mission-bound choice.
    internal sealed class LocalAutosaveMenuControl : ContentControl
    {
        private readonly IngameMenuContentContext context;
        private readonly Func<int> getMinutes;
        private readonly Action<int> setPending;
        private readonly Action invalidDraft;
        private readonly Action cancelPending;
        private readonly TextBox input;
        private readonly TextBlock unit;
        private bool updating;
        private bool cancelled;
        private string Off => SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveOff");

        internal LocalAutosaveMenuControl(IngameMenuContentContext context, Func<int> getMinutes,
            Action<int> setPending, Action invalidDraft, Action cancelPending)
        {
            this.context = context;
            this.getMinutes = getMinutes;
            this.setPending = setPending;
            this.invalidDraft = invalidDraft;
            this.cancelPending = cancelPending;
            FontSize = 14;
            Height = 31;
            Focusable = false;

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock {
                Text = SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveLabel"),
                FontSize = FontSize, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 4, 0)
            };
            grid.Children.Add(label);
            var editor = new Grid();
            editor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editor.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            input = new TextBox {
                FontSize = FontSize, Foreground = Brushes.White, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Padding = new Thickness(3, 1, 1, 0),
                TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                MaxLength = 32
            };
            editor.Children.Add(input);
            var arrows = new Grid();
            arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(arrows, 1);
            arrows.Children.Add(Arrow("\u25b4", 1, 0));
            arrows.Children.Add(Arrow("\u25be", -1, 1));
            editor.Children.Add(arrows);
            var border = new Border {
                Background = new SolidColorBrush(Color.FromArgb(255, 31, 33, 27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 151, 137, 104)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Child = editor
            };
            Grid.SetColumn(border, 1);
            grid.Children.Add(border);
            unit = new TextBlock {
                FontSize = FontSize, Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0)
            };
            Grid.SetColumn(unit, 2);
            grid.Children.Add(unit);
            Content = grid;
            input.GotKeyboardFocus += (sender, args) => { cancelled = false; input.SelectAll(); TextChanged(); };
            input.PreviewMouseDown += (sender, args) => TextChanged();
            input.TextChanged += (sender, args) => TextChanged();
            input.PreviewKeyDown += OnEditorKeyDown;
            input.LostKeyboardFocus += (sender, args) => {
                if (!IsVisible || cancelled) return;
                if (!Commit()) context.TryExecute(cancelPending);
                Restore();
            };
            IsVisibleChanged += (sender, args) => { if (IsVisible) Restore(); };
            Loaded += (sender, args) => Restore();
            Restore();
        }

        private RepeatButton Arrow(string text, int delta, int row)
        {
            var button = new RepeatButton {
                Content = text, FontSize = 11, Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(255, 51, 49, 55)),
                BorderThickness = new Thickness(0), Padding = new Thickness(0),
                Focusable = false, Delay = 400, Interval = 90,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(button, row);
            button.Click += (sender, args) => {
                if (context.TryExecute(() => setPending(LocalAutosaveInputPolicy.Step(input.Text, Off, getMinutes(), delta))))
                    Restore();
                args.Handled = true;
            };
            return button;
        }

        private void TextChanged()
        {
            if (updating) return;
            cancelled = false;
            if (LocalAutosaveInputPolicy.TryParse(input.Text, Off, out int minutes))
                context.TryExecute(() => setPending(minutes));
            else
                context.TryExecute(invalidDraft);
        }

        private bool Commit()
        {
            if (!LocalAutosaveInputPolicy.TryParse(input.Text, Off, out int minutes)) return false;
            return context.TryExecute(() => setPending(minutes));
        }

        private void OnEditorKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                cancelled = true;
                context.TryExecute(cancelPending);
                Restore();
                // Do not consume Escape: Vanilla still closes the menu and restores pause.
                return;
            }
            if (args.Key == Key.Return || args.Key == Key.Enter)
            {
                if (Commit()) Restore();
                args.Handled = true;
            }
            else if (args.Key == Key.Up || args.Key == Key.Down)
            {
                int delta = args.Key == Key.Up ? 1 : -1;
                if (context.TryExecute(() => setPending(LocalAutosaveInputPolicy.Step(input.Text, Off, getMinutes(), delta)))) Restore();
                args.Handled = true;
            }
            else TextChanged(); // Cursor/selection editing also restarts the inactivity window.
        }

        private void Restore()
        {
            int minutes = getMinutes();
            updating = true;
            try { input.Text = minutes == 0 ? Off : minutes.ToString(CultureInfo.InvariantCulture); }
            finally { updating = false; }
            unit.Text = minutes == 0 ? string.Empty : SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveUnit");
        }
    }
}

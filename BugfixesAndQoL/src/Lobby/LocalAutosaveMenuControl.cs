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
            FontSize = 16;
            Height = 28;
            Focusable = false;
            Foreground = context.Menu.TryFindResource("Brush.Accent7") as Brush ?? Brushes.White;
            Template = (ControlTemplate)GUI.ParseXaml(SurfaceTemplate);

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            var label = new TextBlock {
                Text = SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveLabel"),
                FontSize = FontSize, Foreground = Foreground,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 4, 0)
            };
            grid.Children.Add(label);
            input = new TextBox {
                FontSize = FontSize, Foreground = Foreground, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Padding = new Thickness(0),
                TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                MaxLength = 32
            };
            Grid.SetColumn(input, 1);
            grid.Children.Add(input);
            var arrows = new Grid();
            arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(arrows, 3);
            arrows.Children.Add(Arrow(1, 0));
            arrows.Children.Add(Arrow(-1, 1));
            grid.Children.Add(arrows);
            unit = new TextBlock {
                FontSize = 14, Foreground = Foreground,
                Text = SerpLocalization.Get("BugfixesAndQoL.LocalAutosaveUnit"),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
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

        private RepeatButton Arrow(int delta, int row)
        {
            var button = new RepeatButton {
                Foreground = Foreground, Background = Brushes.Transparent,
                Template = (ControlTemplate)GUI.ParseXaml(ArrowTemplate.Replace("{CHEVRON}",
                    delta > 0 ? "M 0,4 L 4,0 L 8,4" : "M 0,0 L 4,4 L 8,0")),
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
            // Hidden preserves the fixed unit column; value changes never move the editor or arrows.
            unit.Visibility = minutes == 0 ? Visibility.Hidden : Visibility.Visible;
        }

        // Vanilla's three-slice red/gold surfaces, resolved from the owning menu after attachment.
        // Default button templates cannot host an editable field, so keep the visual contract here.
        private const string SurfaceTemplate = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ContentControl'>
  <Grid UseLayoutRounding='False'>
    <Rectangle Fill='Transparent'/>
    <Grid x:Name='NormalMode' Height='31' Margin='-1.5' FlowDirection='LeftToRight' IsHitTestVisible='False'>
      <Grid.ColumnDefinitions><ColumnDefinition Width='29.5'/><ColumnDefinition Width='*'/><ColumnDefinition Width='29.5'/></Grid.ColumnDefinitions>
      <Image Source='{DynamicResource Button_01_Left}'/>
      <Rectangle Grid.Column='1' Margin='-0.5,0,-0.5,0'>
        <Rectangle.Fill><ImageBrush ImageSource='{DynamicResource Button_01_Center}' Viewport='0,0,29.5,31' ViewportUnits='Absolute' AlignmentX='Center' AlignmentY='Top' TileMode='Tile'/></Rectangle.Fill>
      </Rectangle>
      <Image Grid.Column='2' Source='{DynamicResource Button_01_Right}' Stretch='Fill'/>
    </Grid>
    <Grid x:Name='GlowMode' Height='31' Margin='-1.5' FlowDirection='LeftToRight' IsHitTestVisible='False' Visibility='Hidden'>
      <Grid.ColumnDefinitions><ColumnDefinition Width='29.5'/><ColumnDefinition Width='*'/><ColumnDefinition Width='29.5'/></Grid.ColumnDefinitions>
      <Image Source='{DynamicResource Button_02_Left_}'/>
      <Rectangle Grid.Column='1' Margin='-0.5,0,-0.5,0'>
        <Rectangle.Fill><ImageBrush ImageSource='{DynamicResource Button_02_Center}' Viewport='0,0,29.5,31' ViewportUnits='Absolute' AlignmentX='Center' AlignmentY='Top' TileMode='Tile'/></Rectangle.Fill>
      </Rectangle>
      <Image Grid.Column='2' Source='{DynamicResource Button_02_Right}' Stretch='Fill'/>
    </Grid>
    <ContentPresenter x:Name='Editor' Content='{TemplateBinding Content}' Margin='12,0' VerticalAlignment='Center'/>
    <Rectangle x:Name='DisabledMask' Fill='#44000000' Visibility='Hidden' IsHitTestVisible='False'/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='NormalMode' Property='Visibility' Value='Hidden'/>
      <Setter TargetName='GlowMode' Property='Visibility' Value='Visible'/>
    </Trigger>
    <Trigger Property='IsKeyboardFocusWithin' Value='True'>
      <Setter TargetName='NormalMode' Property='Visibility' Value='Hidden'/>
      <Setter TargetName='GlowMode' Property='Visibility' Value='Visible'/>
    </Trigger>
    <Trigger Property='IsEnabled' Value='False'>
      <Setter TargetName='NormalMode' Property='Visibility' Value='Visible'/>
      <Setter TargetName='GlowMode' Property='Visibility' Value='Hidden'/>
      <Setter TargetName='DisabledMask' Property='Visibility' Value='Visible'/>
      <Setter TargetName='Editor' Property='Opacity' Value='0.7'/>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";

        private const string ArrowTemplate = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='RepeatButton'>
  <Grid Background='Transparent'>
    <Rectangle x:Name='Hover' Fill='#40FFE0A0' Visibility='Hidden'/>
    <Path x:Name='Chevron' Data='{CHEVRON}' Width='8' Height='4' Stretch='Fill'
          Stroke='{TemplateBinding Foreground}' StrokeThickness='1.5'
          HorizontalAlignment='Center' VerticalAlignment='Center'/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Hover' Property='Visibility' Value='Visible'/></Trigger>
    <Trigger Property='IsPressed' Value='True'><Setter TargetName='Chevron' Property='Opacity' Value='0.6'/><Setter TargetName='Hover' Property='Fill' Value='#80FFE0A0'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Hover' Property='Visibility' Value='Hidden'/><Setter TargetName='Chevron' Property='Opacity' Value='0.4'/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
    }
}

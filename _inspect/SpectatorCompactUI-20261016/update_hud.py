from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=root/'Testmods/SpectatorEditorBuildTest/src/SpectatorEditorBuildTestPlugin.Hud.cs'
s=p.read_text(encoding='utf-8-sig')
s=s.replace('    public sealed partial class SpectatorEditorBuildTestPlugin\n    {','''    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private static CreditAmountInput amountInput;
        private static readonly Dictionary<Button, string> playerTooltipTexts = new Dictionary<Button, string>();''',1)
s=s.replace('StackPanel owners = screen.FindName("SpectatorEditorHumanOwners") as StackPanel;', 'Panel owners = screen.FindName("SpectatorEditorHumanOwners") as Panel;')
s=s.replace('            toggle.Click += ToggleHumanMode;', '''            var amount = screen.FindName("TestToolsAmount") as TextBox;
            if (amount == null) throw new MissingMemberException("Test tools amount field missing.");
            amountInput = new CreditAmountInput(amount, toolsModel);
            panel.SizeChanged += OnHumanPanelSizeChanged;
            toggle.Click += ToggleHumanMode;''',1)
s=s.replace('''        private static void DetachHumanControls()
        {''','''        private static void DetachHumanControls()
        {
            amountInput?.Detach();
            amountInput = null;
            playerTooltipTexts.Clear();
            if (humanPanel != null) humanPanel.SizeChanged -= OnHumanPanelSizeChanged;''',1)
s=s.replace('available && (owner - 1) / 4 == toolsModel.OwnerPage','available')
s=s.replace('targetAvailable && (owner - 1) / 4 == toolsModel.TargetPage','targetAvailable')
s=s.replace('                    humanOwnerNumbers[owner].Text = PlayerLabel(owner);','''                    string label = PlayerLabel(owner);
                    humanOwnerNumbers[owner].Text = label;
                    SetPlayerTooltip(button, label);''')
s=s.replace('                    ((TextBlock)humanScreen.FindName("TestTargetText" + owner)).Text = PlayerLabel(owner);','''                    string label = PlayerLabel(owner);
                    var targetText = (TextBlock)humanScreen.FindName("TestTargetText" + owner);
                    targetText.Text = label;
                    RefreshHumanOwnerColour(owner);
                    targetText.Foreground = humanOwnerNumbers[owner].Foreground;
                    SetPlayerTooltip(target, label);''')
s=s.replace('            humanToggle.IsEnabled = humanPlacementMode || firstAi > 0;', '''            toolsModel.SetRecipientAvailable(IsSelectableAi(humanSelectedOwner));
            humanToggle.IsEnabled = humanPlacementMode || firstAi > 0;''')
a=s.index('            var root = humanScreen.FindName("TestToolsRoot") as Grid;')
b=s.index('        private static void RefreshPlayerFace',a)
s=s[:a]+'''            var root = humanScreen.FindName("TestToolsRoot") as Grid;
            root.Width = toolsModel.Expanded ? 638f : 238f;
            humanPanel.Width = Math.Min(toolsModel.Expanded ? 640f : 240f, Math.Max(120f, humanCanvas.ActualWidth - 20f));
            // Height is measured from the actual visible tool page, not from a fixed canvas.
            humanPanel.Height = float.NaN;
            humanPanel.MaxHeight = Math.Max(36f, humanCanvas.ActualHeight - 20f);
            PositionHumanPanel();
        }

        private static void SetPlayerTooltip(Button button, string label)
        {
            string text = label + " — " + SerpLocalization.Get("Tools.SelectHelp");
            if (playerTooltipTexts.TryGetValue(button, out string previous) && previous == text) return;
            var style = humanPanel.TryFindResource("ToolsTooltip") as Style;
            if (style == null) throw new MissingMemberException("Test tools popup style missing.");
            ToolTipService.SetToolTip(button, new ToolTip { Style = style, Content = text });
            ToolTipService.SetShowDuration(button, 60000);
            playerTooltipTexts[button] = text;
        }

        private static void OnHumanPanelSizeChanged(object sender, SizeChangedEventArgs args) => ClampHumanPanel();

'''+s[b:]
s=s.replace('            if (command == "Placement") { ToggleHumanMode(null, null); return; }', '            if (command == "StopCursor") { MainControls.instance?.StopAllPlacement(); return; }')
s=s.replace('            toolsModel?.ActionCommand.Execute("PlacementView");\n','')
s=s.replace('humanCanvas.ActualHeight - humanPanel.Height','humanCanvas.ActualHeight - humanPanel.ActualHeight')
p.write_text(s,encoding='utf-8',newline='\r\n')

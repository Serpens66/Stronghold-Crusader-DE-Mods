from pathlib import Path
root=Path(__file__).resolve().parents[2]
mod=root/'Testmods/SpectatorEditorBuildTest'
def read(name): return (mod/'src'/name).read_text(encoding='utf-8-sig')
def write(name,s): (mod/'src'/name).write_text(s,encoding='utf-8',newline='\r\n')
name='SpectatorEditorBuildTestPlugin.cs';s=read(name)
s=s.replace('private static ManualLogSource log;', 'private static TimestampLog log;')
s=s.replace('        private static bool failureLogged;','        private static readonly HashSet<string> loggedFailures = new HashSet<string>();')
s=s.replace('log = Logger;', 'log = new TimestampLog(Logger);')
s=s.replace('                harmony = candidate;','                harmony = candidate;\n                RegisterTestActions();')
s=s.replace('            if (failureLogged) return;\n            failureLogged = true;','            string key = message.Split(new[] { \':\', \' \' }, 2)[0];\n            lock (loggedFailures) if (!loggedFailures.Add(key)) return;')
write(name,s)
name='SpectatorEditorBuildTestPlugin.Native.cs';s=read(name)
s=s.replace('            if (availabilityDetour != null) return;', '            InitializeNativeOrders(context);\n            if (availabilityDetour != null) return;',1)
write(name,s)
name='SpectatorEditorBuildTestPlugin.Session.cs';s=read(name)
s=s.replace('            log?.LogInfo("SPECTATOR_EDITOR_SESSION_START', '            actionLedger.Reset(sessionId);\n            log?.LogInfo("SPECTATOR_EDITOR_SESSION_START')
s=s.replace('        private static void ResetSessionState()\n        {','        private static void ResetSessionState()\n        {\n            ResetTestActions(0);')
s=s.replace('                var state = GameData.Instance?.lastGameState;','                if (!MainViewModel.viewModelLoaded) return false;\n                var state = GameData.Instance?.lastGameState;',1)
a=s.index('        private static bool IsHumanSingleplayerSession()');b=s.index('        private static bool IsPlacementSession()',a)
s=s[:a]+'''        private static bool IsHumanSingleplayerSession() => IsToolsSession();

'''+s[b:]
a=s.index('        private static bool IsPlacementSession()');b=s.index('    }\n}',a)
s=s[:a]+'''        private static bool IsPlacementSession() => humanPlacementMode && IsToolsSession();

        private static bool IsSelectableAi(int owner) => IsToolsPlayer(owner, true);

        private static int CurrentAiOwner() => IsPlacementSession() && IsSelectableAi(humanSelectedOwner)
            ? humanSelectedOwner : -1;

'''+s[b:]
write(name,s)
name='SpectatorEditorBuildTestPlugin.Hud.cs';s=read(name)
s=s.replace('            humanOwners = owners;','            humanOwners = owners;\n            toolsModel = new TestToolsViewModel(ExecuteToolsCommand, IsToolsSession);\n            panel.DataContext = toolsModel;\n            toolsModel.Status = SerpLocalization.Get("Tools.Ready");')
s=s.replace('                    if (!humanPlacementMode || !IsHumanSingleplayerSession() ||','                    if (!IsHumanSingleplayerSession() ||')
a=s.index('        private static void RefreshHumanControls(');b=s.index('        private static void RefreshHumanOwnerColour(',a)
s=s[:a]+'''        private static void RefreshHumanControls(bool eligible)
        {
            if (humanPanel == null || toolsModel == null) return;
            bool show = eligible && (IsEditorToolsSession() || MainViewModel.Instance.Show_HUD_Main) &&
                !MainViewModel.Instance.Show_HUD_Briefing;
            humanPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;
            toolsModel.RefreshLocale();
            int firstAi = -1;
            for (int owner = 1; owner <= 8; owner++)
            {
                bool available = IsSelectableAi(owner);
                if (available && firstAi < 0) firstAi = owner;
                Button button = humanOwnerButtons[owner];
                button.Visibility = available && (owner - 1) / 4 == toolsModel.OwnerPage ? Visibility.Visible : Visibility.Collapsed;
                if (available)
                {
                    RefreshHumanOwnerColour(owner);
                    humanOwnerNumbers[owner].Text = PlayerLabel(owner);
                    RefreshPlayerFace("OwnerFace" + owner, owner);
                }
                var target = humanScreen.FindName("TestTarget" + owner) as Button;
                bool targetAvailable = IsToolsPlayer(owner, false);
                target.Visibility = targetAvailable && (owner - 1) / 4 == toolsModel.TargetPage ? Visibility.Visible : Visibility.Collapsed;
                target.Background = owner == toolsModel.Target ? ownerSelectedBrush : ownerNormalBrush;
                if (targetAvailable)
                {
                    ((TextBlock)humanScreen.FindName("TestTargetText" + owner)).Text = PlayerLabel(owner);
                    RefreshPlayerFace("TargetFace" + owner, owner);
                }
            }
            if (!IsSelectableAi(humanSelectedOwner))
            {
                humanSelectedOwner = firstAi;
                if (firstAi < 0) MainControls.instance?.StopAllPlacement();
            }
            for (int owner = 1; owner <= 8; owner++)
                humanOwnerButtons[owner].Background = owner == humanSelectedOwner ? ownerSelectedBrush : ownerNormalBrush;
            humanToggle.IsEnabled = humanPlacementMode || firstAi > 0;
            humanToggleText.Text = SerpLocalization.Get(humanPlacementMode ? "Tools.StopPlacement" : "Tools.Placement");
            humanOwners.Visibility = toolsModel.Expanded ? Visibility.Visible : Visibility.Collapsed;
            toolsModel.SelectedPlayer = SerpLocalization.Get("Tools.Actor") + ": " +
                (humanSelectedOwner > 0 ? PlayerLabel(humanSelectedOwner) : SerpLocalization.Get("Tools.NoAi"));
            toolsModel.SelectedTarget = SerpLocalization.Get("Tools.Target") + ": " +
                (IsToolsPlayer(toolsModel.Target, false) ? PlayerLabel(toolsModel.Target) : SerpLocalization.Get("Tools.SelectTarget"));
            var root = humanScreen.FindName("TestToolsRoot") as Grid;
            root.Width = toolsModel.Expanded ? 698f : 238f;
            root.Height = toolsModel.Expanded ? 568f : 38f;
            float scale = (float)Math.Min(1, Math.Min((humanCanvas.ActualWidth - 20) / 700, (humanCanvas.ActualHeight - 20) / 570));
            scale = Math.Max(0.1f, scale);
            humanPanel.Width = (toolsModel.Expanded ? 700 : 240) * scale;
            humanPanel.Height = (toolsModel.Expanded ? 570 : 40) * scale;
            PositionHumanPanel();
        }

        private static void RefreshPlayerFace(string element, int owner)
        {
            var image = humanScreen.FindName(element) as Image;
            if (image == null) return;
            var state = GameData.Instance.lastGameState;
            ImageSource face = state.is_skirmish_player(owner)
                ? MainViewModel.Instance.getAIFace(state.computer_register[owner], true) : null;
            if (!ReferenceEquals(image.Source, face)) image.Source = face;
        }

        private static void ExecuteToolsCommand(string command)
        {
            if (!IsToolsSession() || toolsModel == null) return;
            if (command == "Placement") { ToggleHumanMode(null, null); return; }
            if (command == "Credit") { QueueCredit(toolsModel.Good, toolsModel.Amount); return; }
            if (command == "Attack") { QueueOrder(TestActionKind.Attack, toolsModel.Target, toolsModel.ForceAcceptance); return; }
            if (command == "Defend") { QueueOrder(TestActionKind.Defend, toolsModel.Target, toolsModel.ForceAcceptance); return; }
            if (command == "Cancel") { QueueOrder(TestActionKind.Cancel, 0, false); return; }
            if (command != null && command.StartsWith("Target", StringComparison.Ordinal) &&
                int.TryParse(command.Substring(6), out int target) && IsToolsPlayer(target, false))
                toolsModel.Target = target;
        }

'''+s[b:]
s=s.replace('            if (!humanPlacementMode || !IsHumanSingleplayerSession() || !IsSelectableAi(owner)) return;', '            if (!IsHumanSingleplayerSession() || !IsSelectableAi(owner)) return;')
s=s.replace('            if (MainViewModel.Instance.HUDmain != null)\n                RefreshBuildIcons(', '            if (humanPlacementMode && MainViewModel.Instance.HUDmain != null)\n                RefreshBuildIcons(')
s=s.replace('            if (!humanPlacementMode || !IsHumanSingleplayerSession() || !IsSelectableAi(owner))\n                return;', '            if (!IsHumanSingleplayerSession() || !IsSelectableAi(owner)) return;')
s=s.replace('        private static void OnBeforeRender()\n        {\n            try','''        private static void OnBeforeRender()
        {
            int frame = UnityEngine.Time.frameCount;
            if (lastRenderFrame == frame) return;
            lastRenderFrame = frame;
            if (!MainViewModel.viewModelLoaded) return;
            try''')
s=s.replace('                RefreshHumanControls(humanEligible);','                RefreshHumanControls(humanEligible);\n                DrainTestActions();',1)
# Keep the editor's established tabs/layout; owner assignment and native editor placement still use our selected slot.
s=s.replace('                bool layoutChanged = EnsurePlacementHudLayout(hud);', '                if (IsEditorToolsSession()) return;\n                bool layoutChanged = EnsurePlacementHudLayout(hud);')
# The primary toggle remains event-backed, while the ViewModel switches the visible tool page.
s=s.replace('            if (!IsHumanSingleplayerSession()) return;\n            HUD_Main hud', '            if (!IsHumanSingleplayerSession()) return;\n            toolsModel?.ActionCommand.Execute("PlacementView");\n            HUD_Main hud')
write(name,s)

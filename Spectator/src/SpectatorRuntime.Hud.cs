using APIShared.GameModes;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Iced.Intel;
using Noesis;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Security.Cryptography;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static CreditAmountInput amountInput;
        private static Border humanDetails;
        private static UIElement inputRoot;
        private static int toolbarOccupiedCount = -1;
        private static readonly Dictionary<Button, string> playerTooltipTexts = new Dictionary<Button, string>();
        private static void EnsureHumanControls()
        {
            IngameUIScreens screen = MainViewModel.Instance?.IngameUI;
            HUD_Main hud = MainViewModel.Instance?.HUDmain;
            if (humanScreen != null && (!ReferenceEquals(screen, humanScreen) ||
                !ReferenceEquals(hud, humanBoundHud)))
            {
                MainControls.instance?.StopAllPlacement();
                humanPlacementMode = false;
                RestorePlacementFreeze();
                SetRevealHidden(0);
                ApplyHumanAi(-1);
                humanSavedTabs = null;
                activeWall = null;
                activeSpecial = null;
                activeHud = null;
                DetachHumanControls();
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_HUD_CHANGED");
            }
            if (screen == null || hud == null || humanPanel != null) return;
            Canvas canvas = screen.FindName("SpectatorEditorHumanCanvas") as Canvas;
            Border panel = screen.FindName("SpectatorEditorHumanPanel") as Border;
            Border dragHandle = screen.FindName("SpectatorEditorHumanDrag") as Border;
            Button toggle = screen.FindName("SpectatorEditorHumanToggle") as Button;
            TextBlock toggleText = screen.FindName("SpectatorEditorHumanToggleText") as TextBlock;
            Panel owners = screen.FindName("SpectatorEditorHumanOwners") as Panel;
            Border details = screen.FindName("SpectatorDetails") as Border;
            if (canvas == null || panel == null || dragHandle == null || toggle == null ||
                toggleText == null || owners == null || details == null)
                return;
            var found = new Button[9];
            var numbers = new TextBlock[9];
            for (int owner = 1; owner <= 8; owner++)
            {
                found[owner] = screen.FindName("SpectatorEditorHumanOwner" + owner) as Button;
                numbers[owner] = screen.FindName("SpectatorEditorHumanNumber" + owner) as TextBlock;
                if (found[owner] == null || numbers[owner] == null) return;
            }
            humanScreen = screen;
            humanBoundHud = hud;
            humanCanvas = canvas;
            humanPanel = panel;
            humanDetails = details;
            humanDragHandle = dragHandle;
            humanToggle = toggle;
            humanToggleText = toggleText;
            long boundSession = CurrentSessionId;
            TestToolsViewModel boundModel = null;
            boundModel = new TestToolsViewModel(ExecuteToolsCommand, () =>
                ReferenceEquals(toolsModel, boundModel) && IsCurrentSession(boundSession) && IsToolbarSession());
            toolsModel = boundModel;
            toolsModel.PropertyChanged += OnToolsModelChanged;
            // A reused view must not flash at its old/default coordinates before HUD layout is ready.
            panel.Opacity = 0; panel.IsHitTestVisible = false;
            panel.DataContext = toolsModel;
            humanDetails.DataContext = toolsModel;
            CacheToolbarElements(screen);
            toolbarDirty = true;
            SpectatorPerspectiveRuntime.OnHudAvailable();
            var amount = screen.FindName("TestToolsAmount") as TextBox;
            if (amount == null) throw new MissingMemberException("Test tools amount field missing.");
            amountInput = new CreditAmountInput(amount, toolsModel);
            panel.SizeChanged += OnHumanPanelSizeChanged;
            // Listen at the whole Noesis view root, including non-focusable map surfaces.
            inputRoot = screen;
            while (VisualTreeHelper.GetParent(inputRoot) is UIElement parent) inputRoot = parent;
            inputRoot.PreviewMouseDown += OnToolbarOutsideClick;
            dragHandle.MouseLeftButtonDown += OnHumanDragDown;
            dragHandle.MouseMove += OnHumanDragMove;
            dragHandle.MouseLeftButtonUp += OnHumanDragUp;
            dragHandle.LostMouseCapture += OnHumanLostCapture;
            canvas.SizeChanged += OnHumanCanvasSizeChanged;
            canvas.IsVisibleChanged += OnHumanCanvasVisibilityChanged;
            toolbarOccupiedCount = -1;
            for (int owner = 1; owner <= 8; owner++)
            {
                int selected = owner;
                humanOwnerButtons[owner] = found[owner];
                humanOwnerNumbers[owner] = numbers[owner];
                humanDisplayedColours[owner] = -1;
                humanOwnerHandlers[owner] = (sender, args) => SelectHumanAi(selected);
                found[owner].Click += humanOwnerHandlers[owner];
                humanJumpHandlers[owner] = (sender, args) =>
                {
                    if (args.ChangedButton != MouseButton.Right &&
                        args.ChangedButton != MouseButton.Middle) return;
                    if (!IsToolbarSession() || !IsSelectableView(selected)) return;
                    args.Handled = true;
                    JumpToHumanAi(selected, args.ChangedButton);
                };
                found[owner].PreviewMouseDown += humanJumpHandlers[owner];
            }
            BindToolbarLayout();
            log.LogInfo("SPECTATOR_EDITOR_HUMAN_CONTROLS_READY");
        }

        private static void DetachHumanControls()
        {
            toolbarDirty = true;
            DetachPortraitNumbers();
            DetachToolbarLayout();
            // The same IngameUIScreens can survive a map transition. Hide before dropping references.
            if (humanPanel != null) { humanPanel.Visibility = Visibility.Collapsed; humanPanel.DataContext = null; }
            // The collapsed common parent already hides the content. Preserve its
            // visibility binding so the same Noesis view can serve another mission.
            if (humanDetails != null) humanDetails.DataContext = null;
            ClosePlayerTooltips();
            foreach (var button in playerTooltipTexts.Keys)
                if (ToolTipService.GetToolTip(button) is ToolTip tip) { MenuToolTipSizing.SetEnabled(tip, false); ToolTipService.SetToolTip(button, null); }
            SpectatorPerspectiveRuntime.OnHudUnavailable();
            RestoreVanillaPowerToolbar(true);
            CancelPowerSelection();
            ClearGateSelection();
            toolMouseState = 0;
            observedGate = null;
            amountInput?.Detach();
            amountInput = null;
            MenuToolTipSizing.SetReference(null);
            playerTooltipTexts.Clear(); statusText = orderStatusText = null;
            if (humanPanel != null) humanPanel.SizeChanged -= OnHumanPanelSizeChanged;
            if (inputRoot != null) inputRoot.PreviewMouseDown -= OnToolbarOutsideClick;
            inputRoot = null;
            if (humanDragHandle != null)
            {
                humanDragHandle.MouseLeftButtonDown -= OnHumanDragDown;
                humanDragHandle.MouseMove -= OnHumanDragMove;
                humanDragHandle.MouseLeftButtonUp -= OnHumanDragUp;
                humanDragHandle.LostMouseCapture -= OnHumanLostCapture;
                if (humanDragHandle.IsMouseCaptured) humanDragHandle.ReleaseMouseCapture();
            }
            if (humanCanvas != null)
            {
                humanCanvas.SizeChanged -= OnHumanCanvasSizeChanged;
                humanCanvas.IsVisibleChanged -= OnHumanCanvasVisibilityChanged;
            }
            for (int owner = 1; owner <= 8; owner++)
            {
                if (humanOwnerButtons[owner] != null && humanOwnerHandlers[owner] != null)
                    humanOwnerButtons[owner].Click -= humanOwnerHandlers[owner];
                if (humanOwnerButtons[owner] != null && humanJumpHandlers[owner] != null)
                    humanOwnerButtons[owner].PreviewMouseDown -= humanJumpHandlers[owner];
                humanOwnerButtons[owner] = null;
                humanOwnerNumbers[owner] = null;
                humanOwnerHandlers[owner] = null;
                humanJumpHandlers[owner] = null;
                humanDisplayedColours[owner] = -1;
            }
            humanDragging = false;
            humanPanel = null;
            humanDetails = null;
            humanDragHandle = null;
            humanCanvas = null;
            humanToggle = null;
            humanToggleText = null;
            humanScreen = null;
            humanBoundHud = null;
            toolbarRoot = null;
            currentOrderIcon = null; currentOrderText = currentOrderTarget = null;
            shownOrderActor = -1; shownOrderLocale = null;
            Array.Clear(targetButtons, 0, targetButtons.Length);
            Array.Clear(targetLabels, 0, targetLabels.Length);
            Array.Clear(ownerFaces, 0, ownerFaces.Length);
            Array.Clear(targetFaces, 0, targetFaces.Length);
            if (toolsModel != null) toolsModel.PropertyChanged -= OnToolsModelChanged;
            toolsModel = null;
        }

        private static void RefreshHumanControls(bool eligible)
        {
            if (humanPanel == null || toolsModel == null) return;
            var view = MainViewModel.Instance;
            if (toolbarDirty || toolbarOccupiedCount < 0)
            {
                toolbarOccupiedCount = 0;
                if (eligible) for (int player = 1; player <= 8; player++) if (IsSelectableView(player)) toolbarOccupiedCount++;
            }
            bool show = humanCanvas.IsVisible && DockHudPolicy.CanShowToolbar(eligible, view.UIVisible, view.Show_InGameUI,
                view.Show_HUD_Extras, view.Show_HUD_Briefing, view.Show_HUD_MissionOver, view.Show_HUD_IngameMenu, toolbarOccupiedCount);
            bool wasShown = humanPanel.Visibility == Visibility.Visible;
            humanPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show)
            {
                if (wasShown || humanDragging || toolbarDrag.Preview || amountInput?.OwnsKeyboard == true)
                    SuspendToolbarInteraction();
                return;
            }
            toolsModel.PlacementActive = humanPlacementMode;
            var state = GameData.Instance.lastGameState;
            bool occupied = view.Show_HUD_Troops || view.Show_HUD_Building || view.Show_HUD_Briefing ||
                view.Show_HUD_MissionOver || view.AlliesPanelVisible || view.MeritPanelVisible || view.MPChatVisible ||
                state == null || GateHudLifecycle.Route(state.app_mode, state.app_sub_mode, false) != ToolsHudRoute.Main;
            toolbarHudDisplay = DockHudPolicy.Evaluate(eligible, true, view.Show_HUD_Main,
                occupied, false, humanPlacementMode, view.buildScreenID);
            toolsModel.SetHudAvailable(toolbarHudDisplay == DockHudDisplay.Tools);
            UpdatePlacementDate(toolbarHudDisplay == DockHudDisplay.Placement);
            string currentLocale = SerpLocalization.GetActiveLocale();
            if (currentLocale != toolbarLocale)
            {
                toolbarLocale = currentLocale; toolbarDirty = true;
                foreach (var number in portraitNumbers) number?.Invalidate();
            }
            if (!toolbarDirty) { PositionToolbar(); return; }
            toolbarDirty = false;
            toolbarLayoutDirty = true;
            toolsModel.RefreshLocale();
            int firstAi = -1;
            for (int owner = 1; owner <= 8; owner++)
            {
                bool available = IsSelectableView(owner);
                if (available && firstAi < 0) firstAi = owner;
                Button button = humanOwnerButtons[owner];
                button.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
                MarkOwnerButton(button, owner == humanSelectedOwner);
                if (available)
                {
                    RefreshHumanOwnerColour(owner);
                    humanOwnerNumbers[owner].Text = owner.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    SetPlayerTooltip(button, PlayerLabel(owner, false), owner);
                    RefreshPlayerFace("OwnerFace" + owner, owner);
                }
                var target = targetButtons[owner];
                bool targetAvailable = IsToolsPlayer(owner, false);
                target.Visibility = targetAvailable ? Visibility.Visible : Visibility.Collapsed;
                MarkOwnerButton(target, owner == toolsModel.Target);
                if (targetAvailable)
                {
                    targetLabels[owner].Text = owner.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    RefreshHumanOwnerColour(owner);
                    targetLabels[owner].Foreground = humanOwnerNumbers[owner].Foreground;
                    SetPlayerTooltip(target, PlayerLabel(owner, false), owner, true);
                    RefreshPlayerFace("TargetFace" + owner, owner);
                }
            }
            if (!spectatorSelectionLinked && humanSelectedOwner == -1 && !initialSelectionChosen && firstAi > 0)
            { initialSelectionChosen = true; ApplyHumanAi(firstAi); }
            else if (!IsSelectableView(humanSelectedOwner)) ApplyHumanAi(-1);
            for (int owner = 1; owner <= 8; owner++) MarkOwnerButton(humanOwnerButtons[owner], owner == humanSelectedOwner);
            toolsModel.SetRecipientAvailable(IsSelectableAi(humanSelectedOwner));
            toolsModel.SetAvailability(IsToolsSession());
            RefreshOrderAvailability();
            humanToggle.IsEnabled = IsToolsSession() && IsSelectableAi(humanSelectedOwner);
            humanToggleText.Text = SerpLocalization.Get("Tools.PlacementShort");
            toolsModel.SelectedTarget = SerpLocalization.Get("Tools.Target") + ": " +
                (IsToolsPlayer(toolsModel.Target, false) ? PlayerLabel(toolsModel.Target) : SerpLocalization.Get("Tools.SelectTarget"));
            toolsModel.SelectedTargetColour = PlayerBrush(toolsModel.Target);
            RefreshOrderPresentation(true);
            RefreshColouredStatus();
            PositionToolbar();
        }

        private static void SetPlayerTooltip(Button button, string label, int player, bool target = false)
        {
            string help = SerpLocalization.Get(target ? "Tools.TargetHelp" : "Tools.SelectHelp");
            string text = label + "\n" + help;
            if (playerTooltipTexts.TryGetValue(button, out string previous) && previous == text) return;
            var style = humanPanel.TryFindResource("PlayerTooltip") as Style;
            if (style == null) throw new MissingMemberException("Test tools popup style missing.");
            var content = new TextBlock { FontSize = ToolbarGeometry.MenuFontSize,
                Foreground = neutralPlayerBrush, TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400 };
            // Some player colours are almost black; tooltip names inherit the readable white text.
            content.Inlines.Add(new Run { Text = label });
            content.Inlines.Add(new Run { Text = "\n" + help });
            var tip = new ToolTip { Style = style, Content = content };
            MenuToolTipSizing.SetEnabled(tip, true);
            if (ToolTipService.GetToolTip(button) is ToolTip previousTip) { previousTip.IsOpen = false; MenuToolTipSizing.SetEnabled(previousTip, false); }
            ToolTipService.SetToolTip(button, tip);
            ToolTipService.SetShowDuration(button, 60000);
            ToolTipService.SetInitialShowDelay(button, 0);
            ToolTipService.SetBetweenShowDelay(button, 0);
            playerTooltipTexts[button] = text;
        }

        private static void OnHumanPanelSizeChanged(object sender, SizeChangedEventArgs args) => toolbarLayoutDirty = true;

        private static void RefreshPlayerFace(string element, int owner)
        {
            var image = element.StartsWith("Owner", StringComparison.Ordinal) ? ownerFaces[owner] : targetFaces[owner];
            if (image == null) return;
            var state = GameData.Instance.lastGameState;
            ImageSource face = state.is_skirmish_player(owner)
                ? MainViewModel.Instance.getAIFace(state.computer_register[owner], true)
                : Platform_Multiplayer.Instance.GetUserAvatar(Platform_Multiplayer.Instance.getPlayerSteamID(owner));
            if (!ReferenceEquals(image.Source, face)) image.Source = face;
        }

        private static void ExecuteToolsCommand(string command)
        {
            try { ExecuteToolsCommandCore(command); }
            catch (Exception error)
            {
                LogOnce("TEST_TOOLS_UI_ERROR feature=" + command + ": " + error);
                if (toolsModel != null) toolsModel.Status = SerpLocalization.Get("Tools.Failed");
            }
        }

        private static void ExecuteToolsCommandCore(string command)
        {
            if (!IsToolbarSession() || toolsModel == null) return;
            toolbarDirty = true;
            if (!IsToolsSession()) return;
            SyncSpectatorSelection();
            if (command == "StopCursor" || command == "CancelPower") { amountInput?.EndEditing(); }
            if (command == "StopCursor") { CancelPowerSelection(); if (humanPlacementMode) ToggleHumanMode(null, null); else MainControls.instance?.StopAllPlacement(); return; }
            if (command == "CancelPower") { CancelPowerSelection(); return; }
            if (command == "EnablePowers" || command == "DisablePowers")
            { QueueTestAction(new TestAction { Kind = TestActionKind.PowerToggle, Actor = humanSelectedOwner, Force = command == "EnablePowers" }); return; }
            if (command == "Placement") { ToggleHumanMode(null, null); return; }
            if (command == "ResetAmount") { amountInput?.ResetAmount(); return; }
            if (command == "Credit" || command == "Debit") { QueueCredit(toolsModel.Good, toolsModel.Amount, command == "Debit"); return; }
            if (command == "Attack") { QueueOrder(TestActionKind.Attack, toolsModel.Target, toolsModel.ForceAcceptance); return; }
            if (command == "Defend") { QueueOrder(TestActionKind.Defend, toolsModel.Target, toolsModel.ForceAcceptance); return; }
            if (command == "Cancel") { QueueOrder(TestActionKind.Cancel, 0, false); return; }
            if (command != null && command.StartsWith("Target", StringComparison.Ordinal) &&
                int.TryParse(command.Substring(6), out int target) && IsToolsPlayer(target, false))
                toolsModel.Target = target;
        }

        private static void RefreshHumanOwnerColour(int owner)
        {
            int[] mapping = SpriteMapping.remapColours;
            int index = mapping != null && owner < mapping.Length ? mapping[owner] : 0;
            UnityEngine.Color colour = UnityEngine.Color.white;
            if (mapping != null && index > 0 && index < mapping.Length &&
                spriteLoader.instance != null)
                spriteLoader.instance.GetGMMaterial(Enums.GM.GM_BODY_ARCHER,
                    index, 0, out colour, 0);
            byte red = (byte)(colour.r * 255f);
            byte green = (byte)(colour.g * 255f);
            byte blue = (byte)(colour.b * 255f);
            int rgb = red << 16 | green << 8 | blue;
            if (humanDisplayedColours[owner] == rgb) return;
            humanOwnerNumbers[owner].Foreground =
                new SolidColorBrush(Noesis.Color.FromRgb(red, green, blue));
            humanDisplayedColours[owner] = rgb;
        }

        private static void OnHumanDragDown(object sender, MouseButtonEventArgs args)
        {
            if (humanCanvas == null || humanPanel == null || humanDragHandle == null ||
                !layoutCached || !humanPanel.IsHitTestVisible) return;
            Point pointer = args.GetPosition(humanCanvas);
            toolbarDrag.Begin(pointer.X, pointer.Y, toolbarVisibleGrip, !toolbarMoved);
            humanDragging = humanDragHandle.CaptureMouse();
            toolbarLayoutDirty = true;
            PositionToolbar();
            args.Handled = true;
        }

        private static void OnHumanDragMove(object sender, MouseEventArgs args)
        {
            if (!humanDragging || humanCanvas == null || humanPanel == null) return;
            Point current = args.GetPosition(humanCanvas);
            SnapToolbarDrag(current.X, current.Y);
            PositionToolbar();
            args.Handled = true;
        }

        private static void OnHumanDragUp(object sender, MouseButtonEventArgs args)
        {
            if (humanDragging && humanCanvas != null)
            {
                Point current = args.GetPosition(humanCanvas);
                Point origin = humanCanvas.PointToScreen(new Point(0, 0));
                Point unit = humanCanvas.PointToScreen(new Point(100, 100));
                toolbarMoved = !toolbarDrag.Drop(current.X, current.Y, toolbarDockGrip,
                    (unit.X - origin.X) / 100, (unit.Y - origin.Y) / 100, toolbarFloatingScale,
                    out toolbarDesiredX, out toolbarDesiredY);
            }
            humanDragging = false;
            toolbarLayoutDirty = true;
            PositionToolbar();
            if (humanDragHandle != null && humanDragHandle.IsMouseCaptured) humanDragHandle.ReleaseMouseCapture();
            args.Handled = true;
        }

        private static void OnHumanLostCapture(object sender, MouseEventArgs args)
        { humanDragging = false; toolbarDrag.Cancel(); toolbarLayoutDirty = true; PositionToolbar(); }
        private static void OnHumanCanvasSizeChanged(object sender, SizeChangedEventArgs args)
        { toolbarDirty = true; toolbarLayoutDirty = true; }

        private static void SuspendToolbarInteraction()
        {
            UpdatePlacementDate(false);
            humanDragging = false; toolbarDrag.Cancel();
            if (humanDragHandle?.IsMouseCaptured == true) humanDragHandle.ReleaseMouseCapture();
            amountInput?.EndEditing(); ClosePlayerTooltips();
            HideDockGuides();
        }

        private static void OnHumanCanvasVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            toolbarDirty = toolbarLayoutDirty = true;
            if (humanCanvas?.IsVisible != true) SuspendToolbarInteraction();
        }

        private static RadioButton[] NormalTabs(HUD_Main hud) => new[] {
            hud.RefTabBuildCastle, hud.RefTabBuildIndustry, hud.RefTabBuildFarms,
            hud.RefTabBuildTown, hud.RefTabBuildWeapons, hud.RefTabBuildFood
        };

        private static void ToggleHumanMode(object sender, RoutedEventArgs args)
        {
            if (!IsHumanSingleplayerSession()) return;
            SyncSpectatorSelection();
            if (!humanPlacementMode && !IsSelectableAi(humanSelectedOwner)) return;
            CancelPowerSelection();
            toolbarDirty = true;
            amountInput?.EndEditing();
            HUD_Main hud = MainViewModel.Instance.HUDmain;
            if (hud == null || MainControls.instance == null) return;
            if (IsEditorToolsSession())
            {
                EnterPlacementHud(MainViewModel.Instance);
                MainControls.instance.StopAllPlacement();
                humanPlacementMode = !humanPlacementMode;
                SetRevealHidden(0);
                RefreshHumanControls(true);
                return;
            }
            if (humanPlacementMode)
            {
                humanPlacementMode = false;
                RestorePlacementFreeze();
                SetRevealHidden(0);
                MainControls.instance.StopAllPlacement();
                activeWall = null;
                activeSpecial = null;
                MainViewModel.Instance.SubMode = humanSavedSubMode;
                hud.SetupNewBuildScreen(-1000);
                EnterPlacementHud(MainViewModel.Instance);
                if (humanSavedScreen == DockHudPolicy.BlankBuildScreen) hud.NewBuildScreenBlank();
                else hud.SetupNewBuildScreen(GateHudLifecycle.IsBuildScreen(humanSavedScreen) ? humanSavedScreen : 1);
                hud.StartScrollSwish();
                RadioButton[] tabs = NormalTabs(hud);
                if (humanSavedTabs != null)
                    for (int index = 0; index < tabs.Length; index++)
                        if (tabs[index] != null) tabs[index].IsChecked = humanSavedTabs[index];
                activeHud = null;
                lastOwner = int.MinValue;
                lastScreen = int.MinValue;
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_MODE_OFF restoredScreen=" + humanSavedScreen);
            }
            else
            {
                int firstAi = -1;
                for (int owner = 1; owner <= 8; owner++)
                    if (IsSelectableAi(owner)) { firstAi = owner; break; }
                if (firstAi < 0) return;
                humanSavedScreen = MainViewModel.Instance.buildScreenID;
                if (!GateHudLifecycle.IsBuildScreen(humanSavedScreen) && humanSavedScreen != DockHudPolicy.BlankBuildScreen) humanSavedScreen = 1;
                humanSavedSubMode = MainViewModel.Instance.SubMode;
                RadioButton[] tabs = NormalTabs(hud);
                humanSavedTabs = new bool?[tabs.Length];
                for (int index = 0; index < tabs.Length; index++)
                    humanSavedTabs[index] = tabs[index]?.IsChecked;
                MainControls.instance.StopAllPlacement();
                if (!IsSelectableAi(humanSelectedOwner)) humanSelectedOwner = firstAi;
                humanPlacementMode = true;
                SetRevealHidden(
                    visualMapDetour != null && visualFrameRegistration != null &&
                    visualResourceDetour != null ? 1 : 0);
                if (Volatile.Read(ref revealHiddenActive) == 0)
                    log.LogWarning("SPECTATOR_EDITOR_VISUAL_UNAVAILABLE: audited native hooks are not installed");
                EnterPlacementHud(MainViewModel.Instance);
                hud.SetupNewBuildScreen(1);
                RefreshBuildIcons(hud, humanSelectedOwner, "human-enter");
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_MODE_ON owner=" + humanSelectedOwner +
                    " savedScreen=" + humanSavedScreen);
            }
            RefreshHumanControls(true);
        }

        private static void SelectHumanAi(int owner)
        {
            if (!IsToolbarSession() || !IsSelectableView(owner)) return;
            if (SpectatorPerspectiveRuntime.IsActiveSpectator())
            {
                SpectatorPerspectiveRuntime.SelectPlayer(owner);
                SyncSpectatorSelection();
            }
            else ApplyHumanAi(owner);
            RefreshHumanControls(true);
        }

        private static unsafe void JumpToHumanAi(int owner, MouseButton button)
        {
            if (!IsToolbarSession() || !IsSelectableView(owner)) return;
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (players == null) return;
                if (button == MouseButton.Right)
                {
                    GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                    if (buildings == null) return;
                    int keepId = players.GetPlayerKeepId(owner);
                    if (keepId <= 0 || !buildings.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                        keep == null || keep->r_AliveState != AliveState.IsAlive ||
                        keep->r_PlayerIdOwner != owner || keep->r_GlobalId == 0 ||
                        keep->r_BuildingType < eStructs.STRUCT_KEEP_ONE ||
                        keep->r_BuildingType > eStructs.STRUCT_KEEP_FIVE) return;
                    players.SetScreenCenterToBuilding(keepId);
                }
                else if (button == MouseButton.Middle)
                {
                    GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
                    if (units == null) return;
                    int lordId = players.GetLordUnitId(owner);
                    int globalId = players.GetLordUnitGlobalId(owner);
                    if (lordId <= 0 || globalId == 0 ||
                        !APIShared.UnitAccess.TryGetById(units, lordId, out GameUnit* lord, out _) || lord == null ||
                        !APIShared.UnitAccess.IsReallyAlive(lord) ||
                        lord->r_ControllableForPlayerId != owner ||
                        lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                        lord->r_CurrentHealth == 0 ||
                        lord->r_GlobalId != unchecked((uint)globalId)) return;
                    players.SetScreenCenterToUnit(lordId);
                }
            }
            catch (Exception error)
            {
                if (humanCameraFailureLogged) return;
                humanCameraFailureLogged = true;
                log.LogError("SPECTATOR_EDITOR_HUMAN_CAMERA_FAILED owner=" + owner +
                    " button=" + button + " error=" + error);
            }
        }

        private static void OnBeforeRender()
        {
            int frame = UnityEngine.Time.frameCount;
            if (lastRenderFrame == frame) return;
            lastRenderFrame = frame;
            log.Flush();
            while (presentationCallbacks.TryDequeue(out Action pending))
                try { pending(); } catch (Exception error) { LogOnce("TEST_SESSION_PRESENTATION_ERROR: " + error); }
            if (!MainViewModel.viewModelLoaded) return;
            try
            {
                SpectatorDefeat.ObservePresentedState();
                Statistics?.ObserveAvailability();
                ObserveRoster();
                bool humanEligible = IsHumanSingleplayerSession();
                SetRevealHidden(
                    humanEligible && humanPlacementMode && !IsEditorToolsSession() && visualMapDetour != null &&
                    visualFrameRegistration != null && visualResourceDetour != null ? 1 : 0);
                bool toolbarEligible = IsToolbarSession();
                if (toolbarEligible) EnsureHumanControls();
                SyncSpectatorSelection();
                RefreshHumanControls(toolbarEligible);
                if (humanEligible) { RefreshPowers(); RefreshVanillaPowerToolbar(); RefreshGateState(); }
                else { RestoreVanillaPowerToolbar(false); CancelPowerSelection(); ClearGateSelection(); }
                DrainTestActions();
                if (!IsPlacementSession())
                {
                    RestorePlacementFreeze();
                    activeHud = null;
                    initialLayoutLoggedHud = null;
                    lastOwner = int.MinValue;
                    lastScreen = int.MinValue;
                    pendingVisibilityHud = null;
                    pendingVisibilityAt = DateTime.MaxValue;
                    activeWall = null;
                    activeSpecial = null;
                    PositionToolbar();
                    return;
                }
                MainViewModel view = MainViewModel.Instance;
                HUD_Main hud = view.HUDmain;
                if (hud == null || !view.Show_HUD_Main) return;
                if (IsEditorToolsSession()) return;
                bool layoutChanged = EnsurePlacementHudLayout(hud);
                bool firstLayout = !ReferenceEquals(hud, initialLayoutLoggedHud);
                if (layoutChanged || firstLayout) hud.UpdateLayout();
                if (layoutChanged || firstLayout)
                {
                    initialLayoutLoggedHud = hud;
                    if (hudLayoutDiagnostics++ < 12)
                        log.LogInfo("SPECTATOR_EDITOR_HUD_LAYOUT screen=" + view.buildScreenID +
                            " tabs=" + EditorTabVisibility(hud) +
                            " corrected=" + layoutChanged);
                }
                if (IsSpectatorSession() && view.FreezeMainControls)
                {
                    RememberPlacementFreeze();
                    view.buildControlsFreeze(false);
                    log.LogInfo("SPECTATOR_EDITOR_FREEZE_RECOVERED");
                }
                int owner = CurrentAiOwner();
                int screen = view.buildScreenID;
                if (!ReferenceEquals(hud, activeHud) || owner != lastOwner || screen != lastScreen)
                {
                    if (screen <= 0 || screen == 15 || screen == 17 ||
                        screen == 18 || screen == 21 || screen == 22 || screen >= 23)
                        hud.SetupNewBuildScreen(1);
                    else
                        RefreshBuildIcons(hud, owner, "session");
                }
                if (!runtimeConfirmed)
                {
                    runtimeConfirmed = true;
                    log.LogInfo("SPECTATOR_EDITOR_RUNTIME_AFTER_STARTUP: HUD active; owner=" +
                        owner + " screen=" + view.buildScreenID);
                }
                if (ReferenceEquals(hud, pendingVisibilityHud) &&
                    DateTime.UtcNow >= pendingVisibilityAt)
                {
                    LogVisibilityAfterAnimation(hud);
                    pendingVisibilityHud = null;
                    pendingVisibilityAt = DateTime.MaxValue;
                }
                PositionToolbar();
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_HUD_ERROR: " + error);
            }
        }

    }
}

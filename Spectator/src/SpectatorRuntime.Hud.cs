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
            ReconcileObserverBuildHud(hud);
            humanCanvas = canvas;
            humanPanel = panel;
            humanDetails = details;
            humanDragHandle = dragHandle;
            humanToggle = toggle;
            humanToggleText = toggleText;
            long boundSession = CurrentSessionId;
            TestToolsViewModel boundModel = null;
            boundModel = new TestToolsViewModel(ExecuteToolsCommand, command =>
                ReferenceEquals(toolsModel, boundModel) && IsCurrentSession(boundSession) &&
                (command == "ToggleDesign" ? IsToolbarSession() : IsToolsSession()), PrepareDockedToolsHud);
            toolsModel = boundModel;
            toolsModel.PropertyChanged += OnToolsModelChanged;
            toolsModel.SetAvailability(IsToolsSession());
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
            toolbarPortraits.Reset();
            ReleaseActivePlayerBuildMenu();
            observerBuildHud.Reset();
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
            statusText = orderStatusText = statusShadow = orderStatusShadow = null;
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
            // Eligibility changes without a roster rebuild, including while the HUD is hidden.
            toolsModel.SetAvailability(IsToolsSession());
            toolsModel.SetOwnBuildMenu(HasOwnBuildMenu());
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
            RefreshPendingToolbarPortraits();
            string currentLocale = ActiveLocale();
            if (currentLocale != toolbarLocale)
            {
                toolbarLocale = currentLocale; toolbarDirty = true;
                foreach (var number in portraitNumbers) number?.Invalidate();
            }
            if (!toolbarDirty) return;
            toolbarDirty = false;
            toolbarLayoutDirty = true;
            toolsModel.RefreshLocale();
            int firstAi = -1, portraitMask = 0;
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
                    portraitMask |= 1 << owner;
                }
                var target = targetButtons[owner];
                bool targetAvailable = IsOrderTarget(owner);
                target.Visibility = targetAvailable ? Visibility.Visible : Visibility.Collapsed;
                MarkOwnerButton(target, owner == toolsModel.Target);
                if (targetAvailable)
                {
                    targetLabels[owner].Text = owner.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    RefreshHumanOwnerColour(owner);
                    targetLabels[owner].Foreground = humanOwnerNumbers[owner].Foreground;
                    SetPlayerTooltip(target, PlayerLabel(owner, false), owner, true);
                    portraitMask |= 1 << owner;
                }
            }
            RefreshToolbarPortraits(portraitMask);
            if (!spectatorSelectionLinked && humanSelectedOwner == -1 && !initialSelectionChosen && firstAi > 0)
            {
                int self = PlayerPerspectiveAPI.GetControlledPlayerId();
                initialSelectionChosen = true;
                ApplyHumanAi(IsActivePlayerTestSession() && IsSelectableView(self) ? self : firstAi);
            }
            else if (!IsSelectableView(humanSelectedOwner)) ApplyHumanAi(-1);
            for (int owner = 1; owner <= 8; owner++) MarkOwnerButton(humanOwnerButtons[owner], owner == humanSelectedOwner);
            toolsModel.SetRecipientAvailable(IsPlacementPlayer(humanSelectedOwner));
            RefreshOrderAvailability();
            humanToggle.IsEnabled = IsPlacementPlayer(humanSelectedOwner);
            humanToggleText.Text = SerpLocalization.Get("Tools.PlacementShort");
            RefreshOrderPresentation(true);
            RequestToolbarStatus();
            RequestToolbarLayout();
        }

        private static string cachedLocale;
        private static long cachedLocaleAt;

        // The language is resolved from Extender and Steam settings. The per-frame toolbar
        // refresh re-checks it at most once per second; a change still rebuilds the toolbar.
        private static string ActiveLocale()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (cachedLocale == null || now - cachedLocaleAt >= System.Diagnostics.Stopwatch.Frequency)
            {
                cachedLocale = SerpLocalization.GetActiveLocale();
                cachedLocaleAt = now;
            }
            return cachedLocale;
        }

        private static void SetPlayerTooltip(Button button, string label, int player, bool target = false)
        {
            string help = SerpLocalization.Get(target ? "Tools.TargetHelp" : "Tools.SelectHelp");
            string text = label + "\n" + help;
            if (playerTooltipTexts.TryGetValue(button, out string previous) && previous == text) return;
            var style = humanPanel.TryFindResource("PlayerTooltip") as Style;
            if (style == null) throw new MissingMemberException("Test tools popup style missing.");
            var content = new TextBlock { Foreground = neutralPlayerBrush,
                TextWrapping = TextWrapping.Wrap, Effect = StatusInlineFormat.Shadow() };
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

        private static void OnHumanPanelSizeChanged(object sender, SizeChangedEventArgs args) => RequestToolbarLayout();

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
            InvalidateToolbar();
            if (command == "ToggleDesign")
            { ToolbarThemePreference.Toggle(); toolbarLayoutDirty = true; return; }
            if (!IsToolsSession()) return;
            SyncSpectatorSelection();
            if (command == "StopCursor" || command == "CancelPower") { amountInput?.EndEditing(); }
            if (command == "StopCursor") { dockBuildMenu.Request(); CancelPowerSelection(); if (humanPlacementMode) ToggleHumanMode(null, null); else MainControls.instance?.StopAllPlacement(); return; }
            if (command == "CancelPower") { dockBuildMenu.Request(); CancelPowerSelection(); return; }
            if (command == "EnablePowers" || command == "DisablePowers")
            { QueueTestAction(new TestAction { Kind = TestActionKind.PowerToggle, Actor = humanSelectedOwner, Force = command == "EnablePowers" }); return; }
            if (command == "Placement") { ToggleHumanMode(null, null); return; }
            if (command == "ResetAmount") { amountInput?.ResetAmount(); return; }
            if (command == "Credit" || command == "Debit")
            { amountInput?.EndEditing(); QueueCredit(toolsModel.Good, toolsModel.Amount, command == "Debit"); return; }
            if (command == "Attack") { QueueOrder(TestActionKind.Attack, toolsModel.Target, toolsModel.ForceAcceptance); return; }
            if (command == "Defend") { QueueOrder(TestActionKind.Defend, toolsModel.Target, toolsModel.ForceAcceptance); return; }
            if (command == "Cancel") { QueueOrder(TestActionKind.Cancel, 0, false); return; }
            if (command != null && command.StartsWith("Target", StringComparison.Ordinal) &&
                int.TryParse(command.Substring(6), out int target) && IsOrderTarget(target))
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
            RequestToolbarLayout();
            args.Handled = true;
        }

        private static void OnHumanDragMove(object sender, MouseEventArgs args)
        {
            if (!humanDragging || humanCanvas == null || humanPanel == null) return;
            Point current = args.GetPosition(humanCanvas);
            SnapToolbarDrag(current.X, current.Y);
            RequestToolbarLayout();
            args.Handled = true;
        }

        private static void OnHumanDragUp(object sender, MouseButtonEventArgs args)
        {
            if (humanDragging && humanCanvas != null)
            {
                Point current = args.GetPosition(humanCanvas);
                Point origin = humanCanvas.PointToScreen(new Point(0, 0));
                Point unit = humanCanvas.PointToScreen(new Point(100, 100));
                toolbarMoved = !toolbarDrag.Drop(current.X, current.Y, ToolbarDropTarget((unit.Y - origin.Y) / 100),
                    (unit.X - origin.X) / 100, (unit.Y - origin.Y) / 100, toolbarFloatingScale,
                    out toolbarDesiredX, out toolbarDesiredY);
                if (!toolbarMoved) dockBuildMenu.Request();
            }
            humanDragging = false;
            RequestToolbarLayout();
            if (humanDragHandle != null && humanDragHandle.IsMouseCaptured) humanDragHandle.ReleaseMouseCapture();
            args.Handled = true;
        }

        private static void OnHumanLostCapture(object sender, MouseEventArgs args)
        { humanDragging = false; toolbarDrag.Cancel(); RequestToolbarLayout(); }
        private static void OnHumanCanvasSizeChanged(object sender, SizeChangedEventArgs args)
        { InvalidateToolbar(); }

        private static void SuspendToolbarInteraction()
        {
            UpdateSpectatorDate(false);
            humanDragging = false; toolbarDrag.Cancel();
            if (humanDragHandle?.IsMouseCaptured == true) humanDragHandle.ReleaseMouseCapture();
            amountInput?.EndEditing(); ClosePlayerTooltips();
            HideDockGuides();
        }

        private static void OnHumanCanvasVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            InvalidateToolbar();
            if (humanCanvas?.IsVisible != true) SuspendToolbarInteraction();
        }

        private static RadioButton[] NormalTabs(HUD_Main hud) => new[] {
            hud.RefTabBuildCastle, hud.RefTabBuildIndustry, hud.RefTabBuildFarms,
            hud.RefTabBuildTown, hud.RefTabBuildWeapons, hud.RefTabBuildFood
        };

        private static void ToggleHumanMode(object sender, RoutedEventArgs args)
        {
            buildPageMutation++;
            try { ToggleHumanModeCore(); }
            finally { buildPageMutation--; }
        }

        private static void ToggleHumanModeCore()
        {
            if (!IsHumanSingleplayerSession()) return;
            SyncSpectatorSelection();
            if (!humanPlacementMode && !IsPlacementPlayer(humanSelectedOwner)) return;
            CancelPowerSelection();
            InvalidateToolbar();
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
                RestorePlacementLayout(); playerPlacementPage = null;
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
                    if (IsPlacementPlayer(owner)) { firstAi = owner; break; }
                if (firstAi < 0) return;
                humanSavedScreen = MainViewModel.Instance.buildScreenID;
                if (!GateHudLifecycle.IsBuildScreen(humanSavedScreen) && humanSavedScreen != DockHudPolicy.BlankBuildScreen) humanSavedScreen = 1;
                humanSavedSubMode = MainViewModel.Instance.SubMode;
                if (IsActivePlayerTestSession())
                    playerPlacementPage = new BuildMenuSnapshot(hud, humanSavedScreen, humanSavedSubMode, CaptureBuildTabs(hud));
                RadioButton[] tabs = NormalTabs(hud);
                humanSavedTabs = new bool?[tabs.Length];
                for (int index = 0; index < tabs.Length; index++)
                    humanSavedTabs[index] = tabs[index]?.IsChecked;
                MainControls.instance.StopAllPlacement();
                if (!IsPlacementPlayer(humanSelectedOwner)) humanSelectedOwner = firstAi;
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

        private static bool PrepareDockedToolsHud()
        {
            try
            {
                if (!IsObserverBuildUiThread || !IsToolsSession() || !MainViewModel.viewModelLoaded) return false;
                var view = MainViewModel.Instance;
                if (view.HUDmain == null) return false;
                // Retire a borrowed gate/building panel through the same transition as Place.
                // The menu publishes expansion only after the native build HUD is available.
                if (!view.Show_HUD_Main || view.Show_HUD_Troops || view.Show_HUD_Building) EnterPlacementHud(view);
                if (!view.Show_HUD_Main || view.Show_HUD_Troops || view.Show_HUD_Building) return false;
                dockBuildMenu.Request();
                return true;
            }
            catch (Exception error) { LogOnce("TEST_TOOLS_HUD_OPEN_ERROR: " + error); return false; }
        }

        private static void SelectHumanAi(int owner)
        {
            if (!IsToolbarSession() || !IsSelectableView(owner)) return;
            if (SpectatorPerspectiveRuntime.IsSpectatorRoster())
            {
                // The roster is shown before perspective initialization; selection waits for it.
                if (!SpectatorPerspectiveRuntime.IsActiveSpectator()) return;
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

        internal static void PrepareEliminatedObserverHud()
        {
            // The old positive participant can still own market, troop and building controls.
            // Retire their cursors and borrowed pages before the first foreign report viewpoint.
            // Vanilla's own deselection while the native view is still the participant's own: a queued
            // deselect then carries the participant as its source. Afterwards CloseTroopsPanel (ESC,
            // Reports) has no selection left that it could deselect as a foreign view.
            try { EngineInterface.GameAction(Enums.GameActionCommand.CloseTroopsPanel, 0, 0); }
            catch (Exception error) { LogOnce("SPECTATOR_OBSERVER_DESELECT_FAILED: " + error); }
            humanPlacementMode = false;
            CancelSelectionActions();
            RestorePlacementLayout(); playerPlacementPage = null;
            dockBuildMenu.Release(); dockBuildMenu.Reset();
            RestorePlacementFreeze();
            ReleaseGateHud(false);
            SetRevealHidden(0);
            ApplyHumanAi(-1);
            InvalidateToolbar();
            if (!MainViewModel.viewModelLoaded) return;
            var view = MainViewModel.Instance;
            view.AlliesPanelVisible = false;
            // Preserve an actual defeat/briefing overlay while hiding interactive panels behind it.
            if (!view.Show_HUD_MissionOver && !view.Show_HUD_Briefing) view.DefaultGameUIGameAction();
            else { view.Show_HUD_Building = false; view.Show_HUD_Troops = false; }
            view.buildControlsFreeze(true);
            // Promotion is confirmed only after this method returns. Clear explicitly
            // now, before a first foreign viewpoint can make the old icons selectable.
            ReconcileObserverBuildHud(preparingPromotion: true);
        }

        private static int controlUnfreezeDiagnostics;
        private static int spriteLeasePlayer;
        private static bool spriteLeaseFailureLogged;

        // Unit HUD sprites (troop tab, recruitment, portraits) follow the player placed for, otherwise the
        // input-controlled player. Only a change calls APIShared; the editor recolours by itself.
        private static void UpdateSpriteColourLease()
        {
            int desired = IsEditorToolsSession() ? 0 : CurrentAiOwner();
            if (desired < 1) desired = SpectatorPerspectiveRuntime.InputControlledPlayer();
            if (desired == spriteLeasePlayer) return;
            spriteLeasePlayer = desired;
            if (desired < 1) { APIShared.UnitHudSpriteColours.Release(Guid); return; }
            // A refused lease is not retried per frame; the next change of player tries again.
            if (!APIShared.UnitHudSpriteColours.TryLease(Guid, desired, out string reason) && !spriteLeaseFailureLogged)
            {
                spriteLeaseFailureLogged = true;
                log.LogWarning("SPECTATOR_SPRITE_COLOUR_UNAVAILABLE: player=" + desired + "; " + reason);
            }
        }

        private static void ResetSpriteColourLease()
        {
            // APIShared also ends the lease at mission end; this only clears our cached player.
            if (spriteLeasePlayer > 0) APIShared.UnitHudSpriteColours.Release(Guid);
            spriteLeasePlayer = 0;
        }

        // Vanilla freezes build tabs for a nonpositive managed player; the controlled player
        // uses them like a normal player. RestorePlacementFreeze re-freezes after control ends.
        private static void UnfreezeControlledBuild()
        {
            var view = MainViewModel.Instance;
            if (!view.FreezeMainControls) return;
            RememberPlacementFreeze();
            view.buildControlsFreeze(false);
            // An open Tools panel keeps its leased blank page; otherwise show the castle page. Marked
            // as our own page change, so the lease observer does not treat it as a Vanilla choice.
            if (dockBuildMenu.Saved == null && view.HUDmain != null)
            {
                buildPageMutation++;
                try { view.HUDmain.SetupNewBuildScreen(1); }
                finally { buildPageMutation--; }
            }
            if (controlUnfreezeDiagnostics++ < 8)
                log.LogInfo("SPECTATOR_CONTROL_BUILD_UNFROZEN player=" + SpectatorPerspectiveRuntime.InputControlledPlayer());
        }

        private static void RenderFrame()
        {
            log.Flush();
            while (presentationCallbacks.TryDequeue(out Action pending))
                try { pending(); } catch (Exception error) { LogOnce("TEST_SESSION_PRESENTATION_ERROR: " + error); }
            if (!MainViewModel.viewModelLoaded) return;
            try
            {
                // Disabling the last applicable feature must retire the old UI too;
                // it cannot depend on another feature keeping presentation alive.
                if (!SessionNeedsPresentation())
                {
                    // Pending mutations must neither keep waking this hidden UI nor
                    // execute belatedly when a test mode is enabled again.
                    while (testActions.TryDequeue(out _)) { }
                    RetireUnavailablePlacement();
                    if (toolsModel?.ToolsAvailable == true)
                    {
                        RestoreVanillaPowerToolbar(false);
                        CancelPowerSelection();
                        ClearGateSelection();
                        SetRevealHidden(0);
                        ReleaseActivePlayerBuildMenu();
                        if (!IsControlledBuildSession()) RestorePlacementFreeze();
                        toolsModel.SetAvailability(false);
                    }
                    if (humanPanel?.Visibility == Visibility.Visible)
                    {
                        humanPanel.Visibility = Visibility.Collapsed;
                        SuspendToolbarInteraction();
                    }
                    return;
                }
                // Identity is an input safety guard, independent of the slower status cadence.
                ObservePresentedPerspective();
                if (!presentationRefresh.Ready(System.Diagnostics.Stopwatch.GetTimestamp())) return;
                ObservePresentationView(MainViewModel.Instance);
                bool stateCheck = TakePresentationStatusCheck();
                if (stateCheck) ObservePresentationColours();
                bool rosterChanged = rosterPending && (stateCheck || toolbarDirty);
                bool contentChanged = toolbarDirty;
                if (stateCheck || rosterChanged || contentChanged)
                {
                    if (SpectatorDefeat.NeedsPresentation) SpectatorDefeat.ObservePresentedState();
                    ReconcileObserverBuildHud();
                    Statistics?.ObserveAvailability(contentChanged);
                    if (rosterChanged) { rosterPending = false; ObserveRoster(); }
                    RetireUnavailablePlacement();
                    if (toolbarDirty) SyncSpectatorSelection();
                    UpdateSpriteColourLease();
                    presentedToolsEligible = IsHumanSingleplayerSession();
                    presentedToolbarEligible = IsToolbarSession();
                }
                bool humanEligible = presentedToolsEligible;
                if (stateCheck || contentChanged) SetRevealHidden(
                    humanEligible && humanPlacementMode && !IsEditorToolsSession() && visualMapDetour != null &&
                    visualFrameRegistration != null && visualResourceDetour != null ? 1 : 0);
                bool toolbarEligible = presentedToolbarEligible;
                if (toolbarEligible && (stateCheck || humanPanel == null)) EnsureHumanControls();
                if (stateCheck || toolbarDirty) RefreshHumanControls(toolbarEligible);
                if (humanEligible)
                {
                    if (stateCheck || contentChanged) RefreshPowers();
                    else if (pendingPower != null) RefreshPowerSelection();
                    if (stateCheck || contentChanged || powerToolbarOwned) RefreshVanillaPowerToolbar();
                    if (gateHudLifecycle.Pending || selectedGate != null || observedGate != null)
                        RefreshGateState(stateCheck || contentChanged);
                }
                else if (stateCheck || contentChanged)
                { RestoreVanillaPowerToolbar(false); CancelPowerSelection(); ClearGateSelection(); }
                DrainTestActions();
                if (HasToolbarViewport() && (stateCheck || contentChanged || toolbarLayoutDirty)) UpdateActivePlayerBuildMenu();
                if (toolbarStatusDirty && humanPanel?.Visibility == Visibility.Visible)
                {
                    toolbarStatusDirty = false;
                    RefreshColouredStatus();
                    RefreshOrderPresentation();
                }
                if (!IsPlacementSession())
                {
                    if (IsControlledBuildSession()) UnfreezeControlledBuild();
                    else RestorePlacementFreeze();
                    activeHud = null;
                    initialLayoutLoggedHud = null;
                    lastOwner = int.MinValue;
                    lastScreen = int.MinValue;
                    DiagForgetIcons();
                    activeWall = null;
                    activeSpecial = null;
                    PositionToolbar();
                    return;
                }
                MainViewModel view = MainViewModel.Instance;
                HUD_Main hud = view.HUDmain;
                if (hud == null || !view.Show_HUD_Main)
                {
                    // Placement may remain active while troop/building HUDs replace the build menu.
                    PositionToolbar();
                    return;
                }
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
                    if (!GateHudLifecycle.IsBuildScreen(screen))
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
                DiagIconsSettled(hud);
                PositionToolbar();
            }
            catch (Exception error)
            {
                // Earlier consumers can have cleared their flags before a later UI write failed.
                // Retry the reconcile, not the already-dequeued commands or presentation callbacks.
                rosterState = null;
                presentationRefresh.Failed(System.Diagnostics.Stopwatch.GetTimestamp(), System.Diagnostics.Stopwatch.Frequency);
                renderWork.RequestAfter(presentationStatusInterval);
                LogOnce("SPECTATOR_EDITOR_HUD_ERROR: " + error);
            }
        }

    }
}

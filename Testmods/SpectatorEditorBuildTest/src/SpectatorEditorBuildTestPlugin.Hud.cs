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

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private static CreditAmountInput amountInput;
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
                Interlocked.Exchange(ref revealHiddenActive, 0);
                humanSelectedOwner = -1;
                humanSavedTabs = null;
                activeWall = null;
                activeSpecial = null;
                RestoreDateLayout();
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
            if (canvas == null || panel == null || dragHandle == null || toggle == null ||
                toggleText == null || owners == null)
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
            humanDragHandle = dragHandle;
            humanToggle = toggle;
            humanToggleText = toggleText;
            humanOwners = owners;
            toolsModel = new TestToolsViewModel(ExecuteToolsCommand, IsToolsSession);
            panel.DataContext = toolsModel;
            toolsModel.Status = SerpLocalization.Get("Tools.Ready");
            var amount = screen.FindName("TestToolsAmount") as TextBox;
            if (amount == null) throw new MissingMemberException("Test tools amount field missing.");
            amountInput = new CreditAmountInput(amount, toolsModel);
            panel.SizeChanged += OnHumanPanelSizeChanged;
            toggle.Click += ToggleHumanMode;
            dragHandle.MouseLeftButtonDown += OnHumanDragDown;
            dragHandle.MouseMove += OnHumanDragMove;
            dragHandle.MouseLeftButtonUp += OnHumanDragUp;
            dragHandle.LostMouseCapture += OnHumanLostCapture;
            canvas.SizeChanged += OnHumanCanvasSizeChanged;
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
                    if (!IsHumanSingleplayerSession() ||
                        !IsSelectableAi(selected)) return;
                    args.Handled = true;
                    JumpToHumanAi(selected, args.ChangedButton);
                };
                found[owner].PreviewMouseDown += humanJumpHandlers[owner];
            }
            humanPositioned = false;
            humanUserMoved = false;
            log.LogInfo("SPECTATOR_EDITOR_HUMAN_CONTROLS_READY");
        }

        private static void DetachHumanControls()
        {
            RestoreVanillaPowerToolbar(true);
            CancelPowerSelection();
            ClearGateSelection();
            consumePowerGesture = false;
            powerMouseState = toolMouseState = 0;
            observedGate = null;
            amountInput?.Detach();
            amountInput = null;
            playerTooltipTexts.Clear();
            if (humanPanel != null) humanPanel.SizeChanged -= OnHumanPanelSizeChanged;
            if (humanToggle != null) humanToggle.Click -= ToggleHumanMode;
            if (humanDragHandle != null)
            {
                humanDragHandle.MouseLeftButtonDown -= OnHumanDragDown;
                humanDragHandle.MouseMove -= OnHumanDragMove;
                humanDragHandle.MouseLeftButtonUp -= OnHumanDragUp;
                humanDragHandle.LostMouseCapture -= OnHumanLostCapture;
                if (humanDragHandle.IsMouseCaptured) humanDragHandle.ReleaseMouseCapture();
            }
            if (humanCanvas != null) humanCanvas.SizeChanged -= OnHumanCanvasSizeChanged;
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
            humanPositioned = false;
            humanUserMoved = false;
            humanPanel = null;
            humanDragHandle = null;
            humanCanvas = null;
            humanToggle = null;
            humanToggleText = null;
            humanOwners = null;
            humanScreen = null;
            humanBoundHud = null;
        }

        private static void RefreshHumanControls(bool eligible)
        {
            if (humanPanel == null || toolsModel == null) return;
            bool show = eligible && (IsEditorToolsSession() || MainViewModel.Instance.Show_HUD_Main || selectedGate != null) &&
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
                button.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
                if (available)
                {
                    RefreshHumanOwnerColour(owner);
                    string label = PlayerLabel(owner);
                    humanOwnerNumbers[owner].Text = label;
                    SetPlayerTooltip(button, label);
                    RefreshPlayerFace("OwnerFace" + owner, owner);
                }
                var target = humanScreen.FindName("TestTarget" + owner) as Button;
                bool targetAvailable = IsToolsPlayer(owner, false);
                target.Visibility = targetAvailable ? Visibility.Visible : Visibility.Collapsed;
                target.Background = owner == toolsModel.Target ? ownerSelectedBrush : ownerNormalBrush;
                if (targetAvailable)
                {
                    string label = PlayerLabel(owner);
                    var targetText = (TextBlock)humanScreen.FindName("TestTargetText" + owner);
                    targetText.Text = label;
                    RefreshHumanOwnerColour(owner);
                    targetText.Foreground = humanOwnerNumbers[owner].Foreground;
                    SetPlayerTooltip(target, label);
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
            toolsModel.SetRecipientAvailable(IsSelectableAi(humanSelectedOwner));
            humanToggle.IsEnabled = humanPlacementMode || firstAi > 0;
            humanToggleText.Text = SerpLocalization.Get(humanPlacementMode ? "Tools.StopPlacement" : "Tools.Placement");
            humanOwners.Visibility = toolsModel.Expanded ? Visibility.Visible : Visibility.Collapsed;
            toolsModel.SelectedPlayer = SerpLocalization.Get("Tools.Actor") + ": " +
                (humanSelectedOwner > 0 ? PlayerLabel(humanSelectedOwner) : SerpLocalization.Get("Tools.NoAi"));
            toolsModel.SelectedTarget = SerpLocalization.Get("Tools.Target") + ": " +
                (IsToolsPlayer(toolsModel.Target, false) ? PlayerLabel(toolsModel.Target) : SerpLocalization.Get("Tools.SelectTarget"));
            var root = humanScreen.FindName("TestToolsRoot") as Grid;
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
            try { ExecuteToolsCommandCore(command); }
            catch (Exception error)
            {
                LogOnce("TEST_TOOLS_UI_ERROR feature=" + command + ": " + error);
                if (toolsModel != null) toolsModel.Status = SerpLocalization.Get("Tools.Failed");
            }
        }

        private static void ExecuteToolsCommandCore(string command)
        {
            if (!IsToolsSession() || toolsModel == null) return;
            if (command == "StopCursor" || command == "CancelPower") amountInput?.EndEditing();
            if (command == "StopCursor") { CancelPowerSelection(); MainControls.instance?.StopAllPlacement(); return; }
            if (command == "CancelPower") { CancelPowerSelection(); return; }
            if (command == "EnablePowers" || command == "DisablePowers")
            { QueueTestAction(new TestAction { Kind = TestActionKind.PowerToggle, Actor = humanSelectedOwner, Force = command == "EnablePowers" }); return; }
            if (command == "Credit") { QueueCredit(toolsModel.Good, toolsModel.Amount); return; }
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

        private static void PositionHumanPanel()
        {
            if (humanCanvas == null || humanPanel == null ||
                humanCanvas.ActualWidth <= 0f || humanCanvas.ActualHeight <= 0f) return;
            if (!humanPositioned || !humanUserMoved)
            {
                Canvas.SetLeft(humanPanel,
                    Math.Max(0f, humanCanvas.ActualWidth - humanPanel.Width - 52f));
                Canvas.SetTop(humanPanel, 54f);
                humanPositioned = true;
            }
            ClampHumanPanel();
        }

        private static void ClampHumanPanel()
        {
            if (!humanPositioned || humanCanvas == null || humanPanel == null ||
                humanCanvas.ActualWidth <= 0f || humanCanvas.ActualHeight <= 0f) return;
            float maxLeft = Math.Max(0f, humanCanvas.ActualWidth - humanPanel.Width);
            float maxTop = Math.Max(0f, humanCanvas.ActualHeight - humanPanel.ActualHeight);
            Canvas.SetLeft(humanPanel, Math.Max(0f, Math.Min(maxLeft, (float)Canvas.GetLeft(humanPanel))));
            Canvas.SetTop(humanPanel, Math.Max(0f, Math.Min(maxTop, (float)Canvas.GetTop(humanPanel))));
        }

        private static void OnHumanDragDown(object sender, MouseButtonEventArgs args)
        {
            if (humanCanvas == null || humanPanel == null || humanDragHandle == null) return;
            humanDragStart = args.GetPosition(humanCanvas);
            humanDragOriginLeft = (float)Canvas.GetLeft(humanPanel);
            humanDragOriginTop = (float)Canvas.GetTop(humanPanel);
            humanDragging = humanDragHandle.CaptureMouse();
            if (humanDragging) humanUserMoved = true;
            args.Handled = true;
        }

        private static void OnHumanDragMove(object sender, MouseEventArgs args)
        {
            if (!humanDragging || humanCanvas == null || humanPanel == null) return;
            Point current = args.GetPosition(humanCanvas);
            Canvas.SetLeft(humanPanel,
                humanDragOriginLeft + (float)(current.X - humanDragStart.X));
            Canvas.SetTop(humanPanel,
                humanDragOriginTop + (float)(current.Y - humanDragStart.Y));
            ClampHumanPanel();
            args.Handled = true;
        }

        private static void OnHumanDragUp(object sender, MouseButtonEventArgs args)
        {
            humanDragging = false;
            if (humanDragHandle != null && humanDragHandle.IsMouseCaptured)
                humanDragHandle.ReleaseMouseCapture();
            args.Handled = true;
        }

        private static void OnHumanLostCapture(object sender, MouseEventArgs args) =>
            humanDragging = false;

        private static void OnHumanCanvasSizeChanged(object sender, SizeChangedEventArgs args) =>
            PositionHumanPanel();

        private static RadioButton[] NormalTabs(HUD_Main hud) => new[] {
            hud.RefTabBuildCastle, hud.RefTabBuildIndustry, hud.RefTabBuildFarms,
            hud.RefTabBuildTown, hud.RefTabBuildWeapons, hud.RefTabBuildFood
        };

        private static void ToggleHumanMode(object sender, RoutedEventArgs args)
        {
            if (!IsHumanSingleplayerSession()) return;
            CancelPowerSelection();
            amountInput?.EndEditing();
            HUD_Main hud = MainViewModel.Instance.HUDmain;
            if (hud == null || MainControls.instance == null) return;
            if (IsEditorToolsSession())
            {
                MainControls.instance.StopAllPlacement();
                humanPlacementMode = !humanPlacementMode;
                Interlocked.Exchange(ref revealHiddenActive, 0);
                RefreshHumanControls(true);
                return;
            }
            if (humanPlacementMode)
            {
                humanPlacementMode = false;
                Interlocked.Exchange(ref revealHiddenActive, 0);
                MainControls.instance.StopAllPlacement();
                activeWall = null;
                activeSpecial = null;
                RestoreDateLayout();
                MainViewModel.Instance.SubMode = humanSavedSubMode;
                hud.SetupNewBuildScreen(-1000);
                if (humanSavedScreen != 0) hud.SetupNewBuildScreen(humanSavedScreen);
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
                if (humanSavedScreen < 0 || humanSavedScreen >= 23) humanSavedScreen = 1;
                humanSavedSubMode = MainViewModel.Instance.SubMode;
                RadioButton[] tabs = NormalTabs(hud);
                humanSavedTabs = new bool?[tabs.Length];
                for (int index = 0; index < tabs.Length; index++)
                    humanSavedTabs[index] = tabs[index]?.IsChecked;
                MainControls.instance.StopAllPlacement();
                if (!IsSelectableAi(humanSelectedOwner)) humanSelectedOwner = firstAi;
                humanPlacementMode = true;
                Interlocked.Exchange(ref revealHiddenActive,
                    visualMapDetour != null && visualFrameDetour != null &&
                    visualResourceDetour != null ? 1 : 0);
                if (Volatile.Read(ref revealHiddenActive) == 0)
                    log.LogWarning("SPECTATOR_EDITOR_VISUAL_UNAVAILABLE: audited native hooks are not installed");
                hud.SetupNewBuildScreen(1);
                RefreshBuildIcons(hud, humanSelectedOwner, "human-enter");
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_MODE_ON owner=" + humanSelectedOwner +
                    " savedScreen=" + humanSavedScreen);
            }
            RefreshHumanControls(true);
        }

        private static void SelectHumanAi(int owner)
        {
            if (!IsHumanSingleplayerSession() || !IsSelectableAi(owner)) return;
            if (humanSelectedOwner == owner) return;
            CancelPowerSelection();
            ClearGateSelection();
            MainControls.instance?.StopAllPlacement();
            activeWall = null;
            activeSpecial = null;
            humanSelectedOwner = owner;
            if (humanPlacementMode && MainViewModel.Instance.HUDmain != null)
                RefreshBuildIcons(MainViewModel.Instance.HUDmain, owner, "human-owner");
            RefreshHumanControls(true);
            log.LogInfo("SPECTATOR_EDITOR_HUMAN_OWNER owner=" + owner);
        }

        private static unsafe void JumpToHumanAi(int owner, MouseButton button)
        {
            if (!IsHumanSingleplayerSession() || !IsSelectableAi(owner)) return;
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
                bool humanEligible = IsHumanSingleplayerSession();
                Interlocked.Exchange(ref revealHiddenActive,
                    humanEligible && humanPlacementMode && !IsEditorToolsSession() && visualMapDetour != null &&
                    visualFrameDetour != null && visualResourceDetour != null ? 1 : 0);
                if (humanEligible) EnsureHumanControls();
                RefreshHumanControls(humanEligible);
                if (humanEligible) { RefreshPowers(); RefreshVanillaPowerToolbar(); RefreshGateState(); }
                else { RestoreVanillaPowerToolbar(false); CancelPowerSelection(); ClearGateSelection(); }
                DrainTestActions();
                if (!IsPlacementSession())
                {
                    RestoreDateLayout();
                    activeHud = null;
                    initialLayoutLoggedHud = null;
                    lastOwner = int.MinValue;
                    lastScreen = int.MinValue;
                    pendingVisibilityHud = null;
                    pendingVisibilityAt = DateTime.MaxValue;
                    activeWall = null;
                    activeSpecial = null;
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
                PositionDate(hud);
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_HUD_ERROR: " + error);
            }
        }

        private static void PositionDate(HUD_Main hud)
        {
            var date = hud.FindName("OST_Date") as TextBlock;
            var lastTab = hud.FindName("RadioButtonMEBedouin") as FrameworkElement;
            var allies = hud.FindName("ButtonAllies") as FrameworkElement;
            var merit = hud.FindName("ButtonMerit") as FrameworkElement;
            if (date == null || lastTab == null || merit == null) return;
            if (dateLayout == null || !ReferenceEquals(dateLayout.Hud, hud) ||
                !ReferenceEquals(dateLayout.Date, date))
            {
                RestoreDateLayout();
                var group = date.RenderTransform as TransformGroup;
                var translate = group != null && group.Children.Count > 0
                    ? group.Children[0] as TranslateTransform : null;
                if (translate == null)
                    throw new MissingMemberException("HUD_Main", "OST_Date translation");
                dateLayout = new DateLayoutState { Hud = hud, Date = date,
                    Translate = translate, OriginalX = translate.X,
                    OriginalFontSize = date.FontSize,
                    OriginalHitTest = date.IsHitTestVisible };
            }
            date.IsHitTestVisible = false;
            if (!lastTab.IsVisible || lastTab.ActualWidth <= 0) return;
            double left = lastTab.TranslatePoint(new Point(lastTab.ActualWidth, 0), hud).X;
            double right = allies != null && allies.IsVisible
                ? allies.TranslatePoint(new Point(0, 0), hud).X
                : merit.TranslatePoint(new Point(0, 0), hud).X;
            if (right <= left + 8) return;
            DateLayoutState state = dateLayout;
            string text = date.Text ?? "";
            if (state.LastLeft == left && state.LastRight == right &&
                state.LastHudWidth == hud.ActualWidth && state.LastText == text &&
                state.LastTranslation == state.Translate.X &&
                state.LastFontSize == date.FontSize)
                return;

            state.Translate.X = 0;
            if (date.FontSize != state.OriginalFontSize)
                date.FontSize = state.OriginalFontSize;
            date.UpdateLayout();
            double available = right - left - 8;
            double naturalWidth = date.ActualWidth;
            if (naturalWidth <= 0) return;
            if (naturalWidth > available)
            {
                date.FontSize = (float)Math.Max(1,
                    state.OriginalFontSize * available / naturalWidth);
                date.UpdateLayout();
                if (date.ActualWidth > available)
                {
                    date.FontSize *= (float)(available / date.ActualWidth);
                    date.UpdateLayout();
                }
            }
            double baseX = date.TranslatePoint(new Point(0, 0), hud).X;
            double targetX = left + 4 + (available - date.ActualWidth) / 2;
            state.Translate.X = (float)(targetX - baseX);
            state.LastLeft = left;
            state.LastRight = right;
            state.LastHudWidth = hud.ActualWidth;
            state.LastText = text;
            state.LastTranslation = state.Translate.X;
            state.LastFontSize = date.FontSize;
            if (dateLayoutDiagnostics++ < 24)
                log.LogInfo("SPECTATOR_EDITOR_DATE_LAYOUT left=" + left +
                    " right=" + right + " textWidth=" + date.ActualWidth +
                    " font=" + date.FontSize + " target=" + targetX +
                    " translation=" + state.Translate.X + " text=" + text);
        }

        private static void RestoreDateLayout()
        {
            DateLayoutState state = dateLayout;
            dateLayout = null;
            if (state == null) return;
            try
            {
                state.Translate.X = state.OriginalX;
                state.Date.FontSize = state.OriginalFontSize;
                state.Date.IsHitTestVisible = state.OriginalHitTest;
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_DATE_RESTORE_ERROR: " + error);
            }
        }

    }
}

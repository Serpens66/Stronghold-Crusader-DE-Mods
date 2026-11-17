using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using APIShared;
using APIShared.Units.AiMilitary;
using CrusaderDE;
using Noesis;
using SHCDESE.Interop;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static bool TryAiMapperType(int mapper, out eChimps type) => AiMapperTypes.TryGet((eMappers)mapper, out type);
        private static volatile IAiMilitaryCapability aiMilitary;
        private static readonly AiPlacementModel aiPlacement = new AiPlacementModel();
        private static Border aiPlacementPanel;
        private static Canvas aiPlacementHost, aiEditorCanvas;
        private static IngameUIScreens aiEditorScreen;
        private static ComboBox aiRolePicker;
        private static Popup aiRolePopup;
        private static AiPlacementChoice[] aiRenderedChoices;
        private static readonly List<ComboBoxItem> aiRoleRows = new List<ComboBoxItem>();
        private static CheckBox aiAdditional, aiEditorApply;
        private static TextBlock aiPlacementTitle, aiPlacementStatus;
        private static Border aiPlacementGrip;
        private static StackPanel aiPlacementRow;
        private static UIElement aiInputRoot;
        private static readonly AiPlacementPosition aiPanelPosition = new AiPlacementPosition();
        private static ToolbarRect aiPanelDock, aiPanelBounds;
        private static bool aiPositioning;
        private static bool aiUpdating;
        private static int aiShownMapper = -1, aiShownOwner = -1;
        private static string aiShownLocale;
        private static float aiPlacementScale = -1;
        private static readonly List<ToolbarRect> aiPlacementObstacles = new List<ToolbarRect>();
        private static AiPlacementIntent aiIntent = new AiPlacementIntent(default, false);
        static partial void DiagAiPlacementResult(long session, int owner, AiMilitaryAssignmentResult result);
        static partial void DiagAiPlacementReset();
        static partial void DiagAiPlacementSessionStarted(long session);

        private sealed class AiPlacementIntent
        {
            internal readonly AiMilitaryAssignment Assignment;
            internal readonly bool ApplyInEditor;
            internal AiPlacementIntent(AiMilitaryAssignment assignment, bool apply)
            { Assignment = assignment; ApplyInEditor = apply; }
        }

        private static void InitializeAiPlacement()
        {
            aiPlacement.PropertyChanged += AiPlacementChanged;
            ApiShared.ForMod(SpectatorPlugin.PluginGuid).WhenReady(client => {
                if (!client.TryGetAiMilitary(out var capability, out var diagnostic))
                { log.LogWarning("SPECTATOR_AI_UNAVAILABLE: state=" + diagnostic.State + " reason=" + diagnostic.Reason); return; }
                aiMilitary = capability;
                PostPresentation(() => { aiShownMapper = -1; InvalidateToolbar(); });
            });
        }

        private static void AiPlacementChanged(object sender, PropertyChangedEventArgs args)
        {
            AiMilitaryRole role;
            if (!Enum.TryParse(aiPlacement.Role, out role)) role = AiMilitaryRole.None;
            Volatile.Write(ref aiIntent, new AiPlacementIntent(new AiMilitaryAssignment(role,
                aiPlacement.Additional ? AiRecruitmentMode.Additional : AiRecruitmentMode.Regular), aiPlacement.ApplyInEditor));
            if (!aiUpdating) UpdateAiPlacementControls();
        }

        private static void EnsureAiPlacementPanel()
        {
            bool editor = IsEditorAiPlacementSession();
            var screen = MainViewModel.Instance?.IngameUI;
            if (aiEditorCanvas != null && (!editor || !ReferenceEquals(screen, aiEditorScreen))) DetachAiPlacementPanel();
            if (editor && aiEditorCanvas == null)
            {
                var template = screen?.FindName("SpectatorEditorHumanCanvas") as Canvas;
                if (!(template?.Parent is Panel parent)) return;
                aiEditorCanvas = new Canvas { Name = "SpectatorAiEditorCanvas" };
                Grid.SetRowSpan(aiEditorCanvas, Grid.GetRowSpan(template));
                Grid.SetColumnSpan(aiEditorCanvas, Grid.GetColumnSpan(template));
                Panel.SetZIndex(aiEditorCanvas, 10032);
                parent.Children.Add(aiEditorCanvas); aiEditorScreen = screen;
            }
            aiPlacementHost = editor ? aiEditorCanvas : humanCanvas;
            if (aiPlacementPanel != null || aiPlacementHost == null) return;
            var body = new StackPanel();
            aiPlacementTitle = new TextBlock { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 460 };
            aiPlacementGrip = new Border { Name = "SpectatorAiPlacementGrip", Background = Brushes.Transparent,
                Padding = new Thickness(0, 0, 0, 4), Child = aiPlacementTitle };
            body.Children.Add(aiPlacementGrip);
            aiPlacementRow = new StackPanel { Orientation = Orientation.Horizontal };
            aiRolePicker = new ComboBox { Name = "SpectatorAiRolePicker", MinWidth = 260, MaxWidth = 300,
                MinHeight = 26, Padding = new Thickness(6, 3, 6, 3) };
            aiRolePicker.SelectionChanged += AiRoleChanged;
            aiRolePicker.PreviewMouseDown += AiRoleOpening;
            aiRolePicker.Loaded += AiRolePickerLoaded; aiRolePicker.Reloaded += AiRolePickerLoaded;
            aiAdditional = new CheckBox { Name = "SpectatorAiAdditional", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            aiAdditional.Checked += AiAdditionalChanged; aiAdditional.Unchecked += AiAdditionalChanged;
            aiPlacementRow.Children.Add(aiRolePicker); aiPlacementRow.Children.Add(aiAdditional); body.Children.Add(aiPlacementRow);
            aiEditorApply = new CheckBox { Name = "SpectatorAiEditorApply", Margin = new Thickness(0, 4, 0, 0) };
            aiEditorApply.Checked += AiEditorApplyChanged; aiEditorApply.Unchecked += AiEditorApplyChanged;
            body.Children.Add(aiEditorApply);
            aiPlacementStatus = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            body.Children.Add(aiPlacementStatus);
            aiPlacementPanel = new Border { Name = "SpectatorAiPlacementPanel", DataContext = aiPlacement, MaxWidth = 500, Padding = new Thickness(6), BorderThickness = new Thickness(1),
                Child = body, Visibility = Visibility.Collapsed };
            Panel.SetZIndex(aiPlacementPanel, 3);
            aiPlacementHost.Children.Add(aiPlacementPanel);
            AttachAiPanelInteraction();
            if (!editor && humanPanel != null) humanPanel.IsVisibleChanged += AiHostVisibilityChanged;
        }

        private static void AiHostVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        { if (!IsEditorAiPlacementSession() && humanPanel?.IsVisible != true) HideAiPlacementPanel(); }

        private static void AiRoleChanged(object sender, SelectionChangedEventArgs args)
        { if (!aiUpdating && aiRolePicker.SelectedItem is ComboBoxItem row && row.DataContext is AiPlacementChoice choice && choice.Available) aiPlacement.Selected = choice; }
        private static void AiRoleOpening(object sender, MouseButtonEventArgs args)
        { BindAiRolePopup(); if (!aiRolePicker.IsDropDownOpen) RefreshAiPlacementOptions(); }
        private static void AiRolePickerLoaded(object sender, RoutedEventArgs args) => BindAiRolePopup();

        private static void BindAiRolePopup()
        {
            if (aiRolePicker == null) return;
            aiRolePicker.ApplyTemplate();
            var popup = aiRolePicker.Template?.FindName("PART_Popup", aiRolePicker) as Popup;
            if (ReferenceEquals(popup, aiRolePopup)) return;
            UnbindAiRolePopup(); aiRolePopup = popup;
            if (popup != null) { popup.Opened += AiRolePopupOpened; popup.Closed += AiRolePopupClosed; }
            SetAiRoleForeground(aiRolePicker.IsDropDownOpen);
        }

        private static void AiRolePopupOpened(object sender, Noesis.EventArgs args) => SetAiRoleForeground(true);
        private static void AiRolePopupClosed(object sender, Noesis.EventArgs args) => SetAiRoleForeground(false);
        private static void SetAiRoleForeground(bool open)
        { if (aiRolePicker != null) aiRolePicker.Foreground = open ? ToolbarPalette.Classic.Ink : ToolbarPalette.Paper.Ink; }
        private static void UnbindAiRolePopup()
        {
            if (aiRolePopup != null) { aiRolePopup.Opened -= AiRolePopupOpened; aiRolePopup.Closed -= AiRolePopupClosed; }
            aiRolePopup = null;
        }
        private static void AiAdditionalChanged(object sender, RoutedEventArgs args)
        { if (!aiUpdating) aiPlacement.Additional = aiAdditional.IsChecked == true; }
        private static void AiEditorApplyChanged(object sender, RoutedEventArgs args)
        { if (!aiUpdating) aiPlacement.ApplyInEditor = aiEditorApply.IsChecked == true; }

        private static void RefreshAiPlacementOptions()
        {
            var choices = new List<AiPlacementChoice>();
            eChimps type;
            if (aiMilitary != null && TryAiMapperType(aiShownMapper, out type))
            {
                lock (engineLock)
                    foreach (var option in aiMilitary.GetOptions(aiShownOwner, type, IsEditorAiPlacementSession()))
                    {
                        if ((eMappers)aiShownMapper == eMappers.MAPPER_PEOPLE_ENGINEERS_POTS && option.Role != AiMilitaryRole.None) continue;
                        bool missingCrew = AiMapperTypes.NeedsCrew(type) && option.Role != AiMilitaryRole.None;
                        choices.Add(new AiPlacementChoice(option.Role.ToString(), AiRoleLabel(option.Role.ToString()),
                            option.IsAvailable && !missingCrew, option.IsDeferred,
                            missingCrew ? AiReason(AiMilitaryReason.IncompleteCrew) : option.IsDeferred ? AiText("Deferred") : AiReason(option.Reason)));
                    }
            }
            if (choices.Count == 0)
                choices.Add(new AiPlacementChoice("None", AiRoleLabel("None"), false, false, AiText("Unavailable")));
            aiUpdating = true;
            try { aiPlacement.SetChoices(choices.ToArray(), AiRoleLabel(aiPlacement.Role), AiText("Incompatible")); }
            finally { aiUpdating = false; }
            UpdateAiPlacementControls();
        }

        private static string AiText(string key) => SerpLocalization.Get("Tools.Ai." + key);
        private static string AiRoleLabel(string role) => SerpLocalization.Get("Tools.AiRole." + role);
        private static string AiReason(AiMilitaryReason reason) => reason == AiMilitaryReason.None ? "" :
            SerpLocalization.Get("Tools.AiReason." + reason);

        private static void UpdateAiPlacementControls()
        {
            if (aiRolePicker == null) return;
            aiUpdating = true;
            try
            {
                if (!ReferenceEquals(aiRenderedChoices, aiPlacement.Choices))
                {
                    ClearAiRoleRows();
                    aiRenderedChoices = aiPlacement.Choices;
                    foreach (var choice in aiRenderedChoices)
                    {
                        // Real Noesis leaves Setter.Value Binding objects unapplied on generated
                        // rows. Own the small option set explicitly, including disabled reason tips.
                        var row = new ComboBoxItem { Content = choice.Name, DataContext = choice, IsEnabled = choice.Available };
                        AiTip(row, string.IsNullOrEmpty(choice.Help) ? AiText(choice.Role == "None" ? "NoneHelp" : "RoleHelp") : choice.Help);
                        aiRoleRows.Add(row);
                    }
                    aiRolePicker.ItemsSource = aiRoleRows.ToArray();
                }
                ComboBoxItem selected = null;
                foreach (var row in aiRoleRows)
                    if (ReferenceEquals(row.DataContext, aiPlacement.Selected)) { selected = row; break; }
                aiRolePicker.SelectedItem = selected;
                aiAdditional.Content = AiText("Additional"); aiAdditional.IsChecked = aiPlacement.Additional;
                aiAdditional.IsEnabled = aiPlacement.CanCount;
                bool editor = IsEditorAiPlacementSession();
                aiEditorApply.Content = AiText("ApplyInEditor"); aiEditorApply.IsChecked = aiPlacement.ApplyInEditor;
                aiEditorApply.Visibility = editor ? Visibility.Visible : Visibility.Collapsed;
                aiEditorApply.IsEnabled = aiPlacement.CanApplyInEditor;
                aiPlacementTitle.Text = AiText("Title") + " · " + PlayerLabel(aiShownOwner);
                aiPlacementStatus.Text = !string.IsNullOrEmpty(aiPlacement.Feedback) ? aiPlacement.Feedback :
                    aiPlacement.Selected?.Help ?? AiText("Unavailable");
                aiPlacementStatus.Visibility = string.IsNullOrWhiteSpace(aiPlacementStatus.Text) ? Visibility.Collapsed : Visibility.Visible;
                aiPlacementPanel.Background = humanPanel?.TryFindResource("SpectatorPaper") as Brush ?? ToolbarPalette.Paper.Input;
                aiPlacementPanel.BorderBrush = ToolbarPalette.Paper.Border;
                aiPlacementTitle.Foreground = aiPlacementStatus.Foreground = ToolbarPalette.Paper.Ink;
                aiAdditional.Foreground = aiEditorApply.Foreground = ToolbarPalette.Paper.Ink;
                SetAiRoleForeground(aiRolePicker.IsDropDownOpen);
                aiRolePicker.Background = ToolbarPalette.Paper.Input;
                AiTip(aiRolePicker, AiText("RoleHelp") + (aiPlacement.Role == "None" ? " " + AiText("NoneHelp") : ""));
                AiTip(aiAdditional, AiText("AdditionalHelp"));
                AiTip(aiEditorApply, AiText("EditorHelp"));
                AiTip(aiPlacementGrip, PlayerLabel(aiShownOwner) + ". " + AiText("DragHelp"));
                RequestRender();
            }
            finally { aiUpdating = false; }
        }

        private static void AiTip(FrameworkElement element, string text)
        {
            if (ToolTipService.GetToolTip(element) is ToolTip existing && Equals(existing.Content, text)) return;
            if (ToolTipService.GetToolTip(element) is ToolTip retired)
            { retired.IsOpen = false; MenuToolTipSizing.SetEnabled(retired, false); }
            var tip = Shared.MenuToolTip.Create(text);
            ToolTipService.SetToolTip(element, tip); ToolTipService.SetShowDuration(element, Shared.MenuToolTip.Duration);
            ToolTipService.SetShowOnDisabled(element, true); MenuToolTipSizing.SetEnabled(tip, true);
        }

        private static void ClearAiRoleRows()
        {
            foreach (var row in aiRoleRows)
            {
                if (ToolTipService.GetToolTip(row) is ToolTip tip)
                { tip.IsOpen = false; MenuToolTipSizing.SetEnabled(tip, false); }
                ToolTipService.SetToolTip(row, null);
            }
            if (aiRolePicker != null) aiRolePicker.ItemsSource = null;
            aiRoleRows.Clear(); aiRenderedChoices = null;
        }

        // Use the existing active presentation publisher; option queries are event/change driven.
        private static void RefreshAiPlacementPanel()
        {
            bool editor = IsEditorAiPlacementSession();
            if (!editor && !IsPlacementSession())
            { if (aiEditorCanvas != null) DetachAiPlacementPanel(); else HideAiPlacementPanel(); return; }
            EnsureAiPlacementPanel();
            if (aiPlacementPanel == null) return;
            int mapper = MainControls.instance?.CurrentSubAction ?? -1;
            bool visible = (editor || IsPlacementSession() && humanPanel?.Visibility == Visibility.Visible && humanPanel.Opacity > 0) &&
                MainViewModel.Instance.Show_HUD_Main && !MainViewModel.Instance.Show_HUD_IngameMenu &&
                AiMapperTypes.IsUnitPlacement(MainControls.instance?.CurrentAction ?? -1, (eMappers)mapper);
            if (!visible) { HideAiPlacementPanel(); return; }
            aiPlacementPanel.Visibility = Visibility.Visible;
            string locale = ActiveLocale();
            int owner = editor ? EditorDirector.instance.ActivePlayerID : humanSelectedOwner;
            if (mapper != aiShownMapper || owner != aiShownOwner || locale != aiShownLocale)
            {
                aiShownMapper = mapper; aiShownOwner = owner; aiShownLocale = locale;
                aiPlacement.Feedback = ""; RefreshAiPlacementOptions();
            }
            PositionAiPlacementPanel();
        }

        private static void PositionAiPlacementPanel()
        {
            if (aiPositioning || aiPlacementPanel?.Visibility != Visibility.Visible || aiPlacementHost == null) return;
            aiPositioning = true;
            try
            {
                bool editor = IsEditorAiPlacementSession();
                float vw = aiPlacementHost.ActualWidth, vh = aiPlacementHost.ActualHeight;
                if (vw <= 0 || vh <= 0) return;
                FrameworkElement nativeFrame = editor ? MainViewModel.Instance.HUDmain?.FindName("MainFrameBuildings") as FrameworkElement : frameAnchor;
                var frame = AiHostBounds(nativeFrame);
                if (frame.Empty) frame = new ToolbarRect(Math.Max(0, vw - 800), vh - 150, 800, 150);
                float nativeScale = nativeFrame?.ActualWidth > 0 ? frame.Width / nativeFrame.ActualWidth : 1;
                float scale = Math.Max(.01f, Math.Min(nativeScale, Math.Min(vw / 280, vh / Math.Max(1, aiPlacementPanel.ActualHeight))));
                float available = Math.Min(500, vw / scale);
                if (aiPlacementPanel.MaxWidth != available) aiPlacementPanel.MaxWidth = available;
                float contentWidth = Math.Max(1, available - 14);
                // A horizontal StackPanel measures without a width limit; stack its controls on
                // narrow viewports before it could clip a translated checkbox or selected caption.
                float rowWidth = aiRolePicker.DesiredSize.Width + aiAdditional.DesiredSize.Width -
                    aiAdditional.Margin.Left - aiAdditional.Margin.Right + 10;
                var orientation = rowWidth > contentWidth ? Orientation.Vertical : Orientation.Horizontal;
                if (aiPlacementRow.Orientation != orientation) aiPlacementRow.Orientation = orientation;
                var margin = orientation == Orientation.Horizontal ? new Thickness(10, 0, 0, 0) : new Thickness(0, 4, 0, 0);
                if (!aiAdditional.Margin.Equals(margin)) aiAdditional.Margin = margin;
                aiRolePicker.MaxWidth = Math.Min(300, contentWidth);
                aiRolePicker.MinWidth = Math.Min(260, contentWidth);
                aiPlacementTitle.MaxWidth = aiPlacementStatus.MaxWidth = contentWidth;
                float width = aiPlacementPanel.ActualWidth * scale, height = aiPlacementPanel.ActualHeight * scale;
                if (width <= 0 || height <= 0) { RequestRender(); return; }
                if (aiPlacementScale != scale)
                { aiPlacementScale = scale; aiPlacementPanel.RenderTransform = new ScaleTransform(scale, scale); }
                aiPlacementObstacles.Clear();
                if (!editor) { aiPlacementObstacles.AddRange(controlsBounds); aiPlacementObstacles.Add(AiHostBounds(humanPanel)); }
                bool dockAvailable = AiPlacementLayout.TryDock(frame, width, height, vw, vh, aiPlacementObstacles, out aiPanelDock);
                if (!dockAvailable && !aiPanelPosition.Floating && !aiPanelPosition.Dragging) { HideAiPlacementPanel(); return; }
                var pose = aiPanelPosition.Arrange(aiPanelDock, width, height, vw, vh);
                aiPanelBounds = pose;
                Canvas.SetLeft(aiPlacementPanel, pose.X); Canvas.SetTop(aiPlacementPanel, pose.Y);
                }
            finally { aiPositioning = false; }
        }

        private static ToolbarRect AiHostBounds(FrameworkElement element)
        {
            if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return default;
            Point a = element.TranslatePoint(new Point(0, 0), aiPlacementHost);
            Point b = element.TranslatePoint(new Point(element.ActualWidth, element.ActualHeight), aiPlacementHost);
            return new ToolbarRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X-a.X), Math.Abs(b.Y-a.Y));
        }

        private static bool IsEditorAiPlacementSession() => settings != null && IsCurrentSession(CurrentSessionId) &&
            sessionMode is APIShared.GameModes.GameModeSnapshot mode && EditorDirector.instance != null && MainControls.instance != null &&
            AiEditorPlacementPolicy.Enabled(settings.Activation.TestTools, mode.IsMapEditor,
                mode.IsRealMultiplayer || Director.instance == null || Director.instance.MultiplayerGame,
                MainControls.instance.CurrentAction, EditorDirector.instance.ActivePlayerID,
                TryAiMapperType(MainControls.instance.CurrentSubAction, out _));

        private static void DetachAiPlacementPanel()
        {
            if (aiPlacementPanel != null)
            {
                DetachAiPanelInteraction();
                if (humanPanel != null) humanPanel.IsVisibleChanged -= AiHostVisibilityChanged;
                foreach (var owner in new FrameworkElement[] { aiRolePicker, aiAdditional, aiEditorApply, aiPlacementGrip })
                    if (ToolTipService.GetToolTip(owner) is ToolTip tip) MenuToolTipSizing.SetEnabled(tip, false);
                aiRolePicker.SelectionChanged -= AiRoleChanged; aiRolePicker.PreviewMouseDown -= AiRoleOpening;
                aiRolePicker.Loaded -= AiRolePickerLoaded; aiRolePicker.Reloaded -= AiRolePickerLoaded;
                UnbindAiRolePopup();
                aiAdditional.Checked -= AiAdditionalChanged; aiAdditional.Unchecked -= AiAdditionalChanged;
                aiEditorApply.Checked -= AiEditorApplyChanged; aiEditorApply.Unchecked -= AiEditorApplyChanged;
                ClearAiRoleRows();
                if (aiPlacementPanel.Parent is Panel parent) parent.Children.Remove(aiPlacementPanel);
            }
            aiPlacementPanel = null; aiRolePicker = null; aiAdditional = aiEditorApply = null;
            aiPlacementTitle = aiPlacementStatus = null; aiShownMapper = aiShownOwner = -1; aiShownLocale = null;
            aiPlacementGrip = null; aiPlacementRow = null;
            aiPlacementScale = -1; aiPlacementObstacles.Clear();
            if (aiEditorCanvas?.Parent is Panel editorParent) editorParent.Children.Remove(aiEditorCanvas);
            aiPlacementHost = aiEditorCanvas = null; aiEditorScreen = null;
        }

        private static bool BeginAiPlacement(int mapper, int owner, out MapperPlacementScope scope)
        {
            scope = new MapperPlacementScope();
            bool started = false;
            try
            {
                if (engineLock == null || aiMilitary == null) return RejectAiPlacement(AiText("Unavailable"));
                Monitor.Enter(engineLock); scope.HoldsEngineMonitor = true;
                bool editor = IsEditorAiPlacementSession();
                if (editor ? owner != EditorDirector.instance.ActivePlayerID : !IsPlacementSession() || owner != CurrentAiOwner())
                    return RejectAiPlacement(AiText("Unavailable"));
                eChimps type;
                if (!TryAiMapperType(mapper, out type)) return RejectAiPlacement(AiText("Unavailable"));
                var intent = Volatile.Read(ref aiIntent);
                if (AiMapperTypes.NeedsCrew(type) && intent.Assignment.Role != AiMilitaryRole.None)
                    return RejectAiPlacement(AiReason(AiMilitaryReason.IncompleteCrew));
                if ((eMappers)mapper == eMappers.MAPPER_PEOPLE_ENGINEERS_POTS && intent.Assignment.Role != AiMilitaryRole.None)
                    return RejectAiPlacement(AiText("OilBound"));
                bool found = false;
                foreach (var option in aiMilitary.GetOptions(owner, type, editor))
                    if (option.Role == intent.Assignment.Role)
                    {
                        if (!option.IsAvailable) return RejectAiPlacement(AiReason(option.Reason));
                        found = true; break;
                    }
                if (!found) return RejectAiPlacement(AiText("Incompatible"));
                long session = CurrentSessionId;
                scope.Session = session; scope.Owner = owner;
                if (!aiMilitary.TryBeginSpawnScope(owner, type, intent.Assignment, editor,
                    editor && intent.ApplyInEditor,
                    result => AiPlacementCompleted(session, owner, result), out scope.AiCapture, out string reason))
                { log.LogWarning("SPECTATOR_AI_CAPTURE_REJECTED: " + reason); return RejectAiPlacement(AiText("Unavailable")); }
                started = true; return true;
            }
            catch (Exception error) { LogOnce("SPECTATOR_AI_CAPTURE_FAILED: " + error); return RejectAiPlacement(AiText("Unavailable")); }
            finally { if (!started) EndAiPlacement(scope, false); }
        }

        private static bool RejectAiPlacement(string reason)
        { PostPresentation(() => aiPlacement.Feedback = reason); return false; }

        private static void AiPlacementCompleted(long session, int owner, AiMilitaryAssignmentResult result)
        {
            DiagAiPlacementResult(session, owner, result);
            PostPresentation(() => {
                if (!IsCurrentSession(session) || (IsEditorAiPlacementSession() ? EditorDirector.instance.ActivePlayerID : humanSelectedOwner) != owner) return;
                aiPlacement.Feedback = result.Status == AiMilitaryAssignmentStatus.Rejected ? AiReason(result.Reason) : AiText(result.Status.ToString());
            });
        }

        private static void EndAiPlacement(MapperPlacementScope scope, bool complete)
        {
            if (scope == null) return;
            try
            {
                if (complete && scope.AiCapture != null)
                    foreach (var result in scope.AiCapture.Complete())
                        if (result.Status == AiMilitaryAssignmentStatus.Queued) AiPlacementCompleted(scope.Session, scope.Owner, result);
            }
            finally
            {
                try { scope.AiCapture?.Dispose(); }
                finally
                {
                    scope.AiCapture = null;
                    if (scope.HoldsEngineMonitor) { scope.HoldsEngineMonitor = false; Monitor.Exit(engineLock); }
                }
            }
        }

        private sealed class MapperPlacementScope
        {
            internal WallStoneScope Stone;
            internal IAiMilitarySpawnScope AiCapture;
            internal bool HoldsEngineMonitor;
            internal long Session;
            internal int Owner;
        }
    }
}

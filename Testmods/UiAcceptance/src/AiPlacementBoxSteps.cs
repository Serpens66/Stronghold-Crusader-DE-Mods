using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;

namespace UiAcceptance
{
    // Actual attached controls and Noesis view input only. No OS pointer/keyboard injection.
    internal static class AiPlacementBoxSteps
    {
        private static readonly BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Type Runtime => GameSteps.FindType("Spectator.SpectatorRuntime");
        private static NoesisView Input => FatControler.instance.NGview;
        private static Border Panel => RuntimeAiUiSteps.Find("SpectatorAiPlacementPanel") as Border;
        private static FrameworkElement Grip => RuntimeAiUiSteps.Find("SpectatorAiPlacementGrip");
        private static object Field(string name) => Runtime.GetField(name, Static)?.GetValue(null) ??
            throw new StepFailure("Missing observed Spectator field: " + name);
        private static bool Flag(string name) => (bool)Field("aiPanelPosition").GetType().GetProperty(name, Instance)
            .GetValue(Field("aiPanelPosition"), null);
        private static bool Visible => Panel?.IsVisible == true && Panel.ActualWidth > 0 && Panel.ActualHeight > 0;

        internal static void Register() => Runner.Register("AiUi.Box", Check);

        // APIShared owns iterator disposal on failure/session replacement, including held-input cleanup.
        // The surrounding existing runner owns scenario setup, settings rollback and screenshot capture.
        private static IEnumerable<Wait> Check(Step step)
        {
            string expectedLocale = step.Arg(1);
            LogLocale();
            Runner.Require(ActiveLocale() == expectedLocale, "expected actual Spectator locale=" + expectedLocale + " observed=" + ActiveLocale());
            AutomationResult result = null;
            var test = new AutomationStep("ai-box-" + step.Id, TimeSpan.FromSeconds(80),
                run => Exercise(run, step.Arg(0, "full") == "full"), missionBound: true);
            ApiShared.ForMod(UiAcceptancePlugin.Guid).Automation.Start(Guid.NewGuid(), new[] { test },
                UiAcceptancePlugin.Log, completed: value => result = value);
            yield return new Until("mission-bound AI box automation", () => result != null);
            Runner.Require(result.Status == AutomationStatus.Passed, "AI box: " + result.Detail);
        }

        private static IEnumerable<AutomationWait> Exercise(AutomationRun run, bool full)
        {
            yield return Settled();
            var mission = run.CurrentMission;
            Runner.Require(mission != null && !mission.Mode.IsRealMultiplayer, "local current mission required");
            CheckContent();
            Runner.Require(!Flag("Dragging") && !Flag("Floating"), "new mission should begin docked");
            CheckDock();
            if (!full) yield break;

            bool originalMouse = Input.EnableMouse;
            NoesisView input = Input;
            int originalWidth = UnityEngine.Screen.width, originalHeight = UnityEngine.Screen.height;
            var originalScreenMode = UnityEngine.Screen.fullScreenMode;
            Point pointer = Center(Grip);
            bool held = false;
            Border removedPanel = null;
            Canvas removedParent = null;
            int unloaded = 0;
            RoutedEventHandler observeUnload = (sender, args) => unloaded++;
            Border observedPanel = Panel;
            observedPanel.Unloaded += observeUnload;
            try
            {
                // Block physical mouse forwarding, not the user's Windows cursor. This prevents a
                // user's unrelated cursor movement from replacing our held Noesis drag location.
                Input.EnableMouse = false;
                Box docked = Bounds(Panel);
                Box floating = new Box(Math.Max(30, (UnityEngine.Screen.width - docked.W) * .42f),
                    Math.Max(30, (UnityEngine.Screen.height - docked.H) * .30f), docked.W, docked.H);
                pointer = BeginDrag(); held = true;
                MoveTo(floating, ref pointer);
                yield return AutomationWait.NextFrame;
                Runner.Require(Flag("Dragging") && Distance(Bounds(Panel), docked) > 20, "header did not move actual panel");
                Input.MouseButtonUp((int)pointer.X, (int)pointer.Y, MouseButton.Left); held = false;
                yield return Settled();
                Runner.Require(Flag("Floating") && !Flag("Dragging") && !Grip.IsMouseCaptured, "mouse-up did not commit floating position/release capture");
                Box committed = Bounds(Panel);
                Point anchor = Normalized(committed);
                CheckContent();
                AcceptLog.Info("AI_BOX_DRAG committed=" + committed + " source=NoesisHeader");
                foreach (var wait in ConvertWaits(RuntimeAiUiSteps.TakeUiScreenshot("ai-box-floating-" + (mission.IsEditor ? "editor" : "sp")))) yield return wait;

                foreach (string cancellation in new[] { "escape", "capture", "hide", "unload" })
                {
                    pointer = BeginDrag(); held = true;
                    MoveTo(new Box(committed.X + 65, committed.Y + 50, committed.W, committed.H), ref pointer);
                    yield return AutomationWait.NextFrame;
                    Runner.Require(Distance(Bounds(Panel), committed) > 20, "cancel fixture never moved preview");
                    if (cancellation == "escape")
                    {
                        // Focus an existing picker so the real view routes PreviewKeyDown through
                        // the common root. This does not fabricate an event or call a mod handler.
                        RuntimeAiUiSteps.Find("SpectatorAiRolePicker").Focus();
                        Input.KeyDown(Key.Escape); Input.KeyUp(Key.Escape);
                    }
                    else if (cancellation == "capture") Grip.ReleaseMouseCapture();
                    else if (cancellation == "unload")
                    {
                        removedPanel = Panel; removedParent = removedPanel.Parent as Canvas;
                        Runner.Require(removedParent != null, "actual panel canvas missing for unload fixture");
                        removedParent.Children.Remove(removedPanel);
                    }
                    else MainControls.instance.StopAllPlacement();
                    yield return AutomationWait.Until("drag cancellation: " + cancellation,
                        () => !Flag("Dragging") && (Grip == null || !Grip.IsMouseCaptured));
                    Input.MouseButtonUp((int)pointer.X, (int)pointer.Y, MouseButton.Left); held = false;
                    if (cancellation == "unload")
                    {
                        yield return AutomationWait.Until("actual panel Unloaded event", () => unloaded > 0);
                        removedParent.Children.Add(removedPanel); removedPanel = null; removedParent = null;
                    }
                    if (cancellation == "hide")
                    {
                        yield return AutomationWait.Until("hidden after native placement cancellation", () => !Visible);
                        foreach (var wait in ReopenMapper(eMappers.MAPPER_PEOPLE_ENGINEERS)) yield return wait;
                    }
                    yield return Settled();
                    Runner.Require(Flag("Floating") && Distance(Bounds(Panel), committed) <= 2,
                        cancellation + " did not roll back to committed panel position");
                    AcceptLog.Info("AI_BOX_CANCEL kind=" + cancellation + " rollback=True capture=False");
                }

                foreach (var mapper in new[] { eMappers.MAPPER_PEOPLE_ARCHERS, eMappers.MAPPER_PEOPLE_ENGINEERS })
                {
                    foreach (var wait in ReopenMapper(mapper)) yield return wait;
                    yield return Settled();
                    Runner.Require(Flag("Floating") && SameAnchor(anchor),
                        "unit mapper change lost confirmed floating position");
                }
                int originalOwner = mission.IsEditor ? EditorDirector.instance.ActivePlayerID : (int)Field("humanSelectedOwner");
                int otherOwner = originalOwner == 1 ? 2 : 1;
                foreach (int next in new[] { otherOwner, originalOwner })
                {
                    if (mission.IsEditor) EditorDirector.instance.SetEditorPlayerID(next);
                    else
                    {
                        Click(GameXAMLManagerAPI.Instance.FindGlobalElement("SpectatorEditorHumanOwner" + next));
                        yield return AutomationWait.Until("actual selected owner", () => (int)Field("humanSelectedOwner") == next);
                        // PlayerSelectionController intentionally cancels placement on owner changes.
                        // Reopen the same mapper through its ordinary UI before inspecting the box.
                        foreach (var wait in ReopenMapper(eMappers.MAPPER_PEOPLE_ENGINEERS)) yield return wait;
                    }
                    yield return AutomationWait.Until("box owner refresh", () => Visible &&
                        (int)Field("aiShownOwner") == next);
                    yield return Settled();
                    Runner.Require(Flag("Floating") && SameAnchor(anchor),
                        "owner change lost confirmed floating position");
                }
                AcceptLog.Info("AI_BOX_PERSIST mapper=True owner=True hide=True");

                UnityEngine.Screen.SetResolution(2560, 1440, UnityEngine.Screen.fullScreenMode);
                yield return AutomationWait.Until("actual 1440p viewport", () => UnityEngine.Screen.width == 2560 && UnityEngine.Screen.height == 1440);
                yield return Settled();
                Runner.Require(Flag("Floating") && SameAnchor(anchor), "resolution change lost normalized floating anchor");
                CheckContent();
                UnityEngine.Screen.SetResolution(originalWidth, originalHeight, UnityEngine.Screen.fullScreenMode);
                yield return AutomationWait.Until("original viewport restored", () => UnityEngine.Screen.width == originalWidth && UnityEngine.Screen.height == originalHeight);
                yield return Settled();
                Runner.Require(Flag("Floating") && SameAnchor(anchor), "viewport restore lost normalized floating anchor");
                AcceptLog.Info("AI_BOX_RESIZE normalizedAnchor=True clamped=True");

                pointer = BeginDrag(); held = true;
                var host = (FrameworkElement)Field("aiPlacementHost");
                object dock = Field("aiPanelDock");
                Point target = host.PointToScreen(new Point(Number(dock, "X"), Number(dock, "Y")));
                MoveTo(new Box(target.X + 4, target.Y + 3, committed.W, committed.H), ref pointer);
                Input.MouseButtonUp((int)pointer.X, (int)pointer.Y, MouseButton.Left); held = false;
                yield return Settled();
                Runner.Require(!Flag("Floating") && !Flag("Dragging") && !Grip.IsMouseCaptured,
                    "drop inside physical dock tolerance did not reattach");
                CheckDock(); CheckContent();
                AcceptLog.Info("AI_BOX_DOCK reattached=True viewport=" + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
            }
            finally
            {
                try
                {
                    if (ReferenceEquals(Input, input) && run.CurrentMission?.SessionId == mission.SessionId)
                    {
                        if (held) input.MouseButtonUp((int)pointer.X, (int)pointer.Y, MouseButton.Left);
                        if (Grip?.IsMouseCaptured == true) Grip.ReleaseMouseCapture();
                    }
                }
                finally
                {
                    observedPanel.Unloaded -= observeUnload;
                    if (removedPanel != null && run.CurrentMission?.SessionId == mission.SessionId &&
                        ReferenceEquals(Field("aiPlacementPanel"), removedPanel) && removedPanel.Parent == null)
                        removedParent.Children.Add(removedPanel);
                    if (input != null) input.EnableMouse = originalMouse;
                    if (UnityEngine.Screen.width != originalWidth || UnityEngine.Screen.height != originalHeight)
                        UnityEngine.Screen.SetResolution(originalWidth, originalHeight, originalScreenMode);
                }
            }
        }

        // Reuse the actual native editor troop buttons already audited by RuntimeAiUiSteps.
        private static IEnumerable<AutomationWait> ReopenMapper(eMappers mapper)
        {
            foreach (var wait in ConvertWaits(RuntimeAiUiSteps.SelectTroopButton(mapper))) yield return wait;
            yield return AutomationWait.Until("box restored with mapper", () => Visible);
        }

        private static IEnumerable<AutomationWait> ConvertWaits(IEnumerable<Wait> waits)
        {
            foreach (Wait wait in waits)
            {
                if (wait is Until until) yield return AutomationWait.Until(until.What, until.Condition);
                else if (wait is Delay delay) yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(delay.Ms));
                else yield return AutomationWait.NextFrame;
            }
        }

        private static Point BeginDrag()
        {
            Runner.Require(Visible && Grip?.IsEnabled == true, "visible enabled header required");
            Point point = Center(Grip);
            Input.MouseMove((int)point.X, (int)point.Y);
            var root = Input.Content;
            var hit = VisualTreeHelper.HitTest(root, root.PointFromScreen(point)).VisualHit;
            Runner.Require(Within(hit, Grip), "header hit-test is covered: " + (hit as FrameworkElement)?.Name);
            Input.MouseButtonDown((int)point.X, (int)point.Y, MouseButton.Left);
            Runner.Require(Flag("Dragging") && Grip.IsMouseCaptured, "real header press did not capture/begin drag");
            return point;
        }

        private static void MoveTo(Box target, ref Point pointer)
        {
            Box current = Bounds(Panel);
            pointer = new Point(pointer.X + target.X - current.X, pointer.Y + target.Y - current.Y);
            Input.MouseMove((int)pointer.X, (int)pointer.Y);
        }

        private static void Click(FrameworkElement element)
        {
            Runner.Require(element?.IsVisible == true && element.IsEnabled, "owner button unavailable");
            Point point = Center(element);
            Input.MouseMove((int)point.X, (int)point.Y);
            Input.MouseButtonDown((int)point.X, (int)point.Y, MouseButton.Left);
            Input.MouseButtonUp((int)point.X, (int)point.Y, MouseButton.Left);
        }

        private static AutomationWait Settled()
        {
            Box previous = default; int stable = 0; var clock = Stopwatch.StartNew();
            return AutomationWait.Until("visible and stable content-sized box", () => {
                if (!Visible) { stable = 0; return false; }
                Box now = Bounds(Panel);
                stable = Distance(now, previous) < .5f && Math.Abs(now.W - previous.W) < .5f && Math.Abs(now.H - previous.H) < .5f ? stable + 1 : 0;
                previous = now;
                return stable >= 3 && clock.ElapsedMilliseconds >= 180;
            });
        }

        private static void CheckDock()
        {
            object dock = Field("aiPanelDock"); var host = (FrameworkElement)Field("aiPlacementHost");
            Point target = host.PointToScreen(new Point(Number(dock, "X"), Number(dock, "Y")));
            Box actual = Bounds(Panel);
            Runner.Require(Math.Abs(actual.X - target.X) <= 2 && Math.Abs(actual.Y - target.Y) <= 2, "docked box differs from computed HUD dock");
            var menu = MainViewModel.Instance.HUDmain.FindName("MainFrameBuildings") as FrameworkElement;
            Runner.Require(menu?.IsVisible == true, "native building menu anchor unavailable");
            Box anchor = Bounds(menu);
            Runner.Require(!actual.Intersects(anchor), "docked box covers native building menu");
            // On constrained HUDs the approved layout may choose a viewport edge.
            bool adjacent = Math.Min(Math.Abs(actual.Bottom-anchor.Y), Math.Min(Math.Abs(actual.X-anchor.Right),
                Math.Min(Math.Abs(actual.Right-anchor.X), Math.Abs(actual.Y-anchor.Bottom)))) <= 3;
            bool edge = Math.Min(actual.X, UnityEngine.Screen.width-actual.Right) <= 3;
            Runner.Require(adjacent || edge, "dock is detached from both build menu and viewport edge");
        }

        private static void CheckContent()
        {
            Border panel = Panel;
            Runner.Require(float.IsNaN(panel.Width) && float.IsNaN(panel.Height), "box must size to current content");
            var body = panel.Child as FrameworkElement;
            Runner.Require(body != null && panel.ActualWidth <= body.DesiredSize.Width + panel.Padding.Left + panel.Padding.Right +
                panel.BorderThickness.Left + panel.BorderThickness.Right + 2, "box reserves empty horizontal space beyond content");
            Runner.Require(panel.ActualHeight <= body.DesiredSize.Height + panel.Padding.Top + panel.Padding.Bottom +
                panel.BorderThickness.Top + panel.BorderThickness.Bottom + 2, "box reserves empty vertical space beyond content");
            Box bounds = Bounds(panel);
            Runner.Require(bounds.X >= -1 && bounds.Y >= -1 && bounds.Right <= UnityEngine.Screen.width+1 &&
                bounds.Bottom <= UnityEngine.Screen.height+1, "box exceeds physical viewport: " + bounds);
            foreach (FrameworkElement control in new[] { Grip, RuntimeAiUiSteps.Find("SpectatorAiRolePicker"),
                RuntimeAiUiSteps.Find("SpectatorAiAdditional"), RuntimeAiUiSteps.Find("SpectatorAiEditorApply") })
                if (control?.IsVisible == true) Runner.Require(bounds.Contains(Bounds(control)), "box clips control " + control.Name);
            CheckCaptions(panel);
            AcceptLog.Info("AI_BOX_CONTENT bounds=" + bounds + " actual=" + panel.ActualWidth + "x" + panel.ActualHeight +
                " desired=" + body.DesiredSize.Width + "x" + body.DesiredSize.Height + " locale=" + ActiveLocale());
        }

        private static void CheckCaptions(DependencyObject root)
        {
            if (root is TextBlock text && text.IsVisible && !string.IsNullOrWhiteSpace(text.Text) && text.TextWrapping == TextWrapping.NoWrap)
            {
                if (Within(text, Grip))
                {
                    // Long player names deliberately use ellipsis; the existing header tooltip
                    // preserves the full actor name, followed by the drag instructions.
                    var tip = ToolTipService.GetToolTip(Grip) as ToolTip;
                    string caption = text.Text;
                    int separator = caption.LastIndexOf('·');
                    string actor = separator >= 0 ? caption.Substring(separator+1).Trim() : caption;
                    Runner.Require(tip?.Content is string help && !string.IsNullOrWhiteSpace(help) && help.Contains(actor),
                        "header tooltip lost the complete actor caption");
                    return;
                }
                // Read the real glyph runs at their actual inherited font. Measuring formatted
                // text does not remeasure/arrange the attached control or construct a second view.
                Size natural = text.FormattedText.Measure(TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None,
                    float.PositiveInfinity, float.PositiveInfinity, 0, LineStackingStrategy.MaxHeight, FlowDirection.LeftToRight);
                Runner.Require(natural.Width <= text.ActualWidth + 2 && natural.Height <= text.ActualHeight + 2,
                    "caption is clipped/ellipsized: " + text.Text + " natural=" + natural.Width + "x" + natural.Height +
                    " actual=" + text.ActualWidth + "x" + text.ActualHeight);
            }
            for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++) CheckCaptions(VisualTreeHelper.GetChild(root,i));
        }

        private static Point Normalized(Box value) => new Point(value.X / Math.Max(1, UnityEngine.Screen.width-value.W),
            value.Y / Math.Max(1, UnityEngine.Screen.height-value.H));
        private static bool SameAnchor(Point expected)
        {
            Point actual = Normalized(Bounds(Panel));
            return Math.Abs(expected.X-actual.X) <= .003f && Math.Abs(expected.Y-actual.Y) <= .003f;
        }

        private static string ActiveLocale()
        {
            Type localization = Runtime.Assembly.GetType("SerpLocalization");
            var method = localization?.GetMethod("GetActiveLocale", Static);
            Runner.Require(method != null, "active localized text provider unavailable");
            return (string)method.Invoke(null, null);
        }
        private static void LogLocale()
        {
            var plugin = SHCDESE.BepInEx.Bootstrap.Plugin.Instance;
            bool referenceNull = ReferenceEquals(plugin, null);
            string raw = referenceNull ? "<reference-null>" : plugin.LanguageProvider.Value;
            Type localization = Runtime.Assembly.GetType("SerpLocalization");
            string configured = (string)localization.GetMethod("GetConfiguredLanguageProvider", Static).Invoke(null, null);
            AcceptLog.Info("AI_BOX_LOCALE referenceNull=" + referenceNull + " unityNull=" + (plugin == null) +
                " rawProvider=" + raw + " configuredProvider=" + configured + " observed=" + ActiveLocale());
        }
        private static float Number(object target, string name) => (float)target.GetType().GetField(name, Instance).GetValue(target);
        private static Point Center(FrameworkElement element) => element.PointToScreen(new Point(element.ActualWidth/2, element.ActualHeight/2));
        private static bool Within(DependencyObject node, DependencyObject parent)
        { for (; node != null; node = VisualTreeHelper.GetParent(node)) if (ReferenceEquals(node, parent)) return true; return false; }
        private static Box Bounds(FrameworkElement element)
        {
            Point a = element.PointToScreen(new Point(0,0)), b = element.PointToScreen(new Point(element.ActualWidth,element.ActualHeight));
            return new Box(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Abs(b.X-a.X),Math.Abs(b.Y-a.Y));
        }
        private static double Distance(Box a, Box b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
        private readonly struct Box
        {
            internal readonly float X,Y,W,H;
            internal float Right => X+W;
            internal float Bottom => Y+H;
            internal Box(float x,float y,float w,float h) { X=x;Y=y;W=w;H=h; }
            internal bool Contains(Box other) => other.X >= X-1 && other.Y >= Y-1 && other.Right <= Right+1 && other.Bottom <= Bottom+1;
            internal bool Intersects(Box other) => X < other.Right-1 && Right > other.X+1 && Y < other.Bottom-1 && Bottom > other.Y+1;
            public override string ToString() => X.ToString("F1")+"/"+Y.ToString("F1")+"/"+W.ToString("F1")+"/"+H.ToString("F1");
        }
    }
}

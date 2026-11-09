using System;
using System.Collections.Generic;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;
using SHCDESE.API;

namespace UiAcceptance
{
    internal static partial class AutoSendSteps
    {
        private static IEnumerable<Wait> InputRegression(Step step)
        {
            yield return new Until("input regression ready", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending") && RenderedValuesMatch());
            var root = (FrameworkElement)Read(Read(runtime, "view"), "root");
            var keyboard = root.Keyboard;
            keyboard.ClearFocus();
            yield return NextFrame.Instance;
            bool baseline = GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard();
            TextBox foreign = null;
            Runner.Require(AllyGoodsPanelExtensions.TryRegisterSection("UiAcceptance_Serp", "foreign-keyboard-fixture", context => {
                foreign = new TextBox { Name = "AutoSendForeignCaptureFixture", Width = 160, Height = 28, Text = "Foreign test field" };
                SHCDESE.UI.KeyboardCaptureBinding.SetEnabled(foreign, true);
                var tip = new ToolTip { Style = (Style)Read(Read(runtime, "view"), "tooltipStyle"),
                    Content = "SE textbox owned by the acceptance test", IsHitTestVisible = false };
                APIShared.ModSettings.SharedToolTipSizing.SetEnabled(tip, true);
                ToolTipService.SetToolTip(foreign, tip); ToolTipService.SetShowDuration(foreign, 60000);
                return foreign;
            }, out var registration, out string reason, 200), "foreign textbox registration failed: " + reason);
            yield return new Until("foreign textbox realized", () => foreign != null && foreign.IsVisible && foreign.ActualWidth > 0);
            EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 1, 1);
            yield return new Until("input fixture paused", () => GamePlayerManagerAPI.Instance.IsLocalPaused());

            Slider slider = EditorSlider("reserve");
            var thumb = FindThumb(slider);
            Runner.Require(thumb != null && thumb.ActualWidth > 0, "realized slider thumb missing");
            NoesisView input = FatControler.instance.NGview;
            int original = Number(Display(), "Reserve");
            Point start = thumb.PointToScreen(new Point((float)thumb.ActualWidth / 2, (float)thumb.ActualHeight / 2));
            int x = (int)start.X, y = (int)start.Y;
            input.MouseMove(x, y); input.MouseButtonDown(x, y, MouseButton.Left);
            input.MouseMove(x + 45, y);
            bool dragged = thumb.IsDragging && slider.Mouse.Captured != null;
            input.KeyDown(Key.Escape); input.KeyUp(Key.Escape);
            bool released = !thumb.IsDragging && !Within(slider.Mouse.Captured, slider);
            input.MouseMove(x + 90, y);
            bool restored = slider.Value == original && EditorBox("reserve").Text == original.ToString(System.Globalization.CultureInfo.InvariantCulture);
            input.MouseButtonUp(x + 90, y, MouseButton.Left);
            bool unchanged = Number(Display(), "Reserve") == original && !Flag(Display(), "Pending");
            bool escapePassed = dragged && released && restored && unchanged;
            AcceptLog.Info("AUTOSEND_ESCAPE dragged=" + dragged + " released=" + released + " restored=" + restored + " unchanged=" + unchanged);

            // The pending gesture is over before the independent ownership case.
            // Focus actual controls; no keyboard-state setter participates in the assertion.
            keyboard.ClearFocus();
            yield return NextFrame.Instance;
            Runner.Require(slider.Focus(), "cannot focus own slider");
            yield return NextFrame.Instance;
            Runner.Require(EditorBox("reserve").Focus(), "cannot focus own textbox");
            yield return NextFrame.Instance;
            Runner.Require(foreign.Focus(), "cannot focus foreign textbox");
            yield return NextFrame.Instance;
            bool foreignFocused = Within(keyboard.FocusedElement, foreign);
            bool foreignOwns = GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard();
            keyboard.ClearFocus();
            yield return NextFrame.Instance;
            bool final = GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard();
            bool foreignPassed = foreignFocused && foreignOwns && final == baseline;
            AcceptLog.Info("AUTOSEND_FOREIGN baseline=" + baseline + " focused=" + foreignFocused + " owns=" + foreignOwns + " final=" + final);

            // Always leave this explicitly owned fixture usable, including an old DLL
            // that fails either regression. Repair occurs only after observations above.
            keyboard.ClearFocus();
            FatControler.instance.SetNoesisKeyboardState(baseline);
            registration.SetVisible(false);
            EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 0, 0);
            yield return new Until("input fixture resumed", () => !GamePlayerManagerAPI.Instance.IsLocalPaused());
            Runner.Require(escapePassed, "Escape did not end the drag and restore the uncommitted value");
            Runner.Require(foreignPassed, "foreign SE textbox inherited the AutoSend keyboard lease");
        }

        private static Thumb FindThumb(DependencyObject node)
        {
            if (node is Thumb thumb) return thumb;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var found = FindThumb(VisualTreeHelper.GetChild(node, i));
                if (found != null) return found;
            }
            return null;
        }
        private static bool Within(DependencyObject node, DependencyObject parent)
        {
            for (; node != null; node = VisualTreeHelper.GetParent(node)) if (ReferenceEquals(node, parent)) return true;
            return false;
        }
    }
}

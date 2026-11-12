using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;
using SHCDESE.API;

namespace UiAcceptance
{
    // Regression test for the real menu-owned template, not a separately constructed control.
    internal static class LocalAutosaveSteps
    {
        private static ContentControl editor;
        private static TextBox box;
        private static RepeatButton up, down;
        private static object runtime;
        private static double[] baseline;
        private static NoesisView Input => FatControler.instance.NGview;
        private static object Read(object target, string name) => GameSteps.Read(target.GetType(), target, name, BindingFlags.Instance);
        private static int Minutes => (int)Read(Read(runtime, "schedule"), "Minutes");
        private static int Draft => (int)((Delegate)Read(editor, "getMinutes")).DynamicInvoke();

        internal static void Register()
        {
            Runner.Register("Autosave.Layout", Layout);
            Runner.Register("Autosave.Input", InputRegression);
            Runner.Register("Autosave.Surface", Surface);
        }

        private static ContentControl AttachedEditor()
        {
            var type = typeof(IngameMenuActions);
            var contents = (IDictionary)GameSteps.Read(type, null, "contents", BindingFlags.Static);
            foreach (object item in contents.Values)
                if (item is ContentControl control && control.GetType().FullName == "BugfixesAndQoL.LocalAutosaveMenuControl" &&
                    control.Parent != null && control.IsVisible && control.ActualWidth > 0) return control;
            return null;
        }

        private static IEnumerable<Wait> Layout(Step step)
        {
            Runner.Require(GamePlayerManagerAPI.Instance.IsLocalPaused(), "autosave UI fixture must remain paused");
            yield return new Until("attached autosave editor", () => (editor = AttachedEditor()) != null);
            box = (TextBox)Read(editor, "input");
            runtime = ((Delegate)Read(editor, "getMinutes")).Target;
            AcceptLog.Info("AUTOSAVE_CANDIDATE assembly=" + editor.GetType().Assembly.FullName + " mvid=" + editor.GetType().Module.ModuleVersionId);
            var arrows = new List<RepeatButton>();
            Collect(editor, arrows);
            Runner.Require(arrows.Count == 2, "two realized repeat buttons required");
            // Visual-tree enumeration order is not the control's semantic arrow order.
            up = arrows.Find(button => Grid.GetRow(button) == 0);
            down = arrows.Find(button => Grid.GetRow(button) == 1);
            Runner.Require(up != null && down != null && !ReferenceEquals(up, down), "distinct arrows in grid rows 0/1 required");
            Runner.Require(Center(up).Y < Center(down).Y, "up arrow must be geometrically above down arrow");
            AcceptLog.Info("AUTOSAVE_ARROWS upRow=" + Grid.GetRow(up) + " downRow=" + Grid.GetRow(down) +
                " upY=" + Center(up).Y + " downY=" + Center(down).Y);
            Runner.Require(editor.Template != null && Find(editor, "NormalMode") != null && Find(editor, "GlowMode") != null,
                "actual surface template was not applied");
            foreach (string key in new[] { "Button_01_Left", "Button_01_Center", "Button_01_Right", "Button_02_Left_", "Button_02_Center", "Button_02_Right" })
                Runner.Require(editor.TryFindResource(key) != null, "attached menu resource missing: " + key);
            CheckImages(Find(editor, "NormalMode"));
            CheckImages(Find(editor, "GlowMode"));
            Runner.Require(Find(up, "Chevron") != null && Find(down, "Chevron") != null, "arrow templates not realized");
            TypeText(step.Arg(0, "0")); KeyStroke(Key.Return);
            yield return NextFrame.Instance;
            int value = step.Int(0);
            Runner.Require(Draft == value, "typed value not acknowledged: " + value);
            var unit = (TextBlock)Read(editor, "unit");
            Runner.Require(unit.Visibility == (value == 0 ? Visibility.Hidden : Visibility.Visible), "unit column visibility");
            double[] geometry = Geometry();
            if (baseline == null) baseline = geometry;
            for (int i = 0; i < geometry.Length; i++) Runner.Require(Math.Abs(geometry[i] - baseline[i]) <= 0.5, "value-dependent editor/arrow movement");
            Runner.Require(box.ActualWidth > 0 && up.ActualWidth > 0 && down.ActualHeight > 0, "empty editor layout");
            AcceptLog.Info("AUTOSAVE_LAYOUT value=" + value + " text=" + box.Text + " geometry=" + string.Join(",", Array.ConvertAll(geometry, n => n.ToString("F2", CultureInfo.InvariantCulture))) + " resources=True template=True");
        }

        private static IEnumerable<Wait> Surface(Step step)
        {
            box.Keyboard.ClearFocus();
            bool disabled = step.Arg(0) == "disabled";
            editor.IsEnabled = !disabled;
            if (step.Arg(0) == "normal") Input.MouseMove(0, 0);
            else Move(editor);
            yield return NextFrame.Instance;
            Runner.Require(Find(editor, "DisabledMask").Visibility == (disabled ? Visibility.Visible : Visibility.Hidden), "disabled surface trigger");
            if (!disabled) Runner.Require(Find(editor, "GlowMode").Visibility == (step.Arg(0) == "normal" ? Visibility.Hidden : Visibility.Visible), "hover surface trigger");
            int before = Draft;
            if (disabled)
            {
                Click(up);
                yield return NextFrame.Instance;
                Runner.Require(Draft == before, "disabled arrow accepted input");
            }
            AcceptLog.Info("AUTOSAVE_SURFACE mode=" + step.Arg(0) + " disabledInputBlocked=" + disabled);
        }

        private static IEnumerable<Wait> InputRegression(Step step)
        {
            editor.IsEnabled = true;
            Runner.Require(GamePlayerManagerAPI.Instance.IsLocalPaused(), "fixture pause lost");
            try
            {
                TypeText("0"); KeyStroke(Key.Return);
                // Close through the real menu visibility property, whose observer applies pending edits.
                Close();
                yield return NextFrame.Instance;
                Runner.Require(Minutes == 0, "zero not applied on menu close");
                Open();
                yield return new Until("editor visible again", () => editor.IsVisible);
                TypeText("10"); KeyStroke(Key.Return);
                Runner.Require(box.IsKeyboardFocused && Draft == 10 && Minutes == 0, "Enter/focus changed the active interval early");
                yield return new Delay(14000);
                Runner.Require(Minutes == 0, "pending interval applied before inactivity delay");
                yield return new Delay(2500);
                Runner.Require(Minutes == 10, "15 second inactivity did not apply interval");
                AcceptLog.Info("AUTOSAVE_PENDING idle15s=True enterKeepsFocus=True");

                TypeText("100"); KeyStroke(Key.Escape);
                Runner.Require(Draft == 10 && Minutes == 10, "Escape did not cancel pending edit");
                Close();
                yield return NextFrame.Instance;
                Runner.Require(Minutes == 10, "cancelled edit committed on close");
                Open();
                yield return new Until("editor reopened after cancel", () => editor.IsVisible);
                TypeText("1"); KeyStroke(Key.Return);
                Close();
                yield return NextFrame.Instance;
                Runner.Require(Minutes == 1, "valid edit did not apply on close");
                Open();
                yield return new Until("editor reopened for repeat", () => editor.IsVisible);
                TypeText("10"); KeyStroke(Key.Return);
                Click(up);
                AcceptLog.Info("AUTOSAVE_REPEAT singleClick row=" + Grid.GetRow(up) + " y=" + Center(up).Y + " draft=" + Draft + " delta=" + (Draft - 10));
                Runner.Require(Draft == 11, "standalone up click did not increment exactly once");
                var repeats = new List<int>();
                foreach (int duration in new[] { 850, 1500 })
                {
                    TypeText("10"); KeyStroke(Key.Return);
                    foreach (Wait wait in Shot("Autosave-repeat-before-" + duration)) yield return wait;
                    Runner.Require(up.IsVisible && up.IsEnabled && ReferenceEquals(editor, AttachedEditor()), "repeat target not the current visible enabled editor");
                    object parent = up.Parent;
                    RepeatState("beforeDown", duration, parent);
                    Point point = Center(up);
                    Input.MouseMove((int)point.X, (int)point.Y);
                    Input.MouseButtonDown((int)point.X, (int)point.Y, MouseButton.Left);
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        RepeatState("afterDown", duration, parent);
                        while (clock.ElapsedMilliseconds < duration)
                        {
                            yield return NextFrame.Instance;
                            point = Center(up);
                            Input.MouseMove((int)point.X, (int)point.Y);
                            RepeatState("frame@" + clock.ElapsedMilliseconds, duration, parent);
                        }
                    }
                    finally { Input.MouseButtonUp((int)point.X, (int)point.Y, MouseButton.Left); RepeatState("afterUp", duration, parent); }
                    int result = Draft;
                    repeats.Add(result);
                    foreach (Wait wait in Shot("Autosave-repeat-after-" + duration)) yield return wait;
                    Runner.Require(Draft == result, "repeat continued after mouse release");
                }
                Runner.Require(repeats.TrueForAll(value => value >= 12), "held native RepeatButton did not repeat at 850ms/1500ms");
                TypeText("1440"); KeyStroke(Key.Return); Click(up);
                Runner.Require(Draft == 1440, "upper arrow exceeded maximum");
                TypeText("0"); KeyStroke(Key.Return); Click(down);
                Runner.Require(Draft == 0, "lower arrow exceeded minimum");
                AcceptLog.Info("AUTOSAVE_INPUT cancel=True closeCommit=True repeat=" + string.Join(",", repeats) + " repeatReleased=True bounds=True");
            }
            finally
            {
                // Also runs on assertion failures. No schedule/files are created by reflection.
                editor.IsEnabled = true;
                Open();
                // Cleanup may follow a failed hit test: use the existing guarded production callback.
                var context = (IngameMenuContentContext)Read(editor, "context");
                Runner.Require(context.TryExecute(() => ((Action<int>)Read(editor, "setPending"))(0)), "cleanup callback rejected");
                Close();
                box.Keyboard.ClearFocus();
                Runner.Require(Minutes == 0 && GamePlayerManagerAPI.Instance.IsLocalPaused(), "cleanup must leave autosave off and simulation paused");
                AcceptLog.Info("AUTOSAVE_CLEANUP interval=0 paused=True");
            }
            yield return NextFrame.Instance;
            Runner.Require(Minutes == 0 && GamePlayerManagerAPI.Instance.IsLocalPaused(), "cleanup must leave autosave off and simulation paused");
        }

        private static IEnumerable<Wait> Shot(string name) => (IEnumerable<Wait>)typeof(GameSteps)
            .GetMethod("Screenshot", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { new Step { Args = new[] { name } } });
        private static void RepeatState(string phase, int duration, object parent)
        {
            Point p = up.Mouse.Position;
            var buttons = new List<RepeatButton>();
            Collect(editor, buttons);
            object physical = GameSteps.FindType("UnityEngine.Input")?.GetProperty("mousePosition", BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
            AcceptLog.Info("AUTOSAVE_REPEAT phase=" + phase + " holdMs=" + duration + " draft=" + Draft +
                " pressed=" + up.IsPressed + " over=" + up.IsMouseOver + " enabled=" + up.IsEnabled +
                " visible=" + up.IsVisible + " captured=" + up.IsMouseCaptured + " focus=" + box.IsKeyboardFocused +
                " noesisPointer=" + p.X + "," + p.Y + " physicalPointer=" + physical +
                " sameParent=" + ReferenceEquals(parent, up.Parent) + " parent=" + up.Parent?.GetType().FullName +
                " row=" + Grid.GetRow(up) + " y=" + Center(up).Y +
                " sameButton=" + (buttons.Count == 2 && buttons.Exists(button => ReferenceEquals(button, up) && Grid.GetRow(button) == 0)) + " attached=" + ReferenceEquals(editor, AttachedEditor()));
        }

        private static void Open() { if (!MainViewModel.Instance.Show_HUD_IngameMenu) MainViewModel.Instance.HUDmain.InGameOptions(null, null); }
        private static void Close()
        {
            Runner.Require(GamePlayerManagerAPI.Instance.IsLocalPaused(), "simulation resumed before menu close");
            MainViewModel.Instance.Show_HUD_IngameMenu = false;
            // No rendered frame may advance with a positive test interval and a resumed world.
            if (!GamePlayerManagerAPI.Instance.IsLocalPaused())
                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 1, 1);
            Runner.Require(GamePlayerManagerAPI.Instance.IsLocalPaused(), "simulation pause not retained after menu close");
            AcceptLog.Info("AUTOSAVE_CLOSE visibilityObserver=True vanillaEscapeRoute=False pausedBeforeAfter=True");
        }
        private static void KeyStroke(Key key) { Input.KeyDown(key); Input.KeyUp(key); }
        private static Point Center(FrameworkElement element) => element.PointToScreen(new Point((float)element.ActualWidth / 2, (float)element.ActualHeight / 2));
        private static void Move(FrameworkElement element) { Point p = Center(element); Input.MouseMove((int)p.X, (int)p.Y); }
        private static void Click(FrameworkElement element)
        {
            Point p = Center(element);
            Input.MouseMove((int)p.X, (int)p.Y); Input.MouseButtonDown((int)p.X, (int)p.Y, MouseButton.Left); Input.MouseButtonUp((int)p.X, (int)p.Y, MouseButton.Left);
        }
        private static void TypeText(string value)
        {
            Click(box);
            Input.KeyDown(Key.LeftCtrl); Input.KeyDown(Key.A); Input.KeyUp(Key.A); Input.KeyUp(Key.LeftCtrl);
            foreach (char c in value) Input.Char(c);
        }
        private static double[] Geometry() => new[] { (double)box.PointToScreen(new Point(0, 0)).X, box.ActualWidth,
            (double)up.PointToScreen(new Point(0, 0)).X, up.ActualWidth, (double)down.PointToScreen(new Point(0, 0)).X, down.ActualWidth };
        private static FrameworkElement Find(DependencyObject root, string name)
        {
            if (root is FrameworkElement element && element.Name == name) return element;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            { var result = Find(VisualTreeHelper.GetChild(root, i), name); if (result != null) return result; }
            return null;
        }
        private static void Collect(DependencyObject root, List<RepeatButton> buttons)
        {
            if (root is RepeatButton button) buttons.Add(button);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) Collect(VisualTreeHelper.GetChild(root, i), buttons);
        }
        private static void CheckImages(DependencyObject root)
        {
            if (root is Image image) Runner.Require(image.Source != null, "surface image binding unresolved");
            if (root is Noesis.Rectangle rectangle && rectangle.Fill is ImageBrush brush)
                Runner.Require(brush.ImageSource != null, "surface center brush binding unresolved");
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) CheckImages(VisualTreeHelper.GetChild(root, i));
        }
    }
}

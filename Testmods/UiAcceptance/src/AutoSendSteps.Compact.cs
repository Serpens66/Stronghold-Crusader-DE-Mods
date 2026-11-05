using System;
using System.Collections;
using System.Collections.Generic;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;

namespace UiAcceptance
{
    internal static partial class AutoSendSteps
    {
        private static IEnumerable<Wait> CompactUi()
        {
            yield return new Until("compact wood selection", () => OpenSelection(first, eGoods.STORED_WOOD_PLANKS));
            yield return new Until("compact editors ready", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending") && RenderedValuesMatch());
            bool originalKeyboard = GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard();
            object view = Read(runtime, "view"), originalRoot = Read(view, "root");
            AcceptLog.Info("AUTOSEND_KEYBOARD start=" + originalKeyboard + " focus=" + KeyboardFocus((FrameworkElement)originalRoot));
            var originalBox = EditorBox("reserve");
            CheckButtons();
            Runner.Require(((FrameworkElement)originalRoot).ActualHeight <= 90, "compact section exceeds 90 XAML units");
            foreach (string member in new[] { "reserve", "minimum", "maximum" })
            {
                Slider slider = EditorSlider(member);
                Runner.Require(slider.Minimum == 0 && slider.Maximum == 100 && slider.TickFrequency == 1,
                    "ware slider range/step changed");
            }
            foreach (Wait wait in TypeValue("maximum", "100", d => Number(Read(d, "Rule"), "Maximum") == 100)) yield return wait;
            foreach (Wait wait in SliderEnd("reserve", 100)) yield return wait;
            foreach (Wait wait in TypeValue("reserve", "111", d => Number(d, "Reserve") == 111)) yield return wait;
            Runner.Require(EditorSlider("reserve").Value == 100 && EditorBox("reserve").Text == "111", "free ware value was coerced by slider");
            foreach (Wait wait in TypeValue("minimum", "35", d => Number(Read(d, "Rule"), "Minimum") == 35)) yield return wait;
            foreach (Wait wait in TypeValue("maximum", "20", d => Number(Read(d, "Rule"), "Maximum") == 20 && Number(Read(d, "Rule"), "Minimum") == 20)) yield return wait;
            foreach (Wait wait in ClickAndAcknowledge("enabled", d => Flag(Read(d, "Rule"), "Enabled"))) yield return wait;
            yield return new Until("active ware marker", () => Marker(eGoods.STORED_WOOD_PLANKS)?.Visibility == Visibility.Visible);
            FrameworkElement originalMarker = Marker(eGoods.STORED_WOOD_PLANKS);

            // A second provider shares the same existing goods button without replacing
            // its command, the first provider, or the compact editor controls.
            var peerMarkers = new List<FrameworkElement>();
            Runner.Require(AllyGoodsPanelExtensions.TryRegisterGoodsAdornment("UiAcceptance_Serp", "autosend-peer",
                (context, good) => { var marker = new Border { Width = 6, Height = 6,
                    Background = new SolidColorBrush(Color.FromArgb(255, 130, 180, 220)) }; peerMarkers.Add(marker); return marker; },
                out _, out string reason), "second marker provider failed: " + reason);
            yield return new Until("second provider attached", () => peerMarkers.Count >= 21 && peerMarkers[0].Parent != null);
            Runner.Require(ReferenceEquals(originalRoot, Read(view, "root")) && ReferenceEquals(originalBox, EditorBox("reserve")) &&
                ReferenceEquals(originalMarker, Marker(eGoods.STORED_WOOD_PLANKS)), "registration recreated existing UI controls");

            yield return new Until("other recipient", () => OpenSelection(second, eGoods.STORED_WOOD_PLANKS));
            yield return new Until("other recipient values/marker", () => RenderedValuesMatch() && Marker(eGoods.STORED_WOOD_PLANKS)?.Visibility != Visibility.Visible);
            Runner.Require(Number(Display(), "Reserve") == 111 && Number(Read(Display(), "Rule"), "Minimum") == 0,
                "own reserve not shared or recipient rule leaked");
            yield return new Until("first recipient again", () => OpenSelection(first, eGoods.STORED_WOOD_PLANKS));
            yield return new Until("first marker restored", () => RenderedValuesMatch() && Marker(eGoods.STORED_WOOD_PLANKS)?.Visibility == Visibility.Visible);

            // Freeze actual simulation, retain a queued Chore, edit another field and
            // change recipient before delivery. Presentation must stay usable throughout.
            EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 1, 1);
            yield return new Until("fixture simulation paused", () => GamePlayerManagerAPI.Instance.IsLocalPaused());
            TypeNow(EditorBox("minimum"), "45");
            yield return new Until("actual queued edit pending", () => Flag(Display(), "Pending"));
            // Model enqueue precedes the next before-render publication. Inspect the
            // actual control only after it has had a chance to reflect that state.
            yield return new Until("toggle reflects pending edit", () => !Flag(Display(), "Pending") ||
                !((Button)Read(view, "enabled")).IsEnabled);
            CheckButtons();
            TypeNow(EditorBox("maximum"), "80");
            yield return new Until("switch recipient with pending edits", () => OpenSelection(second, eGoods.STORED_WOOD_PLANKS));
            EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 0, 0);
            yield return new Until("pending edits delivered to frozen recipient", () => !Flag(Display(), "Pending"));
            Runner.Require(Number(Read(Display(), "Rule"), "Minimum") == 0 && Number(Read(Display(), "Rule"), "Maximum") == 0,
                "queued edit mutated new recipient");
            yield return new Until("inspect frozen original recipient", () => OpenSelection(first, eGoods.STORED_WOOD_PLANKS));
            yield return new Until("original edited values rendered", RenderedValuesMatch);
            Runner.Require(Number(Read(Display(), "Rule"), "Minimum") == 45 && Number(Read(Display(), "Rule"), "Maximum") == 80,
                "pending edits were dropped or reordered");
            Runner.Require(ReferenceEquals(originalRoot, Read(view, "root")) && ReferenceEquals(originalBox, EditorBox("reserve")),
                "edits recreated compact controls");
            foreach (Wait wait in TypeValue("maximum", "0", d => !Flag(Read(d, "Rule"), "Enabled") && Number(Read(d, "Rule"), "Minimum") == 0)) yield return wait;
            yield return new Until("stopped ware marker hidden", () => Marker(eGoods.STORED_WOOD_PLANKS)?.Visibility != Visibility.Visible);

            yield return new Until("gold editor", () => OpenSelection(first, eGoods.STORED_GOLD));
            yield return new Until("gold rendered", RenderedValuesMatch);
            Runner.Require(EditorSlider("reserve").Maximum == 10000 && EditorSlider("reserve").TickFrequency == 10, "gold slider range/step wrong");
            foreach (Wait wait in TypeValue("reserve", "17777", d => Number(d, "Reserve") == 17777)) yield return wait;
            Runner.Require(EditorSlider("reserve").Value == 10000 && EditorBox("reserve").Text == "17777", "large gold value coerced");
            foreach (Wait wait in TypeValue("reserve", "13", d => Number(d, "Reserve") == 13)) yield return wait;
            Runner.Require(EditorBox("reserve").Text == "13", "manual single gold pieces lost");
            foreach (Wait wait in SliderEnd("reserve", 10000)) yield return wait;
            object panel = GameSteps.Read(typeof(MainViewModel), MainViewModel.Instance, "HUDAlliesPanel", System.Reflection.BindingFlags.Instance);
            panel.GetType().GetMethod("ButtonClicked", Instance, null, new[] { typeof(string) }, null).Invoke(panel, new object[] { "Back" });
            yield return new Until("send panel closed", () => AllyGoodsPanelExtensions.TryGetContext(out var closed) && !closed.IsSendMode);
            AcceptLog.Info("AUTOSEND_KEYBOARD afterBack=" + GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard() +
                " expected=" + originalKeyboard + " focus=" + KeyboardFocus((FrameworkElement)originalRoot));
            yield return new Until("keyboard returned to previous owner", () => GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard() == originalKeyboard);
            yield return new Until("reopen after keyboard restore", () => OpenSelection(first, eGoods.STORED_GOLD));
            yield return new Until("reopened values rendered", RenderedValuesMatch);
            FatControler.instance.NGview.MouseMove(1, 1);
            yield return NextFrame.Instance;
            AcceptLog.Info("AUTOSEND_COMPACT actual text/keys/toggle; free values; coupled bounds; pending queue/context switch; stable controls; two marker providers PASS");
        }

        private static TextBox EditorBox(string member) => (TextBox)Read(Read(Read(runtime, "view"), member), "Box");
        private static string KeyboardFocus(FrameworkElement root)
        {
            var focused = root.Keyboard.FocusedElement;
            return focused == null ? "none" : focused.GetType().Name + "/" + (focused as FrameworkElement)?.Name;
        }
        private static Slider EditorSlider(string member) => (Slider)Read(Read(Read(runtime, "view"), member), "Slider");
        private static IEnumerable<Wait> SliderEnd(string member, int expected)
        {
            var slider = EditorSlider(member);
            Point center = slider.PointToScreen(new Point((float)slider.ActualWidth / 2, (float)slider.ActualHeight / 2));
            NoesisView input = FatControler.instance.NGview;
            int x = (int)center.X, y = (int)center.Y;
            input.MouseMove(x, y); input.MouseButtonDown(x, y, MouseButton.Left); input.MouseButtonUp(x, y, MouseButton.Left);
            yield return NextFrame.Instance;
            Runner.Require(GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard(), "slider focus did not capture keyboard from game");
            input.KeyDown(Key.End); input.KeyUp(Key.End);
            yield return new Until("slider keyboard end acknowledged", () => !Flag(Display(), "Pending") && Number(Display(), "Reserve") == expected);
            yield return new Until("slider keyboard value rendered", RenderedValuesMatch);
            AcceptLog.Info("AUTOSEND_SLIDER " + member + "=" + expected + " keyboardCaptured=True");
        }
        private static FrameworkElement Marker(eGoods good)
        {
            var map = (IDictionary)Read(Read(runtime, "view"), "badges");
            return map.Contains(good) ? Read(map[good], "Element") as FrameworkElement : null;
        }
        private static void TypeNow(TextBox box, string value)
        {
            NoesisView input = FatControler.instance.NGview;
            Point center = box.PointToScreen(new Point((float)box.ActualWidth / 2, (float)box.ActualHeight / 2));
            int x = (int)center.X, y = (int)center.Y;
            input.MouseMove(x, y); input.MouseButtonDown(x, y, MouseButton.Left); input.MouseButtonUp(x, y, MouseButton.Left);
            input.KeyDown(Key.LeftCtrl); input.KeyDown(Key.A); input.KeyUp(Key.A); input.KeyUp(Key.LeftCtrl);
            foreach (char c in value) input.Char(c);
            input.KeyDown(Key.Return); input.KeyUp(Key.Return);
        }
        private static IEnumerable<Wait> TypeValue(string member, string value, Func<object, bool> acknowledged)
        {
            yield return new Until("ready before text entry", () => Flag(Display(), "Ready") && !Flag(Display(), "Pending") && RenderedValuesMatch());
            TypeNow(EditorBox(member), value);
            yield return new Until("native acknowledgement of typed " + member + "=" + value, () => !Flag(Display(), "Pending") && acknowledged(Display()));
            yield return new Until("typed numeric values rendered", RenderedValuesMatch);
            AcceptLog.Info("AUTOSEND_TYPED " + member + "=" + value + " actualNoesisInput=True");
        }
    }
}

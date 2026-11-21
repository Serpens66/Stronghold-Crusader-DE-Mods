using System;
using System.Collections.Generic;
using APIShared.Input;
using CrusaderDE;
using Noesis;
using UnityEngine;
using Key = Noesis.Key;
using Point = Noesis.Point;

namespace UiAcceptance
{
    internal static class ModKeybindSteps
    {
        private const string FixtureTitle = "Test options binding";

        internal static void Register() => Runner.Register("ModKeybinds.Options", Options);

        private static IEnumerable<Wait> Options(Step step)
        {
            KeybindChord initial = new KeybindChord(KeyCode.F9);
            KeybindChord configured = initial;
            var fixture = new ModKeybindRegistration(UiAcceptancePlugin.Guid, "options-acceptance", "UI Acceptance",
                () => FixtureTitle, initial, () => configured, chord => configured = chord,
                chord => true, () => true, () => false, () => { });
            Runner.Require(ModKeybinds.TryRegister(fixture, out string reason), "Fixture registration failed: " + reason);

            MainViewModel model = MainViewModel.Instance;
            yield return new Delay(1000);
            model.FrontEndMenu.ButtonClicked("Options");
            yield return new Until("Vanilla options opened", () => model.Show_HUD_Options && model.HUDOptions != null);
            HUD_Options options = model.HUDOptions;
            options.ButtonClicked(3);
            var keyboardTab = options.FindName("KeySettings") as Grid;
            var vanillaList = options.FindName("HotKeyList") as ListView;
            Runner.Require(keyboardTab != null && vanillaList != null, "Vanilla keyboard tab or list missing");
            var input = FatControler.instance.NGview;
            bool originalMouse = input.EnableMouse;
            bool originalKeyboard = input.EnableKeyboard;
            try
            {
                input.EnableMouse = false;
                input.EnableKeyboard = true;
                const string footerName = "APISharedModKeybindsFooter";
                StackPanel footer = null;
                yield return new Until("Mod shortcuts in the vanilla scroll area", () =>
                    IsReady(footer = Find(vanillaList, element => element.Name == footerName) as StackPanel));
                var scroll = Find(vanillaList, element => element.Name == "APISharedHotKeyScroll") as ScrollViewer;
                Runner.Require(scroll != null && Find(scroll, element => ReferenceEquals(element, footer)) != null &&
                    Find(scroll, element => element is ItemsPresenter) != null, "Vanilla rows and mod footer do not share a scroller");
                int vanillaRows = vanillaList.Items.Count;
                Runner.Require(vanillaRows > 0, "Vanilla hotkey list is empty");
                yield return new Delay(1000);
                GameSteps.CaptureScreenshot("mod-keybinds-list-top");
                yield return NextFrame.Instance;
                Point scrollPoint = scroll.PointToScreen(new Point(scroll.ActualWidth / 2, scroll.ActualHeight / 2));
                float offsetBeforeWheel = scroll.VerticalOffset;
                input.MouseWheel((int)scrollPoint.X, (int)scrollPoint.Y, -120);
                yield return new Until("Mouse wheel moves the shared scroll area", () => scroll.VerticalOffset > offsetBeforeWheel);
                scroll.ScrollToVerticalOffset(0);
                yield return new Until("Scroll area returned to its beginning", () => scroll.VerticalOffset < 1);

                FrameworkElement vanillaPopup = options.FindName("OptionsHotKeyPanel") as FrameworkElement;
                Runner.Require(vanillaPopup != null, "Vanilla editor is missing");
                vanillaList.SelectedIndex = 0;
                yield return new Until("Vanilla row opens the vanilla editor", () => vanillaPopup.IsVisible);
                options.ButtonClicked(-101);
                yield return new Until("Vanilla editor closed", () => !vanillaPopup.IsVisible);
                Runner.Require(vanillaList.Items.Count == vanillaRows, "Vanilla editor changed the list length");

                ExpectLoadedModRow(footer, "CastlePlanner_Serp", "blueprint-toggle");
                ExpectLoadedModRow(footer, "BugfixesAndQoL_Serp", "damaged-health-bars");
                var row = Find(footer, element => element is Button button && button.Content is Grid content &&
                    content.Children[0] is TextBlock label && label.Text == FixtureTitle) as Button;
                Runner.Require(row != null, "The registered action has no settings row");
                var value = (TextBlock)((Grid)row.Content).Children[1];
                Runner.Require(value.Text == ModKeybinds.Format(initial), "Default chord did not appear in the row");
                scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
                yield return new Until("Shared scrollbar at the bottom", () => scroll.ScrollableHeight > 0 &&
                    Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 2);
                yield return NextFrame.Instance;
                GameSteps.CaptureScreenshot("mod-keybinds-inline");
                Runner.Require(ToolTipService.GetToolTip(row) is ToolTip tip && Equals(tip.Content, fixture.ModName),
                    "Mod shortcut tooltip contains more than the mod name");
                object vanillaContext = vanillaPopup.DataContext;
                var choose = FindCommandButton(vanillaPopup, "-102");
                var reset = FindCommandButton(vanillaPopup, "-103");
                var cancel = FindCommandButton(vanillaPopup, "-104");
                var apply = FindCommandButton(vanillaPopup, "-105");
                var clear = FindCommandButton(vanillaPopup, "-106");
                var vanillaBinding = KeyManager.instance.GetKeyCode(Enums.KeyFunctions.Left, 0);

                foreach (Wait wait in Click(row)) yield return wait;
                yield return new Until("Mod row opens the original game dialog", () =>
                    vanillaPopup.IsVisible && !ReferenceEquals(vanillaPopup.DataContext, vanillaContext) && IsReady(choose));
                foreach (Wait wait in Click(choose)) yield return wait;
                Runner.Require(ModKeybinds.IsCapturing(fixture), "Choose did not start capture. Application focus=" + Application.isFocused);
                yield return new Until("Vanilla capture controls visible", () => IsReady(cancel) && IsReady(clear));
                Runner.Require(!apply.IsEnabled, "Apply enabled without a pending key");
                GameSteps.CaptureScreenshot("mod-keybind-vanilla-editor");
                yield return NextFrame.Instance;

                input.KeyDown(Key.F8);
                input.KeyUp(Key.F8);
                var captured = new KeybindChord(KeyCode.F8);
                yield return new Until("Pending key enables the game's Apply button", () => IsReady(apply) && apply.IsEnabled);
                Runner.Require(configured.Equals(initial) && ModKeybinds.IsCapturing(fixture), "Preview committed before Apply");
                foreach (Wait wait in Click(apply)) yield return wait;
                yield return new Until("Apply commits the shortcut", () => configured.Equals(captured) &&
                    !ModKeybinds.IsCapturing(fixture) && value.Text == ModKeybinds.Format(captured) && IsReady(choose));

                foreach (Wait wait in Click(choose)) yield return wait;
                yield return new Until("Second capture active", () => ModKeybinds.IsCapturing(fixture) && IsReady(cancel));
                yield return NextFrame.Instance;
                input.KeyDown(Key.F7);
                input.KeyUp(Key.F7);
                yield return new Until("Alternative key is staged", () => IsReady(apply) && apply.IsEnabled);
                input.KeyDown(Key.Escape);
                input.KeyUp(Key.Escape);
                yield return new Until("Escape cancels only the capture", () => !ModKeybinds.IsCapturing(fixture) && IsReady(choose));
                Runner.Require(configured.Equals(captured), "Escape saved a pending key");

                foreach (Wait wait in Click(choose)) yield return wait;
                yield return new Until("Clear available during capture", () => IsReady(clear) && clear.IsEnabled);
                foreach (Wait wait in Click(clear)) yield return wait;
                yield return new Until("Clear removes the shortcut", () => configured.Key == KeyCode.None && IsReady(reset));
                foreach (Wait wait in Click(reset)) yield return wait;
                yield return new Until("Reset restores the default", () =>
                    configured.Equals(initial) && value.Text == ModKeybinds.Format(initial));
                Runner.Require(KeyManager.instance.GetKeyCode(Enums.KeyFunctions.Left, 0) == vanillaBinding,
                    "Mod edits changed a vanilla shortcut");

                input.KeyDown(Key.Escape);
                input.KeyUp(Key.Escape);
                yield return new Until("Escape restores the original context and focuses the row", () =>
                    !vanillaPopup.IsVisible && row.IsKeyboardFocused && ReferenceEquals(vanillaPopup.DataContext, vanillaContext));
                input.KeyDown(Key.Return);
                input.KeyUp(Key.Return);
                yield return new Until("Enter opens the focused mod row", () => vanillaPopup.IsVisible);
                options.ButtonClicked(2);
                yield return new Until("Tab change closes the dialog", () => !vanillaPopup.IsVisible);
                options.ButtonClicked(3);
                yield return new Until("Inline footer reappears", () => footer.IsVisible);
                vanillaList.SelectedIndex = 0;
                yield return new Until("Vanilla editor works after a mod edit", () =>
                    vanillaPopup.IsVisible && ReferenceEquals(vanillaPopup.DataContext, vanillaContext));
                foreach (Wait wait in Click(choose)) yield return wait;
                yield return new Until("Vanilla capture command still works", () =>
                    KeyManager.instance.HotKeySelectorMode && IsReady(cancel));
                Runner.Require(!ModKeybinds.IsCapturing(fixture), "Vanilla capture used the mod registry");
                foreach (Wait wait in Click(cancel)) yield return wait;
                yield return new Until("Vanilla capture cancelled", () => !KeyManager.instance.HotKeySelectorMode && IsReady(choose));
                options.ButtonClicked(-101);
                yield return new Until("Vanilla editor closed again", () => !vanillaPopup.IsVisible);
                Runner.Require(fixture.TrySet(captured), "Could not prepare nondefault mod binding");
                options.ButtonClicked(42);
                yield return NextFrame.Instance;
                Runner.Require(configured.Equals(captured) && vanillaList.Items.Count == vanillaRows,
                    "Vanilla reset modified a mod shortcut or the original list");
                AcceptLog.Info("MOD_HOTKEYS_VIEWPORT " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height
                    + " mode=" + UnityEngine.Screen.fullScreenMode);
                ModKeybindOptions.SetEnabled(keyboardTab, false);
                yield return new Until("Original vanilla template restored", () => Find(vanillaList,
                    element => element.Name == footerName) == null && vanillaList.Items.Count == vanillaRows);
                yield return NextFrame.Instance;
                GameSteps.CaptureScreenshot("vanilla-list-baseline");
                yield return NextFrame.Instance;
            }
            finally
            {
                ModKeybinds.CancelCapture(fixture);
                fixture.TrySet(initial);
                input.EnableKeyboard = originalKeyboard;
                input.EnableMouse = originalMouse;
            }
        }

        private static void ExpectLoadedModRow(FrameworkElement page, string owner, string action)
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(owner)) return;
            foreach (ModKeybindRegistration registration in ModKeybinds.Registrations)
            {
                if (registration.OwnerGuid != owner || registration.Id != action) continue;
                string label = registration.DisplayName;
                Runner.Require(Find(page, element => element is TextBlock text && text.Text == label) != null,
                    "Registered action is missing from the options: " + owner + "/" + action);
                return;
            }
            throw new StepFailure("Loaded mod did not register its action: " + owner + "/" + action);
        }

        private static Button FindCommandButton(FrameworkElement popup, string parameter)
        {
            var button = Find(popup, element => element is Button candidate &&
                Convert.ToString(candidate.CommandParameter, System.Globalization.CultureInfo.InvariantCulture) == parameter) as Button;
            Runner.Require(button != null, "Missing original options button " + parameter);
            return button;
        }

        private static FrameworkElement Find(DependencyObject root, Predicate<FrameworkElement> matches)
        {
            if (root == null) return null;
            if (root is FrameworkElement element && matches(element)) return element;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                FrameworkElement found = Find(VisualTreeHelper.GetChild(root, index), matches);
                if (found != null) return found;
            }
            return null;
        }

        private static bool IsReady(FrameworkElement element) => element != null && element.IsVisible &&
            element.ActualWidth > 0 && element.ActualHeight > 0;

        private static IEnumerable<Wait> Click(FrameworkElement element)
        {
            Runner.Require(IsReady(element) && element.IsEnabled, "Mod keybind control is hidden or disabled");
            Point point = element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
            var input = FatControler.instance.NGview;
            input.MouseMove((int)point.X, (int)point.Y);
            Runner.Require(element.IsMouseOver, $"Mouse missed {element.Name} at {point.X},{point.Y} (screen {UnityEngine.Screen.width}x{UnityEngine.Screen.height})");
            input.MouseButtonDown((int)point.X, (int)point.Y, MouseButton.Left);
            yield return NextFrame.Instance;
            input.MouseButtonUp((int)point.X, (int)point.Y, MouseButton.Left);
        }
    }
}

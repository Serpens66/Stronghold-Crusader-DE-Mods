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
                const string openName = "APISharedModKeybindsOpen";
                const string pageName = "APISharedModKeybindsPage";
                yield return new Until("Mod Keybinds and vanilla list visible", () =>
                    IsReady(Find(keyboardTab, element => element.Name == openName)) && IsReady(vanillaList));
                int vanillaRows = vanillaList.Items.Count;
                Runner.Require(vanillaRows > 0, "Vanilla hotkey list is empty");
                yield return new Delay(1000);
                foreach (Wait wait in Click(Find(keyboardTab, element => element.Name == openName))) yield return wait;
                FrameworkElement page = Find(keyboardTab, element => element.Name == pageName);
                yield return new Until("Mod Keybinds page visible", () => IsReady(page));
                ExpectLoadedModRow(page, "CastlePlanner_Serp", "blueprint-toggle");
                ExpectLoadedModRow(page, "BugfixesAndQoL_Serp", "damaged-health-bars");
                GameSteps.CaptureScreenshot("mod-keybinds");
                var title = Find(page, element => element is TextBlock label && label.Text == FixtureTitle);
                var row = title?.Parent as StackPanel;
                Runner.Require(row != null && row.Children.Count == 4 && row.Children[3] is StackPanel,
                    "The registered action has no settings row");
                var buttons = (StackPanel)row.Children[3];
                var value = (TextBlock)row.Children[1];
                var assign = (Button)buttons.Children[0];
                var clear = (Button)buttons.Children[1];
                var reset = (Button)buttons.Children[2];
                Runner.Require(value.Text == ModKeybinds.Format(initial), "Default chord did not appear in the row");
                assign.BringIntoView();
                yield return NextFrame.Instance;
                foreach (Wait wait in Click(assign)) yield return wait;
                Runner.Require(ModKeybinds.IsCapturing(fixture), "Assign did not start capture. Application focus=" + Application.isFocused);
                yield return NextFrame.Instance;
                input.KeyDown(Key.F8);
                input.KeyUp(Key.F8);
                var captured = new KeybindChord(KeyCode.F8);
                yield return new Until("Captured key shown in its row", () => configured.Equals(captured) &&
                    !ModKeybinds.IsCapturing(fixture) && value.Text == ModKeybinds.Format(captured) && assign.IsEnabled);

                foreach (Wait wait in Click(assign)) yield return wait;
                Runner.Require(ModKeybinds.IsCapturing(fixture), "Could not start a second assignment");
                yield return NextFrame.Instance;
                input.KeyDown(Key.Escape);
                input.KeyUp(Key.Escape);
                yield return new Until("Escape cancelled assignment", () => !ModKeybinds.IsCapturing(fixture) && assign.IsEnabled);
                Runner.Require(configured.Equals(captured) && page.IsVisible, "Escape during assignment changed the chord or left the page");

                foreach (Wait wait in Click(clear)) yield return wait;
                yield return new Until("Clear removed the key", () => configured.Key == KeyCode.None);
                foreach (Wait wait in Click(reset)) yield return wait;
                yield return new Until("Reset restored the default chord", () =>
                    configured.Equals(initial) && value.Text == ModKeybinds.Format(initial));
                var back = Find(page, element => element.Name == "APISharedModKeybindsBack");
                Runner.Require(back != null && back.Focus(), "Back could not receive keyboard focus");
                input.KeyDown(Key.Escape);
                input.KeyUp(Key.Escape);
                yield return new Until("Escape returned to vanilla hotkeys", () => !page.IsVisible && IsReady(vanillaList));
                Runner.Require(vanillaList.Items.Count == vanillaRows, "The vanilla hotkey rows changed");
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

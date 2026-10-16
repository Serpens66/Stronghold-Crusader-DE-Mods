using APIShared.Commands;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using System;
using System.Collections.Generic;

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private static HUD_ExtremePowers powerToolbar;
        private static Grid powerToolbarRoot;
        private static UIElement powerToolbarHost;
        private static FrameworkElement powerMeter;
        private static TextBlock powerToolbarLabel;
        private static Canvas powerToolbarNote;
        private static readonly Button[] vanillaPowerButtons = new Button[8];
        private static readonly List<ToolbarProperty> toolbarProperties = new List<ToolbarProperty>();
        private static bool powerToolbarOwned;
        private static DateTime nextToolbarSearch;

        // Preserve bindings themselves: restoring a sampled bool would permanently detach Vanilla updates.
        private sealed class ToolbarProperty
        {
            internal DependencyObject Target;
            internal DependencyProperty Property;
            internal BindingBase Binding;
            internal object Local;
            internal ToolbarProperty(DependencyObject target, DependencyProperty property)
            {
                Target = target; Property = property;
                Binding = BindingOperations.GetBindingBase(target, property);
                Local = target.ReadLocalValue(property);
            }
            internal void Restore()
            {
                if (Binding != null) BindingOperations.SetBinding(Target, Property, Binding);
                else if (Local == DependencyProperty.UnsetValue) Target.ClearValue(Property);
                else Target.SetValue(Property, Local);
            }
        }

        private sealed class ToolbarRequest
        {
            internal long Session, Generation;
            internal int Actor, Power;
        }

        private static bool HasPowerTestContext() => toolsModel != null && IsToolsSession() &&
            IsSelectableAi(humanSelectedOwner) && GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled();

        private static void RegisterVanillaPowerActions()
        {
            if (!GameActionEvents.TryRegister(Guid, "SelectedAiExtremePower", args => {
                if (!log.IsMainThread || args.Command != Enums.GameActionCommand.ExtremePower || args.SkipOriginalFunction ||
                    !HasPowerTestContext() || (uint)args.StructureId >= 8) return;
                args.State = new ToolbarRequest { Session = CurrentSessionId, Generation = powerGeneration, Actor = humanSelectedOwner, Power = args.StructureId };
                args.SkipOriginalFunction = true;
            }, args => {
                if (!(args.State is ToolbarRequest request)) return;
                // Never run UI/native actions recursively inside the shared GameAction publisher.
                presentationCallbacks.Enqueue(() => {
                    if (!IsCurrentSession(request.Session) || !ToolPolicy.PowerContextMatches(request.Session, CurrentSessionId,
                        request.Actor, humanSelectedOwner, request.Generation, powerGeneration, HasPowerTestContext())) return;
                    SelectPower(request.Power);
                });
            }, out string reason)) throw new InvalidOperationException("Extreme power command registration failed: " + reason);
        }

        private static T FindVisual<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T found) return found;
            if (root == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                T child = FindVisual<T>(VisualTreeHelper.GetChild(root, i));
                if (child != null) return child;
            }
            return null;
        }

        private static void CollectPowerButtons(DependencyObject root)
        {
            if (root is FrameworkElement element && BindingOperations.GetBinding(element, FrameworkElement.HeightProperty)?.Path?.Path == "ExtremePoints")
                powerMeter = element;
            if (root is Button button && int.TryParse(button.CommandParameter as string, out int id) && (uint)id < 8)
                vanillaPowerButtons[id] = button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) CollectPowerButtons(VisualTreeHelper.GetChild(root, i));
        }

        private static void RefreshVanillaPowerToolbar()
        {
            bool active = HasPowerTestContext();
            if (!active) { RestoreVanillaPowerToolbar(false); return; }
            if (powerToolbar == null)
            {
                if (DateTime.UtcNow < nextToolbarSearch) return;
                nextToolbarSearch = DateTime.UtcNow.AddMilliseconds(250);
                powerToolbar = FindVisual<HUD_ExtremePowers>(humanScreen);
                if (powerToolbar == null) return;
                powerToolbarRoot = powerToolbar.Content as Grid;
                DependencyObject parent = powerToolbar;
                while (parent != null && !(parent is Viewbox)) parent = VisualTreeHelper.GetParent(parent);
                powerToolbarHost = parent as UIElement;
                CollectPowerButtons(powerToolbar);
                for (int i = 0; i < 8; i++)
                    if (vanillaPowerButtons[i] == null || powerToolbarRoot == null || powerToolbarHost == null)
                    { RestoreVanillaPowerToolbar(true); LogOnce("TEST_POWER_TOOLBAR_UNAVAILABLE: Vanilla toolbar contract changed."); return; }
                powerToolbarLabel = new TextBlock { Width = 180, FontSize = 14, Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Right,
                    IsHitTestVisible = false };
                // Zero-size overlay leaves the Vanilla Viewbox's measure/scale and hit area intact.
                powerToolbarNote = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 48, 0), IsHitTestVisible = false };
                Canvas.SetLeft(powerToolbarLabel, -180);
                powerToolbarNote.Children.Add(powerToolbarLabel);
                powerToolbarRoot.Children.Add(powerToolbarNote);
                log.LogInfo("TEST_POWER_TOOLBAR_READY: eight Vanilla buttons connected; explicit AI actor and free test actions.");
            }
            if (!powerToolbarOwned)
            {
                toolbarProperties.Add(new ToolbarProperty(powerToolbarHost, UIElement.VisibilityProperty));
                if (powerMeter != null) toolbarProperties.Add(new ToolbarProperty(powerMeter, FrameworkElement.HeightProperty));
                foreach (Button button in vanillaPowerButtons) toolbarProperties.Add(new ToolbarProperty(button, UIElement.IsEnabledProperty));
                powerToolbarOwned = true;
            }
            powerToolbarHost.Visibility = MainViewModel.Instance.UIVisible && !MainViewModel.Instance.Show_HUD_Briefing
                ? Visibility.Visible : Visibility.Collapsed;
            if (powerMeter != null) powerMeter.Height = 236;
            powerToolbarLabel.Visibility = Visibility.Visible;
            powerToolbarLabel.Text = SerpLocalization.Get("Tools.PowerToolbarContext", "Player", PlayerLabel(humanSelectedOwner));
            if (pendingPower != null) powerToolbarLabel.Text += "\n" + SerpLocalization.Get("Tools.PowerChooseTarget");
            for (int id = 0; id < 8; id++) vanillaPowerButtons[id].IsEnabled = toolsModel.Powers[id].Enabled;
        }

        private static void RestoreVanillaPowerToolbar(bool detach)
        {
            if (powerToolbarOwned)
            {
                foreach (ToolbarProperty saved in toolbarProperties) saved.Restore();
                toolbarProperties.Clear();
                powerToolbarOwned = false;
            }
            if (powerToolbarLabel != null) powerToolbarLabel.Visibility = Visibility.Collapsed;
            if (!detach) return;
            if (powerToolbarNote != null) powerToolbarRoot?.Children.Remove(powerToolbarNote);
            powerToolbarNote = null; powerToolbarLabel = null; powerToolbarRoot = null; powerToolbar = null; powerToolbarHost = null; powerMeter = null;
            Array.Clear(vanillaPowerButtons, 0, vanillaPowerButtons.Length);
            nextToolbarSearch = DateTime.MinValue;
        }
    }
}

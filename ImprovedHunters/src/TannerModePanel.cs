using APIShared.Networking;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// The slider of the Tannery panel: how many hides of a cow become meat instead of the workshop's own
    /// good. APIShared's building panel sections show it only for the local player's Tanneries; it reads
    /// the shared table and changes it only through APIShared's synchronized request.
    /// </summary>
    internal sealed class TannerModePanel
    {
        internal const string PanelName = "ImprovedHuntersTannerPanel";
        internal const string ControlsName = "ImprovedHuntersTannerControls";
        internal const string SliderName = "ImprovedHuntersTannerSlider";
        internal const string MeatRunsName = "ImprovedHuntersTannerMeatRuns";
        internal const string GoodRunsName = "ImprovedHuntersTannerGoodRuns";

        // Above the place of Vanilla's workshop product buttons (380,0,0,28; 114x57), which stays free for
        // mods that add goods to the Tannery. The section host is laid out like Vanilla's 800x170 panels.
        private const string Layout =
            "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Name=\"" + PanelName + "\">" +
            "<StackPanel Name=\"" + ControlsName + "\" Height=\"32\" Margin=\"270,0,0,87\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Bottom\" Orientation=\"Horizontal\">" +
            "<Image Width=\"32\" Height=\"32\" VerticalAlignment=\"Center\" />" +
            "<TextBlock Name=\"" + MeatRunsName + "\" Width=\"18\" VerticalAlignment=\"Center\" FontSize=\"18\" FontWeight=\"Bold\" Foreground=\"#FF3A2412\" Text=\"0\" TextAlignment=\"Center\" />" +
            "<Slider Name=\"" + SliderName + "\" Width=\"120\" Margin=\"4,0\" VerticalAlignment=\"Center\" Focusable=\"False\" LargeChange=\"1\" IsSnapToTickEnabled=\"True\" Maximum=\"3\" Minimum=\"0\" TickFrequency=\"1\" Value=\"3\" />" +
            "<TextBlock Name=\"" + GoodRunsName + "\" Width=\"18\" VerticalAlignment=\"Center\" FontSize=\"18\" FontWeight=\"Bold\" Foreground=\"#FF3A2412\" Text=\"3\" TextAlignment=\"Center\" />" +
            "<Image Width=\"32\" Height=\"32\" VerticalAlignment=\"Center\" />" +
            "</StackPanel></Grid>";

        private readonly ManualLogSource log;
        private readonly Func<bool> isEnabled;
        private readonly TannerMeatState state;
        private Slider slider;
        private TextBlock meatRuns;
        private TextBlock goodRuns;
        private int shownBuilding;
        private uint shownGlobalId;
        private int shownTableRuns;
        private bool syncingSlider;
        private bool resyncSlider;

        private TannerModePanel(ManualLogSource log, Func<bool> isEnabled, TannerMeatState state)
        {
            this.log = log;
            this.isEnabled = isEnabled;
            this.state = state;
        }

        internal static TannerModePanel Install(ManualLogSource log, Func<bool> isEnabled, TannerMeatState state)
        {
            var created = new TannerModePanel(log, isEnabled, state);
            var options = new BuildingPanelSectionOptions(
                ImprovedHuntersPlugin.PluginGuid,
                "tanner-meat",
                new[] { eStructs.STRUCT_TANNERS_WORKSHOP },
                created.Create)
            {
                ShowWhen = context => context.IsOwnedByLocalPlayer && created.isEnabled(),
                Opened = created.OnOpened,
                Updated = created.OnUpdated,
                Closed = created.OnClosed
            };
            if (!BuildingPanelSections.TryRegister(options, out _, out string reason))
                throw new InvalidOperationException(reason);

            return created;
        }

        // Once per building HUD; the controls of an earlier HUD are released by APIShared.
        private FrameworkElement Create(BuildingPanelSectionFactoryContext context)
        {
            var panel = (Grid)GUI.ParseXaml(Layout);
            // Taken by position from the layout above, independent of XAML name scopes.
            var controls = (StackPanel)panel.Children[0];
            ((Image)controls.Children[0]).Source = Sprite(context, "UISprites I017");
            meatRuns = (TextBlock)controls.Children[1];
            slider = (Slider)controls.Children[2];
            goodRuns = (TextBlock)controls.Children[3];
            ((Image)controls.Children[4]).Source = Sprite(context, "UISprites I039");
            shownBuilding = 0;
            slider.ValueChanged += (sender, args) => OnSliderChanged();
            // Vanilla's hover text location of the building panel, as for its own building buttons.
            panel.MouseEnter += (sender, args) => MainViewModel.Instance.HUDmain.HoverString = SerpLocalization.Get("ImprovedHunters.TannerSliderHover");
            panel.MouseLeave += (sender, args) => MainViewModel.Instance.HUDmain.HoverString = "";
            return panel;
        }

        private static ImageSource Sprite(BuildingPanelSectionFactoryContext context, string key) =>
            context.Host.TryFindResource(key) as ImageSource ??
            throw new InvalidOperationException("The Tannery panel icon is missing: " + key);

        private void OnOpened(BuildingPanelSectionEventArgs args)
        {
            shownBuilding = args.Context.BuildingId;
            shownGlobalId = args.Context.GlobalId;
            ShowTable(state.GetMeatRuns(shownBuilding, shownGlobalId));
        }

        // Follows the table, not every frame. A change reaches the table only when its Chore runs, some
        // ticks later and after any earlier ones, so until the last one has arrived the slider must not
        // jump back, nor under the player's hand.
        private void OnUpdated(BuildingPanelSectionEventArgs args)
        {
            int tableRuns = state.GetMeatRuns(shownBuilding, shownGlobalId);
            if (resyncSlider || (!state.HasPendingChange && tableRuns != shownTableRuns && !slider.IsMouseCaptureWithin))
                ShowTable(tableRuns);
        }

        private void OnClosed(BuildingPanelSectionEventArgs args)
        {
            shownBuilding = 0;
            state.ForgetPendingChange();
        }

        private void ShowTable(int tableRuns)
        {
            resyncSlider = false;
            shownTableRuns = tableRuns;
            syncingSlider = true;
            slider.Value = TannerMeatPolicy.SliderValueFromMeatRuns(tableRuns);
            syncingSlider = false;
            ShowRuns();
        }

        private void OnSliderChanged()
        {
            if (syncingSlider || shownBuilding == 0)
                return;

            ShowRuns();
            bool allOfSameType = (slider.Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            NetworkSendResult result = state.RequestMeatRuns(shownBuilding, shownGlobalId, CurrentMeatRuns, allOfSameType);
            if (result?.Submitted == true)
                return;

            Shared.DebugLogHelper.LogWarning(log, $"Improved Hunters could not send the Tannery ratio: {result?.Reason ?? "invalid value"}");
            resyncSlider = true; // Shows the table's value again on the next check.
        }

        private int CurrentMeatRuns => TannerMeatPolicy.MeatRunsFromSliderValue((int)Math.Round(slider.Value));

        private void ShowRuns()
        {
            int meat = CurrentMeatRuns;
            meatRuns.Text = meat.ToString();
            goodRuns.Text = (TannerMeatPolicy.HidesPerCow - meat).ToString();
        }
    }
}

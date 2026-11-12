using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// The slider of the Tannery panel: how many hides of a cow become meat instead of the
    /// workshop's own good. It reads the shared table and changes it only through the Chore.
    /// </summary>
    internal sealed class TannerModePanel
    {
        internal const string PanelName = "ImprovedHuntersTannerPanel";
        internal const string SliderName = "ImprovedHuntersTannerSlider";
        internal const string MeatRunsName = "ImprovedHuntersTannerMeatRuns";
        internal const string GoodRunsName = "ImprovedHuntersTannerGoodRuns";

        private readonly ManualLogSource log;
        private readonly Func<bool> isEnabled;
        private readonly TannerRunTable table;
        private readonly TannerRatioSync sync;
        private HUD_Buildings observedHud;
        private Grid panel;
        private Slider slider;
        private TextBlock meatRuns;
        private TextBlock goodRuns;
        private int shownBuilding;
        private uint shownGlobalId;
        private int shownTableRuns;
        private bool syncingSlider;
        private bool failureLogged;

        private TannerModePanel(ManualLogSource log, Func<bool> isEnabled, TannerRunTable table, TannerRatioSync sync)
        {
            this.log = log;
            this.isEnabled = isEnabled;
            this.table = table;
            this.sync = sync;
        }

        internal static TannerModePanel Install(ManualLogSource log, Func<bool> isEnabled, TannerRunTable table, TannerRatioSync sync)
        {
            var created = new TannerModePanel(log, isEnabled, table, sync);
            if (!APIShared.Presentation.PresentationEvents.TryRegister(
                APIShared.Presentation.PresentationOperation.GuiChecks,
                ImprovedHuntersPlugin.PluginGuid,
                "TannerModePanel",
                null,
                args =>
                {
                    if (args.OriginalCompleted)
                        created.OnGuiChecks();
                },
                out string reason))
            {
                throw new InvalidOperationException(reason);
            }

            return created;
        }

        private void OnGuiChecks()
        {
            try
            {
                Update();
            }
            catch (Exception exception)
            {
                if (failureLogged)
                    return;

                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters Tannery panel update failed: {exception}");
            }
        }

        private unsafe void Update()
        {
            if (!MainViewModel.viewModelLoaded)
                return;

            MainViewModel view = MainViewModel.Instance;
            if (view?.HUDmain == null || view.HUDBuildingPanel == null)
                return;

            if (!ReferenceEquals(view.HUDBuildingPanel, observedHud))
                Bind(view.HUDBuildingPanel);
            if (panel == null)
                return;

            int building = 0;
            uint globalId = 0;
            if (isEnabled() && view.Show_HUD_Building)
            {
                EngineInterface.PlayState state = GameData.Instance.lastGameState;
                if (state != null &&
                    state.app_sub_mode == (int)Enums.InBuildingModes.INSIDE_TANNERS_WORKSHOP &&
                    TannerRunTable.IsValidId(state.in_structure) &&
                    global::APIShared.BuildingAccess.TryGetById(state.in_structure, out GameBuilding* record, out _) &&
                    record != null &&
                    record->r_BuildingType == eStructs.STRUCT_TANNERS_WORKSHOP &&
                    record->r_PlayerIdOwner == APIShared.PlayerPerspectiveAPI.GetControlledPlayerId())
                {
                    building = state.in_structure;
                    globalId = record->r_GlobalId;
                }
            }

            if (building == 0)
            {
                shownBuilding = 0;
                sync.ForgetPendingChange();
                if (panel.Visibility != Visibility.Hidden)
                    panel.Visibility = Visibility.Hidden;
                return;
            }

            // Follows the building and the table, not every frame. A change reaches the table only when
            // its Chore runs, some ticks later and after any earlier ones, so until the last one has
            // arrived the slider must not jump back, nor under the player's hand.
            int tableRuns = table.GetMeatRuns(building, globalId);
            bool otherBuilding = shownBuilding != building || shownGlobalId != globalId;
            bool tableMoved = !sync.HasPendingChange && tableRuns != shownTableRuns && !slider.IsMouseCaptureWithin;
            if (otherBuilding || tableMoved)
            {
                shownBuilding = building;
                shownGlobalId = globalId;
                shownTableRuns = tableRuns;
                syncingSlider = true;
                slider.Value = TannerMeatPolicy.SliderValueFromMeatRuns(tableRuns);
                syncingSlider = false;
                ShowRuns();
            }

            if (panel.Visibility != Visibility.Visible)
                panel.Visibility = Visibility.Visible;
        }

        private void Bind(HUD_Buildings hud)
        {
            observedHud = hud;
            shownBuilding = 0;
            panel = hud.FindName(PanelName) as Grid;
            slider = hud.FindName(SliderName) as Slider;
            meatRuns = hud.FindName(MeatRunsName) as TextBlock;
            goodRuns = hud.FindName(GoodRunsName) as TextBlock;
            if (panel == null || slider == null || meatRuns == null || goodRuns == null)
            {
                panel = null;
                if (!failureLogged)
                {
                    failureLogged = true;
                    Shared.DebugLogHelper.LogError(log, "Improved Hunters Tannery panel is missing from HUD_Buildings.");
                }

                return;
            }

            slider.ValueChanged += (sender, args) => OnSliderChanged();
            // Vanilla's hover text location of the building panel, as for its own building buttons.
            panel.MouseEnter += (sender, args) => MainViewModel.Instance.HUDmain.HoverString = SerpLocalization.Get("ImprovedHunters.TannerSliderHover");
            panel.MouseLeave += (sender, args) => MainViewModel.Instance.HUDmain.HoverString = "";
        }

        private void OnSliderChanged()
        {
            if (syncingSlider || shownBuilding == 0)
                return;

            ShowRuns();
            if (!sync.Send(shownBuilding, shownGlobalId, CurrentMeatRuns))
                shownBuilding = 0; // Shows the table's value again on the next check.
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

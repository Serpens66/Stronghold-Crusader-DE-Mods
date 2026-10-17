using APIShared.Presentation;
using CrusaderDE;
using System;
using System.Collections.Generic;

namespace PopularityTest
{
    internal sealed class PopularityPresentation
    {
        private readonly Func<KeyValuePair<long, IReadOnlyDictionary<string, int>>> capture;
        private readonly Action<string> info, error;
        private long loggedSession;
        private int failures;
        internal PopularityPresentation(Func<KeyValuePair<long, IReadOnlyDictionary<string, int>>> capture, Action<string> info, Action<string> error)
        { this.capture = capture; this.info = info; this.error = error; }
        internal void Register()
        {
            if (!PresentationEvents.TryRegister(PresentationOperation.GuiChecks, Plugin.Guid, "popularity-report", null, Presented, out string reason))
                throw new InvalidOperationException("Popularity presentation unavailable: " + reason);
        }
        private void Presented(PresentationPostEventArgs args)
        {
            if (!args.OriginalCompleted || !MainViewModel.viewModelLoaded) return;
            try
            {
                var active = capture();
                if (active.Key == 0 || active.Value == null || active.Value.Count == 0) return;
                var vm = MainViewModel.Instance;
                var state = GameData.Instance?.lastGameState;
                if (vm.HUDmain == null || vm.HUDBuildingPanel == null || state == null) return;
                var original = Capture(state);
                var corrected = original.Correct(active.Value);
                Apply(vm, state, original, corrected);
                if (loggedSession != active.Key)
                {
                    loggedSession = active.Key;
                    info("POPULARITY_DISPLAY session=" + active.Key + "; original=" + original.Total + "; corrected=" + corrected.Total +
                        "; activeOverrides=" + active.Value.Count + "; GUI Post confirmed; units=internal/25");
                }
            }
            catch (Exception ex)
            {
                failures++;
                if ((failures & (failures - 1)) == 0) error("POPULARITY_DISPLAY_FAILED count=" + failures + "; " + ex);
            }
        }
        internal static PopularityReport Capture(EngineInterface.PlayState s) => new PopularityReport {
            Population=s.population, FoodAvailable=s.food_types_available, FoodTypes=s.food_types_eaten,
            RationMode=s.rationing, TaxMode=s.tax_rate, Gold=s.gold, BlessedPercent=s.blessed_percent,
            AlePercent=s.inn_coverage_percent, FearLevel=s.fear_factor,
            Rations=s.rationing_popularity, Variety=s.foodsEaten_popularity, Food=s.food_popularity,
            Tax=s.tax_popularity, Crowding=s.overcrowding_popularity, Religion=s.religion_popularity,
            Buildings=s.church_adjustment, Ale=s.inn_coverage_popularity, Fear=s.fearFactor_popularity,
            Fairs=s.fairs_popularity, Total=s.upcoming_total_popularity,
            Events=s.fairs_popularity+s.plague_popularity+s.wolves_popularity+s.bandits_popularity+s.fire_popularity+s.marriage_popularity+s.jester_popularity
        };
        private static string Points(int value) => (value / PopularityCatalog.UnitsPerPoint).ToString();
        private static void Apply(MainViewModel vm, EngineInterface.PlayState s, PopularityReport old, PopularityReport value)
        {
            if (value.Total != old.Total)
            {
                vm.PopularityIncreasingVis = value.Total > 0;
                vm.PopularityDecreasingVis = value.Total < 0;
            }
            // Audited Vanilla app/sub-mode wire values (0x19D960 and FatControler); SE has no enum for these panels.
            if (s.app_mode != 16) return;
            switch (s.app_sub_mode)
            {
                case 4: // Granary, including uncommitted ration preview.
                    if (value.Rations != old.Rations) vm.InGranaryRationsPopText = Points(value.Rations);
                    if (value.Variety != old.Variety && s.food_types_eaten > 1) vm.InGranaryTypesPopFoodText = Points(value.Variety);
                    break;
                case 2: if (value.Tax != old.Tax) vm.InKeepTaxPopText = Points(value.Tax); break;
                case 3: if (value.Ale != old.Ale) vm.InInnPopularityText = Points(value.Ale); break;
                case 69: case 72:
                    if (value.Food != old.Food) vm.PopReportFoodText = Points(value.Food);
                    if (value.Tax != old.Tax) vm.PopReportTaxText = Points(value.Tax);
                    if (value.Crowding != old.Crowding) vm.PopReportCrowdingText = Points(value.Crowding);
                    if (value.Fear != old.Fear) vm.PopReportFearFactorText = Points(value.Fear);
                    if (value.Religion + value.Buildings != old.Religion + old.Buildings) vm.PopReportReligionText = Points(value.Religion + value.Buildings);
                    if (value.Ale != old.Ale) vm.PopReportAleText = Points(value.Ale);
                    if (value.Fairs != old.Fairs) vm.PopReportFairsText = Points(value.Fairs);
                    if (value.Events != old.Events) vm.PopReportEventsText = Points(value.Events);
                    if (value.Events != old.Events && s.app_sub_mode == 72)
                        vm.HUDBuildingPanel.RefShowEventsButton.Visibility = value.Events == 0 ? Noesis.Visibility.Hidden : Noesis.Visibility.Visible;
                    if (value.Total != old.Total) vm.PopReportTotalText = Points(value.Total);
                    break;
                case 73: if (value.Fear != old.Fear) vm.FFReportFearFactorText = Points(value.Fear); break;
                case 79:
                    if (value.Religion != old.Religion) vm.RelReportPopEffectText = Points(s.blessed_popularity + value.Religion - old.Religion);
                    // Vanilla already selected the localized building label/visibility using its identity marker.
                    if (value.Buildings != old.Buildings) vm.RelReportDemandEffectText = Points(value.Buildings);
                    break;
            }
        }
    }
}

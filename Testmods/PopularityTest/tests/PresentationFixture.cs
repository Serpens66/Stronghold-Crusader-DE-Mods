using System;
using System.Collections.Generic;
using System.Linq;
using PopularityTest;

// Stand-ins only at engine/API boundaries; the production adapter and calculator run unchanged.
// RuntimeSignatures checks those same production sources against the actual installed dependencies.
namespace APIShared.Presentation
{
    public enum PresentationOperation { GuiChecks }
    public sealed class PresentationPostEventArgs { public bool OriginalCompleted; }
    public static class PresentationEvents
    {
        public static Action<PresentationPostEventArgs> Callback;
        public static bool TryRegister(PresentationOperation op, string owner, string id, object pre,
            Action<PresentationPostEventArgs> post, out string reason) { Callback=post; reason=null; return true; }
    }
}
namespace Noesis { public enum Visibility { Visible, Hidden } public class Grid { public Visibility Visibility; } }
namespace CrusaderDE
{
    public class Panel { public Noesis.Grid RefShowEventsButton=new Noesis.Grid(); }
    public class MainViewModel
    {
        public static bool viewModelLoaded;
        public static MainViewModel Instance=new MainViewModel();
        public object HUDmain=new object(); public Panel HUDBuildingPanel=new Panel();
        public bool PopularityIncreasingVis, PopularityDecreasingVis;
        public string InGranaryRationsPopText, InGranaryTypesPopFoodText, InKeepTaxPopText, InInnPopularityText;
        public string PopReportFoodText, PopReportTaxText, PopReportCrowdingText, PopReportFearFactorText, PopReportReligionText;
        public string PopReportAleText, PopReportFairsText, PopReportEventsText, PopReportTotalText;
        public string FFReportFearFactorText, RelReportPopEffectText, RelReportDemandEffectText;
    }
}
public class GameData { public static GameData Instance=new GameData(); public EngineInterface.PlayState lastGameState; }
public static class EngineInterface
{
    public class PlayState
    {
        public int population, food_types_available, food_types_eaten, rationing, tax_rate, gold, blessed_percent, inn_coverage_percent, fear_factor;
        public int rationing_popularity, foodsEaten_popularity, food_popularity, tax_popularity, overcrowding_popularity, religion_popularity;
        public int church_adjustment, inn_coverage_popularity, fearFactor_popularity, fairs_popularity, upcoming_total_popularity;
        public int plague_popularity, wolves_popularity, bandits_popularity, fire_popularity, marriage_popularity, jester_popularity, blessed_popularity;
        public int app_mode, app_sub_mode;
    }
}
namespace PopularityTest { internal static class Plugin { internal const string Guid="PopularityTest_Serp"; } internal static class PopularityCatalog { internal const int UnitsPerPoint=25; } }
internal static class PresentationFixture
{
    internal static void Run(Action<bool,string> check)
    {
        long session=1; int logs=0, errors=0;
        var active=new Dictionary<string,int>{{"ChurchPopModifier",325},{"CathedralPopModifier",350},{"RationsPopModifierExtra",50},{"FairsPopularityEventModifier",0}};
        var presenter=new PopularityPresentation(()=>new KeyValuePair<long,IReadOnlyDictionary<string,int>>(session,active), _=>logs++, _=>errors++);
        presenter.Register();
        var state=new EngineInterface.PlayState { population=20, food_types_available=1, rationing=3, tax_rate=3, gold=100,
            rationing_popularity=100, food_popularity=100, church_adjustment=75, fairs_popularity=400, plague_popularity=-400,
            upcoming_total_popularity=175, app_mode=16, app_sub_mode=72 };
        GameData.Instance.lastGameState=state;
        var fields=typeof(EngineInterface.PlayState).GetFields(); var before=fields.Select(f=>f.GetValue(state)).ToArray();
        CrusaderDE.MainViewModel.viewModelLoaded=true; var vm=CrusaderDE.MainViewModel.Instance;
        Action<bool> invoke=done=>APIShared.Presentation.PresentationEvents.Callback(new APIShared.Presentation.PresentationPostEventArgs{OriginalCompleted=done});
        vm.PopReportTotalText="foreign"; invoke(false); check(vm.PopReportTotalText=="foreign","skipped original unchanged");
        invoke(true);
        check(vm.PopReportReligionText=="27" && vm.PopReportFoodText=="2" && vm.PopReportEventsText=="-16" && vm.PopReportTotalText=="13","production adapter totals and conversion");
        check(vm.HUDBuildingPanel.RefShowEventsButton.Visibility==Noesis.Visibility.Visible,"event button visibility");
        check(vm.PopularityIncreasingVis && !vm.PopularityDecreasingVis,"positive direction");
        for(int i=0;i<fields.Length;i++) check(Equals(before[i],fields[i].GetValue(state)),"PlayState immutable "+fields[i].Name);
        invoke(true); check(vm.PopReportTotalText=="13" && logs==1,"repeat and one marker per session");
        state.app_sub_mode=79; invoke(true); check(vm.RelReportDemandEffectText=="27","religion direct int output");
        state.app_sub_mode=4; invoke(true); check(vm.InGranaryRationsPopText=="2","granary preview");
        active["ChurchPopModifier"]=-400; active["CathedralPopModifier"]=-400; invoke(true);
        check(vm.PopularityDecreasingVis && !vm.PopularityIncreasingVis,"negative direction");
        state.church_adjustment=0; state.fairs_popularity=0; state.plague_popularity=0; state.upcoming_total_popularity=50;
        invoke(true); check(!vm.PopularityIncreasingVis && !vm.PopularityDecreasingVis,"neutral direction after perspective change");
        session=0; vm.InGranaryRationsPopText="vanilla"; invoke(true); check(vm.InGranaryRationsPopText=="vanilla","session ended gate");
        session=2; active.Clear(); invoke(true); check(vm.InGranaryRationsPopText=="vanilla","disabled no writes");
        active["RationsPopModifierExtra"]=75; vm.HUDmain=null; invoke(true); check(vm.InGranaryRationsPopText=="vanilla","missing HUD gate");
        vm.HUDmain=new object(); invoke(true); check(vm.InGranaryRationsPopText=="3" && logs==2,"new session recreated HUD");
        check(errors==0,"no presentation errors");
    }
}

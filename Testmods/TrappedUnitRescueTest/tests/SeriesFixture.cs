using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared.Automation;

namespace TrappedUnitRescueTest.Tests
{
    internal static class SeriesFixture
    {
        private static int checks;
        private static void Check(bool value,string why){checks++;if(!value)throw new Exception("Series fixture: "+why);}
        private static T Read<T>(AutomationStep step,string name)=>(T)typeof(AutomationStep).GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(step,null);
        private static IEnumerable<AutomationWait> Invoke(Action action){action();yield return AutomationWait.NextFrame;}
        internal static void Run()
        {
            checks=0;Func<string,bool> owns=s=>s=="rescue-off" || s=="rescue-on";
            Check(RescueScenarioSelection.TryParse("rescue-off, rescue-on,rescue-off",owns,out var selection) && selection.SequenceEqual(new[]{"rescue-off","rescue-on","rescue-off"}),"selection preserves order and explicit repetitions");
            foreach(string bad in new[]{null,""," ","rescue-off,","rescue-unknown","rescue-off\nrescue-on",",rescue-on","RESCUE-ON"})
                Check(!RescueScenarioSelection.TryParse(bad,owns,out _),"invalid selection rejected before any case creation");
            Check(!RescueScenarioSelection.TryParse(string.Join(",",Enumerable.Repeat("rescue-on",65)),owns,out _),"bounded selection rejects65 cases");
            Check(RescueScenarioSelection.TryParse(string.Join(",",Enumerable.Repeat("rescue-on",64)),owns,out var maximum) && maximum.Length==64,"maximum explicit series accepted");
            int execution=0;
            var original=new AutomationStep("probe",TimeSpan.FromSeconds(23),run=>Invoke(()=>execution++),true);
            var wrapped=RescueSeriesStepMetadata.Prefix(original,"s01-rescue-on-");
            Check(Read<string>(wrapped,"Name")=="s01-rescue-on-probe" && Read<TimeSpan>(wrapped,"Timeout")==TimeSpan.FromSeconds(23) && Read<bool>(wrapped,"MissionBound"),"actual SDK metadata clone retains timeout and mission binding");
            Check(execution==0,"enumeration/planning does not execute a scenario");
            using(var iterator=Read<Func<AutomationRun,IEnumerable<AutomationWait>>>(wrapped,"Execute")(null).GetEnumerator())
                Check(iterator.MoveNext() && ReferenceEquals(iterator.Current,AutomationWait.NextFrame) && execution==1,"wrapped step executes the original delegate and wait");
            int stopped=0,selected=0;
            var cases=new[]{new RescueSeriesCase("rescue-on",new[]{original},()=>selected++,()=>stopped++),new RescueSeriesCase("rescue-on",new[]{original},()=>selected++,()=>stopped++)};
            var series=new RescueScenarioSeries(cases,_=>{});cases[0]=null;
            var steps=series.Steps().ToArray();var names=steps.Select(s=>Read<string>(s,"Name")).ToArray();
            Check(steps.Length==6 && names.Distinct(StringComparer.Ordinal).Count()==6 && names[0]=="s01-rescue-on-select" && names[3]=="s02-rescue-on-select","repeated scenarios use unique indexed step IDs and independent case slots");
            Check(!Read<bool>(steps[0],"MissionBound") && !Read<bool>(steps[2],"MissionBound") && Read<bool>(steps[1],"MissionBound"),"selection/menu transitions unbound; actual mission steps retain binding");
            Check(selected==0 && stopped==0,"step materialization has no lifecycle or setting side effects");
            series.MissionEnded(123);Check(stopped==0,"unrelated mission end cannot stop a future case");
            series.Stop();Check(stopped==2,"completion stops every retained case");
            Console.WriteLine("SeriesFixture: "+checks+" checks passed; actual SDK step metadata, no Unity/game calls");
        }
    }
}

#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Threading;
using APIShared.Statistics;
using Noesis;

namespace LordOverviewTest {
 // Fixed diagnostic scenario for repeatable PerfProbe runs; no release code/config switch.
 internal static partial class OverviewRuntime {
  private static long perfSession;
  private static double perfStart;
  private static int perfPhase=-1,perfShotPhase=-1;
  private static string perfOriginalColumns,perfShotPrefix;
  private static OverviewDesign perfOriginalDesign;
  private static LordStatisticColumn perfProvider;
  private static readonly IReadOnlyDictionary<int,decimal?> perfValues=new Dictionary<int,decimal?> {{1,1},{2,2},{3,3},{4,4},{5,5},{6,6},{7,7},{8,8}};
  static partial void DiagOverviewGui(){
   long id=Interlocked.Read(ref session);
   if(id<=0 || main?.Show_InGameUI!=true || sourceHost?.IsLoaded!=true || panel?.IsLoaded!=true || !mode.HasValue)return;
   double now=UnityEngine.Time.realtimeSinceStartup;
   if(id!=perfSession){
    perfSession=id;perfStart=now;perfPhase=perfShotPhase=-1;perfOriginalColumns=settings.DisplayColumns;perfOriginalDesign=settings.DisplayDesign;
    perfShotPrefix="lord-"+DateTime.Now.ToString("HHmmss");
   }
   int phase=Math.Min(5,(int)((now-perfStart)/18));
   if(phase!=perfPhase){
    perfPhase=phase;
    Shared.DevDiag.Log(()=>"overview.scenario phase="+phase+" session="+id+" players="+rows.Count+" allocationCounter="+(OverviewAllocationProbe.Read()>=0));
    if(phase==0 || phase==5){
     Close();
     if(phase==5){settings.SetDisplayColumns(perfOriginalColumns);settings.SetDisplayDesign(perfOriginalDesign);}
    }else{
     CloseSettingsMenu();
     settings.SetDisplayDesign(phase==1?OverviewDesign.List:phase==2?OverviewDesign.SplitList:OverviewDesign.Cards);
     if(phase==4){
      if(perfProvider==null && !LordStatistics.TryRegister("LordOverviewTest_Serp",new LordStatisticColumnDefinition("performance-fixture",()=>"Fixture",()=>"Diagnostic constant values",()=>null,request=>perfValues),out perfProvider,out string reason))throw new InvalidOperationException(reason);
      settings.SetDisplayColumns(Metrics.Serialize(Metrics.Builtins.SelectMetricIds())+","+perfProvider.Key);
     }else settings.SetDisplayColumns(Metrics.Serialize(Metrics.Defaults));
     selection.Load(settings.DisplayColumns);
     if(panel.IsVisible){Render();RequestRefresh();}else OpenOverview();
     if(phase==2)OpenSettingsMenu();
    }
   }
   if(phase>0 && phase<5 && perfShotPhase!=phase && now-perfStart-phase*18>4 && rowVisuals.Count>0){
    perfShotPhase=phase;
    var folder=System.IO.Path.Combine(BepInEx.Paths.GameRootPath,"BepInEx","PerfProbe_shots");System.IO.Directory.CreateDirectory(folder);
    UnityEngine.ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder,perfShotPrefix+"-"+phase+".png"));
    if(phase==3)foreach(var pair in rowVisuals){
     var view=pair.Value;if(!hoverTargets.TryGetValue("view/player/"+pair.Key,out var target) || !target.Element.IsVisible)continue;
     var center=target.Element.PointToScreen(new Point(target.Element.ActualWidth/2,target.Element.ActualHeight/2));var root=inputRoot as Visual;
     var before=VisualTreeHelper.HitTest(root,root.PointFromScreen(center)).VisualHit;
     target.Tip.IsOpen=true;var after=VisualTreeHelper.HitTest(root,root.PointFromScreen(center)).VisualHit;
     bool passive=!target.Element.IsHitTestVisible && !target.Tip.IsHitTestVisible && ReferenceEquals(before,after);
     target.Tip.IsOpen=false;
     Shared.DevDiag.Log(()=>"overview.portrait probe passive="+passive+" name="+view.FullName+" hit="+(before==null?"none":before.GetType().Name));break;
    }
   }
  }
 }
 internal static class PerfMetricIds {
  internal static IEnumerable<Metric> SelectMetricIds(this MetricDefinition[] definitions){foreach(var definition in definitions)yield return definition.Id;}
 }
 internal static class OverviewAllocationProbe {
  private static readonly Func<long> read=CreateReader();
  private static Func<long> CreateReader(){var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);return method==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);}
  internal static long Read()=>read==null?-1:read();
 }
}
#endif

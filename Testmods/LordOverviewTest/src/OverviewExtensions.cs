using System;
using System.Collections.Generic;
using System.Linq;
using APIShared.Statistics;
using Noesis;

namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static readonly Dictionary<Metric,LordStatisticColumn> providers=new Dictionary<Metric,LordStatisticColumn>();
  private static long catalogRevision;
  private static void StartExtensions(){LordStatistics.CatalogChanged+=OnCatalogChanged;SyncCatalog(LordStatistics.GetCatalog());}
  private static void SyncCatalog(LordStatisticCatalog catalog){
   providers.Clear();catalogRevision=catalog.Revision;
   foreach(var column in catalog.Columns){
    var id=Metrics.RegisterExternal(column.Key,column.Theme.ToString(),column.Format);
    providers[id]=column;
   }
  }
  private static void OnCatalogChanged(LordStatisticCatalog catalog){
   SyncCatalog(catalog);
   // Catalog callbacks never sample statistics, rebuild a popup or change its ScrollViewer.
   // A gesture keeps the realized geometry until its end; data stays unknown meanwhile.
   foreach(var row in rows)foreach(var id in providers.Keys)row.SetExtra(id,null);
   signature="";if(panel?.IsVisible==true && !headerGesture.Active)Render();
   SyncColumnChoices();UpdateColumnsPopup();
  }
  private static bool Available(Metric metric)=>!providers.TryGetValue(metric,out var column)?(int)metric>=0:column.IsActive;
  private static string MetricName(Metric metric)=>providers.TryGetValue(metric,out var column)?column.GetName():(int)metric>=0?Text(metric.ToString()):Metrics.Key(metric);
  private static void ReadExtensions(long expected){
   var allowed=rows.Where(r=>r.ExtendedAllowed).Select(r=>r.Player).ToArray();
   foreach(var id in selection.Columns.Where(m=>(int)m<0)){
    if(!providers.TryGetValue(id,out var provider) || !provider.IsActive)continue;
    var snapshot=provider.Read(expected,allowed);
    // A provider may synchronously trigger a lifecycle or logical activation change.
    if(expected!=System.Threading.Interlocked.Read(ref session) || panel?.IsVisible!=true)return;
    foreach(var row in rows)row.SetExtra(id,row.ExtendedAllowed && provider.IsActive && snapshot.TryGetValue(row.Player,out var value)?value:null);
   }
  }
  private static ToolTip CreateTip(string text,FrameworkElement reference){
   var tip=new ToolTip {Style=tipStyle,Content=text,IsHitTestVisible=false};
   APIShared.ModSettings.SharedToolTipSizing.SetReferenceElement(tip,reference);
   APIShared.ModSettings.SharedToolTipSizing.SetReferenceFontSize(tip,16);
   APIShared.ModSettings.SharedToolTipSizing.SetEnabled(tip,true);return tip;
  }
 }
}

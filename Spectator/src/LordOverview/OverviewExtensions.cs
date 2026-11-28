using System;
using System.Collections.Generic;
using System.Linq;
using APIShared.Statistics;
using Noesis;

namespace Spectator.LordOverview {
 internal static partial class OverviewRuntime {
  private static readonly Dictionary<Metric,LordStatisticColumn> providers=new Dictionary<Metric,LordStatisticColumn>();
  private static long catalogRevision;
  private static bool catalogUiPending;
  private static void StartExtensions(){LordStatistics.CatalogChanged+=OnCatalogChanged;SyncCatalog(LordStatistics.GetCatalog());}
  private static void SyncCatalog(LordStatisticCatalog catalog){
   providers.Clear();extensionIcons.Clear();catalogRevision=catalog.Revision;
   foreach(var column in catalog.Columns){
    var id=Metrics.RegisterExternal(column.Key,column.Theme.ToString(),column.Format);
    providers[id]=column;
   }
  }
  private static void OnCatalogChanged(LordStatisticCatalog catalog){
   SyncCatalog(catalog);
   // Drop previous values before queued UI work. A visibility change can revoke display and sorting.
   var invalidated=new List<LordRow>(rows.Count);
   foreach(var row in rows)invalidated.Add(new LordRow(row.Player,row.Alive,row.DisplayMask,row.Values));
   rows=invalidated;
   if(catalogUiPending)return;catalogUiPending=true;
   // Metadata/value providers can publish a new catalog synchronously. Never rebuild
   // the view reentrantly while a card or its metadata is still being constructed.
   Post(()=>{
    catalogUiPending=false;
    if(panel?.IsVisible==true){CancelHeaderGesture();Render();sampling.Request();requestedRefresh.Request();}
    SyncColumnChoices();UpdateColumnsPopup();
   });
  }
  private static bool Available(Metric metric)=>!providers.TryGetValue(metric,out var column)?(int)metric>=0:column.IsActive;
  private static string MetricName(Metric metric)=>providers.TryGetValue(metric,out var column)?column.GetName():(int)metric>=0?Text(metric.ToString()):Metrics.Key(metric);
  private static bool ReadExtensions(List<LordRow> sample,UiActionContext stamp,int informationVersion){
   var columns=selection.Columns;
   LordStatisticView[] views=null;
   List<(Metric Id,LordStatisticColumn Column,long Revision,IReadOnlyDictionary<int,LordStatisticCell> Cells)> projected=null;
   for(int i=0;i<columns.Length;i++){
    var id=columns[i];if((int)id>=0 || !providers.TryGetValue(id,out var provider) || !provider.IsActive)continue;
    if(views==null){
     if(!OverviewData.ViewerStillCurrent(settings,mode,stamp.Session,informationVersion))return false;
     views=OverviewData.GetViews(sample);
    }
    long revision=provider.Revision;
    long elapsed=0;Shared.DevDiag.Start(ref elapsed);
    IReadOnlyDictionary<int,LordStatisticCell> snapshot;
    try{snapshot=provider.ReadForView(stamp.Session,views);}finally{Shared.DevDiag.StopSpike(ref elapsed,"overview.provider",1000);}
    if(!stamp.Allows(CurrentUiContext(),false) ||
     informationVersion!=System.Threading.Volatile.Read(ref informationGeneration) ||
     !OverviewData.ViewerStillCurrent(settings,mode,stamp.Session,informationVersion))return false;
    if(snapshot.Count!=0){
     if(projected==null)projected=new List<(Metric,LordStatisticColumn,long,IReadOnlyDictionary<int,LordStatisticCell>)>();
     projected.Add((id,provider,revision,snapshot));
    }
   }
   // A later provider can invalidate an earlier column during the same refresh.
   if(projected!=null)foreach(var projection in projected){
    if(projection.Column.Revision!=projection.Revision)continue;
    foreach(var row in sample)if(projection.Cells.TryGetValue(row.Player,out var cell))
     row.SetExtra(projection.Id,cell.DisplayValue,cell.SortValue);
   }
   return true;
  }
  private static ToolTip CreateTip(string text,FrameworkElement reference){
   var tip=new ToolTip {Style=tipStyle,Content=text,IsHitTestVisible=false};
   APIShared.ModSettings.SharedToolTipSizing.SetReferenceElement(tip,reference);
   APIShared.ModSettings.SharedToolTipSizing.SetReferenceFontSize(tip,OverviewPresentationPolicy.FontSize);
   APIShared.ModSettings.SharedToolTipSizing.SetEnabled(tip,true);return tip;
  }
 }
}

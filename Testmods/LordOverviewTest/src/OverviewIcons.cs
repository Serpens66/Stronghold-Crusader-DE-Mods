using System;
using System.Collections.Generic;
using Noesis;
namespace LordOverviewTest {
 internal static partial class OverviewRuntime {
  private static ImageSource[] builtinIcons,popularityMasks;
  private static readonly Dictionary<Metric,ImageSource> extensionIcons=new Dictionary<Metric,ImageSource>();
  private static void ResetMetricIcons(){builtinIcons=null;popularityMasks=null;extensionIcons.Clear();}
  private static void EnsureMetricIcons(){
   if(builtinIcons!=null)return;
   var images=new ImageSource[Metrics.Builtins.Length];var masks=new ImageSource[3];
   for(int i=0;i<masks.Length;i++)masks[i]=main.GameSprites[i]??throw new InvalidOperationException("Vanilla popularity mask unavailable: "+i);
   for(int i=0;i<images.Length;i++){
    var metric=(Metric)i;if(metric==Metric.Popularity){images[i]=masks[0];continue;}
    string key=OverviewPresentationPolicy.IconKey(metric);
    Shared.DevDiag.Count("overview.icon.resource-lookup");
    images[i]=sourceHost.TryFindResource(key) as ImageSource??throw new InvalidOperationException("Vanilla statistics icon unavailable: "+key);
   }
   popularityMasks=masks;builtinIcons=images;
  }
  private static FrameworkElement MetricIcon(Metric metric){
   ImageSource image;
   if((int)metric>=0){EnsureMetricIcons();image=builtinIcons[(int)metric];}
   else if(!extensionIcons.TryGetValue(metric,out image)){
    long revision=catalogRevision;
    Shared.DevDiag.Count("overview.icon.provider-lookup");image=providers.TryGetValue(metric,out var provider)?provider.GetIcon():null;
    if(revision==catalogRevision)extensionIcons[metric]=image;else image=null;
   }
   return image!=null?(FrameworkElement)new Image {Source=image,Width=18,Height=18,IsHitTestVisible=false}:Label("?");
  }
 }
}

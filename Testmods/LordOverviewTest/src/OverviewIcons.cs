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
    if(metric==Metric.GoldAcquired){images[i]=images[(int)Metric.Gold];continue;}
    string key=OverviewPresentationPolicy.IconKey(metric);
    Shared.DevDiag.Count("overview.icon.resource-lookup");
    var source=sourceHost.TryFindResource(key) as BitmapSource??throw new InvalidOperationException("Vanilla statistics bitmap unavailable: "+key);
    var crop=OverviewPresentationPolicy.Crop(metric);
    if(source.PixelWidth!=crop.SourceWidth || source.PixelHeight!=crop.SourceHeight)throw new InvalidOperationException("Vanilla statistics bitmap dimensions changed: "+key);
    images[i]=new CroppedBitmap(source,new Int32Rect(crop.X,crop.Y,crop.Width,crop.Height));
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
   return image!=null?(FrameworkElement)new Image {Source=image,Width=OverviewPresentationPolicy.IconSize,Height=OverviewPresentationPolicy.IconSize,Stretch=Stretch.Uniform,IsHitTestVisible=false}:Label("?");
  }
 }
}

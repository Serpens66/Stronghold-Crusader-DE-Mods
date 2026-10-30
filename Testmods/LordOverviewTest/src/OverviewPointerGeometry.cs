using System;
namespace LordOverviewTest {
 internal static class HoverOcclusion {
  // A read-only ancestry walk: never temporarily alter hit testing on the real HUD.
  internal static bool Blocks<T>(T hit,T overlay,Func<T,T> parent,Func<T,int> z,Func<T,T,int> index) where T:class {
   if(hit==null)return false;if(overlay==null)return true;
   for(T node=hit;node!=null;node=parent(node))if(ReferenceEquals(node,overlay))return false;
   for(T node=overlay;node!=null;node=parent(node))if(ReferenceEquals(node,hit))return false;
   for(T a=hit;a!=null;a=parent(a)){
    T common=parent(a);if(common==null)break;
    for(T b=overlay;b!=null;b=parent(b))if(ReferenceEquals(parent(b),common)){
     int ai=index(common,a),bi=index(common,b);if(ai<0 || bi<0)return true;
     int az=z(a),bz=z(b);return az!=bz?az>bz:ai>bi;
    }
   }
   return true;
  }
 }
 internal static class WrappedDropGeometry {
  internal static bool TryTarget(double x,double y,double width,double height,MenuRect[] cells,int source,out int target,out MenuRect marker){
   target=-1;marker=default(MenuRect);
   if(double.IsNaN(x) || double.IsNaN(y) || x<0 || y<0 || x>=width || y>=height || cells==null || source<0 || source>=cells.Length)return false;
   int start=-1,end=-1;
   for(int i=0;i<cells.Length;i++)if(y>=cells[i].Y && y<cells[i].Bottom){if(start<0)start=i;end=i;}
   if(start<0)return false;
   int insertion=end+1;double boundary=cells[end].Right;
   for(int i=start;i<=end;i++)if(x<cells[i].X+cells[i].Width/2){insertion=i;boundary=cells[i].X;break;}
   target=insertion>source?insertion-1:insertion;
   marker=new MenuRect(boundary-1,cells[start].Y,2,cells[start].Height);return true;
  }
 }
}

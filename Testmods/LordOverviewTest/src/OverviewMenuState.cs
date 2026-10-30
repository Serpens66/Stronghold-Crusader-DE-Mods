using System;

namespace LordOverviewTest {
 internal struct MenuRect {
  internal readonly double X,Y,Width,Height;
  internal double Right=>X+Width;
  internal double Bottom=>Y+Height;
  internal MenuRect(double x,double y,double width,double height){X=x;Y=y;Width=width;Height=height;}
  internal bool Overlaps(MenuRect other)=>Width>0 && Height>0 && other.Width>0 && other.Height>0 && X<other.Right && Right>other.X && Y<other.Bottom && Bottom>other.Y;
 }
 internal sealed class MenuWindowState {
  internal MenuRect Bounds {get;private set;}
  internal bool HasPosition {get;private set;}
  internal bool Dragging {get;private set;}
  private double startX,startY,originX,originY;
  internal void Reset(){HasPosition=false;Dragging=false;Bounds=default(MenuRect);}
  internal MenuRect Arrange(double viewportWidth,double viewportHeight,MenuRect crown,MenuRect statistics){
   double width=Math.Min(520,Math.Max(0,viewportWidth-16)),height=Math.Min(420,Math.Max(0,viewportHeight-16));
   if(!HasPosition){
    var candidates=new[]{new MenuRect(crown.Right+8,crown.Bottom-height,width,height),new MenuRect(crown.X,crown.Y-height-8,width,height),
     new MenuRect(statistics.Right+8,statistics.Bottom-height,width,height),new MenuRect(statistics.X,statistics.Y-height-8,width,height)};
    MenuRect best=Clamp(candidates[0],viewportWidth,viewportHeight);double score=double.MaxValue;
    foreach(var candidate in candidates){var placed=Clamp(candidate,viewportWidth,viewportHeight);
     double distance=Math.Pow(placed.X-crown.Right,2)+Math.Pow(placed.Bottom-crown.Bottom,2);
     double penalty=placed.Overlaps(statistics)?1e12:0;if(placed.Overlaps(crown))penalty+=1e12;
     if(penalty+distance<score){score=penalty+distance;best=placed;}
    }
    Bounds=best;HasPosition=true;
   }else Bounds=Clamp(new MenuRect(Bounds.X,Bounds.Y,width,height),viewportWidth,viewportHeight);
   return Bounds;
  }
  internal static MenuRect Clamp(MenuRect value,double viewportWidth,double viewportHeight){
   double width=Math.Max(0,Math.Min(value.Width,viewportWidth)),height=Math.Max(0,Math.Min(value.Height,viewportHeight));
   return new MenuRect(Math.Max(0,Math.Min(value.X,Math.Max(0,viewportWidth-width))),Math.Max(0,Math.Min(value.Y,Math.Max(0,viewportHeight-height))),width,height);
  }
  internal void BeginDrag(double x,double y){startX=x;startY=y;originX=Bounds.X;originY=Bounds.Y;Dragging=HasPosition;}
  internal void MoveDrag(double x,double y,double width,double height){if(Dragging)Bounds=Clamp(new MenuRect(originX+x-startX,originY+y-startY,Bounds.Width,Bounds.Height),width,height);}
  internal void EndDrag(){Dragging=false;}
 }
 internal sealed class CrownPress {
  private UiActionContext stamp;
  internal bool Active=>stamp!=null;
  internal bool Begin(UiActionContext context,bool right){stamp=right && context.Allows(context,false)?context:null;return stamp!=null;}
  internal bool End(UiActionContext context,bool inside,bool right){var before=stamp;stamp=null;return right && inside && before!=null && before.Allows(context,false);}
  internal void Cancel(){stamp=null;}
 }
 internal static class CrownSettingsAction {
  internal static void Run(bool overviewVisible,bool menuOpen,Action showOverview,Action openMenu,Action closeMenu){
   if(!overviewVisible)showOverview();
   if(overviewVisible && menuOpen)closeMenu();else openMenu();
  }
 }
}

using System;

namespace LordOverviewTest {
 // Independent of Noesis: a stable target survives statistic refreshes, but never another HUD/session.
 internal sealed class OverviewHoverState {
  internal string Target {get;private set;}
  private double entered,opened,lastTime;
  private bool displayed,expired;
  internal bool Update(string target,double now,double delay,double duration){
   if(now<lastTime)Reset();lastTime=now;
   if(target!=Target){Target=target;entered=now;displayed=false;expired=false;}
   if(target==null || expired)return false;
   if(!displayed && now-entered>=Math.Max(0,delay)){displayed=true;opened=now;}
   if(displayed && now-opened>=Math.Max(0,duration)){displayed=false;expired=true;}
   return displayed;
  }
  internal void Reset(){Target=null;entered=opened=lastTime=0;displayed=expired=false;}
 }
 internal static class TableInteraction {
  internal static bool CanEdit(bool menuOpen,bool visible,long session,long expected,int epoch,int expectedEpoch)=>
   menuOpen && visible && session>0 && session==expected && epoch==expectedEpoch;
 }
}

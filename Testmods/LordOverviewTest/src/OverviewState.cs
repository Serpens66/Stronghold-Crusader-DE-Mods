using System;
using System.Collections.Generic;
namespace LordOverviewTest {
 internal sealed class LordRow {
  internal readonly int Player; internal readonly bool Alive; internal readonly long?[] Values;
  internal LordRow(int player,bool alive,params long?[] values) { if(values.Length>5)throw new ArgumentException();Player=player;Alive=alive;Values=(long?[])values.Clone(); }
 }
 internal sealed class OverviewState {
  internal int Column {get;private set;} = -1;
  internal bool Descending {get;private set;}
  internal void Select(int column) {if(column < -1 || column>=5)throw new ArgumentOutOfRangeException();Descending=column==Column?!Descending:true;Column=column;}
  internal List<LordRow> Sort(IEnumerable<LordRow> rows){var result=new List<LordRow>(rows);result.Sort(Compare);return result;}
  private int Compare(LordRow a,LordRow b){
   if(Column<0)return Descending?b.Player.CompareTo(a.Player):a.Player.CompareTo(b.Player);
   long? av=Column<a.Values.Length?a.Values[Column]:null,bv=Column<b.Values.Length?b.Values[Column]:null;
   if(av.HasValue!=bv.HasValue)return av.HasValue?-1:1;
   int cmp=av.HasValue?av.Value.CompareTo(bv.Value):0;
   return cmp==0?a.Player.CompareTo(b.Player):Descending?-cmp:cmp;
  }
 }
 internal sealed class RefreshGate {
  private int ticks;
  internal bool Tick(bool visible){if(!visible){ticks=0;return false;}if(++ticks<40)return false;ticks=0;return true;}
 }
}

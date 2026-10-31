using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace LordOverviewTest {
 // Stable persisted metric identifiers never depend on translated labels or column positions.
 internal enum Metric { Gold, Army, Popularity, Fear, GoldAcquired, MaxPopulation, FoodProduced, WoodProduced, StoneProduced, IronProduced, PitchProduced, WeaponsProduced, GoodsReceived, GoodsSent, TroopsProduced, TroopsKilled, TroopsLost, BuildingsDestroyed, BuildingsLost, LordsKilled }
 internal sealed class MetricDefinition {
  internal readonly Metric Id; internal string Group; internal readonly string Key; internal Func<decimal,string> Formatter;
  internal bool Historical => (int)Id>=0 && Id>=Metric.GoldAcquired;
  internal MetricDefinition(Metric id,string group){Id=id;Group=group;Key=id.ToString();}
  internal MetricDefinition(Metric id,string key,string group){Id=id;Key=key;Group=group;}
  internal string Format(decimal? value)=>!value.HasValue?"?":Formatter!=null?Formatter(value.Value):Id==Metric.Fear?value.Value.ToString("+0;-0;+0",CultureInfo.InvariantCulture):value.Value.ToString(CultureInfo.InvariantCulture);
 }
 internal static class Metrics {
  internal static readonly string[] Groups={"Economy","Military","PopulationMood","OtherMods"};
  internal static readonly MetricDefinition[] Builtins=Enum.GetValues(typeof(Metric)).Cast<Metric>().Select(m=>new MetricDefinition(m,Group(m))).ToArray();
  private static string Group(Metric metric){switch(metric){
   case Metric.Popularity:case Metric.Fear:case Metric.MaxPopulation:return "PopulationMood";
   case Metric.Army:case Metric.TroopsProduced:case Metric.TroopsKilled:case Metric.TroopsLost:case Metric.BuildingsDestroyed:case Metric.BuildingsLost:case Metric.LordsKilled:return "Military";
   default:return "Economy";
  }}
  private static readonly Dictionary<Metric,MetricDefinition> extras=new Dictionary<Metric,MetricDefinition>();
  internal static MetricDefinition[] All=>Builtins.Concat(extras.Values).ToArray();
  internal static MetricDefinition Get(Metric id)=>(int)id>=0?Builtins[(int)id]:extras[id];
  internal static Metric RegisterExternal(string key,string group="OtherMods",Func<decimal,string> formatter=null){
   var found=extras.Values.FirstOrDefault(d=>d.Key==key);if(found!=null){found.Formatter=formatter;found.Group=group;return found.Id;}
   var id=(Metric)(-extras.Count-1);extras[id]=new MetricDefinition(id,key,group){Formatter=formatter};return id;
  }
  internal static bool IsExternalKey(string key)=>!string.IsNullOrWhiteSpace(key) && key.Count(c=>c=='/')==1 && !key.Contains(",") && key.Split('/').All(p=>p.Length>0);
  internal static string Key(Metric id)=>Get(id).Key;
  internal static readonly Metric[] Defaults={Metric.Gold,Metric.Army,Metric.Popularity,Metric.Fear};
  internal static string Serialize(IEnumerable<Metric> columns)=>string.Join(",",columns.Select(Key));
  internal static Metric[] Parse(string text){var columns=new List<Metric>();foreach(string key in (text??"").Split(',')){
   Metric id;if(Enum.TryParse(key,out id) && Enum.IsDefined(typeof(Metric),id) && key==id.ToString()){}
   else if(IsExternalKey(key))id=extras.Values.FirstOrDefault(d=>d.Key==key)?.Id??RegisterExternal(key);else continue;
   if(!columns.Contains(id))columns.Add(id);
  }return columns.Count<1?(Metric[])Defaults.Clone():columns.ToArray();}
 }
 internal sealed class ColumnMenuState {
  private readonly Dictionary<string,double> offsets=new Dictionary<string,double>();
  internal string Group {get;private set;}="Economy";
  internal double Offset=>offsets.TryGetValue(Group,out var offset)?offset:0;
  internal bool Select(string group,double oldOffset){if(!Metrics.Groups.Contains(group))return false;Remember(oldOffset);Group=group;return true;}
  internal void Remember(double offset){offsets[Group]=Math.Max(0,offset);}
  internal void Reset(){Group="Economy";offsets.Clear();}
 }
 internal sealed class ColumnSelection {
  private List<Metric> columns=new List<Metric>(Metrics.Defaults);
  private Metric[] snapshot;
  internal Metric[] Columns=>snapshot??(snapshot=columns.ToArray());
  internal void Load(string value){var parsed=Metrics.Parse(value);if(OverviewLayout.SameColumns(Columns,parsed))return;columns=new List<Metric>(parsed);snapshot=parsed;}
  internal bool Toggle(Metric id){if(!Metrics.All.Any(d=>d.Id==id))return false;if(columns.Contains(id)){if(columns.Count==1)return false;columns.Remove(id);}else{columns.Add(id);}snapshot=null;return true;}
  internal bool MoveTo(Metric id,int index){int old=columns.IndexOf(id);if(old<0 || index<0 || index>=columns.Count || old==index)return false;columns.RemoveAt(old);columns.Insert(index,id);snapshot=null;return true;}
 }
 internal enum HeaderGestureResult { Cancel, Sort, Reorder }
 internal static class HeaderDropGeometry {
  internal static bool TryTarget(double x,double y,double height,double start,double[] widths,int source,out int target,out double boundary){
   target=-1;boundary=start;
   if(double.IsNaN(start) || double.IsInfinity(start) || start<0 || double.IsNaN(height) || height<=0 || widths==null || source<0 || source>=widths.Length || double.IsNaN(x) || double.IsNaN(y) || y<0 || y>=height || widths.Any(w=>w<=0 || double.IsNaN(w) || double.IsInfinity(w)))return false;
   double end=start+widths.Sum();if(x<start || x>=end)return false;
   int insertion=widths.Length;boundary=end;double left=start;
   for(int i=0;i<widths.Length;i++){if(x<left+widths[i]/2){insertion=i;boundary=left;break;}left+=widths[i];}
   target=insertion>source?insertion-1:insertion;return true;
  }
 }
 // Coordinates and the six-unit threshold are in the header's logical UI space, not screen pixels.
 internal sealed class HeaderGesture {
  internal bool Active {get;private set;}
  internal bool Dragging {get;private set;}
  internal Metric Metric {get;private set;}
  internal int Target {get;private set;}=-1;
  private double startX,startY;private long session;private int hud;
  internal void Begin(Metric metric,double x,double y,long sessionId,int generation){Active=true;Dragging=false;Metric=metric;startX=x;startY=y;session=sessionId;hud=generation;Target=-1;}
  internal void Update(double x,double y,int target){if(!Active)return;if((x-startX)*(x-startX)+(y-startY)*(y-startY)>=36)Dragging=true;Target=target;}
  internal HeaderGestureResult End(bool overOriginal,long sessionId,int generation,out Metric metric,out int index){metric=Metric;index=Target;
   var result=!Active || sessionId!=session || generation!=hud?HeaderGestureResult.Cancel:Dragging?(Target>=0?HeaderGestureResult.Reorder:HeaderGestureResult.Cancel):overOriginal?HeaderGestureResult.Sort:HeaderGestureResult.Cancel;
   Cancel();return result;
  }
  internal void Cancel(){Active=false;Dragging=false;Target=-1;}
 }
 internal static class InformationPolicy {
  internal static bool CanRead(Metric metric,bool editor,bool spectator,bool eliminated,bool own,bool allied,bool enemyPermissionConfirmed)=>metric==Metric.Gold || metric==Metric.Army || editor || spectator || eliminated || own || allied || enemyPermissionConfirmed;
  internal static bool IsEliminated(bool officialLoss,bool meritDefeat,bool identifiedDeadLord)=>officialLoss || meritDefeat || identifiedDeadLord;
  internal static bool HasDeadLordEvidence(bool initializing,bool zeroHealth,bool projectileDeath,bool deleting)=>!initializing && (zeroHealth || projectileDeath || deleting);
 }
 internal sealed class LordRow {
  internal readonly int Player; internal readonly bool Alive; internal readonly long?[] Values;
  internal LordRow(int player,bool alive,params long?[] values) {Player=player;Alive=alive;Values=(long?[])values.Clone();}
  private Dictionary<Metric,decimal?> extra;
  internal bool ExtendedAllowed {get;set;}
  internal decimal? Get(Metric metric)=>(int)metric>=0?((int)metric<Values.Length?(decimal?)Values[(int)metric]:null):(extra!=null && extra.TryGetValue(metric,out var value)?value:null);
  internal void SetExtra(Metric metric,decimal? value){if(!value.HasValue){extra?.Remove(metric);return;}if(extra==null)extra=new Dictionary<Metric,decimal?>();extra[metric]=value;}
 }
 internal sealed class OverviewState : IComparer<LordRow> {
  int IComparer<LordRow>.Compare(LordRow a,LordRow b)=>Compare(a,b);
  internal Metric? Column {get;private set;}
  internal bool Descending {get;private set;}
  internal void Select(Metric? column) {Descending=column==Column?!Descending:true;Column=column;}
  internal void Reconcile(IEnumerable<Metric> visible){if(Column.HasValue && !visible.Contains(Column.Value)){Column=null;Descending=false;}}
  internal List<LordRow> Sort(IEnumerable<LordRow> rows){var result=new List<LordRow>(rows);result.Sort(this);return result;}
  internal static int VisibleCount(List<LordRow> source,bool showEliminated){int count=0;for(int i=0;i<source.Count;i++)if(showEliminated || source[i].Alive)count++;return count;}
  internal void SortInto(List<LordRow> source,List<LordRow> target,bool showEliminated=true){target.Clear();for(int i=0;i<source.Count;i++)if(showEliminated || source[i].Alive)target.Add(source[i]);target.Sort(this);}
  private int Compare(LordRow a,LordRow b){
   if(!Column.HasValue)return Descending?b.Player.CompareTo(a.Player):a.Player.CompareTo(b.Player);
   decimal? av=a.Get(Column.Value),bv=b.Get(Column.Value);
   if(av.HasValue!=bv.HasValue)return av.HasValue?-1:1;
   int cmp=av.HasValue?av.Value.CompareTo(bv.Value):0;
   return cmp==0?a.Player.CompareTo(b.Player):Descending?-cmp:cmp;
  }
 }
}

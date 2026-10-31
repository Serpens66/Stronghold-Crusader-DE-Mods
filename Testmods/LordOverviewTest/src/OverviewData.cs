using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared;
using APIShared.GameModes;
using APIShared.Statistics;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
namespace LordOverviewTest {
 internal static unsafe class OverviewData {
  private static readonly object engineLock=ResolveEngineLock();
  private static readonly DeferredRead pending=new DeferredRead(CopyRaw);
  private static readonly PreparedRead reader=new PreparedRead(engineLock,pending.Run);
  private static EngineReadbackRegistration readback;
  private static readonly int historyBytes=ValidateHistoryContract();
  private static RawSnapshot front=new RawSnapshot(historyBytes),back=new RawSnapshot(historyBytes);
  private static readonly long?[] values=new long?[Metrics.Builtins.Length];
  private static readonly List<LordRow> assembled=new List<LordRow>(8);
  private static GamePlayerManagerAPI players;
  private static bool historyDue,hasSample,capturing;
  private static double lastHistory=double.NegativeInfinity;
  private static long cachedSession;
  private static int cachedRights;
  private static long requestedSession;
  private static int requestedRights,requestedGeneration;
  private static double copiedAt;
#if SERP_DEVDIAG
  private static long rawTicks,rawAllocated;
  private static int rawThread,mainThread;
#endif
  internal static bool HasPending=>pending.Busy;
  internal static bool PollPending(double now)=>pending.Poll(now,reader);
  internal static void EnableReadbacks(){
#if SERP_DEVDIAG
   mainThread=System.Threading.Thread.CurrentThread.ManagedThreadId;_ = OverviewAllocationProbe.Read();
#endif
   if(!EngineReadbacks.TryRegister("LordOverviewTest_Serp","statistics",pending.Run,out readback))throw new InvalidOperationException("Engine readback publisher unavailable.");
  }
  internal static void CancelPending(){readback?.Cancel();pending.Cancel();}
  private static object ResolveEngineLock(){var field=typeof(EngineInterface).GetField("threadLock",BindingFlags.Static|BindingFlags.NonPublic);
   if(field==null || field.FieldType!=typeof(object))throw new InvalidOperationException("Vanilla engine lock contract unavailable.");
   return field.GetValue(null)??throw new InvalidOperationException("Vanilla engine lock not initialized.");}
  private static int ValidateHistoryContract(){
   var type=typeof(EngineInterface.multiplayer_stats_export);
   if(!type.IsExplicitLayout || System.Runtime.InteropServices.Marshal.SizeOf(type)!=sizeof(EngineInterface.multiplayer_stats_export))throw new InvalidOperationException("Vanilla statistics layout unavailable.");
   foreach(string name in new[]{"valid","gold_acquired","max_population","food_produced","wood_produced","stone_produced","iron_produced","pitch_produced","weapons_produced","goods_received","goods_sent","troops_produced","who_killed_who","enemy_buildings_destroyed","buildings_lost","lords_killed"}){
    var field=type.GetField(name,BindingFlags.Public|BindingFlags.Instance);
    var fixedBuffer=field==null?null:(System.Runtime.CompilerServices.FixedBufferAttribute)Attribute.GetCustomAttribute(field,typeof(System.Runtime.CompilerServices.FixedBufferAttribute));
    if(fixedBuffer==null || fixedBuffer.ElementType!=typeof(int) || fixedBuffer.Length!=(name=="who_killed_who"?81:9))throw new InvalidOperationException("Vanilla statistics field unavailable: "+name);
   }
   return sizeof(EngineInterface.multiplayer_stats_export);
  }
  internal static void Reset(){CancelPending();hasSample=false;cachedSession=0;cachedRights=0;lastHistory=double.NegativeInfinity;front.Clear();if(!pending.Busy)back.Clear();assembled.Clear();}
  internal static bool HasSample(long session,int rights)=>hasSample && cachedSession==session && cachedRights==rights;
  internal static List<LordRow> FromCache(Metric[] visible,OverviewSettings settings,GameModeSnapshot? mode,List<LordRow> previous,long session,int rights){
   if(!HasSample(session,rights) || capturing)return null;
   return BuildRows(front,visible,settings,mode,previous,false);
  }
  internal static List<LordRow> Capture(Metric[] visible,OverviewSettings settings,GameModeSnapshot? mode,List<LordRow> previous,long session,int rights,int generation,double now){
   if(capturing)return null;capturing=true;
   long elapsed=0;Shared.DevDiag.Start(ref elapsed);
   try {
    settings.System_RefreshSettingsAccess();
    if(hasSample && (cachedSession!=session || cachedRights!=rights))Reset();
    if(!pending.Busy){
     players=GamePlayerManagerAPI.Instance;_ = GameUnitManagerAPI.Instance;
     bool historical=false;for(int i=0;i<visible.Length;i++)if((int)visible[i]>=0 && Metrics.Get(visible[i]).Historical){historical=true;break;}
     historyDue=historical && (!front.HasHistory || now-lastHistory>=2);
     back.Prepare(historyDue,front);back.Controlled=PlayerPerspectiveAPI.GetControlledPlayerId();
     requestedSession=session;requestedRights=rights;requestedGeneration=generation;
     pending.Queue(now);
     // Pause/idle: copy immediately if free. Busy simulation: let its completed-run publisher copy.
     if(!reader.TryRead())Shared.DevDiag.Count("overview.lock-busy");
     if(!pending.Complete)readback.Request();
    }
    if(!pending.Complete)return null;
    bool valid=pending.Take(out var failure);
    readback.Cancel();
#if SERP_DEVDIAG
    // Aggregate/log only after producer ownership ends, outside the engine critical section.
    long measured=System.Diagnostics.Stopwatch.GetTimestamp()-rawTicks;Shared.DevDiag.StopSpike(ref measured,"overview.raw",1000);
    if(rawAllocated>=0){Shared.DevDiag.Count("overview.raw.allocation-samples");Shared.DevDiag.Count("overview.raw.bytes",rawAllocated);}
    if(failure==null){Shared.DevDiag.Count("overview.raw.success");Shared.DevDiag.Count(rawThread==mainThread?"overview.raw.main":"overview.raw.sim");}
#endif
    if(failure!=null)throw new InvalidOperationException("Deferred statistics read failed.",failure);
    if(!valid || requestedSession!=session || requestedRights!=rights || requestedGeneration!=generation || back.Controlled!=PlayerPerspectiveAPI.GetControlledPlayerId()){back.Clear();return null;}
    var old=front;front=back;back=old;hasSample=true;cachedSession=session;cachedRights=rights;
    if(historyDue){lastHistory=copiedAt;Shared.DevDiag.Count("overview.history");}
    return BuildRows(front,visible,settings,mode,previous,true);
   }finally {capturing=false;Shared.DevDiag.StopSpike(ref elapsed,"overview.capture",1000);}
  }
  // Prepared buffers and services only: no managed allocation, conversion or presentation while locked.
  private static void CopyRaw(){
#if SERP_DEVDIAG
   long started=System.Diagnostics.Stopwatch.GetTimestamp(),allocated=OverviewAllocationProbe.Read();
#endif
   try{CopyNative();}
   finally{
#if SERP_DEVDIAG
    rawAllocated=allocated<0?-1:OverviewAllocationProbe.Read()-allocated;
    rawTicks=System.Diagnostics.Stopwatch.GetTimestamp()-started;rawThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
#endif
   }
  }
  private static void CopyNative(){
   fixed(int* target=back.Merit)EngineInterface.DLL_GetMeritData(target);
   if(historyDue)fixed(byte* target=back.History)EngineInterface.DLL_GetMPScoreData(target);
   int controlled=back.Controlled;
   back.LocalValid=controlled>=1 && controlled<=8 && back.Merit[controlled*5]>0;
   for(int player=1;player<=8;player++){
    if(back.Merit[player*5]<=0 || players==null || !players.TryGetPlayerResourcesById(player,out GamePlayerResources* resource) || resource==null)continue;
    back.Resources[player]=new ResourceSnapshot {Present=true,Defeated=resource->r_WinLossState==WinLossState.Loss,Popularity=resource->r_CurrentPopularity,Fear=resource->r_GoodBadThingBoost};
    if(player!=controlled)continue;
    bool dead=false;long lord=resource->r_LordUnitId;
    if(lord>0 && lord<=int.MaxValue && UnitAccess.TryGetById((int)lord,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId>0 && unit->r_UnitChimp==eChimps.CHIMP_TYPE_LORD && unit->r_ControllableForPlayerId==player)
     dead=InformationPolicy.HasDeadLordEvidence(unit->r_AliveState==AliveState.None || unit->r_AliveState==AliveState.NeedsInit,unit->r_CurrentHealth==0,unit->r_IsKilledByProjectile!=0,unit->r_AliveState==AliveState.MarkedForDeletion);
    if(player==controlled)back.Eliminated=InformationPolicy.IsEliminated(resource->r_WinLossState==WinLossState.Loss,back.Merit[player*5+1]<0,dead);
   }
   copiedAt=System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency;
  }
  private static List<LordRow> BuildRows(RawSnapshot raw,Metric[] visible,OverviewSettings settings,GameModeSnapshot? mode,List<LordRow> previous,bool includeExtensions){
   bool editor=mode.HasValue && mode.Value.Kind==GameModeKind.MapEditor;var state=GameData.Instance?.lastGameState;
   bool spectator=state!=null && state.spectatorMode!=0 && EditorDirector.instance!=null && EditorDirector.instance.ActivePlayerID<=0;
   bool permission=settings.AllowEnemyDetails && mode.HasValue && mode.Value.Kind!=GameModeKind.Unknown && (!mode.Value.IsRealMultiplayer || settings.IsRealMultiplayerContext && (settings.IsLocalSettingsHost || settings.EnemyDetailsReceived));
   bool external=false;for(int i=0;i<visible.Length;i++)if((int)visible[i]<0){external=true;break;}
   assembled.Clear();bool unchanged=true;
   for(int player=1;player<=8;player++){
    int slot=player*5;if(raw.Merit[slot]<=0)continue;
    Array.Clear(values,0,values.Length);bool own=raw.LocalValid && player==raw.Controlled;
    bool allied=raw.LocalValid && raw.Merit[raw.Controlled*5+4]>0 && raw.Merit[raw.Controlled*5+4]==raw.Merit[slot+4];
    bool extended=InformationPolicy.CanRead(Metric.Popularity,editor,spectator,raw.Eliminated,own,allied,permission);
    for(int i=0;i<visible.Length;i++){
     var metric=visible[i];if((int)metric<0 || !InformationPolicy.CanRead(metric,editor,spectator,raw.Eliminated,own,allied,permission))continue;
     switch(metric){
      case Metric.Gold:values[(int)metric]=raw.Merit[slot+2];break;
      case Metric.Army:values[(int)metric]=raw.Merit[slot+3];break;
      case Metric.Popularity:if(raw.Resources[player].Present)values[(int)metric]=StatisticValues.Popularity(raw.Resources[player].Popularity);break;
      case Metric.Fear:if(raw.Resources[player].Present)values[(int)metric]=StatisticValues.Fear(raw.Resources[player].Fear);break;
      default:if(raw.HasHistory)fixed(byte* buffer=raw.History){var data=(EngineInterface.multiplayer_stats_export*)buffer;if(data->valid[player]>0)values[(int)metric]=Historical(data,metric,player);}break;
     }
    }
    LordRow old=null;for(int i=0;i<previous.Count;i++)if(previous[i].Player==player){old=previous[i];break;}
    bool alive=!InformationPolicy.IsEliminated(raw.Resources[player].Present && raw.Resources[player].Defeated,raw.Merit[slot+1]<0,false);
    bool same=old!=null && old.Alive==alive && old.ExtendedAllowed==extended;
    if(same)for(int i=0;i<values.Length;i++)if(values[i]!=old.Values[i]){same=false;break;}
    if(external && includeExtensions)same=false; // Providers must not mutate a published row.
    var row=same?old:new LordRow(player,alive,values){ExtendedAllowed=extended};
    if(!same && !includeExtensions && external && old!=null && extended)for(int i=0;i<visible.Length;i++)if((int)visible[i]<0)row.SetExtra(visible[i],old.Get(visible[i]));
    if(assembled.Count>=previous.Count || !ReferenceEquals(row,previous[assembled.Count]))unchanged=false;
    assembled.Add(row);
   }
   return unchanged && assembled.Count==previous.Count?previous:new List<LordRow>(assembled);
  }
  private static long? Historical(EngineInterface.multiplayer_stats_export* data,Metric metric,int p){switch(metric){
   case Metric.GoldAcquired:return data->gold_acquired[p];case Metric.MaxPopulation:return data->max_population[p];
   case Metric.FoodProduced:return data->food_produced[p];case Metric.WoodProduced:return data->wood_produced[p];case Metric.StoneProduced:return data->stone_produced[p];
   case Metric.IronProduced:return data->iron_produced[p];case Metric.PitchProduced:return data->pitch_produced[p];case Metric.WeaponsProduced:return data->weapons_produced[p];
   case Metric.GoodsReceived:return data->goods_received[p];case Metric.GoodsSent:return data->goods_sent[p];case Metric.TroopsProduced:return data->troops_produced[p];
   case Metric.TroopsKilled:case Metric.TroopsLost:long total=0;bool losses=metric==Metric.TroopsLost;for(int other=losses?0:1;other<=8;other++)if(other!=p)total+=data->who_killed_who[losses?other*9+p:p*9+other];return total;
   case Metric.BuildingsDestroyed:return data->enemy_buildings_destroyed[p];case Metric.BuildingsLost:return data->buildings_lost[p];case Metric.LordsKilled:return data->lords_killed[p];
   default:return null;
  }}
 }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.GameModes;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
namespace LordOverviewTest {
 internal static unsafe class OverviewData {
  private static readonly object engineLock=ResolveEngineLock();
  private static object ResolveEngineLock(){var field=typeof(EngineInterface).GetField("threadLock",BindingFlags.Static|BindingFlags.NonPublic);
   if(field==null || field.FieldType!=typeof(object))throw new InvalidOperationException("Vanilla engine lock contract unavailable.");
   return field.GetValue(null)??throw new InvalidOperationException("Vanilla engine lock not initialized.");}
  internal static List<LordRow> Capture(Metric[] visible,OverviewSettings settings,GameModeSnapshot? mode){
   settings.System_RefreshSettingsAccess();
   lock(engineLock){
    var merit=EngineInterface.GetMeritData();var players=GamePlayerManagerAPI.Instance;
    int controlled=PlayerPerspectiveAPI.GetControlledPlayerId();bool localValid=controlled>=1 && controlled<=8 && merit[controlled,0]>0;
    bool editor=mode.HasValue && mode.Value.Kind==GameModeKind.MapEditor;var state=GameData.Instance?.lastGameState;
    bool spectator=state!=null && state.spectatorMode!=0 && EditorDirector.instance!=null && EditorDirector.instance.ActivePlayerID<=0;bool eliminated=false;
    if(localValid && players!=null && players.TryGetPlayerResourcesById(controlled,out GamePlayerResources* local) && local!=null){
     bool deadLord=false;int lord=checked((int)local->r_LordUnitId);
     if(lord>0 && UnitAccess.TryGetById(lord,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId>0 && unit->r_UnitChimp==eChimps.CHIMP_TYPE_LORD && unit->r_ControllableForPlayerId==controlled)
      deadLord=InformationPolicy.HasDeadLordEvidence(unit->r_AliveState==AliveState.None || unit->r_AliveState==AliveState.NeedsInit,
       unit->r_CurrentHealth==0,unit->r_IsKilledByProjectile!=0,unit->r_AliveState==AliveState.MarkedForDeletion);
     eliminated=InformationPolicy.IsEliminated(local->r_WinLossState==WinLossState.Loss,merit[controlled,1]<0,deadLord);
    }
    bool permission=settings.AllowEnemyDetails && mode.HasValue && mode.Value.Kind!=GameModeKind.Unknown && (!mode.Value.IsRealMultiplayer ||
     settings.IsRealMultiplayerContext && (settings.IsLocalSettingsHost || settings.EnemyDetailsReceived));
    EngineInterface.MPScoreData statistics=visible.Any(m=>Metrics.Get(m).Historical)?EngineInterface.GetMPScoreData():null;
    var result=new List<LordRow>();
    for(int player=1;player<=8;player++)if(merit[player,0]>0){
     var values=new long?[Metrics.Builtins.Length];bool own=localValid && player==controlled;
     bool allied=localValid && merit[controlled,4]>0 && merit[controlled,4]==merit[player,4];
     GamePlayerResources* resource=null;if(players!=null && (visible.Contains(Metric.Popularity) || visible.Contains(Metric.Fear)))players.TryGetPlayerResourcesById(player,out resource);
     foreach(var metric in visible.Where(m=>(int)m>=0)){
      if(!InformationPolicy.CanRead(metric,editor,spectator,eliminated,own,allied,permission))continue;
      switch(metric){
       case Metric.Gold:values[(int)metric]=merit[player,2];break;
       case Metric.Army:values[(int)metric]=merit[player,3];break;
       case Metric.Popularity:if(resource!=null)values[(int)metric]=StatisticValues.Popularity(resource->r_CurrentPopularity);break;
       case Metric.Fear:if(resource!=null)values[(int)metric]=StatisticValues.Fear(resource->r_GoodBadThingBoost);break;
       default:if(statistics!=null && StatisticValues.At(statistics.valid,player)>0)values[(int)metric]=Historical(statistics,metric,player);break;
      }
     }
     result.Add(new LordRow(player,merit[player,1]>=0,values){ExtendedAllowed=InformationPolicy.CanRead(Metric.Popularity,editor,spectator,eliminated,own,allied,permission)});
    }
    return result;
   }
  }
  private static long? Historical(EngineInterface.MPScoreData data,Metric metric,int p){switch(metric){
   case Metric.GoldAcquired:return StatisticValues.At(data.gold_acquired,p);
   case Metric.MaxPopulation:return StatisticValues.At(data.max_population,p);
   case Metric.FoodProduced:return StatisticValues.At(data.food_produced,p);
   case Metric.WoodProduced:return StatisticValues.At(data.wood_produced,p);
   case Metric.StoneProduced:return StatisticValues.At(data.stone_produced,p);
   case Metric.IronProduced:return StatisticValues.At(data.iron_produced,p);
   case Metric.PitchProduced:return StatisticValues.At(data.pitch_produced,p);
   case Metric.WeaponsProduced:return StatisticValues.At(data.weapons_produced,p);
   case Metric.GoodsReceived:return StatisticValues.At(data.goods_received,p);
   case Metric.GoodsSent:return StatisticValues.At(data.goods_sent,p);
   case Metric.TroopsProduced:return StatisticValues.At(data.troops_produced,p);
   case Metric.TroopsKilled:return StatisticValues.Kills(data.who_killed_who,p,false);
   case Metric.TroopsLost:return StatisticValues.Kills(data.who_killed_who,p,true);
   case Metric.BuildingsDestroyed:return StatisticValues.At(data.enemy_buildings_destroyed,p);
   case Metric.BuildingsLost:return StatisticValues.At(data.buildings_lost,p);
   case Metric.LordsKilled:return StatisticValues.At(data.lords_killed,p);
   default:return null;
  }}
 }
}

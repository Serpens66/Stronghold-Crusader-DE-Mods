using System;
using System.Collections.Generic;
using System.Threading;
using AIVParser.Core;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
namespace CastlePlanner {
 internal sealed partial class BlueprintRuntimeController {
  private BlueprintLivePlan spectatorPlan;
  private bool spectatorRequested;
  private volatile bool spectatorSampling;
  private int spectatorPlayer=-1,spectatorTicks,spectatorDirty;
  private void InitializeSpectatorBlueprints(){
   subscriptions.Add(APIShared.PlayerPerspectiveAPI.SubscribeChanges(CastlePlannerPlugin.PluginGuid,"blueprint-view",change=>{
    Shared.UnityMainThreadDispatch.TryEnqueue(()=>{if(HasSpectatorIdentity)RefreshSpectatorIdentity(true);});
   }));
   GameTimeManagerAPI.Instance.OnTick+=tick=>{if(BlueprintSpectatorPolicy.ShouldSample(ref spectatorTicks,spectatorSampling)){Interlocked.Exchange(ref spectatorDirty,1);}};
  }
  private void RefreshSpectatorIdentity(bool force=false){
   int player=HasSpectatorIdentity?APIShared.PlayerPerspectiveAPI.GetViewedPlayerId():-1;
   if(!force && player==spectatorPlayer)return;
   spectatorPlayer=player;spectatorAivFileName=null;spectatorPlan=null;layout=null;renderer.Clear();blueprintVisible=false;
   controlledKeepAvailable=false;Interlocked.Exchange(ref spectatorDirty,1);
   RefreshHud();
  }
  private void ResetSpectatorBlueprints(){spectatorAivNames.Clear();spectatorAivFileName=null;spectatorNameErrorLogged=false;spectatorTicks=0;spectatorRequested=false;spectatorSampling=false;spectatorPlan=null;spectatorPlayer=-1;Interlocked.Exchange(ref spectatorDirty,0);}
  private unsafe BlueprintLivePlan CaptureSpectatorPlan(){
   int player=spectatorPlayer;
   if(player<1 || player>8 || !GamePlayerManagerAPI.Instance.IsAIPlayer(player))return null;
   var field=typeof(EngineInterface).GetField("threadLock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
   if(field==null || field.FieldType!=typeof(object))throw new MissingFieldException("EngineInterface.threadLock contract changed.");
   object gate=field.GetValue(null);if(gate==null)return null;
   lock(gate){
    var merit=EngineInterface.GetMeritData();if(merit[player,0]!=player || merit[player,1]<0)return null;
    // The village can outlive its Keep. Validate the current 1-based building ID,
    // including owner and live type, before publishing the retained plan.
    int keepId=GamePlayerManagerAPI.Instance.GetPlayerKeepId(player);
    var buildings=GameBuildingManagerAPI.Instance;
    if(!buildings.IsValidId(keepId) || !buildings.TryGetBuildingById(keepId,out GameBuilding* keep) ||
       keep->r_PlayerIdOwner!=player || !IsKeep(keep->r_BuildingType) || keep->r_AliveState!=AliveState.IsAlive)return null;
    var api=GameAIVManagerAPI.Instance;
    if(!api.TryGetVillageSlotByPlayerId(player,out int slot) || !api.TryGetVillageByPlayerId(player,out AivVillageState* village) || village->OwnerPlayerId!=player || village->LayoutSelectionState==AivLayoutSelectionState.None)return null;
    if(village->Rotation!=SHCDESE.Interop.Enums.AivRotation.South && village->Rotation!=SHCDESE.Interop.Enums.AivRotation.East && village->Rotation!=SHCDESE.Interop.Enums.AivRotation.North && village->Rotation!=SHCDESE.Interop.Enums.AivRotation.West)return null;
    var steps=api.GetBuildSteps(slot);int maximum=village->MaximumBuildStep;
    if(maximum<0 || maximum>=steps.Length)return null;
    var result=new List<BlueprintLiveStep>();
    for(int frame=0;frame<=maximum;frame++){
     var step=steps[frame];
     if(!BlueprintSpectatorPolicy.IncludeStep(step.State))continue;
     if(step.BuildingType==0 || step.TileCount==0)continue;
     if(GameAIVManagerAPI.UsesOrderedMapTileBuffer(step.BuildingType)){
      int count=api.GetOrderedMapTiles(slot).Length;
      if(step.MapTileIdOrBufferIndex<0 || step.MapTileIdOrBufferIndex>count-step.TileCount)return null;
     }
     var tiles=api.GetBuildStepMapTiles(slot,frame);var positions=new List<BlueprintWorldTile>();
     foreach(int tile in tiles){if(tile<=0 || tile>=GameTileManagerView.NativePackedTileCapacity)continue;var p=GameTileManagerAPI.Instance.GetTileVectorFromId(tile);positions.Add(new BlueprintWorldTile(p.X,p.Y));}
     if(positions.Count>0 && positions.Count==tiles.Length)result.Add(new BlueprintLiveStep((int)step.BuildingType,positions.ToArray()));
    }
    return result.Count==0?null:new BlueprintLivePlan(player,slot,village->SelectedVariantIndex,village->LayoutOriginX,village->LayoutOriginY,village->KeepX,village->KeepY,village->Rotation==SHCDESE.Interop.Enums.AivRotation.East?AIVParser.Core.AivRotation.Degrees90:village->Rotation==SHCDESE.Interop.Enums.AivRotation.North?AIVParser.Core.AivRotation.Degrees180:village->Rotation==SHCDESE.Interop.Enums.AivRotation.West?AIVParser.Core.AivRotation.Degrees270:AIVParser.Core.AivRotation.Degrees0,result.ToArray());
   }
  }
  private void UpdateSpectatorBlueprint(){
   if(!HasSpectatorIdentity || !EffectiveBlueprintMode)return;
   spectatorSampling=spectatorRequested || Hud.SettingsPanelVisible;
   if(Interlocked.Exchange(ref spectatorDirty,0)==0)return;
   try{
    var current=CaptureSpectatorPlan();
    RefreshSpectatorAivName(current);
    controlledKeepAvailable=current!=null;
    if(current==null){spectatorPlan=null;layout=null;renderer.Clear();blueprintVisible=false;RefreshHud();return;}
    if(!spectatorRequested){spectatorPlan=current;layout=null;RefreshHud();return;}
    if(current.SameGeometry(spectatorPlan) && layout!=null){RefreshHud();return;}
    spectatorPlan=current;layout=current.Build(settings.GetBlueprintDisplayOptions());
    layoutKeepX=current.KeepX;layoutKeepY=current.KeepY;renderer.Clear();blueprintVisible=false;
    renderer.PreloadDepthCaptures(layout);
    if(spectatorRequested)SetBlueprintVisible(true,"spectator AI plan");else RefreshHud();
   }catch(Exception error){spectatorAivFileName=null;spectatorPlan=null;layout=null;renderer.Clear();blueprintVisible=false;controlledKeepAvailable=false;RefreshHud();Shared.DebugLogHelper.LogErrorLimited(log,"SpectatorBlueprint","AI Blueprint unavailable: "+error);}
  }
  private void ToggleSpectatorBlueprint(){
   spectatorRequested=!spectatorRequested;
   if(!spectatorRequested){SetBlueprintVisible(false,"spectator toggle");spectatorSampling=Hud.SettingsPanelVisible;return;}
   Interlocked.Exchange(ref spectatorDirty,1);UpdateSpectatorBlueprint();
   if(layout!=null && !blueprintVisible)SetBlueprintVisible(true,"spectator toggle");
  }
 }
}

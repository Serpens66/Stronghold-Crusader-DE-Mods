using System;
using System.Collections.Generic;
using System.Linq;
using AIVParser.Core;
using Rotation = AIVParser.Core.AivRotation;
namespace CastlePlanner {
 internal sealed class BlueprintLiveStep {
  internal readonly int Mapper; internal readonly BlueprintWorldTile[] Positions;
  internal BlueprintLiveStep(int mapper,BlueprintWorldTile[] positions){Mapper=mapper;Positions=(BlueprintWorldTile[])positions.Clone();}
 }
 internal sealed class BlueprintLivePlan {
  internal readonly int Player,Slot,Variant,OriginX,OriginY,KeepX,KeepY;
  internal readonly Rotation Rotation;
  internal readonly BlueprintLiveStep[] Steps;
  internal BlueprintLivePlan(int player,int slot,int variant,int x,int y,int keepX,int keepY,Rotation rotation,BlueprintLiveStep[] steps){Player=player;Slot=slot;Variant=variant;OriginX=x;OriginY=y;KeepX=keepX;KeepY=keepY;Rotation=rotation;Steps=(BlueprintLiveStep[])steps.Clone();}
  internal bool SameGeometry(BlueprintLivePlan other)=>other!=null && Player==other.Player && Slot==other.Slot && Variant==other.Variant && OriginX==other.OriginX && OriginY==other.OriginY && KeepX==other.KeepX && KeepY==other.KeepY && Rotation==other.Rotation && Steps.Length==other.Steps.Length && Steps.Zip(other.Steps,(a,b)=>a.Mapper==b.Mapper && a.Positions.SequenceEqual(b.Positions)).All(v=>v);
  internal static AivGridPoint UnprojectOrigin(BlueprintWorldTile world,int size,int originX,int originY,Rotation rotation){
   int maxRow=AivGridPoint.GridSize-1-(world.Y-originY),minColumn=world.X-originX;
   Rotation inverse=rotation==Rotation.Degrees90?Rotation.Degrees270:rotation==Rotation.Degrees270?Rotation.Degrees90:rotation;
   var corners=new[]{new AivGridPoint(maxRow,minColumn),new AivGridPoint(maxRow-size+1,minColumn),new AivGridPoint(maxRow,minColumn+size-1),new AivGridPoint(maxRow-size+1,minColumn+size-1)}.Select(p=>AivGridTransform.Rotate(p,inverse)).ToArray();
   return new AivGridPoint(corners.Max(p=>p.Row),corners.Min(p=>p.Column));
  }
  internal BlueprintLayout Build(AivSpawnOptions options){
   // Reconstruct only accepted placements, never decode into Vanilla's shared working grids.
   var frames=new List<AivJsonFrame>{new AivJsonFrame {itemType=(int)SHCDESE.Interop.eMappers.MAPPER_KEEP2,tilePositionOfsets=new List<int>{(AivGridPoint.GridSize/2)*AivGridPoint.GridSize+AivGridPoint.GridSize/2}}};
   foreach(var step in Steps){var mapper=AivMapperCatalog.Resolve(step.Mapper);if(AivMapperCatalog.IsKeep(step.Mapper))continue;
    var positions=new List<int>();foreach(var tile in step.Positions){var raw=UnprojectOrigin(tile,mapper.FootprintSize??1,OriginX,OriginY,Rotation);positions.Add(raw.Row*AivGridPoint.GridSize+raw.Column);}
    frames.Add(new AivJsonFrame {itemType=step.Mapper,tilePositionOfsets=positions});
   }
   var document=AivSpawnPlan.Filter(new AivJsonDocument {frames=frames,miscItems=new List<AivJsonMiscItem>()},options);
   var offset=AivWorldTransform.ProjectNativeFit(new AivGridPoint(AivGridPoint.GridSize-1,0),0,0,Rotation.Degrees0);
   var built=BlueprintLayoutBuilder.Build(document,OriginX-offset.X,OriginY-offset.Y,Rotation,BlueprintProjectionMode.NativeFixedGrid);
   return new BlueprintLayout(built.Tiles,built.Icons,built.UnknownMapperCount,Rotation,new BlueprintWorldTile(KeepX,KeepY));
  }
 }
}

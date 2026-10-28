using System;
using System.Collections.Generic;
using System.Linq;
using AIVParser.Core;
using CastlePlanner;
using AivJsonDocument=CastlePlanner.AivJsonDocument;
using AivJsonFrame=CastlePlanner.AivJsonFrame;
using AivJsonMiscItem=CastlePlanner.AivJsonMiscItem;
internal static class SpectatorBlueprintTests {
 internal static void Run(){
  int checks=0;
  void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
  int ticks=0;for(int i=1;i<=120;i++)Check(BlueprintSpectatorPolicy.ShouldSample(ref ticks,true)==(i%40==0),"snapshot scheduling is once per 40 ticks");
  for(int i=0;i<80;i++)Check(!BlueprintSpectatorPolicy.ShouldSample(ref ticks,false),"no sampling when closed");
  foreach(var st in Enum.GetValues<SHCDESE.Interop.Enums.AivBuildStepState>())Check(BlueprintSpectatorPolicy.IncludeStep(st)==(st!=SHCDESE.Interop.Enums.AivBuildStepState.Inactive && st!=SHCDESE.Interop.Enums.AivBuildStepState.Abandoned),"mirror native rejection of inactive/abandoned steps");
  foreach(AivRotation rotation in Enum.GetValues(typeof(AivRotation)))
  foreach(int size in new[]{1,2,3,5,8})
  foreach(var raw in new[]{new AivGridPoint(20,20),new AivGridPoint(50,43),new AivGridPoint(90,80)}){
   var offset=AivWorldTransform.ProjectNativeFit(new AivGridPoint(99,0),0,0,AivRotation.Degrees0);
   var world=AivNativeBuildingPlacement.ResolveBuildStructureOrigin(raw,size,200-offset.X,300-offset.Y,rotation);
   var restored=BlueprintLivePlan.UnprojectOrigin(new BlueprintWorldTile(world.X,world.Y),size,200,300,rotation);
   Check(restored.Row==raw.Row && restored.Column==raw.Column,"prepared footprint must unproject exactly in every rotation");
  }
  foreach(AivRotation rotation in Enum.GetValues(typeof(AivRotation))){
   var origin=AivWorldTransform.ProjectNativeFit(new AivGridPoint(99,0),0,0,AivRotation.Degrees0);
   int referenceX=200-origin.X,referenceY=300-origin.Y;
   int[] types={(int)SHCDESE.Interop.eMappers.MAPPER_WALL,(int)SHCDESE.Interop.eMappers.MAPPER_PITCH_DITCH,(int)SHCDESE.Interop.eMappers.MAPPER_MOAT,(int)SHCDESE.Interop.eMappers.MAPPER_GATE_STONE1A,(int)SHCDESE.Interop.eMappers.MAPPER_GATE_STONE2A,(int)SHCDESE.Interop.eMappers.MAPPER_DRAWBRIDGE,(int)SHCDESE.Interop.eMappers.MAPPER_STAIR1,(int)SHCDESE.Interop.eMappers.MAPPER_TOWER1};
   var steps=new List<BlueprintLiveStep>();var frames=new List<AivJsonFrame>{new AivJsonFrame {itemType=(int)SHCDESE.Interop.eMappers.MAPPER_KEEP2,tilePositionOfsets=new List<int>{5050}}};
   for(int i=0;i<types.Length;i++){var raw=new AivGridPoint(20+(i/5)*20,12+(i%5)*15);var pos=AivNativeBuildingPlacement.ResolveBuildStructureOrigin(raw,AivMapperCatalog.Resolve(types[i]).FootprintSize??1,referenceX,referenceY,rotation);steps.Add(new BlueprintLiveStep(types[i],new[]{new BlueprintWorldTile(pos.X,pos.Y)}));frames.Add(new AivJsonFrame {itemType=types[i],tilePositionOfsets=new List<int>{raw.Row*100+raw.Column}});}
   var plan=new BlueprintLivePlan(2,7,3,200,300,250,350,rotation,steps.ToArray());
   var options=new AivSpawnOptions {SpawnFortifications=true,SpawnDefensiveGroundFeatures=true,SpawnBuildings=true,SpawnFearFactorBuildings=true};
   var actual=plan.Build(options);var expected=BlueprintLayoutBuilder.Build(new AivJsonDocument {frames=frames,miscItems=new List<AivJsonMiscItem>()},referenceX,referenceY,rotation,BlueprintProjectionMode.NativeFixedGrid);
   Check(actual.Tiles.Select(t=>t.Tile).OrderBy(t=>t.X).ThenBy(t=>t.Y).SequenceEqual(expected.Tiles.Select(t=>t.Tile).OrderBy(t=>t.X).ThenBy(t=>t.Y)),"live coordinates match renderer: rotation="+rotation+", missing="+string.Join(";",expected.Tiles.Select(t=>t.Tile).Except(actual.Tiles.Select(t=>t.Tile)).Select(t=>t.X+","+t.Y))+", types="+string.Join(";",types.Select(t=>t+":"+AivMapperCatalog.Resolve(t).IsKnown)));
   Check(actual.Icons.Select(i=>(i.MapperValue,i.MinimumWorldX,i.MaximumWorldX,i.MinimumWorldY,i.MaximumWorldY,i.AdjacentGateCenter,i.StairLowEnd,i.StairHighEnd)).SequenceEqual(expected.Icons.Select(i=>(i.MapperValue,i.MinimumWorldX,i.MaximumWorldX,i.MinimumWorldY,i.MaximumWorldY,i.AdjacentGateCenter,i.StairLowEnd,i.StairHighEnd))),"prepared gate/stair/tower icon geometry matches original projection");
   Check(plan.SameGeometry(new BlueprintLivePlan(2,7,3,200,300,250,350,rotation,steps.ToArray())),"unchanged plan reuses geometry");
   Check(!plan.SameGeometry(new BlueprintLivePlan(3,7,3,200,300,250,350,rotation,steps.ToArray())),"owner changes invalidate slot reuse");
   Check(!plan.SameGeometry(new BlueprintLivePlan(2,7,4,200,300,250,350,rotation,steps.ToArray())),"variant changes invalidate snapshot");
   Check(!plan.SameGeometry(new BlueprintLivePlan(2,7,3,201,300,250,350,rotation,steps.ToArray())),"layout relocation invalidates snapshot");
  }
  var names=new string[(int)SHCDESE.Interop.AILords.SK_JEWEL,8];
  int wolf=(int)SHCDESE.Interop.AILords.SK_WOLF-1;
  int jewel=(int)SHCDESE.Interop.AILords.SK_JEWEL-1;
  for(int i=0;i<8;i++){names[wolf,i]="wolf"+(i+1)+".aivjson";names[jewel,i]="jewel"+(i+1)+".aivjson";}
  Check(BlueprintAivNames.BuiltIn(wolf,5,BlueprintAivSet.Default,names,null)=="wolf6.aivjson","default variant keeps native zero-based import index");
  Check(BlueprintAivNames.BuiltIn(wolf,5,BlueprintAivSet.Community,names,null)=="Community_wolf6.aivjson","community source filename");
  Check(BlueprintAivNames.BuiltIn(wolf,0,BlueprintAivSet.Historical,names,null)=="Community_Historical_wolf.aivjson","historical source filename");
  Check(BlueprintAivNames.BuiltIn(wolf,1,BlueprintAivSet.Historical,names,null)==null,"historical bank has only its first variant");
  Check(BlueprintAivNames.BuiltIn(jewel,0,BlueprintAivSet.Historical,names,null)=="jewel1.aivjson","later lords do not invent nonexistent alternate sets");
  Check(BlueprintAivNames.BuiltIn(wolf,-1,BlueprintAivSet.Default,names,null)==null,"negative variant rejected");
  Check(BlueprintAivNames.BuiltIn(wolf,8,BlueprintAivSet.Default,names,null)==null,"out-of-bank variant rejected");
  Check(BlueprintAivNames.BuiltIn(-1,0,BlueprintAivSet.Default,names,null)==null,"unknown lord rejected");
  Check(BlueprintAivNames.BuiltIn(wolf,0,BlueprintAivSet.Default,null,null)==null,"missing source metadata stays unknown");
  Check(BlueprintAivNames.Candidate("Default 6",true,6,wolf,names,null)=="wolf6.aivjson","selected built-in uses checksum rather than translated label");
  Check(BlueprintAivNames.Candidate("Community 6",true,56,wolf,names,null)=="Community_wolf6.aivjson","mixed custom list preserves community source index");
  Check(BlueprintAivNames.Candidate("Historical",true,61,wolf,names,null)=="Community_Historical_wolf.aivjson","mixed custom list historical source");
  Check(BlueprintAivNames.Candidate("guess",true,999,wolf,names,null)==null,"unknown built-in checksum is not guessed");
  Check(BlueprintAivNames.Candidate("C:/Custom Lords/Nimrodwest2",false,999,wolf,names,null)=="Nimrodwest2.aivjson","custom name strips provider path and adds extension once");
  Check(BlueprintAivNames.FileName(@"C:\Lords\MyCastle.AIVJSON")=="MyCastle.AIVJSON","Windows paths and existing extension");
  Check(BlueprintAivNames.FileName("  ")==null,"empty name is unavailable");
  var imported=new[]{"Nimrodwest2.aivjson",null,"Nimrod1.aivjson"};
  Check(BlueprintAivNames.At(imported,0)=="Nimrodwest2.aivjson" && BlueprintAivNames.At(imported,2)=="Nimrod1.aivjson","restricted and reordered lists retain exact import order");
  Check(BlueprintAivNames.At(imported,1)==null && BlueprintAivNames.At(imported,3)==null && BlueprintAivNames.At(null,0)==null,"missing entries are not filtered into wrong native indices");
  int overrideCalls=0;
  string Replacement(int lord,int index){overrideCalls++;Check(lord==wolf && index==5,"override receives actual source lord and variant");return "AIV/SK_WOLF_5.aivjson";}
  Check(BlueprintAivNames.Candidate("Default 6",true,6,wolf,names,Replacement)=="SK_WOLF_5.aivjson" && overrideCalls==1,"registered Extender override wins without asset content reads");
  Console.WriteLine("PASS: "+checks+" prepared spectator Blueprint geometry checks.");
 }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class DirectionWatchTests
{
    internal static void Run(string root)
    {
        string path = "Pathfinding/src/Engine/AnchorRouting.cs";
        string Method(string text) => CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "WatchNativeDirections").ToFullString();
        string source = Fixture.Replace("/* METHODS */", Method(File.ReadAllText(Path.Combine(root, path))));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualDirectionWatch", new[] { CSharpSyntaxTree.ParseText(source) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
                optimizationLevel: OptimizationLevel.Release));
        using var bytes = new MemoryStream(); var result = compilation.Emit(bytes);
        if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics));
        Assembly.Load(bytes.ToArray()).GetType("WatchFixture")!.GetMethod("Run")!.Invoke(null, null);
    }

    private const string Fixture = """
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
public sealed class GameTileManagerAPI {
 public static GameTileManagerAPI Current; public static int Gets;
 public static GameTileManagerAPI Instance { get { Gets++;return Current; } }
 public int[] Prefix;public int Calls;
 public int GetTileId(int x,int y) {Calls++;return x+Prefix[y];}
}
public unsafe sealed class WatchFixture {
 const int MapWidth=800,NativeTileCount=320800,AnchorWatchNodesPerTick=40000,Total=640000;
 sealed class Runtime {public byte* nativeMovementMasks;}
 readonly Runtime runtime=new();
 int[] anchorWatchTiles;byte[] anchorWatchBytes;int anchorWatchCursor,anchorWatchPrimed;long anchorWatchChanges;
 readonly byte[] Memory=new byte[NativeTileCount];
 readonly GameTileManagerAPI Tiles=new();
 readonly List<int> Marks=new();
 int GetterCount,ThrowAfter;bool MutateFuture,Null,Record=true;
 static bool IsValidTileId(int id)=>id>=0&&id<NativeTileCount;
 void MarkFastTopologyTile(int tile,int radius) {
  if(radius!=1)throw new Exception("radius");
  if(Record)Marks.Add(tile);
  if(MutateFuture && tile+1<NativeTileCount)Memory[tile+1]++;
  if(ThrowAfter>0&&Marks.Count==ThrowAfter)throw new InvalidOperationException("mark");
 }
 /* METHODS */
 // Normative scalar oracle from accepted source 245bf45f20cad44dbf43c135988b33b40cdb7793.
 // Kept here so release/shallow source trees need no repository history. The contract is
 // 40000 coordinate slots including aliases: ordered single native read, shadow update,
 // immediate Mark before the next read; priming observes without marking. This deliberately
 // retains the simple per-node algorithm, independently of the candidate's row arithmetic.
 private void Original()
 {
  byte* masks = runtime.nativeMovementMasks;
  int total = MapWidth * MapWidth;
  if (masks == null || total <= 0) return;
  if (anchorWatchTiles == null || anchorWatchTiles.Length != total)
  { anchorWatchTiles = new int[total]; anchorWatchBytes = new byte[total]; anchorWatchCursor = anchorWatchPrimed = 0; }
  int end = Math.Min(total, anchorWatchCursor + AnchorWatchNodesPerTick);
  GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
  for (int node = anchorWatchCursor; node < end; node++)
  {
   if (node >= anchorWatchPrimed)
   {
    int id = tiles.GetTileId(node % MapWidth, node / MapWidth);
    anchorWatchTiles[node] = IsValidTileId(id) ? id : -1;
    anchorWatchBytes[node] = IsValidTileId(id) ? masks[id] : (byte)0;
    continue;
   }
   int tile = anchorWatchTiles[node];
   if (tile < 0) continue;
   byte current = masks[tile];
   if (current == anchorWatchBytes[node]) continue;
   anchorWatchBytes[node] = current; anchorWatchChanges++;
   MarkFastTopologyTile(tile, 1);
  }
  if (end > anchorWatchPrimed) anchorWatchPrimed = end;
  anchorWatchCursor = end >= total ? 0 : end;
 }

 static int[] NativeRows() {
  var p=new int[800];int packed=0;
  for(int y=0;y<800;y++) {int margin=y<400?399-y:y-400;p[y]=packed-margin;packed+=800-2*margin;}
  Check(packed==NativeTileCount,"native row terminal");return p;
 }
 WatchFixture(int[] rows) {Tiles.Prefix=(int[])rows.Clone();for(int i=0;i<Memory.Length;i++)Memory[i]=(byte)(i*13);}
 void Prepare(int cursor,int primed) {
  anchorWatchCursor=cursor;anchorWatchPrimed=primed;
  anchorWatchTiles=new int[Total];anchorWatchBytes=new byte[Total];
  for(int n=0;n<primed;n++) {int id=n%800+Tiles.Prefix[n/800];anchorWatchTiles[n]=IsValidTileId(id)?id:-1;anchorWatchBytes[n]=IsValidTileId(id)?Memory[id]:(byte)0;}
 }
 string Call(bool old) {
  GameTileManagerAPI.Current=Tiles;GameTileManagerAPI.Gets=0;
  try {fixed(byte* p=Memory) {runtime.nativeMovementMasks=Null?null:p;if(old)Original();else WatchNativeDirections();}return "ok";}
  catch(Exception ex) {return ex.GetType().Name+":"+ex.Message;}
  finally {GetterCount+=GameTileManagerAPI.Gets;runtime.nativeMovementMasks=null;}
 }
 static void Check(bool value,string why) {if(!value)throw new Exception(why);}
 static void Same(WatchFixture a,WatchFixture b,string why) {
  Check(a.anchorWatchCursor==b.anchorWatchCursor&&a.anchorWatchPrimed==b.anchorWatchPrimed&&a.anchorWatchChanges==b.anchorWatchChanges,why+" state");
  Check(a.GetterCount==b.GetterCount&&a.Tiles.Calls==b.Tiles.Calls,why+" getter order/count");
  Check(a.Marks.SequenceEqual(b.Marks)&&a.Memory.SequenceEqual(b.Memory),why+" effects/order");
  Check((a.anchorWatchTiles==null)==(b.anchorWatchTiles==null),why+" allocation");
  if(a.anchorWatchTiles!=null)Check(a.anchorWatchTiles.SequenceEqual(b.anchorWatchTiles)&&a.anchorWatchBytes.SequenceEqual(b.anchorWatchBytes),why+" shadows");
 }
 static void Step(WatchFixture a,WatchFixture b,string why) {Check(a.Call(false)==b.Call(true),why+" exception");Same(a,b,why);}
 static void Alter(WatchFixture a,WatchFixture b) {for(int i=0;i<NativeTileCount;i+=17){a.Memory[i]^=37;b.Memory[i]^=37;}}
 public static void Run() {
  int[] rows=NativeRows();var a=new WatchFixture(rows);var b=new WatchFixture(rows);
  a.Null=b.Null=true;Step(a,b,"null before allocation");a.Null=b.Null=false;
  for(int i=0;i<16;i++)Step(a,b,"prime chunk "+i);
  Check(a.anchorWatchPrimed==Total&&a.anchorWatchCursor==0&&a.Tiles.Calls==Total&&a.Marks.Count==0,"priming contract");
  Alter(a,b);for(int i=0;i<16;i++)Step(a,b,"warm aliases chunk "+i);
  Check(a.Marks.Count>0,"changed aliases exercised");
  a.Null=b.Null=true;Step(a,b,"null warm");a.Null=b.Null=false;
  a.anchorWatchTiles=b.anchorWatchTiles=null;a.anchorWatchBytes=b.anchorWatchBytes=null;Step(a,b,"reset reallocates and primes");
  foreach(var pair in new[]{(1987,2053),(39997,40003),(639995,640000),(123,123),(802,4000)}) {
   a=new WatchFixture(rows);b=new WatchFixture(rows);a.Prepare(pair.Item1,pair.Item2);b.Prepare(pair.Item1,pair.Item2);Alter(a,b);Step(a,b,"partial "+pair);
  }
  int[] synthetic=new int[800];Array.Fill(synthetic,NativeTileCount+1);
  synthetic[0]=-8;synthetic[1]=NativeTileCount-4;synthetic[2]=0;synthetic[3]=-900;
  a=new WatchFixture(synthetic);b=new WatchFixture(synthetic);a.Prepare(0,Total);b.Prepare(0,Total);
  Alter(a,b);Step(a,b,"numeric prefix suffix zero and all invalid rows");
  // A callback changes the next native byte. A separate equality/read-ahead pass fails this case.
  a=new WatchFixture(rows);b=new WatchFixture(rows);a.Prepare(320000,Total);b.Prepare(320000,Total);
  int first=rows[400];a.Memory[first]++;b.Memory[first]++;a.MutateFuture=b.MutateFuture=true;
  Step(a,b,"callback mutates future native byte");Check(a.Marks.Count>=40000,"mutation chain observed immediately");
  a=new WatchFixture(rows);b=new WatchFixture(rows);a.Prepare(39999,Total);b.Prepare(39999,Total);Alter(a,b);
  a.ThrowAfter=b.ThrowAfter=3;Step(a,b,"throw leaves partial shadow and old cursor");
  Check(a.anchorWatchCursor==39999&&a.Marks.Count==3,"exception commit boundary");
  a.ThrowAfter=b.ThrowAfter=0;Step(a,b,"retry skips already committed shadows");
  // Informational host runtime timing only; installed Mono measurement decides the optimization.
  a=new WatchFixture(rows);b=new WatchFixture(rows);a.Prepare(0,Total);b.Prepare(0,Total);a.Record=b.Record=false;
  for(int i=0;i<32;i++){a.Call(false);b.Call(true);}
  var sw=Stopwatch.StartNew();for(int i=0;i<160;i++)a.Call(false);sw.Stop();double candidate=sw.Elapsed.TotalMilliseconds;
  sw.Restart();for(int i=0;i<160;i++)b.Call(true);sw.Stop();
  Console.WriteLine($"DirectionWatch actual-method parity PASS; host warm 160 calls candidate={candidate:F3}ms baseline={sw.Elapsed.TotalMilliseconds:F3}ms (informational)");
 }
}
""";
}

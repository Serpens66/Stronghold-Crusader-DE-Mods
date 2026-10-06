using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Offline;
internal static unsafe class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if(args.Length==1 && args[0]=="--self-test") { SelfTest(); return 0; }
            if(args.Length<5) { Console.WriteLine("Usage: Offline <geometry.json> <validate|enumerate> <leaderX> <leaderY> <playerId>. Results are scenario selection only."); return 2; }
            var document=(Dictionary<string,object>)Shared.DependencyFreeJson.Parse(File.ReadAllText(args[0]));
            using var model=new SearchModel(document);
            int x=int.Parse(args[2]),y=int.Parse(args[3]),player=int.Parse(args[4]);
            if(args[1]=="validate")
            {
                int tile=model.Find(x,y,player,false);
                var actual=model.Tile(Convert.ToInt32(document["observedOriginX"])+1,Convert.ToInt32(document["observedOriginY"])+1);
                bool matches=tile==actual && model.ProducerAccepts(tile);
                Console.WriteLine(Shared.DependencyFreeJson.Serialize(new Dictionary<string,object> { ["modelMatches"] = matches,
                    ["expectedCenter"] = actual, ["computedCenter"] = tile, ["status"] = "INCONCLUSIVE",
                    ["reason"] = "Match validates this observed search only; exact producer leader and pre-search state must be independently verified." }));
                return matches ? 0 : 1;
            }
            if(args[1]!="enumerate") throw new ArgumentException("Unknown mode.");
            var candidates=new List<object>();
            ushort[] initial=(ushort[])model.Structures.Clone();
            var initialBuildings=GameBuildingManagerAPI.Instance.Buildings;
            // Natural first-tent candidates and several later crew origins yield small staging geometries.
            for(int firstY=-4;firstY<=4;firstY+=2) for(int firstX=-4;firstX<=4;firstX+=2)
            {
                initial.CopyTo(model.Structures,0); GameBuildingManagerAPI.Instance.Buildings=initialBuildings;
                int first=model.Find(x+firstX,y+firstY,player,false);
                if(!model.ProducerAccepts(first))continue;
                model.AddHypotheticalTent(first,player);
                for(int dy=-2;dy<=2;dy+=2)for(int dx=-2;dx<=2;dx+=2)
                {
                    if(model.Tile(x+dx,y+dy)<0)continue;
                    int original=model.Find(x+dx,y+dy,player,false),alternative=model.Find(x+dx,y+dy,player,true);
                    if(original==alternative || !model.ProducerAccepts(original) || !model.ProducerAccepts(alternative))continue;
                    var a=model.Position(original); var b=model.Position(alternative); var f=model.Position(first);
                    candidates.Add(new Dictionary<string,object> { ["firstCrewX"]=x+firstX,["firstCrewY"]=y+firstY,
                        ["hypotheticalFirstTentCenterX"]=f.x,["hypotheticalFirstTentCenterY"]=f.y,
                        ["crewX"]=x+dx,["crewY"]=y+dy,["originalCenterX"]=a.x,["originalCenterY"]=a.y,
                        ["counterfactualCenterX"]=b.x,["counterfactualCenterY"]=b.y });
                }
            }
            Console.WriteLine(Shared.DependencyFreeJson.Serialize(new Dictionary<string,object> { ["candidates"]=candidates,["count"]=candidates.Count,
                ["status"]="INCONCLUSIVE", ["modelValidatedForThisState"]=false,["stopMapExperiments"]=candidates.Count==0,
                ["reason"]="Counterfactual only changes two validity guards. Neither different placement nor model match proves blocked engineers." }));
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("INCONCLUSIVE: " + ex); return 1; }
    }
    private static void SelfTest()
    {
        if(!GamePlayerManagerAPI.Instance.IsPlayerIdValid(1) || GamePlayerManagerAPI.Instance.IsPlayerIdValid(0))throw new Exception("Player contract.");
        // Callback rejection can be tested independently of native search coordinates.
        int[] rows=Enumerable.Range(0,2400).Select(i=>i/3*200).ToArray(); ushort[] columns=Enumerable.Range(0,320800).Select(i=>(ushort)(i/200)).ToArray();
        fixed(int* r=rows) fixed(ushort* c=columns)
        {
            GameTileManagerAPI.Instance=new GameTileManagerAPI { MapRowLookupTable=r,MapColumnLookupTable=c,TileManager=new TileView{StructureGrid=new ushort[320800]} };
            GamePlayerManagerAPI.Instance.Players[1].r_SiegeAttackTargetPlayerId=2;
            GamePlayerManagerAPI.Instance.Players[2].r_KeepDoorTilePositionX=100;
            GamePlayerManagerAPI.Instance.Players[2].r_KeepDoorTilePositionY=40;
            GameBuildingManagerAPI.Instance.Buildings=new[]{new GameBuilding{r_PlayerIdOwner=1,r_BuildingType=eStructs.STRUCT_SIEGE_TENT_CATAPULT,r_TilePositionXBegin=39,r_TilePositionYBegin=39}};
            GameTileManagerAPI.Instance.TileManager.StructureGrid[40*200+40]=1;
            int cell=40*200+46;
            if(OriginalFixesCallback.Evaluate(cell,cell,1)!=1 || CounterfactualFixesCallback.Evaluate(cell,cell,1)!=0) throw new Exception("Directional counterfactual mismatch.");
            if(OriginalFixesCallback.Evaluate(40*200+41,40*200+41,1)!=0)throw new Exception("Working 5x5 gap check regressed.");
        }
        SelfTestSearch();
        Console.WriteLine("PASS: unchanged callback and exactly two guard changes; synthetic fixture is not native search validation or gameplay proof.");
    }
    private static void SelfTestSearch()
    {
        // Small flat deterministic map: first site is five northward steps from the leader.
        var rows=Enumerable.Range(0,2400).Select(i=>i/3*200).ToArray();
        var columns=Enumerable.Range(0,320800).Select(i=>(ushort)(i/200)).ToArray();
        var neighbors=new int[6400]; var edges=new byte[320800]; var active=new byte[640000];
        int[] dx={0,1,1,1,0,-1,-1,-1};
        for(int y=0;y<800;y++)for(int d=0;d<8;d++)neighbors[y*8+d]=dx[d]+NativeDirections.Y[d]*200;
        for(int y=10;y<90;y++)for(int x=10;x<90;x++) { active[y*800+x]=1; if(x>10 && x<89 && y>10 && y<89)edges[y*200+x]=255; }
        var document=new Dictionary<string,object>{["nativeHash"]=SearchModel.Hash,
            ["rows"]=Box(rows),["columns"]=Box(columns),["neighbors"]=Box(neighbors),["edges"]=Box(edges),
            ["logic"]=Box(new int[320800]),["structures"]=Box(new ushort[320800]),["units"]=Box(new ushort[320800]),
            ["heights"]=Box(new byte[320800]),["metric"]=Box(Enumerable.Repeat((short)60,320800)),
            ["treeAllowed"]=Box(new byte[320800]),["activeCoordinates"]=Box(active),["tents"]=new List<object>(),
            ["player"]=new Dictionary<string,object>{["playerId"]=1,["targetPlayerId"]=2,["targetKeepX"]=80,["targetKeepY"]=50}};
        using var model=new SearchModel(document);
        int first=model.Find(50,50,1,false);
        if(first!=45*200+50 || !model.ProducerAccepts(first))throw new Exception("Search order/depth/origin contract.");
        model.Heights[44*200+50]=12;
        if(model.Find(50,50,1,false)==first)throw new Exception("Footprint height range accepted at boundary12.");
        model.Heights[44*200+50]=0;
        model.AddHypotheticalTent(first,1);
        int second=model.Find(50,50,1,false);
        if(second==first || second==0)throw new Exception("Working tent gap or occupied footprint regressed.");
        System.Array.Fill(model.Metric,(short)59);
        if(model.Find(50,50,1,false)!=0)throw new Exception("Native player metric filter ignored.");
        Console.WriteLine("PASS: modeled FIFO order, depth, origin, height boundary, occupancy/gap and player metric; synthetic only.");
    }
    private static List<object> Box<T>(IEnumerable<T> values)=>values.Select(v=>(object)v).ToList();
}

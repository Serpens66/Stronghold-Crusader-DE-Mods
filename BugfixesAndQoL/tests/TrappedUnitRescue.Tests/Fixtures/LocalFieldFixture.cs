using System;
using System.Collections.Generic;

namespace TrappedUnitRescueTest.Tests
{
    internal static class LocalFieldFixture
    {
        private static int checks;
        private static readonly int[] Dx={0,1,1,1,0,-1,-1,-1},Dy={-1,-1,0,1,1,1,0,-1};
        private static void Require(bool value,string why)
        {checks++;if(!value)throw new InvalidOperationException("LocalFieldFixture: "+why);}
        private sealed class World
        {
            internal Func<int,int,int> Map=(x,y)=>x>=0 && x<31 && y>=0 && y<31?y*31+x:-1;
            internal readonly HashSet<int> Blocked=new HashSet<int>();
            internal readonly Dictionary<int,byte> Links=new Dictionary<int,byte>();
            internal byte DefaultLinks=255;
            internal int GroundReads,ConnectionReads;
            internal bool Ground(int tile){GroundReads++;return !Blocked.Contains(tile);}
            internal byte Connections(int tile){ConnectionReads++;return Links.TryGetValue(tile,out byte value)?value:DefaultLinks;}
            internal bool Edge(int source,int x,int y,int d)
            {int target=Map(x+Dx[d],y+Dy[d]);return target>=0 && ((Connections(source)&(1<<d))!=0 || (Connections(target)&(1<<(d^4)))!=0);}
        }
        private static void Equal(List<RescueCandidate> expected,List<RescueCandidate> actual,string why)
        {
            Require(expected.Count==actual.Count,why+" count");
            for(int i=0;i<expected.Count;i++)
            {
                var a=expected[i];var b=actual[i];
                Require(a.X==b.X && a.Y==b.Y && a.Tile==b.Tile && a.Distance==b.Distance,why+" ordered candidate"+i);
            }
        }
        private static RescueLocalField Compare(World world,int x,int y,string why)
        {
            var expected=RescueRuntime.FindLocalCandidates(x,y,world.Map,world.Ground,world.Edge);
            Require(RescueLocalField.TryCapture(x,y,world.Map(x,y),world.Map,world.Ground,world.Connections,out var field,out string reason),why+" capture "+reason);
            int groundReads=world.GroundReads,connectionReads=world.ConnectionReads;
            Require(field.TryFindCandidates(world.Map,out var actual,out reason),why+" find "+reason);
            Require(world.GroundReads==groundReads && world.ConnectionReads==connectionReads,why+" no live terrain readers");
            Equal(expected,actual,why);
            var encoded=field.Export();
            Require(encoded.Length==326 && (encoded[36]&254)==0,why+" canonical326 bytes");
            Require(RescueLocalField.TryRestore(x,y,world.Map(x,y),encoded,world.Map,out var restored,out reason),why+" restore "+reason);
            Require(restored.TryFindCandidates(world.Map,out actual,out reason),why+" restored find");Equal(expected,actual,why+" roundtrip");
            return field;
        }
        internal static void Run()
        {
            var open=new World();var old=Compare(open,15,15,"open cyclic eight-neighbour field");
            Require(old.TryFindCandidates(open.Map,out var all,out _) && all.Count==288,"full radius8 excludes origin");
            Require(all[0].X==15 && all[0].Y==14 && all[1].X==16 && all[1].Y==14,"old DxDy FIFO order");
            var wall=new World();for(int y=0;y<31;y++)wall.Blocked.Add(wall.Map(16,y));Compare(wall,15,15,"disconnected wall");
            var asymmetric=new World{DefaultLinks=4};var line=Compare(asymmetric,15,15,"asymmetric east-only native bytes");
            Require(line.TryFindCandidates(asymmetric.Map,out var corridor,out _) && corridor.Count==16,"opposite-end OR gives both halves of corridor");
            var isolated=new World{DefaultLinks=0};Compare(isolated,15,15,"interrupted all edges");
            var maze=new World{DefaultLinks=0};
            for(int y=7;y<=23;y++)for(int x=7;x<=23;x++)
            {int id=maze.Map(x,y);if((x+y)%7==0)maze.Blocked.Add(id);maze.Links[id]=(byte)((x%3==0?1:4)|(y%4==0?16:64));}
            maze.Blocked.Remove(maze.Map(15,15));Compare(maze,15,15,"mixed cyclic fragmented field");
            var illegal=new World();illegal.Blocked.Add(illegal.Map(15,15));Compare(illegal,15,15,"valid tile but illegal origin ground");
            var diamond=Diamond();Compare(diamond,400,200,"diamond apex packed row prefixes");
            Compare(diamond,500,400,"400-map valid coordinate beyond mapSize rectangle");
            Compare(open,0,0,"global axes clipped");
            FrozenGeometry(open,old);Codec(diamond);Failures(open,old);
            Console.WriteLine("PASS LocalFieldFixture "+checks+" checks");
        }
        private static World Diamond()
        {
            var starts=new int[800];int cursor=0;
            for(int y=0;y<800;y++)
            {int half=200-Math.Abs(y-400);if(half<0)continue;starts[y]=cursor-(400-half);cursor+=half*2+1;}
            return new World{Map=(x,y)=>(uint)x<800 && (uint)y<800 && Math.Abs(x-400)+Math.Abs(y-400)<=200?x+starts[y]:-1};
        }
        private static void FrozenGeometry(World world,RescueLocalField field)
        {
            Require(field.TryFindCandidates(world.Map,out var expected,out _),"old field before live mutation");
            world.Blocked.Add(world.Map(15,15));world.DefaultLinks=0;
            Require(RescueRuntime.FindLocalCandidates(15,15,world.Map,world.Ground,world.Edge).Count==0,"live field actually changed");
            Require(field.TryFindCandidates(world.Map,out var actual,out _),"old field survives live terrain change");Equal(expected,actual,"immutable before geometry");
            byte[] outside=field.Export();Array.Clear(outside,0,outside.Length);
            Require(field.TryFindCandidates(world.Map,out actual,out _),"export cannot mutate retained field");Equal(expected,actual,"export defensive copy");
            world.Blocked.Clear();world.DefaultLinks=255;
        }
        private static void Codec(World world)
        {
            var field=Compare(world,400,200,"codec border");byte[] data=field.Export();int origin=world.Map(400,200);
            Require(RescueLocalField.TryRestore(400,200,origin,data,world.Map,out var restored,out _),"valid immutable restore");
            Array.Clear(data,0,data.Length);
            Require(field.TryFindCandidates(world.Map,out var expected,out _),"original old geometry readable");
            Require(restored.TryFindCandidates(world.Map,out var actual,out _),"restored old geometry readable");
            Equal(expected,actual,"restore defensive copy");
            for(int bit=1;bit<8;bit++)
            {
                data=field.Export();data[36]|=(byte)(1<<bit);
                Require(!RescueLocalField.TryRestore(400,200,origin,data,world.Map,out var rejected,out var reason) && rejected==null && reason=="local-field-unused-bits","each unused bit rejected");
            }
            data=field.Export();data[0]|=1;
            Require(!RescueLocalField.TryRestore(400,200,origin,data,world.Map,out _,out var invalid) && invalid=="local-field-invalid-cell","invalid map cell cannot claim ordinary");
            data=field.Export();data[37]=1;
            Require(!RescueLocalField.TryRestore(400,200,origin,data,world.Map,out _,out invalid) && invalid=="local-field-invalid-cell","invalid map cell cannot retain connection byte");
            foreach(var bad in new[]{null,new byte[325],new byte[327]})
                Require(!RescueLocalField.TryRestore(400,200,origin,bad,world.Map,out _,out invalid) && invalid=="local-field-length","strict codec length");
        }
        private static void Failures(World world,RescueLocalField field)
        {
            int origin=world.Map(15,15);
            Require(!RescueLocalField.TryCapture(15,15,origin+1,world.Map,world.Ground,world.Connections,out _,out _),"capture origin identity");
            Require(!RescueLocalField.TryRestore(15,15,origin+1,field.Export(),world.Map,out _,out _),"restore origin identity");
            Require(!field.TryFindCandidates((x,y)=>world.Map(x,y)+1,out var empty,out _) && empty.Count==0,"find rejects changed origin layout");
            Func<int,int,int> alias=(x,y)=>x==16 && y==15?origin:world.Map(x,y);
            Require(!RescueLocalField.TryCapture(15,15,origin,alias,world.Ground,world.Connections,out _,out _),"capture rejects packed ID aliases");
            Require(!field.TryFindCandidates(alias,out empty,out _) && empty.Count==0,"index BFS cannot accept alias diverging from old tile seen set");
            Require(!RescueLocalField.TryRestore(15,15,origin,field.Export(),alias,out _,out _),"restore rejects alias");
            Require(!RescueLocalField.TryCapture(31,15,-1,world.Map,world.Ground,world.Connections,out _,out _),"invalid origin rejected");
            Require(RescueRuntime.FindLocalCandidates(31,15,world.Map,world.Ground,world.Edge).Count==0,"old invalid origin is also empty");
            int calls=0;
            Require(!RescueLocalField.TryCapture(15,15,origin,world.Map,_=>{if(++calls==17)throw new InvalidOperationException();return true;},world.Connections,out var partial,out _) && partial==null,"interrupted ground capture exposes no partial field");
            calls=0;
            Require(!RescueLocalField.TryCapture(15,15,origin,world.Map,world.Ground,_=>{if(++calls==17)throw new InvalidOperationException();return 255;},out partial,out _) && partial==null,"interrupted connection capture exposes no partial field");
            Require(!field.TryFindCandidates((x,y)=>{if(x==17 && y==17)throw new InvalidOperationException();return world.Map(x,y);},out empty,out _) && empty.Count==0,"mapping failure has no partial candidates");
            Func<int,int,int> bad=(x,y)=>x==16 && y==15?NativeRelocation.TileCapacity:world.Map(x,y);
            Require(!field.TryFindCandidates(bad,out empty,out _) && empty.Count==0,"packed capacity enforced");
            Require(!RescueLocalField.TryCapture(15,15,origin,world.Map,null,world.Connections,out _,out _),"missing reader rejected");
            int reads=0;
            Require(RescueLocalField.TryCapture(15,15,origin,(x,y)=>{reads++;return world.Map(x,y);},world.Ground,world.Connections,out _,out _) && reads==290,"bounded mapping includes one independent origin verification");
        }
    }
}

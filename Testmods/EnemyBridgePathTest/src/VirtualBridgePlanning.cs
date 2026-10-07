using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // Copied planning state only. Not called by a live hook until its integration gates pass.
    internal sealed class VirtualPlanningInput
    {
        internal const int Capacity=320800;
        internal readonly int[] Flags,Rows,Offsets;
        internal readonly ushort[] Components,BuildingTypes;
        internal readonly short[] Y;
        internal readonly byte[] ClosedGateEdges,BuildingBlocks;
        internal readonly sbyte[] Coarse;
        internal VirtualPlanningInput(int[] flags,int[] rows,int[] offsets,ushort[] pcl,ushort[] types,short[] y,byte[] closedEdges,sbyte[] coarse,byte[] buildingBlocks)
        {
            if(flags.Length!=Capacity||pcl.Length!=Capacity||types.Length!=Capacity||y.Length!=Capacity||closedEdges.Length!=Capacity||rows.Length<800||offsets.Length<6400||coarse.Length!=25600)
                throw new ArgumentException("Incomplete planning capacity");
            Flags=(int[])flags.Clone();Rows=(int[])rows.Clone();Offsets=(int[])offsets.Clone();Components=(ushort[])pcl.Clone();BuildingTypes=(ushort[])types.Clone();Y=(short[])y.Clone();ClosedGateEdges=(byte[])closedEdges.Clone();Coarse=(sbyte[])coarse.Clone();
            if(buildingBlocks==null)throw new ArgumentException("Missing decision-time building blocking data");BuildingBlocks=(byte[])buildingBlocks.Clone();if(BuildingBlocks.Length!=Capacity)throw new ArgumentException("Building blocking capacity");
        }
        // Native raised overlay, not a generic blocked-cell planning filter.
        // Rebuilt components must come from an independently completed copied topology calculation.
        internal VirtualPlanningInput Raised(IEnumerable<int> deck,ushort[] rebuiltComponents)
        {
            if(rebuiltComponents==null||rebuiltComponents.Length!=Capacity)throw new ArgumentException("Missing rebuilt virtual components");
            var flags=(int[])Flags.Clone();var edges=(byte[])ClosedGateEdges.Clone();
            foreach(int tile in deck)
            {
                if((uint)tile>=Capacity)throw new ArgumentException("Unknown virtual deck tile");
                flags[tile]|=0x40000000;
                for(int direction=0;direction<8;direction++)
                {int next=Neighbor(tile,Y[tile],direction);edges[tile]&=unchecked((byte)~(1<<direction));edges[next]&=unchecked((byte)~(1<<((direction+4)&7)));}
            }
            return new VirtualPlanningInput(flags,Rows,Offsets,rebuiltComponents,BuildingTypes,Y,edges,Coarse,BuildingBlocks);
        }
        internal int Neighbor(int tile,int y,int direction)
        {if((uint)y>=800||(uint)direction>=8)throw new InvalidOperationException("Unresolved native row/direction");int n=unchecked(tile+Offsets[y*8+direction]);if((uint)n>=Capacity)throw new InvalidOperationException("Unresolved native neighbor");return n;}
    }
    internal sealed class VirtualPlanningState
    {
        internal readonly sbyte[] Seeds=new sbyte[VirtualPlanningInput.Capacity];
        internal readonly short[] Distance=new short[VirtualPlanningInput.Capacity],Visit=new short[VirtualPlanningInput.Capacity],QueueY=new short[VirtualPlanningInput.Capacity];
        internal readonly int[] Queue=new int[VirtualPlanningInput.Capacity];
        internal readonly List<int> Candidates=new List<int>();
        internal int Generation,Level,Head,Tail,BatchStart,BatchEnd,MaxDistance;
        internal long Steps;
        internal bool Complete;
        internal string Reason;
    }
    internal static class VirtualBridgePlanning
    {
        private static void Budget(VirtualPlanningState s,long budget)
        {if(++s.Steps>budget)throw new InvalidOperationException("planning-budget-exhausted");}
        // D95E0 R8 controls the 2000 versus 6000 queue limit; R9 is not read by this build.
        internal static bool Seed(VirtualPlanningInput input,VirtualPlanningState s,int targetTile,bool targetActive,bool shortLimit,long budget=2000000)
        {
            s.Complete=false;s.Reason="pending";
            try
            {
                if(!targetActive){s.Complete=true;s.Reason="inactive-target-preserved";return true;}
                if((uint)targetTile>=VirtualPlanningInput.Capacity)throw new InvalidOperationException("unresolved-target-seed");
                s.Level=1;s.Head=0;s.Tail=1;s.QueueY[0]=input.Y[targetTile];s.Queue[0]=targetTile;s.Seeds[targetTile]=1;int alternate=0;
                while(s.Head!=s.Tail)
                {
                    s.BatchStart=s.Head;
                    while(s.Head!=s.Tail)
                    {
                        Budget(s,budget);int tile=s.Queue[s.Head],y=s.QueueY[s.Head];
                        for(int direction=0;direction<8;direction++)
                        {
                            int n=input.Neighbor(tile,y,direction);
                            if(s.Seeds[n]>=1||input.Components[n]==0)continue;
                            int kind=input.BuildingTypes[n];
                            if((input.ClosedGateEdges[tile]&(1<<direction))==0&&kind!=45&&kind!=46)continue;
                            if((uint)s.Tail>=VirtualPlanningInput.Capacity)throw new InvalidOperationException("invalid-native-tail");
                            s.Seeds[n]=1;s.QueueY[s.Tail]=unchecked((short)(y+VirtualBridgeMap.Dy[direction]));s.Queue[s.Tail++]=n;
                            if(s.Tail>320799){s.Complete=true;s.Reason="native-tail-cap";return true;}
                        }
                        s.Head++;if(s.Head>320799)s.Head=0;
                        if(s.Tail>(shortLimit?1999:5999)){s.Complete=true;s.Reason="native-seed-limit";return true;}
                    }
                    s.Head=s.BatchStart;s.BatchEnd=s.Tail;
                    while(s.Head!=s.BatchEnd)
                    {
                        Budget(s,budget);int tile=s.Queue[s.Head],y=s.QueueY[s.Head];s.Level=s.Seeds[tile]+1;
                        for(int direction=0;direction<8;direction++)
                        {
                            int n=input.Neighbor(tile,y,direction),flags=input.Flags[n];
                            // C4BF0 closes gates temporarily; live gate deck state is supplied independently.
                            if(s.Seeds[n]>=1||(flags&0x31)!=0||input.BuildingBlocks[n]!=0)continue;
                            int value=s.Level;
                            if((flags&0x100)!=0)value+=6;else if((flags&0x40000000)!=0&&alternate!=0)value--;
                            s.Seeds[n]=unchecked((sbyte)value);
                        }
                        s.Head++;if(s.Head>320799)s.Head=0;
                    }
                    alternate=alternate==0?1:0;
                }
                s.Complete=true;s.Reason="native-seed-complete";return true;
            }
            catch(InvalidOperationException ex){s.Reason=ex.Message;return false;}
        }
        // The callback must be an offline/cached component oracle, never a new Vanilla search.
        internal static bool Distance(VirtualPlanningInput input,VirtualPlanningState s,int limit,int candidateDistance,int originComponent,Func<int,int,int?> regionResult,long budget=2000000)
        {
            s.Complete=false;s.Reason="pending";bool second=false;int deferred=0;
            try
            {
                do
                {
                    s.Generation=unchecked(s.Generation+1);if(s.Generation>32000){s.Generation=1;Array.Clear(s.Visit,0,s.Visit.Length);}
                    s.Level=1;s.Head=0;s.Tail=0;s.MaxDistance=0;
                    for(int tile=0;tile<VirtualPlanningInput.Capacity;tile++)if(s.Seeds[tile]==1)
                    {int q=s.Tail;s.QueueY[q]=input.Y[tile];s.Queue[q]=tile;s.Distance[tile]=1;s.Visit[tile]=unchecked((short)s.Generation);s.Tail++;if(s.Tail>320799)s.Tail=0;}
                    while(s.Head!=s.Tail)
                    {
                        Budget(s,budget);int tile=s.Queue[s.Head];if((uint)tile>=VirtualPlanningInput.Capacity)break;int y=s.QueueY[s.Head];
                        s.Level=s.Distance[tile];s.MaxDistance=Math.Max(s.MaxDistance,s.Level);if(limit<s.Level)break;
                        if(candidateDistance!=0&&s.Level==candidateDistance)
                        {
                            int x=tile-input.Rows[y];int coarse=(x/5*160+y/5);
                            if((uint)coarse>=input.Coarse.Length)throw new InvalidOperationException("unresolved-native-coarse-cell");
                            if(second||input.Coarse[coarse]<5)
                            {
                                int? reachable=regionResult?.Invoke(input.Components[tile],originComponent);if(!reachable.HasValue)throw new InvalidOperationException("unresolved-native-region-result");
                                if(reachable.Value!=0&&s.Candidates.Count<1000)s.Candidates.Add(tile);
                            }
                            else deferred++;
                        }
                        if((input.Flags[tile]&0x100031)==0)
                            for(int direction=0;direction<8;direction+=2)
                            {
                                int n=input.Neighbor(tile,y,direction);if(s.Visit[n]==s.Generation)continue;
                                s.Distance[n]=unchecked((short)(s.Level+1));s.Visit[n]=unchecked((short)s.Generation);
                                s.QueueY[s.Tail]=unchecked((short)(y+VirtualBridgeMap.Dy[direction]));s.Queue[s.Tail]=n;s.Tail++;if(s.Tail>320799)s.Tail=0;
                            }
                        s.Head++;if(s.Head>320799)s.Head=0;
                    }
                    if(candidateDistance==0||s.Candidates.Count!=0||second||deferred<1)break;
                    second=true;
                }while(true);
                s.Complete=true;s.Reason=second?"native-second-pass-complete":"native-distance-complete";return true;
            }
            catch(InvalidOperationException ex){s.Reason=ex.Message;return false;}
        }
    }
}

using System;

namespace EnemyBridgePathTest
{
    // E49D0's component flood on immutable, copied post-gate-closure input.
    // Does not run C5040/C4BF0 or modify native memory. Missing closure data
    // must be reported by the caller rather than treated as a negative proof.
    internal sealed class VirtualTopologyRebuild
    {
        private readonly VirtualBridgeMap map;
        private readonly int[] offsets, queue;
        private readonly short[] queueRows;
        private readonly int[] rowEnds;
        private int scan, scanRow, head, tail;
        internal readonly ushort[] Components;
        internal readonly int[] Counts = new int[1000];
        internal int NextComponent { get; private set; } = 1;
        internal int Total { get; private set; }
        internal bool Complete { get; private set; }
        internal bool Proven { get; private set; }
        internal string Reason { get; private set; } = "pending";
        internal long Work { get; private set; }
        private static readonly int[] Order = {6,2,0,7,1,4,5,3};
        internal VirtualTopologyRebuild(VirtualBridgeMap map, int[] directionOffsets, int[] nativeRowEnds)
        {
            this.map=map;offsets=(int[])directionOffsets.Clone();rowEnds=(int[])nativeRowEnds.Clone();
            Components=new ushort[map.Components.Length];queue=new int[Components.Length];queueRows=new short[Components.Length];
            if(Components.Length!=320800||offsets.Length!=6400||rowEnds.Length!=800||!map.Complete)
                Finish(false,"incomplete-topology-input");
        }
        private void Finish(bool proven,string reason)
        { Complete=true;Proven=proven;Reason=reason;for(int i=1;i<1000;i++)Total+=Counts[i]; }
        internal void Step(int budget)
        {
            if(budget<1)throw new ArgumentOutOfRangeException(nameof(budget));
            while(!Complete&&budget-->0)
            {
                Work++;
                if(head<tail)
                {
                    int at=queue[head];int row=queueRows[head++];Counts[NextComponent]++;
                    if((uint)row>=800){Finish(false,"invalid-flood-row");continue;}
                    int north=at+offsets[row*8],south=at+offsets[row*8+4];
                    foreach(int d in Order)
                    {
                        if((map.Edges[at]&(1<<d))==0)continue;
                        int next=d==6?at-1:d==2?at+1:d==0?north:d==7?north-1:d==1?north+1:d==4?south:d==5?south-1:south+1;
                        int nextRow=row+(d==0||d==7||d==1?-1:d==4||d==5||d==3?1:0);
                        if((uint)next>=(uint)Components.Length||(uint)nextRow>=800)
                        {Finish(false,"invalid-transition-or-queue-bound");break;}
                        if(Components[next]!=0)continue;
                        if(tail>=queue.Length){Finish(false,"queue-bound");break;}
                        Components[next]=(ushort)NextComponent;queue[tail]=next;queueRows[tail++]=(short)nextRow;
                    }
                    if(!Complete&&head==tail){head=tail=0;if(++NextComponent>999)Finish(false,"native-component-limit");}
                    continue;
                }
                if(scan==Components.Length){Finish(true,"complete");continue;}
                if(scanRow<800&&scan>=rowEnds[scanRow])scanRow++;
                if(scanRow>=800){Finish(false,"invalid-scan-row");continue;}
                int tile=scan++;
                if(Components[tile]!=0)continue;
                int eligible=map.SeedEligibility(tile);
                if(eligible<0){Finish(false,"missing-special-seed-input");continue;}
                if(eligible==0)continue;
                Components[tile]=(ushort)NextComponent;queue[0]=tile;queueRows[0]=(short)scanRow;head=0;tail=1;
            }
        }
    }
}

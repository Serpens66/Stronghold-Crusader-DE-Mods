using System;
using System.Diagnostics;

namespace MoveMoatTest
{
    public static class SearchKernelTests
    {
        private static readonly int[] Dx = {0,1,1,1,0,-1,-1,-1}, Dy = {-1,-1,0,1,1,1,0,-1};
        private static int assertions;
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new Exception("Search kernel: " + message); }

        public static void Run()
        {
            var random = new Random(1420);
            const int width = 5, count = 25, maximum = 12;
            for (int map = 0; map < 40; map++)
            {
                var edges = new byte[count, 8];
                for (int n = 0; n < count; n++)
                for (int d = 0; d < 8; d++)
                {
                    int x=n%width+Dx[d], y=n/width+Dy[d];
                    if(x>=0&&y>=0&&x<width&&y<width) edges[n,d]=(byte)random.Next(0,5);
                }
                bool Edge(int from,int to,int d,out bool moat,out bool structure)
                { byte e=edges[from,d]; moat=e==2||e==4; structure=e>=3; return e!=0; }
                var kernel = new MoatSearchKernel(width,width,Edge);
                for(int query=0;query<30;query++)
                {
                    int start=random.Next(count), goal=random.Next(count);
                    bool require=query%2==0, exclude=query%3==0;
                    long ground=1+map%3, wet=ground+4;
                    MoatSearchLimit[] limits=query%4==0 ? null : new[] {
                        new MoatSearchLimit(ground,wet,12+query),
                        new MoatSearchLimit(2,10,15+query*2)
                    };
                    long expected=Oracle(edges,width,start,goal,maximum,require,exclude,ground,wet,limits);
                    bool found=kernel.Search(start,goal,ground,wet,maximum,require,exclude,limits,true,out int[] path);
                    Check(found==(expected!=long.MaxValue), "directed graph reachability/profile feasibility");
                    if(found)
                    {
                        Check(path[0]==start&&path[path.Length-1]==goal,"exact endpoints");
                        long actual=0; int g=0,m=0;
                        for(int i=1;i<path.Length;i++)
                        {
                            int d=kernel.Direction(path[i-1],path[i]);
                            Check(d>=0&&Edge(path[i-1],path[i],d,out _,out _),"original edge orientation");
                            byte e=edges[path[i-1],d]; if(e==2||e==4)m++;else g++;
                        }
                        actual=g*ground+m*wet;
                        Check(actual==expected,"optimal feasible route against independent step-count oracle");
                    }
                }
                kernel.Invalidate();
                Array.Clear(edges,0,edges.Length);
                Check(!kernel.Search(0,24,1,5,maximum,false,false,null,true,out _),"new terrain invalidates positive field");
            }
            LongReachability();
            Performance();
            Console.WriteLine("PASS: "+assertions+" independent search assertions (directed edges, profile conflicts, length limits, terrain changes).");
        }

        // Exhaustive dynamic programming over exact step/moat counts; no production heap,
        // heuristic, dominance rule or reverse field is used by this oracle.
        private static long Oracle(byte[,] edges,int width,int start,int goal,int max,bool require,bool exclude,
            long ground,long wet,MoatSearchLimit[] limits)
        {
            int count=edges.GetLength(0); var reachable=new bool[max+1,count,max+1]; reachable[0,start,0]=true;
            long best=long.MaxValue;
            for(int step=0;step<=max;step++)
            for(int node=0;node<count;node++)
            for(int moat=0;moat<=step;moat++)
            {
                if(!reachable[step,node,moat])continue;
                if(node==goal&&(!require||moat>0))
                {
                    bool allowed=true;
                    if(limits!=null)foreach(var limit in limits)
                        if(limit.Ground*(step-moat)+limit.Moat*moat>limit.Maximum)allowed=false;
                    if(allowed)best=Math.Min(best,ground*(step-moat)+wet*moat);
                }
                if(step==max)continue;
                for(int d=0;d<8;d++)
                {
                    byte e=edges[node,d]; if(e==0||(exclude&&e>=3))continue;
                    int next=(node/width+Dy[d])*width+node%width+Dx[d];
                    reachable[step+1,next,moat+((e==2||e==4)?1:0)]=true;
                }
            }
            return best;
        }

        private static void LongReachability()
        {
            bool Edge(int from,int to,int d,out bool wet,out bool structure)
            { wet=from==1500||to==1500;structure=false;return d==2||d==6; }
            var k=new MoatSearchKernel(2502,1,Edge);
            Check(k.Search(0,2501,1,1,int.MaxValue,false,false,null,true,out var path)&&path.Length==2502,
                "topological reachability beyond native 2000 directions");
            Check(!k.Search(0,2501,1,1,2000,false,false,null,true,out _),"native buffer capacity remains enforced");
            Check(k.Search(0,2000,1,1,2000,false,false,null,true,out _),"exact buffer boundary");
        }

        private static void Performance()
        {
            const int width=220,height=150;
            int Kind(int node) { int x=node%width,y=node/width; return x==110&&y<130 ? (y>=68&&y<=72?1:-1):0; }
            bool Edge(int from,int to,int d,out bool wet,out bool structure)
            {
                wet=Kind(from)==1||Kind(to)==1;structure=false;
                if(Kind(from)<0||Kind(to)<0)return false;
                if((d&1)!=0&&(Kind(from/width*width+to%width)<0||Kind(to/width*width+from%width)<0))return false;
                return true;
            }
            foreach(int count in new[]{1,5,20,27,29})
            {
                var scalar=new MoatSearchKernel(width,height,Edge);
                var shared=new MoatSearchKernel(width,height,Edge);
                var paths=new int[count][];
                long allocated=GC.GetAllocatedBytesForCurrentThread(); var watch=Stopwatch.StartNew();
                for(int i=0;i<count;i++)
                {
                    int s=(25+i/3)*width+25+i%3,t=(25+i/3)*width+190+i%3;
                    Check(scalar.Search(s,t,1,13,2000,false,false,null,false,out paths[i]),"scalar benchmark path");
                }
                double independentMs=watch.Elapsed.TotalMilliseconds;
                long independentBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
                allocated=GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
                for(int i=0;i<count;i++)
                {
                    int s=(25+i/3)*width+25+i%3,t=(25+i/3)*width+190+i%3;
                    Check(shared.Search(s,t,1,13,2000,false,false,null,true,out var b),"shared benchmark path");
                    long Cost(int[] p) { long c=0;for(int j=1;j<p.Length;j++)c+=Kind(p[j])==1||Kind(p[j-1])==1?13:1;return c; }
                    Check(Cost(paths[i])==Cost(b),"formation paths keep exact optimal costs");
                }
                double sharedMs=watch.Elapsed.TotalMilliseconds;
                long sharedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
                if(count>=20)Check(shared.Expanded<scalar.Expanded,"shared field reduces large-group search work");
                Console.WriteLine($"SEARCH MODEL units={count} independentNodes={scalar.Expanded} sharedNodes={shared.Expanded} sharedRuns={shared.Searches} cacheHits={shared.FieldHits} independentMs={independentMs:F2} sharedMs={sharedMs:F2} independentAllocBytes={independentBytes} sharedAllocBytes={sharedBytes}");
            }
        }
    }
}

using System;
using System.Diagnostics;

namespace MoatMove
{
    public static class SearchKernelTests
    {
        private static readonly int[] Dx = {0,1,1,1,0,-1,-1,-1}, Dy = {-1,-1,0,1,1,1,0,-1};
        private static int assertions;
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new Exception("Search kernel: " + message); }

        private static void CandidateFieldTests()
        {
            const int width=9, size=81;
            var random=new Random(77401);var field=new MoatCandidateField(width,width);
            int comparisons=0;
            for(int map=0;map<80;map++)
            {
                var edges=new bool[size,size];var terminal=new bool[size,size];
                for(int from=0;from<size;from++)for(int to=0;to<size;to++)
                    if(from!=to && Math.Abs(from%width-to%width)<=1 && Math.Abs(from/width-to/width)<=1)
                    { edges[from,to]=random.Next(5)>1;terminal[from,to]=random.Next(5)==0; }
                MoatSearchEdge normal=(int f,int t,int d,out bool m,out bool st)=>{m=st=false;return edges[f,t];};
                MoatSearchEdge end=(int f,int t,int d,out bool m,out bool st)=>{m=st=false;return terminal[f,t];};
                var starts=new[]{map%size,(map*7)%size};var targets=new int[size];for(int i=0;i<size;i++)targets[i]=i;
                int[] actual=field.Resolve(starts,targets,normal,end);
                // Independent adjacency-matrix BFS. Terminal-only nodes must not be expanded.
                var ground=new int[size];for(int i=0;i<size;i++)ground[i]=-1;
                var queue=new System.Collections.Generic.Queue<int>();foreach(int start in starts)if(ground[start]<0){ground[start]=0;queue.Enqueue(start);}
                while(queue.Count!=0){int f=queue.Dequeue();for(int t=0;t<size;t++)if(edges[f,t]&&ground[t]<0){ground[t]=ground[f]+1;queue.Enqueue(t);}}
                for(int t=0;t<size;t++)
                {
                    int expected=ground[t]<0?int.MaxValue:ground[t];
                    for(int f=0;f<size;f++)if(ground[f]>=0&&terminal[f,t])expected=Math.Min(expected,ground[f]+1);
                    Check(actual[t]==(expected==int.MaxValue?-1:expected),"shared directed field matches independent terminal-aware reference");comparisons++;
                }
                Check(field.Expanded<=size,"shared field visits each source cell once");
                // Same reusable field with changed terrain has no stale positive/negative answers.
                Array.Clear(edges,0,edges.Length);Array.Clear(terminal,0,terminal.Length);
                var reset=field.Resolve(new[]{0},new[]{80},normal,end);
                Check(reset[0]==-1,"new command terrain resets shared field");
            }
            var budgetField = new MoatCandidateField(40, 40);
            MoatSearchEdge open = (int f, int t, int d, out bool m, out bool st) =>
            { m = st = false; return true; };
            int[] budgeted = budgetField.Resolve(
                new[] { 0 }, new[] { 1599 }, open, open, maximumExpanded: 32,
                maximumDistance: 2000);
            Check(budgeted[0] == -1 && budgetField.BudgetExceeded &&
                budgetField.Expanded == 32,
                "shared field stops deterministically at the Fast node budget");
            int[] exact = budgetField.Resolve(new[] { 0 }, new[] { 1599 }, open, open);
            Check(exact[0] >= 0 && !budgetField.BudgetExceeded,
                "unlimited shared field retains Exact behavior");
            Console.WriteLine($"PASS: {comparisons} independent building-field distances, directed terminal edges and fresh-state reuse.");
        }

        public static void Run()
        {
            CandidateFieldTests();
            PreciseComparison();
            PlannerComparison();
            CacheBoundaryComparison();
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
            DeterministicBudget();
            ProfilePool();
            GroupPipelinePerformance();
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

        private static void PreciseComparison()
        {
            var random = new Random(926183);
            const int w = 13, h = 11;
            var edges = new byte[w * h, 8];
            bool Edge(int f, int t, int d, out bool wet, out bool structure)
            { byte e = edges[f, d]; wet = e == 2 || e == 4; structure = e >= 3; return e != 0; }
            var old = new ComparisonMoatSearchKernel(w, h, Edge);
            var current = new MoatSearchKernel(w, h, Edge);
            long Cost(int[] path, int ground, int moat)
            {
                long result = 0;
                for (int i = 1; i < path.Length; i++)
                {
                    int d = current.Direction(path[i - 1], path[i]);
                    Check(d >= 0 && Edge(path[i - 1], path[i], d, out _, out _), "comparison path edge exists");
                    result += edges[path[i - 1], d] == 2 || edges[path[i - 1], d] == 4 ? moat : ground;
                }
                return result;
            }
            for (int map = 0; map < 60; map++)
            {
                for (int n = 0; n < w * h; n++) for (int d = 0; d < 8; d++)
                    edges[n, d] = (byte)random.Next(5);
                old.Invalidate(); current.Invalidate();
                for (int q = 0; q < 60; q++)
                {
                    int start = random.Next(w * h), target = random.Next(w * h);
                    int g = 1 + q % 3, m = g + 6, maximum = 10 + q % 20;
                    bool require = q % 2 == 0, exclude = q % 3 == 0;
                    var limits = q % 4 == 0 ? null : new[] {
                        new MoatSearchLimit(g, m, 40 + q), new MoatSearchLimit(3, 12, 60 + q) };
                    bool expected = old.Search(start, target, g, m, maximum, require, exclude, limits, true, out var a);
                    bool actual = current.Search(start, target, g, m, maximum, require, exclude, limits, true, out var b);
                    Check(expected == actual, "accepted precise copy reachability and constrained feasibility");
                    if (actual)
                    {
                        Check(b[0] == start && b[b.Length - 1] == target, "exact comparison endpoints");
                        Check(Cost(a, g, m) == Cost(b, g, m), "accepted precise copy optimum cost");
                        Check(System.Linq.Enumerable.SequenceEqual(a, b), "accepted precise copy exact route including ties");
                    }
                }
            }
            foreach (int units in new[] { 1, 120, 680 })
            {
                const int width = 160, height = 120;
                bool Terrain(int f, int t, int d, out bool wet, out bool structure)
                {
                    wet = f % width >= 76 && f % width <= 79 || t % width >= 76 && t % width <= 79;
                    structure = false;
                    // A wall with a distant opening forces useful work beyond a straight line.
                    return !(t % width == 100 && t / width > 18 && t / width < 102);
                }
                var reference = new ComparisonMoatSearchKernel(width, height, Terrain);
                var optimized = new MoatSearchKernel(width, height, Terrain);
                long RouteCost(int[] p)
                {
                    long c = 0;
                    for (int i = 1; i < p.Length; i++)
                    { Terrain(p[i - 1], p[i], 0, out bool wet, out _); c += wet ? 7 : 1; }
                    return c;
                }
                var expected = new long[units];
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < units; i++)
                {
                    int start = (45 + i % 12) * width + 8 + i % 8;
                    int target = (48 + i % 12) * width + 140 + i % 8;
                    Check(reference.Search(start, target, 1, 7, 2000, false, false, null, true, out var path), "comparison formation route");
                    expected[i] = RouteCost(path);
                }
                double referenceMs = watch.Elapsed.TotalMilliseconds;
                watch.Restart();
                for (int i = 0; i < units; i++)
                {
                    int start = (45 + i % 12) * width + 8 + i % 8;
                    int target = (48 + i % 12) * width + 140 + i % 8;
                    Check(optimized.Search(start, target, 1, 7, 2000, false, false, null, true, out var path), "optimized formation route");
                    Check(RouteCost(path) == expected[i], "formation optimum matches accepted precise copy");
                }
                Console.WriteLine($"PRECISE COMPARISON units={units} oldMs={referenceMs:F2} newMs={watch.Elapsed.TotalMilliseconds:F2} oldNodes={reference.Expanded} newNodes={optimized.Expanded} edgeEvaluations={optimized.EdgeEvaluations} edgeCacheHits={optimized.EdgeCacheHits}");
                Check(optimized.Expanded == reference.Expanded, "search ordering and node budgets unchanged");
                if (units >= 120) Check(optimized.EdgeCacheHits > optimized.EdgeEvaluations * 20, "formation removes repeated edge evaluations");
            }
        }

        private static unsafe void PlannerComparison()
        {
            const int nativeTiles = 320800, width = 160, height = 120;
            var allocations = new System.Collections.Generic.List<IntPtr>();
            IntPtr Allocate(int bytes)
            {
                var value = (IntPtr)System.Runtime.InteropServices.NativeMemory.AllocZeroed((nuint)bytes);
                allocations.Add(value); return value;
            }
            try
            {
                int* rows = (int*)Allocate(800 * 3 * 4);
                uint* flags = (uint*)Allocate(nativeTiles * 4);
                ushort* buildings = (ushort*)Allocate(nativeTiles * 2);
                byte* heights = (byte*)Allocate(nativeTiles);
                byte* masks = (byte*)Allocate(nativeTiles);
                byte* directions = (byte*)Allocate(8);
                byte* types = (byte*)Allocate(0x32C * 10001);
                for (int y = 0; y < 800; y++) rows[y * 3] = y < height ? y * 800 : nativeTiles;
                for (int i = 0; i < nativeTiles; i++) flags[i] = 0x100;
                for (int d = 0; d < 8; d++) directions[d] = (byte)(1 << d);
                bool Open(int x, int y) => x >= 0 && x < width && y >= 0 && y < height && !(x == 100 && y > 18 && y < 102);
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    if (!Open(x, y)) continue;
                    int tile = y * 800 + x;
                    bool moat = x >= 76 && x <= 79;
                    flags[tile] = moat ? 0x40000000U : 0x8000U;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + Dx[d], ny = y + Dy[d];
                        if (Open(nx, ny) && !moat && !(nx >= 76 && nx <= 79)) masks[tile] |= directions[d];
                    }
                }
                WeightedMovementCostProfile.TryCreate(1, 1, 0, 0, 0, 0, false, out var profile, out _);
                foreach (int units in new[] { 1, 120, 680 })
                {
                    var previous = new ComparisonWeightedMoatRoutePlanner(rows, flags, buildings, heights, masks, directions, types,
                        (p, t) => CompletedMoatRelationship.Friendly, t => false);
                    var current = new WeightedMoatRoutePlanner(rows, flags, buildings, heights, masks, directions, types,
                        (p, t) => CompletedMoatRelationship.Friendly, t => false);
                    object session = new object();
                    previous.SetSearchSession(session, 1, 1, 1); current.SetSearchSession(session, 1, 1, 1);
                    var paths = new WeightedMoatEncodedRoute[units];
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < units; i++)
                        Check(previous.TryBuildImprovement(1, 8 + i % 8, 45 + i % 12, 140 + i % 8, 48 + i % 12,
                            profile, false, null, out _, out paths[i], requireMoat: false), "original planner fixture route");
                    double oldMs = watch.Elapsed.TotalMilliseconds;
                    watch.Restart();
                    for (int i = 0; i < units; i++)
                    {
                        Check(current.TryBuildImprovement(1, 8 + i % 8, 45 + i % 12, 140 + i % 8, 48 + i % 12,
                            profile, false, null, out _, out var path, requireMoat: false), "optimized planner fixture route");
                        Check(path.DirectionCount == paths[i].DirectionCount && System.Linq.Enumerable.SequenceEqual(path.Bytes, paths[i].Bytes),
                            "actual planner publishes byte-identical precise candidate");
                    }
                    Console.WriteLine($"ACTUAL PRECISE PLANNER units={units} oldMs={oldMs:F2} newMs={watch.Elapsed.TotalMilliseconds:F2} oldNodes={previous.SearchNodes} newNodes={current.SearchNodes}");
                }
            }
            finally { foreach (IntPtr p in allocations) System.Runtime.InteropServices.NativeMemory.Free((void*)p); }
        }

        private static void CacheBoundaryComparison()
        {
            const int w = 64, h = 40;
            bool blocked = false;
            bool Edge(int f, int t, int d, out bool wet, out bool structure)
            { wet = f % w == 31 || t % w == 31; structure = t % w == 40; return !blocked || t % w != 31; }
            var previous = new ComparisonMoatSearchKernel(w, h, Edge);
            var current = new MoatSearchKernel(w, h, Edge);
            for (int revision = 0; revision < 8; revision++)
            {
                blocked = revision % 2 != 0;
                previous.Invalidate(); current.Invalidate();
                foreach (bool shared in new[] { true, false }) foreach (int budget in new[] { 16, 128, 16384 })
                {
                    long oldNodes = previous.Expanded, newNodes = current.Expanded;
                    bool a = previous.Search(22 * w + 2, 22 * w + 61, 1, 1, 2000, false, false, null, shared, out var oldPath, budget);
                    bool b = current.Search(22 * w + 2, 22 * w + 61, 1, 1, 2000, false, false, null, shared, out var newPath, budget);
                    Check(a == b && previous.LastSearchBudgetExceeded == current.LastSearchBudgetExceeded, "Fast fixed-budget behavior matches original after invalidation");
                    Check(previous.Expanded - oldNodes == current.Expanded - newNodes, "Fast expansion count unchanged");
                    if (a) Check(System.Linq.Enumerable.SequenceEqual(oldPath, newPath), "Fast unweighted path unchanged");
                }
            }
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

        private static void DeterministicBudget()
        {
            bool Edge(int from,int to,int d,out bool wet,out bool structure)
            { wet=false;structure=false;return true; }
            var limited=new MoatSearchKernel(200,200,Edge);
            Check(!limited.Search(0,39999,1,1,2000,false,false,null,false,out _,32) &&
                limited.LastSearchBudgetExceeded && limited.Expanded==32,
                "fixed expansion budget aborts deterministically");
            var exact=new MoatSearchKernel(200,200,Edge);
            Check(exact.Search(0,39999,1,1,2000,false,false,null,false,out _) &&
                !exact.LastSearchBudgetExceeded,
                "unlimited exact search remains available");
        }

        private static void ProfilePool()
        {
            bool Edge(int from,int to,int d,out bool wet,out bool structure)
            {wet=from==9||to==9;structure=false;return d==2||d==6;}
            var k=new MoatSearchKernel(20,1,Edge);
            Check(k.Search(0,19,1,7,2000,false,false,null,true,out _),"profile seed");
            long before=k.Expanded;
            Check(k.Search(0,19,3,21,2000,false,false,new[]{new MoatSearchLimit(3,21,93)},true,out _),"scaled exact bound");
            Check(k.Expanded==before && k.CachedFields==1,"proportional profile shares normalized field");
            for(int i=0;i<12;i++)Check(k.Search(0,19,1,8+i,2000,false,false,null,true,out _),"LRU profile search");
            Check(k.CachedFields==8,"bounded LRU");
            k.Invalidate();
            Check(k.Search(0,19,1,7,2000,false,false,null,true,out _),"invalidated field recomputes");
            Check(!k.Search(0,19,3,21,2000,false,false,new[]{new MoatSearchLimit(3,21,92)},true,out _),"scaled below-bound remains excluded");
        }

        private static void GroupPipelinePerformance()
        {
            const int w=100,h=70;
            bool Edge(int from,int to,int d,out bool wet,out bool structure)
            {wet=from%w==50||to%w==50;structure=false;return true;}
            bool Ground(int from,int to,int d,out bool wet,out bool structure)
            {Edge(from,to,d,out wet,out structure);return !wet;}
            foreach(int count in new[]{1,120,680})
            {
                var ground=new ReferenceMoatSearchKernel(w,h,Ground);var reach=new ReferenceMoatSearchKernel(w,h,Edge);
                var weighted=new ReferenceMoatSearchKernel(w,h,Edge);var combined=new MoatSearchKernel(w,h,Edge);
                var expected=new long[count];
                long Cost(int[] p,int g,int m) {long c=0;for(int j=1;j<p.Length;j++)c+=(p[j]%w==50||p[j-1]%w==50)?m:g;return c;}
                long allocation=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
                for(int i=0;i<count;i++)
                {
                    int s=(5+i%55)*w+5+i%10,t=(5+i%55)*w+80+i%10;
                    int g=1+i%2,m=7+i%2;
                    Check(!ground.Search(s,t,1,1,int.MaxValue,false,false,null,true,out _),"reference excludes ground");
                    Check(reach.Search(s,t,1,1,2000,false,false,null,true,out _),"reference qualification");
                    Check(reach.Search(s,t,1,1,2000,false,false,null,true,out _),"reference reconstruction");
                    Check(weighted.Search(s,t,g,m,2000,false,false,null,true,out var p),"reference weighted path");
                    expected[i]=Cost(p,g,m);
                }
                double oldMs=watch.Elapsed.TotalMilliseconds;long oldBytes=GC.GetAllocatedBytesForCurrentThread()-allocation;
                allocation=GC.GetAllocatedBytesForCurrentThread();watch.Restart();
                for(int i=0;i<count;i++)
                {
                    int s=(5+i%55)*w+5+i%10,t=(5+i%55)*w+80+i%10;
                    int g=1+i%2,m=7+i%2;
                    Check(combined.Search(s,t,g,m,2000,false,false,null,true,out var p),"combined qualified path");
                    Check(Cost(p,g,m)==expected[i],"combined route cost unchanged");
                }
                long oldNodes=ground.Expanded+reach.Expanded+weighted.Expanded;
                if(count>=120)Check(combined.Expanded<oldNodes,"combined large group reduces total search nodes");
                Console.WriteLine($"GROUP PIPELINE MODEL units={count} referenceMs={oldMs:F2} combinedMs={watch.Elapsed.TotalMilliseconds:F2} referenceNodes={oldNodes} combinedNodes={combined.Expanded} referenceBytes={oldBytes} combinedBytes={GC.GetAllocatedBytesForCurrentThread()-allocation}");
            }
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

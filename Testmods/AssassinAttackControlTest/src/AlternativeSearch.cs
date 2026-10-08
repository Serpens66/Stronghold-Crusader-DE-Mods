using System;
using System.Collections.Generic;
using APIShared;
namespace AssassinAttackControlTest
{
    internal enum SearchOutcome { Unknown, NoAlternative, Alternative }
    internal sealed class SearchResult
    {
        internal SearchOutcome Outcome;
        internal int Goal, BuildingId, Expanded;
        internal int[] Route;
    }
    /// <summary>Owned scratch buffers; never touches native search stamps, queues or route caches.</summary>
    internal sealed class AlternativeSearch
    {
        internal const int Width=800, Capacity=Width*Width;
        private readonly long[] costs=new long[Capacity];
        private readonly int[] parents=new int[Capacity];
        private readonly SortedSet<Entry> queue=new SortedSet<Entry>();
        private readonly struct Entry : IComparable<Entry>
        {
            internal readonly long Cost; internal readonly int Node;
            internal Entry(long cost,int node) { Cost=cost; Node=node; }
            public int CompareTo(Entry other) { int c=Cost.CompareTo(other.Cost); return c!=0 ? c : Node.CompareTo(other.Node); }
        }
        private IAssassinTraversalView view;
        private Dictionary<int,int> gates;
        private HashSet<int> interior;
        private SearchResult result;
        private int start, bestInterior;
        internal void Begin(IAssassinTraversalView view,int start,Dictionary<int,int> gates,HashSet<int> interior)
        {
            this.view=view; this.start=start; this.gates=gates; this.interior=interior; bestInterior=-1;
            result=new SearchResult { Outcome=SearchOutcome.Unknown };
            queue.Clear();
            if(view==null || !view.IsCurrent || start<0 || start>=Capacity || (gates.Count==0 && interior.Count==0)) return;
            for(int i=0;i<Capacity;i++) { costs[i]=long.MaxValue; parents[i]=-1; }
            costs[start]=0; queue.Add(new Entry(0,start));
        }
        internal SearchResult Step(int budget)
        {
            if(view==null || !view.ValidateTopology()) { queue.Clear(); return result; }
            if(queue.Count==0) return result;
            int processed=0;
            while(queue.Count>0 && processed++<budget)
            {
                Entry current=queue.Min; queue.Remove(current); result.Expanded++;
                if(gates.TryGetValue(current.Node,out int building))
                    return Complete(result,current.Node,building,start);
                if(bestInterior<0 && interior.Contains(current.Node)) bestInterior=current.Node;
                if(gates.Count==0 && bestInterior>=0) return Complete(result,bestInterior,0,start);
                int x=current.Node%Width,y=current.Node/Width;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                {
                    if((dx==0 && dy==0) || x+dx<0 || x+dx>=Width || y+dy<0 || y+dy>=Width) continue;
                    int next=current.Node+dy*Width+dx;
                    if(!view.TryGetEdgeCost(x,y,x+dx,y+dy,out int edge) || edge<=0) continue;
                    long proposed=current.Cost+edge;
                    if(proposed>=costs[next]) continue;
                    if(costs[next]!=long.MaxValue) queue.Remove(new Entry(costs[next],next));
                    costs[next]=proposed; parents[next]=current.Node; queue.Add(new Entry(proposed,next));
                }
            }
            if(queue.Count>0) return result;
            if(!view.IsCurrent) return result;
            if(bestInterior>=0) return Complete(result,bestInterior,0,start);
            result.Outcome=SearchOutcome.NoAlternative; return result;
        }
        internal bool HasPendingNodes=>queue.Count>0;
        internal SearchResult Find(IAssassinTraversalView view,int start,Dictionary<int,int> gates,HashSet<int> interior,int budget=320800)
        { Begin(view,start,gates,interior); return Step(budget); }
        private SearchResult Complete(SearchResult result,int goal,int building,int start)
        {
            var route=new List<int>(); int at=goal;
            while(at>=0 && route.Count<=Capacity) { route.Add(at); if(at==start) break; at=parents[at]; }
            if(route[route.Count-1]!=start) return result;
            route.Reverse(); result.Outcome=SearchOutcome.Alternative; result.Goal=goal;
            result.BuildingId=building; result.Route=route.ToArray(); return result;
        }
        internal static bool Validate(IAssassinTraversalView view,int[] route)
        {
            if(view==null || !view.IsCurrent || route==null || route.Length==0) return false;
            for(int i=1;i<route.Length;i++) if(!view.TryGetEdgeCost(route[i-1]%Width,route[i-1]/Width,
                route[i]%Width,route[i]/Width,out _)) return false;
            return view.IsCurrent;
        }
    }
}

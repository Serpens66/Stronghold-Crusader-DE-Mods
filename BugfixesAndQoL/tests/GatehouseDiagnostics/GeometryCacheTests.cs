using System;
using System.Collections.Generic;
using System.Threading;

namespace BugfixesAndQoL
{
    internal static class GeometryCacheTests
    {
        private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
        internal static void Run()
        {
            for(int size=1;size<=9;size++)
            {
                var expected=APIShared.GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates(70,80,size);
                var actual=GatehouseFootprintGeometryCache.Get(70,80,size);
                Check(actual.Count==expected.Count,"Geometry count changed");
                for(int i=0;i<expected.Count;i++)Check(actual[i].X==expected[i].X&&actual[i].Y==expected[i].Y&&
                    actual[i].OutwardX==expected[i].OutwardX&&actual[i].OutwardY==expected[i].OutwardY,"Geometry/native perimeter order changed");
                try { ((IList<APIShared.GatehouseFootprintCandidate>)actual)[0]=default;throw new Exception("Cached geometry is mutable"); }
                catch(NotSupportedException) { }
            }
            for(int y=81;y<=83;y++)for(int x=71;x<=73;x++)
            {
                var expected=APIShared.GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates(x,y,3);
                var actual=GatehouseFootprintGeometryCache.Get(x,y,3);
                Check(actual[0].X==expected[0].X&&actual[0].Y==expected[0].Y,"Geometry key omitted an origin coordinate");
            }
            var retained=GatehouseFootprintGeometryCache.Get(-1000,0,5);
            Check(ReferenceEquals(retained,GatehouseFootprintGeometryCache.Get(-1000,0,5)),"Identical geometry was rebuilt");
            IReadOnlyList<APIShared.GatehouseFootprintCandidate> other=null;
            Exception failure=null;
            var thread=new Thread(()=>
            {
                try
                {
                    other=GatehouseFootprintGeometryCache.Get(-1000,0,5);
                    for(int i=0;i<255;i++)GatehouseFootprintGeometryCache.Get(1000+i,0,5);
                    Check(ReferenceEquals(other,GatehouseFootprintGeometryCache.Get(-1000,0,5)),"Cache cleared before 256 geometries");
                    GatehouseFootprintGeometryCache.Get(2000,0,5);
                    Check(!ReferenceEquals(other,GatehouseFootprintGeometryCache.Get(-1000,0,5)),"Geometry bound did not evict old entries");
                    Check(other.Count==20&&other[0].X==-998,"Eviction mutated a retained outer query");
                }
                catch(Exception error){failure=error;}
            });thread.Start();
            Check(thread.Join(10000)&&!ReferenceEquals(other,GatehouseFootprintGeometryCache.Get(-1000,0,5)),"Geometry cache is not thread-local");
            if(failure!=null)throw failure;
            GatehouseFootprintGeometryCache.Get(0,0,5); // Warm cache and diagnostic counters.
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<10000;i++)GatehouseFootprintGeometryCache.Get(0,0,5);
            Check(GC.GetAllocatedBytesForCurrentThread()==before,"Warm geometry lookup allocated memory");
        }
    }
}

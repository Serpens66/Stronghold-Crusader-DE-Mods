using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using APIShared.WorldMutations;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Entire file exists only in DEVDIAG. No new publisher, native hook or unit scan.
    internal static class RescueCaptureDiagnostics
    {
        internal struct Sample
        {
            internal long Bytes;internal int Thread,C0,C1,C2;
        }
        internal struct CaptureScope {internal Sample Start;internal Counters Parent;}
        internal struct Counters
        {
            internal bool Active;internal WorldMutationSource Source;internal int Owner;
            internal long Requests,Reads,Buildings,Searches,Hits,BrushNodes,Writes,Halo,Links,Selected,Snapshots,OwnerDropped,Unsupported,
                Bfs,BfsNodes,ForeignBfs,ForeignBfsNodes,Alloc;internal int G0,G1,G2;
        }
        [ThreadStatic] private static Counters current,last;
        private static Func<long> allocated;
        private static int allocationAvailable;
        private static readonly Func<string> CaptureDetail=DescribeCapture;
        private static readonly Func<string> AllocatorDetail=DescribeAllocator;
        private static readonly Dictionary<WorldMutationSource,string> SourceCounters=MakeSourceCounters();
        private static Dictionary<WorldMutationSource,string> MakeSourceCounters()
        {
            var result=new Dictionary<WorldMutationSource,string>();
            foreach(WorldMutationSource source in Enum.GetValues(typeof(WorldMutationSource)))
                result[source]="rescue.source."+source+".calls";
            return result;
        }
        internal static void Initialize()
        {
            try
            {
                var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static,
                    null,Type.EmptyTypes,null);
                if(method!=null && method.ReturnType==typeof(long) && !method.ContainsGenericParameters)
                {
                    allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
                    long before=allocated();var witness=new byte[256];witness[0]=1;long after=allocated();GC.KeepAlive(witness);
                    if(before>=0 && after>before)Volatile.Write(ref allocationAvailable,1);
                }
            }
            catch{Volatile.Write(ref allocationAvailable,0);}
            Shared.DevDiag.Log(AllocatorDetail);
        }
        private static string DescribeAllocator()=>"rescue-allocation-capability method=GC.GetAllocatedBytesForCurrentThread available="+
            (Volatile.Read(ref allocationAvailable)!=0)+" gcCounters=process-wide noGcDurationClaim=true";
        private static long Bytes()
        {
            if(Volatile.Read(ref allocationAvailable)==0 || allocated==null)return -1;
            try{return allocated();}catch{Interlocked.Exchange(ref allocationAvailable,0);return -1;}
        }
        internal static Sample Read()
        {
            var sample=new Sample{Thread=Thread.CurrentThread.ManagedThreadId,Bytes=Bytes(),C0=-1,C1=-1,C2=-1};
            try{sample.C0=GC.CollectionCount(0);sample.C1=GC.CollectionCount(1);sample.C2=GC.CollectionCount(2);}
            catch{sample.C0=sample.C1=sample.C2=-1;}
            return sample;
        }
        private static int GcDelta(int start,int end)=>start>=0 && end>=start?end-start:-1;
        private static long Delta(Sample start,Sample end)=>start.Thread==end.Thread && start.Bytes>=0 && end.Bytes>=start.Bytes?end.Bytes-start.Bytes:-1;
        internal static CaptureScope Begin(WorldMutationSource source)
        {
            var scope=new CaptureScope{Start=Read(),Parent=current};current=new Counters{Active=true,Source=source};return scope;
        }
        internal static void Owner(int owner){if(current.Active)current.Owner=owner;}
        internal static void Plan(int requests,int reads,int buildings,int searches,int hits,int nodes,int writes)
        {if(!current.Active)return;current.Requests+=requests;current.Reads+=reads;current.Buildings+=buildings;current.Searches+=searches;current.Hits+=hits;current.BrushNodes+=nodes;current.Writes+=writes;}
        internal static void Discovery(int halo,int links,int selected)
        {if(!current.Active)return;current.Halo+=halo;current.Links+=links;current.Selected+=selected;}
        internal static void Snapshot(int owner,bool unsupported)
        {if(!current.Active)return;current.Snapshots++;if(current.Owner>0 && owner!=current.Owner)current.OwnerDropped++;if(unsupported)current.Unsupported++;}
        internal static void CandidateBfs(int owner,int nodes)
        {if(!current.Active)return;current.Bfs++;current.BfsNodes+=nodes;if(current.Owner>0 && owner!=current.Owner){current.ForeignBfs++;current.ForeignBfsNodes+=nodes;}}
        internal static void End(ref CaptureScope scope)
        {
            var end=Read();last=current;current=scope.Parent;last.Alloc=Delta(scope.Start,end);
            last.G0=GcDelta(scope.Start.C0,end.C0);last.G1=GcDelta(scope.Start.C1,end.C1);last.G2=GcDelta(scope.Start.C2,end.C2);
        }
        internal static void PublishCapture()
        {
            if(SourceCounters.TryGetValue(last.Source,out var name))Shared.DevDiag.Count(name);
            Shared.DevDiag.Count("rescue.count.planRequests",last.Requests);Shared.DevDiag.Count("rescue.count.planNativeReadCacheMisses",last.Reads);Shared.DevDiag.Count("rescue.count.buildingSlots",last.Buildings);
            Shared.DevDiag.Count("rescue.count.brushSearches",last.Searches);Shared.DevDiag.Count("rescue.count.brushCacheHits",last.Hits);Shared.DevDiag.Count("rescue.count.brushNodes",last.BrushNodes);
            Shared.DevDiag.Count("rescue.count.writeTiles",last.Writes);Shared.DevDiag.Count("rescue.count.haloCells",last.Halo);Shared.DevDiag.Count("rescue.count.chainLinks",last.Links);Shared.DevDiag.Count("rescue.count.selectedOccupants",last.Selected);
            Shared.DevDiag.Count("rescue.count.snapshots",last.Snapshots);Shared.DevDiag.Count("rescue.count.ownerDroppedAfterSnapshot",last.OwnerDropped);Shared.DevDiag.Count("rescue.count.unsupportedSnapshots",last.Unsupported);
            Shared.DevDiag.Count("rescue.count.candidateBfs",last.Bfs);Shared.DevDiag.Count("rescue.count.candidateNodes",last.BfsNodes);Shared.DevDiag.Count("rescue.count.foreignCandidateBfs",last.ForeignBfs);Shared.DevDiag.Count("rescue.count.foreignCandidateNodes",last.ForeignBfsNodes);
            if(last.Alloc>=0)Shared.DevDiag.Count("rescue.capture.threadAllocatedBytes",last.Alloc);else Shared.DevDiag.Count("rescue.capture.allocationUnavailable");
            if(last.G0>=0 && last.G1>=0 && last.G2>=0)
            {Shared.DevDiag.Count("rescue.capture.gc0Overlap",last.G0);Shared.DevDiag.Count("rescue.capture.gc1Overlap",last.G1);Shared.DevDiag.Count("rescue.capture.gc2Overlap",last.G2);}
            else Shared.DevDiag.Count("rescue.capture.gcCountersUnavailable");
        }
        internal static void StopCapture(ref long clock)=>Shared.DevDiag.StopSpike(ref clock,"rescue.capture.total.inclusive",2000,CaptureDetail);
        private static string DescribeCapture()=>"source="+last.Source+" owner="+last.Owner+" requests="+last.Requests+" planReadCacheMisses="+last.Reads+" buildings="+last.Buildings+
            " brushSearches="+last.Searches+" brushNodes="+last.BrushNodes+" writes="+last.Writes+" halo="+last.Halo+" links="+last.Links+
            " selected="+last.Selected+" snapshots="+last.Snapshots+" ownerDroppedAfterSnapshot="+last.OwnerDropped+" unsupported="+last.Unsupported+
            " bfs="+last.Bfs+" bfsNodes="+last.BfsNodes+" foreignBfs="+last.ForeignBfs+" foreignNodes="+last.ForeignBfsNodes+
            " threadAllocatedBytes="+last.Alloc+" gc0Overlap="+last.G0+" gc1Overlap="+last.G1+" gc2Overlap="+last.G2;
        internal static void EndObserver(Sample start)
        {
            var end=Read();long bytes=Delta(start,end);
            if(bytes>=0)Shared.DevDiag.Count("rescue.observer.threadAllocatedBytes.inclusive",bytes);else Shared.DevDiag.Count("rescue.observer.allocationUnavailable");
        }
        // Main-thread automation boundaries are NOT a simulation-thread allocation measurement.
        internal static string WindowDelta(Sample start,Sample end)=>"boundaryThread="+end.Thread+" boundaryThreadAllocatedBytesDelta="+Delta(start,end)+
            " processGc0Delta="+GcDelta(start.C0,end.C0)+" processGc1Delta="+GcDelta(start.C1,end.C1)+" processGc2Delta="+GcDelta(start.C2,end.C2)+" noGcDurationClaim=true";
    }
}

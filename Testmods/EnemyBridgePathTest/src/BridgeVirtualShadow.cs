using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EnemyBridgePathTest
{
    // Simulation-thread capture, render-thread pure managed work. Both are serialized
    // by gate; no Span/pointer or live member crosses the capture boundary.
    internal sealed unsafe class BridgeVirtualShadow
    {
        private readonly object gate=new object();
        private readonly Action<string,string> emit;
        private readonly Func<int,int> read;
        private readonly LinkedList<Request> pending=new LinkedList<Request>();
        private readonly Dictionary<QueryKey,Repeat> repeats=new Dictionary<QueryKey,Repeat>();
        private struct Repeat {internal long Id,Op,Parent,Count;}
        private readonly struct QueryKey : IEquatable<QueryKey>
        {
            private readonly string stage;
            private readonly int player,from,to,mode,result;
            private readonly long revision,identity;
            internal QueryKey(string stage,int player,int from,int to,int mode,int result,long revision,long identity)
            {this.stage=stage;this.player=player;this.from=from;this.to=to;this.mode=mode;this.result=result;this.revision=revision;this.identity=identity;}
            public bool Equals(QueryKey b) => stage==b.stage&&player==b.player&&from==b.from&&to==b.to&&mode==b.mode&&result==b.result&&revision==b.revision&&identity==b.identity;
            public override bool Equals(object b) => b is QueryKey value&&Equals(value);
            public override int GetHashCode() => unchecked(stage.GetHashCode()*397^player*31^from*11^to^mode*17^result^revision.GetHashCode()^identity.GetHashCode());
        }
        private long queryId,session,revision=-1,identity,nextCapture,captureTicks,computeTicks,captures,coalesced,rejected;
        private long costClock,lastCaptureTicks,lastComputeTicks,lastCaptures,lastCoalesced,lastRejected;
        private Captured captured;
        private Request active;
        private sealed class Captured
        {
            internal long Session,Revision,Identity,Clock;
            internal VirtualBridgeMap Map;
            internal ushort[] Pcl,X,Y,SpecialIds;
            internal short[] SpecialKinds;
            internal byte[] Edges;
            internal int[] Flags,Rows;
            internal PathConnectionRecord[] Records;
            internal int[] Anchors;
            internal Deck[] Decks;
            internal int[] Owners,Capturers;
            internal int ConnectionLimit;
            internal uint[] Globals,UnitGlobals;
            internal bool[,] Allies;
        }
        private sealed class Deck {internal int Id,Owner,Capturer,NativeParent;internal uint Global;internal int[] Tiles;internal bool Authorized,BoundaryVerified;}
        private sealed class Request
        {
            internal string Stage;
            internal long Id,Op,Parent,Clock;
            internal int Player,From,To,Mode,Native,UnknownRecords,DeckCells;
            internal Captured Input;
            internal IEnemyGateRoutePolicySnapshot GatePolicy;
            internal VirtualBridgeQuery Query;
            internal bool Authorization;
        }
        internal BridgeVirtualShadow(Action<string,string> emit,Func<int,int> read) {this.emit=emit;this.read=read;}
        internal void Begin(long value) {lock(gate) {session=value;revision=-1;captured=null;pending.Clear();active=null;repeats.Clear();nextCapture=0;identity++;captures=coalesced=rejected=captureTicks=computeTicks=0;costClock=lastCaptureTicks=lastComputeTicks=lastCaptures=lastCoalesced=lastRejected=0;}}
        internal void Invalidate() {lock(gate) {identity++;captured=null;}}
        internal void End()
        {
            lock(gate)
            {
                var cancelled=new System.Text.StringBuilder();if(active!=null)cancelled.Append(active.Id).Append(';');foreach(var request in pending)cancelled.Append(request.Id).Append(';');
                emit("virtual-shadow-end","shadowSession="+session+",captureCount="+captures+",captureMs="+Ms(captureTicks)+",computeMs="+Ms(computeTicks)+",coalesced="+coalesced+",rejected="+rejected+",pending="+(pending.Count+(active==null?0:1))+",pendingOutcome=cancelled-at-session-end,cancelledDefinitions=["+cancelled+"],behavior=unchanged");
                session=0;captured=null;pending.Clear();active=null;
            }
        }
        private static string Ms(long ticks) => (ticks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        internal void Rebuilt(long traceSession,long traceRevision,BridgeBuildingIndex index)
        {
            lock(gate)
            {
                revision=traceRevision;captured=null;
                if(session==0||session!=traceSession)return;
                long now=Stopwatch.GetTimestamp();if(now<nextCapture)return;
                nextCapture=now+Stopwatch.Frequency;
                long start=now;
                try
                {
                    // The audited E49D0 post is the only publication opportunity.
                    int nativeRevision=read(0x60AD6D4);
                    if(read(0x60AD6CC)!=0)return;
                    var path=GamePathingManagerAPI.Instance;var tiles=GameTileManagerAPI.Instance;
                    var pcl=path.GetPathComponentGrid();var edges=path.GetPathEdgeMaskGrid();var flags=tiles.GetLogicLayer();
                    if(pcl.Length!=320800||edges.Length!=pcl.Length||flags.Length!=pcl.Length)throw new InvalidOperationException("Packed capacity changed");
                    var result=new Captured {Session=session,Revision=revision,Identity=identity,Clock=now,Pcl=pcl.ToArray(),Edges=edges.ToArray(),Flags=flags.ToArray(),Rows=new int[800],X=new ushort[pcl.Length],Y=new ushort[pcl.Length],Anchors=new int[65536]};
                    for(int i=0;i<result.Anchors.Length;i++)result.Anchors[i]=-1;
                    if(tiles.MapRowLookupTable==null||tiles.MapColumnLookupTable==null)throw new InvalidOperationException("Packed coordinate view absent");
                    for(int y=0;y<800;y++)result.Rows[y]=tiles.MapRowLookupTable[3*y];
                    for(int tile=0;tile<pcl.Length;tile++)
                    {
                        int y=tiles.MapColumnLookupTable[tile];if((uint)y>=800)throw new InvalidOperationException("Packed row outside audited extent");
                        int x=tile-result.Rows[y];if((uint)x>=800)throw new InvalidOperationException("Packed column outside audited extent");
                        result.X[tile]=(ushort)x;result.Y[tile]=(ushort)y;
                        if(result.Pcl[tile]!=0&&result.Anchors[result.Pcl[tile]]<0)result.Anchors[result.Pcl[tile]]=tile;
                    }
                    result.SpecialIds=tiles.GetOrganismLayer().ToArray();
                    if(result.SpecialIds.Length!=pcl.Length)throw new InvalidOperationException("Special grid capacity changed");
                    result.SpecialKinds=new short[5000];
                    // Full audited 107160 consumes signed native record IDs, with
                    // kind at native-root + record*0x9C + 0x6A. No public ID lookup.
                    var copiedKinds=new bool[result.SpecialKinds.Length];
                    foreach(ushort raw in result.SpecialIds)
                    {
                        int record=(short)raw;
                        if(record>0&&record<copiedKinds.Length&&!copiedKinds[record])
                        {result.SpecialKinds[record]=unchecked((short)read(0x32DE440+record*0x9C+0x6A));copiedKinds[record]=true;}
                    }
                    result.Records=path.GetPathConnectionRecords().ToArray();
                    result.ConnectionLimit=read(0x60AD660);
                    if(result.ConnectionLimit<1||result.ConnectionLimit>result.Records.Length)throw new InvalidOperationException("Connection high-water exceeds API view");
                    var buildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    result.Owners=new int[buildings.Length];result.Capturers=new int[buildings.Length];result.Globals=new uint[buildings.Length];
                    for(int spanIndex=0;spanIndex<buildings.Length;spanIndex++)if(BridgeSnapshot.Active(buildings[spanIndex].r_AliveState))
                    {result.Owners[spanIndex]=buildings[spanIndex].r_PlayerIdOwner;result.Capturers[spanIndex]=buildings[spanIndex].r_CapturedByPlayerId;result.Globals[spanIndex]=buildings[spanIndex].r_GlobalId;}
                    var units=GameUnitManagerAPI.Instance.GetUnitsAsSpan();result.UnitGlobals=new uint[units.Length];
                    for(int spanIndex=0;spanIndex<units.Length;spanIndex++)if(BridgeSnapshot.Active(units[spanIndex].r_AliveState))result.UnitGlobals[spanIndex]=units[spanIndex].r_GlobalId;
                    result.Allies=new bool[9,9];for(int a=1;a<=8;a++)for(int b=1;b<=8;b++)result.Allies[a,b]=BridgeSnapshot.Allied(a,b);
                    index.Refresh();var decks=new List<Deck>();
                    foreach(var entry in index.Entries)
                    {
                        int spanIndex=BridgeBuildingIndex.SpanIndex(entry.Id,buildings.Length);if(spanIndex<0)continue;
                        ref GameBuilding b=ref buildings[spanIndex];
                        if(b.r_GlobalId!=entry.Global||!BridgeSnapshot.Active(b.r_AliveState))throw new InvalidOperationException("Bridge identity changed during capture");
                        if(b.r_OccupyTileGridSize!=5||b.r_SpriteVariationIndex>7)throw new InvalidOperationException("Deck mapper unknown");
                        var deck=new List<int>();fixed(GameBuilding* ptr=&b)
                        {uint* occupied=&ptr->r_OccupiedTileIdsArrayBegin;for(int i=0;i<25;i++)if(read(0x2D1A30+(b.r_SpriteVariationIndex/2)*100+i*4)!=0)
                        {if(occupied[i]>=pcl.Length)throw new InvalidOperationException("Deck tile outside copied map");deck.Add((int)occupied[i]);}}
                        int parent=BridgeBuildingIndex.SpanIndex(b.r_GatehouseId,buildings.Length);
                        bool authorized=parent>=0&&BridgeSnapshot.Active(buildings[parent].r_AliveState)&&BridgeSnapshot.IsGate(buildings[parent].r_BuildingType)&&buildings[parent].r_PlayerIdOwner==b.r_PlayerIdOwner;
                        decks.Add(new Deck {Id=entry.Id,Global=entry.Global,Owner=b.r_PlayerIdOwner,Capturer=b.r_CapturedByPlayerId,NativeParent=b.r_GatehouseId,Tiles=deck.ToArray(),Authorized=authorized,
                            // Only the documented orientation0 fixture has a closed-boundary comparison.
                            BoundaryVerified=b.r_SpriteVariationIndex==0&&deck.Count==15});
                    }
                    result.Decks=decks.ToArray();
                    if(read(0x60AD6CC)!=0||read(0x60AD6D4)!=nativeRevision||session!=result.Session||identity!=result.Identity)return;
                    result.Map=new VirtualBridgeMap(result.Session,result.Revision,result.Pcl,result.Edges,result.Flags,result.X,result.Y,result.Rows,Array.Empty<VirtualConnection>(),true,result.SpecialIds,result.SpecialKinds);
                    captured=result;captures++;
                    emit("virtual-topology","revision="+revision+",nativeRevision="+nativeRevision+",captureClock="+now+",tiles="+pcl.Length+",connections="+result.Records.Length+",bridges="+decks.Count+",input=copied-after-rebuild,seedExceptions=copied-signed-107160-records,macroThirdEndpoints=conditional,negativeCutPolicy=unvalidated,behavior=unchanged");
                }
                catch(Exception error) {emit("virtual-input-unknown","reason="+error.GetType().Name+",message="+error.Message);}
                finally {captureTicks+=Stopwatch.GetTimestamp()-start;}
            }
        }
        internal void Compare(string stage,long op,long parent,int player,int from,int to,int mode,int native)
        {
            lock(gate)
            {
                if(session==0||player<1||player>8)return;
                if(captured!=null)
                {
                    bool coherent=read(0x60AD6CC)==0;
                    var currentBuildings=GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();var currentUnits=GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                    foreach(var bridge in captured.Decks)
                    {
                        int slot=BridgeBuildingIndex.SpanIndex(bridge.Id,currentBuildings.Length);
                        if(slot<0||!BridgeSnapshot.Active(currentBuildings[slot].r_AliveState)||currentBuildings[slot].r_GlobalId!=bridge.Global||currentBuildings[slot].r_PlayerIdOwner!=bridge.Owner||currentBuildings[slot].r_CapturedByPlayerId!=bridge.Capturer||currentBuildings[slot].r_GatehouseId!=bridge.NativeParent)coherent=false;
                    }
                    for(int recordId=1;recordId<captured.ConnectionLimit&&coherent;recordId++)
                    {
                        int slot=BridgeBuildingIndex.SpanIndex(captured.Records[recordId].r_BuildingId,currentBuildings.Length);
                        if(slot>=0&&(!BridgeSnapshot.Active(currentBuildings[slot].r_AliveState)||currentBuildings[slot].r_GlobalId!=captured.Globals[slot]||currentBuildings[slot].r_PlayerIdOwner!=captured.Owners[slot]||currentBuildings[slot].r_CapturedByPlayerId!=captured.Capturers[slot]))coherent=false;
                        int unitSlot=BridgeBuildingIndex.SpanIndex(captured.Records[recordId].r_UnitId,currentUnits.Length);
                        if(unitSlot>=0&&(!BridgeSnapshot.Active(currentUnits[unitSlot].r_AliveState)||currentUnits[unitSlot].r_GlobalId!=captured.UnitGlobals[unitSlot]))coherent=false;
                    }
                    for(int a=1;a<=8&&coherent;a++)for(int b=1;b<=8;b++)if(captured.Allies[a,b]!=BridgeSnapshot.Allied(a,b)) {coherent=false;break;}
                    if(!coherent) {identity++;captured=null;}
                }
                var key=new QueryKey(stage,player,from,to,mode,native,revision,identity);
                if(repeats.TryGetValue(key,out Repeat repeat))
                {
                    coalesced++;repeat.Count++;
                    if(repeat.Parent!=parent)
                        emit("virtual-shadow-reference","stage="+stage+",op="+op+",parentOp="+parent+",sourceDefinition="+repeat.Id+",sourceQueryOp="+repeat.Op+",player="+player+",revision="+revision+",observationClock="+Stopwatch.GetTimestamp()+",sameNumericInput=True,resultTiming=source-query,behavior=unchanged");
                    repeat.Parent=parent;repeats[key]=repeat;return;
                }
                if(repeats.Count>4096)repeats.Clear();
                if(pending.Count>=32) {rejected++;return;}
                long definition=++queryId;repeats[key]=new Repeat {Id=definition,Op=op,Parent=parent,Count=1};
                IEnemyGateRoutePolicySnapshot gatePolicy=null;
                var provider=EnemyGatePathPolicyBridge.Current as IEnemyGateRoutePolicyProvider;
                if(provider!=null)provider.TryCaptureRoutePolicy(player,out gatePolicy);
                var request=new Request {Id=definition,Stage=stage,Op=op,Parent=parent,Player=player,From=from,To=to,Mode=mode,Native=native,Clock=Stopwatch.GetTimestamp(),Input=captured,GatePolicy=gatePolicy};
                emit("virtual-shadow-input","definition="+definition+",stage="+stage+",op="+op+",parentOp="+parent+",player="+player+",fromTile="+from+",toTile="+to+",mode="+mode+",observationClock="+request.Clock+",sourceSession="+(captured?.Session??0)+",captureClock="+(captured?.Clock??0)+",revision="+(captured?.Revision??-1)+",captureAvailable="+(captured!=null));
                if(stage=="keep-access")pending.AddFirst(request);else pending.AddLast(request);
            }
        }
        internal void CompareXY(string stage,long op,long parent,int player,int x,int y,int targetX,int targetY)
        {
            lock(gate)
            {
                var input=captured;if(input==null)return;
                int from=Tile(input,x,y),to=Tile(input,targetX,targetY);
                // Event exposes no audited group mode. Report both hypotheses explicitly.
                Compare(stage+"-mode-hypothesis",op,parent,player,from,to,0,-1);
                Compare(stage+"-mode-hypothesis",op,parent,player,from,to,1,-1);
            }
        }
        private static int Tile(Captured input,int x,int y)
        {if((uint)x>=800||(uint)y>=800)return -1;long tile=(long)input.Rows[y]+x;return tile>=0&&tile<input.Pcl.Length&&input.X[tile]==x&&input.Y[tile]==y?(int)tile:-1;}
        private bool Current(Request request) => request.Input!=null&&request.Input.Session==session&&request.Input.Revision==revision&&request.Input.Identity==identity&&(request.GatePolicy==null||request.GatePolicy.IsCurrent);
        private void Prepare(Request request)
        {
            var input=request.Input;var deck=new List<int>();var affected=new HashSet<int>();bool authorized=true,boundary=true;
            foreach(var bridge in input.Decks)
            {
                if(bridge.Owner<1||bridge.Owner>8||bridge.Capturer<0||bridge.Capturer>8) {authorized=false;continue;}
                if(input.Allies[request.Player,bridge.Owner]||bridge.Capturer!=0&&input.Allies[request.Player,bridge.Capturer])continue;
                bool open=false;foreach(int tile in bridge.Tiles)if((input.Flags[tile]&0x40000000)==0)open=true;
                if(open) {authorized&=bridge.Authorized;boundary&=bridge.BoundaryVerified;}
                foreach(int tile in bridge.Tiles) {if((input.Flags[tile]&0x40000000)!=0)continue;deck.Add(tile);if(input.Pcl[tile]!=0)affected.Add(input.Pcl[tile]);}
            }
            var connections=new List<VirtualConnection>();
            for(int recordId=1;recordId<input.ConnectionLimit;recordId++)
            {
                var r=input.Records[recordId];int buildingSlot=BridgeBuildingIndex.SpanIndex(r.r_BuildingId,input.Owners.Length);
                bool nativeAccess=(r.r_OwnerOrAccessPlayerId>0&&r.r_OwnerOrAccessPlayerId<9&&input.Allies[request.Player,r.r_OwnerOrAccessPlayerId])||buildingSlot>=0&&input.Capturers[buildingSlot]!=0;
                if(buildingSlot>=0&&input.Owners[buildingSlot]>0&&input.Owners[buildingSlot]<9)
                    nativeAccess&=input.Allies[request.Player,input.Owners[buildingSlot]]||input.Capturers[buildingSlot]>0&&input.Capturers[buildingSlot]<9&&input.Allies[request.Player,input.Capturers[buildingSlot]];
                bool known=NativeEndpoint(input,r.r_EntryTileId,r.r_PathComponentA)&&NativeEndpoint(input,r.r_ExitTileId,r.r_PathComponentB);
                if(r.r_BuildingId!=0)known&=buildingSlot>=0&&input.Globals[buildingSlot]!=0&&input.Globals[buildingSlot]==r.r_SubjectGlobalId;
                int unitSlot=BridgeBuildingIndex.SpanIndex(r.r_UnitId,input.UnitGlobals.Length);
                if(r.r_UnitId!=0)known&=unitSlot>=0&&input.UnitGlobals[unitSlot]!=0&&input.UnitGlobals[unitSlot]==r.r_SubjectGlobalId;
                int third=-1;
                if(r.r_PathComponentC>0) {known&=r.r_PathComponentC<input.Anchors.Length&&!affected.Contains(r.r_PathComponentC);if(known)third=input.Anchors[r.r_PathComponentC];known&=third>=0;}
                if(r.r_IsActive==1&&r.r_IsEnabledOrOpen!=0&&nativeAccess&&!known)
                {
                    request.UnknownRecords++;
                    if(request.UnknownRecords<=2)emit("virtual-connection-unknown","op="+request.Op+",recordId="+recordId+",captureClock="+input.Clock+",recordGlobal="+r.r_RecordGlobalId+",subjectGlobal="+r.r_SubjectGlobalId+",buildingId="+r.r_BuildingId+",unitId="+r.r_UnitId+",class="+(int)r.r_ConnectionClass+",entry="+r.r_EntryTileId+",exit="+r.r_ExitTileId+",components="+r.r_PathComponentA+"/"+r.r_PathComponentB+"/"+r.r_PathComponentC+",thirdAffected="+affected.Contains(r.r_PathComponentC)+",reason=endpoint-or-subject-global-unresolved");
                }
                connections.Add(new VirtualConnection(r.r_EntryTileId,r.r_ExitTileId,third,(int)r.r_ConnectionClass,r.r_IsActive==1,r.r_IsEnabledOrOpen!=0,nativeAccess,known));
            }
            var map=input.Map.WithConnections(connections.ToArray());
            request.DeckCells=deck.Count;
            request.Authorization=authorized&&boundary&&deck.Count==0&&request.GatePolicy!=null&&!request.Stage.Contains("hypothesis");
            request.Query=new VirtualBridgeQuery(map,request.From,request.To,request.Mode,deck,true,
                request.GatePolicy==null?(Func<int,int,bool>)null:request.GatePolicy.IsDirectionAllowed,
                // The tile cut is a hypothesis until all seed/closed-boundary contracts are proven.
                negativeProofComplete:true);
        }
        private static bool NativeEndpoint(Captured input,int tile,int component) => (uint)tile<(uint)input.Pcl.Length&&component>0&&input.Pcl[tile]==component;
        internal void Pump()
        {
            lock(gate)
            {
                long start=Stopwatch.GetTimestamp();
                try
                {
                    while((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency<1)
                    {
                        if(active==null) {if(pending.Count==0)break;active=pending.First.Value;pending.RemoveFirst();}
                        if(!Current(active)) {Deliver("Unknown","Unknown","stale-or-missing-topology-or-policy",0,false);active=null;continue;}
                        if(active.Query==null)Prepare(active);
                        active.Query.Step(64);
                        if(!active.Query.Complete)continue;
                        Deliver(active.Query.Result.ToString(),active.Authorization?active.Query.Result.ToString():"Unknown",active.Query.Reason,active.Query.Expanded,active.Query.StructureRequired);active=null;
                    }
                }
                catch(Exception error)
                {
                    if(active!=null)Deliver("Unknown","Unknown","calculation-error-"+error.GetType().Name,active.Query?.Expanded??0,false);
                    else emit("virtual-shadow-error","reason="+error.GetType().Name);
                    active=null;
                }
                finally {computeTicks+=Stopwatch.GetTimestamp()-start;}
            }
        }
        internal void FlushCosts()
        {
            lock(gate)
            {
                long now=Stopwatch.GetTimestamp();if(costClock==0) {costClock=now;return;}
                double seconds=(now-costClock)/(double)Stopwatch.Frequency;if(seconds<10)return;
                emit("interval-virtual-cost","seconds="+seconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",captures="+(captures-lastCaptures)+",captureMs="+Ms(captureTicks-lastCaptureTicks)+",computeMs="+Ms(computeTicks-lastComputeTicks)+",coalesced="+(coalesced-lastCoalesced)+",rejected="+(rejected-lastRejected)+",pending="+(pending.Count+(active==null?0:1))+",activeRevision="+revision+",retainedSnapshot="+(captured!=null));
                costClock=now;lastCaptures=captures;lastCaptureTicks=captureTicks;lastComputeTicks=computeTicks;lastCoalesced=coalesced;lastRejected=rejected;
            }
        }
        private void Deliver(string hypothesis,string policy,string reason,long expanded,bool structure)
        {
            emit("virtual-shadow","definition="+active.Id+",stage="+active.Stage+",op="+active.Op+",parentOp="+active.Parent+",player="+active.Player+",fromTile="+active.From+",toTile="+active.To+",nativeQueryOrder="+(active.Stage=="keep-access"?"keep-target-to-attacker":"not-observed")+",comparisonDirection=attacker-to-target,mode="+active.Mode+",observedOriginalReturn="+active.Native+",observationClock="+active.Clock+",sourceSession="+(active.Input?.Session??0)+",captureClock="+(active.Input?.Clock??0)+",revision="+(active.Input?.Revision??-1)+",geometricResult="+hypothesis+",policyResult="+policy+",policyEvidence="+(active.Authorization?"copied-baseline":"authorization-boundary-mode-or-gate-policy-unvalidated")+",reason="+reason+",candidateCutCells="+active.DeckCells+",unknownRecords="+active.UnknownRecords+",expanded="+expanded+",structureRequired="+structure+",behavior=unchanged");
        }
    }
}

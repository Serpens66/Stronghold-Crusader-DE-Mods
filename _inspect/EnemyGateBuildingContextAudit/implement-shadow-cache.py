from pathlib import Path
p=Path('Testmods/EnemyBridgePathTest/src/BridgeVirtualShadow.cs')
s=p.read_text()
def change(a,b):
    global s
    assert s.count(a)==1,a
    s=s.replace(a,b)
change('private readonly bool policyValid;', 'private readonly bool policyValid;\n            private readonly Captured content;')
change('IEnemyGateRoutePolicySnapshot policy,bool policyValid)\n            {this.policy', 'IEnemyGateRoutePolicySnapshot policy,bool policyValid,Captured content=null)\n            {this.content=content?.Core??content;this.policy')
change('revision==b.revision&&identity==b.identity&&ReferenceEquals(policy,b.policy)', '(content==null&&b.content==null?revision==b.revision&&identity==b.identity:ReferenceEquals(content,b.content))&&ReferenceEquals(policy,b.policy)')
change('^revision.GetHashCode()^identity.GetHashCode());', '^(content==null?revision.GetHashCode()^identity.GetHashCode():System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(content)));')
change('private Captured captured;', '''private Captured captured,lastContent;
        private readonly Dictionary<QueryKey,Preparation[]> preparations=new Dictionary<QueryKey,Preparation[]>();
        private VirtualBridgeWorkspace workspace;
        private long attempted,backgroundDeferred,prepareTicks,searchTicks,formatTicks,comparisonTicks,preparationHits,contentReused;
        private long lastPrepareTicks,lastSearchTicks,lastFormatTicks,lastComparisonTicks,lastPreparationHits,lastContentReused,lastDeferred;
        private sealed class Preparation
        {internal VirtualBridgeMap Map;internal int[] Deck;internal VirtualComponentLink[] Links;internal int Unknown;internal bool Authorized,Boundary;}''')
change('internal long Session,Revision,Identity,Clock;', 'internal long Session,Revision,Identity,Clock;\n            internal Captured Core;')
change('internal string Stage,InputReason;', 'internal string Stage,InputReason;\n            internal Request Alternative;\n            internal bool Scheduled;')
change('pending.Clear();active=null;repeats.Clear();groups.Clear();keepArtifact', 'pending.Clear();active=null;repeats.Clear();groups.Clear();preparations.Clear();lastContent=null;workspace=null;keepArtifact')
change('lastArtifactTicks=artifacts.Ticks;}}', 'lastArtifactTicks=artifacts.Ticks;attempted=backgroundDeferred=prepareTicks=searchTicks=formatTicks=comparisonTicks=preparationHits=contentReused=0;lastPrepareTicks=lastSearchTicks=lastFormatTicks=lastComparisonTicks=lastPreparationHits=lastContentReused=lastDeferred=0;}}')
change('",computeMs="+Ms(computeTicks)+",coalesced="', '",computeMs="+Ms(computeTicks)+",prepareMs="+Ms(prepareTicks)+",searchMs="+Ms(searchTicks)+",formatMs="+Ms(formatTicks)+",contentCompareMs="+Ms(comparisonTicks)+",preparationHits="+preparationHits+",contentReused="+contentReused+",attempted="+attempted+",backgroundDeferred="+backgroundDeferred+",comparisonScope=fresh-keeps-selected-targets-proven-bridge-groups,coalesced="')
change('captured=result;coordinateRows', '''long comparisonStart=Stopwatch.GetTimestamp();bool sameContent=SameContent(lastContent,result);comparisonTicks+=Stopwatch.GetTimestamp()-comparisonStart;
                    result.Core=sameContent?(lastContent.Core??lastContent):result;if(sameContent)contentReused++;lastContent=result;
                    captured=result;coordinateRows''')
change('",coordinatesReusedAfterExactCompare="+sameCoordinates+', '",contentReusedAfterExactCompare="+sameContent+",coordinatesReusedAfterExactCompare="+sameCoordinates+')
change('if(session==0||player<1||player>8)return;', 'if(session==0||player<1||player>8)return;\n                attempted++;\n                if(stage.StartsWith("group-formation")&&player!=8) {backgroundDeferred++;return;}')
change('var key=new QueryKey(stage,player,from,to,mode,native,revision,identity,gatePolicy,policyValid);', '''if(stage.StartsWith("group-formation"))
                {
                    backgroundDeferred++;
                    var retained=new Request {QuerySourceOp=op,Stage=stage,Op=op,Parent=parent,Player=player,From=from,To=to,Mode=mode,Native=native,Clock=Stopwatch.GetTimestamp(),Input=captured,InputReason=captured==null?missingInput:"valid-at-decision",GatePolicy=gatePolicy,PolicyValidAtDecision=policyValid};
                    if(mode==0) {if(groups.Count>=32)groups.Clear();groups[op]=retained;}
                    else if(groups.TryGetValue(op,out Request primary))primary.Alternative=retained;
                    return; // Promote retained Pre inputs only after stored deck-route evidence.
                }
                var key=new QueryKey(stage,player,from,to,mode,native,revision,identity,gatePolicy,policyValid,captured);''')
change('if(repeat.Parent!=parent&&player==8)', 'if(repeat.Parent!=parent&&player==8)') if False else None
change('if(player==8&&request.Input!=null&&request.PolicyValidAtDecision&&stage=="keep-access"&&!keepArtifact)', 'request.Scheduled=true;\n                if(player==8&&request.Input!=null&&request.PolicyValidAtDecision&&stage=="keep-access"&&!keepArtifact)')
change('var input=captured;if(input==null)return;\n                int from=Tile(input,x,y),to=Tile(input,targetX,targetY);', 'var input=captured;\n                int from=input==null?-1:Tile(input,x,y),to=input==null?-1:Tile(input,targetX,targetY);')
change('if(player!=8||groupArtifact||!groups.TryGetValue(groupOp,out Request request)||request.Input==null||!request.PolicyValidAtDecision)return;\n                groupArtifact=artifacts.Enqueue(request.Input.Session,request.Id,Artifact(request));', '''if(player!=8||!groups.TryGetValue(groupOp,out Request request)||request.Input==null||!request.PolicyValidAtDecision)return;
                if(!Promote(request))return;
                if(request.Alternative!=null)Promote(request.Alternative);
                if(!groupArtifact)groupArtifact=artifacts.Enqueue(request.Input.Session,request.Id,Artifact(request));''')
a=s.index('        private void Prepare(Request request)')
s=s[:a]+'''        private bool Promote(Request request)
        {
            if(request.Scheduled)return true;
            var key=new QueryKey(request.Stage,request.Player,request.From,request.To,request.Mode,request.Native,request.Input.Revision,request.Input.Identity,request.GatePolicy,request.PolicyValidAtDecision,request.Input);
            if(repeats.TryGetValue(key,out Repeat repeat))
            {
                coalesced++;repeat.Count++;repeat.Parent=request.Parent;repeat.LastOp=request.Op;repeats[key]=repeat;
                request.Id=repeat.Id;request.QuerySourceOp=repeat.Op;request.Scheduled=true;
                emit("virtual-shadow-reference","stage="+request.Stage+",op="+request.Op+",parentOp="+request.Parent+",sourceDefinition="+repeat.Id+",sourceQueryOp="+repeat.Op+",player="+request.Player+",captureClock="+request.Input.Clock+",observationClock="+request.Clock+",sameNumericInput=True,resultTiming=source-query,behavior=unchanged");return true;
            }
            if(pending.Count>=32) {rejected++;return false;}
            if(repeats.Count>4096) {FlushRepeats();repeats.Clear();}
            request.Id=++queryId;request.Scheduled=true;
            repeats[key]=new Repeat {Id=request.Id,Op=request.Op,Parent=request.Parent,Count=1,LastOp=request.Op};
            emit("virtual-shadow-input","definition="+request.Id+",stage="+request.Stage+",op="+request.Op+",parentOp="+request.Parent+",player="+request.Player+",fromTile="+request.From+",toTile="+request.To+",mode="+request.Mode+",observationClock="+request.Clock+",sourceSession="+request.Input.Session+",captureClock="+request.Input.Clock+",revision="+request.Input.Revision+",captureAvailable=True,inputReason="+request.InputReason+",policyValidAtDecision="+request.PolicyValidAtDecision+",selection=stored-deck-route");
            pending.AddFirst(request);return true;
        }
''' + s[a:]
change('var input=request.Input;var deck=new List<int>();', '''var input=request.Input;
            var preparationKey=new QueryKey("prepared",request.Player,0,0,0,0,input.Revision,input.Identity,request.GatePolicy,request.PolicyValidAtDecision,input);
            if(!preparations.TryGetValue(preparationKey,out Preparation[] variants))
            {if(preparations.Count>=128)preparations.Clear();preparations[preparationKey]=variants=new Preparation[3];}
            int variant=request.Variant%3;
            if(variants[variant]!=null) {preparationHits++;UsePreparation(request,variants[variant]);return;}
            var deck=new List<int>();''')
a=s.index('            var map=input.Map.WithConnections');b=s.index('        private static bool NativeEndpoint',a)
s=s[:a]+'''            var prepared=new Preparation {Map=input.Map.WithConnections(connections.ToArray()),Deck=deck.ToArray(),Links=componentLinks.ToArray(),Unknown=request.UnknownRecords,Authorized=authorized,Boundary=boundary};
            variants[variant]=prepared;UsePreparation(request,prepared);
        }
        private void UsePreparation(Request request,Preparation prepared)
        {
            request.UnknownRecords=prepared.Unknown;request.DeckCells=prepared.Deck.Length;
            request.Authorization=prepared.Authorized&&prepared.Boundary&&prepared.Deck.Length==0&&request.GatePolicy!=null&&!request.Stage.Contains("hypothesis");
            if(request.Variant==0&&request.Stage=="keep-access"&&(uint)request.From<(uint)request.Input.Pcl.Length&&(uint)request.To<(uint)request.Input.Pcl.Length)
                request.Macro=VirtualComponentControl.Evaluate(request.Input.Pcl[request.To],request.Input.Pcl[request.From],request.Mode,prepared.Links);
            request.Cuts[request.Variant]=prepared.Deck.Length;
            int from=request.Variant<3?request.From:request.To,to=request.Variant<3?request.To:request.From;
            if(workspace==null||workspace.Cut.Length!=prepared.Map.Components.Length)workspace=new VirtualBridgeWorkspace(prepared.Map.Components.Length);
            request.Query=new VirtualBridgeQuery(prepared.Map,from,to,request.Mode,prepared.Deck,true,
                request.GatePolicy==null?(Func<int,int,bool>)null:request.GatePolicy.IsDirectionAllowed,negativeProofComplete:true,workspace:workspace);
        }
''' + s[b:]
change('if(active.Query==null)Prepare(active);\n                        active.Query.Step(64);', 'if(active.Query==null) {long prepareStart=Stopwatch.GetTimestamp();try {Prepare(active);}finally {prepareTicks+=Stopwatch.GetTimestamp()-prepareStart;}}\n                        long searchStart=Stopwatch.GetTimestamp();try {active.Query.Step(64);}finally {searchTicks+=Stopwatch.GetTimestamp()-searchStart;}')
change('completedQueries++;\n            if(active.Variant==6)', '''long formatStart=Stopwatch.GetTimestamp();try {DeliverCore(hypothesis,policy,reason,expanded,structure);}finally {formatTicks+=Stopwatch.GetTimestamp()-formatStart;}
        }
        private void DeliverCore(string hypothesis,string policy,string reason,long expanded,bool structure)
        {
            completedQueries++;
            if(active.Variant==6)''')
change('",computeMs="+Ms(computeTicks-lastComputeTicks)+",coalesced="', '",computeMs="+Ms(computeTicks-lastComputeTicks)+",prepareMs="+Ms(prepareTicks-lastPrepareTicks)+",searchMs="+Ms(searchTicks-lastSearchTicks)+",formatMs="+Ms(formatTicks-lastFormatTicks)+",contentCompareMs="+Ms(comparisonTicks-lastComparisonTicks)+",preparationHits="+(preparationHits-lastPreparationHits)+",contentReused="+(contentReused-lastContentReused)+",backgroundDeferred="+(backgroundDeferred-lastDeferred)+",coalesced="')
change('costClock=now;lastCaptures=captures;', 'costClock=now;lastPrepareTicks=prepareTicks;lastSearchTicks=searchTicks;lastFormatTicks=formatTicks;lastComparisonTicks=comparisonTicks;lastPreparationHits=preparationHits;lastContentReused=contentReused;lastDeferred=backgroundDeferred;lastCaptures=captures;')
a=s.index('        private static bool NativeEndpoint')
s=s[:a]+'''        private static bool EqualArray<T>(T[] a,T[] b)
        {if(ReferenceEquals(a,b))return true;if(a==null||b==null||a.Length!=b.Length)return false;var comparer=EqualityComparer<T>.Default;for(int i=0;i<a.Length;i++)if(!comparer.Equals(a[i],b[i]))return false;return true;}
        private static bool SameContent(Captured a,Captured b)
        {
            if(a==null||a.Session!=b.Session||a.ConnectionLimit!=b.ConnectionLimit||a.Decks.Length!=b.Decks.Length)return false;
            if(!EqualArray(a.Pcl,b.Pcl)||!EqualArray(a.Edges,b.Edges)||!EqualArray(a.Flags,b.Flags)||!EqualArray(a.X,b.X)||!EqualArray(a.Y,b.Y)||!EqualArray(a.Rows,b.Rows)||!EqualArray(a.SpecialIds,b.SpecialIds)||!EqualArray(a.SpecialKinds,b.SpecialKinds)||!EqualArray(a.Owners,b.Owners)||!EqualArray(a.Capturers,b.Capturers)||!EqualArray(a.Globals,b.Globals)||!EqualArray(a.UnitGlobals,b.UnitGlobals))return false;
            for(int i=0;i<9;i++)for(int j=0;j<9;j++)if(a.Allies[i,j]!=b.Allies[i,j])return false;
            for(int i=1;i<a.ConnectionLimit;i++)
            {
                var x=a.Records[i];var y=b.Records[i];
                if(x.r_IsActive!=y.r_IsActive||x.r_ConnectionClass!=y.r_ConnectionClass||x.r_RecordGlobalId!=y.r_RecordGlobalId||x.r_BuildingId!=y.r_BuildingId||x.r_UnitId!=y.r_UnitId||x.r_SubjectGlobalId!=y.r_SubjectGlobalId||x.r_IsEnabledOrOpen!=y.r_IsEnabledOrOpen||x.r_EntryTileId!=y.r_EntryTileId||x.r_ExitTileId!=y.r_ExitTileId||x.r_PathComponentA!=y.r_PathComponentA||x.r_PathComponentB!=y.r_PathComponentB||x.r_OwnerOrAccessPlayerId!=y.r_OwnerOrAccessPlayerId||x.r_PathComponentC!=y.r_PathComponentC)return false;
            }
            for(int i=0;i<a.Decks.Length;i++)
            {var x=a.Decks[i];var y=b.Decks[i];if(x.Id!=y.Id||x.Global!=y.Global||x.Owner!=y.Owner||x.Capturer!=y.Capturer||x.NativeParent!=y.NativeParent||x.Authorized!=y.Authorized||x.BoundaryVerified!=y.BoundaryVerified||!EqualArray(x.Tiles,y.Tiles))return false;}
            return true;
        }
''' + s[a:]
p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode())

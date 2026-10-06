from pathlib import Path
p=Path('Testmods/EnemyBridgePathTest/src/BridgeVirtualShadow.cs');s=p.read_text()
s=s.replace('identity,nextCapture,captureTicks','identity,captureTicks')
s=s.replace('private Request active;','''private Request active;
        private readonly BridgeInputArtifact artifacts;
        private readonly Dictionary<long,Request> groups=new Dictionary<long,Request>();
        private bool keepArtifact,groupArtifact;
        internal bool ArtifactPending(long value) => artifacts.Pending(value);
        internal void PumpArtifacts() {lock(gate)artifacts.Pump();}''')
s=s.replace('internal bool Authorization;','''internal bool Authorization,PolicyValidAtDecision;
            internal int Variant;
            internal VirtualReachability Macro=VirtualReachability.Unknown;
            internal readonly VirtualReachability[] Results=new VirtualReachability[6];
            internal readonly string[] Reasons=new string[6];
            internal readonly int[] Cuts=new int[6];
            internal readonly long[] Expansions=new long[6];''')
s=s.replace('internal BridgeVirtualShadow(Action<string,string> emit,Func<int,int> read) {this.emit=emit;this.read=read;}','''internal BridgeVirtualShadow(Action<string,string> emit,Func<int,int> read,Action<long,string,string> artifactEmit=null,string artifactDirectory=null)
        {
            this.emit=emit;this.read=read;
            string folder=artifactDirectory??System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(BridgeVirtualShadow).Assembly.Location),"Diagnostics",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            artifacts=new BridgeInputArtifact(folder,artifactEmit??((_,kind,detail)=>emit(kind,detail)));
        }''')
s=s.replace('repeats.Clear();nextCapture=0;identity++','repeats.Clear();groups.Clear();keepArtifact=groupArtifact=false;identity++')
s=s.replace('long now=Stopwatch.GetTimestamp();if(now<nextCapture)return;\n                nextCapture=now+Stopwatch.Frequency;','long now=Stopwatch.GetTimestamp();')
s=s.replace('Input=captured,GatePolicy=gatePolicy};','Input=captured,GatePolicy=gatePolicy,PolicyValidAtDecision=gatePolicy==null||gatePolicy.IsCurrent};')
s=s.replace('if(stage=="keep-access")pending.AddFirst(request);else pending.AddLast(request);','''if(player==8&&request.Input!=null&&stage=="keep-access"&&!keepArtifact)
                {keepArtifact=artifacts.Enqueue(session,definition,Artifact(request));}
                if(stage.StartsWith("group-formation")&&mode==0)
                {if(groups.Count>=32)groups.Clear();groups[op]=request;}
                if(stage=="keep-access")pending.AddFirst(request);else pending.AddLast(request);''')
start=s.index('        private bool Current(Request request)')
end=s.index('        private void Prepare(Request request)',start)
s=s[:start]+'''        // Publication changes do not invalidate an immutable historical input.
        // The live checks were made once, at the decision, on the simulation thread.
        private bool Current(Request request) => request.Input!=null&&request.Input.Session==session&&request.PolicyValidAtDecision;
        internal void BridgeGroup(long groupOp,int player)
        {
            lock(gate)
            {
                if(player!=8||groupArtifact||!groups.TryGetValue(groupOp,out Request request)||request.Input==null)return;
                groupArtifact=artifacts.Enqueue(request.Input.Session,request.Id,Artifact(request));
            }
        }
''' +s[end:]
s=s.replace('foreach(var bridge in input.Decks)\n            {\n                if(bridge.Owner','foreach(var bridge in input.Decks)\n            {\n                if(request.Variant%3==0||request.Variant%3==1&&bridge.Id!=703)continue;\n                if(bridge.Owner')
s=s.replace('var connections=new List<VirtualConnection>();','var connections=new List<VirtualConnection>();var componentLinks=new List<VirtualComponentLink>();\n            request.UnknownRecords=0;')
s=s.replace('if(buildingSlot>=0&&input.Owners[buildingSlot]>0', '''componentLinks.Add(new VirtualComponentLink(r.r_PathComponentA,r.r_PathComponentB,r.r_PathComponentC,(int)r.r_ConnectionClass,r.r_IsActive==1&&r.r_IsEnabledOrOpen!=0&&nativeAccess));
                if(buildingSlot>=0&&input.Owners[buildingSlot]>0''')
s=s.replace('if(request.UnknownRecords<=2)emit','if(request.Variant==0&&request.UnknownRecords<=2)emit')
s=s.replace('request.Query=new VirtualBridgeQuery(map,request.From,request.To,request.Mode,deck,true,','''if(request.Variant==0&&request.Stage=="keep-access"&&(uint)request.From<(uint)input.Pcl.Length&&(uint)request.To<(uint)input.Pcl.Length)
                request.Macro=VirtualComponentControl.Evaluate(input.Pcl[request.To],input.Pcl[request.From],request.Mode,componentLinks.ToArray());
            request.Cuts[request.Variant]=deck.Count;
            int from=request.Variant<3?request.From:request.To,to=request.Variant<3?request.To:request.From;
            request.Query=new VirtualBridgeQuery(map,from,to,request.Mode,deck,true,''')
s=s.replace('"stale-or-missing-topology-or-policy"','"missing-decision-input-or-policy"')
s=s.replace('''Deliver(active.Query.Result.ToString(),active.Authorization?active.Query.Result.ToString():"Unknown",active.Query.Reason,active.Query.Expanded,active.Query.StructureRequired);active=null;''','''active.Results[active.Variant]=active.Query.Result;active.Reasons[active.Variant]=active.Query.Reason;active.Expansions[active.Variant]=active.Query.Expanded;
                        if(++active.Variant<6) {active.Query=null;continue;}
                        Deliver(active.Results[2].ToString(),"Unknown","controlled-six-variant-comparison",Sum(active.Expansions),false);active=null;''')
s=s.replace('''        private void Deliver(string hypothesis''','''        private static long Sum(long[] values) {long sum=0;foreach(long value in values)sum+=value;return sum;}
        private void Deliver(string hypothesis''')
s=s.replace('''            completedQueries++;
            emit("virtual-shadow"''','''            completedQueries++;
            if(active.Variant==6)
            {
                bool observed=active.Stage=="keep-access"&&active.Native>=0;
                bool macroMatch=observed&&active.Macro!=VirtualReachability.Unknown&&(active.Macro==VirtualReachability.Reachable)==(active.Native!=0);
                bool geometryMatch=observed&&active.Results[3]!=VirtualReachability.Unknown&&(active.Results[3]==VirtualReachability.Reachable)==(active.Native!=0);
                for(int direction=0;direction<2;direction++)
                {
                    int b=direction*3;bool baseline=active.Results[b]==VirtualReachability.Reachable;
                    emit("virtual-control","definition="+active.Id+",op="+active.Op+",parentOp="+active.Parent+",player="+active.Player+",direction="+(direction==0?"attacker-to-target":"target-to-attacker")+",mode="+active.Mode+",modeEvidence="+(observed?"observed-CF-mode":"hypothesis")+",noCut="+active.Results[b]+",only703="+active.Results[b+1]+",allHostile="+active.Results[b+2]+",cutCells="+active.Cuts[b]+"/"+active.Cuts[b+1]+"/"+active.Cuts[b+2]+",reasons=["+active.Reasons[b]+";"+active.Reasons[b+1]+";"+active.Reasons[b+2]+"],macroControl="+active.Macro+",nativeBoolean="+active.Native+",nativeDirection=target-to-attacker,macroMatchesNative="+macroMatch+",geometryMatchesNative="+geometryMatch+",cutAssessment="+(observed?macroMatch&&geometryMatch&&baseline?"conditional-baseline-agrees":"blocked-baseline-mismatch-or-unknown":baseline?"geometric-hypothesis-only":"blocked-geometric-baseline")+",historicalInput=True,captureClock="+active.Input.Clock+",revision="+active.Input.Revision+",policyResult=Unknown,behavior=unchanged");
                }
            }
            emit("virtual-shadow"''')
s=s.replace('candidateCutCells="+active.DeckCells','candidateCutCells="+(active.Variant==6?active.Cuts[2]:active.DeckCells)')
s=s.replace('",reason="+reason+', '",historicalInput=True,decisionInputMissing="+(active.Input==null)+",policyValidAtDecision="+active.PolicyValidAtDecision+",reason="+reason+')
s=s.replace('",retainedSnapshot="+(captured!=null)', '",retainedSnapshot="+(captured!=null)+",artifactBytes="+artifacts.Bytes+",artifactWriteMs="+Ms(artifacts.Ticks)')
# Lazy, sectioned binary input. No live reads or native delegate is retained.
pos=s.index('        internal void FlushCosts()')
artifact='''        private IEnumerable<byte[]> Artifact(Request request)
        {
            var input=request.Input;
            yield return BridgeInputArtifact.Header("nativeHash="+BridgeNativeDefinition.NativeHash+"\\nbackendHash="+BridgeNativeDefinition.BackendHash+"\\nsession="+input.Session+"\\nrevision="+input.Revision+"\\nidentity="+input.Identity+"\\ncaptureClock="+input.Clock+"\\nobservationClock="+request.Clock+"\\ndefinition="+request.Id+"\\nop="+request.Op+"\\nparent="+request.Parent+"\\nstage="+request.Stage+"\\nplayer="+request.Player+"\\nfrom="+request.From+"\\nto="+request.To+"\\nmode="+request.Mode+"\\nnativeBoolean="+request.Native+"\\nconnectionLimit="+input.ConnectionLimit+"\\npolicyValidAtDecision="+request.PolicyValidAtDecision+"\\nnativeDirection=target-to-attacker\\nmovementDirection=attacker-to-target\\nvariants=none,only703,all-hostile\\ngateFilter="+(request.GatePolicy==null?"absent":"immutable-player-direction-mask"));
            foreach(var block in BridgeInputArtifact.Section("components",input.Pcl,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("edges",input.Edges,1))yield return block;
            foreach(var block in BridgeInputArtifact.Section("flags",input.Flags,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("x",input.X,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("y",input.Y,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("rows",input.Rows,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("specialIds",input.SpecialIds,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("specialKinds",input.SpecialKinds,2))yield return block;
            foreach(var block in BridgeInputArtifact.Section("owners",input.Owners,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("capturers",input.Capturers,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("globals",input.Globals,4))yield return block;
            foreach(var block in BridgeInputArtifact.Section("unitGlobals",input.UnitGlobals,4))yield return block;
            var allies=new byte[81];for(int a=0;a<9;a++)for(int b=0;b<9;b++)allies[a*9+b]=(byte)(input.Allies[a,b]?1:0);
            foreach(var block in BridgeInputArtifact.Section("allies",allies,1))yield return block;
            // All consumed public record members, including raw component C (no guessed endpoint).
            var records=new int[input.Records.Length*13];for(int i=0;i<input.Records.Length;i++)
            {
                var r=input.Records[i];int p=i*13;
                records[p]=r.r_IsActive;records[p+1]=(int)r.r_ConnectionClass;records[p+2]=unchecked((int)r.r_RecordGlobalId);
                records[p+3]=r.r_BuildingId;records[p+4]=r.r_UnitId;records[p+5]=unchecked((int)r.r_SubjectGlobalId);records[p+6]=r.r_IsEnabledOrOpen;
                records[p+7]=r.r_EntryTileId;records[p+8]=r.r_ExitTileId;records[p+9]=r.r_PathComponentA;records[p+10]=r.r_PathComponentB;records[p+11]=r.r_OwnerOrAccessPlayerId;records[p+12]=r.r_PathComponentC;
            }
            foreach(var block in BridgeInputArtifact.Section("records13",records,4))yield return block;
            var decks=new List<int>();foreach(var d in input.Decks)
            {decks.Add(d.Id);decks.Add(unchecked((int)d.Global));decks.Add(d.Owner);decks.Add(d.Capturer);decks.Add(d.NativeParent);decks.Add(d.Authorized?1:0);decks.Add(d.BoundaryVerified?1:0);decks.Add(d.Tiles.Length);decks.AddRange(d.Tiles);}
            foreach(var block in BridgeInputArtifact.Section("decks",decks.ToArray(),4))yield return block;
            // Mask generation is lazy in small pieces; snapshot getter is copied managed data.
            foreach(var block in BridgeInputArtifact.Section("gateMaskHeader",new[]{input.Pcl.Length,request.GatePolicy==null?0:1},4))yield return block;
            for(int start=0;start<input.Pcl.Length;start+=512)
            {
                var mask=new byte[Math.Min(512,input.Pcl.Length-start)];for(int i=0;i<mask.Length;i++)
                    for(int direction=0;direction<8;direction++)if(request.GatePolicy==null||request.GatePolicy.IsDirectionAllowed(start+i,direction))mask[i]|=(byte)(1<<direction);
                foreach(var block in BridgeInputArtifact.Section("gateMaskChunk",mask,1))yield return block;
            }
        }
'''
s=s[:pos]+artifact+s[pos:]
p.write_bytes(s.replace('\n','\r\n').encode())
for name in ['EnemyBridgePathTest.csproj','EnemyBridgePathTest.PolicyTests.csproj']:
 p=Path('Testmods/EnemyBridgePathTest')/name;s=p.read_text().replace('<Compile Include="src\\VirtualBridgeGraph.cs" />','<Compile Include="src\\VirtualBridgeGraph.cs" /><Compile Include="src\\VirtualComponentControl.cs" /><Compile Include="src\\BridgeInputArtifact.cs" />');p.write_bytes(s.replace('\n','\r\n').encode())
p=Path('Testmods/EnemyBridgePathTest/src/BridgeDecisionTrace.cs');s=p.read_text().replace('new BridgeVirtualShadow(Observe,Read)','new BridgeVirtualShadow(Observe,Read,ArtifactObservation)')
s=s.replace('internal void Observe(string kind,string detail)','''private void ArtifactObservation(long sourceSession,string kind,string detail)
        {
            var record=StampRecord(new Record {Kind=kind,Detail=detail});record.Session=sourceSession;
            // This callback also runs after End; do not replace its historical session.
            Interlocked.Increment(ref queued);lines.Enqueue(record);
        }
        internal void Observe(string kind,string detail)''')
s=s.replace('long ended=endedSessions.Dequeue();','long ended=endedSessions.Peek();if(VirtualShadow?.ArtifactPending(ended)==true)return;endedSessions.Dequeue();')
p.write_bytes(s.replace('\n','\r\n').encode())
p=Path('Testmods/EnemyBridgePathTest/src/BridgeDiagnostics.cs');s=p.read_text().replace('Trace.VirtualShadow?.Pump();Trace.Drain();','Trace.VirtualShadow?.Pump();Trace.VirtualShadow?.PumpArtifacts();Trace.Drain();')
needle='foreach(var frame in frames)\n            {'
assert needle in s
s=s.replace(needle,needle+'\n                if(frame.Kind!="unit")Trace.VirtualShadow?.BridgeGroup(frame.TraceId,frame.Player);',1)
p.write_bytes(s.replace('\n','\r\n').encode())

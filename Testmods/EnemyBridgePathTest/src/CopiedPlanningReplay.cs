using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // No live integration. A mismatch is evidence against release of the decision fix.
    internal static class CopiedPlanningReplay
    {
        private static T[] Values<T>(BridgePlanningImporter.Artifact artifact,string key,int width) where T:struct
        {if(!artifact.Sections.TryGetValue(key,out byte[] bytes)||bytes.Length%width!=0)throw new ArgumentException("missing-geometry:"+key);var result=new T[bytes.Length/width];Buffer.BlockCopy(bytes,0,result,0,bytes.Length);return result;}
        internal static bool TryBaseline(BridgePlanningImporter.Artifact artifact,out string reason)
        {
            reason="missing-historical-planning-values";if(artifact?.Planning==null)return false;
            try
            {
                ushort[] components=Values<ushort>(artifact,"components",2),x=Values<ushort>(artifact,"x",2),y=Values<ushort>(artifact,"y",2);
int[] rows=Values<int>(artifact,"rows",4),flags=Values<int>(artifact,"flags",4);byte[] edges=Values<byte>(artifact,"edges",1);
                if(components.Length!=320800||rows.Length!=800)throw new ArgumentException("incomplete-packed-geometry");
                Equal(components,CopiedPlanningBundle.Resolve(artifact.Planning,"components"),"physical-components");Equal(edges,CopiedPlanningBundle.Resolve(artifact.Planning,"edges"),"physical-edges");Equal(flags,CopiedPlanningBundle.Resolve(artifact.Planning,"flags"),"physical-flags");
                var map=new VirtualBridgeMap(artifact.Planning.Session,artifact.Planning.Revision,components,edges,flags,x,y,rows,Array.Empty<VirtualConnection>(),true,artifact.Sections.ContainsKey("specialIds")?Values<ushort>(artifact,"specialIds",2):Array.Empty<ushort>(),artifact.Sections.ContainsKey("specialKinds")?Values<short>(artifact,"specialKinds",2):Array.Empty<short>());
                return TryBaseline(artifact.Planning,map,out reason);
            }
            catch(Exception error){reason="invalid-replay-geometry:"+error.GetType().Name+":"+error.Message;return false;}
        }
        private static int[] Ints(byte[] bytes){if(bytes.Length%4!=0)throw new ArgumentException("integer-extent");var data=new int[bytes.Length/4];Buffer.BlockCopy(bytes,0,data,0,bytes.Length);return data;}
        private static void Equal(Array actual,byte[] expected,string name)
        {int bytes=Buffer.ByteLength(actual);if(bytes!=expected.Length)throw new InvalidOperationException("baseline-extent:"+name);var data=new byte[bytes];Buffer.BlockCopy(actual,0,data,0,bytes);for(int i=0;i<bytes;i++)if(data[i]!=expected[i])throw new InvalidOperationException("baseline-mismatch:"+name+":"+i);}
        private static void Compare(BridgePlanningCapture.Bundle b,VirtualPlanningState actual,VirtualBridgeMap map,string stage)
        {
            VirtualPlanningInput ignored;VirtualPlanningState expected;string reason;
            if(!CopiedPlanningBundle.TryRead(b,map,stage,out ignored,out expected,out reason))throw new InvalidOperationException(reason);
            Equal(actual.Seeds,CopiedPlanningBundle.Resolve(b,stage+"/seeds"),stage+"/seeds");Equal(actual.Distance,CopiedPlanningBundle.Resolve(b,stage+"/distance"),stage+"/distance");Equal(actual.Visit,CopiedPlanningBundle.Resolve(b,stage+"/visits"),stage+"/visits");Equal(actual.Queue,CopiedPlanningBundle.Resolve(b,stage+"/queue"),stage+"/queue");Equal(actual.QueueY,CopiedPlanningBundle.Resolve(b,stage+"/queueRows"),stage+"/queueRows");
            if(actual.Generation!=expected.Generation||actual.MaxDistance!=expected.MaxDistance||actual.Level!=expected.Level||actual.Head!=expected.Head||actual.Tail!=expected.Tail||actual.BatchStart!=expected.BatchStart||actual.BatchEnd!=expected.BatchEnd)throw new InvalidOperationException("baseline-control-mismatch:"+stage);
            string pre=stage=="caller-post"?"distance-pre":stage.Replace("-post","-pre");int[] control=Ints(CopiedPlanningBundle.Resolve(b,pre+"/controls")),queue=Ints(CopiedPlanningBundle.Resolve(b,pre+"/queueControls"));control[1]=actual.Generation;control[0x8c/4]=actual.MaxDistance;queue[0]=actual.Level;queue[1]=actual.Head;queue[2]=actual.BatchStart;queue[3]=actual.Tail;queue[4]=actual.BatchEnd;Equal(control,CopiedPlanningBundle.Resolve(b,stage+"/controls"),stage+"/all-controls");Equal(queue,CopiedPlanningBundle.Resolve(b,stage+"/queueControls"),stage+"/all-queue-controls");
            if(actual.Candidates.Count!=expected.Candidates.Count)throw new InvalidOperationException("baseline-candidate-count:"+stage);for(int i=0;i<actual.Candidates.Count;i++)if(actual.Candidates[i]!=expected.Candidates[i])throw new InvalidOperationException("baseline-candidate-order:"+stage);
            byte[] candidateCapacity=(byte[])CopiedPlanningBundle.Resolve(b,pre+"/candidateCapacity8").Clone();if(candidateCapacity.Length!=8000)throw new InvalidOperationException("candidate-capacity");for(int i=0;i<actual.Candidates.Count;i++)Buffer.BlockCopy(BitConverter.GetBytes(actual.Candidates[i]),0,candidateCapacity,i*8,4);Equal(candidateCapacity,CopiedPlanningBundle.Resolve(b,stage+"/candidateCapacity8"),stage+"/whole-candidate-table-preserved-second-column");
        }
        internal static bool TryBaseline(BridgePlanningCapture.Bundle bundle,VirtualBridgeMap map,out string reason) => TryBaseline(bundle,map,out reason,out _);
        private static bool TryBaseline(BridgePlanningCapture.Bundle bundle,VirtualBridgeMap map,out string reason,out VirtualPlanningState calculated)
        {
            calculated=null;
            reason="missing-historical-planning-values";if(bundle==null)return false;
            try
            {
                VirtualPlanningInput input;VirtualPlanningState state;
                if(!CopiedPlanningBundle.TryRead(bundle,map,"caller-pre",out input,out state,out reason))return false;
                int[] seed=Ints(CopiedPlanningBundle.Resolve(bundle,"seed-pre/arguments4")),players=Ints(CopiedPlanningBundle.Resolve(bundle,"playerScalars7"));
                if(seed.Length!=4||seed[0]!=bundle.Target||players.Length!=63)throw new ArgumentException("seed-player-contract");
                int x=players[bundle.Target*7+1],y=players[bundle.Target*7+2];if(y<0||y>=800)throw new ArgumentException("target-castle-row");int tile=checked(input.Rows[y]+x);
                Func<int,int,int?> callerRegions;if(!CopiedPlanningRegions.TryCreate(bundle,"caller-pre",false,bundle.Attacker,0,out callerRegions,out reason))return false;
                int origin=map.Components[players[bundle.Attacker*7+3]],destination=map.Components[tile];int? callerResult=callerRegions(destination,origin);
                if(!callerResult.HasValue||(callerResult.Value!=0)!=(seed[1]!=0))throw new InvalidOperationException("baseline-caller-region-parameter-mismatch");
                int[] callerControls=Ints(CopiedPlanningBundle.Resolve(bundle,"caller-pre/controls"));
                if(origin!=0&&destination!=0&&origin!=destination&&players[bundle.Attacker*7]!=0&&players[bundle.Target*7]!=0){callerControls[0xc4/4]=unchecked(callerControls[0xc4/4]+1);callerControls[0xc0/4]=callerResult.Value!=0?0:1;}
                Equal(callerControls,CopiedPlanningBundle.Resolve(bundle,"seed-pre/controls"),"caller-region-scratch-controls");
                if(BitConverter.ToInt32(CopiedPlanningBundle.Resolve(bundle,"seed-pre/selectedBank"),0)!=bundle.Target||BitConverter.ToInt32(CopiedPlanningBundle.Resolve(bundle,"caller-post/selectedBank"),0)!=bundle.Target)throw new InvalidOperationException("caller-selected-distance-bank");
                Array.Clear(state.Seeds,0,state.Seeds.Length);Compare(bundle,state,map,"seed-pre");
                if(!VirtualBridgePlanning.Seed(input,state,tile,players[bundle.Target*7]!=0,seed[1]!=0))throw new InvalidOperationException(state.Reason);Compare(bundle,state,map,"seed-post");
                Compare(bundle,state,map,"distance-pre"); // Carry computed seed output; never reload a recorded Post state.
                int[] distance=Ints(CopiedPlanningBundle.Resolve(bundle,"distance-pre/arguments4"));if(distance.Length!=4||distance[3]!=bundle.Attacker)throw new ArgumentException("distance-player-contract");
                Func<int,int,int?> regions;if(!CopiedPlanningRegions.TryCreate(bundle,"distance-pre",true,bundle.Attacker,0,out regions,out reason))return false;
                if(!VirtualBridgePlanning.Distance(input,state,distance[0],distance[1],distance[2],regions))throw new InvalidOperationException(state.Reason);Compare(bundle,state,map,"distance-post");Compare(bundle,state,map,"caller-post");
                int selections=0;
                foreach(var pair in bundle.Sections)if(pair.Key.StartsWith("target/",StringComparison.Ordinal)&&pair.Key.EndsWith("/values8",StringComparison.Ordinal))
                {
                    string prefix=pair.Key.Substring(0,pair.Key.Length-7);int[] values=Ints(pair.Value),args=Ints(CopiedPlanningBundle.Resolve(bundle,prefix+"argumentsAndTiles4"));
                    if(values.Length!=8||args.Length!=4||args[0]!=bundle.Attacker||args[1]!=bundle.Target)throw new ArgumentException("selection-players");
                    var selected=new VirtualTargetSelection(input,state,args[2],args[3],Ints(CopiedPlanningBundle.Resolve(bundle,"componentCounts1000")));
                    if(selected.SeedMode!=values[2]||selected.Seed!=values[3]||selected.Region!=values[4]||selected.Count!=values[7]||selected.TargetCastleRegion!=values[6])throw new InvalidOperationException("baseline-target-selection-mismatch");
                    // Effective Fixes count is captured separately; no presumed equality to native.
                    selections++;
                }
                reason=selections==0?"baseline-fields-match;target-selection-not-observed":"continuous-caller-seed-distance-fields-and-native-selection-match;effective-fixes-count-separate";calculated=state;return true;
            }
            catch(Exception error){reason="baseline-not-proven:"+error.GetType().Name+":"+error.Message;return false;}
        }

        // One state from caller entry through computed seed/distance and observed
        // 2C5A0 projection. This does not invent an unrecorded 2C480 target choice.
        internal static bool TryVariants(BridgePlanningImporter.Artifact artifact,out string report,int selectedBridge=0,uint selectedGlobal=0)
        {
            report="missing-historical-planning-values";string baseline;
            if(!TryBaseline(artifact,out baseline)){report=baseline;return false;}
            try
            {
                var b=artifact.Planning;
                ushort[] components=Values<ushort>(artifact,"components",2),x=Values<ushort>(artifact,"x",2),y=Values<ushort>(artifact,"y",2);
                if((b.Sections.ContainsKey("consumer/pre/identity")||b.References.ContainsKey("consumer/pre/identity"))&&!TryEffectiveCounts(b,components,out _,out string countReason)){report=countReason+";fullVariants=not-evaluated-before-valid-baseline;behaviorFix=disabled";return false;}

                var map=new VirtualBridgeMap(b.Session,b.Revision,components,Values<byte>(artifact,"edges",1),Values<int>(artifact,"flags",4),x,y,Values<int>(artifact,"rows",4),Array.Empty<VirtualConnection>(),true,Values<ushort>(artifact,"specialIds",2),Values<short>(artifact,"specialKinds",2));
                int[] players=Ints(CopiedPlanningBundle.Resolve(b,"playerScalars7")),rr=Ints(CopiedPlanningBundle.Resolve(b,"rowRecords"));
                int targetTile=map.Tile(players[b.Target*7+1],players[b.Target*7+2]),originTile=players[b.Attacker*7+3];
                if((uint)originTile>=320800||targetTile<0||players[b.Attacker*7+4]!=b.Target)throw new ArgumentException("caller-player-target-or-castle");
                var lines=new List<string>();lines.Add("baseline="+baseline+",dirty="+b.Dirty+",nativeGeneration="+b.Revision+",planningClock="+b.Clock+",captureClock="+artifact.Metadata["captureClock"]+",observationClock="+artifact.Metadata["observationClock"]+",inputTiming="+artifact.Metadata["inputTiming"]+",negativeEligible=False,behaviorFix=disabled");
                ValidateConnectionIdentities(artifact);
                int[] parents=Values<int>(artifact,"coupledParents5",4),decks=Values<int>(artifact,"decks",4);var singles=new Dictionary<string,int[]>();var cutAll=new List<int>();
                if(parents.Length%5!=0)throw new ArgumentException("coupling-definition-extent");
                for(int p=0;p<decks.Length;)
                {
                    if(p+8>decks.Length)throw new ArgumentException("deck-definition");int id=decks[p],owner=decks[p+2],capturer=decks[p+3],count=decks[p+7];p+=8;
                    if(count<0||p+count>decks.Length)throw new ArgumentException("deck-extent");
                    int parent=-1;for(int q=0;q<parents.Length;q+=5)if(parents[q]==id){if(parent!=-1)throw new ArgumentException("ambiguous-coupling");parent=q;}
                    if(parent<0)throw new ArgumentException("unproven-coupling");
                    if(parents.Length%5!=0||decks[p-3]==0)throw new ArgumentException("unconfirmed-coupling-policy");
                    uint[] globals=Values<uint>(artifact,"globals",4);int[] owners=Values<int>(artifact,"owners",4),capturers=Values<int>(artifact,"capturers",4);
                    int gateId=parents[parent+1];if(id<1||gateId<1||id>globals.Length||gateId>globals.Length||globals[id-1]!=unchecked((uint)decks[p-7])||globals[gateId-1]!=unchecked((uint)parents[parent+2])||owners[id-1]!=owner||capturers[id-1]!=capturer||owners[gateId-1]!=parents[parent+3]||capturers[gateId-1]!=parents[parent+4])throw new ArgumentException("coupling-identity-role-mismatch");
                    bool allowed=Related(artifact,b.Attacker,owner)||Related(artifact,b.Attacker,capturer)||Related(artifact,b.Attacker,parents[parent+3])||Related(artifact,b.Attacker,parents[parent+4]);
                    if(!allowed){var cells=new int[count];Array.Copy(decks,p,cells,0,count);cutAll.AddRange(cells);uint global=unchecked((uint)decks[p-7]);if(selectedBridge==0||selectedBridge==id&&selectedGlobal==global)singles.Add(id+"/g"+global,cells);}p+=count;
                }
                if(selectedBridge!=0&&(selectedGlobal==0||singles.Count!=1))throw new ArgumentException("selected-bridge-missing-stale-or-authorized");
                var variants=new List<KeyValuePair<string,int[]>> {new KeyValuePair<string,int[]>("none",Array.Empty<int>()),new KeyValuePair<string,int[]>("rebuild-only-control",Array.Empty<int>())};
                foreach(var single in singles)variants.Add(new KeyValuePair<string,int[]>("single:"+single.Key,single.Value));
                variants.Add(new KeyValuePair<string,int[]>("all-unallowed-coupled",cutAll.ToArray()));
                for(int variant=0;variant<variants.Count;variant++)
                {
                    VirtualPlanningInput input;VirtualPlanningState state;string reason;
                    if(!CopiedPlanningBundle.TryRead(b,map,"caller-pre",out input,out state,out reason))throw new ArgumentException(reason);
                    VirtualBridgeMap topology=map;int[] counts=Histogram(map.Components);int[] cut=variants[variant].Value;
                    if(variant!=0)
                    {
                        var ends=new int[800];for(int i=0;i<ends.Length;i++)ends[i]=rr[i*3+2];
                        var publication=new VirtualRaisedPlanning(input,map,cut,ends,CopiedPlanningBundle.Resolve(b,"buildingRecords"),ValuesFrom<ushort>(CopiedPlanningBundle.Resolve(b,"buildingIds"),2));
                        while(!publication.Rebuild.Complete)publication.Step(4096);
                        if(!publication.Rebuild.Proven)throw new InvalidOperationException("virtual-rebuild-unknown:"+publication.Rebuild.Reason);
                        topology=publication.Physical;input=publication.Planning;counts=publication.Rebuild.Counts;
                    }
                    Func<int,int,int?> keep,regions;
                    if(!CopiedPlanningRegions.TryCreate(b,"caller-pre",false,b.Attacker,0,variant==0?null:map,variant==0?null:topology.Components,out keep,out reason))throw new ArgumentException(reason);
                    if(!CopiedPlanningRegions.TryCreate(b,"distance-pre",true,b.Attacker,0,variant==0?null:map,variant==0?null:topology.Components,out regions,out reason))throw new ArgumentException(reason);
                    int origin=topology.Components[originTile],destination=topology.Components[targetTile];int? reachable=keep(destination,origin);
                    if(!reachable.HasValue)throw new ArgumentException("unresolved-caller-region");
                    var call=new VirtualPlanningCall(b.Attacker,b.Target,b.Mode,players[b.Target*7+5],origin,reachable.Value!=0);
                    if(!VirtualPlanningCaller.Run(input,state,call,targetTile,players[b.Target*7]!=0,regions))throw new InvalidOperationException(state.Reason);
                    if(variant==0){Compare(b,state,map,"distance-post");Compare(b,state,map,"caller-post");}
                    int[] seedNative=Ints(CopiedPlanningBundle.Resolve(b,"seed-pre/arguments4")),distanceNative=Ints(CopiedPlanningBundle.Resolve(b,"distance-pre/arguments4"));
                    if(variant==0&&(call.SeedShortLimit!=(seedNative[1]!=0)||call.CandidateDistance!=distanceNative[1]||call.OriginComponent!=distanceNative[2]))throw new InvalidOperationException("caller-arguments-mismatch");
                    int seedChanged=Differences(state.Seeds,CopiedPlanningBundle.Resolve(b,"seed-post/seeds"),1),distanceChanged=Differences(state.Distance,CopiedPlanningBundle.Resolve(b,"distance-post/distance"),2);
                    string selection="";
                    foreach(var pair in b.Sections)if(pair.Key.StartsWith("target/",StringComparison.Ordinal)&&pair.Key.EndsWith("/values8",StringComparison.Ordinal))
                    {
                        string prefix=pair.Key.Substring(0,pair.Key.Length-7);int[] values=Ints(pair.Value),args=Ints(CopiedPlanningBundle.Resolve(b,prefix+"argumentsAndTiles4"));
                        var selected=new VirtualTargetSelection(input,state,args[2],args[3],counts);
                        if(variant==0&&selected.Count!=values[5])throw new InvalidOperationException("effective-Fixes-histogram-mismatch");
                        selection+=";observedSelectionTile="+args[2]+",computedSeed="+selected.Seed+",computedRegion="+selected.Region+",effectiveCount="+selected.Count+",capturedNativeCount="+values[7]+",capturedEffectiveCount="+values[5]+",targetCastleRegion="+selected.TargetCastleRegion;
                    }
                    string continuation=VariantContinuation(b,state,topology,map);
                    lines.Add("variant="+variants[variant].Key+",cutCells="+cut.Length+",partitionEquivalentToRecorded="+EquivalentPartitions(map.Components,topology.Components)+",partitionDelta=["+PartitionDelta(map,topology.Components)+"],nativeDirection=target-to-attacker,keep="+(reachable.Value==0?"NoRoute":"Reachable")+",seedShortLimit="+call.SeedShortLimit+",origin="+origin+",destination="+destination+",candidateDistance="+call.CandidateDistance+",seedChanged="+seedChanged+",distanceChanged="+distanceChanged+",candidateTableCount="+state.Candidates.Count+",candidateProvenance="+(call.CandidateDistance==0?"inherited-no-new-D9190-candidates":"computed-plus-inherited")+",candidateSequence="+string.Join("/",state.Candidates)+selection+",firstChange="+(call.SeedShortLimit!=(seedNative[1]!=0)?"CF360-to-D95E0-R8":seedChanged!=0?"seed-fields":distanceChanged!=0?"distance-fields":"none")+",basis="+(b.Dirty!=0?"conditional-captured-physics-pending-native-topology":"copied-physics")+",newTargetChoice=Unknown-unrecorded-consumer,firstConsumerContinuation=["+continuation+"],sequentialMilitaryContinuation=Unknown,policy=Unknown");
                }
                report=string.Join(Environment.NewLine,lines);return true;
            }
            catch(Exception error){report="virtual-planning-not-proven:"+error.GetType().Name+":"+error.Message;return false;}
        }
        private static T[] ValuesFrom<T>(byte[] bytes,int width) where T:struct
        {var values=new T[bytes.Length/width];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);return values;}
        private static bool Related(BridgePlanningImporter.Artifact a,int player,int other)
        {if(other==0)return false;if(player<1||player>8||other<1||other>8||!a.Sections.TryGetValue("allies",out byte[] allies)||allies.Length!=81)throw new ArgumentException("incomplete-player-relations");return player==other||allies[player*9+other]!=0;}
        private static void ValidateConnectionIdentities(BridgePlanningImporter.Artifact artifact)
        {
            uint[] buildings=Values<uint>(artifact,"globals",4),units=Values<uint>(artifact,"unitGlobals",4);byte[] records=CopiedPlanningBundle.Resolve(artifact.Planning,"caller-pre/macroRecords");
            for(int at=0;at<records.Length;at+=0x204)
            {
                if(BitConverter.ToInt32(records,at)!=1||BitConverter.ToInt32(records,at+24)==0)continue;
                int building=BitConverter.ToInt32(records,at+12),unit=BitConverter.ToInt32(records,at+16);uint subject=BitConverter.ToUInt32(records,at+20);
                if(building!=0&&(building<1||building>buildings.Length||subject==0||buildings[building-1]!=subject)||unit!=0&&(unit<1||unit>units.Length||subject==0||units[unit-1]!=subject))throw new ArgumentException("connection-subject-identity-mismatch");
                if(building==0&&unit==0)throw new ArgumentException("unproven-connection-subject");
            }
        }
        private static string PartitionDelta(VirtualBridgeMap original,ushort[] next)
        {
            int added=0,removed=0,split=0,merged=0,first=-1;var forward=new Dictionary<int,int>();var reverse=new Dictionary<int,int>();
            for(int i=0;i<next.Length;i++)
            {
                int a=original.Components[i],b=next[i];bool changed=false;
                if(a==0&&b!=0){added++;changed=true;}else if(a!=0&&b==0){removed++;changed=true;}
                else if(a!=0){if(forward.TryGetValue(a,out int f)&&f!=b){split++;changed=true;}if(reverse.TryGetValue(b,out int r)&&r!=a){merged++;changed=true;}forward[a]=b;reverse[b]=a;}
                if(changed&&first<0)first=i;
            }
            return "added="+added+",removed="+removed+",splitObservations="+split+",mergeObservations="+merged+",firstTile="+first+(first<0?"":",firstXY="+original.X[first]+"/"+original.Y[first]+",firstFlags="+unchecked((uint)original.Flags[first]).ToString("X8")+",firstComponents="+original.Components[first]+"/"+next[first]);
        }
        private static bool EquivalentPartitions(ushort[] a,ushort[] b)
        {
            if(a.Length!=b.Length)return false;var forward=new Dictionary<int,int>();var reverse=new Dictionary<int,int>();
            for(int i=0;i<a.Length;i++)
            {
                if((a[i]==0)!=(b[i]==0))return false;if(a[i]==0)continue;
                if(forward.TryGetValue(a[i],out int f)&&f!=b[i]||reverse.TryGetValue(b[i],out int r)&&r!=a[i])return false;
                forward[a[i]]=b[i];reverse[b[i]]=a[i];
            }
            return true;
        }
        internal static bool TryConsumerStages(BridgePlanningImporter.Artifact artifact,out string reason)
        {
            reason="Unknown:missing-consumer-sequence";if(artifact?.Planning==null)return false;
            try
            {
                CopiedConsumerSequence.Validate(artifact.Planning);int count=CopiedConsumerSequence.Count(artifact.Planning);if(count==0)return false;
                var reports=new List<string>();
                for(int n=0;n<count;n++)
                {var own=new BridgePlanningImporter.Artifact {Schema=artifact.Schema,Hash=artifact.Hash,Planning=CopiedConsumerSequence.View(artifact.Planning,n)};foreach(var section in artifact.Sections)own.Sections.Add(section.Key,section.Value);foreach(var metadata in artifact.Metadata)own.Metadata.Add(metadata.Key,metadata.Value);if(!TrySingleConsumerStages(own,out string report)){reason="consumerOrdinal="+(n+1)+";"+report;return false;}reports.Add("consumerOrdinal="+(n+1)+";"+report);}
                reason=string.Join(" | ",reports)+";interveningReplay="+BetweenConsumers(artifact)+";fullMilitaryEntryAndFormation=Unknown";return true;
            }
            catch(Exception error){reason="Unknown:consumer-sequence:"+error.Message;return false;}
        }
        private static bool TrySingleConsumerStages(BridgePlanningImporter.Artifact artifact,out string reason)
        {
            reason="Unknown:missing-historical-consumer-inputs";var b=artifact?.Planning;if(b==null||!b.Sections.ContainsKey("consumer/pre/identity"))return false;
            try
            {
                // This is the captured handoff from weight completion to the five
                // availability consumers. It is not a replay of candidate building.
                var computed=VirtualCandidateConsumers.Availability(CopiedPlanningBundle.Resolve(b,"consumer/weight-post/candidates"),b.Attacker,1);
                Equal(computed,CopiedPlanningBundle.Resolve(b,"consumer/post/candidates"),"availability-handoff-complete-manager");
                int count;var unitTargets=VirtualCandidateConsumers.UnitTargets(CopiedPlanningBundle.Resolve(b,"consumer/pre/unitManager"),b.Attacker,out count);
                var after=CopiedPlanningBundle.Resolve(b,"consumer/build-pre/candidates");int at=0x2E93E8C-VirtualCandidateConsumers.Root;
                var recorded=new byte[4000*4];Buffer.BlockCopy(after,at,recorded,0,recorded.Length);Equal(unitTargets,recorded,"unit-target-handoff");if(BitConverter.ToInt32(after,0x2E97D0C-VirtualCandidateConsumers.Root)!=count)throw new InvalidOperationException("unit-target-count");
                var beforeWeights=CopiedPlanningBundle.Resolve(b,"consumer/weight-pre/candidates");
                byte[] Weights(int maximum)=>VirtualCandidateWeights.Run(beforeWeights,b.Attacker,b.Target,CopiedPlanningBundle.Resolve(b,"consumer/pre/players"),CopiedPlanningBundle.Resolve(b,"distance-post/distance"),b.Bank,CopiedPlanningBundle.Resolve(b,"consumer/pre/flags"),CopiedPlanningBundle.Resolve(b,"consumer/pre/unitTiles"),CopiedPlanningBundle.Resolve(b,"consumer/pre/unitManager"),CopiedPlanningBundle.Resolve(b,"tileRows"),CopiedPlanningBundle.Resolve(b,"directionOffsets"),maximum);
                string weight="Unknown:missing-weight-mode-values-at-3665F10-and-3665F28";
                if(b.Sections.ContainsKey("consumer/weight-pre/gameModeValues"))
                {
                    byte[] values=CopiedPlanningBundle.Resolve(b,"consumer/weight-pre/gameModeValues");if(values.Length!=8)throw new ArgumentException("weight-mode-values-extent");int maximum=BitConverter.ToInt32(values,4)==0&&BitConverter.ToInt32(values,0)==28?150:70;
                    Equal(Weights(maximum),CopiedPlanningBundle.Resolve(b,"consumer/weight-post/candidates"),"full-weight-handoff");weight="Matched-actual-input-limit-"+maximum;
                }
                reason="captured-local-handoffs-match;fullWeightReplay="+weight+";fullCandidateBuildAndWeightReplay="+FullCandidateReplay(artifact)+";fullMilitaryConsumer=Unknown;behaviorFix=disabled";return true;
            }
            catch(Exception error){reason="Unknown:consumer-stage-mismatch:"+error.Message;return false;}
        }
        private static string FullCandidateReplay(BridgePlanningImporter.Artifact artifact) => FullCandidateReplay(artifact,out _,out _,out _);
        private static string FullCandidateReplay(BridgePlanningImporter.Artifact artifact,out VirtualCandidateBuilder computed,out VirtualPlanningState computedState,out int computedCastle)
        {
            computed=null;computedState=null;computedCastle=-1;
            try
            {
                var b=artifact.Planning;byte[] Get(string key)=>CopiedPlanningBundle.Resolve(b,key);
                var map=new VirtualBridgeMap(b.Session,b.Revision,Values<ushort>(artifact,"components",2),Values<byte>(artifact,"edges",1),Values<int>(artifact,"flags",4),Values<ushort>(artifact,"x",2),Values<ushort>(artifact,"y",2),Values<int>(artifact,"rows",4),Array.Empty<VirtualConnection>(),true,Values<ushort>(artifact,"specialIds",2),Values<short>(artifact,"specialKinds",2));
                Equal(map.Components,Get("consumer/pre/components"),"consumer-own-geometry-components");Equal(map.Flags,Get("consumer/pre/flags"),"consumer-own-geometry-flags");Equal(map.Edges,Get("consumer/pre/edges"),"consumer-own-geometry-edges");
                VirtualPlanningState state;string reason;
                if(!TryBaseline(b,map,out reason,out state))return "Unknown:"+reason;
                VirtualCandidateBuilder builder;
                if(!TryEffectiveCounts(b,map.Components,out int[] effective,out reason))return reason;
                if(!CopiedCandidateInputs.TryCreate(b,state,map,Get("consumer/pre/macroRecords"),effective,out builder,out reason,projectTarget:!reason.StartsWith("own-cached-selection-input",StringComparison.Ordinal)))return reason;
                Equal(builder.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/build-pre/candidates"),"full-projection-unit-handoff");
                builder.Run(b.Attacker,b.Target);
                Equal(builder.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/build-post/candidates"),"full-candidate-builder");Equal(builder.Bytes(0x535ef90,320800),Get("consumer/build-post/seeds"),"full-candidate-seeds");Equal(builder.Bytes(0x53ad4b0,320800),Get("consumer/build-post/selectionMask"),"full-candidate-mask");
                var modes=Get("consumer/weight-pre/gameModeValues");int maximum=BitConverter.ToInt32(modes,4)==0&&BitConverter.ToInt32(modes,0)==28?150:70;
                var weights=VirtualCandidateWeights.Run(builder.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),b.Attacker,b.Target,Get("consumer/pre/players"),Raw(state.Distance),b.Bank,Get("consumer/pre/flags"),Get("consumer/pre/unitTiles"),Get("consumer/pre/unitManager"),Get("tileRows"),Get("directionOffsets"),maximum);
                Equal(weights,Get("consumer/weight-post/candidates"),"full-computed-candidate-weights");
                var available=VirtualCandidateConsumers.Availability(weights,b.Attacker,1);builder.Replace(VirtualCandidateConsumers.Root,available);builder.PostConsumer(b.Target);
                Equal(builder.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/post/candidates"),"full-computed-availability");
                foreach(var range in new[]{Tuple.Create("workControls",0x60ad660,0xe0),Tuple.Create("workQueueControls",0x60ad660+0x155f38,0x34),Tuple.Create("workQueue",0x60ad660+0x155f6c,320800*4),Tuple.Create("workQueueRows",0x60ad660+0x28f3ec,320800*2),Tuple.Create("workQueueX",0x60ad660+0x32be2c,320800*2),Tuple.Create("workDistances",0x5225b10,320800*2),Tuple.Create("workVisits",0x52c2550,320800*2),Tuple.Create("workTargets",0x37f1edc,9*50*16)})Equal(builder.Bytes(range.Item2,range.Item3),Get("consumer/post/"+range.Item1),"full-computed-"+range.Item1);
                computedCastle=builder.Castle(b.Attacker,b.Target);computed=builder;computedState=state;
                return "Matched-computed-caller-candidates-weight-availability-work;computedCastle="+computedCastle+";militaryContinuation=Unknown";
            }
            catch(Exception error){return "Unknown:"+error.GetType().Name+":"+error.Message;}
        }
        private static string BetweenConsumers(BridgePlanningImporter.Artifact artifact) => BetweenConsumers(artifact,out _);
        internal static string BetweenConsumers(BridgePlanningImporter.Artifact artifact,out VirtualPlanningState calculated)
        {
            calculated=null;
            try
            {
                var b=artifact.Planning;if(CopiedConsumerSequence.Count(b)<2)return "not-observed";
                if(b.Sections.TryGetValue("military/entryPlayer",out var ownEntry)&&BitConverter.ToInt32(ownEntry,0x379d974-0x379ae00)!=4)return "Unknown:inter-consumer-entry-is-not-phase4";
                string first=FullCandidateReplay(artifact,out var prior,out var earlier,out int castle);
                if(prior==null)return "Unknown:first-consumer-not-reproduced:"+first;
                if(castle!=0)return "Unknown:positive-castle-would-return-before-second-consumer";
                byte[] players=prior.Bytes(0x379ae00,9*0x583c),tribes=CopiedPlanningBundle.Resolve(b,"consumer/pre/tribes");int offset=b.Attacker*0x583c;
                foreach(var role in new[]{Tuple.Create(0x4888,0x4af8,"3C150->DA590/F2CB0/11B520"),Tuple.Create(0x4886,0x4af4,"3B8D0->1140C0/113AB0"),Tuple.Create(0x488a,0x4afc,"2B2C0->122B40")})
                {
                    int id=BitConverter.ToInt16(players,offset+role.Item1);if(id==0)continue;if(id<1||id>=4500)throw new ArgumentException("intervening-role-tribe-id");int at=id*0x688;
                    if(at+0x5c>tribes.Length)throw new ArgumentException("intervening-role-tribe-extent");
                    if(BitConverter.ToUInt32(tribes,at+0x34)==BitConverter.ToUInt32(players,offset+role.Item2)&&BitConverter.ToInt16(tribes,at+0x5a)!=0)
                    {
                        string selection="not-this-role";
                        if(role.Item1==0x4886)
                        {
                            int leader=BitConverter.ToInt16(tribes,at+0x5a);var units=CopiedPlanningBundle.Resolve(b,"consumer/pre/unitManager");int unit=leader*0x490;if(leader<1||unit+0x720>units.Length)throw new ArgumentException("active-role-leader-extent");int x=BitConverter.ToInt16(units,unit+0x71c),y=BitConverter.ToInt16(units,unit+0x71e);
                            int target=prior.FindEnemyGatehouse(b.Attacker,x,y,80);if(target==0)target=prior.FindEnemyGatehouse(b.Attacker,x,y,200);
                            var buildings=CopiedPlanningBundle.Resolve(b,"consumer/pre/buildingRecords");int record=(target-1)*0x32c;
                            selection=target==0?"no-gatehouse":"building="+target+"/g"+BitConverter.ToUInt32(buildings,record+0x134)+";orderXY="+BitConverter.ToInt16(buildings,record+0x2bc)+"/"+BitConverter.ToInt16(buildings,record+0x2be);
                        }
                        return "Unknown:active-role-requires-"+role.Item3+";tribe="+id+";copiedGateSelection=["+selection+"];stored30E90TargetIsIndependent=True;later-consumer-input-not-substituted";
                    }
                }
                Buffer.BlockCopy(BitConverter.GetBytes(5),0,players,offset+0x2b74,4);prior.Replace(0x379ae00,players);
                var next=CopiedConsumerSequence.View(b,1);byte[] Get(string key)=>CopiedPlanningBundle.Resolve(next,key);
                Equal(players,Get("consumer/pre/players"),"intervening-calculated-players");
                foreach(string stable in new[]{"unitManager","tribes","flags","components","edges","height","baseHeight","unitTiles","buildingIds","terrainOwner"})Equal(CopiedPlanningBundle.Resolve(b,"consumer/pre/"+stable),Get("consumer/pre/"+stable),"intervening-unchanged-"+stable);
                Equal(prior.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/pre/candidates"),"intervening-computed-candidates");
                var map=new VirtualBridgeMap(b.Session,b.Revision,Values<ushort>(artifact,"components",2),Values<byte>(artifact,"edges",1),Values<int>(artifact,"flags",4),Values<ushort>(artifact,"x",2),Values<ushort>(artifact,"y",2),Values<int>(artifact,"rows",4),Array.Empty<VirtualConnection>(),true,Values<ushort>(artifact,"specialIds",2),Values<short>(artifact,"specialKinds",2));
                VirtualPlanningInput input;VirtualPlanningState state;string reason;if(!CopiedPlanningBundle.TryRead(b,map,"caller-pre",out input,out state,out reason))return "Unknown:"+reason;
                // Every mutable planning input comes from our prior computation.
                Buffer.BlockCopy(prior.Bytes(0x535ef90,320800),0,state.Seeds,0,320800);Buffer.BlockCopy(Raw(earlier.Distance),0,state.Distance,0,320800*2);Buffer.BlockCopy(prior.Bytes(0x52c2550,320800*2),0,state.Visit,0,320800*2);Buffer.BlockCopy(prior.Bytes(0x60ad660+0x155f6c,320800*4),0,state.Queue,0,320800*4);Buffer.BlockCopy(prior.Bytes(0x60ad660+0x28f3ec,320800*2),0,state.QueueY,0,320800*2);
                state.Candidates.Clear();state.Candidates.AddRange(earlier.Candidates);int[] control=Ints(prior.Bytes(0x60ad660,0xe0)),queue=Ints(prior.Bytes(0x60ad660+0x155f38,0x34));state.Generation=control[1];state.MaxDistance=control[0x8c/4];state.Level=queue[0];state.Head=queue[1];state.BatchStart=queue[2];state.Tail=queue[3];state.BatchEnd=queue[4];
                int[] scalars=Ints(CopiedPlanningBundle.Resolve(b,"playerScalars7"));int tile=input.Rows[scalars[b.Target*7+2]]+scalars[b.Target*7+1],origin=map.Components[scalars[b.Attacker*7+3]],destination=map.Components[tile];
                Func<int,int,int?> keep,regions;if(!CopiedPlanningRegions.TryCreate(b,"caller-pre",false,b.Attacker,0,out keep,out reason)||!CopiedPlanningRegions.TryCreate(b,"distance-pre",true,b.Attacker,0,out regions,out reason))return "Unknown:"+reason;
                int? reachable=keep(destination,origin);if(!reachable.HasValue)return "Unknown:intervening-region";
                if(origin!=0&&destination!=0&&origin!=destination&&scalars[b.Attacker*7]!=0&&scalars[b.Target*7]!=0){control[0xc4/4]=unchecked(control[0xc4/4]+1);control[0xc0/4]=reachable.Value!=0?0:1;}
                var call=new VirtualPlanningCall(b.Attacker,b.Target,0,scalars[b.Target*7+5],origin,reachable.Value!=0);if(!VirtualPlanningCaller.Run(input,state,call,tile,scalars[b.Target*7]!=0,regions))return "Unknown:"+state.Reason;
                control[1]=state.Generation;control[0x8c/4]=state.MaxDistance;queue[0]=state.Level;queue[1]=state.Head;queue[2]=state.BatchStart;queue[3]=state.Tail;queue[4]=state.BatchEnd;
                Equal(state.Seeds,Get("consumer/pre/seeds"),"intervening-next-computed-seeds");Equal(state.Visit,Get("consumer/pre/workVisits"),"intervening-next-computed-visits");Equal(state.Queue,Get("consumer/pre/workQueue"),"intervening-next-computed-queue");Equal(state.QueueY,Get("consumer/pre/workQueueRows"),"intervening-next-computed-queue-rows");Equal(control,Get("consumer/pre/workControls"),"intervening-next-computed-controls");Equal(queue,Get("consumer/pre/workQueueControls"),"intervening-next-computed-queue-controls");
                calculated=state;return "Matched-negative-branch-no-active-roles-and-computed-next-planning-inputs;phase=4-to-5;entryAndFormation=Unknown;secondDistanceBankNotDirectlyCaptured=True";
            }
            catch(Exception error){return "Unknown:"+error.GetType().Name+":"+error.Message;}
        }
        private static string VariantContinuation(BridgePlanningCapture.Bundle b,VirtualPlanningState state,VirtualBridgeMap map,VirtualBridgeMap original)
        {
            try {return FirstConsumerVariant(b,state,map,RemapCandidateConnections(CopiedPlanningBundle.Resolve(b,"consumer/pre/macroRecords"),original,map.Components));}
            catch(Exception error){return "Unknown:"+error.GetType().Name+":"+error.Message;}
        }
        private static byte[] RemapCandidateConnections(byte[] recorded,VirtualBridgeMap original,ushort[] components)
        {
            if(recorded.Length%0x204!=0)throw new ArgumentException("candidate-macro-extent");var copy=(byte[])recorded.Clone();
            for(int at=0;at<copy.Length;at+=0x204)
            {
                if(BitConverter.ToInt32(copy,at)!=1||BitConverter.ToInt32(copy,at+24)==0)continue;
                int entry=BitConverter.ToInt32(copy,at+36),exit=BitConverter.ToInt32(copy,at+48),c=BitConverter.ToInt32(copy,at+0x1e8),third=0;
                if((uint)entry>=components.Length||(uint)exit>=components.Length||original.Components[entry]!=BitConverter.ToInt32(copy,at+52)||original.Components[exit]!=BitConverter.ToInt32(copy,at+56))throw new ArgumentException("unproved-candidate-macro-endpoints");
                if(c!=0&&!VirtualGateEndpoint.TryResolve(original,BitConverter.ToInt32(copy,at+4),entry,exit,c,out third))throw new ArgumentException("unproved-candidate-third-endpoint");
                Buffer.BlockCopy(BitConverter.GetBytes((int)components[entry]),0,copy,at+52,4);Buffer.BlockCopy(BitConverter.GetBytes((int)components[exit]),0,copy,at+56,4);if(c!=0)Buffer.BlockCopy(BitConverter.GetBytes((int)components[third]),0,copy,at+0x1e8,4);
            }
            return copy;
        }
        private static string FirstConsumerVariant(BridgePlanningCapture.Bundle b,VirtualPlanningState state,VirtualBridgeMap map,byte[] macro)
        {
            byte[] Get(string key)=>CopiedPlanningBundle.Resolve(b,key);VirtualCandidateBuilder builder;string reason;
            if(!CopiedCandidateInputs.TryCreate(b,state,map,macro,Histogram(map.Components),out builder,out reason))return reason;
            builder.Run(b.Attacker,b.Target);int candidates=Differences(builder.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/build-post/candidates"),4);
            var modes=Get("consumer/weight-pre/gameModeValues");if(modes.Length!=8)throw new ArgumentException("weight-mode-extent");int maximum=BitConverter.ToInt32(modes,4)==0&&BitConverter.ToInt32(modes,0)==28?150:70;
            var weighted=VirtualCandidateWeights.Run(builder.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),b.Attacker,b.Target,Get("consumer/pre/players"),Raw(state.Distance),b.Bank,Raw(map.Flags),Get("consumer/pre/unitTiles"),Get("consumer/pre/unitManager"),Get("tileRows"),Get("directionOffsets"),maximum);
            int weights=Differences(weighted,Get("consumer/weight-post/candidates"),4);var available=VirtualCandidateConsumers.Availability(weighted,b.Attacker,1);builder.Replace(VirtualCandidateConsumers.Root,available);builder.PostConsumer(b.Target);
            int availability=Differences(available,Get("consumer/post/candidates"),4),castle=builder.Castle(b.Attacker,b.Target);int gateTarget=0;
            var players=Get("consumer/pre/players");var tribes=Get("consumer/pre/tribes");int offset=b.Attacker*0x583c,role=BitConverter.ToInt16(players,offset+0x4886);
            if(castle==0&&role>0&&role<4500&&BitConverter.ToUInt32(tribes,role*0x688+0x34)==BitConverter.ToUInt32(players,offset+0x4af4))
            {int leader=BitConverter.ToInt16(tribes,role*0x688+0x5a);var units=Get("consumer/pre/unitManager");if(leader>0&&leader*0x490+0x720<=units.Length){int x=BitConverter.ToInt16(units,leader*0x490+0x71c),y=BitConverter.ToInt16(units,leader*0x490+0x71e);gateTarget=builder.FindEnemyGatehouse(b.Attacker,x,y,80);if(gateTarget==0)gateTarget=builder.FindEnemyGatehouse(b.Attacker,x,y,200);}}
            return "computed-own-first-consumer;gateSelectorTarget="+gateTarget+";candidatesChangedInt32="+candidates+";weightsChangedInt32="+weights+";availabilityChangedInt32="+availability+";castle="+castle+";firstChangedStage="+(candidates!=0?"candidate-building":weights!=0?"weights":availability!=0?"availability":"none")+";nextMilitaryBranch="+(b.Sections.TryGetValue("military/entryPlayer",out var entry)&&BitConverter.ToInt32(entry,0x379d974-0x379ae00)==4?(castle==0?"4-to-5-follow-up-required":"4-to-6-return"):"Unknown:entry-specific-tail-not-replayed")+";recordedPostUsedOnlyForComparison=True";
        }
        private static byte[] Raw(Array values){var bytes=new byte[Buffer.ByteLength(values)];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);return bytes;}
        internal static bool TryConsumerInputClosure(BridgePlanningImporter.Artifact artifact,out string reason)
        {
            var b=artifact?.Planning;if(b==null){reason="Unknown:missing-planning";return false;}
            string[] required={"consumer/weight-pre/gameModeValues","buildingUpdaterControls","buildingUpdaterSlots","consumer/pre/packedValidity","consumer/pre/combatClassMask","consumer/pre/buildingTransitionClasses","consumer/pre/buildingSeedClasses","consumer/pre/workControls","consumer/pre/workQueueControls","consumer/pre/workQueue","consumer/pre/workQueueRows","consumer/pre/workQueueX","consumer/pre/workDistances","consumer/pre/workVisits","consumer/pre/workTargets","consumer/post/workControls","consumer/post/workQueueControls","consumer/post/workQueue","consumer/post/workQueueRows","consumer/post/workQueueX","consumer/post/workDistances","consumer/post/workVisits","consumer/post/workTargets"};
            var missing=new List<string>();foreach(string key in required)if(!b.Sections.ContainsKey(key)&&!b.References.ContainsKey(key))missing.Add(key);
            reason=missing.Count==0?"bounded-extra-inputs-present;fullCandidateBuilderAndMilitaryReplay=Unknown;negativeEligible=False":"Unknown:missing-native-inputs=["+string.Join(";",missing)+"];no-defaults;negativeEligible=False";return missing.Count==0;
        }
        internal static bool TryEffectiveCounts(BridgePlanningCapture.Bundle bundle,ushort[] components,out int[] values,out string reason)
        {
            values=null;reason="Unknown:missing-effective-component-count-input";
            try
            {
                var targetKeys=new System.Collections.Generic.List<string>();
                long ownConsumer=0;
                try {ownConsumer=BitConverter.ToInt64(CopiedPlanningBundle.Resolve(bundle,"consumer/pre/identity"),16);}catch(ArgumentException){}
                foreach(string key in bundle.Sections.Keys)if(key.StartsWith("target/",StringComparison.Ordinal)&&key.EndsWith("/values8",StringComparison.Ordinal))
                {
                    string owner=key.Substring(0,key.Length-7)+"identity5";
                    if(ownConsumer!=0&&(!bundle.Sections.TryGetValue(owner,out var frame)||frame.Length!=40||BitConverter.ToInt64(frame,16)!=ownConsumer))continue;
                    targetKeys.Add(key);
                }
                if(targetKeys.Count==0&&ownConsumer!=0&&bundle.Sections.TryGetValue("consumer/projectionCoverage4",out var coverage)&&coverage.Length==4&&BitConverter.ToInt32(coverage,0)==2)
                {
                    byte[] pre=CopiedPlanningBundle.Resolve(bundle,"consumer/pre/candidates");if(pre.Length!=VirtualCandidateConsumers.Bytes)throw new ArgumentException("cached-selection-extent");
                    int cachedRegion=BitConverter.ToInt32(pre,0x2e97d10-VirtualCandidateConsumers.Root),cachedCount=BitConverter.ToInt32(pre,0x2e9ca14-VirtualCandidateConsumers.Root);
                    if(cachedRegion<0||cachedRegion>=1000||cachedCount<0||cachedCount>320800)throw new ArgumentException("cached-selection-range");
                    values=new int[1000];values[cachedRegion]=cachedCount;reason="own-cached-selection-input;no-observed-target-projection;unused-counts-not-known";return true;
                }
                if(targetKeys.Count!=1){reason="Unknown:ambiguous-effective-count-selection";return false;}
                string prefix=targetKeys[0].Substring(0,targetKeys[0].Length-7);var outputs=bundle.Sections[targetKeys[0]];
                if(outputs.Length!=32||BitConverter.ToInt32(outputs,0)!=bundle.Attacker||BitConverter.ToInt32(outputs,4)!=bundle.Target)throw new ArgumentException("effective-count-selection-players");
                int region=BitConverter.ToInt32(outputs,16),observed=BitConverter.ToInt32(outputs,20);
                if(region<0||region>=1000||observed<0||observed>320800)throw new ArgumentException("effective-count-selection-range");
                if(bundle.Sections.TryGetValue(prefix+"effectiveCountsPre",out var raw))
                {
                    if(!bundle.Sections.TryGetValue(prefix+"identity5",out var identity)||identity.Length!=40||BitConverter.ToInt64(identity,0)!=bundle.Session||BitConverter.ToInt64(identity,24)!=bundle.Root)throw new ArgumentException("effective-count-input-frame");
                    if(raw.Length!=4000&&raw.Length!=16000)throw new ArgumentException("effective-count-input-capacity");
                    if(!bundle.Sections.TryGetValue(prefix+"effectiveCountsPost",out var after)||raw.Length!=after.Length)throw new ArgumentException("effective-count-post-capacity");
                    for(int i=0;i<raw.Length;i++)if(raw[i]!=after[i])throw new ArgumentException("effective-count-input-changed");
                    var all=new int[raw.Length/4];Buffer.BlockCopy(raw,0,all,0,raw.Length);BridgePlanningCapture.ValidateEffectiveCounts(all);
                    if(all[region]!=observed)throw new ArgumentException("effective-count-output-mismatch");
                    values=new int[1000];Array.Copy(all,values,1000);reason="own-effective-count-pre-input;post-used-only-for-comparison";return true;
                }
                var histogram=Histogram(components);
                if(histogram[region]!=observed){reason="Unknown:missing-effective-component-count-input:region="+region+",observed="+observed+",histogram="+histogram[region]+",dirty="+bundle.Dirty;return false;}
                values=histogram;reason="legacy-histogram-selected-value-agrees;full-effective-table-not-captured";return true;
            }
            catch(Exception error){reason="Unknown:effective-count-input:"+error.Message;return false;}
        }
        private static int[] Histogram(ushort[] components)
        {var counts=new int[1000];foreach(int component in components){if(component>=counts.Length)throw new ArgumentException("unsupported-component-capacity");if(component!=0)counts[component]++;}return counts;}
        private static int Differences(Array values,byte[] expected,int width)
        {var bytes=new byte[Buffer.ByteLength(values)];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);if(bytes.Length!=expected.Length)throw new ArgumentException("comparison-capacity");int changed=0;for(int i=0;i<bytes.Length;i+=width){bool different=false;for(int n=0;n<width;n++)different|=bytes[i+n]!=expected[i+n];if(different)changed++;}return changed;}
    }
}

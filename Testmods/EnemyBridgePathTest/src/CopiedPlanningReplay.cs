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
                ushort[] components=Values<ushort>(artifact,"components",2),x=Values<ushort>(artifact,"x",2),y=Values<ushort>(artifact,"y",2);int[] rows=Values<int>(artifact,"rows",4),flags=Values<int>(artifact,"flags",4);byte[] edges=Values<byte>(artifact,"edges",1);
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
        internal static bool TryBaseline(BridgePlanningCapture.Bundle bundle,VirtualBridgeMap map,out string reason)
        {
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
                reason=selections==0?"baseline-fields-match;target-selection-not-observed":"continuous-caller-seed-distance-fields-and-native-selection-match;effective-fixes-count-separate";return true;
            }
            catch(Exception error){reason="baseline-not-proven:"+error.GetType().Name+":"+error.Message;return false;}
        }

        // One state from caller entry through computed seed/distance and observed
        // 2C5A0 projection. This does not invent an unrecorded 2C480 target choice.
        internal static bool TryVariants(BridgePlanningImporter.Artifact artifact,out string report)
        {
            report="missing-historical-planning-values";string baseline;
            if(!TryBaseline(artifact,out baseline)){report=baseline;return false;}
            try
            {
                var b=artifact.Planning;
                ushort[] components=Values<ushort>(artifact,"components",2),x=Values<ushort>(artifact,"x",2),y=Values<ushort>(artifact,"y",2);
                var map=new VirtualBridgeMap(b.Session,b.Revision,components,Values<byte>(artifact,"edges",1),Values<int>(artifact,"flags",4),x,y,Values<int>(artifact,"rows",4),Array.Empty<VirtualConnection>(),true,Values<ushort>(artifact,"specialIds",2),Values<short>(artifact,"specialKinds",2));
                int[] players=Ints(CopiedPlanningBundle.Resolve(b,"playerScalars7")),rr=Ints(CopiedPlanningBundle.Resolve(b,"rowRecords"));
                int targetTile=map.Tile(players[b.Target*7+1],players[b.Target*7+2]),originTile=players[b.Attacker*7+3];
                if((uint)originTile>=320800||targetTile<0||players[b.Attacker*7+4]!=b.Target)throw new ArgumentException("caller-player-target-or-castle");
                var lines=new List<string>();lines.Add("baseline="+baseline+",dirty="+b.Dirty+",nativeGeneration="+b.Revision+",planningClock="+b.Clock+",captureClock="+artifact.Metadata["captureClock"]+",observationClock="+artifact.Metadata["observationClock"]+",inputTiming="+artifact.Metadata["inputTiming"]+",negativeEligible=False,behaviorFix=disabled");
                ValidateConnectionIdentities(artifact);
                int[] parents=Values<int>(artifact,"coupledParents5",4),decks=Values<int>(artifact,"decks",4);var cutOne=new List<int>();var cutAll=new List<int>();
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
                    if(!allowed)for(int n=0;n<count;n++){cutAll.Add(decks[p+n]);if(id==703)cutOne.Add(decks[p+n]);}p+=count;
                }
                for(int variant=0;variant<4;variant++)
                {
                    VirtualPlanningInput input;VirtualPlanningState state;string reason;
                    if(!CopiedPlanningBundle.TryRead(b,map,"caller-pre",out input,out state,out reason))throw new ArgumentException(reason);
                    VirtualBridgeMap topology=map;int[] counts=Histogram(map.Components);int[] cut=variant<=1?Array.Empty<int>():variant==2?cutOne.ToArray():cutAll.ToArray();
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
                    lines.Add("variant="+(variant==0?"none":variant==1?"rebuild-only-control":variant==2?"only703":"all-unallowed-coupled")+",cutCells="+cut.Length+",partitionEquivalentToRecorded="+EquivalentPartitions(map.Components,topology.Components)+",partitionDelta=["+PartitionDelta(map,topology.Components)+"],nativeDirection=target-to-attacker,keep="+(reachable.Value==0?"NoRoute":"Reachable")+",seedShortLimit="+call.SeedShortLimit+",origin="+origin+",destination="+destination+",candidateDistance="+call.CandidateDistance+",seedChanged="+seedChanged+",distanceChanged="+distanceChanged+",candidateTableCount="+state.Candidates.Count+",candidateProvenance="+(call.CandidateDistance==0?"inherited-no-new-D9190-candidates":"computed-plus-inherited")+",candidateSequence="+string.Join("/",state.Candidates)+selection+",firstChange="+(call.SeedShortLimit!=(seedNative[1]!=0)?"CF360-to-D95E0-R8":seedChanged!=0?"seed-fields":distanceChanged!=0?"distance-fields":"none")+",basis="+(b.Dirty!=0?"conditional-captured-physics-pending-native-topology":"copied-physics")+",newTargetChoice=Unknown-unrecorded-consumer,policy=Unknown");
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
                reason="captured-local-handoffs-match;fullCandidateBuildAndWeightReplay=Unknown;fullMilitaryConsumer=Unknown;behaviorFix=disabled";return true;
            }
            catch(Exception error){reason="Unknown:consumer-stage-mismatch:"+error.Message;return false;}
        }
        private static int[] Histogram(ushort[] components)
        {var counts=new int[1000];foreach(int component in components){if(component>=counts.Length)throw new ArgumentException("unsupported-component-capacity");if(component!=0)counts[component]++;}return counts;}
        private static int Differences(Array values,byte[] expected,int width)
        {var bytes=new byte[Buffer.ByteLength(values)];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);if(bytes.Length!=expected.Length)throw new ArgumentException("comparison-capacity");int changed=0;for(int i=0;i<bytes.Length;i+=width){bool different=false;for(int n=0;n<width;n++)different|=bytes[i+n]!=expected[i+n];if(different)changed++;}return changed;}
    }
}

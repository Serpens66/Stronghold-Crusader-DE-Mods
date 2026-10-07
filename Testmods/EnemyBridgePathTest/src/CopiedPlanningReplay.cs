using System;
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
                var map=new VirtualBridgeMap(artifact.Planning.Session,artifact.Planning.Revision,components,edges,flags,x,y,rows,Array.Empty<VirtualConnection>(),true);
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
            string pre=stage.Replace("-post","-pre");int[] control=Ints(CopiedPlanningBundle.Resolve(b,pre+"/controls")),queue=Ints(CopiedPlanningBundle.Resolve(b,pre+"/queueControls"));control[1]=actual.Generation;control[0x8c/4]=actual.MaxDistance;queue[0]=actual.Level;queue[1]=actual.Head;queue[2]=actual.BatchStart;queue[3]=actual.Tail;queue[4]=actual.BatchEnd;Equal(control,CopiedPlanningBundle.Resolve(b,stage+"/controls"),stage+"/all-controls");Equal(queue,CopiedPlanningBundle.Resolve(b,stage+"/queueControls"),stage+"/all-queue-controls");
            if(actual.Candidates.Count!=expected.Candidates.Count)throw new InvalidOperationException("baseline-candidate-count:"+stage);for(int i=0;i<actual.Candidates.Count;i++)if(actual.Candidates[i]!=expected.Candidates[i])throw new InvalidOperationException("baseline-candidate-order:"+stage);
            byte[] candidateCapacity=(byte[])CopiedPlanningBundle.Resolve(b,pre+"/candidateCapacity8").Clone();if(candidateCapacity.Length!=8000)throw new InvalidOperationException("candidate-capacity");for(int i=0;i<actual.Candidates.Count;i++)Buffer.BlockCopy(BitConverter.GetBytes(actual.Candidates[i]),0,candidateCapacity,i*8,4);Equal(candidateCapacity,CopiedPlanningBundle.Resolve(b,stage+"/candidateCapacity8"),stage+"/whole-candidate-table-preserved-second-column");
        }
        internal static bool TryBaseline(BridgePlanningCapture.Bundle bundle,VirtualBridgeMap map,out string reason)
        {
            reason="missing-historical-planning-values";if(bundle==null)return false;
            try
            {
                VirtualPlanningInput input;VirtualPlanningState state;
                if(!CopiedPlanningBundle.TryRead(bundle,map,"seed-pre",out input,out state,out reason))return false;
                int[] seed=Ints(CopiedPlanningBundle.Resolve(bundle,"seed-pre/arguments4")),players=Ints(CopiedPlanningBundle.Resolve(bundle,"playerScalars7"));
                if(seed.Length!=4||seed[0]!=bundle.Target||players.Length!=63)throw new ArgumentException("seed-player-contract");
                int x=players[bundle.Target*7+1],y=players[bundle.Target*7+2];if(y<0||y>=800)throw new ArgumentException("target-castle-row");int tile=checked(input.Rows[y]+x);
                if(!VirtualBridgePlanning.Seed(input,state,tile,players[bundle.Target*7]!=0,seed[1]!=0))throw new InvalidOperationException(state.Reason);Compare(bundle,state,map,"seed-post");
                if(!CopiedPlanningBundle.TryRead(bundle,map,"distance-pre",out input,out state,out reason))return false;
                int[] distance=Ints(CopiedPlanningBundle.Resolve(bundle,"distance-pre/arguments4"));if(distance.Length!=4||distance[3]!=bundle.Attacker)throw new ArgumentException("distance-player-contract");
                Func<int,int,int?> regions;if(!CopiedPlanningRegions.TryCreate(bundle,"distance-pre",true,bundle.Attacker,0,out regions,out reason))return false;
                if(!VirtualBridgePlanning.Distance(input,state,distance[0],distance[1],distance[2],regions))throw new InvalidOperationException(state.Reason);Compare(bundle,state,map,"distance-post");
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
                reason=selections==0?"baseline-fields-match;target-selection-not-observed":"baseline-fields-and-native-selection-match;effective-fixes-count-separate";return true;
            }
            catch(Exception error){reason="baseline-not-proven:"+error.GetType().Name+":"+error.Message;return false;}
        }
    }
}

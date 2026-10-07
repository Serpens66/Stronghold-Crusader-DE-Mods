using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EnemyBridgePathTest
{
    // Offline reader. Never accepts an unfinished publisher file or invents schema1 data.
    internal static class BridgePlanningImporter
    {
        internal sealed class Artifact
        {
            internal int Schema;
            internal string Hash;
            internal readonly Dictionary<string,string> Metadata=new Dictionary<string,string>(StringComparer.Ordinal);
            internal readonly Dictionary<string,byte[]> Sections=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            internal BridgePlanningCapture.Bundle Planning;
        }
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        private static long Number(Artifact a,string key)
        {if(!a.Metadata.TryGetValue(key,out string text)||!long.TryParse(text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out long n))throw new InvalidDataException("missing-or-invalid-"+key);return n;}
        internal static bool TryLinkGroup(Artifact planning,Artifact group,out string reason)
        {
            reason="unresolved-planning-group-link";
            try
            {
                if(planning?.Planning==null||!planning.Planning.Complete||group==null||group.Schema!=2)throw new InvalidDataException("missing-complete-planning");
                var b=planning.Planning;long current=Number(group,"planningRoot"),retained=Number(group,"decisionRoot");
                if(Number(group,"session")!=b.Session||Number(group,"planningBundleRoot")!=b.Root||Number(group,"planningBundleDefinition")!=Number(planning,"definition")||Number(group,"player")!=b.Attacker||b.Root<=0)throw new InvalidDataException("artifact-source-identity");
                if(current!=b.Root&&retained!=b.Root)throw new InvalidDataException("unrelated-military-root");
                if(!group.Metadata.TryGetValue("planningAssociation",out string association)||association!=(current==b.Root?"same-parent-root":"retained-decision-root"))throw new InvalidDataException("association-branch");
                reason="exact-artifact-source-linked;not-a-positive-region-proof";return true;
            }
            catch(Exception error){reason="unresolved-planning-group-link:"+error.Message;return false;}
        }
        internal static bool TryRead(string path,out Artifact artifact,out string reason)
        {
            artifact=null;reason="invalid-artifact";
            try
            {
                if(!string.Equals(Path.GetExtension(path),".bin",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("unfinished-artifact");
                var info=new FileInfo(path);if(info.Length<64||info.Length>128L*1024*1024)throw new InvalidDataException("artifact-extent");
                byte[] bytes=File.ReadAllBytes(path);int end=bytes.Length-48;
                if(Encoding.ASCII.GetString(bytes,0,8)!="BRGINP01"||Encoding.ASCII.GetString(bytes,bytes.Length-8,8)!="BRGEND01"||BitConverter.ToInt64(bytes,end+32)!=end)throw new InvalidDataException("footer-or-header");
                var a=new Artifact {Schema=BitConverter.ToInt32(bytes,8)};
                if(a.Schema!=1&&a.Schema!=2)throw new InvalidDataException("unsupported-schema");
                using(var sha=SHA256.Create())
                {byte[] hash=sha.ComputeHash(bytes,0,end);for(int i=0;i<32;i++)if(hash[i]!=bytes[end+i])throw new InvalidDataException("payload-hash");a.Hash=BridgeInputArtifact.Hex(sha.ComputeHash(bytes));}
                int metadataLength=BitConverter.ToInt32(bytes,12);if(metadataLength<0||metadataLength>65536||16L+metadataLength>end)throw new InvalidDataException("metadata-extent");
                foreach(string row in Utf8.GetString(bytes,16,metadataLength).Split('\n'))
                {if(row.Length==0)continue;int split=row.IndexOf('=');if(split<=0)throw new InvalidDataException("metadata-entry");a.Metadata.Add(row.Substring(0,split),row.Substring(split+1));}
                if(a.Metadata.TryGetValue("nativeHash",out string nativeHash)&&nativeHash!=NativePathfindingTableCopy.Hash)throw new InvalidDataException("native-provenance");
                if(a.Metadata.TryGetValue("backendHash",out string backendHash)&&backendHash!="0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0")throw new InvalidDataException("backend-provenance");
                int cursor=16+metadataLength;
                while(cursor<end)
                {
                    if(end-cursor<4)throw new InvalidDataException("section-header");int length=BitConverter.ToInt32(bytes,cursor);cursor+=4;
                    if(length<1||length>1024||end-cursor<length+8)throw new InvalidDataException("section-name");string name=Utf8.GetString(bytes,cursor,length);cursor+=length;
                    int count=BitConverter.ToInt32(bytes,cursor),width=BitConverter.ToInt32(bytes,cursor+4);cursor+=8;
                    long size=(long)count*width;if(count<0||(width!=1&&width!=2&&width!=4)||size>16*1024*1024||size>end-cursor)throw new InvalidDataException("section-extent");
                    var data=new byte[(int)size];Buffer.BlockCopy(bytes,cursor,data,0,data.Length);cursor+=data.Length;
                    if(name=="gateMaskChunk"&&a.Sections.TryGetValue(name,out byte[] previous))
                    {if(previous.Length+size>16*1024*1024)throw new InvalidDataException("chunk-extent");var merged=new byte[previous.Length+data.Length];Buffer.BlockCopy(previous,0,merged,0,previous.Length);Buffer.BlockCopy(data,0,merged,previous.Length,data.Length);a.Sections[name]=merged;}
                    else a.Sections.Add(name,data);
                }
                if(a.Schema==2&&a.Metadata.TryGetValue("planningComplete",out string complete)&&complete=="True")
                {
                    var b=new BridgePlanningCapture.Bundle {Complete=true,Reason="validated-file",Op=Number(a,"planningBundleOp"),Parent=Number(a,"planningParent"),Root=Number(a,"planningBundleRoot"),Clock=Number(a,"planningCaptureClock"),Session=a.Metadata.ContainsKey("planningSession")?Number(a,"planningSession"):Number(a,"session"),Attacker=checked((int)Number(a,"planningAttacker")),Target=checked((int)Number(a,"planningTarget")),Mode=checked((int)Number(a,"planningMode")),Bank=checked((int)Number(a,"planningBank")),Thread=checked((int)Number(a,"planningThread")),Revision=a.Metadata.ContainsKey("planningRevision")?checked((int)Number(a,"planningRevision")):0,Dirty=a.Metadata.ContainsKey("planningDirty")?checked((int)Number(a,"planningDirty")):0};
                    if(b.Session<=0||b.Op<=0||b.Attacker<1||b.Attacker>8||b.Target<1||b.Target>8||b.Bank!=b.Target)throw new InvalidDataException("planning-identity");
                    if(a.Metadata.ContainsKey("session")&&Number(a,"session")!=b.Session)throw new InvalidDataException("planning-source-session");
                    foreach(var pair in a.Sections)
                    {if(pair.Key.StartsWith("plan/ref/",StringComparison.Ordinal))b.References.Add(pair.Key.Substring(9),Utf8.GetString(pair.Value));else if(pair.Key.StartsWith("plan/",StringComparison.Ordinal))b.Sections.Add(pair.Key.Substring(5),pair.Value);}
                    foreach(var pair in b.References)if(b.Sections.ContainsKey(pair.Key)||!b.Sections.ContainsKey(pair.Value))throw new InvalidDataException("missing-or-conflicting-definition");
                    foreach(string stage in new[]{"caller-pre","seed-pre","seed-post","distance-pre","distance-post","caller-post"})
                    {
                        string[] names={"seeds","visits","distance","queue","queueRows","controls","queueControls","selectedBank","candidateCount"};int[] lengths={320800,641600,641600,1283200,641600,224,52,4,4};
                        for(int i=0;i<names.Length;i++)if(CopiedPlanningBundle.Resolve(b,stage+"/"+names[i]).Length!=lengths[i])throw new InvalidDataException("stage-extent:"+stage+"/"+names[i]);
                        int candidates=BitConverter.ToInt32(CopiedPlanningBundle.Resolve(b,stage+"/candidateCount"),0);if(candidates<0||candidates>1000||CopiedPlanningBundle.Resolve(b,stage+"/candidates8").Length!=candidates*8)throw new InvalidDataException("candidate-extent");
                        if(b.Sections.ContainsKey(stage+"/candidateCapacity8")||b.References.ContainsKey(stage+"/candidateCapacity8"))if(CopiedPlanningBundle.Resolve(b,stage+"/candidateCapacity8").Length!=8000)throw new InvalidDataException("candidate-table-capacity");
                        if(stage=="seed-pre"||stage=="seed-post"||stage=="distance-pre"||stage=="distance-post")if(CopiedPlanningBundle.Resolve(b,stage+"/arguments4").Length!=16)throw new InvalidDataException("arguments-extent");
                        if(stage=="distance-pre"||stage=="distance-post")if(BitConverter.ToInt32(CopiedPlanningBundle.Resolve(b,stage+"/selectedBank"),0)!=b.Bank)throw new InvalidDataException("actual-distance-bank");
                    }
                    string[] common={"flags","components","edges","directionOffsets","rowRecords","tileRows","buildingIds","coarseRecords","profiles90","permissions540","componentCounts1000","playerScalars7","buildingCount"};int[] sizes={1283200,641600,320800,25600,9600,641600,641600,1228800,360,2160,4000,252,4};
                    for(int i=0;i<common.Length;i++)if(CopiedPlanningBundle.Resolve(b,common[i]).Length!=sizes[i])throw new InvalidDataException("common-extent:"+common[i]);
                    int buildings=BitConverter.ToInt32(CopiedPlanningBundle.Resolve(b,"buildingCount"),0);if(buildings<1||buildings>4001||CopiedPlanningBundle.Resolve(b,"buildingRecords").Length!=(buildings-1)*0x32c)throw new InvalidDataException("building-extent");
                    foreach(var pair in b.Sections)if(pair.Key.StartsWith("target/",StringComparison.Ordinal)&&pair.Key.EndsWith("/values8",StringComparison.Ordinal))
                    {
                        byte[] identity=CopiedPlanningBundle.Resolve(b,pair.Key.Substring(0,pair.Key.Length-7)+"identity5");
                        long op;if(!long.TryParse(pair.Key.Split('/')[1],out op)||pair.Value.Length!=32||identity.Length!=40||BitConverter.ToInt64(identity,0)!=b.Session||BitConverter.ToInt64(identity,8)!=op||BitConverter.ToInt64(identity,24)!=b.Root||BitConverter.ToInt32(pair.Value,0)!=b.Attacker)throw new InvalidDataException("target-selection-identity");
                        byte[] args=CopiedPlanningBundle.Resolve(b,pair.Key.Substring(0,pair.Key.Length-7)+"argumentsAndTiles4");if(args.Length!=16||BitConverter.ToInt32(args,0)!=b.Attacker||BitConverter.ToInt32(args,4)!=BitConverter.ToInt32(pair.Value,4))throw new InvalidDataException("target-argument-identity");
                    }
                    if(a.Metadata.TryGetValue("planningConsumerCaptureVersion",out string consumerVersion)&&consumerVersion!="0"&&consumerVersion!="1"&&consumerVersion!="2")throw new InvalidDataException("consumer-version");
                    b.ConsumerVersion=string.IsNullOrEmpty(consumerVersion)?0:int.Parse(consumerVersion);
                    if((consumerVersion=="1"||consumerVersion=="2")&&!b.Sections.ContainsKey("consumer/pre/identity"))throw new InvalidDataException("missing-versioned-consumer");
                    if(b.Sections.ContainsKey("consumer/pre/identity"))
                    {
                        foreach(string stage in new[]{"consumer/pre","consumer/post","consumer/build-pre","consumer/build-post","consumer/weight-pre","consumer/weight-post"})
                        {
                            byte[] identity=CopiedPlanningBundle.Resolve(b,stage+"/identity");int expected=stage=="consumer/pre"||stage=="consumer/post"?72:80;
                            if(identity.Length!=expected||BitConverter.ToInt64(identity,0)!=b.Session||BitConverter.ToInt64(identity,8)!=b.Root||BitConverter.ToInt64(identity,16)<=0)throw new InvalidDataException("consumer-identity:"+stage);
                            if(CopiedPlanningBundle.Resolve(b,stage+"/candidates").Length!=VirtualCandidateConsumers.Bytes||CopiedPlanningBundle.Resolve(b,stage+"/selectionMask").Length!=320800||CopiedPlanningBundle.Resolve(b,stage+"/seeds").Length!=320800)throw new InvalidDataException("consumer-state-extent:"+stage);
                        }
                        byte[] before=CopiedPlanningBundle.Resolve(b,"consumer/pre/identity"),after=CopiedPlanningBundle.Resolve(b,"consumer/post/identity");if(BitConverter.ToInt64(before,16)!=BitConverter.ToInt64(after,16)||BitConverter.ToInt64(before,32)!=b.Attacker||BitConverter.ToInt64(before,40)!=b.Target||BitConverter.ToInt64(after,32)!=b.Attacker||BitConverter.ToInt64(after,40)!=b.Target)throw new InvalidDataException("consumer-pre-post-binding");
                        foreach(string family in new[]{"build","weight"})
                        {
                            byte[] entry=CopiedPlanningBundle.Resolve(b,"consumer/"+family+"-pre/identity"),exit=CopiedPlanningBundle.Resolve(b,"consumer/"+family+"-post/identity");
                            for(int n=0;n<7;n++)if(BitConverter.ToInt64(entry,n*8)!=BitConverter.ToInt64(exit,n*8))throw new InvalidDataException("consumer-child-pre-post-binding");
                            if(BitConverter.ToInt64(entry,24)!=BitConverter.ToInt64(before,16)||BitConverter.ToInt64(entry,32)!=(family=="build"?b.Attacker:b.Target)||BitConverter.ToInt64(entry,40)!=(family=="build"?b.Target:b.Attacker))throw new InvalidDataException("consumer-child-parent-or-player");
                        }
                        if(CopiedPlanningBundle.Resolve(b,"consumer/pre/macroLimit").Length!=4||CopiedPlanningBundle.Resolve(b,"consumer/pre/buildingCount").Length!=4)throw new InvalidDataException("consumer-count-extent");
                        int consumerBuildings=BitConverter.ToInt32(CopiedPlanningBundle.Resolve(b,"consumer/pre/buildingCount"),0);if(consumerBuildings<1||consumerBuildings>4001||CopiedPlanningBundle.Resolve(b,"consumer/pre/buildingRecords").Length!=(consumerBuildings-1)*0x32c||CopiedPlanningBundle.Resolve(b,"consumer/post/players").Length!=9*0x583c)throw new InvalidDataException("consumer-building-or-player-extent");
                        int macroLimit=BitConverter.ToInt32(CopiedPlanningBundle.Resolve(b,"consumer/pre/macroLimit"),0);if(macroLimit<1||macroLimit>1000||CopiedPlanningBundle.Resolve(b,"consumer/pre/macroRecords").Length!=(macroLimit-1)*0x204||CopiedPlanningBundle.Resolve(b,"consumer/pre/nativeAlliances9").Length!=36)throw new InvalidDataException("consumer-macro-capacity");
                        byte[] units=CopiedPlanningBundle.Resolve(b,"consumer/pre/unitManager");int unused;VirtualCandidateConsumers.UnitTargets(units,b.Attacker,out unused);
                        string[] consumerNames={"unitTiles","height","baseHeight","terrainOwner","flags","components","edges","buildingIds","costBranch","players","tribes"};int[] consumerSizes={641600,320800,320800,320800,1283200,641600,320800,641600,1,9*0x583c,0x2a+4500*0x688};for(int n=0;n<consumerNames.Length;n++)if(CopiedPlanningBundle.Resolve(b,"consumer/pre/"+consumerNames[n]).Length!=consumerSizes[n])throw new InvalidDataException("consumer-input-extent:"+consumerNames[n]);
                        if(consumerVersion=="2")
                        {
                            if(CopiedPlanningBundle.Resolve(b,"buildingUpdaterControls").Length!=8||CopiedPlanningBundle.Resolve(b,"buildingUpdaterSlots").Length!=3999*0x32c||CopiedPlanningBundle.Resolve(b,"consumer/pre/packedValidity").Length!=800*800||CopiedPlanningBundle.Resolve(b,"consumer/pre/combatClassMask").Length!=90||CopiedPlanningBundle.Resolve(b,"consumer/pre/buildingTransitionClasses").Length!=336*4||CopiedPlanningBundle.Resolve(b,"consumer/pre/buildingSeedClasses").Length!=336*4)throw new InvalidDataException("consumer-v2-native-input-extents");
                            foreach(string stage in new[]{"consumer/pre","consumer/post"})
                            {
                                string[] names={"workControls","workQueueControls","workQueue","workQueueRows","workQueueX","workDistances","workVisits","workTargets"};int[] sizes2={224,52,1283200,641600,641600,641600,641600,7200};for(int n=0;n<names.Length;n++)if(CopiedPlanningBundle.Resolve(b,stage+"/"+names[n]).Length!=sizes2[n])throw new InvalidDataException("consumer-v2-work-extent:"+names[n]);
                            }
                            byte[] modePre=CopiedPlanningBundle.Resolve(b,"consumer/weight-pre/gameModeValues"),modePost=CopiedPlanningBundle.Resolve(b,"consumer/weight-post/gameModeValues");if(modePre.Length!=8||modePost.Length!=8)throw new InvalidDataException("consumer-v2-mode-extent");for(int n=0;n<8;n++)if(modePre[n]!=modePost[n])throw new InvalidDataException("weight-mode-changed-during-call");
                        }
                        foreach(string state in new[]{"entryPlayer","exitPlayer"})if(CopiedPlanningBundle.Resolve(b,"military/"+state).Length!=0x583c)throw new InvalidDataException("military-player-extent");
                    }
                    b.Stages.AddRange(new[]{0,1,2,3});a.Planning=b;
                }
                artifact=a;reason=a.Planning==null?"missing-historical-planning-values":"validated-planning-artifact";return true;
            }
            catch(Exception error){reason="invalid-artifact:"+error.GetType().Name+":"+error.Message;return false;}
        }
    }
}

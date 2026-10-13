using System;
namespace EnemyBridgePathTest
{
    // Restores ONLY own command Pre bytes. It never merges a later topology,
    // planner consumer or current game lookup into missing command inputs.
    internal static class CopiedStructureInput
    {
        internal static bool TryCreate(BridgePlanningImporter.Artifact artifact,out VirtualCandidateBuilder builder,out string reason)
        {
            builder=null;reason="Unknown:missing-own-structure-command";
            try
            {
                if(artifact==null||!artifact.Sections.TryGetValue("structure/identity18",out byte[] identity))return false;
                if(identity.Length!=144)throw new ArgumentException("structure-identity-extent");long[] fields=new long[18];Buffer.BlockCopy(identity,0,fields,0,identity.Length);
                var b=new VirtualCandidateBuilder();
                if(!artifact.Sections.TryGetValue("structure/pre/workControls",out byte[] controlBytes)||controlBytes.Length!=0xe0)throw new ArgumentException("structure-controls");int limit=BitConverter.ToInt32(controlBytes,0);if(limit<1||limit>1000||!artifact.Sections.TryGetValue("structure/pre/macroRecords",out byte[] records)||records.Length!=(limit-1)*0x204)throw new ArgumentException("structure-macro-extent");
                void Put(string name,int address,int expected=-1)
                {if(!artifact.Sections.TryGetValue("structure/pre/"+name,out byte[] bytes)||expected>=0&&bytes.Length!=expected)throw new ArgumentException("missing-own-command:"+name);b.Put(address,bytes);}
                foreach(var p in new[]{Tuple.Create("flags",0x48f71b0,1283200),Tuple.Create("components",0x50ec690,641600),Tuple.Create("edges",0x51890d0,320800),Tuple.Create("height",0x4ddd350,320800),Tuple.Create("baseHeight",0x4e2b870,320800),Tuple.Create("buildingIds",0x4b6aa50,641600),Tuple.Create("packedValidity",0x3a11ea4,640000),Tuple.Create("rowRecords",0x402ff2c,9600),Tuple.Create("tileRows",0x3aae2a4,641600),Tuple.Create("directionOffsets",0x405edb0,25600),Tuple.Create("ringOffsets",0x2e3fa0,0xb60),Tuple.Create("ringCounts14",0x2e8fd8,56),Tuple.Create("contactDirections8",0x3123b0,32),Tuple.Create("buildingHeights336",0x2e5360,1344),Tuple.Create("nativeAlliances9",0x37edf3c,36),Tuple.Create("approaches501",0x60ad660+0x1b344,6012),Tuple.Create("workControls",0x60ad660,0xe0),Tuple.Create("workQueueControls",0x60ad660+0x155f38,0x34),Tuple.Create("workQueue",0x60ad660+0x155f6c,1283200),Tuple.Create("workQueueRows",0x60ad660+0x28f3ec,641600),Tuple.Create("workQueueX",0x60ad660+0x32be2c,641600),Tuple.Create("workDistances",0x5225b10,641600),Tuple.Create("workVisits",0x52c2550,641600),Tuple.Create("workDirections64",0x2d2e50,64),Tuple.Create("terrainOwner",0x4e79d90,320800),Tuple.Create("costConfig4",0x8574b90,4),Tuple.Create("structureConfig4",0x37ed4c4,4),Tuple.Create("buildingTransitionClasses",0x2e6710,1344)})Put(p.Item1,p.Item2,p.Item3);
                Put("players9",0x379ae00,9*0x583c);Put("unitManagerFull",0x67e8400,BridgePlanningCapture.EntryUnitBytes);Put("tribesFull",0x7cc6720,BridgePlanningCapture.EntryTribeBytes);
                if(artifact.Sections.ContainsKey("structure/pre/profiles90"))Put("profiles90",0x322540,360);
                if(artifact.Sections.ContainsKey("structure/pre/permissions540"))Put("permissions540",0x32BDB0,2160);
                if(artifact.Sections.ContainsKey("structure/pre/lineMode"))Put("lineMode",0x2f7a690,4);
                if(artifact.Sections.ContainsKey("structure/pre/lineBuildingClasses336"))Put("lineBuildingClasses336",0x2e68d0,1344);
                if(artifact.Sections.ContainsKey("structure/pre/lineBlockGrid"))Put("lineBlockGrid",0x51d75f0,320800);
                Put("tribe",checked(0x7cc6720+(int)fields[6]*0x688),0x688);Put("buildingRecords",0x64ccbb0+0x32c);Put("target",checked(0x64ccbb0+(int)fields[9]*0x32c),0x32c);Put("macroRecords",0x60ad660+0x2228);
                b.Put(0x67e8380,new byte[8]); // C0270 writes both values before every read.
                foreach(var pair in artifact.Sections)if(pair.Key.StartsWith("structure/pre/unit-",StringComparison.Ordinal))
                {int id=int.Parse(pair.Key.Substring(19),System.Globalization.CultureInfo.InvariantCulture);if(id<1||id>9999||pair.Value.Length!=0xb00)throw new ArgumentException("structure-unit-slot");b.Put(0x67e8400+id*0x490,pair.Value);}
                string origin=artifact.Sections.TryGetValue("structure/observationOrigin",out byte[] rawOrigin)?System.Text.Encoding.UTF8.GetString(rawOrigin):"legacy-own-command-pre";
                builder=b;reason="own-input-restored;origin="+origin+";freshDecisionProof=False;dirty="+fields[16]+";nativeGeneration="+fields[17]+";postUsedOnlyForComparison=True;originalSelectorResult=not-observed;originalDA020Arguments=requires-computed-command-entry;ownCommandProfiles="+(artifact.Sections.ContainsKey("structure/pre/profiles90")?"present":"Unknown:missing-own-322540-table")+";ownCommandArguments="+(artifact.Sections.ContainsKey("structure/unitCommandArguments7")?"present":"requires-exact-hash-bound-log-pre")+";originalCommandReplay=not-executed;behaviorFix=disabled";return true;
            }
            catch(Exception error){reason="Unknown:structure-command-input:"+error.Message;return false;}
        }
        internal static bool TryCommandClosure(BridgePlanningImporter.Artifact artifact,out string reason)
        {
            if(!TryCreate(artifact,out var builder,out reason))return false;
            try
            {
                if(!artifact.Sections.TryGetValue("structure/unitCommandArguments7",out var args)||args.Length!=56)throw new ArgumentException("missing-original-unit-command-arguments");
                int id=checked((int)BitConverter.ToInt64(args,16)),record=checked(0x67e8400+id*0x490);
                int type=BitConverter.ToInt16(builder.Bytes(record+0x6e6,2),0);if(type<0||type>=90)throw new ArgumentException("unknown-unit-type");
                builder.Bytes(0x322540+type*4,4);
                int mode=BitConverter.ToInt32(builder.Bytes(0x2f7a690,4),0);
                builder.Bytes(0x51d75f0,320800); // Normal flood fallback is independent of line mode.
                builder.Bytes(0x2e68d0,1344);
                reason="own-command-input-closure=initial-profile-line-and-normal-flood-inputs-only;fullCommandReplay=Unknown;postUsedOnlyForComparison=True;behaviorFix=disabled";return true;
            }
            catch(Exception error){reason="Unknown:own-command-input-closure:"+error.Message+";time=unit-command-pre-before-196280;post-substitution=False";return false;}
        }
    }
}

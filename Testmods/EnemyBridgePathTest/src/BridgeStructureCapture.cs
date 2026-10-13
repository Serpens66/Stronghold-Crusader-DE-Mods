using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace EnemyBridgePathTest
{
    // Own command-event inputs. Does not call a selector, path search or game API.
    internal sealed class BridgeStructureCapture
    {
        internal sealed class Input
        {
            internal long Session,Op,Root,Thread,Clock,PostClock;internal int Player,Tribe,Command,Building;internal uint TribeGlobal,BuildingGlobal;
            internal bool ExplicitBuilding,Complete;internal object Token;
            internal int Unit;internal uint UnitGlobal;internal string Origin="group-command-pre",TargetEvidence="legacy";internal bool SnapshotOnly;
            internal int RelevanceRank=>ExplicitBuilding?4:TargetEvidence.StartsWith("native-unit-context",StringComparison.Ordinal)?(SnapshotOnly?2:3):1;
            internal readonly Dictionary<string,byte[]> Sections=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        }
        private readonly Func<int,int,byte[]> read;private readonly Func<object> token;private readonly Action<string,string> emit;
        private readonly object gate=new object();private long session;private int attempts;private Input pending,retained;
        internal BridgeStructureCapture(Func<int,int,byte[]> read,Func<object> token,Action<string,string> emit){this.read=read;this.token=token;this.emit=emit;}
        internal bool Available {get {lock(gate)return session!=0&&attempts<2&&pending==null&&(retained==null||retained.RelevanceRank<4);}}
        internal bool CanCaptureRank(int rank){lock(gate)return Available&&(retained==null||rank>retained.RelevanceRank);}
        internal void Begin(long value){lock(gate){session=value;attempts=0;pending=retained=null;emit("structure-capture-ready","selection=dynamic-coupled-target-or-coordinate-candidate,attemptLimit=2,requires=own-command-pre-post,selectorAndAttackProof=separate,behavior=unchanged");}}
        internal void End(){lock(gate){if(pending!=null)emit("structure-command-incomplete","op="+pending.Op+",reason=map-ended-before-command-post");pending=null;emit("structure-capture-end","attempts="+attempts+",complete="+(retained?.Complete??false)+",missing="+(retained==null?"own-relevant-structure-command":"selector-and-executed-attack-remain-separate")+",behavior=unchanged");session=0;}}
        private int Int(int a)=>BitConverter.ToInt32(read(a,4),0);
        private static uint Global(byte[] unit)=>BitConverter.ToUInt32(unit,0x6f0);
        internal Input Capture(long op,long root,int player,int tribe,uint tribeGlobal,int command,int building,uint buildingGlobal,bool explicitBuilding,int fromX,int fromY,int orderX,int orderY,int unit=0,uint unitGlobal=0,uint unitTargetTile=0,string origin="group-command-pre",string targetEvidence="legacy",bool snapshotOnly=false)
        {lock(gate)return CaptureCore(op,root,player,tribe,tribeGlobal,command,building,buildingGlobal,explicitBuilding,fromX,fromY,orderX,orderY,unit,unitGlobal,unitTargetTile,origin,targetEvidence,snapshotOnly);}
        private Input CaptureCore(long op,long root,int player,int tribe,uint tribeGlobal,int command,int building,uint buildingGlobal,bool explicitBuilding,int fromX,int fromY,int orderX,int orderY,int unit,uint unitGlobal,uint unitTargetTile,string origin,string targetEvidence,bool snapshotOnly)
        {
            int rank=explicitBuilding?4:targetEvidence.StartsWith("native-unit-context",StringComparison.Ordinal)?(snapshotOnly?2:3):1;
            if(retained!=null&&rank<=retained.RelevanceRank)return null;
            if(!Available||token()==null||player<1||player>8||tribe<1||tribe>=4500||building<1||building>4000||tribeGlobal==0||buildingGlobal==0||(uint)fromX>=800||(uint)fromY>=800||(uint)orderX>=800||(uint)orderY>=800)return null;
            attempts++;long start=Stopwatch.GetTimestamp();
            try
            {
                var input=new Input{Session=session,Op=op,Root=root,Player=player,Tribe=tribe,TribeGlobal=tribeGlobal,Command=command,Building=building,BuildingGlobal=buildingGlobal,ExplicitBuilding=explicitBuilding,Token=token(),Thread=Thread.CurrentThread.ManagedThreadId,Clock=start,Unit=unit,UnitGlobal=unitGlobal,Origin=origin,TargetEvidence=targetEvidence,SnapshotOnly=snapshotOnly};
                void Copy(string name,int a,int n){input.Sections.Add("pre/"+name,read(a,n));}
                int buildingLimit=Int(0x64ccbb0+0x50);if(buildingLimit<1||buildingLimit>4001||building>=buildingLimit)throw new InvalidOperationException("command-building-high-water");
                int macroLimit=Int(0x60ad660);if(macroLimit<1||macroLimit>1000)throw new InvalidOperationException("command-macro-limit");
                Copy("players9",0x379ae00,9*0x583c);
                // Own writable command tables: 196280 reads profiles on entry.
                Copy("profiles90",0x322540,90*4);
                Copy("permissions540",0x32BDB0,90*6*4);
                Copy("lineMode",0x2f7a690,4);
                Copy("lineBuildingClasses336",0x2e68d0,336*4);
                Copy("lineBlockGrid",0x51d75f0,320800); // DAAC0 fallback reads this even when F32B0 line mode is zero.
                input.Sections.Add("pre/pathInputCoverage4",BitConverter.GetBytes(1));
                Copy("unitManagerFull",0x67e8400,BridgePlanningCapture.EntryUnitBytes);Copy("tribesFull",0x7cc6720,BridgePlanningCapture.EntryTribeBytes);
                Copy("tribe",0x7cc6720+tribe*0x688,0x688);Copy("target",0x64ccbb0+building*0x32c,0x32c);
                if(BitConverter.ToInt32(input.Sections["pre/tribe"],0x2c)!=player||BitConverter.ToUInt32(input.Sections["pre/tribe"],0x34)!=tribeGlobal||BitConverter.ToUInt32(input.Sections["pre/target"],0x134)!=buildingGlobal)throw new InvalidOperationException("command-identity");
                int leader=BitConverter.ToInt16(input.Sections["pre/tribe"],0x5a);var ids=new HashSet<int>{leader};var tribeBytes=input.Sections["pre/tribe"];
                for(int word=0;word<0x271;word++){ushort bits=BitConverter.ToUInt16(tribeBytes,0x60+word*2);for(int bit=0;bit<16;bit++)if((bits&(1<<bit))!=0)ids.Add(word*16+bit);}
                if(unit!=0)ids.Add(unit);
                foreach(int id in ids){if(id<1||id>9999)throw new InvalidOperationException("command-member-identity");Copy("unit-"+id,0x67e8400+id*0x490,0xb00);}
                // Native slot window starts 0x65C before the public GameUnit:
                // Global 0x65C+0x94=0x6F0; context tile 0x65C+0x3A4=0xA00.
                if(unit!=0){if(unitGlobal==0||Global(input.Sections["pre/unit-"+unit])!=unitGlobal||BitConverter.ToUInt32(input.Sections["pre/unit-"+unit],0xa00)!=unitTargetTile)throw new InvalidOperationException("unit-structure-identity-or-target-changed");long[] own={unit,unitGlobal,unitTargetTile,snapshotOnly?1:0};var bytes=new byte[32];Buffer.BlockCopy(own,0,bytes,0,32);input.Sections.Add("unitIdentity4",bytes);}
                input.Sections.Add("observationOrigin",System.Text.Encoding.UTF8.GetBytes(origin));input.Sections.Add("targetEvidence",System.Text.Encoding.UTF8.GetBytes(targetEvidence));
                foreach(var field in new[]{Tuple.Create("flags",0x48f71b0,320800*4),Tuple.Create("components",0x50ec690,320800*2),Tuple.Create("edges",0x51890d0,320800),Tuple.Create("height",0x4ddd350,320800),Tuple.Create("baseHeight",0x4e2b870,320800),Tuple.Create("buildingIds",0x4b6aa50,320800*2),Tuple.Create("packedValidity",0x3a11ea4,640000),Tuple.Create("rowRecords",0x402ff2c,9600),Tuple.Create("tileRows",0x3aae2a4,320800*2),Tuple.Create("directionOffsets",0x405edb0,25600),Tuple.Create("ringOffsets",0x2e3fa0,0xb60),Tuple.Create("ringCounts14",0x2e8fd8,56),Tuple.Create("contactDirections8",0x3123b0,32),Tuple.Create("buildingHeights336",0x2e5360,336*4),Tuple.Create("nativeAlliances9",0x37edf3c,36),Tuple.Create("approaches501",0x60ad660+0x1b344,501*12),Tuple.Create("workControls",0x60ad660,0xe0),Tuple.Create("macroRecords",0x60ad660+0x2228,(macroLimit-1)*0x204),Tuple.Create("buildingRecords",0x64ccbb0+0x32c,(buildingLimit-1)*0x32c),Tuple.Create("workQueueControls",0x60ad660+0x155f38,0x34),Tuple.Create("workQueue",0x60ad660+0x155f6c,320800*4),Tuple.Create("workQueueRows",0x60ad660+0x28f3ec,320800*2),Tuple.Create("workQueueX",0x60ad660+0x32be2c,320800*2),Tuple.Create("workDistances",0x5225b10,320800*2),Tuple.Create("workVisits",0x52c2550,320800*2),Tuple.Create("workDirections64",0x2d2e50,64),Tuple.Create("terrainOwner",0x4e79d90,320800),Tuple.Create("costConfig4",0x8574b90,4),Tuple.Create("structureConfig4",0x37ed4c4,4),Tuple.Create("buildingTransitionClasses",0x2e6710,336*4)})
                {if(field.Item3<0||field.Item3>1000*0x204&&field.Item1=="macroRecords")throw new InvalidOperationException("command-macro-limit");Copy(field.Item1,field.Item2,field.Item3);}
                long[] identity={session,op,root,input.Thread,start,player,tribe,tribeGlobal,command,building,buildingGlobal,explicitBuilding?1:0,fromX,fromY,orderX,orderY,Int(0x60ad660+0x6c),Int(0x60ad660+0x74)};
                var raw=new byte[identity.Length*8];Buffer.BlockCopy(identity,0,raw,0,raw.Length);input.Sections.Add("identity18",raw);
                // Validates own stable identities and physical grids, not a claim
                // that later command Post is the time of the selector itself.
                if(!ReferenceEquals(input.Token,token())||Int(0x60ad660+0x74)!=identity[17]||Int(0x60ad660+0x6c)!=identity[16]||Int(0x7cc6720+tribe*0x688+0x34)!=unchecked((int)tribeGlobal)||Int(0x64ccbb0+building*0x32c+0x134)!=unchecked((int)buildingGlobal))throw new InvalidOperationException("command-changed-during-copy");
                foreach(string name in new[]{"flags","components","edges"}){int address=name=="flags"?0x48f71b0:name=="components"?0x50ec690:0x51890d0;var old=input.Sections["pre/"+name];var now=read(address,old.Length);for(int i=0;i<old.Length;i++)if(old[i]!=now[i])throw new InvalidOperationException("command-physical-changed-during-copy");}
                CheckOwnTables(input);pending=input;emit("structure-command-copied","op="+op+",player="+player+",building="+building+"/g"+buildingGlobal+",unit="+unit+"/g"+unitGlobal+",targetEvidence="+targetEvidence+",timing="+origin+",freshDecisionProof=False,captureMs="+((Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",attempt="+attempts+",behavior=unchanged");return input;
            }
            catch(Exception error){emit("structure-command-incomplete","op="+op+",reason="+error.Message);return null;}
        }
        internal Input Complete(Input input,bool originalCalled){lock(gate)return CompleteCore(input,originalCalled);}
        private void CheckOwnTables(Input input)
        {
            foreach(var field in new[]{Tuple.Create("profiles90",0x322540),Tuple.Create("permissions540",0x32BDB0),Tuple.Create("lineMode",0x2f7a690),Tuple.Create("lineBuildingClasses336",0x2e68d0)})
            {
                var before=input.Sections["pre/"+field.Item1];var now=read(field.Item2,before.Length);
                if(now==null||now.Length!=before.Length)throw new InvalidOperationException("command-table-short-copy");
                for(int i=0;i<before.Length;i++)if(before[i]!=now[i])throw new InvalidOperationException("command-table-changed:"+field.Item1);
            }
        }
        private Input CompleteCore(Input input,bool originalCalled)
        {
            if(input==null||!ReferenceEquals(input,pending))return null;
            pending=null;long postStarted=Stopwatch.GetTimestamp();
            try
            {
                if((!originalCalled&&!input.SnapshotOnly)||input.Session!=session||input.Thread!=Thread.CurrentThread.ManagedThreadId||!ReferenceEquals(input.Token,token()))throw new InvalidOperationException("command-post-suppressed-or-context-changed");
                if(Int(0x7cc6720+input.Tribe*0x688+0x34)!=unchecked((int)input.TribeGlobal)||Int(0x64ccbb0+input.Building*0x32c+0x134)!=unchecked((int)input.BuildingGlobal))throw new InvalidOperationException("command-post-identity");
                CheckOwnTables(input);input.Sections.Add("post/players9",read(0x379ae00,9*0x583c));
                input.Sections.Add("post/unitManagerFull",read(0x67e8400,BridgePlanningCapture.EntryUnitBytes));input.Sections.Add("post/tribesFull",read(0x7cc6720,BridgePlanningCapture.EntryTribeBytes));
                input.Sections.Add("post/tribe",read(0x7cc6720+input.Tribe*0x688,0x688));input.Sections.Add("post/approaches501",read(0x60ad660+0x1b344,501*12));input.Sections.Add("post/workControls",read(0x60ad660,0xe0));
                foreach(var key in new List<string>(input.Sections.Keys))if(key.StartsWith("pre/unit-",StringComparison.Ordinal)){int id=int.Parse(key.Substring(9),System.Globalization.CultureInfo.InvariantCulture);input.Sections.Add("post/unit-"+id,read(0x67e8400+id*0x490,0xb00));if(Global(input.Sections[key])!=Global(input.Sections["post/unit-"+id]))throw new InvalidOperationException("command-post-unit-reused");}
                input.Sections.Add("post/topology",read(0x60ad660+0x6c,12));
                input.PostClock=Stopwatch.GetTimestamp();input.Complete=true;retained=input;emit("structure-command-complete","op="+input.Op+",building="+input.Building+"/g"+input.BuildingGlobal+",originalCalled="+originalCalled+",timing="+input.Origin+",freshDecisionProof=False,postCopyMs="+((input.PostClock-postStarted)*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",actualAttack=not-observed,selectorResult=not-directly-observed");return input;
            }
            catch(Exception error){emit("structure-command-incomplete","op="+input.Op+",reason="+error.Message);return null;}
        }
    }
}

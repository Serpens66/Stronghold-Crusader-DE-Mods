using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace EnemyBridgePathTest
{
    // Hash-validated reader is supplied by the existing native owner. No writes,
    // searches, retained pointers, events or independent hook ownership.
    internal sealed class BridgePlanningCapture
    {
        internal sealed class Bundle
        {
            internal long Session,Op,Parent,Root,Clock,Ticks,ConsumerTicks;
            internal int Attacker,Target,Mode,Bank,Thread,Revision,Dirty;
            internal int ConsumerVersion=2;
            internal bool NegativeEligible => false; // Capture completeness never authorizes the dormant policy.
            internal string Reason="pending",SelectionReason="legacy-unspecified";
            internal readonly Dictionary<string,byte[]> Sections=new Dictionary<string,byte[]>();
            internal readonly Dictionary<string,string> References=new Dictionary<string,string>();
            internal readonly List<int> Stages=new List<int>();
            internal bool Complete;
            internal IEnumerable<byte[]> Serialize(bool transientBuffers=false)
            {
                foreach(var pair in Sections)foreach(var block in BridgeInputArtifact.Section("plan/"+pair.Key,pair.Value,1,transientBuffers))yield return block;
                foreach(var pair in References)foreach(var block in BridgeInputArtifact.Section("plan/ref/"+pair.Key,System.Text.Encoding.UTF8.GetBytes(pair.Value),1,transientBuffers))yield return block;
            }
            internal string Metadata => "planningSession="+Session+"\nplanningBundleOp="+Op+"\nplanningParent="+Parent+"\nplanningBundleRoot="+Root+"\nplanningCaptureClock="+Clock+"\nplanningRevision="+Revision+"\nplanningDirty="+Dirty+"\nplanningNegativeEligible="+NegativeEligible+"\nplanningComplete="+Complete+"\nplanningSelectionReason="+SelectionReason+"\nplanningReason="+Reason+"\nplanningAttacker="+Attacker+"\nplanningTarget="+Target+"\nplanningConsumerCaptureVersion="+(Sections.ContainsKey("consumer/pre/identity")?ConsumerVersion:0)+"\nplanningMode="+Mode+"\nplanningBank="+Bank+"\nplanningThread="+Thread+"\nplanningConsumerCopyMs="+(ConsumerTicks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"\nplanningCaptureMs="+(Ticks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        }
        private const int N=320800,Root=0x60AD660;
        private readonly Func<int,int,byte[]> read;
        private readonly Func<object> token;
        private readonly Action<Bundle> deliver;
        private readonly Action<string,string> emit;
        private readonly int moduleLength;
        private readonly bool requireMilitaryRoot;
        private readonly Func<Bundle,bool> prepare;
        private readonly Func<int,string> relevance;
        private readonly Func<int[]> effectiveCounts;
        private string selectionReason="not-selected";
        internal void ReleaseForStrongerAttempt(){selected=false;lastReason="weak-bundle-retained-awaiting-stronger-relevance";}
        private Bundle current;
        private Bundle ready;
        private readonly Dictionary<long,int[]> targetFrames=new Dictionary<long,int[]>();
        private object identity,consumerIdentity;
        private int consumerCount;private string consumerPrefix="consumer";
        private long consumerOp,militaryEntryOp;private byte[] militaryEntry;
        private Dictionary<string,byte[]> militaryInputs;
        internal const int EntryUnitBytes=0x65c+10000*0x490,EntryTribeBytes=0x2a+4500*0x688;
        private int entryAttempts;private long entryCopyTicks;
        private long session;
        private bool selected;
        private int attempts;
        internal long ActiveFamily => current?.Op??0;
        internal bool HasOwnFrame(long run,long root,int player){var b=current??ready;return b!=null&&b.Session==run&&b.Root==root&&b.Attacker==player&&root>0;}
        private NativePathfindingTableCopy tablesAtStart;
        private long skipped,rejected,completedBundles,lastTicks,effectiveCountTicks,effectiveCountReads;
        private string lastReason="awaiting-fresh-relevant-planning";
        private string terminalReason="none",entryReason="not-observed";
        private long entrySkipped;
        // The only phase-5 paths before this test retain phase 5 or leave for
        // 7/8, which cannot call 2D250. 3CF20/126020 and 2FC80 do not write
        // E78C. The signed increment wraps exactly as the native word INC.
        internal static bool Phase5PlanningDue(short before) => unchecked((short)(before+1))>3;
        internal static int Phase5Strength(int age,int infantry,int a,int b,int c,int d,int e,int f,int reserve)
        {
            int total=unchecked((age<13?infantry:infantry/8)+a+b+c+d+e+f);
            return total>1&&(total>4||reserve/2<=unchecked(total*2))?0:1;
        }
        private bool? LeaderPresent(int player)
        {
            if(player<0||player>8)return null;
            int id=BitConverter.ToInt16(Read(0x3A0F9F0+player*2,2),0);
            if(id==0)return false;if(id<1||id>9999)return null;
            int at=0x67E8400+id*0x490;
            return Int(at+0x6f0)==Int(0x3A0FA04+player*4)&&(short)Int(at+0x6e4)==2&&(short)Int(at+0x8f8)==0;
        }
        private bool EntryMayPlan(int player)
        {
            int stride=player*0x583c,phase=Int(0x379D974+stride);
            if(!IsDecisionCapturePhase(phase)){entryReason="entry-phase-not-supported";return false;}
            if(Int(0x379D0D0+stride)==0){entryReason="entry-lord-disabled";return false;}
            if(phase==5&&!Phase5PlanningDue(BitConverter.ToInt16(Read(0x379E78C+stride,2),0))){entryReason="phase5-signed-timer-wait";return false;}
            if(phase==5)
            {
                if(Int(0x379E61C+stride)==2)
                {
                    bool? leader=LeaderPresent(Int(0x379E6B4+stride));
                    if(leader!=false){entryReason=leader==true?"phase5-special-leader-return":"Unknown:phase5-special-leader-identity";return false;}
                }
                int age=Int(0x379E740+stride);
                if(Int(0x379E6C0+stride)>0||age>199||Phase5Strength(age,Int(0x379D994+stride),Int(0x379D990+stride),Int(0x379D984+stride),Int(0x379D980+stride),Int(0x379D9A0+stride),Int(0x379D99C+stride),Int(0x379D998+stride),Int(0x379D97C+stride))!=0){entryReason="phase5-early-phase8-return";return false;}
                bool? target=LeaderPresent(Int(0x379D9A8+stride));
                if(target!=true){entryReason=target==false?"phase5-target-lord-phase7-return":"Unknown:phase5-target-lord-identity";return false;}
            }
            // Phase 4 may reset its timer after the engineer scan: the old
            // timer is deliberately not treated as a proven early return.
            entryReason=phase==5?"phase5-timer-due-other-branches-observed-by-root":"phase4-potential-engineer-dependent";
            return true;
        }
        internal string Status => "planningEffectiveCountReads="+effectiveCountReads+",planningEffectiveCountCopyMs="+(effectiveCountTicks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",planningCaptureAttempts="+attempts+",militaryEntryAttempts="+entryAttempts+",militaryEntryCopyMs="+(entryCopyTicks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",planningBundles="+completedBundles+",planningCaptureRejected="+rejected+",planningCaptureSkipped="+skipped+",planningCapturePending="+(current!=null||ready!=null)+",planningCaptureReason="+lastReason+",planningTerminalReason="+terminalReason+",militaryEntrySkipped="+entrySkipped+",militaryEntrySelectionReason="+entryReason+",planningSelectionReason="+selectionReason+",planningBudgetExhausted="+((attempts>=2||entryAttempts>=2)&&!selected)+",missingSuitablePlanning="+(completedBundles==0)+",planningCopyMs="+(lastTicks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        internal void PublicationRejected(long op,string reason){selected=false;lastReason=terminalReason=reason;emit("planning-artifact-retry","op="+op+",reason="+reason+",attempts="+attempts+",attemptLimit=2");}
        internal BridgePlanningCapture(int length,Func<int,int,byte[]> read,Func<object> token,Action<Bundle> deliver,Action<string,string> emit,bool requireMilitaryRoot=false,Func<Bundle,bool> prepare=null,Func<int,string> relevance=null,Func<int[]> effectiveCounts=null)
        {moduleLength=length;this.read=read;this.token=token;this.deliver=deliver;this.emit=emit;this.requireMilitaryRoot=requireMilitaryRoot;this.prepare=prepare;this.relevance=relevance;this.effectiveCounts=effectiveCounts;}
        private byte[] Read(int at,int bytes)
        {if(at<0||bytes<0||(long)at+bytes>moduleLength)throw new InvalidOperationException("planning-read-outside-module");byte[] b=read(at,bytes);if(b==null||b.Length!=bytes)throw new InvalidOperationException("short-planning-copy");return b;}
        private int Int(int at)=>BitConverter.ToInt32(Read(at,4),0);
        internal static bool IsDecisionCapturePhase(int phase)=>phase==4||phase==5;
        internal void Begin(long value){if(current!=null)Reject("map-changed-during-planning");current=ready=null;targetFrames.Clear();identity=consumerIdentity=null;consumerOp=militaryEntryOp=0;consumerCount=0;consumerPrefix="consumer";militaryEntry=null;militaryInputs=null;session=value;selectionReason="not-selected";selected=false;attempts=entryAttempts=0;entrySkipped=0;entryReason="not-observed";terminalReason="none";entryCopyTicks=0;skipped=rejected=completedBundles=lastTicks=0;lastReason="awaiting-fresh-relevant-planning";effectiveCountTicks=effectiveCountReads=0;emit("planning-capture-ready","attacker=automatic,attemptLimit=2,planningArtifactLimit=1,groupArtifactLimit=1,requires=stable-map-and-military-parent,dirtyCaptureAllowed=True,requiredEntryPhases=4/5,entryInputVersion=1,entryContinuationVersion=1,ownLineModeAndBlockGrid=True,ownFormationCounter=True,entryAttemptLimit=2,fullUnitAndTribeManagers=True,negativePolicyRequiresCompletedTopology=True,behavior=unchanged");}
        internal void End(){if(current!=null)Reject("map-ended-during-planning");if(ready!=null){emit("planning-capture-incomplete","op="+ready.Op+",reason=map-ended-before-military-root-completed");ready=null;targetFrames.Clear();}emit("planning-capture-end",Status+",historicalMissingValues=unknown,behavior=unchanged");session=0;militaryInputs=null;militaryEntry=null;militaryEntryOp=0;}
        private void Reject(string reason)
        {if(current==null)return;lastReason=terminalReason=reason;rejected++;lastTicks=current.Ticks;current.Reason=reason;current.Complete=false;emit("planning-capture-incomplete","op="+current.Op+",reason="+reason+",behavior=unchanged");current=null;identity=null;}
        private void Store(string name,byte[] bytes)
        {
            foreach(var pair in current.Sections)
            {
                if(pair.Value.Length!=bytes.Length)continue;bool equal=true;
                for(int i=0;i<bytes.Length;i++)if(pair.Value[i]!=bytes[i]){equal=false;break;}
                if(equal){current.References.Add(name,pair.Key);return;}
            }
            current.Sections.Add(name,bytes);
        }
        private void Copy(string name,int at,int bytes)=>Store(name,Read(at,bytes));
        internal void Observe(int rva,bool post,long run,long op,long parent,int attacker,int a,int b,int c,int d,bool completed,long root=0,bool suitable=true)
        {
            if(rva==0x10DF60||rva==0x115B10){ObserveConsumerChild(rva,post,run,op,parent,a,b,c,completed,root);return;}
            if(rva==0x2C480){ObserveConsumer(post,run,op,parent,a,completed,root);return;}
            if(rva==0x3C2E0&&!post&&run==session&&a>=1&&a<=8&&!selected&&attempts<2)
            {
                if(current!=null||ready!=null){emit("planning-military-entry-incomplete","op="+op+",reason=nested-military-entry-preserved-outer");return;}
                long entryStart=0;
                try
                {
                    militaryEntry=null;militaryInputs=null;militaryEntryOp=0;
                    long entryClock=Stopwatch.GetTimestamp();
                    if(!string.IsNullOrEmpty(selectionReason=relevance==null?"fixture-unrestricted":relevance(a))&&EntryMayPlan(a))
                    {
                        if(entryAttempts>=2){emit("planning-military-entry-incomplete","op="+op+",reason=entry-attempt-limit,attemptLimit=2");return;}
                        entryAttempts++;entryStart=Stopwatch.GetTimestamp();object ownToken=token();if(ownToken==null)throw new InvalidOperationException("military-missing-map-identity");
                        militaryEntry=Read(0x379AE00+a*0x583c,0x583c);
                        var copied=new Dictionary<string,byte[]>();
                        // Writable native tables, captured at this own entry, not
                        // supplied from a later consumer or the PE's defaults.
                        copied.Add("military/entryLeaderIds9",Read(0x3A0F9F0,18));
                        copied.Add("military/entryLeaderGlobals9",Read(0x3A0FA04,36));
                        copied.Add("military/entryUpdateClasses11",Read(0x2C8040,88));
                        copied.Add("military/entryMoveClasses11",Read(0x2C80E0,88));
                        int lord=BitConverter.ToInt32(militaryEntry,0x379D0D0-0x379AE00);
                        if(lord<0)throw new InvalidOperationException("military-negative-lord");
                        var config=new int[]{lord,0,0,0,Int(0x3665FBC)};
                        if(lord!=0)
                        {
                            int record=checked(0x366C210+lord*0x5e4);
                            config[1]=Int(checked(record-0x3d8));
                            config[2]=Int(checked(record-0x2f8));
                            config[3]=Read(checked(record-0x2f0),1)[0];
                        }
                        var controls=new byte[20];Buffer.BlockCopy(config,0,controls,0,20);
                        copied.Add("military/entryConfig5",controls);
                        copied.Add("military/entryLordConfigRow",Read(checked(0x366C210+(lord-1)*0x5e4),0x5e4));
                        copied.Add("military/entryPhase5Controls",Read(0x3665fb8,8));
                        long[] context={run,op,entryClock,Thread.CurrentThread.ManagedThreadId};
                        var bytes=new byte[32];Buffer.BlockCopy(context,0,bytes,0,32);
                        copied.Add("military/entryIdentity4",bytes);
                        copied.Add("military/entryInputVersion",BitConverter.GetBytes(1));
                        // Audited F3060/F32B0/DAAC0 and11B520 inputs. The latter
                        // counter lies beyond the copied4500 tribe-slot view.
                        copied.Add("military/entryContinuationVersion",BitConverter.GetBytes(1));
                        copied.Add("military/entryLineMode4",Read(0x2f7a690,4));
                        copied.Add("military/entryBlockGrid",Read(0x51d75f0,N));
                        copied.Add("military/entryFormationCounter4",Read(0x7cc6720+0x736b10,4));
                        copied.Add("military/entryUnitManagerFull",Read(0x67E8400,EntryUnitBytes));
                        copied.Add("military/entryTribesFull",Read(0x7CC6720,EntryTribeBytes));
                        copied.Add("military/entryPlayers9",Read(0x379AE00,9*0x583c));
                        copied.Add("military/entryQuota",Read(checked(0x366C210+lord*0x5e4-0x380),4));
                        var counters=new byte[8];Buffer.BlockCopy(Read(checked(0x2EB2CCC+a*0x177bc),4),0,counters,0,4);Buffer.BlockCopy(Read(checked(0x2EBAA04+a*0x177bc),4),0,counters,4,4);
                        copied.Add("military/entryEngineerCounters2",counters);
                        // All 9999 slots are used by 3FE50/2A6B0; high-water is
                        // not a coverage bound. Tribe bitsets supply 119F90/126020.
                        // Verify this own entry before Vanilla is allowed to mutate it.
                        if(!Same(copied["military/entryUnitManagerFull"],Read(0x67E8400,EntryUnitBytes))||
                           !Same(copied["military/entryTribesFull"],Read(0x7CC6720,EntryTribeBytes))||
                           !Same(militaryEntry,Read(0x379AE00+a*0x583c,0x583c))||
                           !Same(copied["military/entryPlayers9"],Read(0x379AE00,9*0x583c))||
                           !Same(copied["military/entryQuota"],Read(checked(0x366C210+lord*0x5e4-0x380),4))||
                           !Equals(ownToken,token()))throw new InvalidOperationException("military-own-entry-changed-during-copy");
                        foreach(var table in new[]{Tuple.Create("military/entryLeaderIds9",0x3A0F9F0),Tuple.Create("military/entryLeaderGlobals9",0x3A0FA04),Tuple.Create("military/entryUpdateClasses11",0x2C8040),Tuple.Create("military/entryMoveClasses11",0x2C80E0)})
                            if(!Same(copied[table.Item1],Read(table.Item2,copied[table.Item1].Length)))throw new InvalidOperationException("military-own-entry-table-changed");
                        if(lord!=0&&(config[1]!=Int(checked(0x366C210+lord*0x5e4-0x3d8))||config[2]!=Int(checked(0x366C210+lord*0x5e4-0x2f8))||config[3]!=Read(checked(0x366C210+lord*0x5e4-0x2f0),1)[0])||config[4]!=Int(0x3665FBC)||BitConverter.ToInt32(counters,0)!=Int(checked(0x2EB2CCC+a*0x177bc))||BitConverter.ToInt32(counters,4)!=Int(checked(0x2EBAA04+a*0x177bc)))throw new InvalidOperationException("military-own-entry-controls-changed");
                        if(!Same(copied["military/entryLordConfigRow"],Read(checked(0x366C210+(lord-1)*0x5e4),0x5e4))||!Same(copied["military/entryPhase5Controls"],Read(0x3665fb8,8)))throw new InvalidOperationException("military-own-entry-phase5-controls-changed");                        foreach(var value in new[]{Tuple.Create("military/entryLineMode4",0x2f7a690),Tuple.Create("military/entryBlockGrid",0x51d75f0),Tuple.Create("military/entryFormationCounter4",0x7cc6720+0x736b10)})
                            if(!Same(copied[value.Item1],Read(value.Item2,copied[value.Item1].Length)))throw new InvalidOperationException("military-own-continuation-input-changed:"+value.Item1);
                        militaryInputs=copied;militaryEntryOp=op;
                    }
                    else entrySkipped++;
                }
                catch(Exception error){emit("planning-military-entry-incomplete","op="+op+",reason="+error.Message);militaryEntry=null;militaryInputs=null;militaryEntryOp=0;}
                finally {if(entryStart!=0)entryCopyTicks+=Stopwatch.GetTimestamp()-entryStart;}
                return;
            }
            if(rva==0x2C5A0||rva==0x3C2E0){ObserveSelection(rva,post,run,op,parent,a,b,completed,root);return;}
            if(rva!=0x2D250&&rva!=0xD95E0&&rva!=0xD9190)return;
            long start=Stopwatch.GetTimestamp();bool attemptStarted=false;
            try
            {
                if(rva==0x2D250&&!post)
                {
                    if(current!=null){Reject("nested-planning-family");return;}
                    if(selected||attempts>=2||run!=session||attacker<1||attacker>8)return;
                    if(relevance!=null&&string.IsNullOrEmpty(selectionReason=relevance(attacker))){skipped++;lastReason="no-observed-bridge-relevance";return;}
                    if(!suitable){skipped++;lastReason="preparatory-phase-not-decision-capture-target";return;}
                    if(requireMilitaryRoot&&root==0){skipped++;lastReason="unresolved-military-parent";if(skipped==1)emit("planning-capture-skipped","op="+op+",reason="+lastReason);return;}
                    if(requireMilitaryRoot&&(root!=militaryEntryOp||militaryEntry==null||militaryInputs==null)){skipped++;lastReason="missing-own-military-entry-at-family";if(skipped<=2)emit("planning-capture-skipped","op="+op+",root="+root+",reason="+lastReason+",entryReason="+entryReason+",attemptConsumed=False");return;}
                    if(token()==null){skipped++;lastReason="missing-map-identity-at-decision";if(skipped==1)emit("planning-capture-skipped","op="+op+",reason="+lastReason);return;}
                    attempts++;attemptStarted=true;int target=Int(0x379D9A8+attacker*0x583c);
                    if(target<1||target>8){lastReason="unresolved-target-player";rejected++;emit("planning-capture-incomplete","op="+op+",reason=unresolved-target-player");return;}
                    identity=token();current=new Bundle {Session=run,Op=op,Parent=parent,Root=root,Attacker=attacker,Target=target,Mode=b,Bank=target,Clock=start,Thread=Thread.CurrentThread.ManagedThreadId,Revision=Int(Root+0x74),Dirty=Int(Root+0x6c),SelectionReason=selectionReason};
                    if(root!=0&&root==militaryEntryOp&&militaryEntry!=null)
                    {
                        current.Sections.Add("military/entryPlayer",(byte[])militaryEntry.Clone());
                        if(militaryInputs!=null)foreach(var pair in militaryInputs)current.Sections.Add(pair.Key,(byte[])pair.Value.Clone());
                    }
                    // These are decision-time physical inputs. Consumer/pre
                    // heights remain separate observations and may differ.
                    Copy("callerHeight",0x4DDD350,N);Copy("callerBaseHeight",0x4E2B870,N);
                    Copy("flags",0x48F71B0,N*4);Copy("components",0x50EC690,N*2);Copy("edges",0x51890D0,N);
                    Copy("directionOffsets",0x405EDB0,6400*4);Copy("rowRecords",0x402FF2C,800*12);Copy("tileRows",0x3AAE2A4,N*2);
                    Copy("buildingIds",0x4B6AA50,N*2);int count=Int(0x64CCBB0+0x50);
                    if(count<1||count>4001)throw new InvalidOperationException("building-high-water-invalid");
                    Copy("buildingUpdaterControls",0x64CCBB0+0x319874,8);Copy("buildingUpdaterSlots",0x64CCBB0+0x32c,3999*0x32c);
                    Copy("buildingCount",0x64CCBB0+0x50,4);Copy("buildingRecords",0x64CCBB0+0x32c,(count-1)*0x32c);
                    var gateConnections=new byte[(count-1)*2];for(int id=1;id<count;id++){byte[] value=Read(0x64CCBB0+id*0x32c+0x32e,2);Buffer.BlockCopy(value,0,gateConnections,(id-1)*2,2);}Store("gateConnectionIds",gateConnections);
                    Copy("nativeAlliances9",0x37EDF3C,9*4);
                    var scalar=new int[9*7];for(int p=0;p<=8;p++){int at=p*7;scalar[at]=Int(0x379AE64+p*0x583c);scalar[at+1]=Int(0x379AFA8+p*0x583c);scalar[at+2]=Int(0x379AFAC+p*0x583c);scalar[at+3]=Int(0x379AFB0+p*0x583c);scalar[at+4]=Int(0x379D9A8+p*0x583c);scalar[at+5]=Int(0x379E768+p*0x583c);scalar[at+6]=Int(0x379D968+p*0x583c);}
                    var scalars=new byte[scalar.Length*4];Buffer.BlockCopy(scalar,0,scalars,0,scalars.Length);Store("playerScalars7",scalars);
                    Copy("coarseRecords",0x35057AF,25600*48);
                    NativePathfindingTableCopy tables;string reason;
                    if(!NativePathfindingTableCopy.TryCapture(NativePathfindingTableCopy.Hash,moduleLength,Int,out tables,out reason))throw new InvalidOperationException(reason);
                    tablesAtStart=tables;StoreInts("profiles90",tables.CopyProfiles());StoreInts("permissions540",tables.CopyPermissions());
                    Copy("componentCounts1000",Root+0xe0,4000);
                    if(prepare!=null&&!prepare(current))throw new InvalidOperationException("decision-input-copy-incomplete");
                    Stage("caller-pre");return;
                }
                if(current==null)return;
                if(run!=current.Session||!ReferenceEquals(identity,token())||Thread.CurrentThread.ManagedThreadId!=current.Thread||Int(Root+0x74)!=current.Revision||Int(Root+0x6c)!=current.Dirty)
                {Reject("session-thread-identity-or-topology-changed");return;}
                if(rva==0x2D250&&post&&op==current.Op)
                {
                    if(!completed||current.Stages.Count!=4||current.Stages[0]!=0||current.Stages[1]!=1||current.Stages[2]!=2||current.Stages[3]!=3){Reject("missing-or-failed-native-stage");return;}
                    NativePathfindingTableCopy tablesNow;string tableReason;
                    if(!NativePathfindingTableCopy.TryCapture(NativePathfindingTableCopy.Hash,moduleLength,Int,out tablesNow,out tableReason)||!tablesAtStart.SameContent(tablesNow)){Reject("permission-tables-changed");return;}
                    if(!Same(current.Sections["nativeAlliances9"],Read(0x37EDF3C,36))){Reject("native-alliance-table-changed");return;}
                    foreach(string gridName in new[]{"flags","components","edges"})
                    {
                        int at=gridName=="flags"?0x48F71B0:gridName=="components"?0x50EC690:0x51890D0;
                        if(!Same(current.Sections[gridName],Read(at,current.Sections[gridName].Length))){Reject("physical-input-changed-during-planning");return;}
                    }
                    byte[] buildings=current.Sections["buildingRecords"],now=Read(0x64CCBB0+0x32c,buildings.Length);
                    for(int i=0;i<buildings.Length;i++)if(i%0x32c!=0x128&&i%0x32c!=0x129&&buildings[i]!=now[i]){Reject("building-identity-or-state-changed");return;}
                    Stage("caller-post");current.Ticks+=Stopwatch.GetTimestamp()-start;current.Complete=true;current.Reason="four-stages-copied";
                    Bundle done=current;current=null;identity=null;selected=true;lastTicks=done.Ticks;lastReason=done.Reason;
                    if(done.Root==0){completedBundles++;deliver(done);}else {ready=done;consumerCount=0;consumerPrefix="consumer";}return;
                }
                if(parent!=current.Op)return;
                int stage=rva==0xD95E0?(post?1:0):(post?3:2);
                if(post&&!completed){Reject("native-stage-original-failed");return;}
                if(stage!=current.Stages.Count){Reject("unexpected-native-stage-order");return;}
                if(rva==0xD95E0&&a!=current.Target){Reject("seed-target-mismatch");return;}
                string name=stage<2?"seed":"distance";name+=post?"-post":"-pre";
                var args=new int[]{a,b,c,d};var argBytes=new byte[16];Buffer.BlockCopy(args,0,argBytes,0,16);Store(name+"/arguments4",argBytes);
                if(rva==0xD9190&&d!=current.Attacker){Reject("distance-attacker-mismatch");return;}
                Stage(name);current.Stages.Add(stage);
            }
            catch(Exception error)
            {
                string reason="capture-error:"+error.GetType().Name+":"+error.Message;
                if(current!=null)Reject(reason);else {if(rva==0x2D250&&!post&&!attemptStarted)attempts++;rejected++;lastReason=reason;emit("planning-capture-incomplete","op="+op+",reason="+reason+",behavior=unchanged");}
            }
            finally{if(current!=null)current.Ticks+=Stopwatch.GetTimestamp()-start;}
        }
        internal void Access(long run,long root,long op,int mode,int native,int effective,bool regionObserved,long result,int[] inputs)
        {
            var b=current??ready;if(b==null||b.Session!=run||b.Root!=root)return;
            var values=new long[8+inputs.Length];values[0]=run;values[1]=root;values[2]=op;values[3]=mode;values[4]=native;values[5]=effective;values[6]=regionObserved?1:0;values[7]=result;for(int i=0;i<inputs.Length;i++)values[8+i]=inputs[i];
            var bytes=new byte[values.Length*8];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);b.Sections["access/"+op+"/observed"] = bytes;
        }
        internal void Outcome(long run,long root,int[] entry,int[] after)
        {
            var b=ready;if(b==null||b.Session!=run||b.Root!=root)return;
            var values=new int[entry.Length+after.Length];Array.Copy(entry,values,entry.Length);Array.Copy(after,0,values,entry.Length,after.Length);var bytes=new byte[values.Length*4];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);b.Sections["military/consumedPlan"] = bytes;
        }
        private void ConsumerCopy(string stage,string name,int address,int length)
        {
            byte[] bytes=Read(address,length);
            // Reuse exact own definitions only; no stale runtime publication.
            foreach(var pair in ready.Sections)if(pair.Value.Length==bytes.Length&&Same(pair.Value,bytes)){ready.References.Add(stage+"/"+name,pair.Key);return;}
            ready.Sections.Add(stage+"/"+name,bytes);
        }
        private void ObserveConsumerChild(int rva,bool post,long run,long op,long parent,int a,int b,int c,bool completed,long root)
        {
            if(ready==null||ready.Session!=run||ready.Root!=root||consumerOp==0||parent!=consumerOp)return;
            long start=Stopwatch.GetTimestamp();
            try
            {
                if(!completed||Thread.CurrentThread.ManagedThreadId!=ready.Thread||!ReferenceEquals(consumerIdentity,token()))throw new InvalidOperationException("consumer-child-boundary");
                if(rva==0x10DF60?(a!=ready.Attacker||b!=ready.Target):(a!=ready.Target||b!=ready.Attacker))throw new InvalidOperationException("consumer-child-players");
                string stage=consumerPrefix+"/"+(rva==0x10DF60?"build":"weight")+(post?"-post":"-pre");
                var context=new long[]{run,root,op,parent,a,b,c,Stopwatch.GetTimestamp(),Int(Root+0x6c),Int(Root+0x74)};var bytes=new byte[context.Length*8];Buffer.BlockCopy(context,0,bytes,0,bytes.Length);ready.Sections.Add(stage+"/identity",bytes);
                ConsumerCopy(stage,"candidates",VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes);ConsumerCopy(stage,"selectionMask",0x53AD4B0,N);ConsumerCopy(stage,"seeds",0x535EF90,N);
                if(rva==0x115B10){var modes=new byte[8];Buffer.BlockCopy(Read(0x3665f10,4),0,modes,0,4);Buffer.BlockCopy(Read(0x3665f28,4),0,modes,4,4);ready.Sections.Add(stage+"/gameModeValues",modes);}
            }
            catch(Exception error){lastReason=terminalReason="consumer-incomplete:"+error.Message;emit("planning-consumer-incomplete","op="+op+",reason="+error.Message);ready=null;selected=false;rejected++;targetFrames.Clear();consumerOp=0;consumerIdentity=null;}
            finally{if(ready!=null){long elapsed=Stopwatch.GetTimestamp()-start;ready.Ticks+=elapsed;ready.ConsumerTicks+=elapsed;}}
        }
        private void ObserveConsumer(bool post,long run,long op,long parent,int player,bool completed,long root)
        {
            if(ready==null||ready.Session!=run||ready.Root!=root||ready.Attacker!=player)return;
            long start=Stopwatch.GetTimestamp();
            try
            {
                if(Thread.CurrentThread.ManagedThreadId!=ready.Thread||token()==null)throw new InvalidOperationException("consumer-map-or-thread");
                if(!post)
                {
                    if(consumerOp!=0)throw new InvalidOperationException("nested-consumer");
                    if(consumerCount>=2)throw new InvalidOperationException("consumer-coverage-limit-two");
                    consumerPrefix=consumerCount==0?"consumer":"consumer2";
                    if(Int(0x379D9A8+player*0x583c)!=ready.Target)throw new InvalidOperationException("consumer-target-changed");
                    consumerOp=op;consumerIdentity=token();ready.Sections.Add(consumerPrefix+"/projectionCoverage4",new byte[4]);
                }
                else if(!completed||consumerOp!=op||!ReferenceEquals(consumerIdentity,token()))throw new InvalidOperationException("consumer-frame-or-map-changed");
                if(post&&BitConverter.ToInt32(ready.Sections[consumerPrefix+"/projectionCoverage4"],0)==0)ready.Sections[consumerPrefix+"/projectionCoverage4"]=BitConverter.GetBytes(2);
                string stage=consumerPrefix+(post?"/post":"/pre");
                if(ready.Sections.ContainsKey("military/entryContinuationVersion")){ConsumerCopy(stage,"commandLineMode4",0x2f7a690,4);ConsumerCopy(stage,"commandBlockGrid",0x51d75f0,N);}
                long[] context={run,root,op,parent,player,ready.Target,Stopwatch.GetTimestamp(),Int(Root+0x6c),Int(Root+0x74)};var identityBytes=new byte[context.Length*8];Buffer.BlockCopy(context,0,identityBytes,0,identityBytes.Length);ready.Sections.Add(stage+"/identity",identityBytes);
                ConsumerCopy(stage,"candidates",VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes);
                ConsumerCopy(stage,"players",0x379AE00,9*0x583c);
                ConsumerCopy(stage,"selectionMask",0x53AD4B0,N);
                ConsumerCopy(stage,"seeds",0x535EF90,N);
                ConsumerCopy(stage,"workControls",Root,0xe0);ConsumerCopy(stage,"workQueueControls",Root+0x155f38,0x34);ConsumerCopy(stage,"workQueue",Root+0x155f6c,N*4);ConsumerCopy(stage,"workQueueRows",Root+0x28f3ec,N*2);ConsumerCopy(stage,"workQueueX",Root+0x32be2c,N*2);ConsumerCopy(stage,"workDistances",0x5225b10,N*2);ConsumerCopy(stage,"workVisits",0x52c2550,N*2);ConsumerCopy(stage,"workTargets",0x37f1edc,9*50*16);
                // Writable .data tables: DLL initial bytes are not historical
                // runtime inputs. Capture at this consumer's own boundaries.
                foreach(var table in new[]{Tuple.Create("nativeWorkDirections64",0x2d2e50,64),Tuple.Create("nativeDirectionMasks8",0x312620,8),Tuple.Create("nativeWorkBuildingClasses336",0x2e68d0,336*4)})
                {
                    ConsumerCopy(stage,table.Item1,table.Item2,table.Item3);
                    if(post&&!Same(CopiedPlanningBundle.Resolve(ready,consumerPrefix+"/pre/"+table.Item1),CopiedPlanningBundle.Resolve(ready,stage+"/"+table.Item1)))
                        throw new InvalidOperationException("consumer-native-table-changed:"+table.Item1);
                }
                foreach(var field in new[]{Tuple.Create("approachRingOffsets",0x2e3fa0,0xb60),Tuple.Create("approachRingCounts14",0x2e8fd8,56),Tuple.Create("approachContactDirections8",0x3123b0,32),Tuple.Create("approachBuildingHeights336",0x2e5360,336*4)})
                {ConsumerCopy(stage,field.Item1,field.Item2,field.Item3);if(post&&!Same(CopiedPlanningBundle.Resolve(ready,consumerPrefix+"/pre/"+field.Item1),CopiedPlanningBundle.Resolve(ready,stage+"/"+field.Item1)))throw new InvalidOperationException("consumer-approach-table-changed:"+field.Item1);}
                if(!post)
                {
                    ConsumerCopy(stage,"buildingTransitionClasses",0x2e6710,336*4);ConsumerCopy(stage,"buildingSeedClasses",0x2e6c50,336*4);ConsumerCopy(stage,"packedValidity",0x3a11ea4,800*800);ConsumerCopy(stage,"combatClassMask",0x8574bcc,90);
                    ConsumerCopy(stage,"unitTiles",0x4C559B0,N*2);ConsumerCopy(stage,"height",0x4DDD350,N);ConsumerCopy(stage,"baseHeight",0x4E2B870,N);ConsumerCopy(stage,"terrainOwner",0x4E79D90,N);
                    int macroLimit=Int(Root);if(macroLimit<1||macroLimit>1000)throw new InvalidOperationException("consumer-macro-limit");ConsumerCopy(stage,"macroLimit",Root,4);ConsumerCopy(stage,"macroRecords",Root+0x2024+0x204,(macroLimit-1)*0x204);ConsumerCopy(stage,"nativeAlliances9",0x37EDF3C,36);
                    ConsumerCopy(stage,"flags",0x48F71B0,N*4);ConsumerCopy(stage,"components",0x50EC690,N*2);ConsumerCopy(stage,"edges",0x51890D0,N);ConsumerCopy(stage,"buildingIds",0x4B6AA50,N*2);
                    int limit=Int(0x67E8400);if(limit<1||limit>10000)throw new InvalidOperationException("consumer-unit-high-water");ConsumerCopy(stage,"unitManager",0x67E8400,checked(0x65c+limit*0x490));
                    int buildings=Int(0x64CCBB0+0x50);if(buildings<1||buildings>4001)throw new InvalidOperationException("consumer-building-high-water");ConsumerCopy(stage,"buildingCount",0x64CCBB0+0x50,4);ConsumerCopy(stage,"buildingRecords",0x64CCBB0+0x32c,(buildings-1)*0x32c);
                    // This exact global determines 10DF60's4/10 cost branch.
                    ConsumerCopy(stage,"costBranch",0x8574B90,1);
                    ConsumerCopy(stage,"structureConfig4",0x37ed4c4,4);ConsumerCopy(stage,"costConfig4",0x8574b90,4);
                    ConsumerCopy(stage,"approachOffsetScratch8",0x67e8380,8);ConsumerCopy(stage,"approaches501",Root+0x1b344,501*12);
                    ConsumerCopy(stage,"tribes",0x7CC6720,0x2a+4500*0x688);
                }
                else {consumerCount++;ready.Sections["consumerSequence/version"]=BitConverter.GetBytes(1);ready.Sections["consumerSequence/count"]=BitConverter.GetBytes(consumerCount);consumerOp=0;consumerIdentity=null;emit("planning-consumer-captured","planningOp="+ready.Op+",consumerOrdinal="+consumerCount+",consumerOp="+op+",root="+root+",player="+player+",inputTiming=own-consumer-pre,outputs=own-consumer-post,fullReplay=requires-audited-input-closure,behavior=unchanged");}
            }
            catch(Exception error){lastReason=terminalReason="consumer-incomplete:"+error.Message;emit("planning-consumer-incomplete","op="+op+",reason="+error.Message);ready=null;selected=false;rejected++;targetFrames.Clear();consumerOp=0;consumerIdentity=null;}
            finally{if(ready!=null){long elapsed=Stopwatch.GetTimestamp()-start;ready.Ticks+=elapsed;ready.ConsumerTicks+=elapsed;}}
        }
        private void ObserveSelection(int rva,bool post,long run,long op,long parent,int a,int b,bool completed,long root)
        {
            if(ready==null||run!=ready.Session)return;
            try
            {
                if(rva==0x3C2E0&&post&&op==ready.Root)
                {
                    if(!completed){emit("planning-capture-incomplete","op="+ready.Op+",reason=military-root-original-failed");ready=null;selected=false;rejected++;targetFrames.Clear();consumerOp=0;consumerIdentity=null;return;}
                    if(consumerOp!=0)throw new InvalidOperationException("unfinished-consumer-frame");
                    if(ready.Sections.ContainsKey("consumer/pre/identity"))
                    {
                        CopiedConsumerSequence.Validate(ready);
                        if(!ready.Sections.ContainsKey("military/entryPlayer"))throw new InvalidOperationException("missing-own-military-entry");
                    }
                    ready.Sections.Add("military/exitPlayer",Read(0x379AE00+ready.Attacker*0x583c,0x583c));
                    if(ready.Sections.ContainsKey("military/entryContinuationVersion")){ConsumerCopy("military","exitLineMode4",0x2f7a690,4);ConsumerCopy("military","exitBlockGrid",0x51d75f0,N);ConsumerCopy("military","exitFormationCounter4",0x7cc6720+0x736b10,4);}
                    emit("planning-consumer-coverage","op="+ready.Op+",root="+ready.Root+",hasConsumer="+ready.Sections.ContainsKey("consumer/post/identity")+",hasMilitaryEntry="+ready.Sections.ContainsKey("military/entryPlayer"));
                    if(targetFrames.Count!=0)throw new InvalidOperationException("unfinished-target-selection-frame");
                    Bundle done=ready;lastTicks=done.Ticks;ready=null;completedBundles++;emit("planning-target-coverage","op="+done.Op+",root="+done.Root+",targetRecords="+TargetRecordCount(done)+",missingTargetOutcome="+(TargetRecordCount(done)==0));deliver(done);return;
                }
                if(rva!=0x2C5A0||root!=ready.Root||a!=ready.Attacker)return;
                if(Thread.CurrentThread.ManagedThreadId!=ready.Thread)throw new InvalidOperationException("target-thread-mismatch");
                if(!post){if(consumerOp!=0&&parent==consumerOp)ready.Sections[consumerPrefix+"/projectionCoverage4"]=BitConverter.GetBytes(1);if(targetFrames.Count>=16||b<1||b>8)throw new InvalidOperationException("target-frame-limit-or-player");targetFrames.Add(op,new[]{a,b,Int(0x379D968+a*0x583c),Int(0x379AFB0+b*0x583c)});
                    if(effectiveCounts!=null)
                    {
                        byte[] raw=ReadEffectiveCounts();
                        ready.Sections.Add("target/"+op+"/effectiveCountsPre",raw);
                        ready.Sections.Add("target/"+op+"/nativeCountsPre",Read(Root+0xe0,1000*4));
                    }
                    return;}
                if(!targetFrames.TryGetValue(op,out int[] args)||args[0]!=a||args[1]!=b||!completed)throw new InvalidOperationException("target-frame-mismatch");
                targetFrames.Remove(op);
                if(effectiveCounts!=null)
                {
                    byte[] raw=ReadEffectiveCounts();
                    if(!Same(raw,ready.Sections["target/"+op+"/effectiveCountsPre"]))throw new InvalidOperationException("effective-count-table-changed-during-selection");
                    ready.Sections.Add("target/"+op+"/effectiveCountsPost",raw);
                    ready.Sections.Add("target/"+op+"/nativeCountsPost",Read(Root+0xe0,1000*4));
                }
                int region=Int(0x2E97D10);if(region<0||region>=1000)throw new InvalidOperationException("selected-region-outside-count-table");
                var values=new[]{a,b,Int(0x2E97EAC),Int(0x2E97EA8),region,Int(0x2E9CA14),Int(0x2E97D14),Int(Root+0xe0+region*4)};
                byte[] bytes=new byte[32];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);ready.Sections.Add("target/"+op+"/values8",bytes);
                bytes=new byte[16];Buffer.BlockCopy(args,0,bytes,0,bytes.Length);ready.Sections.Add("target/"+op+"/argumentsAndTiles4",bytes);
                var identityValues=new[]{run,op,parent,root,Stopwatch.GetTimestamp()};bytes=new byte[40];Buffer.BlockCopy(identityValues,0,bytes,0,bytes.Length);ready.Sections.Add("target/"+op+"/identity5",bytes);
            }
            catch(Exception error){lastReason=terminalReason="selection-incomplete:"+error.Message;emit("planning-target-incomplete","op="+op+",reason="+error.Message);targetFrames.Remove(op);ready=null;selected=false;rejected++;targetFrames.Clear();consumerOp=0;consumerIdentity=null;}
        }
        private byte[] ReadEffectiveCounts()
        {
            long start=Stopwatch.GetTimestamp();effectiveCountReads++;
            try{var counts=effectiveCounts();ValidateEffectiveCounts(counts);var raw=new byte[counts.Length*4];Buffer.BlockCopy(counts,0,raw,0,raw.Length);return raw;}
            finally{effectiveCountTicks+=Stopwatch.GetTimestamp()-start;}
        }
        internal static void ValidateEffectiveCounts(int[] values)
        {
            if(values==null||(values.Length!=1000&&values.Length!=4000))throw new InvalidOperationException("effective-count-capacity");
            for(int i=0;i<values.Length;i++)if(values[i]<0||values[i]>N)throw new InvalidOperationException("effective-count-value");
        }
        private static int TargetRecordCount(Bundle bundle){int count=0;foreach(string key in bundle.Sections.Keys)if(key.StartsWith("target/",StringComparison.Ordinal)&&key.EndsWith("/values8",StringComparison.Ordinal))count++;return count;}
        private void Stage(string name)
        {
            int limit=Int(Root);if(limit<1||limit>1000)throw new InvalidOperationException("macro-copy-limit");
            Copy(name+"/macroLimit",Root,4);Copy(name+"/macroRecords",Root+0x2024+0x204,(limit-1)*0x204);
            Copy(name+"/seeds",0x535EF90,N);Copy(name+"/visits",0x52C2550,N*2);
            Copy(name+"/distance",0x5759230+current.Bank*N*2,N*2);Copy(name+"/queue",Root+0x155f6c,N*4);Copy(name+"/queueRows",Root+0x28f3ec,N*2);
            Copy(name+"/controls",Root,0xe0);Copy(name+"/queueControls",Root+0x155f38,0x34);
            Copy(name+"/selectedBank",0x2EA70DC+current.Attacker*0x177bc,4);
            int count=Int(0x405B4F0);if(count<0||count>1000)throw new InvalidOperationException("candidate-count-invalid");
            Copy(name+"/candidateCount",0x405B4F0,4);Copy(name+"/candidates8",0x405B4F4,count*8);
            Copy(name+"/candidateCapacity8",0x405B4F4,1000*8);
        }
        private void StoreInts(string name,int[] values){var bytes=new byte[values.Length*4];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);Store(name,bytes);}
        private static bool Same(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
}

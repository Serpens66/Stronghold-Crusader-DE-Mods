using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class RescueReplanIntentFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private static int Tile(int x,int y)=>x>=0 && x<800 && y>=0 && y<800?y*800+x:-1;
        private sealed class Source : IWorkerResumeWorld
        {
            internal readonly byte[] Bytes=new byte[0xAEC];
            internal int X=4,Y=5,NextX=4,NextY=5,Flags=2,Substep=8;
            internal uint Health=100,Pending,TreeGlobal=777;
            internal bool Ordinary=true,BindingsAlive=true,CrewAlive=true,PoseReadable=true,ThrowCapture;
            internal int Captures,Mutations;
            internal readonly RescueReplanIntentWorld Boundary;
            internal ushort Word(int at)=>(ushort)(Bytes[at]|Bytes[at+1]<<8);
            internal uint Dword(int at)=>(uint)(Word(at)|(uint)Word(at+2)<<16);
            internal void Word(int at,uint value){Bytes[at]=(byte)value;Bytes[at+1]=(byte)(value>>8);}
            internal void Dword(int at,uint value){Word(at,value);Word(at+2,value>>16);}
            internal Source(eChimps type,int state)
            {
                Word(0x6E4,(uint)AliveState.IsAlive);Word(0x6E6,(uint)type);Word(0x6EE,2);Dword(0x6F0,42);Word(0x918,(uint)state);
                Word(0x930,2);Dword(0x93C,33);Word(0x956,3);Word(0xA9E,1);Word(0x744,20);Word(0x746,30);
                Word(0x990,11);Dword(0x9C0,111);Word(0x98E,12);Dword(0x6F8,112);Word(0x9A0,13);Dword(0x700,777);
                int count=CommandJourneySnapshot.RequiredCrewCount(type);Word(0xA0C,(uint)count);
                for(int i=0;i<count;i++){Word(0x96C+i*2,(uint)(100+i));Dword(0x974+i*4,(uint)(1000+i));}
                Boundary=new RescueReplanIntentWorld(Snapshot,ReadPose,this,Tile);
            }
            private eChimps Type=>(eChimps)Word(0x6E6);
            internal bool ReadPose(int id,out RescueReplanPose pose)
            {
                pose=new RescueReplanPose(Dword(0x6F0),Health,Ordinary,X,Y,Tile(X,Y),NextX,NextY,Tile(NextX,NextY),Flags,Substep,Pending);
                return id==7 && PoseReadable;
            }
            internal UnitSnapshot Snapshot(int id)
            {
                if(ThrowCapture)throw new InvalidOperationException("capture unavailable");
                if(id!=7)return null;
                var result=new UnitSnapshot{Id=7,GlobalId=Dword(0x6F0),Type=(int)Type,Owner=Word(0x6EE),Tribe=Word(0x930),TribeGlobalId=Dword(0x93C),
                    AIState=Word(0x918),X=X,Y=Y,TileId=Tile(X,Y),NextX=NextX,NextY=NextY,NextTileId=Tile(NextX,NextY),
                    Flags=Flags,Substep=Substep,OrdinaryStep=Substep<8 && (X!=NextX || Y!=NextY),Health=Health,
                    RequestedX=Word(0x744),RequestedY=Word(0x746),CrewIdentity=Array.Empty<uint>(),CrewCount=Word(0xA0C)};
                if(WorkerTravelPolicy.TryDescribe(Type,result.AIState,out var rule) && rule.CanResumeOrdinary)
                {
                    WorkerJourneySnapshot.TryCapture(rule,Word,Dword,out result.Job);
                    if((rule.RequiredExternalBindings&WorkerExternalBindings.Vegetation)!=0)
                    {result.HasCapturedVegetation=true;result.CapturedVegetationGlobal=TreeGlobal;}
                }
                else if(!CommandJourneySnapshot.TryCapture(Type,result.AIState,Word,Dword,out result.Command))result.Unsupported="unqualified-intent";
                if(CommandResumePolicy.IsSiege(Type))
                {result.CrewIdentity=new uint[8];for(int i=0;i<4;i++){result.CrewIdentity[i*2]=Word(0x96C+i*2);result.CrewIdentity[i*2+1]=Dword(0x974+i*4);}}
                return result;
            }
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket receipt,out string reason)
            {
                Captures++;receipt=null;reason=null;
                if(!BindingsAlive || !CrewAlive || before==null || before.GlobalId!=Dword(0x6F0) || before.Type!=(int)Type ||
                    before.AIState!=Word(0x918) || before.Owner!=Word(0x6EE) || before.Tribe!=Word(0x930) || before.TribeGlobalId!=Dword(0x93C) ||
                    before.RequestedX!=Word(0x744) || before.RequestedY!=Word(0x746))return false;
                if(before.Job!=null)
                {
                    if(!before.Job.MatchesContinuation(Type,Word(0x918),Word,Dword) ||
                        !WorkerJourneySnapshot.TryCapture(before.Job.Rule,Word,Dword,out var job))return false;
                    var bits=job.Rule.RequiredExternalBindings;
                    if((bits&WorkerExternalBindings.Vegetation)!=0 && (!before.HasCapturedVegetation || before.CapturedVegetationGlobal!=TreeGlobal))return false;
                    var bindings=new WorkerBindingSnapshot(bits,(bits&WorkerExternalBindings.Workplace)!=0?Word(0x990):0,
                        (bits&WorkerExternalBindings.Workplace)!=0?Dword(0x9C0):0,(bits&WorkerExternalBindings.Store)!=0?Word(0x98E):0,
                        (bits&WorkerExternalBindings.Store)!=0?Dword(0x6F8):0,(bits&WorkerExternalBindings.Vegetation)!=0?Word(0x9A0):0,
                        (bits&WorkerExternalBindings.Vegetation)!=0?TreeGlobal:0);
                    receipt=new WorkerResumeTicket(7,Dword(0x6F0),Type,Word(0x918),Word(0x6EE),Word(0x930),Dword(0x93C),Word(0x744),Word(0x746),job,bindings);
                }
                else
                {
                    if(before.Command==null || !before.Command.Matches(Type,Word(0x918),Word,Dword))return false;
                    receipt=WorkerResumeTicket.ForCommand(7,Dword(0x6F0),Type,Word(0x918),Word(0x6EE),Word(0x930),Dword(0x93C),Word(0x744),Word(0x746),before.Command);
                }
                return receipt.IsValid;
            }
            public WorkerReceiptState Inspect(WorkerResumeTicket receipt,bool continuation)
            {
                if(!BindingsAlive || !CrewAlive || receipt.UnitGlobalId!=Dword(0x6F0) || receipt.UnitType!=Type || receipt.AIState!=Word(0x918) ||
                    receipt.Owner!=Word(0x6EE) || receipt.TribeId!=Word(0x930) || receipt.TribeGlobalId!=Dword(0x93C) ||
                    receipt.RequestedX!=Word(0x744) || receipt.RequestedY!=Word(0x746))return WorkerReceiptState.Unknown;
                if(receipt.Job!=null && (!receipt.Job.MatchesContinuation(Type,Word(0x918),Word,Dword) ||
                    (receipt.Bindings.Required&WorkerExternalBindings.Vegetation)!=0 && receipt.Bindings.VegetationGlobal!=TreeGlobal))return WorkerReceiptState.Unknown;
                if(receipt.Command!=null && !receipt.Command.Matches(Type,Word(0x918),Word,Dword))return WorkerReceiptState.Unknown;
                return Flags==0?WorkerReceiptState.MatchingStopped:Flags==5?WorkerReceiptState.MatchingParked:WorkerReceiptState.MatchingActive;
            }
            private bool Forbidden(){Mutations++;throw new InvalidOperationException("intent boundary must be read-only");}
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)=>Forbidden();
            public bool Reissue(WorkerResumeTicket ticket)=>Forbidden();
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>Forbidden();
            public bool ProbeOriginal(WorkerResumeTicket ticket)=>Forbidden();
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>Forbidden();
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long result,out WorkerResumeTicket replacement)
            {replacement=null;Forbidden();return WorkerOrderObservation.Keep;}
        }
        private static RescueReplanIntent Capture(Source source)
        {Check(source.Boundary.TryCapture(7,20,30,0,out var intent),"qualified existing-goal Pre captures unreserved intent");return intent;}
        internal static void Run()
        {
            checks=0;var workers=new HashSet<eChimps>();int commandPhases=0;
            foreach(eChimps type in Enum.GetValues(typeof(eChimps)))for(int state=0;state<=102;state++)
            {
                bool worker=WorkerTravelPolicy.TryDescribe(type,state,out var rule) && rule.CanResumeOrdinary;
                if(!worker && !CommandJourneySnapshot.IsSupportedPhase(type,state))continue;
                if(worker)workers.Add(type);else commandPhases++;
                var source=new Source(type,state);var intent=Capture(source);
                UnitSnapshot exact=null;
                Check(source.Boundary.IsCurrent(intent) && source.Boundary.TryGetSnapshot(intent,out exact),"all admitted active families validate after refusal");
                Check((exact.Job!=null)==worker && (exact.Command!=null)==!worker && exact.X==4 && exact.Flags==2 && exact.Substep==8,"family remains explicit with fresh Pre pose");
                source.Health=77;
                Check(source.Boundary.TryGetSnapshot(intent,out exact) && exact.Health==77 && intent.CopyBefore().Health==100,"legitimate damage refreshes without changing Pre proof");
                source.Word(worker?0x9E0:0x956,9);
                Check(!source.Boundary.IsCurrent(intent) && !source.Boundary.TryGetSnapshot(intent,out _),"same goal with later changed purpose never inherits Pre intent");
                Check(source.Mutations==0,"intent operations never reserve/park/reissue/probe");
            }
            Check(workers.Count==20 && commandPhases>50,"every admitted worker family and active command phase exercised");
            var ordinary=new Source(eChimps.CHIMP_TYPE_SPEARMAN,101);
            foreach(int extra in new[]{-1,1,2})Check(!ordinary.Boundary.TryCapture(7,20,30,extra,out _),"reconstruction/special selectors are not ordinary fresh replans");
            Check(!ordinary.Boundary.TryCapture(7,21,30,0,out _) && !ordinary.Boundary.TryCapture(0,20,30,0,out _),"new destination or invalid unit is not this source");
            foreach(int flags in new[]{0,1,3,5,6}){ordinary.Flags=flags;Check(!ordinary.Boundary.TryCapture(7,20,30,0,out _),"Pre must contain an active ordinary segment");}
            ordinary.Flags=2;ordinary.Pending=1;Check(!ordinary.Boundary.TryCapture(7,20,30,0,out _),"substep8 with pending midpoint cannot qualify");ordinary.Pending=0;
            ordinary.NextX=5;Check(!ordinary.Boundary.TryCapture(7,20,30,0,out _),"substep8 cannot smuggle a different Next");ordinary.NextX=4;
            var stable=Capture(ordinary);ordinary.Flags=0;
            Check(!ordinary.Boundary.TryGetSnapshot(stable,out _),"arbitrary stop0 at old current is not accepted-step completion");
            var step=new Source(eChimps.CHIMP_TYPE_SPEARMAN,101){NextX=5,Substep=2};var moving=Capture(step);
            step.Substep=4;Check(step.Boundary.TryGetSnapshot(moving,out var progressed) && progressed.Substep==2 && progressed.X==4 && progressed.NextX==5,
                "same accepted step can progress while output retains Pre pose");
            step.Substep=1;Check(!step.Boundary.IsCurrent(moving),"step reset cannot inherit the original substep proof");
            step.Substep=3;step.NextY=6;Check(!step.Boundary.IsCurrent(moving),"new next step cannot inherit original route proof");
            step.X=5;step.Y=5;step.NextX=5;step.NextY=5;step.Substep=8;step.Flags=0;
            Check(step.Boundary.TryGetSnapshot(moving,out progressed) && progressed.Flags==2 && progressed.Substep==2 && progressed.X==4,
                "exact accepted Next completion0 remains an originally active input to GeometryJoin");
            step.X=6;step.NextX=6;Check(!step.Boundary.IsCurrent(moving),"second unproved successor rejects intent");
            var ox=new Source(eChimps.CHIMP_TYPE_QUARRY_OX,1);var oxIntent=Capture(ox);ox.Word(0x8E6,3);
            Check(ox.Boundary.TryGetSnapshot(oxIntent,out var timed) && timed.Job.TryGetValue(0x8E6,out uint countdown) && countdown==3 &&
                oxIntent.CopyBefore().Job.TryGetValue(0x8E6,out countdown) && countdown==0,"audited role timer is refreshed exactly without editing Pre job");
            var apple=new Source(eChimps.CHIMP_TYPE_FARMER_APPLE,2);var treeIntent=Capture(apple);apple.TreeGlobal=888;
            Check(!apple.Boundary.IsCurrent(treeIntent) && !apple.Boundary.TryGetSnapshot(treeIntent,out _),"Pre vegetation lifetime must survive to delayed admission");
            foreach(int at in new[]{0x918,0x6EE,0x930,0x744,0x9F4})
            {
                var changed=new Source(eChimps.CHIMP_TYPE_SPEARMAN,101);var prior=Capture(changed);changed.Word(at,(uint)(changed.Word(at)+1));
                Check(!changed.Boundary.TryGetSnapshot(prior,out _),"caller/legacy Post AI/owner/group/goal/command changes win");
            }
            var skirmisher=new Source(eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,101);var failed=Capture(skirmisher);skirmisher.Word(0xA7E,100);
            Check(skirmisher.Boundary.TryGetSnapshot(failed,out _),"native refusal A7E timer is not restored as command identity");
            skirmisher.Word(0x918,110);Check(!skirmisher.Boundary.IsCurrent(failed),"native retirement AI110 wins over failed replan intent");
            var engine=new Source(eChimps.CHIMP_TYPE_CATAPULT,101);var crew=Capture(engine);engine.Dword(0x978,9999);
            Check(!engine.Boundary.IsCurrent(crew),"crew lifetime change invalidates delayed command intent");
            var immutable=new Source(eChimps.CHIMP_TYPE_CATAPULT,101);var receipt=Capture(immutable);var copy=receipt.CopyBefore();
            copy.CrewIdentity[0]=999;copy.Candidates.Add(new RescueCandidate{Tile=100});copy.RequestedX=50;
            var again=receipt.CopyBefore();Check(again.CrewIdentity[0]==100 && again.Candidates.Count==0 && again.RequestedX==20,"Pre snapshot exposes no mutable crew/candidate/goal alias");
            immutable.BindingsAlive=false;Check(!immutable.Boundary.IsCurrent(receipt),"expired active external bindings deny admission before recapture");
            var unavailable=new Source(eChimps.CHIMP_TYPE_SPEARMAN,101){ThrowCapture=true};
            Check(!unavailable.Boundary.TryCapture(7,20,30,0,out _),"read exceptions never emit partial intent");
            unavailable.ThrowCapture=false;var available=Capture(unavailable);unavailable.Ordinary=false;Check(!unavailable.Boundary.IsCurrent(available),"mode/portal/mount/kill guard remains live");
            Check(!unavailable.Boundary.IsCurrent(null) && !unavailable.Boundary.TryGetSnapshot(null,out _),"missing intent grants no authority");
            Console.WriteLine("PASS: rescue replan intent "+checks+" checks; Pre typed receipts, live pose/bindings, exact refresh and read-only boundary; no game execution.");
        }
    }
}

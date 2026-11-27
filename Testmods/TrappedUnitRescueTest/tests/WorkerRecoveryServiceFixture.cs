using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using APIShared.Savegames;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerRecoveryServiceFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        private sealed class World : IWorkerResumeWorld
        {
            internal long Session=10,Generation=1;
            internal bool Available=true;
            internal WorkerResumeTicket Ticket;
            internal Action DuringMove;
            internal int Moves;
            internal bool Read(out long session,out long generation){session=Session;generation=Generation;return Available;}
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason){ticket=Ticket;reason=null;return ticket!=null;}
            public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)=>WorkerReceiptState.MatchingParked;
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)=>true;
            public bool Reissue(WorkerResumeTicket ticket){Moves++;DuringMove?.Invoke();return false;}
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>false;
            public bool ProbeOriginal(WorkerResumeTicket ticket)=>throw new InvalidOperationException("Unexpected native probe.");
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>false;
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement)
            {replacement=null;return WorkerOrderObservation.Keep;}
        }
        private static WorkerResumeTicket Ticket(int id)
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_QUARRY_OX,1,out var rule);
            WorkerJourneySnapshot.TryCapture(rule,offset=>offset==0x6E6?(ushort)rule.UnitType:
                offset==0x918 || offset==0x990 || offset==0x98E?(ushort)1:offset==0x6EE?(ushort)2:(ushort)0,
                offset=>offset==0x6F0?(uint)id:0,out var job);
            return new WorkerResumeTicket(id,(uint)id,rule.UnitType,1,2,0,0,20,30,job,
                new WorkerBindingSnapshot(rule.RequiredExternalBindings,1,0,1,0,0,0));
        }
        internal static void Run()
        {
            checks=0;ActualArchiveLoadCallback();LegacyMigration();var world=new World{Ticket=Ticket(1)};var supervisor=new WorkerResumeSupervisor(world);
            bool recovering=false;
            var service=new WorkerRecoveryService(supervisor,()=>recovering,_=>{},world.Read);
            Check(!service.Ready && !service.AdmitTick(0),"no session cannot execute");
            service.BeginSession(10,true);Check(service.Ready && service.AdmitTick(int.MaxValue-1),"bind ready session clock");
            Check(service.AdmitTick(int.MinValue+1) && service.TickValue==3,"native counter wraps monotonically");
            service.BeginSession(10,true);service.AdmitTick(0);
            Check(service.AdmitTick(20) && service.TickValue==20,"forward clock");
            Check(!service.AdmitTick(19) && service.TickValue==20,"backward tick cannot age receipts");
            Check(service.AdmitTick(21) && service.TickValue==21,"rejected backward sample does not poison clock");
            Check(supervisor.Ledger.TryReserve(world.Ticket,out var reserved),"reserve ownership");
            Check(!service.TryCaptureOwnedRecord(out var blocked) && blocked==null,"save refuses transient receipt");
            supervisor.Ledger.CommitPark(reserved,21);
            Check(service.TryCaptureOwnedRecord(out var bytes),"save captures owned park");
            recovering=true;Check(!service.TryCaptureOwnedRecord(out _),"save refuses general native recovery");recovering=false;
            service.EndSession();Check(!service.Ready && supervisor.Ledger.Count==0 && service.TickValue==0,"end retires old world");
            service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,bytes);
            var original=(byte[])bytes.Clone();bytes[0]^=1;
            Check(supervisor.Ledger.Count==0,"load stage never applies before start");
            service.BeginSession(10,true);
            Check(service.Ready && supervisor.Ledger.Count==1,"load clone applies to fresh session");
            Check(service.TryCaptureOwnedRecord(out var restored) && Equal(original,restored),"fresh save preserves parked metadata exactly");
            Check(service.AdmitTick(100) && service.TickValue==0,"restored deadline clock rebinds");
            world.Session=11;Check(!service.AdmitTick(101) && supervisor.Ledger.Count==1,"foreign session cannot discard owned receipt");world.Session=10;
            world.Available=false;Check(!service.AdmitTick(101) && supervisor.Ledger.Count==1,"missing world is no reset signal");world.Available=true;
            world.Generation++;
            Check(service.AdmitTick(101) && service.TickValue==0 && supervisor.Ledger.Count==0,"new world generation drops only old world receipts");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,original);service.BeginSession(10,false);
            Check(!service.Ready && supervisor.Ledger.Count==1 && world.Moves==0,"inadmissible mode keeps ownership without executing");
            Check(service.TryCaptureOwnedRecord(out var inactive) && Equal(original,inactive),"inactive ownership persists without setting mutation");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,null);service.BeginSession(10,true);
            Check(!service.Ready && !service.TryCaptureOwnedRecord(out _),"missing required restored entry fails closed");
            Check(service.AdmitTick(0),"unusable worker record does not block independent unit queue clock");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,new byte[]{1,2,3});service.BeginSession(10,true);
            Check(!service.Ready && !service.TryCaptureOwnedRecord(out _),"invalid binary restore cannot become a fresh empty record");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.TooNew,null);service.BeginSession(10,true);
            Check(!service.Ready && service.AdmitTick(0),"newer worker data blocks workers, not ordinary rescue");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.NoRecord,null);service.BeginSession(10,true);
            Check(service.Ready && supervisor.Ledger.Count==0,"no record starts fresh without old-save inference");
            service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,original);
            service.StageOwnedRecord(VersionedSaveLoadStatus.NoRecord,null);service.BeginSession(10,true);
            Check(supervisor.Ledger.Count==0,"later load cannot inherit earlier staged receipts");
            world.DuringMove=()=>Check(!service.TryCaptureOwnedRecord(out _) && supervisor.IsExecutingNativeCall,"save gate during own native call");
            Check(supervisor.TryRecover(null,()=>true,0,out var outcome,out _) && outcome==WorkerRecoveryOutcome.Parked,"service native-call gate exercised");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.OutOfScope,null);service.BeginSession(10,true);
            Check(service.Ready && supervisor.Ledger.Count==0,"out-of-scope load retires previous ownership");
            Console.WriteLine("PASS: worker recovery service "+checks+" checks; staged ownership, atomic capture gates, mode and native clock lifecycle; no game execution.");
        }
        // Invoke the real registered consumer callback with the actual SDK load
        // object. Parser-return tests alone cannot prove that exceptions reach the
        // SDK's whole-record KeepLoaded branch.
        private static void InvokeLoad(WorkerRecoveryService service,IReadOnlyDictionary<string,byte[]> entries)
        {
            var ctor=typeof(VersionedSaveLoad).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[]{typeof(VersionedSaveLoadStatus),typeof(VersionedSaveLoadSource),typeof(string),typeof(int),typeof(int),
                    typeof(IReadOnlyDictionary<string,byte[]>),typeof(int),typeof(string)},null);
            Check(ctor!=null,"actual SDK load constructor shape");
            var value=(VersionedSaveLoad)ctor.Invoke(new object[]{VersionedSaveLoadStatus.Restored,VersionedSaveLoadSource.SaveGame,
                "owned-fixture.sav",1,1,entries,0,string.Empty});
            var method=typeof(WorkerRecoveryService).GetMethod("Load",BindingFlags.Instance|BindingFlags.NonPublic);
            Check(method!=null,"actual registered Load callback");
            try{method.Invoke(service,new object[]{value});}
            catch(TargetInvocationException error){if(error.InnerException is InvalidDataException invalid)throw invalid;throw;}
        }
        private static void ActualArchiveLoadCallback()
        {
            var native=new World();var source=new WorkerRecoveryService(new WorkerResumeSupervisor(native),()=>false,_=>{},native.Read);
            source.BeginSession(10,true);Park(source.Supervisor.Ledger,Ticket(71));
            var unit=new UnitSnapshot{Id=33,GlobalId=133,Type=(int)eChimps.CHIMP_TYPE_PEASANT,Owner=2,Health=10000,AIState=0,
                X=20,Y=20,TileId=1000,NextX=20,NextY=20,NextTileId=1000,RequestedX=20,RequestedY=20,Flags=0,Substep=8,
                CivilianRemoval=true,AffectedCurrent=true,CurrentWasOrdinary=true,CurrentHadExit=true,NextWasOrdinary=true,CrewIdentity=new uint[0]};
            var active=new RescuePendingCandidate<UnitSnapshot>("AiWall",unit,200,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,false);
            source.CapturePending=(out RescuePendingCandidate<UnitSnapshot>[] rows)=>{rows=new[]{active};return true;};
            Check(source.TryCaptureArchive(out var good) && good.ContainsKey("compact/meta"),"actual Service creates compact record");
            WorkerRecoveryService Receiver(out Func<int> pending,out Func<int> geometry)
            {
                int p=-1,g=-1;var result=new WorkerRecoveryService(new WorkerResumeSupervisor(native),()=>false,_=>{},native.Read);
                result.RestorePending=rows=>p=rows.Length;result.RestoreGeometry=rows=>g=rows.Length;
                pending=()=>p;geometry=()=>g;return result;
            }
            var receiver=Receiver(out var pending,out var geometry);InvokeLoad(receiver,good);
            Check(receiver.Supervisor.Ledger.Count==0,"valid callback only stages ownership");receiver.BeginSession(10,true);
            Check(receiver.Ready && receiver.Supervisor.Ledger.Count==1 && pending()==1 && geometry()==0,"valid compact callback restores all components together");
            var legacy=new SortedDictionary<string,byte[]>(StringComparer.Ordinal){{"owned-workers",good["owned-workers"]}};
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("AiWall",unit,1,0,0),out var old),"actual legacy causal record");legacy.Add("pending/000",old);
            receiver=Receiver(out pending,out geometry);InvokeLoad(receiver,legacy);receiver.BeginSession(10,true);
            Check(receiver.Ready && receiver.Supervisor.Ledger.Count==1 && pending()==1,"valid legacy callback retained");
            foreach(int which in new[]{0,1,2,3})
            {
                var bad=new SortedDictionary<string,byte[]>(which==1?legacy:good,StringComparer.Ordinal);
                if(which==0){bad["compact/meta"]=(byte[])bad["compact/meta"].Clone();bad["compact/meta"][4]=99;}
                else if(which==1)bad["pending/000"]=new byte[]{1,2,3};
                else if(which==2)bad["owned-workers"]=new byte[]{1,2,3};
                else bad["geometry/000"]=new byte[]{1}; // Mixed compact/legacy namespaces.
                receiver=Receiver(out pending,out geometry);bool rejected=false;
                try{InvokeLoad(receiver,bad);}catch(InvalidDataException){rejected=true;}
                Check(rejected,"actual load callback signals whole-record preservation "+which);
                Check(!receiver.TryCaptureArchive(out var replacement) && replacement==null,"rejected callback cannot write fresh empty metadata "+which);
                receiver.BeginSession(10,true);
                Check(!receiver.Ready && receiver.Supervisor.Ledger.Count==0 && pending()==0 && geometry()==0,"no valid prefix activates after rejected archive "+which);
                Check(!receiver.TryCaptureOwnedRecord(out _),"invalid archive remains unusable after start "+which);
            }
        }
        private sealed class LegacyWorld : IWorkerLegacyRetirementWorld
        {
            internal int Inspections,Marks;internal bool MarkSuccess=true,Confirm=true,Throw;
            internal WorkerLegacyRetirement State=WorkerLegacyRetirement.ReadyToMark;
            internal Action BeforeMark;internal bool Published;
            public bool IsPublishedOrder(WorkerResumeTicket ticket,long nativeResult)=>Published && nativeResult==1;
            public WorkerLegacyRetirement Retire(WorkerResumeTicket ticket,Func<bool> authority)=>
                NativeWorkerLegacyRetirementWorld.Run(ticket,authority,()=>{Inspections++;return State;},()=>
                {Marks++;BeforeMark?.Invoke();if(Throw)throw new Exception("marker fixture");return MarkSuccess;},()=>Confirm);
        }
        private static WorkerResumeTicket LegacyTicket(int id)
        {
            Check(WorkerTravelPolicy.TryDescribeHistorical(eChimps.CHIMP_TYPE_FARMER_APPLE,2,out var rule),"historical Apple rule available for original saved receipt");
            var offsets=new int[rule.FieldCount];var widths=new int[rule.FieldCount];var values=new uint[rule.FieldCount];
            for(int i=0;i<values.Length;i++)
            {
                var field=rule.Field(i);offsets[i]=field.NativeOffset;widths[i]=field.Width;
                values[i]=field.NativeOffset==0x6E6?(uint)rule.UnitType:field.NativeOffset==0x918?2u:
                    field.NativeOffset==0x6EE?2u:field.NativeOffset==0x990?1u:field.NativeOffset==0x9A0?7u:
                    field.NativeOffset==0x6F0?(uint)id:field.NativeOffset==0x9C0?11u:0u;
            }
            Check(WorkerJourneySnapshot.TryRestoreKnownSnapshot(rule.UnitType,2,offsets,widths,values,out var job),
                "historical immutable job restored from its original wire schema, never admitted by live capture");
            var ticket=new WorkerResumeTicket(id,(uint)id,rule.UnitType,2,2,0,0,20,30,job,
                new WorkerBindingSnapshot(rule.RequiredExternalBindings,1,11,0,0,7,222));
            Check(ticket.IsValid,"historical receipt has a valid original binding schema");return ticket;
        }
        private static void Park(WorkerResumeLedger ledger,WorkerResumeTicket ticket)
        {Check(ledger.TryReserve(ticket,out var token) && ledger.CommitPark(token,0),"construct owned historical parked receipt");}
        private static unsafe void LegacyMigration()
        {
            var ticket=LegacyTicket(5);var world=new World{Ticket=ticket};var owner=new WorkerResumeSupervisor(world);
            Park(owner.Ledger,ticket);owner.Ledger.Drain(0,512,1,_=>WorkerResumeDecision.WaitForGeometry);
            Check(owner.Ledger.TrySerialize(out var saved),"dormant original receipt serializes");
            owner.Reset();var service=new WorkerRecoveryService(owner,()=>false,null,world.Read);var legacy=new LegacyWorld();
            service.EnableLegacyRetirement(legacy);service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,saved);service.BeginSession(10,true);
            Check(service.AdvanceLegacyTick(100)==0 && legacy.Marks==0,"load/BeginSession do not mutate before a qualified world tick");
            Check(service.AdmitTick(100) && service.AdvanceLegacyTick(100)==1 && legacy.Marks==1 && owner.Ledger.Count==0 && world.Moves==0,
                "waiting restored human park retires without gameplay setting, mutation wakeup or job reissue");
            Check(service.AdvanceLegacyTick(100)==0 && legacy.Marks==1,"same native tick cannot repeat migration");
            Check(service.TryCaptureOwnedRecord(out var empty) && empty.Length<saved.Length,"confirmed marker is acknowledged before the next archive");
            Check(!owner.TryRecover(null,()=>{throw new Exception("obsolete worker mutation");},1,out _,out _),
                "enabled migration cannot create a new human-worker resume transaction");
            owner.Reset();Park(owner.Ledger,ticket);Park(owner.Ledger,Ticket(6));
            owner.Ledger.Drain(0,512,4,_=>WorkerResumeDecision.WaitForGeometry);
            legacy.MarkSuccess=false;service.AdmitTick(101);service.AdvanceLegacyTick(101);
            Check(owner.Ledger.Count==2 && legacy.Marks==2 && world.Moves==0,"failed marker preserves both original receipts without reissue");
            legacy.MarkSuccess=true;legacy.Confirm=false;
            owner.Ledger.DrainLegacyCivilian(512,4,t=>legacy.Retire(t,()=>true));
            Check(owner.Ledger.Count==2,"unconfirmed marker cannot release historical ownership");
            legacy.Confirm=true;legacy.Throw=true;
            owner.Ledger.DrainLegacyCivilian(512,4,t=>legacy.Retire(t,()=>true));
            Check(owner.Ledger.Count==2,"marker exception preserves receipt and ledger drain exits cleanly");
            legacy.Throw=false;legacy.State=WorkerLegacyRetirement.Released;
            owner.Ledger.DrainLegacyCivilian(512,4,t=>legacy.Retire(t,()=>true));
            Check(owner.Ledger.Count==1 && owner.Ledger.TryGetParkedByUnit(6,out _),"gone/new-order human acknowledgement leaves Ox receipt untouched");

            owner.Reset();Park(owner.Ledger,ticket);legacy.State=WorkerLegacyRetirement.ReadyToMark;
            int calls=legacy.Marks;owner.ObserveOrderPre(ticket.UnitId);
            owner.RetireLegacyCivilianParks(512,4,legacy,()=>true);
            Check(legacy.Marks==calls && owner.Ledger.Count==1,"in-flight external order prevents any legacy marking");
            legacy.Published=true;owner.ObserveOrderPost(ticket.UnitId,0,1);legacy.Published=false;
            legacy.State=WorkerLegacyRetirement.ReadyToMark;
            owner.RetireLegacyCivilianParks(512,4,legacy,()=>true);
            Check(owner.Ledger.Count==0 && legacy.Marks==calls && !owner.HasPendingExternalOrders,
                "completed successful external route retires old receipt without recreating an arrival barrier");
            owner.Reset();Park(owner.Ledger,ticket);legacy.State=WorkerLegacyRetirement.ReadyToMark;
            legacy.BeforeMark=()=>Check(!service.TryCaptureOwnedRecord(out _),"atomic save blocked during migration marker transaction");
            owner.RetireLegacyCivilianParks(512,4,legacy,()=>true);
            Check(owner.Ledger.Count==0 && !owner.IsExecutingNativeCall,"confirmed migration finally clears executing state");
            legacy.BeforeMark=null;
            owner.Reset();Park(owner.Ledger,ticket);int reads=0;
            var outcome=NativeWorkerLegacyRetirementWorld.Run(ticket,()=>++reads<3,()=>WorkerLegacyRetirement.ReadyToMark,
                ()=>{throw new Exception("lost authority must never mark");},()=>true);
            Check(outcome==WorkerLegacyRetirement.Deferred,"lost final authority prevents actual marker");
            int inspected=0;outcome=NativeWorkerLegacyRetirementWorld.Run(ticket,()=>true,
                ()=>++inspected==1?WorkerLegacyRetirement.ReadyToMark:WorkerLegacyRetirement.Released,
                ()=>{throw new Exception("new live route must never mark");},()=>true);
            Check(outcome==WorkerLegacyRetirement.Released && inspected==2,"second live read respects intervening original lifetime/order replacement");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,saved);service.BeginSession(10,false);service.AdmitTick(200);
            calls=legacy.Marks;Check(service.AdvanceLegacyTick(200)==0 && legacy.Marks==calls && owner.Ledger.Count==1,
                "excluded mode retains metadata without native migration");
            service.EndSession();service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,saved);service.BeginSession(10,true);service.AdmitTick(201);
            world.Generation++;Check(service.AdvanceLegacyTick(201)==0 && owner.Ledger.Count==1,"world loss before marker retains old-world ownership until normal reset");
            service.AdmitTick(202);Check(owner.Ledger.Count==0,"new admitted world cannot inherit historical park cleanup");

            GameUnit unit=default;unit.r_AliveState=SHCDESE.Interop.Enums.AliveState.IsAlive;unit.r_GlobalId=ticket.UnitGlobalId;
            unit.r_UnitChimp=ticket.UnitType;unit.r_ControllableForPlayerId=ticket.Owner;unit.r_CurrentHealth=10;unit.r_AIState=3;
            unit.r_PathPlanStateBitFlags=5;unit.r_CurrentTilePositionX=20;unit.r_CurrentTilePositionY=30;
            unit.r_NextTilePositionX2=20;unit.r_NextTilePositionY2=30;unit.r_CurrentPositionTileId=123;
            unit.r_NextPositionTileId2=999;unit.r_MovementSubstep=8;
            Check(NativeWorkerLegacyRetirementWorld.InspectBarrier(ticket,&unit,0,123,true)==WorkerLegacyRetirement.ReadyToMark,
                "actual typed reader ignores old job/AI phase/external bindings and inactive packedNext cache");
            unit.r_PathPlanStateBitFlags=0;unit.r_TargetTilePositionX2=20;unit.r_TargetTilePositionY2=30;
            Check(NativeWorkerLegacyRetirementWorld.IsPublishedOrder(ticket,&unit,1,0,123),"actual Native1 stationary endpoint is a successful fresh external order");
            Check(!NativeWorkerLegacyRetirementWorld.IsPublishedOrder(ticket,&unit,0,0,123),"Native0 at a coincident goal is not a witnessed publication");
            unit.r_PathPlanStateBitFlags=5;
            Check(!NativeWorkerLegacyRetirementWorld.IsPublishedOrder(ticket,&unit,1,0,123),"flags5 alone cannot invent successful publication from Native1");
            unit.r_PathPlanStateBitFlags=2;
            Check(NativeWorkerLegacyRetirementWorld.InspectBarrier(ticket,&unit,0,123,true)==WorkerLegacyRetirement.Released,"actual new active route wins");
            unit.r_PathPlanStateBitFlags=5;unit.r_GlobalId++;
            Check(NativeWorkerLegacyRetirementWorld.InspectBarrier(ticket,&unit,0,123,true)==WorkerLegacyRetirement.Released,"actual slot reuse never marks replacement");
            unit.r_GlobalId=ticket.UnitGlobalId;unit.r_AIState=109;
            Check(NativeWorkerLegacyRetirementWorld.InspectBarrier(ticket,&unit,0,123,true)==WorkerLegacyRetirement.Released,"recruitment state is protected from migration");
            unit.r_AIState=3;unit.r_MovementSubstep=7;
            Check(NativeWorkerLegacyRetirementWorld.InspectBarrier(ticket,&unit,0,123,true)==WorkerLegacyRetirement.Deferred,"unknown started pose cannot claim normalized owned park");
            unit.r_MovementSubstep=8;
            Check(NativeWorkerLegacyRetirementWorld.InspectBarrier(ticket,&unit,0,123,false)==WorkerLegacyRetirement.Deferred,"missing exact occupancy prevents removal");
            var log=new System.Collections.Generic.List<string>();var diagnosticWorld=new World();var diagnosticOwner=new WorkerResumeSupervisor(diagnosticWorld);
            var diagnostic=new WorkerRecoveryService(diagnosticOwner,()=>false,message=>log.Add(message),diagnosticWorld.Read);
            var uncertain=new LegacyWorld{MarkSuccess=false};diagnostic.EnableLegacyRetirement(uncertain);diagnostic.BeginSession(10,true);
            Park(diagnosticOwner.Ledger,LegacyTicket(7));
            for(int tick=0;tick<32;tick++){diagnostic.AdmitTick(tick);diagnostic.AdvanceLegacyTick(tick);}
            Check(log.Count==1,"unchanged unqualified legacy barrier reports once rather than every visit");
            uncertain.Throw=true;
            for(int tick=32;tick<64;tick++){diagnostic.AdmitTick(tick);diagnostic.AdvanceLegacyTick(tick);}
            Check(log.Count==2 && log[1].Contains("exception-Exception") && diagnosticOwner.Ledger.Count==1,
                "changed actual marker exception reports once and retains unknown ownership");
            var commandType=eChimps.CHIMP_TYPE_SPEARMAN;
            Check(CommandJourneySnapshot.TryCapture(commandType,101,offset=>offset==0x6E4?(ushort)2:offset==0x6E6?(ushort)commandType:
                offset==0x918?(ushort)101:offset==0x6EE?(ushort)2:offset==0xA9E?(ushort)1:(ushort)0,
                offset=>offset==0x6F0?88u:0u,out var command),"actual command receipt fixture");
            var commandTicket=WorkerResumeTicket.ForCommand(88,88,commandType,101,2,0,0,20,30,command);
            var mixed=new WorkerResumeLedger();Park(mixed,commandTicket);Park(mixed,Ticket(89));Park(mixed,LegacyTicket(90));
            Check(mixed.LegacyCivilianCount==1,"only historical employed humans create migration demand");
            mixed.DrainLegacyCivilian(512,4,t=>WorkerLegacyRetirement.Marked);
            Check(mixed.Count==2 && mixed.LegacyCivilianCount==0 && mixed.TryGetParkedByUnit(88,out _) && mixed.TryGetParkedByUnit(89,out _),
                "command and Ox ownership survive migration and zero legacy demand returns to dormant");
            int marked=0;var many=new WorkerResumeLedger();
            for(int i=1;i<=7;i++)Park(many,LegacyTicket(100+i));
            many.DrainLegacyCivilian(32,4,t=>{marked++;return WorkerLegacyRetirement.Marked;});
            Check(marked==4 && many.Count==3,"legacy migration retains strict four-action budget");
        }
        private static bool Equal(byte[] first,byte[] second)
        {if(first.Length!=second.Length)return false;for(int i=0;i<first.Length;i++)if(first[i]!=second[i])return false;return true;}
    }
}

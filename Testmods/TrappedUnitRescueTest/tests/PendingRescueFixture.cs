using System;
using System.Linq;
using System.Collections.Generic;
using APIShared.Savegames;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class PendingRescueFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private sealed class QuietWorld : IWorkerResumeWorld
        {
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason){ticket=null;reason=null;return false;}
            public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)=>WorkerReceiptState.Unknown;
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)=>false;
            public bool Reissue(WorkerResumeTicket ticket)=>false;
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>false;
            public bool ProbeOriginal(WorkerResumeTicket ticket)=>false;
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>false;
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long nativeResult,out WorkerResumeTicket replacement){replacement=null;return WorkerOrderObservation.Keep;}
        }
        private static UnitSnapshot Soldier(int id=1)
        {
            var u=new UnitSnapshot{Id=id,GlobalId=(uint)id,Type=(int)eChimps.CHIMP_TYPE_SPEARMAN,Owner=2,Health=500,AIState=101,
                X=30,Y=40,TileId=1234,NextX=31,NextY=40,NextTileId=1235,RequestedX=80,RequestedY=80,
                OrdinaryStep=true,AffectedNext=true,CurrentWasOrdinary=true,NextWasOrdinary=true,Flags=2,Substep=3,CrewIdentity=new uint[0]};
            CommandJourneySnapshot.TryCapture((eChimps)u.Type,u.AIState,offset=>offset==0x6E4?(ushort)2:offset==0x6E6?(ushort)u.Type:
                offset==0x918?(ushort)u.AIState:offset==0x6EE?(ushort)u.Owner:offset==0xA9E?(ushort)1:(ushort)0,
                offset=>offset==0x6F0?u.GlobalId:0,out u.Command);
            u.Candidates.Add(new RescueCandidate{X=30,Y=40,Tile=1234,Distance=0});
            u.Candidates.Add(new RescueCandidate{X=29,Y=40,Tile=1233,Distance=1});return u;
        }
        internal static void Run()
        {
            checks=0;var original=new RescuePendingCandidate<UnitSnapshot>("BuildStructure",Soldier(),17,2,1);
            Check(RescuePendingCodec.TryEncode(original,out byte[] bytes),"encode actual causal snapshot");
            Check(RescuePendingCodec.TryDecode(bytes,out var copy),"decode causal snapshot");
            Check(copy.Age==17 && copy.Attempts==2 && copy.RetryIn==1 && copy.Source==original.Source,"retry history retained without process counters");
            Check(copy.Snapshot.Id==1 && copy.Snapshot.GlobalId==1 && copy.Snapshot.Candidates.Count==2 && copy.Snapshot.AffectedNext && copy.Snapshot.OrdinaryStep,
                "identity, accepted step and old reachable fields retained");
            copy.Snapshot.Candidates.Clear();
            Check(original.Snapshot.Candidates.Count==2,"decode owns its independent old reachability proof");
            var worker=Soldier();worker.Type=(int)eChimps.CHIMP_TYPE_QUARRY_OX;worker.AIState=1;worker.Command=null;
            WorkerTravelPolicy.TryDescribe((eChimps)worker.Type,worker.AIState,out var rule);
            WorkerJourneySnapshot.TryCapture(rule,offset=>offset==0x6E6?(ushort)worker.Type:offset==0x918?(ushort)worker.AIState:
                offset==0x6EE?(ushort)worker.Owner:(ushort)0,offset=>offset==0x6F0?worker.GlobalId:0,out worker.Job);
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",worker,1,0,0),out var workerBytes) &&
                RescuePendingCodec.TryDecode(workerBytes,out var workerCopy) && workerCopy.Snapshot.Job.Rule.UnitType==(eChimps)worker.Type,
                "pending worker preserves its exact known job schema for later live revalidation");
            var stationary=Soldier();stationary.AIState=2;stationary.Flags=0;stationary.Substep=8;stationary.OrdinaryStep=false;
            stationary.NextX=stationary.X;stationary.NextY=stationary.Y;stationary.NextTileId=stationary.TileId;
            stationary.RequestedX=stationary.X;stationary.RequestedY=stationary.Y;
            CommandJourneySnapshot.TryCaptureStationary((eChimps)stationary.Type,stationary.AIState,
                offset=>offset==0x6E4?(ushort)2:offset==0x6E6?(ushort)stationary.Type:offset==0x918?(ushort)stationary.AIState:
                    offset==0x6EE?(ushort)stationary.Owner:offset==0xA9E?(ushort)1:(ushort)0,
                offset=>offset==0x6F0?stationary.GlobalId:0,out stationary.Command);
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",stationary,0,0,0),out var idleBytes) &&
                RescuePendingCodec.TryDecode(idleBytes,out var idleCopy) && idleCopy.Snapshot.Command!=null && idleCopy.Snapshot.AIState==2,
                "stationary intent guards survive save/load without becoming an active resume receipt");
            stationary.Flags=2;
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",stationary,0,0,0),out _),
                "stationary-only phase cannot be upgraded to active travel through a saved snapshot");
            stationary.Flags=0;stationary.Command=null;
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",stationary,0,0,0),out _),
                "stationary pending rescue without semantic guards is not persisted");
            for(int length=0;length<bytes.Length;length++)
                Check(!RescuePendingCodec.TryDecode(bytes.Take(length).ToArray(),out _),"truncated record is not a partial proof");
            byte[] trailing=bytes.Concat(new byte[]{0}).ToArray();Check(!RescuePendingCodec.TryDecode(trailing,out _),"trailing bytes rejected");
            var changed=(byte[])bytes.Clone();changed[0]^=1;Check(!RescuePendingCodec.TryDecode(changed,out _),"unknown signature rejected");
            changed=(byte[])bytes.Clone();changed[4]=127;Check(!RescuePendingCodec.TryDecode(changed,out _),"unknown schema rejected");
            changed=(byte[])bytes.Clone();changed[6]=255;changed[7]=127;Check(!RescuePendingCodec.TryDecode(changed,out _),"source allocation bounded before reading");
            var bad=Soldier();bad.Candidates.Add(bad.Candidates[0]);Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",bad,0,0,0),out _),"duplicate reachable tiles rejected");
            bad=Soldier();bad.Candidates.Add(new RescueCandidate{X=400,Y=400,Tile=1200,Distance=1});
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",bad,0,0,0),out _),"distant target cannot be injected through a saved local proof");
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",Soldier(),80,0,0),out _),"expired proof not saved");
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",Soldier(),0,4,0),out _),"exhausted attempts not reset by save");
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",Soldier(),0,0,2),out _),"retry delay contract retained");
            var queue=new RescueCandidateQueue<UnitSnapshot>();queue.Reset(1,2);
            var identity=new RescueIdentity(1,2,1,1,4);queue.Offer(identity,123,"bridge",Soldier(),int.MaxValue-1);
            queue.Drain(int.MaxValue-1,1,1,item=>RescueQueueDecision.Retry);
            Check(queue.TryCopyPending(int.MaxValue,out var pending) && pending.Length==1 && pending[0].Age==1 && pending[0].Attempts==1 && pending[0].RetryIn==0,
                "live export preserves age and retry state across signed clock edge");
            Check(queue.Count==1,"save capture does not mutate live queue");
            queue.Reset(8,9);
            Check(queue.OfferRestored(new RescueIdentity(8,9,1,1,0),long.MaxValue,pending[0],0),"load binds fresh process identities");
            int calls=0;
            queue.Drain(0,1,1,item=>{calls++;Check(item.Attempts==2 && item.Identity.SessionId==8 && item.Identity.WorldGeneration==9 && item.Identity.OrderSerial==0,
                "loaded proof preserves attempts and rebinds identity");return RescueQueueDecision.Complete;});
            Check(calls==1 && queue.Count==0,"restored pending cause runs once");
            queue.OfferRestored(new RescueIdentity(8,9,1,1,0),55,pending[0],0);
            Check(queue.Offer(new RescueIdentity(8,9,1,1,0),55,"bridge",Soldier(),0)==RescueOfferResult.ConflictingMutation && queue.Count==0,
                "a reused process batch number cannot deduplicate a saved cause");
            queue.Offer(new RescueIdentity(8,9,1,1,0),2,"bridge",Soldier(),0);
            Check(queue.TryCopyPending(80,out pending) && pending.Length==0 && queue.Count==1,"expired save filtering is read-only");
            queue.Drain(0,1,1,item=>{Check(!queue.TryCopyPending(0,out _),"save during an active mutation is refused");return RescueQueueDecision.Complete;});
            ArchiveRoundtrip(original,bytes);
            Console.WriteLine("PASS: pending rescue persistence checks="+checks+"; production queue and binary codec, no native calls.");
        }
        private static void ArchiveRoundtrip(RescuePendingCandidate<UnitSnapshot> original,byte[] bytes)
        {
            WorkerWorldReader read=(out long session,out long generation)=>{session=7;generation=9;return true;};
            var service=new WorkerRecoveryService(new WorkerResumeSupervisor(new QuietWorld()),()=>false,null,read);
            service.StageOwnedRecord(VersionedSaveLoadStatus.NoRecord,null);
            service.CapturePending=(out RescuePendingCandidate<UnitSnapshot>[] records)=>{records=new[]{original};return true;};
            Check(service.TryCaptureArchive(out var archive) && archive.Count==2 && archive.ContainsKey("owned-workers") && archive.ContainsKey("pending/000"),
                "the actual archive capture includes owned barriers and the causal pending record atomically");
            var loaded=new WorkerRecoveryService(new WorkerResumeSupervisor(new QuietWorld()),()=>false,null,read);
            loaded.StageOwnedRecord(VersionedSaveLoadStatus.Restored,archive["owned-workers"]);
            Check(loaded.StagePendingRecords(VersionedSaveLoadStatus.Restored,archive),"stage actual saved entries");
            RescuePendingCandidate<UnitSnapshot>[] staged=null;loaded.RestorePending=records=>staged=records;
            loaded.BeginSession(7,true);
            Check(staged!=null && staged.Length==1 && staged[0].Snapshot.GlobalId==1 && staged[0].Age==17,"load delivers detached causal metadata after session creation");
            service.CapturePending=(out RescuePendingCandidate<UnitSnapshot>[] records)=>{records=new[]{original,original};return true;};
            Check(!service.TryCaptureArchive(out var rejected) && rejected==null,"duplicate unit causes abort the whole capture before any writer entry");
            var invalid=new SortedDictionary<string,byte[]>(StringComparer.Ordinal){{"pending/000",bytes},{"pending/001",bytes}};
            Check(!loaded.StagePendingRecords(VersionedSaveLoadStatus.Restored,invalid),"duplicate loaded unit causes are rejected atomically");
            invalid=new SortedDictionary<string,byte[]>(StringComparer.Ordinal){{"pending/001",bytes}};
            Check(!loaded.StagePendingRecords(VersionedSaveLoadStatus.Restored,invalid),"missing ordinal entries are rejected");
            Check(loaded.StagePendingRecords(VersionedSaveLoadStatus.NoRecord,invalid),"a fresh world cannot inherit an earlier staged cause");
            loaded.BeginSession(7,true);Check(staged.Length==0,"rejected or out-of-scope metadata never leaks a partial staged proof");
        }
    }
}

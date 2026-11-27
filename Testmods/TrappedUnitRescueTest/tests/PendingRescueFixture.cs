using System;
using System.Linq;
using System.IO;
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
            checks=0;RemovalRoundtrips();var original=new RescuePendingCandidate<UnitSnapshot>("BuildStructure",Soldier(),17,2,1);
            Check(RescuePendingCodec.TryEncode(original,out byte[] bytes),"encode actual causal snapshot");
            Check(RescuePendingCodec.TryDecode(bytes,out var copy),"decode causal snapshot");
            Check(RescuePendingCodec.TryDecode(AsVersion4(bytes),out var legacySoldier) && !legacySoldier.Snapshot.CivilianRemoval &&
                legacySoldier.Snapshot.Command!=null && !legacySoldier.Snapshot.CurrentHadExit && !legacySoldier.Snapshot.NextHadExit,
                "v4 command record retains its historical meaning without removal inference");
            Check(copy.Age==17 && copy.Attempts==2 && copy.RetryIn==1 && copy.Source==original.Source,"retry history retained without process counters");
            Check(copy.Snapshot.Id==1 && copy.Snapshot.GlobalId==1 && copy.Snapshot.Candidates.Count==2 && copy.Snapshot.AffectedNext && copy.Snapshot.OrdinaryStep,
                "identity, accepted step and old reachable fields retained");
            copy.Snapshot.Candidates.Clear();
            Check(original.Snapshot.Candidates.Count==2,"decode owns its independent old reachability proof");
            var worker=Soldier();worker.Type=(int)eChimps.CHIMP_TYPE_QUARRY_OX;worker.AIState=1;worker.Command=null;
            WorkerTravelPolicy.TryDescribeHistorical((eChimps)worker.Type,worker.AIState,out var rule);
            LegacyWorkerFixtureData.TryDecode(rule,offset=>offset==0x6E6?(ushort)worker.Type:offset==0x918?(ushort)worker.AIState:
                offset==0x6EE?(ushort)worker.Owner:(ushort)0,offset=>offset==0x6F0?worker.GlobalId:0,out worker.Job);
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",worker,1,0,0),out var workerBytes) &&
                RescuePendingCodec.TryDecode(workerBytes,out var workerCopy) && workerCopy.Snapshot.Job.Rule.UnitType==(eChimps)worker.Type,
                "pending worker preserves its exact known job schema for later live revalidation");
            Check(RescuePendingCodec.TryDecode(AsVersion4(workerBytes),out var legacyWorker) && !legacyWorker.Snapshot.CivilianRemoval &&
                legacyWorker.Snapshot.Job!=null && legacyWorker.Snapshot.Command==null,
                "v4 worker job stays a worker job, never an inferred removal");
            worker.CivilianRemoval=true;worker.CurrentHadExit=true;worker.NextHadExit=true;worker.Candidates.Clear();
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",worker,0,0,0),out _),
                "historical worker job payload cannot become removal by switching the new action flag");
            var apple=Soldier();apple.Type=(int)eChimps.CHIMP_TYPE_FARMER_APPLE;apple.AIState=2;apple.Command=null;
            WorkerTravelPolicy.TryDescribeHistorical((eChimps)apple.Type,apple.AIState,out var appleRule);
            LegacyWorkerFixtureData.TryDecode(appleRule,offset=>offset==0x6E6?(ushort)apple.Type:offset==0x918?(ushort)apple.AIState:
                offset==0x6EE?(ushort)apple.Owner:offset==0x9A0?(ushort)7:(ushort)0,
                offset=>offset==0x6F0?apple.GlobalId:0,out apple.Job);
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("BuildStructure",apple,0,0,0),out _),
                "selected vegetation without pre-mutation lifetime cannot become a saved causal proof");
            apple.HasCapturedVegetation=true;apple.CapturedVegetationGlobal=222;
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("BuildStructure",apple,0,0,0),out var appleBytes) &&
                RescuePendingCodec.TryDecode(appleBytes,out var appleCopy) && appleCopy.Snapshot.HasCapturedVegetation && appleCopy.Snapshot.CapturedVegetationGlobal==222,
                "Apple pre-mutation tree lifetime survives save/load instead of being adopted at recovery time");
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
        private static UnitSnapshot Removal()
        {
            var u=Soldier();u.Type=(int)eChimps.CHIMP_TYPE_PEASANT;u.AIState=0;u.Command=null;
            u.Candidates.Clear();u.CivilianRemoval=true;u.CurrentHadExit=true;u.NextHadExit=true;return u;
        }
        // Derive the position from the wire header, not from source-name length assumptions.
        // v4 lacks exactly these three v5 booleans; all remaining historical bytes are unchanged.
        private static int RemovalFlagsOffset(byte[] data)
        {
            using(var stream=new MemoryStream(data,false))using(var reader=new BinaryReader(stream))
            {
                reader.ReadUInt32();reader.ReadUInt16();int length=reader.ReadUInt16();reader.ReadBytes(length);
                stream.Position+=3*4+16*4+5*4+1+4+5+1;return checked((int)stream.Position);
            }
        }
        private static byte[] AsVersion4(byte[] data)
        {
            int offset=RemovalFlagsOffset(data);var legacy=data.Take(offset).Concat(data.Skip(offset+3)).ToArray();
            legacy[4]=4;legacy[5]=0;return legacy;
        }
        private static void RemovalRoundtrips()
        {
            var u=Removal();var record=new RescuePendingCandidate<UnitSnapshot>("MoatDig",u,7,1,1);
            Check(RescuePendingCodec.TryEncode(record,out var bytes) && RescuePendingCodec.TryDecode(bytes,out var restored) &&
                restored.Snapshot.CivilianRemoval && restored.Snapshot.CurrentHadExit && restored.Snapshot.NextHadExit &&
                restored.Snapshot.Job==null && restored.Snapshot.Command==null && restored.Snapshot.Candidates.Count==0 &&
                restored.Age==7 && restored.Attempts==1 && restored.RetryIn==1,
                "minimal removal identity/exit witness roundtrips without job, vegetation or banks");
            Check(!RescuePendingCodec.TryDecode(AsVersion4(bytes),out _),"removal kind cannot be reinterpreted as a historical v4 job");
            int offset=RemovalFlagsOffset(bytes);
            for(int i=0;i<3;i++)
            {
                var invalid=(byte[])bytes.Clone();invalid[offset+i]=2;
                Check(!RescuePendingCodec.TryDecode(invalid,out _),"all three new booleans reject noncanonical byte values");
            }
            var unknown=(byte[])bytes.Clone();unknown[unknown.Length-1]=3;
            Check(!RescuePendingCodec.TryDecode(unknown,out _),"removal does not bypass unknown intent-kind validation");
            foreach(eChimps type in Enum.GetValues(typeof(eChimps)))
            {
                var sample=Removal();sample.Type=(int)type;
                Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",sample,0,0,0),out _)==
                    CivilianRemovalPolicy.IsRemovableType(type),"codec uses actual type policy, including protected military and Ox types");
            }
            u=Removal();u.Flags=0;u.Substep=8;u.OrdinaryStep=false;u.NextX=u.X;u.NextY=u.Y;u.NextTileId=u.TileId;
            u.AffectedCurrent=true;u.AffectedNext=false;u.NextHadExit=false;
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("AiBuildWall",u,0,0,0),out _),
                "stationary Peasant with affected Current and an old exit needs no travel job");
            Action<UnitSnapshot>[] invalidChanges={
                x=>x.CivilianRemoval=false,x=>x.AIState=109,x=>x.AIState=108,x=>x.AIState=-1,
                x=>x.Type=ushort.MaxValue,x=>x.GlobalId=0,x=>x.Health=0,x=>x.Id=0,x=>x.Owner=9,
                x=>{x.CurrentHadExit=false;x.NextHadExit=false;},
                x=>x.CurrentWasOrdinary=false,x=>x.NextWasOrdinary=false,
                x=>{x.AffectedCurrent=false;x.AffectedNext=false;},
                x=>x.OrdinaryStep=false,x=>x.Substep=8,
                x=>x.HasCapturedVegetation=true,x=>x.CapturedVegetationGlobal=5,
                x=>x.Command=Soldier().Command,
                x=>{x.CrewCount=8;x.CrewIdentity=new uint[8];},
                x=>x.Candidates.Add(new RescueCandidate{X=x.X,Y=x.Y,Tile=x.TileId,Distance=0})
            };
            foreach(var change in invalidChanges)
            {
                var invalid=Removal();change(invalid);
                Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",invalid,0,0,0),out _),
                    "removal rejects mismatched action, recruitment, identity, old-exit or semantic payload");
            }
            var soldier=Soldier();
            Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",soldier,0,0,0),out var commandBytes),
                "prepare actual command wire record for action-kind mismatch");
            int commandOffset=RemovalFlagsOffset(commandBytes);
            commandBytes[commandOffset]=1;commandBytes[commandOffset+1]=1;commandBytes[commandOffset+2]=1;
            Check(!RescuePendingCodec.TryDecode(commandBytes,out _),"wire removal flag cannot claim a saved military command");
            soldier.CurrentHadExit=true;
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("MoatDig",soldier,0,0,0),out _),
                "new exit flags cannot be smuggled into the historical command action");
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

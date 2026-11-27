using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using APIShared.Savegames;
using APIShared.Units;
using APIShared.WorldMutations;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class RescueGeometryPersistenceFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private static int Tile(int x,int y)=>y*800+x;
        private static RescueGeometryWitness Witness(int id)
        {
            var current=new RescueGeometryEndpoint(50,20,Tile(50,20),true,true,1);
            var next=new RescueGeometryEndpoint(51,20,Tile(51,20),true,true,2);
            Check(RescueGeometryWitness.TryCreate(id,(uint)(1000+id),(int)eChimps.CHIMP_TYPE_SPEARMAN,2,current,next,true,
                new[]{new RescueCandidate{X=50,Y=20,Tile=Tile(50,20),Distance=0},
                    new RescueCandidate{X=42,Y=20,Tile=Tile(42,20),Distance=8}},out var witness),"original accepted-step geometry is valid");
            return witness;
        }
        private static UnitSnapshot Joined(int id)
        {
            var type=eChimps.CHIMP_TYPE_SPEARMAN;
            Check(CommandJourneySnapshot.TryCapture(type,101,
                at=>at==0x6E4?(ushort)AliveState.IsAlive:at==0x6E6?(ushort)type:
                    at==0x6EE || at==0x930?(ushort)2:at==0x918?(ushort)101:at==0xA9E?(ushort)1:(ushort)0,
                at=>at==0x6F0?(uint)(1000+id):at==0x93C?33u:0,out var command),"fresh typed command capture");
            var fresh=new UnitSnapshot{Id=id,GlobalId=(uint)(1000+id),Type=(int)type,Owner=2,Tribe=2,TribeGlobalId=33,
                Health=100,AIState=101,Flags=2,Substep=8,X=51,Y=20,TileId=Tile(51,20),NextX=51,NextY=20,NextTileId=Tile(51,20),
                RequestedX=80,RequestedY=20,Command=command,CrewIdentity=Array.Empty<uint>()};
            Check(RescueGeometryWitness.TryJoin(Witness(id),fresh,out var joined),"original Next can be fresh Current without rewriting historical banks");
            return joined;
        }
        internal static void Run()
        {
            checks=0;var original=new RescuePendingGeometry("causal-replan",Witness(1),79,3,1);
            Check(RescueGeometryCodec.TryEncode(original,out var bytes) && RescueGeometryCodec.TryDecode(bytes,out _),"geometry-only record roundtrips at final admissible age and attempt");
            Check(RescueGeometryCodec.TryDecode(bytes,out var restored) && restored.Age==79 && restored.Attempts==3 && restored.RetryIn==1,
                "geometry record preserves original age and retry metadata");
            Check(restored.Witness.Current.X==50 && restored.Witness.Next.X==51 && restored.Witness.HasAcceptedNext,
                "geometry-only original coordinate frame is preserved");
            var joined=Joined(1);var pending=new RescuePendingCandidate<UnitSnapshot>("failed-existing-goal-replan",joined,20,1,0);
            Check(RescuePendingCodec.TryEncode(pending,out var encoded),"joined candidate with original radius9 bank encodes");
            Check(RescuePendingCodec.TryDecode(encoded,out var decoded) && decoded.Snapshot.GeometryWitness!=null,"joined candidate preserves explicit original geometry witness");
            var after=decoded.Snapshot;
            Check(after.X==51 && after.NextX==51 && after.RequestedX==80 && after.AIState==101 && after.Job==null && after.Command!=null,
                "fresh command and requested goal are retained independently of original geometry");
            Check(after.Candidates.Count==2 && after.Candidates[0].X==50 && after.Candidates[0].Distance==0 &&
                after.Candidates[1].X==42 && Math.Abs(after.Candidates[1].X-after.X)==9 && after.Candidates[1].Distance==8,
                "distance0 old origin and old radius8 bank are not reinterpreted around rebased Current");
            VerifyGeometryOnly();VerifyCompactGeometry();VerifyMalformedFrames();VerifyArchives();VerifyServiceObserverLifecycle();
            Console.WriteLine("PASS: rescue geometry persistence "+checks+" checks; production codecs, shared archive bounds, atomic staging and observer lifecycle; no game execution.");
        }
        private static bool Equal(byte[] first,byte[] second)
        {if(first==null || second==null || first.Length!=second.Length)return false;for(int i=0;i<first.Length;i++)if(first[i]!=second[i])return false;return true;}
        private static byte[] IntAt(byte[] original,int at,int value)
        {var copy=(byte[])original.Clone();Array.Copy(BitConverter.GetBytes(value),0,copy,at,4);return copy;}
        private static byte[] ByteAt(byte[] original,int at,byte value)
        {var copy=(byte[])original.Clone();copy[at]=value;return copy;}
        private static byte[] Shorter(byte[] original,int length)
        {var copy=new byte[length];Array.Copy(original,copy,length);return copy;}
        private static RescuePendingCandidate<UnitSnapshot> Pending(int id=1)=>new RescuePendingCandidate<UnitSnapshot>("replan/test",Joined(id),20,0,0);
        private static RescuePendingGeometry Geometry(int id=2)=>new RescuePendingGeometry("test",Witness(id),25,2,1);
        private static void VerifyGeometryOnly()
        {
            var first=Joined(3);first.X=50;first.TileId=Tile(50,20);first.NextX=51;first.NextTileId=Tile(51,20);first.Substep=2;first.OrdinaryStep=true;
            first.AffectedCurrent=first.AffectedNext=first.CurrentWasOrdinary=first.NextWasOrdinary=true;
            first.Candidates.Clear();first.Candidates.AddRange(Witness(3).CopyCandidates());
            var second=RescueReplanIntent.CopySnapshot(first);second.AIState=0;second.RequestedX=10;second.RequestedY=15;second.Tribe=7;second.TribeGlobalId=999;
            second.Job=null;second.Command=null;second.Health=1;
            RescueGeometryWitness a=null,b=null;
            Check(RescueGeometryWitness.TryCapture(first,out a) && RescueGeometryWitness.TryCapture(second,out b),"historical capture depends only on the original spatial record");
            Check(RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",a,10,1,0),out var aa) &&
                RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",b,10,1,0),out var bb) && Equal(aa,bb),
                "geometry-only wire contains no previous AI/job/goal/group/health execution state");
            Check(!RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",a,80,0,0),out _) &&
                !RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",a,-1,0,0),out _) &&
                !RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",a,0,4,0),out _) &&
                !RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",a,0,-1,0),out _) &&
                !RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",a,0,0,2),out _),"expired/exhausted/invalid geometry metadata cannot be refreshed through save");
            Check(!RescueGeometryCodec.TryEncode(new RescuePendingGeometry("\uD800",a,0,0,0),out _) &&
                !RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("\uD800",Joined(3),0,0,0),out _),"both TryEncode contracts reject non-UTF8 source strings without throwing");
        }
        private static void VerifyCompactGeometry()
        {
            var legacy=Witness(9);
            Check(RescueLocalField.TryCapture(legacy.Current.X,legacy.Current.Y,legacy.Current.Tile,Tile,_=>true,_=>255,out var field,out _),"compact field captured for codec");
            Check(RescueGeometryWitness.TryCreateCompact(legacy.UnitId,legacy.GlobalId,legacy.Type,legacy.Owner,legacy.Current,legacy.Next,true,field,out var witness),"compact witness created");
            var record=new RescuePendingGeometry("x",witness,10000,2,1,RescueQueueLifetimeMode.ActiveWindow,7,true);
            Check(RescueGeometryCodec.TryEncode(record,out var bytes) && BitConverter.ToUInt16(bytes,4)==2 && bytes.Length==407,"compact v2 has one326B field and no candidate bank");
            Check(RescueGeometryCodec.TryDecode(bytes,out var restored) && restored.Witness.LocalField!=null && restored.Witness.CandidateCount==0 &&
                restored.Age==10000 && restored.ActiveAge==7 && restored.AwaitingResources && restored.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow,"v2 preserves active-window metadata and unbound old field");
            Check(restored.Witness.LocalField.TryFindCandidates(Tile,out var first,out _),"restored old field binds on use");
            Check(field.TryFindCandidates(Tile,out var second,out _),"captured old field binds on use");
            Check(first.Count==second.Count && first[0].Tile==second[0].Tile && first[first.Count-1].Distance==second[second.Count-1].Distance,"compact codec retains actual field result");
            var unstarted=new RescuePendingGeometry("x",witness,int.MaxValue,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,true);
            Check(RescueGeometryCodec.TryEncode(unstarted,out var waiting) && RescueGeometryCodec.TryDecode(waiting,out var wait) && wait.ActiveAge==-1 && wait.Age==int.MaxValue,"resource waiting does not invent active age or bound source age to old80ticks");
            foreach(var invalid in new[]{
                new RescuePendingGeometry("x",witness,100,1,0,RescueQueueLifetimeMode.ActiveWindow,-1,true),
                new RescuePendingGeometry("x",witness,100,0,0,RescueQueueLifetimeMode.ActiveWindow,80,false),
                new RescuePendingGeometry("x",witness,1,0,0,RescueQueueLifetimeMode.ActiveWindow,2,false),
                new RescuePendingGeometry("x",witness,1,0,0,(RescueQueueLifetimeMode)42,0,false)})
                Check(!RescueGeometryCodec.TryEncode(invalid,out _),"invalid active metadata rejected");
            Check(RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",legacy,100,1,0,RescueQueueLifetimeMode.ActiveWindow,3,false),out var activeLegacy) &&
                RescueGeometryCodec.TryDecode(activeLegacy,out var old) && old.Witness.LocalField==null && old.Witness.CandidateCount==2,"active-window legacy banks use v2 without field loss");
            Check(!RescueGeometryCodec.TryDecode(ByteAt(bytes,21,42),out _) && !RescueGeometryCodec.TryDecode(ByteAt(bytes,26,2),out _) &&
                !RescueGeometryCodec.TryDecode(ByteAt(bytes,27,2),out _) && !RescueGeometryCodec.TryDecode(ByteAt(bytes,81+36,255),out _),"v2 mode/bools/unusedbits remain strict");
            Check(!RescueGeometryCodec.TryDecode(Shorter(bytes,bytes.Length-1),out _),"v2 field cannot truncate");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                RescueGeometryCodec.WriteCompactWitness(writer,witness);writer.Flush();Check(stream.Length==53,"pooled compact witness helper has no duplicate field");
                stream.Position=0;using(var reader=new BinaryReader(stream))
                    Check(RescueGeometryCodec.TryReadCompactWitness(reader,field,out var shared) && ReferenceEquals(shared.LocalField,field),"embedded helper binds exact shared immutable field");
            }
            bool refused=false;
            try{using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))RescueGeometryCodec.WriteWitness(writer,witness);}
            catch(InvalidDataException){refused=true;}Check(refused,"legacy nested writer must not erase compact field as empty candidate bank");
            // Unbound decoding deliberately knows no diamond. A forged nonzero cell
            // outside the later real map must still fail before any candidate use.
            var raw=field.Export();Check(RescueLocalField.TryReadUnbound(50,20,legacy.Current.Tile,raw,out var unbound,out _),"unbound read needs no native/world callbacks");
            Check(!unbound.TryFindCandidates((x,y)=>x==42 && y==12?-1:Tile(x,y),out var none,out _) && none.Count==0,"late clipped invalid cell rejects unbound geometry");
        }
        private sealed class Layout
        {internal int Data,VegetationFlag,WitnessFlag,Witness,Kind,FieldCount,FirstField;}
        private static Layout PendingLayout(byte[] bytes)
        {
            var result=new Layout();
            using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream))
            {
                stream.Position=4;int version=reader.ReadUInt16();Check(version==4 || version==5,"locate supported pending wire version");
                int source=reader.ReadUInt16();result.Data=8+source+12;stream.Position=result.Data+64+20;
                result.VegetationFlag=(int)stream.Position;stream.Position+=5+5+1;
                if(version==5)stream.Position+=3; // Explicit action and old-exit evidence precede crew.
                int crew=reader.ReadInt32();stream.Position+=crew*4;result.WitnessFlag=(int)stream.Position;
                if(reader.ReadBoolean()){result.Witness=(int)stream.Position;Check(RescueGeometryCodec.TryReadWitness(reader,out _),"locate valid nested witness schema");}
                int candidates=reader.ReadInt32();stream.Position+=candidates*16;result.Kind=(int)stream.Position;reader.ReadByte();
                result.FieldCount=(int)stream.Position;reader.ReadInt32();result.FirstField=(int)stream.Position;
            }
            return result;
        }
        private static void VerifyMalformedFrames()
        {
            Check(RescueGeometryCodec.TryEncode(new RescuePendingGeometry("x",Witness(1),10,1,0),out var geometry),"geometry corruption control encodes");
            int body=8+1+12;
            foreach(int length in new[]{0,1,4,6,7,8,body-1,body+16,body+52,geometry.Length-1})
                Check(!RescueGeometryCodec.TryDecode(Shorter(geometry,length),out _),"truncated geometry cannot become a partial spatial proof");
            var trailing=new byte[geometry.Length+1];Array.Copy(geometry,trailing,geometry.Length);
            Check(!RescueGeometryCodec.TryDecode(trailing,out _) && !RescueGeometryCodec.TryDecode(ByteAt(geometry,0,0),out _) &&
                !RescueGeometryCodec.TryDecode(ByteAt(geometry,4,127),out _),"unknown geometry signature/version and trailing payload are rejected");
            Check(!RescueGeometryCodec.TryDecode(ByteAt(geometry,8,255),out _),"geometry source decoding is strict UTF8");
            foreach(int flag in new[]{28,29,46,47,52})Check(!RescueGeometryCodec.TryDecode(ByteAt(geometry,body+flag,2),out _),"geometry booleans must be zero or one");
            foreach(var change in new[]{new[]{body+8,65536+(int)eChimps.CHIMP_TYPE_SPEARMAN},new[]{body+12,9},new[]{body+16,800},
                new[]{body+38,800},new[]{body+53,290},new[]{9,80},new[]{13,4},new[]{17,2},new[]{body+73,41}})
                Check(!RescueGeometryCodec.TryDecode(IntAt(geometry,change[0],change[1]),out _),"geometry type/owner/frame/count/TTL/attempt/delay bounds are enforced");
            int firstCandidate=body+57,secondCandidate=firstCandidate+16;
            Check(!RescueGeometryCodec.TryDecode(IntAt(geometry,secondCandidate+8,BitConverter.ToInt32(geometry,firstCandidate+8)),out _),"duplicate old candidate tile is rejected");
            Check(RescuePendingCodec.TryEncode(Pending(),out var pending),"pending corruption control encodes");var layout=PendingLayout(pending);
            foreach(int length in new[]{0,6,8,layout.Data-1,layout.Witness+16,layout.Kind,layout.FirstField+4,pending.Length-1})
                Check(!RescuePendingCodec.TryDecode(Shorter(pending,length),out _),"truncated joined pending proof cannot lose its nested geometry or typed intent");
            Check(!RescuePendingCodec.TryDecode(ByteAt(pending,4,127),out _) && !RescuePendingCodec.TryDecode(ByteAt(pending,8,255),out _),"pending schema and UTF8 remain strict");
            Check(!RescuePendingCodec.TryDecode(ByteAt(pending,layout.VegetationFlag,2),out _) &&
                !RescuePendingCodec.TryDecode(ByteAt(pending,layout.WitnessFlag,2),out _) &&
                !RescuePendingCodec.TryDecode(ByteAt(pending,layout.Witness+28,2),out _),"outer and nested booleans share strict decoding");
            Check(!RescuePendingCodec.TryDecode(IntAt(pending,layout.Data+4,65536+(int)eChimps.CHIMP_TYPE_SPEARMAN),out _),"pending wide type cannot truncate to a known native enum");
            Check(!RescuePendingCodec.TryDecode(ByteAt(pending,layout.Kind,3),out _) &&
                !RescuePendingCodec.TryDecode(IntAt(pending,layout.FieldCount,1),out _) &&
                !RescuePendingCodec.TryDecode(ByteAt(pending,layout.FirstField+4,3),out _),"unknown typed kind/field schema/width cannot masquerade as a valid intent");
            var changed=Joined(1);changed.Candidates[0]=new RescueCandidate{X=51,Y=20,Tile=Tile(51,20),Distance=0};
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",changed,0,0,0),out _),"joined distance0 cannot be rewritten to the fresh origin");
            changed=Joined(1);changed.GroundFlags^=1;
            Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("x",changed,0,0,0),out _),"joined historical flags must equal the explicit original witness");
        }
        private sealed class World : IWorkerResumeWorld,IRescueMutationWorld
        {
            internal long Session=70,Generation=8;internal int TickValue=100,Rescues;
            internal bool Read(out long session,out long generation){session=Session;generation=Generation;return true;}
            public bool TryCapture(UnitSnapshot before,out WorkerResumeTicket ticket,out string reason){ticket=null;reason=null;return false;}
            public WorkerReceiptState Inspect(WorkerResumeTicket ticket,bool continuation)=>WorkerReceiptState.Unknown;
            public bool TryPark(WorkerResumeTicket ticket,bool continuation)=>throw new InvalidOperationException("Persistence test must not park native units");
            public bool Reissue(WorkerResumeTicket ticket)=>throw new InvalidOperationException("Persistence test must not reissue native orders");
            public bool CanProbeOriginal(WorkerResumeTicket ticket)=>false;
            public bool ProbeOriginal(WorkerResumeTicket ticket)=>throw new InvalidOperationException("No native probes in persistence fixture");
            public bool IsSuccessfulPublication(WorkerResumeTicket ticket)=>false;
            public WorkerOrderObservation ObserveOrderPost(WorkerResumeTicket ticket,long result,out WorkerResumeTicket replacement){replacement=null;return WorkerOrderObservation.Keep;}
            public bool TryCapture(WorldMutationContext context,out List<UnitSnapshot> before,out string reason){before=new List<UnitSnapshot>();reason=null;return true;}
            public bool HasChangedTrapCandidate(UnitSnapshot before)=>true;
            public bool TryRescue(UnitSnapshot before,string cause,bool resume,out RescueResult result){Rescues++;result=new RescueResult{Status=RescueStatus.NoCandidate};return false;}
            public bool TryGetWorld(out long session,out long generation)=>Read(out session,out generation);
            public bool TryGetTick(out int tick){tick=TickValue;return true;}
        }
        private sealed class QuietIntents : IRescueReplanIntentWorld
        {
            public bool TryCapture(int id,int x,int y,int extra,out RescueReplanIntent intent){intent=null;return false;}
            public bool IsCurrent(RescueReplanIntent intent)=>false;
            public bool TryGetSnapshot(RescueReplanIntent intent,out UnitSnapshot exact){exact=null;return false;}
        }
        private static WorkerRecoveryService Service(World world)=>new WorkerRecoveryService(new WorkerResumeSupervisor(world),()=>false,_=>{},world.Read);
        private static SortedDictionary<string,byte[]> Archive(RescuePendingCandidate<UnitSnapshot>[] pending,RescuePendingGeometry[] geometry)
        {
            var service=Service(new World());
            service.CapturePending=(out RescuePendingCandidate<UnitSnapshot>[] value)=>{value=pending;return true;};
            service.CaptureGeometry=(out RescuePendingGeometry[] value)=>{value=geometry;return true;};
            Check(service.TryCaptureArchive(out var entries),"production service emits complete pending+geometry archive");return entries;
        }
        private static void RejectStage(IReadOnlyDictionary<string,byte[]> entries,string reason)
        {
            var world=new World();var service=Service(world);int pending=-1,geometry=-1;
            service.RestorePending=records=>pending=records.Length;service.RestoreGeometry=records=>geometry=records.Length;
            Check(!service.StagePendingRecords(VersionedSaveLoadStatus.Restored,entries),reason);
            service.BeginSession(world.Session,true);
            Check(pending==0 && geometry==0,"rejected archive applies no prefix from either record namespace");
        }
        private static void VerifyArchives()
        {
            var mixed=Archive(new[]{Pending(1)},new[]{Geometry(2)});
            Check(mixed.Count==3 && mixed.ContainsKey("owned-workers") && mixed.ContainsKey("pending/000") && mixed.ContainsKey("geometry/000"),"archive namespaces are deterministic and distinct");
            var overlap=new SortedDictionary<string,byte[]>(mixed,StringComparer.Ordinal);
            RescueGeometryCodec.TryEncode(Geometry(1),out var duplicate);overlap["geometry/000"]=duplicate;
            RejectStage(overlap,"pending and retained geometry cannot own the same unit");
            var duplicatePending=new SortedDictionary<string,byte[]>(mixed,StringComparer.Ordinal){{"pending/001",mixed["pending/000"]}};
            RejectStage(duplicatePending,"duplicate pending unit IDs reject the entire load");
            var duplicateGeometry=new SortedDictionary<string,byte[]>(mixed,StringComparer.Ordinal){{"geometry/001",mixed["geometry/000"]}};
            RejectStage(duplicateGeometry,"duplicate geometry unit IDs reject the entire load");
            var gap=new SortedDictionary<string,byte[]>(mixed,StringComparer.Ordinal);gap.Remove("geometry/000");gap.Add("geometry/001",mixed["geometry/000"]);
            RejectStage(gap,"noncanonical namespace sequence cannot leave a hidden gap");
            var partial=new SortedDictionary<string,byte[]>(mixed,StringComparer.Ordinal){{"geometry/001",new byte[]{1,2,3}}};
            RejectStage(partial,"malformed later geometry cannot apply the earlier valid pending/geometry prefix");
            var service=Service(new World());var pending=new[]{Pending(1)};var geometry=new[]{Geometry(1)};
            service.CapturePending=(out RescuePendingCandidate<UnitSnapshot>[] value)=>{value=pending;return true;};
            service.CaptureGeometry=(out RescuePendingGeometry[] value)=>{value=geometry;return true;};
            Check(!service.TryCaptureArchive(out var rejected) && rejected==null,"overlapping capture publishes no partial dictionary");
            geometry=new[]{Geometry(2),new RescuePendingGeometry("x",Witness(3),80,0,0)};
            Check(!service.TryCaptureArchive(out rejected) && rejected==null,"invalid final geometry invalidates every archive entry atomically");
            geometry=new RescuePendingGeometry[511];for(int i=0;i<geometry.Length;i++)geometry[i]=Geometry(i+2);
            Check(service.TryCaptureArchive(out var maximum) && maximum.Count==513,"one pending plus511 geometry fits the shared512 budget");
            var receiver=Service(new World());Check(receiver.StagePendingRecords(VersionedSaveLoadStatus.Restored,maximum),"maximum mixed archive is accepted on load");
            var over=new RescuePendingGeometry[512];Array.Copy(geometry,over,511);over[511]=Geometry(513);geometry=over;
            Check(!service.TryCaptureArchive(out rejected) && rejected==null,"513 combined units are rejected before archive publication");
            var oversized=new SortedDictionary<string,byte[]>(maximum,StringComparer.Ordinal);RescueGeometryCodec.TryEncode(Geometry(513),out var extra);oversized.Add("geometry/511",extra);
            RejectStage(oversized,"513 combined restored units exceed the shared budget");
        }
        private static UnitMoveContext MoveContext(World world,int id)=>(UnitMoveContext)Activator.CreateInstance(typeof(UnitMoveContext),
            BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{1L,0L,world.Session,world.Generation,id,80,20,0,null},null);
        private static UnitMoveCompletion Refused(UnitMoveContext context)
        {
            var result=(UnitMoveCompletion)Activator.CreateInstance(typeof(UnitMoveCompletion),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{context},null);
            typeof(UnitMoveCompletion).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(result,new object[]{true,true,0L,null,true});return result;
        }
        private static void VerifyServiceObserverLifecycle()
        {
            var original=Archive(new[]{Pending(1)},new[]{Geometry(2)});var world=new World();var service=Service(world);
            var observer=new GeneralRescueObserver(world,_=>{},_=>{}){ReplanIntents=new QuietIntents()};
            service.CapturePending=observer.TryCapturePending;service.CaptureGeometry=observer.TryCaptureGeometry;
            service.RestorePending=observer.StagePending;service.RestoreGeometry=observer.StageGeometry;
            service.StageOwnedRecord(VersionedSaveLoadStatus.Restored,original["owned-workers"]);
            Check(service.StagePendingRecords(VersionedSaveLoadStatus.Restored,original) && observer.PendingCount==0,"load staging alone cannot bind or execute native recovery");
            // Decoded records own their arrays; changes to the input archive after staging cannot alter them.
            original["geometry/000"][0]^=1;
            observer.SetSession(world.Session);service.BeginSession(world.Session,true);
            Check(observer.PendingCount==2 && world.Rescues==0 && service.Ready,"begin applies managed staged proofs without native work");
            Check(service.AdmitTick(world.TickValue) && service.TickValue==0,"loaded proofs bind their clock to the new world's first admitted tick");
            observer.Tick(world.TickValue);
            Check(observer.TryCaptureGeometry(out var bound) && bound.Length==1 && bound[0].Age==25 && bound[0].Attempts==2,
                "geometry binds to the fresh world without resetting original age/attempts");
            Check(service.TryCaptureArchive(out var current),"actual service and observer recapture both restored record kinds");
            Check(RescueCompactArchiveCodec.TryDecode(current,out var restoredPending,out var restoredGeometry,out _) &&
                restoredPending.Length==1 && restoredGeometry.Length==1 && restoredGeometry[0].Age==25 && restoredGeometry[0].Attempts==2,
                "active-window recapture preserves both restored proof kinds in the compact archive");
            observer.ObserveOrder(2);var context=MoveContext(world,2);var call=observer.TryBegin(context);
            Check(call!=null && observer.HasUnsettledReplans,"external original creates the actual general-observer unsettled state");
            Check(!service.TryCaptureArchive(out var blocked) && blocked==null,"source callback false blocks complete atomic archive during unsettled Pre");
            call.Complete(Refused(context));
            Check(observer.HasUnsettledReplans && !service.TryCaptureArchive(out blocked) && blocked==null,"original completion is not the final caller-tail boundary for saving");
            world.TickValue++;service.AdmitTick(world.TickValue);observer.Tick(world.TickValue);
            Check(!observer.HasUnsettledReplans && service.TryCaptureArchive(out current),"first admitted tick settles source metadata before archive capture");
            Check(observer.TryCaptureGeometry(out bound) && bound.Length==1 && bound[0].Age==26 && bound[0].Attempts==2,
                "new order and rejected fresh intent do not refresh geometry lifetime or attempts");
            world.Generation++;world.TickValue++;Check(service.AdmitTick(world.TickValue) && service.TickValue==0,"native world replacement rebinds service clock");
            observer.Tick(world.TickValue);
            Check(observer.PendingCount==0 && observer.TryCaptureGeometry(out bound) && bound.Length==0,"old-world pending and retained geometry cannot survive replacement");
            observer.SetSession(0);service.EndSession();world.Session++;observer.SetSession(world.Session);
            service.StageOwnedRecord(VersionedSaveLoadStatus.NoRecord,null);service.StagePendingRecords(VersionedSaveLoadStatus.NoRecord,current);service.BeginSession(world.Session,true);
            Check(observer.PendingCount==0 && service.Ready,"next no-record session cannot inherit earlier staged geometry or intent");
        }
    }
}

using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class CommandJourneyFixture
    {
        private static int checks;
        private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException(reason);}
        internal sealed class Memory
        {
            internal readonly byte[] Data=new byte[0xAEC];
            internal ushort Word(int at)=>(ushort)(Data[at]|Data[at+1]<<8);
            internal uint Dword(int at)=>(uint)(Word(at)|(uint)Word(at+2)<<16);
            internal void Word(int at,uint value){Data[at]=(byte)value;Data[at+1]=(byte)(value>>8);}
            internal void Dword(int at,uint value){Word(at,value);Word(at+2,value>>16);}
            internal Memory(eChimps type,int state,uint global=42)
            {
                Word(0x6E4,(uint)AliveState.IsAlive);Word(0x6E6,(uint)type);Word(0x6EE,2);Dword(0x6F0,global);Word(0x918,(uint)state);
                Word(0x930,2);Dword(0x93C,33);Word(0x956,3);Word(0xA9E,1);
                int count=CommandJourneySnapshot.RequiredCrewCount(type);Word(0xA0C,(uint)count);
                for(int i=0;i<count;i++){Word(0x96C+i*2,(uint)(100+i));Dword(0x974+i*4,0x10000u+(uint)i);}
            }
        }
        internal static void Run()
        {
            checks=0;
            foreach(eChimps type in Enum.GetValues(typeof(eChimps)))
            {
                for(int state=0;state<=101;state++)
                {
                    var memory=new Memory(type,state);
                    bool rangedWithoutIdleOne=type==eChimps.CHIMP_TYPE_ARCHER || type==eChimps.CHIMP_TYPE_ARAB_BOW ||
                        type==eChimps.CHIMP_TYPE_ARAB_SLINGER || type==eChimps.CHIMP_TYPE_ARAB_GRENADIER ||
                        type==eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER || type==eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER;
                    bool expected=CommandResumePolicy.IsKnownType(type) && type!=eChimps.CHIMP_TYPE_TREBUCHET &&
                        (state==101 || type!=eChimps.CHIMP_TYPE_ENGINEER &&
                        (state==0 || state==1 && !rangedWithoutIdleOne));
                    bool captured=CommandJourneySnapshot.TryCapture(type,state,memory.Word,memory.Dword,out var snapshot);
                    Check(captured==expected,"explicit type and functional phase matrix");
                    bool stationaryExpected=expected && (state==0 || state==1) || type==eChimps.CHIMP_TYPE_SPEARMAN && state==2 ||
                        type==eChimps.CHIMP_TYPE_ARCHER && (state==7 || state==8 || state==11);
                    Check(CommandJourneySnapshot.IsSupportedStationaryPhase(type,state)==stationaryExpected,
                        "stationary idle admission does not inherit completed contact phases");
                    Check(CommandJourneySnapshot.TryCaptureStationary(type,state,memory.Word,memory.Dword,out _)==stationaryExpected,
                        "stationary capture checks the independent idle phase matrix");
                    if(!captured)continue;
                    Check(snapshot.Matches(type,state,memory.Word,memory.Dword),"unchanged command matches");
                    for(int field=0;field<snapshot.FieldCount;field++)
                    {
                        var descriptor=snapshot.Field(field);byte original=memory.Data[descriptor.NativeOffset];memory.Data[descriptor.NativeOffset]^=1;
                        Check(!snapshot.Matches(type,state,memory.Word,memory.Dword),"every captured command and crew field is exact");
                        memory.Data[descriptor.NativeOffset]=original;
                    }
                    memory.Word(0x74E,5);memory.Word(0x750,8);memory.Word(0x9C8,1);memory.Word(0x9BA,44);
                    memory.Dword(0x908,123);memory.Dword(0x9DC,999);memory.Word(0x9A2,99);
                    Check(snapshot.Matches(type,state,memory.Word,memory.Dword),"route, ammo, timers and movement do not become command identity");
                    var values=snapshot.CopyValues();var offsets=new int[values.Length];var widths=new int[values.Length];
                    for(int i=0;i<values.Length;i++){offsets[i]=snapshot.Field(i).NativeOffset;widths[i]=snapshot.Field(i).Width;}
                    Check(CommandJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out var restored) &&
                        restored.Matches(type,state,memory.Word,memory.Dword),"strict command field schema restores");
                    values[0]=0;Check(restored.TryGetValue(0x6E4,out uint alive) && alive==(uint)AliveState.IsAlive,"restored command values immutable");
                    widths[0]=4;Check(!CommandJourneySnapshot.TryRestoreKnownSnapshot(type,state,offsets,widths,values,out _),"mutated command schema fails");
                }
            }
            var engineer=new Memory(eChimps.CHIMP_TYPE_ENGINEER,101);engineer.Word(0x9E0,1);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_ENGINEER,101,engineer.Word,engineer.Dword,out _),"engineer job payload requires its native owner");
            var stationary=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,2);
            Check(CommandJourneySnapshot.TryCaptureStationary(eChimps.CHIMP_TYPE_SPEARMAN,2,stationary.Word,stationary.Dword,out var idle),"separate Spearman2 animation idle proof");
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_SPEARMAN,2,stationary.Word,stationary.Dword,out _),"stationary proof cannot grant an active resume phase");
            var idleOffsets=new int[idle.FieldCount];var idleWidths=new int[idle.FieldCount];
            for(int i=0;i<idle.FieldCount;i++){idleOffsets[i]=idle.Field(i).NativeOffset;idleWidths[i]=idle.Field(i).Width;}
            Check(CommandJourneySnapshot.TryRestoreKnownSnapshot(idle.UnitType,idle.AIState,idleOffsets,idleWidths,idle.CopyValues(),out var restoredIdle) &&
                restoredIdle.Matches(idle.UnitType,idle.AIState,stationary.Word,stationary.Dword),"stationary-only schema restores without granting continuation");
            Check(!WorkerResumeTicket.ForCommand(7,42,idle.UnitType,2,2,2,33,20,30,restoredIdle).IsValid,"stationary-only snapshot cannot become an owned active command receipt");
            stationary.Word(0x9F4,(uint)TribeAICommand.DigMoatTileId);
            Check(!CommandJourneySnapshot.TryCaptureStationary(eChimps.CHIMP_TYPE_SPEARMAN,2,stationary.Word,stationary.Dword,out _),"stationary dig intent fails the same semantic guard");
            var dig=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);dig.Word(0x9F4,(uint)TribeAICommand.DigMoatTileId);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_SPEARMAN,101,dig.Word,dig.Dword,out _),"dig intent is not a plain move");
            dig.Word(0x9F4,0);dig.Word(0x956,6);
            Check(CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_SPEARMAN,101,dig.Word,dig.Dword,out _),"work-mode flags are not the native dig command selector");
            var siege=new Memory(eChimps.CHIMP_TYPE_CATAPULT,101);siege.Word(0xA0C,1);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_CATAPULT,101,siege.Word,siege.Dword,out _),"missing crew cannot be adopted");
            siege.Word(0xA0C,2);siege.Word(0x96E,100);
            Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_CATAPULT,101,siege.Word,siege.Dword,out _),"duplicate crew cannot be adopted");
            var actor=new Memory(eChimps.CHIMP_TYPE_ARCHER,101,42);
            Check(CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_ARCHER,101,actor.Word,actor.Dword,out var command),"command ticket fixture");
            var ticket=WorkerResumeTicket.ForCommand(7,42,eChimps.CHIMP_TYPE_ARCHER,101,2,2,33,20,30,command);
            Check(ticket.IsValid && ticket.Kind==ResumeReceiptKind.Command && ticket.Job==null && ticket.Bindings.IsEmpty,"command receipt contains no invented worker job");
            var ledger=new WorkerResumeLedger();Check(ledger.TryReserve(ticket,out var token) && ledger.CommitPark(token,0),"reserve command before mutation");
            Check(ledger.TrySerialize(out var encoded),"serialize explicit command kind");
            var loaded=new WorkerResumeLedger();Check(loaded.TryRestore(encoded,100) && loaded.TryGetParkedByUnit(7,out var restoredTicket) &&
                restoredTicket.Kind==ResumeReceiptKind.Command && restoredTicket.Job==null && restoredTicket.Command.Matches(eChimps.CHIMP_TYPE_ARCHER,101,actor.Word,actor.Dword),"command codec roundtrip");
            var unknown=(byte[])encoded.Clone();unknown[45]=255;Check(!loaded.TryRestore(unknown,100) && loaded.Count==1,"unknown kind cannot replace existing ownership");
            var masquerade=(byte[])encoded.Clone();masquerade[45]=(byte)ResumeReceiptKind.Worker;
            Check(!loaded.TryRestore(masquerade,100),"command cannot masquerade as worker schema");
            var wideType=(byte[])encoded.Clone();wideType[20]=1;
            Check(!loaded.TryRestore(wideType,100),"wide serialized enum cannot truncate into an admitted type");
            var invalidId=(byte[])encoded.Clone();Array.Copy(BitConverter.GetBytes(NativeRelocation.MaximumUnitId+1),0,invalidId,10,4);
            Check(!loaded.TryRestore(invalidId,100),"out-of-range native unit identity rejected before adoption");
            VerifyNativeFailedOrderHandoff();
            VerifyArcherStationary();
            Console.WriteLine("PASS: command journey "+checks+" checks; explicit phases, crew, intent schemas and command-kind codec; no game execution.");
        }
        private static void VerifyArcherStationary()
        {
            var original=new Memory(eChimps.CHIMP_TYPE_ARCHER,0);
            Check(CommandJourneySnapshot.TryCaptureStationary(eChimps.CHIMP_TYPE_ARCHER,0,original.Word,original.Dword,out var old),"existing Archer0 schema");
            foreach(int state in new[]{7,8,11})foreach(uint command in new[]{(uint)TribeAICommand.Unknown0,(uint)TribeAICommand.MoveHerePosition})
            {
                var memory=new Memory(eChimps.CHIMP_TYPE_ARCHER,state);memory.Word(0x9F4,command);
                Check(CommandJourneySnapshot.TryCaptureStationary(eChimps.CHIMP_TYPE_ARCHER,state,memory.Word,memory.Dword,out var snapshot),"audited Archer idle animation has stationary intent");
                Check(snapshot.FieldCount==old.FieldCount+2 && snapshot.Field(old.FieldCount).NativeOffset==0x748 &&
                    snapshot.Field(old.FieldCount+1).NativeOffset==0x74A,"only new idle schema adds deferred-goal evidence");
                Check(!CommandJourneySnapshot.TryCapture(eChimps.CHIMP_TYPE_ARCHER,state,memory.Word,memory.Dword,out _) &&
                    !WorkerResumeTicket.ForCommand(7,42,eChimps.CHIMP_TYPE_ARCHER,state,2,2,33,20,30,snapshot).IsValid,
                    "stationary Archer animation cannot become an active/parked worker command receipt");
                var offsets=new int[snapshot.FieldCount];var widths=new int[snapshot.FieldCount];
                for(int i=0;i<offsets.Length;i++){offsets[i]=snapshot.Field(i).NativeOffset;widths[i]=snapshot.Field(i).Width;}
                Check(CommandJourneySnapshot.TryRestoreKnownSnapshot(snapshot.UnitType,state,offsets,widths,snapshot.CopyValues(),out var restored) &&
                    restored.Matches(snapshot.UnitType,state,memory.Word,memory.Dword),"new stationary schema roundtrip preserves immutable idle proof");
                var pending=new UnitSnapshot{Id=7,GlobalId=42,Type=(int)snapshot.UnitType,Owner=2,Tribe=2,TribeGlobalId=33,Health=100,
                    X=10,Y=10,TileId=100,NextX=10,NextY=10,NextTileId=100,RequestedX=10,RequestedY=10,
                    AIState=state,Flags=0,Substep=8,CurrentWasOrdinary=true,AffectedCurrent=true,CrewIdentity=new uint[0],Command=snapshot};
                pending.Candidates.Add(new RescueCandidate{X=11,Y=10,Tile=101,Distance=1});
                Check(RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("EditorLand",pending,0,0,0),out var encodedIdle) &&
                    RescuePendingCodec.TryDecode(encodedIdle,out var pendingCopy) &&
                    pendingCopy.Snapshot.Command.Matches(snapshot.UnitType,state,memory.Word,memory.Dword),
                    "actual pending codec roundtrip retains new Archer idle no-intent schema");
                var changedIdle=(byte[])encodedIdle.Clone();changedIdle[changedIdle.Length-4]=1;
                Check(!RescuePendingCodec.TryDecode(changedIdle,out _),"actual pending codec rejects modified last deferred-goal field");
                pending.Flags=2;
                Check(!RescuePendingCodec.TryEncode(new RescuePendingCandidate<UnitSnapshot>("EditorLand",pending,0,0,0),out _),
                    "new idle phase cannot enter active pending-command persistence");
                foreach(int at in new[]{0x996,0x99C,0x9F6,0x748,0x74A})foreach(uint value in new uint[]{1,0xFFFE,0xFFFF})
                {
                    memory.Word(at,value);
                    Check(!snapshot.Matches(snapshot.UnitType,state,memory.Word,memory.Dword) &&
                        !CommandJourneySnapshot.TryCaptureStationary(snapshot.UnitType,state,memory.Word,memory.Dword,out _),"live or sentinel target/deferred intent defeats stationary proof");
                    memory.Word(at,0);
                }
                foreach(int at in new[]{0x998,0x6F8,0x9F8,0xA00})
                {
                    memory.Dword(at,0x10000);
                    Check(!CommandJourneySnapshot.TryCaptureStationary(snapshot.UnitType,state,memory.Word,memory.Dword,out _),"full-width target lifetime cannot be ignored");memory.Dword(at,0);
                }
                foreach(var cmd in new[]{TribeAICommand.AttackUnit,TribeAICommand.AttackTilePosition,TribeAICommand.DigMoatTileId,
                    TribeAICommand.Unknown7,TribeAICommand.AttackBuilding,TribeAICommand.Unknown22,TribeAICommand.AttackWallTileId,TribeAICommand.Unknown34})
                {
                    memory.Word(0x9F4,(uint)cmd);
                    Check(!CommandJourneySnapshot.TryCaptureStationary(snapshot.UnitType,state,memory.Word,memory.Dword,out _),"explicit attack/work command cannot masquerade as no-target idle");
                }
                memory.Word(0x9F4,command);
                var values=snapshot.CopyValues();values[values.Length-1]=1;
                Check(!CommandJourneySnapshot.TryRestoreKnownSnapshot(snapshot.UnitType,state,offsets,widths,values,out _),"restored new idle proof rejects deferred movement");
                values=snapshot.CopyValues();int index=Array.IndexOf(offsets,0x99C);values[index]=0xFFFF;
                Check(!CommandJourneySnapshot.TryRestoreKnownSnapshot(snapshot.UnitType,state,offsets,widths,values,out _),"restored idle proof rejects ranged tile sentinel");
                memory.Word(0x918,0);
                Check(!snapshot.Matches(snapshot.UnitType,0,memory.Word,memory.Dword),"natural Archer AI transition supersedes its old immutable idle proof");
            }
            Check(!CommandJourneySnapshot.IsSupportedStationaryPhase(eChimps.CHIMP_TYPE_ARCHER,1),"absent native ArcherAI1 remains excluded");
        }
        private static int Tile(int x,int y)=>x>=0 && x<800 && y>=0 && y<800?y*800+x:-1;
        private static void HandoffPose(Memory memory,int flags)
        {
            memory.Dword(0xA20,100);memory.Word(0x74E,(uint)flags);memory.Word(0x750,8);memory.Dword(0x740,0);
            memory.Word(0x71C,4);memory.Word(0x71E,5);memory.Dword(0x72C,(uint)Tile(4,5));
            memory.Word(0x738,4);memory.Word(0x73A,5);memory.Dword(0x73C,(uint)Tile(4,5));
            memory.Word(0x744,90);memory.Word(0x746,91);
        }
        private static WorkerResumeTicket CommandTicket(Memory memory,eChimps type,int state)
        {
            Check(CommandJourneySnapshot.TryCapture(type,state,memory.Word,memory.Dword,out var snapshot),"production handoff initial command capture");
            return WorkerResumeTicket.ForCommand(7,42,type,state,2,2,33,20,30,snapshot);
        }
        private static bool Prepare(Memory memory,WorkerResumeTicket ticket,long result,out WorkerResumeTicket replacement)=>
            NativeWorkerResumeWorld.TryPrepareFailedOrderHandoff(ticket,result,memory.Word,memory.Dword,Tile,33,out replacement);
        private static void VerifyNativeFailedOrderHandoff()
        {
            // This invokes the actual adapter's pure reader core, not FakeWorld's
            // model of the adapter. Native writes/lifetime lookups remain game tests.
            foreach(int flags in new[]{0,2,4,5})
            {
                var memory=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);var ticket=CommandTicket(memory,eChimps.CHIMP_TYPE_SPEARMAN,101);
                HandoffPose(memory,flags);memory.Word(0x956,9);var bytes=(byte[])memory.Data.Clone();
                Check(Prepare(memory,ticket,0,out var fresh),"actual0 accepts coherent stale flags and fresh command context");
                Check(fresh.RequestedX==90 && fresh.RequestedY==91 && ticket.RequestedX==20 && ticket.RequestedY==30,
                    "handoff retains live new goal and never edits old receipt");
                Check(fresh.Command.Matches(fresh.UnitType,fresh.AIState,memory.Word,memory.Dword),"handoff uses the fresh semantic snapshot");
                for(int i=0;i<bytes.Length;i++)Check(bytes[i]==memory.Data[i],"production handoff preparation is read-only");
                // Rescue normalization may retain the old accepted Next cache.
                // A failed foreign order before the next step must still adopt
                // its fresh live goal, with all actual pose/identity guards intact.
                foreach(uint inactiveNext in new uint[]{(uint)Tile(5,5),0,uint.MaxValue})
                {
                    memory.Dword(0x73C,inactiveNext);
                    Check(Prepare(memory,ticket,0,out var staleNext) && staleNext.RequestedX==90 && staleNext.RequestedY==91 &&
                        staleNext.Command.Matches(staleNext.UnitType,staleNext.AIState,memory.Word,memory.Dword),
                        "failed order adopts fresh intent with inactive packed Next for normalized flags0/2/4/5");
                }
                Check(!Prepare(memory,ticket,1,out _) && !Prepare(memory,ticket,-1,out _),"no refusal handoff from positive or unsupported native result");
                memory.Word(0x918,0);
                Check(Prepare(memory,ticket,0,out _)==(flags==5),"AI change needs the already owned native barrier");
            }
            var guardOffsets=new[]{0x6E4,0x6E6,0x6EE,0x930,0x6EC,0x9B0,0x8EC,0x8F6,0x8F8,0x750,0x71C,0x71E,0x738,0x73A,0xA9E};
            foreach(int at in guardOffsets)
            {
                var memory=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);var ticket=CommandTicket(memory,eChimps.CHIMP_TYPE_SPEARMAN,101);HandoffPose(memory,2);
                memory.Word(at,(uint)(memory.Word(at)^1));Check(!Prepare(memory,ticket,0,out _),"actual handoff rejects changed identity/mode/pose/capability");
            }
            foreach(int at in new[]{0x6F0,0x93C,0x740,0x72C})
            {
                var memory=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);var ticket=CommandTicket(memory,eChimps.CHIMP_TYPE_SPEARMAN,101);HandoffPose(memory,2);
                memory.Dword(at,memory.Dword(at)^0x10000u);Check(!Prepare(memory,ticket,0,out _),"full-width identity/pose read cannot ignore high bits");
            }
            foreach(int flags in new[]{1,3,6,255})
            {
                var memory=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);var ticket=CommandTicket(memory,eChimps.CHIMP_TYPE_SPEARMAN,101);HandoffPose(memory,flags);
                Check(!Prepare(memory,ticket,0,out _),"special path states cannot be adopted as failed order handoff");
            }
            var invalid=new Memory(eChimps.CHIMP_TYPE_SPEARMAN,101);var original=CommandTicket(invalid,eChimps.CHIMP_TYPE_SPEARMAN,101);HandoffPose(invalid,2);
            invalid.Word(0x744,800);Check(!Prepare(invalid,original,0,out _),"new goal must have a valid native tile");invalid.Word(0x744,90);
            invalid.Word(0x9C8,2);Check(!Prepare(invalid,original,0,out _),"unknown movement permission cannot inherit receipt");invalid.Word(0x9C8,1);
            Check(Prepare(invalid,original,0,out _),"fresh legitimate native moat marker can retain owned barrier");invalid.Dword(0xA20,0);
            Check(!Prepare(invalid,original,0,out _),"dead unit cannot be parked");
            var siege=new Memory(eChimps.CHIMP_TYPE_CATAPULT,101);var engine=CommandTicket(siege,eChimps.CHIMP_TYPE_CATAPULT,101);HandoffPose(siege,2);
            siege.Dword(0x978,siege.Dword(0x978)+1);Check(!Prepare(siege,engine,0,out _),"new crew lifetime cannot inherit old engine receipt");
            var types=new System.Collections.Generic.HashSet<eChimps>();
            foreach(eChimps type in Enum.GetValues(typeof(eChimps)))for(int state=0;state<=102;state++)
            {
                if(!WorkerTravelPolicy.TryDescribe(type,state,out var rule) || !rule.CanResumeOrdinary)continue;
                types.Add(type);var memory=new Memory(type,state);HandoffPose(memory,2);
                memory.Word(0x990,11);memory.Dword(0x9C0,111);memory.Word(0x98E,12);memory.Dword(0x6F8,112);
                memory.Word(0x9A0,13);memory.Dword(0x700,113);
                Check(WorkerJourneySnapshot.TryCapture(rule,memory.Word,memory.Dword,out var job),"worker handoff selected active job capture");
                var required=rule.RequiredExternalBindings;
                var bindings=new WorkerBindingSnapshot(required,(required&WorkerExternalBindings.Workplace)!=0?11:0,(required&WorkerExternalBindings.Workplace)!=0?111u:0,
                    (required&WorkerExternalBindings.Store)!=0?12:0,(required&WorkerExternalBindings.Store)!=0?112u:0,
                    (required&WorkerExternalBindings.Vegetation)!=0?13:0,(required&WorkerExternalBindings.Vegetation)!=0?113u:0);
                var ticket=new WorkerResumeTicket(7,42,type,state,2,2,33,20,30,job,bindings);
                Check(ticket.IsValid && Prepare(memory,ticket,0,out var fresh) && fresh.RequestedX==90 && fresh.RequestedY==91,
                    "all admitted worker travel segments hand off their live new goal");
                memory.Word(0x9E0,1);Check(!Prepare(memory,ticket,0,out _),"changed worker job/cargo cannot inherit failed external order");
            }
            Check(types.Count==1 && types.Contains(eChimps.CHIMP_TYPE_QUARRY_OX),"production worker handoff is restricted to Ox");
            Check(!NativeWorkerResumeWorld.TryPrepareFailedOrderHandoff(original,0,_=>throw new InvalidOperationException(),invalid.Dword,Tile,33,out _),
                "failed reader never admits a partial handoff");
        }
    }
}

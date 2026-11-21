using System;
using System.Runtime.InteropServices;
using System.Threading;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace DesyncTest
{
    internal sealed unsafe partial class NativeCapture
    {
        internal readonly Recorder Recorder;
        internal readonly byte* Base;
        private readonly int imageSize;
        [ThreadStatic] private static NativeCapture dispatchOwner;
        [ThreadStatic] private static CaptureEvent dispatchContext;
        [ThreadStatic] private static bool dispatchContextValid;
        private int lastSample=-1,lastCheckpoint=-1;
        private int lastCheckpointMode=-1,lastCheckpointCommand=-1;
        private int lastWorkerTick=-1, workerHighWater;
        private int workerDecisionsDisabled;
        private readonly WorkerMark[] workerMarks;
        internal bool UnitContract=true;
        // All offsets are pinned to the audited DLL, not inferred from mutable struct comments.
        internal const int Manager=0x8574320, TickRva=0x3665F58, WireUnits=0x67E8A58, Paths=0x7338278;
        internal const int UnitChecksumSlots = 9999;
        internal const int BuildingChecksumSlots = 3999, BuildingRecordSize = 0x32C, BuildingRecords = 0x64CCC0C;
        internal const int BuildingChecksumsOffset = 0x1A6F0C;
        private const int MaximumBuildingRecordsPerCheckpoint = 512;
        private readonly uint[] recordedBuildingHashes = new uint[BuildingChecksumSlots + 1];
        private readonly bool[] recordedBuildings = new bool[BuildingChecksumSlots + 1];
        private const int UnitManager = 0x67E8400;
        private const int WorkerPayloadSize = 28;

        [StructLayout(LayoutKind.Sequential, Pack=1)]
        private struct WorkerMark
        {
            internal uint Identity, WorkplaceGlobal;
            internal ushort Kind, PendingKind, Workplace, State, WorkMode, Alive, X, Y, TargetX, TargetY;
            internal ushort Player; // Owner in the common record header, not in the format-2 payload.

            internal bool SameAssignment(WorkerMark other) => Identity==other.Identity && WorkplaceGlobal==other.WorkplaceGlobal &&
                Kind==other.Kind && PendingKind==other.PendingKind && Workplace==other.Workplace &&
                State==other.State && WorkMode==other.WorkMode && Alive==other.Alive && Player==other.Player;
        }
        internal NativeCapture(Recorder recorder,IntPtr module,int size)
        {
            Recorder=recorder;Base=(byte*)module;imageSize=size;
            if(size<Manager+0x370BFC)throw new InvalidOperationException("Native image lacks audited data ranges.");
            if(Marshal.SizeOf<GameBuilding>()!=BuildingRecordSize ||
                Marshal.OffsetOf<GameBuilding>(nameof(GameBuilding.r_GlobalId)).ToInt32()!=0xD8)
                throw new InvalidOperationException("Building trace layout differs from the audited layout.");
            if(recorder.Options.WorkerPlayer!=0)
            {
                ValidateWorkerFields();
                workerMarks=new WorkerMark[GameUnitManager.NativeUnitSlotCount];
            }
        }
        internal int Tick => *(int*)(Base+TickRva);
        internal byte* M => Base+Manager;
        internal bool Active => Recorder.Active;
        internal bool ObserveWorker(int player) =>
            Active && Volatile.Read(ref workerDecisionsDisabled)==0 &&
            (Recorder.Options.WorkerPlayer == -1 || Recorder.Options.WorkerPlayer == player && player != 0);
        internal void DisableWorkerDecisions() => Volatile.Write(ref workerDecisionsDisabled,1);

        internal void WorkerDecision(int building,int player,int role,int ordinal,int entranceCalls,int result) =>
            RecordWorkerDecision(RecordKind.WorkerDecision,building,player,role,ordinal,entranceCalls,0,0,result);

        internal void WorkerEntrance(int building,int player,int role,int ordinal,int call,int requested,int expansion,int result) =>
            RecordWorkerDecision(RecordKind.WorkerEntrance,building,player,role,ordinal,call,requested,expansion,result);

        private void RecordWorkerDecision(RecordKind kind,int building,int player,int role,int ordinal,int call,int requested,int expansion,int result)
        {
            if(building < 1 || building > BuildingChecksumSlots)
            {
                Interlocked.Increment(ref Recorder.CallbackFailures);
                return;
            }
            byte* record=Base+BuildingRecords+building*BuildingRecordSize;
            int* detail=stackalloc int[6];
            detail[0]=result;
            detail[1]=requested;
            detail[2]=expansion;
            detail[3]=*(short*)(record+0xFC);
            detail[4]=*(short*)(record+0xFE);
            detail[5]=*(short*)(record+0x100);
            Recorder.Record(new CaptureEvent{Kind=kind,Tick=Tick,Phase=11,Opcode=-1,Player=player,Command=role,Planned=ordinal,
                Unit=building,Global=*(int*)(record+0xD8),Stage=call},(IntPtr)detail,6*sizeof(int));
        }
        internal void ResetSession()
        {
            lastSample=-1;lastCheckpoint=-1;lastCheckpointMode=-1;lastCheckpointCommand=-1;
            lastWorkerTick=-1;workerHighWater=0;
            terrainTick=-1;terrainPass=0;
            commandTick=-1;commandsThisTick=0;
            if(workerMarks!=null)Array.Clear(workerMarks,0,workerMarks.Length);
            Array.Clear(recordedBuildings,0,recordedBuildings.Length);
        }

        private static void ValidateWorkerFields()
        {
            (string Name,int Offset)[] fields = {
                (nameof(GameUnit.r_AliveState),0x88), (nameof(GameUnit.r_UnitChimp),0x8A),
                (nameof(GameUnit.r_ControllableForPlayerId),0x92), (nameof(GameUnit.r_GlobalId),0x94),
                (nameof(GameUnit.r_TransformIntoUnitOfType),0x2C6), (nameof(GameUnit.r_AIState),0x2BC),
                (nameof(GameUnit.r_WorkModeFlags),0x2FA), (nameof(GameUnit.r_LinkedProductionBuildingId),0x334),
                (nameof(GameUnit.r_LinkedProductionBuildingGlobalId),0x364),
                (nameof(GameUnit.r_CurrentTilePositionX),0xC0), (nameof(GameUnit.r_CurrentTilePositionY),0xC2),
                (nameof(GameUnit.r_TargetTilePositionX),0xC4), (nameof(GameUnit.r_TargetTilePositionY),0xC6)
            };
            if(Marshal.OffsetOf<WorkerMark>(nameof(WorkerMark.Player)).ToInt32()!=WorkerPayloadSize || Marshal.OffsetOf<GameUnitManager>(nameof(GameUnitManager.r_NextUnitId)).ToInt32()!=0 ||
                Marshal.SizeOf<GameUnit>()!=UnitChecksum.RecordSize || UnitManager+0x65C!=WireUnits+4)
                throw new InvalidOperationException("Worker trace layout differs from the audited layout.");
            foreach(var field in fields)
                if(Marshal.OffsetOf<GameUnit>(field.Name).ToInt32()!=field.Offset)
                    throw new InvalidOperationException("Worker trace field layout differs: "+field.Name);
        }
        internal CaptureEvent Context(RecordKind kind,int stage=0)
        {
            var e=new CaptureEvent{Kind=kind,Tick=Tick,Phase=*(int*)(M+0x84CCC),Opcode=-1,Player=*(int*)(M+0x36FA4C),Command=-1,Planned=-1,Stage=stage};
            int slot=*(int*)(M+0x84CC8);
            if(slot>=0&&slot<500)
            {byte* p=M+slot*0x500;e.Opcode=*(sbyte*)(p+0xB0C00);e.Command=*(int*)(p+0xB0C04);e.Planned=*(int*)(p+0xB0BF8);if(e.Phase!=0)e.Player=*(int*)(p+0xB0BFC);}
            return e;
        }
        internal void Dispatch(IntPtr target)
        {
            // The live table can contain a managed thunk (SE's Chore 106). Invoke
            // its audited native ABI directly so Mono never casts its original delegate.
            var original = (delegate* unmanaged[Cdecl]<void>)(void*)target;
            CaptureEvent e=default;bool valid=false;
            if(Active)try {e=Context(RecordKind.Dispatch);valid=true;Recorder.Record(e,IntPtr.Zero,0);}catch{Interlocked.Increment(ref Recorder.CallbackFailures);}
            var previousOwner=dispatchOwner;var previousContext=dispatchContext;bool previousValid=dispatchContextValid;
            dispatchOwner=this;dispatchContext=e;dispatchContextValid=valid;
            bool commandObserved=false;
            try
            {
              if(valid&&e.Phase==0&&e.Opcode==106)
              {
                  try{commandObserved=BeginCommand(e);}
                  catch{Interlocked.Increment(ref Recorder.CallbackFailures);}
              }
              original(); // Always exactly once; capture failures never suppress Vanilla.
              if(Active&&valid)try
              {
                if(commandObserved)EndCommand(e);
                e.Stage=1;Recorder.Record(e,IntPtr.Zero,0);
                if(e.Phase==0&&e.Opcode==56)CaptureUnit(*(int*)(M+0x14D00C),4,RecordKind.Unit,e.Phase);
                if(e.Phase==0&&e.Opcode==101)Recorder.Record(Context(RecordKind.CategoryFlags),(IntPtr)(M+0x14D34C),14);
              }catch{Interlocked.Increment(ref Recorder.CallbackFailures);}
            }
            finally
            {
                if(commandObserved)Volatile.Write(ref commandBusy,0);
                dispatchOwner=previousOwner;dispatchContext=previousContext;dispatchContextValid=previousValid;
            }
        }
        internal void Arrival(int player,IntPtr data,int length)
        {
            if(!Active||length<1||length>180000||data==IntPtr.Zero)return;
            // Transport callback only copies supplied bytes; it never reads mutable simulation state.
            byte* wire=(byte*)data;
            var e=new CaptureEvent{Kind=RecordKind.Arrival,Tick=-1,Phase=-1,Opcode=wire[0],Player=player,Command=-1,Planned=-1};
            // Only ordinary inner Chores use this header. Compression/sync/ping retain raw data.
            if(length>=8&&e.Opcode>=2&&e.Opcode<=120)
            {e.Planned=wire[1]|wire[2]<<8|wire[3]<<16;e.Command=*(int*)(wire+4);}
            Recorder.Record(e,data,length);
        }
        internal void BeforeField(IntPtr manager,IntPtr field,int length,int scheduled,int unpack)
        {
            if(!Active||manager!=(IntPtr)M||field==IntPtr.Zero||length<1||length>180000)return;
            // Several handlers change the native phase from pack(1) to execute(0) before
            // transferring fields. Preserve the entry context, including nested dispatches.
            var e=dispatchOwner==this&&dispatchContextValid?dispatchContext:Context(RecordKind.Field);
            e.Kind=RecordKind.Field;e.Global=unpack; // Field.Global is the audited transfer direction.
            int offset=*(int*)(M+0x370BF8),slot=*(int*)(M+0x84CC8);
            int limit=scheduled==0?180000:0x4EC;
            if(offset<0||offset>limit-length||(scheduled!=0&&(slot<0||slot>=500))){Interlocked.Increment(ref Recorder.CallbackFailures);return;}
            byte* wire=scheduled==0?M+0x84CD8+offset:M+0xB0C09+slot*0x500+offset;
            if(e.Phase==0&&e.Opcode==56&&unpack==1&&offset==0&&length==4)
                CaptureUnit(*(int*)wire,0,RecordKind.Unit,e.Phase);
            // Capture actual wire fields during execution/packing, never mode-2 measurement.
            if(e.Phase==2)return;
            e.Unit=e.Opcode==56?*(int*)(M+0x14D00C):0;
            e.Stage=offset; // For Field records Stage stores wire offset, not an incident number.
            if(Recorder.IsDeep||*(int*)(M+0xDAC)!=0||(e.Phase==0&&e.Opcode==106&&unpack==1))
                Recorder.Record(e,unpack==1?(IntPtr)wire:field,length);
        }
        internal void Checksum(bool ran,int mode)
        {
            if(!Active||!ran)return;
            int player=*(int*)(M+0x36FA50);
            if(player<1||player>8){Interlocked.Increment(ref Recorder.CallbackFailures);return;}
            var e=Context(RecordKind.Checksum);e.Phase=mode==0?10:13;e.Stage=mode;e.Player=player;
            e.Planned=*(int*)(M+0x14D0E4+player*4);
            Recorder.Record(e,(IntPtr)(M+0x14D108+player*56),56);
            // Copy the freshly generated local slot table before the native caller resumes.
            // This is also available in Light and requires no Unit/Path traversal.
            uint* checksums = (uint*)(M + 0x14F0CC) + player * (UnitChecksumSlots + 1);
            if (mode == 0 || mode == 1)
            {
                e.Kind = RecordKind.UnitChecksumTable;
                Recorder.Record(e, (IntPtr)(checksums + 1), UnitChecksumSlots * sizeof(uint));
                CaptureBuildingCheckpoint(e,player,mode);
            }
            if(!Recorder.IsDeep||!UnitContract||(lastCheckpoint==Tick&&lastCheckpointMode==mode&&lastCheckpointCommand==e.Command))return;
            lastCheckpoint=Tick;lastCheckpointMode=mode;lastCheckpointCommand=e.Command;
            for(int id=1;id<=UnitChecksumSlots;id++)
            {
                if(!Recorder.IsDeep)break;
                byte* record=Base+WireUnits+id*UnitChecksum.RecordSize;byte* path=Base+Paths+id*UnitChecksum.PathSize;
                uint actual=checksums[id];
                if(UnitChecksum.Compute(record,path)!=actual)
                {
                    UnitContract=false;Interlocked.Increment(ref Recorder.HashMismatches);
                    var failure=Context(RecordKind.Fault);failure.Unit=id;failure.Phase=e.Phase;
                    Recorder.Record(failure,IntPtr.Zero,0);return;
                }
                CaptureUnit(id,0,RecordKind.WorldUnit,e.Phase);
            }
        }
        private void CaptureBuildingCheckpoint(CaptureEvent e,int player,int mode)
        {
            uint* checksums=(uint*)(M+BuildingChecksumsOffset)+player*(BuildingChecksumSlots+1);
            e.Kind=RecordKind.BuildingChecksumTable;
            if(!Recorder.Record(e,(IntPtr)(checksums+1),BuildingChecksumSlots*sizeof(uint))||mode!=0)return;
            e.Kind=RecordKind.BuildingState;
            int budget=MaximumBuildingRecordsPerCheckpoint;
            for(int id=1;id<=BuildingChecksumSlots;id++)
            {
                uint checksum=checksums[id];
                bool hadRecord=recordedBuildings[id];
                if(hadRecord&&recordedBuildingHashes[id]==checksum)continue;
                var building=(GameBuilding*)(Base+BuildingRecords+id*BuildingRecordSize);
                uint global=building->r_GlobalId;
                if(!hadRecord&&global==0)continue;
                if(budget==0){Interlocked.Increment(ref Recorder.BuildingRowsDeferred);continue;}
                budget--;
                e.Unit=id;e.Global=unchecked((int)global);e.Command=unchecked((int)checksum);
                if(!Recorder.Record(e,(IntPtr)building,BuildingRecordSize))continue;
                recordedBuildingHashes[id]=checksum;
                recordedBuildings[id]=true;
            }
        }
        internal void Sample()
        {
            if(workerMarks!=null && Active)SampleWorkers(Tick);
            if(!Active||!Recorder.IsDeep||!UnitContract||Tick==lastSample||Tick%Recorder.Options.Interval!=0)return;
            lastSample=Tick;
            byte* hash=stackalloc byte[12];
            for(int id=1;id<10000;id++)
            {
                if(!Recorder.IsDeep)break;
                byte* record=Base+WireUnits+id*UnitChecksum.RecordSize;byte* path=Base+Paths+id*UnitChecksum.PathSize;
                *(uint*)hash=UnitChecksum.Compute(record,path);*(ulong*)(hash+4)=UnitChecksum.Strong(record,path);
                var e=Context(RecordKind.UnitHash);e.Phase=11;e.Unit=id;e.Global=*(int*)(record+UnitChecksum.GlobalOffset);
                Recorder.Record(e,(IntPtr)hash,12);
            }
            foreach(int id in Recorder.Options.Focus){if(!Recorder.IsDeep)break;CaptureUnit(id,0,RecordKind.WorldUnit,11);}
        }
        private void SampleWorkers(int tick)
        {
            if(tick==lastWorkerTick)return;
            uint bound=((GameUnitManager*)(Base+UnitManager))->r_NextUnitId;
            if(bound<1 || bound>workerMarks.Length)return;
            lastWorkerTick=tick;
            int upper=Math.Max(workerHighWater,(int)bound);
            workerHighWater=(int)bound;
            for(int id=GameUnitManager.FirstLiveUnitId;id<upper;id++)
            {
                GameUnit* unit=(GameUnit*)(Base+WireUnits+4+id*UnitChecksum.RecordSize);
                WorkerMark next=default;
                if(id<bound && unit->r_ControllableForPlayerId>=1 && unit->r_ControllableForPlayerId<=8 &&
                    (Recorder.Options.WorkerPlayer<0 || unit->r_ControllableForPlayerId==Recorder.Options.WorkerPlayer) && unit->r_GlobalId!=0 &&
                    (unit->r_AliveState==AliveState.IsAlive || unit->r_AliveState==AliveState.NeedsInit) &&
                    (unit->r_UnitChimp==eChimps.CHIMP_TYPE_PEASANT || unit->r_UnitChimp==eChimps.CHIMP_TYPE_MILLER))
                {
                    next=new WorkerMark{Identity=unit->r_GlobalId,WorkplaceGlobal=unit->r_LinkedProductionBuildingGlobalId,
                        Kind=(ushort)unit->r_UnitChimp,PendingKind=(ushort)unit->r_TransformIntoUnitOfType,
                        Workplace=unit->r_LinkedProductionBuildingId,State=unit->r_AIState,WorkMode=unit->r_WorkModeFlags,
                        Alive=(ushort)unit->r_AliveState,Player=unit->r_ControllableForPlayerId,X=unit->r_CurrentTilePositionX,Y=unit->r_CurrentTilePositionY,
                        TargetX=unit->r_TargetTilePositionX,TargetY=unit->r_TargetTilePositionY};
                }
                var old=workerMarks[id];
                if(next.SameAssignment(old))continue;
                // Keep the prior identity on an exit. A recycled slot gets a separate enter record.
                if(old.Identity!=0 && old.Identity!=next.Identity)
                {
                    if(!RecordWorker(id,tick,2,old))
                    {
                        workerHighWater=Math.Max(workerHighWater,id+1);
                        continue;
                    }
                    workerMarks[id]=default;
                }
                if(next.Identity!=0 && !RecordWorker(id,tick,old.Identity==next.Identity?1:0,next))continue;
                workerMarks[id]=next;
            }
        }

        private bool RecordWorker(int id,int tick,int stage,WorkerMark mark) =>
            Recorder.Record(new CaptureEvent{Kind=RecordKind.WorkerAssignment,Tick=tick,Phase=11,Opcode=-1,
                Player=mark.Player,Command=-1,Planned=-1,Unit=id,Global=unchecked((int)mark.Identity),Stage=stage},
                (IntPtr)(&mark),WorkerPayloadSize);
        private void CaptureUnit(int id,int stage,RecordKind kind,int phase)
        {
            if(id<1||id>9999){Interlocked.Increment(ref Recorder.CallbackFailures);return;}
            byte* record=Base+WireUnits+id*UnitChecksum.RecordSize;byte* path=Base+Paths+id*UnitChecksum.PathSize;
            var e=Context(kind,stage);e.Phase=phase;e.Unit=id;e.Global=*(int*)(record+UnitChecksum.GlobalOffset);
            Recorder.Record(e,(IntPtr)record,UnitChecksum.RecordSize);e.Stage=stage+1;
            Recorder.Record(e,(IntPtr)path,UnitChecksum.PathSize);
        }
        internal void Forced(int tick,int expected,int started,int now)
        {
            if(!Active)return;
            Recorder.Record(new CaptureEvent{Kind=RecordKind.ForcedRun,Tick=tick,Phase=12,Planned=expected,Command=started,Player=now,Opcode=120},IntPtr.Zero,0);
        }
    }
}

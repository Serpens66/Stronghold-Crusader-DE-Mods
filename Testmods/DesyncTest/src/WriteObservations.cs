using System;
using System.Threading;
using SHCDESE.Interop;

namespace DesyncTest
{
    internal sealed unsafe partial class NativeCapture
    {
        internal const int TileManagerRva=0x405EDB0, TerrainPending=0x204E62C;
        private const int Logic=0x898400, Dirty=0xA20D40, Height=0xD7E5A0, Soil=0x9D2500, Structure=0xB0BCA0;
        private const int MapView=0x204E66C, DirtyRows=0x60AD680, RowTable=0x402FF28, RowTileCounts=0x6400;
        private const int TerrainScanLimit=16384, TerrainPassLimit=2, CommandRowLimit=96;
        private readonly TileSample[] tileSamples=new TileSample[256];
        private int terrainBusy,terrainDisabled,terrainTick=-1,terrainPass,terrainCount,terrainScanned,terrainView,terrainIncomplete;
        internal void DisableTerrain() => Volatile.Write(ref terrainDisabled,1);
        private int commandBusy,commandTick=-1,commandsThisTick;
        // Fixed-size reusable storage; the native engine runs the original between the two reads.
        private readonly BuildingSample[] commandBefore=new BuildingSample[BuildingChecksumSlots+1];
        private const int VegetationRva=0x32DE440, VegetationSlots=4000, VegetationStride=0x9C;
        private readonly long[] cropBefore=new long[VegetationSlots];

        private struct TileSample
        {
            internal int Slot,Flags,Height,Soil,Building;
        }

        private struct BuildingSample : IEquatable<BuildingSample>
        {
            internal int Global, Status, Owner, Position, Tile, Access, Workers, Health, Quarry, Stone;
            public bool Equals(BuildingSample other) =>
                Global==other.Global&&Status==other.Status&&Owner==other.Owner&&Position==other.Position&&Tile==other.Tile&&
                Access==other.Access&&Workers==other.Workers&&Health==other.Health&&Quarry==other.Quarry&&Stone==other.Stone;
        }

        internal bool BeginTerrain(IntPtr manager)
        {
            byte* tile=(byte*)manager;
            if(!Active||Volatile.Read(ref terrainDisabled)!=0||tile!=Base+TileManagerRva||*(int*)(tile+TerrainPending)==0)return false;
            if(Interlocked.CompareExchange(ref terrainBusy,1,0)!=0)return false;
            try
            {
                int tick=Tick;
                if(terrainTick!=tick){terrainTick=tick;terrainPass=0;}
                if(++terrainPass>TerrainPassLimit)
                {
                    if(terrainPass==TerrainPassLimit+1)
                        RecordTerrainCoverage(0,0,2);
                    Volatile.Write(ref terrainBusy,0);
                    return false;
                }
                terrainCount=0;terrainScanned=0;terrainIncomplete=0;
                terrainView=*(int*)(tile+MapView);
                int min=*(int*)(Base+DirtyRows),max=*(int*)(Base+DirtyRows+4);
                if((uint)min>=800||max<min)
                {
                    terrainIncomplete=1;
                    return true;
                }
                int id=*(int*)(Base+RowTable+min*12);
                for(int row=min;row<800&&row<=max;row++)
                {
                    int count=*(int*)(tile+RowTileCounts+row*4);
                    for(int n=0;n<count;n++,id++)
                    {
                        if((uint)id>=GameTileManagerView.NativePackedTileCapacity||terrainScanned==TerrainScanLimit||
                           terrainCount==tileSamples.Length)
                        {
                            terrainIncomplete=1;
                            return true;
                        }
                        terrainScanned++;
                        if(tile[Dirty+id]==0||(*(int*)(tile+Logic+id*4)&0x30)!=0)continue;
                        tileSamples[terrainCount++]=ReadTile(tile,id);
                    }
                }
                return true;
            }
            catch
            {
                Volatile.Write(ref terrainBusy,0);
                throw;
            }
        }

        internal void EndTerrain(IntPtr manager,bool completed)
        {
            try
            {
                if(!completed||!Active)return;
                byte* tile=(byte*)manager;
                int* values=stackalloc int[10];
                for(int i=0;i<terrainCount;i++)
                {
                    var before=tileSamples[i];
                    var after=ReadTile(tile,before.Slot);
                    if((before.Flags&0x40000000)==0&&before.Flags==after.Flags&&before.Height==after.Height&&
                        before.Soil==after.Soil&&before.Building==after.Building)continue;
                    values[0]=before.Flags;values[1]=after.Flags;values[2]=before.Height;values[3]=after.Height;
                    values[4]=before.Soil;values[5]=after.Soil;values[6]=before.Building;values[7]=after.Building;
                    values[8]=terrainView;values[9]=*(int*)(tile+MapView);
                    Recorder.Record(new CaptureEvent{Kind=RecordKind.TerrainDelta,Tick=terrainTick,Phase=11,Unit=before.Slot,Stage=terrainPass},
                        (IntPtr)values,10*sizeof(int));
                }
                if(terrainCount!=0||terrainIncomplete!=0)RecordTerrainCoverage(terrainScanned,terrainCount,terrainIncomplete);
            }
            finally{Volatile.Write(ref terrainBusy,0);}
        }

        private void RecordTerrainCoverage(int scanned,int sampled,int reason)
        {
            int* values=stackalloc int[3]{scanned,sampled,reason};
            Recorder.Record(new CaptureEvent{Kind=RecordKind.TerrainCoverage,Tick=terrainTick,Phase=11,Stage=terrainPass},
                (IntPtr)values,3*sizeof(int));
        }

        private static TileSample ReadTile(byte* manager,int slot) => new TileSample
        {
            Slot=slot,Flags=*(int*)(manager+Logic+slot*4),Height=manager[Height+slot],Soil=manager[Soil+slot],
            Building=*(ushort*)(manager+Structure+slot*2)
        };

        internal int FindWorkerCandidate(IntPtr manager,int player)
        {
            if((byte*)manager!=Base+UnitManager)return -1;
            int count=*(int*)manager;
            if(count<1||count>UnitChecksumSlots+1)return -1;
            for(int slot=1;slot<count;slot++)
            {
                byte* unit=Base+WireUnits+4+slot*UnitChecksum.RecordSize;
                if(*(short*)(unit+0x92)!=player||*(short*)(unit+0x8A)!=1||
                    *(short*)(unit+0x29C)!=0||*(short*)(unit+0x88)!=2)continue;
                short state=*(short*)(unit+0x2BC);
                if(state!=4&&state!=5&&state!=0x6D&&state!=0x6A)return slot;
            }
            return 0;
        }

        internal void WorkerInputs(int building,int player,int role,int ordinal,int candidate,int result,int calls,int firstResult,
            int firstX,int firstY,int firstView,int unitGlobalBefore,int unitStateBefore)
        {
            if(building<1||building>BuildingChecksumSlots)return;
            int* values=stackalloc int[14];
            values[0]=candidate;values[1]=result;values[2]=calls;values[3]=firstResult;
            values[4]=firstX;values[5]=firstY;values[6]=firstView;
            values[7]=unitGlobalBefore;values[8]=unitStateBefore;
            int picked=result>0?result:candidate;
            var unit=ReadWorker(picked);
            values[9]=unit==null?0:(int)unit->r_GlobalId;
            values[10]=unit==null?-1:(int)unit->r_AIState;
            values[11]=unit==null?-1:(int)unit->r_TransformIntoUnitOfType;
            values[12]=unit==null?-1:unit->r_LinkedProductionBuildingId;
            values[13]=unit==null?0:(int)unit->r_LinkedProductionBuildingGlobalId;
            byte* b=Base+BuildingRecords+building*BuildingRecordSize;
            Recorder.Record(new CaptureEvent{Kind=RecordKind.WorkerInputs,Tick=Tick,Phase=11,Unit=building,Global=*(int*)(b+0xD8),
                Player=player,Command=role,Planned=ordinal},(IntPtr)values,14*sizeof(int));
        }

        internal GameUnit* ReadWorker(int id) => id>=1&&id<=UnitChecksumSlots?
            (GameUnit*)(Base+WireUnits+4+id*UnitChecksum.RecordSize):null;
        internal int ReadView() => *(int*)(Base+TileManagerRva+MapView);
        internal void ReadEntrance(int building,out int x,out int y)
        {
            x=0;y=0;
            if(building<1||building>BuildingChecksumSlots)return;
            byte* b=Base+BuildingRecords+building*BuildingRecordSize;
            x=*(short*)(b+0xFE);y=*(short*)(b+0x100);
        }

        private bool BeginCommand(CaptureEvent context)
        {
            if(Interlocked.CompareExchange(ref commandBusy,1,0)!=0)
            {
                RecordCommandSkipped(context,1);
                return false;
            }
            try
            {
                if(commandTick!=context.Tick){commandTick=context.Tick;commandsThisTick=0;}
                if(++commandsThisTick>4)
                {
                    RecordCommandSkipped(context,2);
                    Volatile.Write(ref commandBusy,0);
                    return false;
                }
                for(int id=1;id<commandBefore.Length;id++)commandBefore[id]=ReadBuilding(id);
                for(int id=0;id<cropBefore.Length;id++)cropBefore[id]=ReadCrop(id);
                return true;
            }
            catch
            {
                Volatile.Write(ref commandBusy,0);
                throw;
            }
        }

        private void RecordCommandSkipped(CaptureEvent e,int reason)
        {
            e.Kind=RecordKind.CommandCoverage;e.Stage=reason;
            int* unknown=stackalloc int[4]{-1,0,-1,0};
            Recorder.Record(e,(IntPtr)unknown,4*sizeof(int));
        }

        private void EndCommand(CaptureEvent e)
        {
            int changed=0,stored=0,vegetation=0,storedVegetation=0;
            e.Kind=RecordKind.CommandBuildingDelta;
            BuildingSample* pair=stackalloc BuildingSample[2];
            for(int id=1;id<commandBefore.Length;id++)
            {
                var before=commandBefore[id];
                var after=ReadBuilding(id);
                if(before.Equals(after))continue;
                changed++;
                if(stored>=CommandRowLimit)continue;
                e.Unit=id;e.Global=after.Global!=0?after.Global:before.Global;e.Stage=0;
                pair[0]=before;pair[1]=after;
                if(Recorder.Record(e,(IntPtr)pair,2*sizeof(BuildingSample)))stored++;
            }
            long* crops=stackalloc long[2];
            for(int id=0;id<cropBefore.Length;id++)
            {
                long after=ReadCrop(id);
                if(cropBefore[id]==after)continue;
                vegetation++;
                if(storedVegetation>=CommandRowLimit)continue;
                e.Unit=id;e.Global=0;e.Stage=1;
                crops[0]=cropBefore[id];crops[1]=after;
                if(Recorder.Record(e,(IntPtr)crops,2*sizeof(long)))storedVegetation++;
            }
            e.Kind=RecordKind.CommandCoverage;e.Unit=0;e.Global=0;e.Stage=0;
            int* coverage=stackalloc int[4]{changed,stored,vegetation,storedVegetation};
            Recorder.Record(e,(IntPtr)coverage,4*sizeof(int));
        }

        private long ReadCrop(int id) => *(long*)(Base+VegetationRva+0xA4+id*VegetationStride);

        private BuildingSample ReadBuilding(int id)
        {
            byte* b=Base+BuildingRecords+id*BuildingRecordSize;
            if(*(ushort*)(b+0xD0)==0)return default;
            return new BuildingSample
            {
                Global=*(int*)(b+0xD8),Status=*(int*)(b+0xD0),Owner=*(ushort*)(b+0xD6),Position=*(int*)(b+0xEE),
                Tile=*(int*)(b+0xF4),Access=*(int*)(b+0xFE),Workers=*(int*)(b+0x19A),Health=*(short*)(b+0x10C),
                Quarry=*(ushort*)(b+0x192),Stone=*(int*)(b+0x130)
            };
        }
    }
}

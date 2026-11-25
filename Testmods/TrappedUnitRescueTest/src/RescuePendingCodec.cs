using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    // One bounded record per pending unit keeps each APIShared archive entry below1MiB.
    // Session, world, order counters and mutation IDs are deliberately rebound on load.
    internal static class RescuePendingCodec
    {
        private const uint Signature=0x52535043;
        private const ushort Version=4;
        private const int MaximumBytes=16384,MaximumCandidates=289;
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        internal static bool TryEncode(RescuePendingCandidate<UnitSnapshot> record,out byte[] data)
        {
            data=null;if(!ValidRecord(record))return false;
            byte[] source;
            try{source=Utf8.GetBytes(record.Source);}catch(ArgumentException){return false;}
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Signature);writer.Write(Version);writer.Write((ushort)source.Length);writer.Write(source);
                writer.Write(record.Age);writer.Write(record.Attempts);writer.Write(record.RetryIn);
                var u=record.Snapshot;
                foreach(int value in new[]{u.Id,u.Type,u.Owner,u.Tribe,u.X,u.Y,u.TileId,u.AIState,u.Flags,u.Substep,
                    u.RequestedX,u.RequestedY,u.CrewCount,u.NextX,u.NextY,u.NextTileId})writer.Write(value);
                writer.Write(u.GroundFlags);writer.Write(u.NextGroundFlags);writer.Write(u.GlobalId);writer.Write(u.Health);writer.Write(u.TribeGlobalId);
                writer.Write(u.HasCapturedVegetation);writer.Write(u.CapturedVegetationGlobal);
                writer.Write(u.OrdinaryStep);writer.Write(u.AffectedCurrent);writer.Write(u.AffectedNext);writer.Write(u.CurrentWasOrdinary);writer.Write(u.NextWasOrdinary);
                writer.Write(u.Connections);writer.Write(u.CrewIdentity.Length);
                foreach(uint value in u.CrewIdentity)writer.Write(value);
                writer.Write(u.GeometryWitness!=null);
                if(u.GeometryWitness!=null)RescueGeometryCodec.WriteWitness(writer,u.GeometryWitness);
                writer.Write(u.Candidates.Count);
                foreach(var c in u.Candidates){writer.Write(c.X);writer.Write(c.Y);writer.Write(c.Tile);writer.Write(c.Distance);}
                writer.Write((byte)(u.Job!=null?1:u.Command!=null?2:0));
                if(u.Job!=null)
                {
                    uint[] values=u.Job.CopyValues();writer.Write(values.Length);
                    for(int i=0;i<values.Length;i++){var f=u.Job.Rule.Field(i);writer.Write(f.NativeOffset);writer.Write((byte)f.Width);writer.Write(values[i]);}
                }
                else if(u.Command!=null)
                {
                    uint[] values=u.Command.CopyValues();writer.Write(values.Length);
                    for(int i=0;i<values.Length;i++){var f=u.Command.Field(i);writer.Write(f.NativeOffset);writer.Write((byte)f.Width);writer.Write(values[i]);}
                }
                writer.Flush();if(stream.Length>MaximumBytes)return false;data=stream.ToArray();return true;
            }
        }
        internal static bool TryDecode(byte[] data,out RescuePendingCandidate<UnitSnapshot> record)
        {
            record=default;if(data==null || data.Length>MaximumBytes)return false;
            try
            {
                using(var stream=new MemoryStream(data,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadUInt32()!=Signature || reader.ReadUInt16()!=Version)return false;
                    int sourceLength=reader.ReadUInt16();if(sourceLength<1 || sourceLength>384)return false;
                    byte[] sourceBytes=reader.ReadBytes(sourceLength);if(sourceBytes.Length!=sourceLength)return false;
                    string source=Utf8.GetString(sourceBytes);if(source.Length<1 || source.Length>96)return false;
                    int age=reader.ReadInt32(),attempts=reader.ReadInt32(),retry=reader.ReadInt32();
                    var u=new UnitSnapshot{Id=reader.ReadInt32(),Type=reader.ReadInt32(),Owner=reader.ReadInt32(),Tribe=reader.ReadInt32(),
                        X=reader.ReadInt32(),Y=reader.ReadInt32(),TileId=reader.ReadInt32(),AIState=reader.ReadInt32(),Flags=reader.ReadInt32(),Substep=reader.ReadInt32(),
                        RequestedX=reader.ReadInt32(),RequestedY=reader.ReadInt32(),CrewCount=reader.ReadInt32(),NextX=reader.ReadInt32(),NextY=reader.ReadInt32(),NextTileId=reader.ReadInt32(),
                        GroundFlags=reader.ReadUInt32(),NextGroundFlags=reader.ReadUInt32(),GlobalId=reader.ReadUInt32(),Health=reader.ReadUInt32(),TribeGlobalId=reader.ReadUInt32(),
                        HasCapturedVegetation=ReadBool(reader),CapturedVegetationGlobal=reader.ReadUInt32(),
                        OrdinaryStep=ReadBool(reader),AffectedCurrent=ReadBool(reader),AffectedNext=ReadBool(reader),CurrentWasOrdinary=ReadBool(reader),NextWasOrdinary=ReadBool(reader),
                        Connections=reader.ReadByte()};
                    int crew=reader.ReadInt32();if(crew!=0 && crew!=8)return false;
                    u.CrewIdentity=new uint[crew];for(int i=0;i<crew;i++)u.CrewIdentity[i]=reader.ReadUInt32();
                    if(ReadBool(reader))
                    {if(!RescueGeometryCodec.TryReadWitness(reader,out var witness))return false;u.GeometryWitness=witness;}
                    int count=reader.ReadInt32();if(count<0 || count>MaximumCandidates)return false;
                    for(int i=0;i<count;i++)u.Candidates.Add(new RescueCandidate{X=reader.ReadInt32(),Y=reader.ReadInt32(),Tile=reader.ReadInt32(),Distance=reader.ReadInt32()});
                    byte kind=reader.ReadByte();if(kind>2)return false;
                    if(kind==1)
                    {
                        if(!WorkerTravelPolicy.TryDescribe((eChimps)u.Type,u.AIState,out var rule) || !rule.CanResumeOrdinary)return false;
                        int fields=reader.ReadInt32();if(fields!=rule.FieldCount)return false;
                        var offsets=new int[fields];var widths=new int[fields];var values=new uint[fields];
                        for(int i=0;i<fields;i++){offsets[i]=reader.ReadInt32();widths[i]=reader.ReadByte();values[i]=reader.ReadUInt32();}
                        if(!WorkerJourneySnapshot.TryRestoreKnownSnapshot((eChimps)u.Type,u.AIState,offsets,widths,values,out u.Job))return false;
                    }
                    else if(kind==2)
                    {
                        if(!(u.Flags==0?CommandJourneySnapshot.IsSupportedStationaryPhase((eChimps)u.Type,u.AIState):
                            CommandJourneySnapshot.IsSupportedPhase((eChimps)u.Type,u.AIState)))return false;
                        int fields=reader.ReadInt32();if(fields<1 || fields>64)return false;
                        var offsets=new int[fields];var widths=new int[fields];var values=new uint[fields];
                        for(int i=0;i<fields;i++){offsets[i]=reader.ReadInt32();widths[i]=reader.ReadByte();values[i]=reader.ReadUInt32();}
                        if(!CommandJourneySnapshot.TryRestoreKnownSnapshot((eChimps)u.Type,u.AIState,offsets,widths,values,out u.Command))return false;
                    }
                    var result=new RescuePendingCandidate<UnitSnapshot>(source,u,age,attempts,retry);
                    if(stream.Position!=stream.Length || !ValidRecord(result))return false;
                    record=result;return true;
                }
            }
            catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
        private static bool ReadBool(BinaryReader reader)
        {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException("Invalid boolean.");return value!=0;}
        private static bool ValidRecord(RescuePendingCandidate<UnitSnapshot> record)
        {
            var u=record.Snapshot;
            if(string.IsNullOrEmpty(record.Source) || record.Source.Length>96 || record.Age<0 || record.Age>=80 ||
                record.Attempts<0 || record.Attempts>=4 || record.RetryIn<0 || record.RetryIn>1 || u==null || u.Unsupported!=null ||
                u.Id<1 || u.Id>NativeRelocation.MaximumUnitId || u.GlobalId==0 || u.Health==0 || u.Owner<0 || u.Owner>8 || u.Tribe<0 ||
                u.Type<0 || u.Type>ushort.MaxValue || !Enum.IsDefined(typeof(eChimps),(eChimps)u.Type) || !XY(u.X,u.Y) || !XY(u.NextX,u.NextY) || !XY(u.RequestedX,u.RequestedY) ||
                !Tile(u.TileId) || !Tile(u.NextTileId) || (u.Flags!=0 && u.Flags!=2 && u.Flags!=4) || u.Substep<0 || u.Substep>8 ||
                !RescueRuntime.HasCoherentCrewShape(u.CrewIdentity,u.CrewCount) || u.Candidates.Count>MaximumCandidates)return false;
            if(u.OrdinaryStep && (u.Flags==0 || u.Substep>=8 || Math.Abs(u.X-u.NextX)>1 || Math.Abs(u.Y-u.NextY)>1 || u.TileId==u.NextTileId))return false;
            if(u.GeometryWitness!=null)
            {if(!RescueGeometryCodec.CanonicalJoin(u))return false;}
            else
            {
                var seen=new HashSet<int>();
                foreach(var c in u.Candidates)
                    if(!XY(c.X,c.Y) || !Tile(c.Tile) || Math.Abs(c.X-u.X)>8 || Math.Abs(c.Y-u.Y)>8 || c.Distance<0 || c.Distance>MaximumCandidates || !seen.Add(c.Tile) ||
                        c.Distance==0 && (c.Tile!=u.TileId || c.X!=u.X || c.Y!=u.Y))return false;
            }
            if(WorkerTravelPolicy.TryDescribe((eChimps)u.Type,u.AIState,out var rule))
                return rule.CanResumeOrdinary &&
                    (((rule.RequiredExternalBindings&WorkerExternalBindings.Vegetation)!=0)?u.HasCapturedVegetation:
                        !u.HasCapturedVegetation && u.CapturedVegetationGlobal==0) &&
                    u.Job!=null && u.Command==null && u.Job.Rule.UnitType==(eChimps)u.Type && u.Job.Rule.AIState==u.AIState &&
                    u.Job.TryGetValue(0x6F0,out uint global) && global==u.GlobalId && u.Job.TryGetValue(0x6EE,out uint owner) && owner==(uint)u.Owner;
            if(u.Job!=null || u.HasCapturedVegetation || u.CapturedVegetationGlobal!=0)return false;
            return (u.Flags==0?CommandJourneySnapshot.IsSupportedStationaryPhase((eChimps)u.Type,u.AIState):
                    CommandJourneySnapshot.IsSupportedPhase((eChimps)u.Type,u.AIState)) &&
                u.Command!=null && u.Command.UnitType==(eChimps)u.Type && u.Command.AIState==u.AIState &&
                u.Command.TryGetValue(0x6F0,out uint commandGlobal) && commandGlobal==u.GlobalId &&
                u.Command.TryGetValue(0x6EE,out uint commandOwner) && commandOwner==(uint)u.Owner;
        }
        private static bool XY(int x,int y)=>(uint)x<800 && (uint)y<800;
        private static bool Tile(int tile)=>(uint)tile<NativeRelocation.TileCapacity;
    }
}

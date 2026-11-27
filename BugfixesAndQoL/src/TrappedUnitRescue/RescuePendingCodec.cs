using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SHCDESE.Interop;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // One bounded record per pending unit keeps each APIShared archive entry below1MiB.
    // Session, world, order counters and mutation IDs are deliberately rebound on load.
    internal static class RescuePendingCodec
    {
        private const uint Signature=0x52535043;
        private const ushort Version=5;
        private const int MaximumBytes=16384,MaximumCandidates=289;
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        internal static bool TryEncode(RescuePendingCandidate<UnitSnapshot> record,out byte[] data)
        {
            data=null;if(!ValidRecord(record))return false;
            if(record.LifetimeMode!=RescueQueueLifetimeMode.AbsoluteSourceAge || record.Snapshot.LocalField!=null || record.Snapshot.GeometryWitness?.LocalField!=null)return TryEncodeV6(record,out data);
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
                writer.Write(u.Connections);
                writer.Write(u.CivilianRemoval);writer.Write(u.CurrentHadExit);writer.Write(u.NextHadExit);
                writer.Write(u.CrewIdentity.Length);
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
                    if(reader.ReadUInt32()!=Signature)return false;
                    ushort version=reader.ReadUInt16();
                    if(version==6){int n=reader.ReadUInt16();if(n<1 || n>384)return false;var compactSource=Utf8.GetString(ReadExact(reader,n));
                        if(!TryReadCompactBody(reader,compactSource,out record) || stream.Position!=stream.Length){record=default;return false;}return true;}
                    if(version!=4 && version!=Version)return false;
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
                    // Historical v4 records keep their original job/command meaning.
                    if(version>=5){u.CivilianRemoval=ReadBool(reader);u.CurrentHadExit=ReadBool(reader);u.NextHadExit=ReadBool(reader);}
                    int crew=reader.ReadInt32();if(crew!=0 && crew!=8)return false;
                    u.CrewIdentity=new uint[crew];for(int i=0;i<crew;i++)u.CrewIdentity[i]=reader.ReadUInt32();
                    if(ReadBool(reader))
                    {if(!RescueGeometryCodec.TryReadWitness(reader,out var witness))return false;u.GeometryWitness=witness;}
                    int count=reader.ReadInt32();if(count<0 || count>MaximumCandidates)return false;
                    for(int i=0;i<count;i++)u.Candidates.Add(new RescueCandidate{X=reader.ReadInt32(),Y=reader.ReadInt32(),Tile=reader.ReadInt32(),Distance=reader.ReadInt32()});
                    byte kind=reader.ReadByte();if(kind>2)return false;
                    if(kind==1)
                    {
                        if(!WorkerTravelPolicy.TryDescribeHistorical((eChimps)u.Type,u.AIState,out var rule) || !rule.HasOrdinarySchema)return false;
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
        internal static bool ValidRecord(RescuePendingCandidate<UnitSnapshot> record)
        {
            var u=record.Snapshot;
            if(string.IsNullOrEmpty(record.Source) || record.Source.Length>96 || !ValidLifetime(record.Age,record.Attempts,record.RetryIn,record.LifetimeMode,record.ActiveAge,record.AwaitingResources) ||
                record.Attempts<0 || record.Attempts>=4 || record.RetryIn<0 || record.RetryIn>1 || u==null || u.Unsupported!=null ||
                u.Id<1 || u.Id>NativeRelocation.MaximumUnitId || u.GlobalId==0 || u.Health==0 || u.Owner<0 || u.Owner>8 || u.Tribe<0 ||
                u.Type<0 || u.Type>ushort.MaxValue || !Enum.IsDefined(typeof(eChimps),(eChimps)u.Type) || !XY(u.X,u.Y) || !XY(u.NextX,u.NextY) || !XY(u.RequestedX,u.RequestedY) ||
                !Tile(u.TileId) || !Tile(u.NextTileId) || (u.Flags!=0 && u.Flags!=2 && u.Flags!=4) || u.Substep<0 || u.Substep>8 ||
                !RescueRuntime.HasCoherentCrewShape(u.CrewIdentity,u.CrewCount) || u.Candidates.Count>MaximumCandidates)return false;
            if(u.OrdinaryStep && (u.Flags==0 || u.Substep>=8 || Math.Abs(u.X-u.NextX)>1 || Math.Abs(u.Y-u.NextY)>1 || u.TileId==u.NextTileId))return false;
            if(u.CivilianRemoval)
            {
                // A removal witness contains geometry and original identity only;
                // no saved job, bank or command may silently turn into deletion.
                return CivilianRemovalPolicy.IsRemovableType((eChimps)u.Type) && u.AIState>=0 && u.AIState<108 &&
                    u.Job==null && u.Command==null && !u.HasCapturedVegetation && u.CapturedVegetationGlobal==0 &&
                    u.CrewCount==0 && u.CrewIdentity!=null && u.CrewIdentity.Length==0 &&
                    u.Candidates.Count==0 && u.GeometryWitness==null && u.LocalField==null &&
                    (!u.CurrentHadExit || u.CurrentWasOrdinary) && (!u.NextHadExit || u.NextWasOrdinary) &&
                    (u.AffectedCurrent && u.CurrentWasOrdinary && u.CurrentHadExit ||
                     u.OrdinaryStep && u.AffectedNext && u.NextWasOrdinary && u.NextHadExit);
            }
            if(u.CurrentHadExit || u.NextHadExit)return false;
            if(u.GeometryWitness!=null)
            {if(!RescueGeometryCodec.CanonicalJoin(u))return false;}
            else if(u.LocalField!=null)
            {if(u.Candidates.Count!=0 || u.LocalField.OriginX!=u.X || u.LocalField.OriginY!=u.Y || u.LocalField.OriginTile!=u.TileId || u.LocalField.OriginWasOrdinary!=u.CurrentWasOrdinary)return false;}
            else
            {
                var seen=new HashSet<int>();
                foreach(var c in u.Candidates)
                    if(!XY(c.X,c.Y) || !Tile(c.Tile) || Math.Abs(c.X-u.X)>8 || Math.Abs(c.Y-u.Y)>8 || c.Distance<0 || c.Distance>MaximumCandidates || !seen.Add(c.Tile) ||
                        c.Distance==0 && (c.Tile!=u.TileId || c.X!=u.X || c.Y!=u.Y))return false;
            }
            if(WorkerTravelPolicy.TryDescribeHistorical((eChimps)u.Type,u.AIState,out var rule))
                return rule.HasOrdinarySchema &&
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

        internal static bool ValidLifetime(int age,int attempts,int retry,RescueQueueLifetimeMode mode,int active,bool awaiting)
            =>age>=0 && attempts>=0 && attempts<4 && retry>=0 && retry<=1 &&
                (mode==RescueQueueLifetimeMode.AbsoluteSourceAge?age<80 && active==-1 && !awaiting:
                 mode==RescueQueueLifetimeMode.ActiveWindow && active>=-1 && active<80 && active<=age && (active!=-1 || attempts==0));
        private static bool TryEncodeV6(RescuePendingCandidate<UnitSnapshot> record,out byte[] bytes)
        {
            bytes=null;
            try{using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
                var source=Utf8.GetBytes(record.Source);writer.Write(Signature);writer.Write((ushort)6);writer.Write((ushort)source.Length);writer.Write(source);
                if(!TryWriteCompactBody(writer,record))return false;writer.Flush();if(stream.Length>MaximumBytes)return false;bytes=stream.ToArray();return true;}}
            catch(ArgumentException){return false;}catch(InvalidDataException){return false;}catch(OverflowException){return false;}
        }
        internal static byte[] ReadExact(BinaryReader reader,int count)
        {var data=reader.ReadBytes(count);if(data.Length!=count)throw new EndOfStreamException();return data;}
        internal static void WriteLifetime(BinaryWriter w,int age,int attempts,int retry,RescueQueueLifetimeMode mode,int active,bool awaiting)
        {w.Write(age);w.Write(active);w.Write((byte)attempts);w.Write((byte)retry);w.Write((byte)mode);w.Write(awaiting);}
        internal static void ReadLifetime(BinaryReader r,out int age,out int attempts,out int retry,out RescueQueueLifetimeMode mode,out int active,out bool awaiting)
        {age=r.ReadInt32();active=r.ReadInt32();attempts=r.ReadByte();retry=r.ReadByte();mode=(RescueQueueLifetimeMode)r.ReadByte();awaiting=ReadBool(r);
            if(!ValidLifetime(age,attempts,retry,mode,active,awaiting))throw new InvalidDataException("Invalid compact lifetime");}
        internal static bool SameField(RescueLocalField a,RescueLocalField b)
        {if(a==null || b==null)return a==b;if(a.OriginX!=b.OriginX || a.OriginY!=b.OriginY || a.OriginTile!=b.OriginTile)return false;
            var first=a.Export();var second=b.Export();if(first.Length!=second.Length)return false;for(int i=0;i<first.Length;i++)if(first[i]!=second[i])return false;return true;}
        internal static void WriteField(BinaryWriter w,RescueLocalField field)
        {w.Write(field!=null);if(field==null)return;w.Write((ushort)field.OriginX);w.Write((ushort)field.OriginY);w.Write(field.OriginTile);w.Write(field.Export());}
        internal static RescueLocalField ReadField(BinaryReader r)
        {if(!ReadBool(r))return null;int x=r.ReadUInt16(),y=r.ReadUInt16(),tile=r.ReadInt32();
            if(!RescueLocalField.TryReadUnbound(x,y,tile,ReadExact(r,326),out var field,out _))throw new InvalidDataException("Invalid local field");return field;}
        // All48 bits are meaningful; no tile/XY relation or list equality is invented.
        internal static void WriteBanks(BinaryWriter w,IList<RescueCandidate> values)
        {if(values==null || values.Count>289)throw new InvalidDataException();w.Write((ushort)values.Count);
            foreach(var c in values){if(!XY(c.X,c.Y) || !Tile(c.Tile) || c.Distance<0 || c.Distance>289)throw new InvalidDataException();
                ulong bits=(uint)c.X|((ulong)(uint)c.Y<<10)|((ulong)(uint)c.Tile<<20)|((ulong)(uint)c.Distance<<39);
                for(int i=0;i<6;i++)w.Write((byte)(bits>>(8*i)));}}
        internal static List<RescueCandidate> ReadBanks(BinaryReader r)
        {int count=r.ReadUInt16();if(count>289)throw new InvalidDataException();var values=new List<RescueCandidate>(count);
            for(int n=0;n<count;n++){ulong bits=0;for(int i=0;i<6;i++)bits|=(ulong)r.ReadByte()<<(8*i);
                var c=new RescueCandidate{X=(int)(bits&1023),Y=(int)((bits>>10)&1023),Tile=(int)((bits>>20)&524287),Distance=(int)(bits>>39)};
                if(!XY(c.X,c.Y) || !Tile(c.Tile) || c.Distance>289)throw new InvalidDataException();values.Add(c);}return values;}
        internal static void WriteWitnessMetadata(BinaryWriter w,RescueGeometryWitness value)
        {w.Write(value.UnitId);w.Write(value.GlobalId);w.Write(value.Type);w.Write(value.Owner);WriteEndpoint(w,value.Current);WriteEndpoint(w,value.Next);w.Write(value.HasAcceptedNext);}
        private static void WriteEndpoint(BinaryWriter w,RescueGeometryEndpoint e)
        {w.Write(e.X);w.Write(e.Y);w.Write(e.Tile);w.Write(e.Affected);w.Write(e.WasOrdinary);w.Write(e.GroundFlags);}
        private static RescueGeometryEndpoint ReadEndpoint(BinaryReader r)=>new RescueGeometryEndpoint(r.ReadInt32(),r.ReadInt32(),r.ReadInt32(),ReadBool(r),ReadBool(r),r.ReadUInt32());
        internal static RescueGeometryWitness ReadWitness(BinaryReader r,RescueLocalField field)
        {int id=r.ReadInt32();uint global=r.ReadUInt32();int type=r.ReadInt32(),owner=r.ReadInt32();var current=ReadEndpoint(r);var next=ReadEndpoint(r);bool accepted=ReadBool(r);
            RescueGeometryWitness value;
            bool valid=field==null?RescueGeometryWitness.TryCreate(id,global,type,owner,current,next,accepted,ReadBanks(r),out value):
                RescueGeometryWitness.TryCreateCompact(id,global,type,owner,current,next,accepted,field,out value);
            if(!valid)throw new InvalidDataException("Invalid witness");return value;}
        internal static bool TryWriteCompactBody(BinaryWriter w,RescuePendingCandidate<UnitSnapshot> record)
        {
            if(!ValidRecord(record))return false;var u=record.Snapshot;
            if(u.Tribe<0 || u.AIState<0 || u.AIState>ushort.MaxValue)return false;
            var field=u.LocalField??u.GeometryWitness?.LocalField;
            if(u.GeometryWitness?.LocalField!=null && !SameField(field,u.GeometryWitness.LocalField) ||
                u.LocalField!=null && u.GeometryWitness!=null && u.GeometryWitness.LocalField==null)return false;
            if(field!=null && u.Candidates.Count!=0)return false;
            WriteLifetime(w,record.Age,record.Attempts,record.RetryIn,record.LifetimeMode,record.ActiveAge,record.AwaitingResources);
            w.Write((ushort)u.Id);w.Write(u.GlobalId);w.Write((ushort)u.Type);w.Write((byte)u.Owner);w.Write(u.Tribe);w.Write(u.TribeGlobalId);w.Write(u.Health);
            w.Write((ushort)u.X);w.Write((ushort)u.Y);w.Write(u.TileId);w.Write((ushort)u.NextX);w.Write((ushort)u.NextY);w.Write(u.NextTileId);w.Write(u.GroundFlags);w.Write(u.NextGroundFlags);
            w.Write((ushort)u.AIState);w.Write((byte)u.Flags);w.Write((byte)u.Substep);w.Write((ushort)u.RequestedX);w.Write((ushort)u.RequestedY);w.Write((byte)u.CrewCount);
            int bits=(u.OrdinaryStep?1:0)|(u.AffectedCurrent?2:0)|(u.AffectedNext?4:0)|(u.CurrentWasOrdinary?8:0)|(u.NextWasOrdinary?16:0)|
                (u.CivilianRemoval?32:0)|(u.CurrentHadExit?64:0)|(u.NextHadExit?128:0)|(u.HasCapturedVegetation?256:0);
            w.Write((ushort)bits);w.Write(u.Connections);w.Write(u.CapturedVegetationGlobal);w.Write((byte)u.CrewIdentity.Length);foreach(uint c in u.CrewIdentity)w.Write(c);
            WriteField(w,field);w.Write(u.GeometryWitness!=null);if(u.GeometryWitness!=null){WriteWitnessMetadata(w,u.GeometryWitness);if(field==null)WriteBanks(w,u.GeometryWitness.CopyCandidates());}
            WriteBanks(w,u.Candidates);
            byte kind=(byte)(u.Job!=null?1:u.Command!=null?2:0);w.Write(kind);
            if(kind!=0){int count=u.Job!=null?u.Job.Rule.FieldCount:u.Command.FieldCount;w.Write((byte)count);
                var values=u.Job!=null?u.Job.CopyValues():u.Command.CopyValues();for(int i=0;i<count;i++){var f=u.Job!=null?u.Job.Rule.Field(i):u.Command.Field(i);WriteValue(w,f,values[i]);}}
            return true;
        }
        private static void WriteValue(BinaryWriter w,WorkerJourneyField field,uint value)
        {if(field.Width==4)w.Write(value);else if(field.Width==2){if(value>65535)throw new InvalidDataException();w.Write((ushort)value);}else if(field.Width==1){if(value>255)throw new InvalidDataException();w.Write((byte)value);}else throw new InvalidDataException();}
        internal static bool TryReadCompactBody(BinaryReader r,string source,out RescuePendingCandidate<UnitSnapshot> record)
        {
            record=default;
            try{
                ReadLifetime(r,out int age,out int attempts,out int retry,out var mode,out int active,out bool awaiting);
                var u=new UnitSnapshot{Id=r.ReadUInt16(),GlobalId=r.ReadUInt32(),Type=r.ReadUInt16(),Owner=r.ReadByte(),Tribe=r.ReadInt32(),TribeGlobalId=r.ReadUInt32(),Health=r.ReadUInt32(),
                    X=r.ReadUInt16(),Y=r.ReadUInt16(),TileId=r.ReadInt32(),NextX=r.ReadUInt16(),NextY=r.ReadUInt16(),NextTileId=r.ReadInt32(),GroundFlags=r.ReadUInt32(),NextGroundFlags=r.ReadUInt32(),
                    AIState=r.ReadUInt16(),Flags=r.ReadByte(),Substep=r.ReadByte(),RequestedX=r.ReadUInt16(),RequestedY=r.ReadUInt16(),CrewCount=r.ReadByte()};
                int bits=r.ReadUInt16();if((bits&~511)!=0)return false;
                u.OrdinaryStep=(bits&1)!=0;u.AffectedCurrent=(bits&2)!=0;u.AffectedNext=(bits&4)!=0;u.CurrentWasOrdinary=(bits&8)!=0;u.NextWasOrdinary=(bits&16)!=0;
                u.CivilianRemoval=(bits&32)!=0;u.CurrentHadExit=(bits&64)!=0;u.NextHadExit=(bits&128)!=0;u.HasCapturedVegetation=(bits&256)!=0;
                u.Connections=r.ReadByte();u.CapturedVegetationGlobal=r.ReadUInt32();int crew=r.ReadByte();if(crew!=0 && crew!=8)return false;u.CrewIdentity=new uint[crew];for(int i=0;i<crew;i++)u.CrewIdentity[i]=r.ReadUInt32();
                u.LocalField=ReadField(r);if(ReadBool(r))u.GeometryWitness=ReadWitness(r,u.LocalField);u.Candidates.AddRange(ReadBanks(r));
                byte kind=r.ReadByte();if(kind>2)return false;
                if(kind!=0){WorkerJourneyField[] schema;
                    if(kind==1){if(!WorkerTravelPolicy.TryDescribeHistorical((eChimps)u.Type,u.AIState,out var rule) || !rule.HasOrdinarySchema)return false;
                        schema=new WorkerJourneyField[rule.FieldCount];for(int i=0;i<schema.Length;i++)schema[i]=rule.Field(i);}
                    else schema=CommandJourneySnapshot.GetSchema((eChimps)u.Type,u.AIState);
                    if(schema==null || r.ReadByte()!=schema.Length)return false;
                    var values=new uint[schema.Length];var offsets=new int[schema.Length];var widths=new int[schema.Length];
                    for(int i=0;i<schema.Length;i++){var f=schema[i];offsets[i]=f.NativeOffset;widths[i]=f.Width;values[i]=f.Width==4?r.ReadUInt32():f.Width==2?r.ReadUInt16():r.ReadByte();}
                    if(kind==1){if(!WorkerJourneySnapshot.TryRestoreKnownSnapshot((eChimps)u.Type,u.AIState,offsets,widths,values,out u.Job))return false;}
                    else if(!CommandJourneySnapshot.TryRestoreKnownSnapshot((eChimps)u.Type,u.AIState,offsets,widths,values,out u.Command))return false;
                }
                var result=new RescuePendingCandidate<UnitSnapshot>(source,u,age,attempts,retry,mode,active,awaiting);if(!ValidRecord(result))return false;record=result;return true;
            }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }

        private static bool XY(int x,int y)=>(uint)x<800 && (uint)y<800;
        private static bool Tile(int tile)=>(uint)tile<NativeRelocation.TileCapacity;
    }
}

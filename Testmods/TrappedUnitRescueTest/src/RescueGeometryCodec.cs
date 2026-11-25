using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    internal readonly struct RescuePendingGeometry
    {
        internal readonly string Source;
        internal readonly RescueGeometryWitness Witness;
        internal readonly int Age,Attempts,RetryIn;
        internal RescuePendingGeometry(string source,RescueGeometryWitness witness,int age,int attempts,int retryIn)
        {Source=source;Witness=witness;Age=age;Attempts=attempts;RetryIn=retryIn;}
    }
    // Geometry-only records never serialize the invalidated AI, job, goal or group.
    internal static class RescueGeometryCodec
    {
        private const uint Signature=0x52475343;
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        internal static bool ValidWitness(RescueGeometryWitness w)=>w!=null && w.Owner>=0 && w.Owner<=8 && w.Type>=0 && w.Type<=ushort.MaxValue &&
            Enum.IsDefined(typeof(eChimps),(eChimps)w.Type) &&
            RescueGeometryWitness.TryCreate(w.UnitId,w.GlobalId,w.Type,w.Owner,w.Current,w.Next,w.HasAcceptedNext,w.CopyCandidates(),out _);
        private static bool Valid(RescuePendingGeometry r)=>!string.IsNullOrEmpty(r.Source) && r.Source.Length<=96 &&
            r.Age>=0 && r.Age<80 && r.Attempts>=0 && r.Attempts<4 && r.RetryIn>=0 && r.RetryIn<=1 && ValidWitness(r.Witness);
        internal static bool TryEncode(RescuePendingGeometry r,out byte[] bytes)
        {
            bytes=null;if(!Valid(r))return false;
            try
            {
                byte[] source=Utf8.GetBytes(r.Source);if(source.Length>384)return false;
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    writer.Write(Signature);writer.Write((ushort)1);writer.Write((ushort)source.Length);writer.Write(source);
                    writer.Write(r.Age);writer.Write(r.Attempts);writer.Write(r.RetryIn);WriteWitness(writer,r.Witness);
                    writer.Flush();if(stream.Length>8192)return false;bytes=stream.ToArray();return true;
                }
            }
            catch(ArgumentException){return false;}
        }
        internal static bool TryDecode(byte[] bytes,out RescuePendingGeometry record)
        {
            record=default;if(bytes==null || bytes.Length>8192)return false;
            try
            {
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadUInt32()!=Signature || reader.ReadUInt16()!=1)return false;
                    int length=reader.ReadUInt16();if(length<1 || length>384)return false;
                    byte[] text=reader.ReadBytes(length);if(text.Length!=length)return false;
                    string source=Utf8.GetString(text);int age=reader.ReadInt32(),attempts=reader.ReadInt32(),retry=reader.ReadInt32();
                    if(!TryReadWitness(reader,out var witness) || stream.Position!=stream.Length)return false;
                    var candidate=new RescuePendingGeometry(source,witness,age,attempts,retry);
                    if(!Valid(candidate))return false;record=candidate;return true;
                }
            }
            catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
        internal static void WriteWitness(BinaryWriter writer,RescueGeometryWitness w)
        {
            writer.Write(w.UnitId);writer.Write(w.GlobalId);writer.Write(w.Type);writer.Write(w.Owner);
            WriteEndpoint(writer,w.Current);WriteEndpoint(writer,w.Next);writer.Write(w.HasAcceptedNext);
            var candidates=w.CopyCandidates();writer.Write(candidates.Length);
            foreach(var c in candidates){writer.Write(c.X);writer.Write(c.Y);writer.Write(c.Tile);writer.Write(c.Distance);}
        }
        private static void WriteEndpoint(BinaryWriter writer,RescueGeometryEndpoint e)
        {writer.Write(e.X);writer.Write(e.Y);writer.Write(e.Tile);writer.Write(e.Affected);writer.Write(e.WasOrdinary);writer.Write(e.GroundFlags);}
        private static bool ReadBool(BinaryReader reader)
        {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException("Invalid geometry boolean.");return value!=0;}
        private static RescueGeometryEndpoint ReadEndpoint(BinaryReader reader)=>new RescueGeometryEndpoint(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32(),
            ReadBool(reader),ReadBool(reader),reader.ReadUInt32());
        internal static bool TryReadWitness(BinaryReader reader,out RescueGeometryWitness witness)
        {
            witness=null;int id=reader.ReadInt32();uint global=reader.ReadUInt32();int type=reader.ReadInt32(),owner=reader.ReadInt32();
            var current=ReadEndpoint(reader);var next=ReadEndpoint(reader);bool accepted=ReadBool(reader);
            int count=reader.ReadInt32();if(count<0 || count>RescueGeometryWitness.MaximumCandidates)return false;
            var candidates=new List<RescueCandidate>(count);
            for(int i=0;i<count;i++)candidates.Add(new RescueCandidate{X=reader.ReadInt32(),Y=reader.ReadInt32(),Tile=reader.ReadInt32(),Distance=reader.ReadInt32()});
            return owner>=0 && owner<=8 && type>=0 && type<=ushort.MaxValue && Enum.IsDefined(typeof(eChimps),(eChimps)type) &&
                RescueGeometryWitness.TryCreate(id,global,type,owner,current,next,accepted,candidates,out witness);
        }
        internal static bool CanonicalJoin(UnitSnapshot snapshot)
        {
            var w=snapshot?.GeometryWitness;
            if(!ValidWitness(w) || !RescueGeometryWitness.TryJoin(w,snapshot,out var joined) ||
                snapshot.AffectedCurrent!=joined.AffectedCurrent || snapshot.AffectedNext!=joined.AffectedNext ||
                snapshot.CurrentWasOrdinary!=joined.CurrentWasOrdinary || snapshot.NextWasOrdinary!=joined.NextWasOrdinary ||
                snapshot.GroundFlags!=joined.GroundFlags || snapshot.NextGroundFlags!=joined.NextGroundFlags || snapshot.Candidates.Count!=joined.Candidates.Count)return false;
            for(int i=0;i<snapshot.Candidates.Count;i++)
            {var a=snapshot.Candidates[i];var b=joined.Candidates[i];if(a.X!=b.X || a.Y!=b.Y || a.Tile!=b.Tile || a.Distance!=b.Distance)return false;}
            return true;
        }
    }
}

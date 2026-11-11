using System;
using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace AreaDemolitionTest
{
    internal enum SimulationPacketKind : byte { ReadyProof, ApplyBatch }
    internal enum ControlPacketKind : byte { Probe, Echo, ProofAck, ExecutionAck }

    [MessagePackObject, MessagePackFormatter(typeof(DemolitionSimulationFormatter))]
    public sealed class DemolitionSimulationPacket
    {
        [Key(0)] public int Protocol = ProtocolPolicy.Version;
        [Key(1)] public byte Kind;
        [Key(2)] public string Token = "";
        [Key(3)] public string Nonce = "";
        [Key(4)] public long Operation;
        [Key(5)] public int Batch;
        [Key(6)] public byte[] Payload = Array.Empty<byte>();
        [Key(7)] public string Digest = "";
    }
    [MessagePackObject, MessagePackFormatter(typeof(DemolitionControlFormatter))]
    public sealed class DemolitionControlPacket
    {
        [Key(0)] public int Protocol = ProtocolPolicy.Version;
        [Key(1)] public byte Kind;
        [Key(2)] public string ProbeNonce = "";
        [Key(3)] public string PeerNonce = "";
        [Key(4)] public string Token = "";
        [Key(5)] public string Fingerprint = "";
        [Key(6)] public byte[] Roster = Array.Empty<byte>();
        [Key(7)] public ulong Origin;
        [Key(8)] public long Operation;
        [Key(9)] public int Batch;
        [Key(10)] public int Command;
        [Key(11)] public int Tick;
        [Key(12)] public string InputDigest = "";
        [Key(13)] public string ResultDigest = "";
        [Key(14)] public bool Failed;
    }
    internal static class PacketRead
    {
        internal static string Text(ref MessagePackReader reader)
        { string value=reader.ReadString(); if(value==null || value.Length>128) throw new InvalidOperationException("Oversized protocol text."); return value; }
        internal static byte[] Bytes(ref MessagePackReader reader, int maximum)
        { var value=reader.ReadBytes(); if(!value.HasValue || value.Value.Length>maximum) throw new InvalidOperationException("Oversized protocol data."); return value.Value.ToArray(); }
    }
    public sealed class DemolitionSimulationFormatter : IMessagePackFormatter<DemolitionSimulationPacket>
    {
        public void Serialize(ref MessagePackWriter writer, DemolitionSimulationPacket value, MessagePackSerializerOptions options)
        {
            if(value==null){writer.WriteNil();return;} writer.WriteArrayHeader(8); writer.Write(value.Protocol);writer.Write(value.Kind);writer.Write(value.Token);writer.Write(value.Nonce);writer.Write(value.Operation);writer.Write(value.Batch);writer.Write(value.Payload);writer.Write(value.Digest);
        }
        public DemolitionSimulationPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if(reader.TryReadNil())return null;if(reader.ReadArrayHeader()!=8)throw new InvalidOperationException("Unsupported demolition simulation packet.");
            return new DemolitionSimulationPacket {Protocol=reader.ReadInt32(),Kind=reader.ReadByte(),Token=PacketRead.Text(ref reader),Nonce=PacketRead.Text(ref reader),Operation=reader.ReadInt64(),Batch=reader.ReadInt32(),Payload=PacketRead.Bytes(ref reader,ProtocolPolicy.TargetBytes*DemolitionPolicy.MaximumBatchTargets),Digest=PacketRead.Text(ref reader)};
        }
    }
    public sealed class DemolitionControlFormatter : IMessagePackFormatter<DemolitionControlPacket>
    {
        public void Serialize(ref MessagePackWriter writer, DemolitionControlPacket value, MessagePackSerializerOptions options)
        {
            if(value==null){writer.WriteNil();return;}writer.WriteArrayHeader(15);writer.Write(value.Protocol);writer.Write(value.Kind);writer.Write(value.ProbeNonce);writer.Write(value.PeerNonce);writer.Write(value.Token);writer.Write(value.Fingerprint);writer.Write(value.Roster);writer.Write(value.Origin);writer.Write(value.Operation);writer.Write(value.Batch);writer.Write(value.Command);writer.Write(value.Tick);writer.Write(value.InputDigest);writer.Write(value.ResultDigest);writer.Write(value.Failed);
        }
        public DemolitionControlPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if(reader.TryReadNil())return null;if(reader.ReadArrayHeader()!=15)throw new InvalidOperationException("Unsupported demolition control packet.");
            return new DemolitionControlPacket {Protocol=reader.ReadInt32(),Kind=reader.ReadByte(),ProbeNonce=PacketRead.Text(ref reader),PeerNonce=PacketRead.Text(ref reader),Token=PacketRead.Text(ref reader),Fingerprint=PacketRead.Text(ref reader),Roster=PacketRead.Bytes(ref reader,1+ProtocolPolicy.MaximumPeers*9),Origin=reader.ReadUInt64(),Operation=reader.ReadInt64(),Batch=reader.ReadInt32(),Command=reader.ReadInt32(),Tick=reader.ReadInt32(),InputDigest=PacketRead.Text(ref reader),ResultDigest=PacketRead.Text(ref reader),Failed=reader.ReadBoolean()};
        }
    }
}

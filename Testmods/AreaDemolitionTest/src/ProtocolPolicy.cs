using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AreaDemolitionTest
{
    internal sealed class ProtocolPeer
    {
        internal ulong SteamId;
        internal int PlayerId;
        internal string Nonce = "";
    }

    // Pure production policy: the native proof, not asynchronous ACK arrival, admits execution.
    internal sealed class NativeProofLedger
    {
        private readonly Dictionary<string, string> proofs = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Receipt> receipts = new Dictionary<string, Receipt>(StringComparer.Ordinal);
        internal sealed class Receipt
        {
            internal long Operation;
            internal int Batch;
            internal string Input, Result;
            internal bool Failed;
        }
        internal void Clear() { proofs.Clear(); receipts.Clear(); }
        internal void Prove(string token, string localNonce) => proofs[token] = localNonce;
        internal bool HasProof(string token, string localNonce) => proofs.TryGetValue(token, out string nonce) && nonce == localNonce;
        internal bool CanApply(string origin, long operation, int batch, string digest, out Receipt duplicate)
        {
            duplicate = null;
            if (operation <= 0 || batch < 0) return false;
            if (!receipts.TryGetValue(origin, out var previous)) return batch == 0;
            if (operation == previous.Operation && batch == previous.Batch)
            { if (digest == previous.Input) duplicate = previous; return false; }
            return operation > previous.Operation ? batch == 0 : operation == previous.Operation && batch == previous.Batch + 1;
        }
        internal void Complete(string origin, long operation, int batch, string input, string result, bool failed = false) =>
            receipts[origin] = new Receipt { Operation = operation, Batch = batch, Input = input, Result = result, Failed = failed };
    }

    internal static class ProtocolPolicy
    {
        // Version 2 admits decoration records. Old peers must reject this contract explicitly.
        internal const int Version = 2, MaximumPeers = 32, TargetBytes = 31;
        internal static string NewNonce()
        { var bytes = new byte[16]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes); return Hex(bytes); }
        internal static bool IsNonce(string value) => IsHex(value, 32);
        internal static bool IsDigest(string value) => IsHex(value, 64);
        private static bool IsHex(string value, int length) => value != null && value.Length == length && value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
        internal static byte[] Unhex(string value)
        { var bytes = new byte[value.Length / 2]; for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return bytes; }
        internal static ProtocolPeer[] CanonicalPeers(IEnumerable<ProtocolPeer> source) => source.OrderBy(p => p.SteamId).Select(p => new ProtocolPeer { SteamId=p.SteamId, PlayerId=p.PlayerId, Nonce=p.Nonce }).ToArray();
        internal static byte[] EncodePeers(ProtocolPeer[] peers, bool nonces)
        {
            if (peers == null || peers.Length < 1 || peers.Length > MaximumPeers) throw new InvalidDataException("Unsupported peer count.");
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)peers.Length);
                ulong previous = 0;
                foreach (var peer in peers)
                {
                    if (peer.SteamId <= previous || peer.PlayerId < 0 || peer.PlayerId > 8 || nonces && !IsNonce(peer.Nonce)) throw new InvalidDataException("Noncanonical peer vector.");
                    writer.Write(peer.SteamId); writer.Write((byte)peer.PlayerId);
                    if (nonces) writer.Write(Unhex(peer.Nonce));
                    previous = peer.SteamId;
                }
                return stream.ToArray();
            }
        }
        internal static ProtocolPeer[] DecodePeers(byte[] bytes, bool nonces)
        {
            int size = nonces ? 25 : 9;
            if (bytes == null || bytes.Length < 1 || bytes[0] < 1 || bytes[0] > MaximumPeers || bytes.Length != 1 + bytes[0] * size) throw new InvalidDataException("Invalid peer vector.");
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                var peers = new ProtocolPeer[reader.ReadByte()]; ulong previous = 0;
                for (int i=0;i<peers.Length;i++)
                {
                    var peer = peers[i] = new ProtocolPeer { SteamId=reader.ReadUInt64(), PlayerId=reader.ReadByte(), Nonce=nonces ? Hex(reader.ReadBytes(16)) : "" };
                    if (peer.SteamId <= previous || peer.PlayerId > 8) throw new InvalidDataException("Invalid peer identity.");
                    previous = peer.SteamId;
                }
                return peers;
            }
        }
        internal static string RosterDigest(ProtocolPeer[] peers) => DemolitionPolicy.Digest(Hex(EncodePeers(peers, false)));
        internal static string WorldToken(string fingerprint, ProtocolPeer[] vector) => DemolitionPolicy.Digest(fingerprint + ":" + Hex(EncodePeers(vector, true)));
        internal static string TargetHash(DemolitionTarget[] targets) => DemolitionPolicy.Digest(Hex(PackTargets(targets)));
        internal static bool HasNativeFailure(IEnumerable<ApplyResult> results) => results.Any(r => r.Status == ApplyStatus.Failed);
        internal static bool HasFailedAcknowledgement(IEnumerable<DemolitionControlPacket> acknowledgements) => acknowledgements.Any(p => p.Failed);
        internal static bool OwnFreshNonce(ProtocolPeer[] vector, ulong localSteam, string nonce) => vector.Any(p => p.SteamId == localSteam && p.Nonce == nonce);
        internal static bool ValidateSimulationPacket(DemolitionSimulationPacket packet)
        {
            if(packet==null || packet.Protocol!=Version || packet.Kind>(byte)SimulationPacketKind.ApplyBatch || !IsDigest(packet.Token) || !IsNonce(packet.Nonce) || !IsDigest(packet.Digest))return false;
            try
            {
                if((SimulationPacketKind)packet.Kind==SimulationPacketKind.ReadyProof) return packet.Operation==0 && packet.Batch==0 && DecodePeers(packet.Payload,true).Length>0;
                return packet.Operation>0 && packet.Batch>=0 && UnpackTargets(packet.Payload).Length>0;
            }
            catch {return false;}
        }
        private static bool ValidGroup(TargetKind kind,TargetGroup group)
        {
            switch(kind)
            {
                case TargetKind.Building:
                    return group==TargetGroup.Buildings || group==TargetGroup.Fortifications || group==TargetGroup.PositiveFear || group==TargetGroup.NegativeFear || group==TargetGroup.DogCages || group==TargetGroup.KillingPits;
                case TargetKind.Pitch:return group==TargetGroup.Pitch;
                case TargetKind.Wall:return group==TargetGroup.Fortifications;
                case TargetKind.DugMoat:return group==TargetGroup.DugMoats;
                case TargetKind.Decoration:return group==TargetGroup.Decorations;
                default:return false;
            }
        }
        internal static byte[] PackTargets(DemolitionTarget[] targets)
        {
            if (targets == null || targets.Length < 1 || targets.Length > DemolitionPolicy.MaximumBatchTargets) throw new InvalidDataException("Invalid target count.");
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                foreach (var target in targets)
                {
                    if (target == null || !ValidGroup(target.Kind,target.Group) || target.Owner < 1 || target.Owner > 8 || target.Slot < 0 || target.Type < 0 && target.Kind != TargetKind.Wall || target.X < 0 || target.X > short.MaxValue || target.Y < 0 || target.Y > short.MaxValue) throw new InvalidDataException("Invalid target record.");
                    writer.Write((byte)target.Kind); writer.Write((byte)target.Group); writer.Write(target.Slot); writer.Write(target.Type); writer.Write((byte)target.Owner);
                    writer.Write((short)target.X); writer.Write((short)target.Y); writer.Write(target.TileId); writer.Write(target.TaskIndex); writer.Write(target.GlobalId); writer.Write(target.Flags);
                }
                return stream.ToArray();
            }
        }
        internal static DemolitionTarget[] UnpackTargets(byte[] bytes)
        {
            if (bytes == null || bytes.Length < TargetBytes || bytes.Length % TargetBytes != 0 || bytes.Length > TargetBytes * DemolitionPolicy.MaximumBatchTargets) throw new InvalidDataException("Invalid target framing.");
            using (var reader = new BinaryReader(new MemoryStream(bytes, false)))
            {
                var targets = new DemolitionTarget[bytes.Length / TargetBytes];
                for (int i=0;i<targets.Length;i++) targets[i] = new DemolitionTarget { Kind=(TargetKind)reader.ReadByte(), Group=(TargetGroup)reader.ReadByte(), Slot=reader.ReadInt32(), Type=reader.ReadInt32(), Owner=reader.ReadByte(), X=reader.ReadInt16(), Y=reader.ReadInt16(), TileId=reader.ReadInt32(), TaskIndex=reader.ReadInt32(), GlobalId=reader.ReadUInt32(), Flags=reader.ReadUInt32() };
                PackTargets(targets); // Apply the same bounded domain contract on received records.
                return targets;
            }
        }
    }
}

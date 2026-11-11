using System;
using System.Linq;
using AreaDemolitionTest;
using MessagePack;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string message) {checks++;if(!value)throw new InvalidOperationException(message);}
    private static void Reject(Action action,string message) {try {action();}catch {checks++;return;}throw new InvalidOperationException(message);}
    private static void Main()
    {
        string nonce=ProtocolPolicy.NewNonce(), other=ProtocolPolicy.NewNonce();
        var vector=ProtocolPolicy.CanonicalPeers(new[] {new ProtocolPeer {SteamId=20,PlayerId=0,Nonce=other},new ProtocolPeer {SteamId=10,PlayerId=1,Nonce=nonce}});
        var restored=ProtocolPolicy.DecodePeers(ProtocolPolicy.EncodePeers(vector,true),true);
        Check(restored[1].PlayerId==0 && restored[1].SteamId==20,"Spectator endpoint was discarded.");
        Check(ProtocolPolicy.OwnFreshNonce(restored,10,nonce),"Fresh local nonce must match.");
        Check(!ProtocolPolicy.OwnFreshNonce(restored,10,other),"Queued proof from an earlier world must fail local freshness.");
        string fingerprint=DemolitionPolicy.Digest("adapter"), token=ProtocolPolicy.WorldToken(fingerprint,restored);
        Check(token==ProtocolPolicy.WorldToken(fingerprint,vector),"Canonical proof must agree across peers.");
        restored[1].Nonce=ProtocolPolicy.NewNonce();
        Check(token!=ProtocolPolicy.WorldToken(fingerprint,restored),"Changed peer world must change token.");
        Reject(()=>ProtocolPolicy.DecodePeers(new byte[] {32},true),"Truncated vector accepted.");

        var ledger=new NativeProofLedger(); string input=DemolitionPolicy.Digest("targets"), result=DemolitionPolicy.Digest("outcome");
        Check(!ledger.HasProof(token,nonce),"Asynchronous readiness alone cannot grant a native proof.");
        ledger.Prove(token,nonce);
        Check(ledger.HasProof(token,nonce),"Native proof is sufficient independently of local ACK timing.");
        Check(!ledger.HasProof(token,other),"Proof crossed a local world replacement.");
        Check(ledger.CanApply("origin",1,0,input,out _),"First batch rejected.");
        ledger.Complete("origin",1,0,input,result);
        Check(!ledger.CanApply("origin",1,0,input,out var duplicate) && duplicate.Result==result,"Exact replay did not return cached outcome.");
        Check(!ledger.CanApply("origin",1,0,result,out duplicate) && duplicate==null,"Altered replay accepted.");
        Check(!ledger.CanApply("origin",1,2,input,out _),"Missing batch gap accepted.");
        Check(ledger.CanApply("origin",1,1,input,out _),"Next batch rejected.");
        Check(ledger.CanApply("origin",3,0,input,out _),"A cancelled unsent operation must not require missing packets.");
        ledger.Clear(); Check(!ledger.HasProof(token,nonce),"End retained native authorization.");

        var targets=Enumerable.Range(0,32).Select(i=>new DemolitionTarget {Kind=TargetKind.DugMoat,Group=TargetGroup.DugMoats,Owner=8,Slot=int.MaxValue-i,Type=int.MaxValue,X=short.MaxValue,Y=short.MaxValue,TileId=int.MaxValue-i,TaskIndex=int.MaxValue,GlobalId=uint.MaxValue-(uint)i,Flags=uint.MaxValue}).ToArray();
        var packed=ProtocolPolicy.PackTargets(targets); var unpacked=ProtocolPolicy.UnpackTargets(packed);
        Check(packed.Length==32*ProtocolPolicy.TargetBytes && DemolitionPolicy.TargetDigest(unpacked)==DemolitionPolicy.TargetDigest(targets),"Stable target identity changed over the wire.");
        var packet=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ApplyBatch,Token=token,Nonce=nonce,Operation=long.MaxValue,Batch=int.MaxValue,Payload=packed,Digest=DemolitionPolicy.TargetDigest(targets)};
        byte[] bytes=MessagePackSerializer.Serialize(packet);
        Check(bytes.Length+2<=1200,"Worst-value 32-target batch exceeds native transport cap.");
        var decoded=MessagePackSerializer.Deserialize<DemolitionSimulationPacket>(bytes);
        Check(decoded.Operation==long.MaxValue && decoded.Batch==int.MaxValue && decoded.Digest==packet.Digest,"Explicit packet formatter lost identity.");
        Check(ProtocolPolicy.ValidateSimulationPacket(decoded),"Current bounded receive schema rejected.");
        decoded.Protocol=1;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"The previous decoration-free protocol must not execute on this receiver.");
        decoded.Protocol=ProtocolPolicy.Version;
        decoded.Kind=byte.MaxValue;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"Unknown simulation message kind accepted.");
        var malformed=(byte[])bytes.Clone();malformed[0]=0x97;
        Reject(()=>MessagePackSerializer.Deserialize<DemolitionSimulationPacket>(malformed),"Old/incomplete packet shape accepted.");
        var wrongType=(byte[])bytes.Clone();wrongType[1]=0xA0;
        Reject(()=>MessagePackSerializer.Deserialize<DemolitionSimulationPacket>(wrongType),"String in the numeric protocol field accepted.");
        decoded.Kind=(byte)SimulationPacketKind.ApplyBatch;decoded.Payload=Array.Empty<byte>();
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"Empty batch accepted at the receive boundary.");
        Reject(()=>ProtocolPolicy.UnpackTargets(packed.Concat(new byte[1]).ToArray()),"Misframed target records accepted.");
        Reject(()=>ProtocolPolicy.PackTargets(targets.Concat(new[] {targets[0]}).ToArray()),"Oversized batch accepted.");
        targets[0].Kind=TargetKind.Wall;targets[0].Group=TargetGroup.Fortifications;targets[0].Type=int.MinValue;
        Check(ProtocolPolicy.UnpackTargets(ProtocolPolicy.PackTargets(targets))[0].Type==int.MinValue,"Signed wall signature changed over the wire.");
        targets[0].Owner=0; Reject(()=>ProtocolPolicy.PackTargets(targets),"Owner zero accepted.");
        targets[0].Owner=8;targets[0].Kind=TargetKind.Decoration;targets[0].Group=TargetGroup.Decorations;targets[0].Type=int.MaxValue;
        var decorationBytes=ProtocolPolicy.PackTargets(targets);
        Check(ProtocolPolicy.UnpackTargets(decorationBytes)[0].Kind==TargetKind.Decoration,"Decoration identity was lost over the wire.");
        var decorationPacket=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ApplyBatch,Token=token,Nonce=nonce,Operation=long.MaxValue,Batch=int.MaxValue,Payload=decorationBytes,Digest=ProtocolPolicy.TargetHash(targets)};
        Check(ProtocolPolicy.ValidateSimulationPacket(decorationPacket) && MessagePackSerializer.Serialize(decorationPacket).Length+2<=1200,"Expanded 32-target schema exceeds cap or rejects decorations.");
        targets[0].Group=TargetGroup.Buildings;
        Reject(()=>ProtocolPolicy.PackTargets(targets),"Decoration with a building-only group accepted.");
        targets[0].Group=TargetGroup.Decorations;targets[0].Kind=(TargetKind)byte.MaxValue;
        Reject(()=>ProtocolPolicy.PackTargets(targets),"Unknown target kind accepted.");
        var invalidRecord=(byte[])decorationBytes.Clone();invalidRecord[1]=byte.MaxValue;
        Reject(()=>ProtocolPolicy.UnpackTargets(invalidRecord),"Unknown received target group accepted.");
        var skipped=new[] {new ApplyResult {Status=ApplyStatus.Missing},new ApplyResult {Status=ApplyStatus.Changed},new ApplyResult {Status=ApplyStatus.Forbidden}};
        Check(!ProtocolPolicy.HasNativeFailure(skipped),"Deterministic skips are not native failures.");
        Check(ProtocolPolicy.HasNativeFailure(skipped.Concat(new[] {new ApplyResult {Status=ApplyStatus.Failed}})),"Native failure must stop offline execution.");
        var failureAck=new DemolitionControlPacket {Kind=(byte)ControlPacketKind.ExecutionAck,Failed=true,ResultDigest=result};
        var decodedAck=MessagePackSerializer.Deserialize<DemolitionControlPacket>(MessagePackSerializer.Serialize(failureAck));
        Check(decodedAck.Failed,"Failure flag was lost by the explicit formatter.");
        Check(ProtocolPolicy.HasFailedAcknowledgement(new[] {decodedAck,new DemolitionControlPacket {ResultDigest=result}}),"Equal digests must not hide a native failure.");
        Check(!ProtocolPolicy.HasFailedAcknowledgement(new[] {new DemolitionControlPacket {ResultDigest=result}}),"Successful acknowledgement incorrectly failed.");
        ledger.Complete("failure",1,0,input,result,true);
        Check(!ledger.CanApply("failure",1,0,input,out duplicate) && duplicate.Failed,"Duplicate failure lost its stop flag.");
        Console.WriteLine("PASS: "+checks+" protocol checks.");
    }
}

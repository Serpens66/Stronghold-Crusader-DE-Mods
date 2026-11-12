using System;
using System.Linq;
using BugfixesAndQoL.AreaDemolition;
using MessagePack;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

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

        var targets=Enumerable.Range(0,32).Select(i=>new DemolitionTarget {Kind=TargetKind.DugMoat,Group=TargetGroup.DugMoats,Owner=8,Slot=int.MaxValue-i,Type=int.MaxValue,X=short.MaxValue,Y=short.MaxValue,TileId=int.MaxValue-i,TaskIndex=int.MaxValue,GlobalId=uint.MaxValue-(uint)i,Flags=uint.MaxValue,Dependency=i}).ToArray();
        var packed=ProtocolPolicy.PackTargets(targets); var unpacked=ProtocolPolicy.UnpackTargets(packed);
        Check(packed.Length==32*ProtocolPolicy.TargetBytes && DemolitionPolicy.TargetDigest(unpacked)==DemolitionPolicy.TargetDigest(targets),"Stable target identity changed over the wire.");
        var packet=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ApplyBatch,Token=token,Nonce=nonce,Operation=long.MaxValue,Batch=int.MaxValue,Payload=packed,Digest=DemolitionPolicy.TargetDigest(targets)};
        byte[] bytes=MessagePackSerializer.Serialize(packet);
        Console.WriteLine("Maximum-value32-target packet: "+(bytes.Length+2)+" bytes including2-byte packet ID.");
        Check(bytes.Length+2<1200,"Worst-value 32-target batch lacks safe native transport margin.");
        var decoded=MessagePackSerializer.Deserialize<DemolitionSimulationPacket>(bytes);
        Check(decoded.Operation==long.MaxValue && decoded.Batch==int.MaxValue && decoded.Digest==packet.Digest,"Explicit packet formatter lost identity.");
        Check(ProtocolPolicy.ValidateSimulationPacket(decoded),"Current bounded receive schema rejected.");
        decoded.Protocol=1;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"The previous decoration-free protocol must not execute on this receiver.");
        decoded.Protocol=2;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"The previous stockpile-free group contract must not execute on this receiver.");
        decoded.Protocol=3;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"Peers with the combined wall/fortification group must not execute this split-group contract.");
        decoded.Protocol=4;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"Packets without frozen dependency metadata cannot execute on this receiver.");
        decoded.Protocol=5;
        Check(!ProtocolPolicy.ValidateSimulationPacket(decoded),"Readiness proofs without explicit attempt identities cannot execute on this receiver.");
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
        targets[0].Flags&=~(uint)TilePropertyFlag.IsStairs;
        Reject(()=>ProtocolPolicy.PackTargets(targets),"An ordinary wall cannot claim the fortifications group.");
        targets[0].Group=TargetGroup.Walls;
        byte[] wallBytes=ProtocolPolicy.PackTargets(targets);
        Check(ProtocolPolicy.UnpackTargets(wallBytes)[0].Group==TargetGroup.Walls,"Independent ordinary-wall group was lost over the wire.");
        var forgedWall=(byte[])wallBytes.Clone();forgedWall[0]=(byte)((byte)TargetKind.Wall|((byte)TargetGroup.Fortifications<<3));
        Reject(()=>ProtocolPolicy.UnpackTargets(forgedWall),"Received ordinary wall with the stair-only group accepted.");
        targets[0].Flags|=(uint)TilePropertyFlag.IsStairs;
        Reject(()=>ProtocolPolicy.PackTargets(targets),"A stair tile cannot claim the ordinary-wall group.");
        targets[0].Group=TargetGroup.Fortifications;
        Check(ProtocolPolicy.UnpackTargets(ProtocolPolicy.PackTargets(targets))[0].Group==TargetGroup.Fortifications,"Stair flag did not preserve the fortifications group.");
        targets[0].Kind=TargetKind.Building;targets[0].Group=TargetGroup.Walls;
        foreach(var wallType in new[]{eStructs.STRUCT_WOOD_WALL,eStructs.STRUCT_STONE_WALL,eStructs.STRUCT_CRENAL_WALL})
        {
            targets[0].Type=(int)wallType;
            var structuralWall=ProtocolPolicy.UnpackTargets(ProtocolPolicy.PackTargets(targets))[0];
            Check(structuralWall.Kind==TargetKind.Building && structuralWall.Group==TargetGroup.Walls && structuralWall.Type==(int)wallType,"Native structural-wall records must retain their kind, type and independent walls group.");
        }
        foreach(var wrongWallType in new[]{eStructs.STRUCT_TOWER1,eStructs.STRUCT_GATE_MAIN})
        {
            targets[0].Type=(int)wrongWallType;
            Reject(()=>ProtocolPolicy.PackTargets(targets),"A tower or gate cannot claim the walls-only filter group.");
        }
        targets[0].Kind=TargetKind.Wall;targets[0].Group=TargetGroup.Fortifications;targets[0].Type=int.MinValue;
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
        var invalidRecord=(byte[])decorationBytes.Clone();invalidRecord[0]=(byte)((byte)TargetKind.Decoration|(15<<3));
        Reject(()=>ProtocolPolicy.UnpackTargets(invalidRecord),"Unknown received target group accepted.");
        targets[0].Kind=TargetKind.Building;targets[0].Group=TargetGroup.Stockpiles;
        var stockpileBytes=ProtocolPolicy.PackTargets(targets);
        Check(ProtocolPolicy.UnpackTargets(stockpileBytes)[0].Group==TargetGroup.Stockpiles,"Stockpile group was lost over the wire.");
        var stockpilePacket=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ApplyBatch,Token=token,Nonce=nonce,Operation=long.MaxValue,Batch=int.MaxValue,Payload=stockpileBytes,Digest=ProtocolPolicy.TargetHash(targets)};
        Check(ProtocolPolicy.ValidateSimulationPacket(stockpilePacket) && MessagePackSerializer.Serialize(stockpilePacket).Length+2<=1200,"Stockpile receive schema rejects a bounded 32-target batch or exceeds transport cap.");
        targets[0].Kind=TargetKind.Decoration;
        Reject(()=>ProtocolPolicy.PackTargets(targets),"Decoration cannot claim the stockpile-only building group.");
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
        Components();PackedCategories();ReadinessPolicies();DispatchPolicies();ExecutionAcknowledgements();
        Console.WriteLine("PASS: "+checks+" protocol checks.");
    }
    private static DemolitionTarget Target(int id) => new DemolitionTarget {Kind=TargetKind.Building,Group=TargetGroup.Buildings,Owner=1,Type=(int)eStructs.STRUCT_HOVEL,Slot=id,GlobalId=(uint)id,TileId=id,X=id,Y=1,OccupiedTiles=new[]{id}};
    private static PreviewRootResult Closure(DemolitionTarget root,params DemolitionTarget[] members) => new PreviewRootResult {Root=root,Closure=members};
    private static void Components()
    {
        var parent=Target(1);var second=Target(2);var shared=Target(3);var isolated=Target(4);
        shared.Kind=TargetKind.Decoration;shared.Group=TargetGroup.Decorations;shared.Type=(int)ProjectileType.Flag1;
        var roots=new[]{Closure(parent,parent,shared),Closure(second,second,shared),Closure(isolated,isolated)};
        var frozen=DemolitionPolicy.FreezeComponents(roots);
        int linked=frozen.Single(t=>t.Identity==parent.Identity).Dependency;
        Check(frozen.Length==4 && frozen.Where(t=>t.Identity!=isolated.Identity).All(t=>t.Dependency==linked) && frozen.Single(t=>t.Identity==isolated.Identity).Dependency!=linked,"Overlapping closures merge while an isolated singleton stays independent.");
        var reordered=DemolitionPolicy.FreezeComponents(roots.Reverse().Select(r=>Closure(r.Root,r.Closure.Reverse().ToArray())));
        Check(DemolitionPolicy.TargetDigest(frozen)==DemolitionPolicy.TargetDigest(reordered),"Frozen ordering and component labels must be independent of root traversal.");
        parent.Owner=2;parent.OccupiedTiles[0]=999;
        var batches=DemolitionPolicy.MakeBatches(frozen);
        var frozenParent=batches.SelectMany(b=>b).Single(t=>t.Identity==parent.Identity);
        var frozenChild=batches.SelectMany(b=>b).Single(t=>t.Identity==shared.Identity);
        Check(frozenParent.Owner==1 && frozenParent.OccupiedTiles[0]==1 && frozenParent.Dependency==frozenChild.Dependency,"A later lost/changed live parent cannot erase frozen parent/child membership during pure batching.");
        Check(frozen[0].Dependency==0 && frozen.Single(t=>t.Identity==isolated.Identity).Dependency==1,"Batch rebasing must not mutate the frozen selection.");
        Check(batches.All(b=>b.Select(t=>t.Identity).SequenceEqual(DemolitionPolicy.Canonicalize(b).Select(t=>t.Identity))),"Packet ordering remains canonical when component kinds interleave.");
        var reversed=DemolitionPolicy.MakeBatches(frozen.Reverse().ToArray());
        Check(batches.Length==reversed.Length && batches.Zip(reversed,(a,b)=>DemolitionPolicy.TargetDigest(a)==DemolitionPolicy.TargetDigest(b)).All(equal=>equal),"Permuting frozen inputs preserves packet order, labels and digest.");
        string digest=DemolitionPolicy.TargetDigest(frozen);var altered=frozen.Select(t=>t.Copy()).ToArray();altered.Single(t=>t.Identity==isolated.Identity).Dependency=0;
        Check(digest!=DemolitionPolicy.TargetDigest(altered),"Dependency membership must be authenticated by the target digest.");
        var conflicting=shared.Copy();conflicting.Owner=8;
        Reject(()=>DemolitionPolicy.FreezeComponents(new[]{Closure(second,second,shared),Closure(parent,parent,conflicting)}),"Conflicting snapshots cannot silently merge a shared target.");
        Reject(()=>DemolitionPolicy.FreezeComponents(new[]{Closure(second,shared)}),"A frozen closure without its parent must fail closed.");
        var singles=DemolitionPolicy.FreezeComponents(Enumerable.Range(10,70).Select(id=>{var target=Target(id);return Closure(target,target);}));
        Check(singles.Select(t=>t.Dependency).Distinct().Count()==70 && singles.Max(t=>t.Dependency)>31,"Global frozen component IDs may exceed the one-packet range.");
        var split=DemolitionPolicy.MakeBatches(singles);
        Check(split.Length==3 && split.SelectMany(b=>b).Count()==70 && split.All(b=>b.Length<=32),"Singleton components pack across bounded packets without loss.");
        foreach(var batch in split)
        {
            Check(batch.Select(t=>t.Dependency).Distinct().OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,batch.Length)),"Each packet rebases independent components densely.");
            Check(DemolitionPolicy.TargetDigest(batch)==DemolitionPolicy.TargetDigest(ProtocolPolicy.UnpackTargets(ProtocolPolicy.PackTargets(batch))),"Rebased dependency IDs must survive wire roundtrip.");
        }
        var large=Enumerable.Range(100,33).Select(Target).ToArray();var exact=Enumerable.Range(200,32).Select(Target).ToArray();var lone=Target(300);
        var sized=DemolitionPolicy.FreezeComponents(new[]{Closure(large[0],large.Take(17).ToArray()),Closure(large[32],large.Skip(16).ToArray()),Closure(exact[0],exact),Closure(lone,lone)});
        var bounded=DemolitionPolicy.MakeBatches(sized);
        Check(bounded.Length==2 && bounded[0].Length==32 && bounded[1].Length==1 && bounded.SelectMany(b=>b).All(t=>t.Slot>=200),"An oversized atomic component is skipped whole; an exact32 component and unrelated singleton survive.");
        Check(bounded[0].All(t=>t.Dependency==0) && bounded[1][0].Dependency==0,"Atomic components cannot split between packets.");
        var invalid=lone.Copy();invalid.Dependency=-1;Reject(()=>DemolitionPolicy.MakeBatches(new[]{invalid}),"Negative frozen component ID accepted.");
        invalid.Dependency=1;Reject(()=>DemolitionPolicy.MakeBatches(new[]{invalid}),"Out-of-range frozen component ID accepted.");
        var duplicate=frozen[0].Copy();duplicate.Dependency=1;
        Reject(()=>DemolitionPolicy.MakeBatches(frozen.Concat(new[]{duplicate}).ToArray()),"One identity cannot belong to conflicting frozen components.");
        Check(DemolitionPolicy.MakeBatches(Array.Empty<DemolitionTarget>()).Length==0,"An empty frozen selection produces no packet.");
    }
    private static void PackedCategories()
    {
        var examples=new[]{Target(1),Target(2),Target(3),Target(4),Target(5),Target(6),Target(7),Target(8),Target(9),Target(10),Target(11),Target(12)};
        var buildingGroups=new[]{TargetGroup.Buildings,TargetGroup.Fortifications,TargetGroup.PositiveFear,TargetGroup.NegativeFear,TargetGroup.DogCages,TargetGroup.KillingPits,TargetGroup.Stockpiles};
        for(int i=0;i<buildingGroups.Length;i++)examples[i].Group=buildingGroups[i];
        examples[7].Group=TargetGroup.Walls;examples[7].Type=(int)eStructs.STRUCT_STONE_WALL;
        examples[8].Kind=TargetKind.Pitch;examples[8].Group=TargetGroup.Pitch;
        examples[9].Kind=TargetKind.Wall;examples[9].Group=TargetGroup.Walls;
        examples[10].Kind=TargetKind.DugMoat;examples[10].Group=TargetGroup.DugMoats;
        examples[11].Kind=TargetKind.Decoration;examples[11].Group=TargetGroup.Decorations;
        foreach(var target in examples)
        {
            var bytes=ProtocolPolicy.PackTargets(new[]{target});var roundtrip=ProtocolPolicy.UnpackTargets(bytes)[0];
            Check(roundtrip.Kind==target.Kind && roundtrip.Group==target.Group,"Packed category must preserve each valid kind/group pair.");
            var reserved=(byte[])bytes.Clone();reserved[0]|=0x80;Reject(()=>ProtocolPolicy.UnpackTargets(reserved),"Reserved category bit accepted.");
        }
        var invalidHeader=ProtocolPolicy.PackTargets(new[]{examples[0]});invalidHeader[0]=7;
        Reject(()=>ProtocolPolicy.UnpackTargets(invalidHeader),"Unknown packed target kind accepted.");
        var invalid=examples[0].Copy();invalid.Dependency=-1;Reject(()=>ProtocolPolicy.PackTargets(new[]{invalid}),"Negative dependency accepted by sender.");
        invalid.Dependency=1;Reject(()=>ProtocolPolicy.PackTargets(new[]{invalid}),"Dependency beyond target count accepted by sender.");
        var invalidWire=ProtocolPolicy.PackTargets(new[]{examples[0]});invalidWire[1]=1;
        Reject(()=>ProtocolPolicy.UnpackTargets(invalidWire),"Dependency beyond target count accepted by receiver.");
    }
    private static void ReadinessPolicies()
    {
        string a=ProtocolPolicy.NewNonce(),b=ProtocolPolicy.NewNonce(),c=ProtocolPolicy.NewNonce();
        var abc=new[]{new ProtocolPeer {SteamId=11,PlayerId=1,Nonce=a},new ProtocolPeer {SteamId=22,PlayerId=2,Nonce=b},new ProtocolPeer {SteamId=33,PlayerId=0,Nonce=c}};
        var ab=abc.Take(2).ToArray();string fingerprint=DemolitionPolicy.Digest("readiness-adapter");
        string abcToken=ProtocolPolicy.WorldToken(fingerprint,abc),abToken=ProtocolPolicy.WorldToken(fingerprint,ab);
        DemolitionControlPacket Ack(ulong sender,long attempt,string token,int command=42,int tick=123) => new DemolitionControlPacket {
            Kind=(byte)ControlPacketKind.ProofAck,Operation=attempt,Token=token,ProbeNonce=a,PeerNonce=sender==11?a:sender==22?b:c,Command=command,Tick=tick};
        var proofs=new NativeProofLedger();proofs.Prove(abcToken,a);
        var ready=new ReadinessAckLedger();ready.Begin(11,a,abcToken,1,abc);
        Check(ready.TryAccept(22,Ack(22,1,abcToken)) && ready.Evaluate()==ReadinessAckState.Pending,"Remote readiness alone cannot bypass the local native proof or spectator acknowledgement.");
        var mutableAck=Ack(11,1,abcToken);Check(ready.TryAccept(11,mutableAck),"Local native reply uses the same validated ACK boundary.");
        mutableAck.Command=999;mutableAck.Tick=999;
        Check(ready.TryAccept(33,Ack(33,1,abcToken)) && ready.Evaluate()==ReadinessAckState.Ready,"All simulator endpoints must agree, and accepted receipts must be immutable.");
        ready.Clear();
        Check(!ready.TryAccept(22,Ack(22,1,abcToken)) && !ready.TryAccept(11,Ack(11,1,abcToken)),"Roster invalidation rejects both queued remote ACKs and delayed native local replies.");
        Check(proofs.HasProof(abcToken,a),"Outgoing readiness invalidation must not retire an already proved native world.");
        ready.Begin(11,a,abToken,2,ab);
        Check(!ready.TryAccept(22,Ack(22,1,abcToken)),"An old roster ACK cannot poison the new candidate.");
        Check(ready.TryAccept(11,Ack(11,2,abToken)) && ready.TryAccept(22,Ack(22,2,abToken)) && ready.Evaluate()==ReadinessAckState.Ready,"The replacement two-peer proof can become ready independently of retired receipts.");
        // ABC -> AB -> ABC deliberately reuses exactly the first world token.
        ready.Begin(11,a,abcToken,3,abc.Reverse().ToArray());
        Check(!ready.TryAccept(22,Ack(22,1,abcToken)) && !ready.TryAccept(11,Ack(11,1,abcToken)),"Token reuse still rejects old-attempt remote ACKs and local replies.");
        var wrongNonce=Ack(22,3,abcToken);wrongNonce.PeerNonce=ProtocolPolicy.NewNonce();
        Check(!ready.TryAccept(22,wrongNonce) && !ready.TryAccept(44,Ack(33,3,abcToken)),"The current attempt also requires the frozen endpoint identity and nonce.");
        var otherOrigin=Ack(22,3,abcToken);otherOrigin.ProbeNonce=b;
        Check(!ready.TryAccept(22,otherOrigin),"A different origin's proof ACK cannot enter this outgoing candidate.");
        Check(ready.TryAccept(22,Ack(22,3,abcToken)) && ready.TryAccept(11,Ack(11,3,abcToken)) && ready.Evaluate()==ReadinessAckState.Pending,"A spectator's current ACK remains mandatory after token reuse.");
        Check(ready.TryAccept(33,Ack(33,3,abcToken)) && ready.Evaluate()==ReadinessAckState.Ready,"Reordered peers and out-of-order ACKs preserve current-attempt readiness.");
        ready.Begin(11,a,abcToken,4,abc);
        ready.TryAccept(11,Ack(11,4,abcToken));ready.TryAccept(22,Ack(22,4,abcToken,43));ready.TryAccept(33,Ack(33,4,abcToken));
        Check(ready.Evaluate()==ReadinessAckState.Mismatch,"Different command IDs for the same attempt must not grant readiness.");
        ready.Begin(11,a,abcToken,5,abc);
        ready.TryAccept(11,Ack(11,5,abcToken));ready.TryAccept(22,Ack(22,5,abcToken,42,124));ready.TryAccept(33,Ack(33,5,abcToken));
        Check(ready.Evaluate()==ReadinessAckState.Mismatch,"Different native ticks for the same attempt must not grant readiness.");
        ready.Begin(11,a,abToken,6,ab);ab[1].Nonce=ProtocolPolicy.NewNonce();
        Check(ready.TryAccept(11,Ack(11,6,abToken)) && ready.TryAccept(22,Ack(22,6,abToken)) && ready.Evaluate()==ReadinessAckState.Ready,"Candidate endpoint nonces are copied rather than retaining mutable roster objects.");
        ab[1].Nonce=b;
        Reject(()=>ready.Begin(11,a,abToken,0,ab),"Nonpositive outgoing proof attempt accepted.");
        Check(!ready.TryAccept(11,Ack(11,6,abToken)),"Invalid Begin cannot leave the previous candidate active.");
        var proof=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ReadyProof,Token=abToken,Nonce=a,Operation=7,Digest=fingerprint,Payload=ProtocolPolicy.EncodePeers(ab,true)};
        Check(ProtocolPolicy.ValidateSimulationPacket(MessagePackSerializer.Deserialize<DemolitionSimulationPacket>(MessagePackSerializer.Serialize(proof))),"Positive proof attempts survive the unchanged simulation formatter.");
        proof.Operation=0;Check(!ProtocolPolicy.ValidateSimulationPacket(proof),"Legacy zero-attempt readiness proof accepted.");
        proof.Operation=-1;Check(!ProtocolPolicy.ValidateSimulationPacket(proof),"Negative readiness proof attempt accepted.");
        proof.Operation=7;proof.Batch=1;Check(!ProtocolPolicy.ValidateSimulationPacket(proof),"A readiness proof cannot masquerade as a gameplay batch.");
        var ack=Ack(11,7,abToken);
        Check(ProtocolPolicy.ValidateControlPacket(MessagePackSerializer.Deserialize<DemolitionControlPacket>(MessagePackSerializer.Serialize(ack))),"Positive proof attempts survive the unchanged control formatter.");
        ack.Operation=0;Check(!ProtocolPolicy.ValidateControlPacket(ack),"Zero proof ACK attempt accepted.");
        ack.Operation=-1;Check(!ProtocolPolicy.ValidateControlPacket(ack),"Negative proof ACK attempt accepted.");
        ack.Operation=7;ack.Batch=1;Check(!ProtocolPolicy.ValidateControlPacket(ack),"Proof ACK batch field must remain zero.");
        var execution=new DemolitionControlPacket {Kind=(byte)ControlPacketKind.ExecutionAck,ProbeNonce=a,PeerNonce=b,Token=abToken,Origin=11,Operation=999,Batch=2,InputDigest=fingerprint,ResultDigest=fingerprint};
        Check(ProtocolPolicy.ValidateControlPacket(execution),"Gameplay operation IDs remain independent from readiness attempt IDs.");
        ready.Begin(11,a,abToken,8,ab);Check(!ready.TryAccept(22,execution),"A gameplay ACK cannot enter the readiness ledger.");
    }
    private static void DispatchPolicies()
    {
        var disabledTicket=new GameplayDispatchTicket(9,1,0);
        int disableSends=0,refreshes=0;
        Action queuedNetworkPump=()=>{if(disabledTicket.TryBeginSend(9,1,0,disabledTicket,false,false))disableSends++;};
        LocalDemolitionGate.ApplyChange(false,()=>disabledTicket.CancelQueued(),()=>
        {
            refreshes++;
            Check(disabledTicket.Phase==GameplayDispatchPhase.Cancelled,"Disable notification cancels synchronously before presentation is requested.");
            queuedNetworkPump();
        });
        queuedNetworkPump();
        Check(disableSends==0 && refreshes==1,"Previously queued network work cannot send after local disable even if it runs before presentation.");
        var enabledTicket=new GameplayDispatchTicket(9,2,0);
        LocalDemolitionGate.ApplyChange(true,()=>enabledTicket.CancelQueued(),()=>refreshes++);
        Check(enabledTicket.TryBeginSend(9,2,0,enabledTicket,false,false),"Enabling presentation leaves authorized queued work untouched.");
        LocalDemolitionGate.ApplyChange(false,()=>enabledTicket.CancelQueued(),()=>refreshes++);
        Check(enabledTicket.Phase==GameplayDispatchPhase.Submitting,"Disabling after transport entry retains uncertain work and its receipt state.");
        int sends=0;
        var queued=new GameplayDispatchTicket(10,1,0);
        Check(queued.CancelQueued() && queued.Phase==GameplayDispatchPhase.Cancelled,"Escape before Pump cancels queued gameplay.");
        if(queued.TryBeginSend(10,1,0,queued,false,false))sends++;
        Check(sends==0,"A cancelled queued batch must never reach the transport boundary.");
        var entered=new GameplayDispatchTicket(10,2,0);
        if(entered.TryBeginSend(10,2,0,entered,false,false))sends++;
        Check(sends==1 && entered.Phase==GameplayDispatchPhase.Submitting && !entered.CancelQueued(),"Cancellation after transport entry must retain uncertain execution and its receipts.");
        entered.CompleteSend(true);
        Check(entered.Phase==GameplayDispatchPhase.Submitted && !entered.TryBeginSend(10,2,0,entered,false,false),"A submitted batch cannot be sent twice.");
        var uncertain=new GameplayDispatchTicket(10,3,0);
        Check(uncertain.TryBeginSend(10,3,0,uncertain,false,false),"Current queued gameplay can cross the transport boundary once.");
        uncertain.CompleteSend(false);
        Check(uncertain.Phase==GameplayDispatchPhase.Submitting && !uncertain.CancelQueued() && !uncertain.TryBeginSend(10,3,0,uncertain,false,false),"A failed/uncertain Send cannot be cancelled as unsent or automatically replayed.");
        var afterFailure=new GameplayDispatchTicket(10,4,0);
        if(afterFailure.TryBeginSend(10,4,0,afterFailure,false,true))sends++;
        Check(sends==1 && afterFailure.Phase==GameplayDispatchPhase.Cancelled,"An earlier send failure blocks a later queued gameplay transport call.");
        Check(!afterFailure.TryBeginSend(10,4,0,afterFailure,false,false),"Clearing a failure flag cannot resurrect discarded queued work.");
        var cancelledBeforeEntry=new GameplayDispatchTicket(10,5,0);
        Check(!cancelledBeforeEntry.TryBeginSend(10,5,0,cancelledBeforeEntry,true,false),"The actual pre-Send boundary rechecks cancellation even before explicit queue removal.");
        var staleGeneration=new GameplayDispatchTicket(10,6,0);
        Check(!staleGeneration.TryBeginSend(11,6,0,staleGeneration,false,false),"A stale world-generation item cannot start gameplay.");
        var staleOperation=new GameplayDispatchTicket(11,6,0);
        Check(!staleOperation.TryBeginSend(11,7,0,staleOperation,false,false),"A stale operation item cannot start gameplay.");
        var staleBatch=new GameplayDispatchTicket(11,7,0);
        Check(!staleBatch.TryBeginSend(11,7,1,staleBatch,false,false),"An earlier batch cannot borrow a later batch's dispatch permission.");
        var oldTicket=new GameplayDispatchTicket(11,8,0);var currentTicket=new GameplayDispatchTicket(11,8,0);
        Check(!oldTicket.TryBeginSend(11,8,0,currentTicket,false,false) && currentTicket.Phase==GameplayDispatchPhase.Queued,"An old queue item cannot mutate the current ticket even if its numeric fields match.");
        Check(currentTicket.TryBeginSend(11,8,0,currentTicket,false,false),"Discarding old queue entries does not block the new operation.");
    }
    private static void ExecutionAcknowledgements()
    {
        string own=ProtocolPolicy.NewNonce(),other=ProtocolPolicy.NewNonce(),token=DemolitionPolicy.Digest("current-token"),oldToken=DemolitionPolicy.Digest("retired-token");
        string input=DemolitionPolicy.Digest("current-input"),result=DemolitionPolicy.Digest("current-result");
        var vector=new[]{new ProtocolPeer {SteamId=11,PlayerId=1,Nonce=own},new ProtocolPeer {SteamId=22,PlayerId=2,Nonce=other}};
        var inflight=new DemolitionSimulationPacket {Kind=(byte)SimulationPacketKind.ApplyBatch,Token=token,Nonce=own,Operation=3,Batch=1,Digest=input};
        DemolitionControlPacket Ack(ulong sender) => new DemolitionControlPacket {Kind=(byte)ControlPacketKind.ExecutionAck,Origin=11,ProbeNonce=own,PeerNonce=sender==11?own:other,Token=token,Operation=3,Batch=1,InputDigest=input,ResultDigest=result};
        Check(ExecutionAckPolicy.Matches(11,11,own,inflight,vector,Ack(11)),"A current native local receipt passes the shared execution ACK policy.");
        Check(ExecutionAckPolicy.Matches(22,11,own,inflight,vector,Ack(22)),"A current authenticated remote endpoint passes the same execution ACK policy.");
        var retained=new NativeProofLedger();retained.Prove(oldToken,own);retained.Complete("old-token-origin",1,0,input,result);
        Check(!retained.CanApply("old-token-origin",1,0,input,out var cached) && cached!=null,"A retained old-token native receipt can legitimately produce a cached reply.");
        var old=Ack(11);old.Token=oldToken;old.Operation=1;old.Batch=0;old.ResultDigest=cached.Result;
        Check(!ExecutionAckPolicy.Matches(11,11,own,inflight,vector,old),"A retained old-token local reply cannot overwrite the new inflight receipt.");
        var wrong=Ack(11);wrong.Operation=2;Check(!ExecutionAckPolicy.Matches(11,11,own,inflight,vector,wrong),"Old gameplay operation receipt rejected locally.");
        wrong=Ack(11);wrong.Batch=0;Check(!ExecutionAckPolicy.Matches(11,11,own,inflight,vector,wrong),"Wrong gameplay batch receipt rejected locally.");
        wrong=Ack(11);wrong.InputDigest=DemolitionPolicy.Digest("other-input");Check(!ExecutionAckPolicy.Matches(11,11,own,inflight,vector,wrong),"Wrong input digest receipt rejected locally.");
        wrong=Ack(22);wrong.PeerNonce=ProtocolPolicy.NewNonce();Check(!ExecutionAckPolicy.Matches(22,11,own,inflight,vector,wrong),"A changed peer world cannot supply the current receipt.");
        wrong=Ack(11);wrong.ProbeNonce=other;Check(!ExecutionAckPolicy.Matches(11,11,own,inflight,vector,wrong),"A reply for another origin world is rejected locally.");
        wrong=Ack(11);wrong.Origin=22;Check(!ExecutionAckPolicy.Matches(11,11,own,inflight,vector,wrong),"A reply for another issuer is rejected locally.");
        Check(!ExecutionAckPolicy.Matches(33,11,own,inflight,vector,Ack(22)),"An endpoint absent from the proof vector cannot supply an execution receipt.");
        Check(!ExecutionAckPolicy.Matches(11,11,own,null,vector,Ack(11)),"A local cached reply cannot create unsolicited inflight state.");
        Check(retained.HasProof(oldToken,own),"Filtering a stale outgoing receipt preserves retired native proof authorization.");
    }
}

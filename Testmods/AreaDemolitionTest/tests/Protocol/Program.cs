using System;
using System.Linq;
using AreaDemolitionTest;
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
        Components();PackedCategories();
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
}

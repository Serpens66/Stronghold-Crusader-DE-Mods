using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    // WIP checkpoint: codec roundtrips are present; archive atomicity/capacity,
    // malformed-frame cases and staged service lifecycle coverage remain to add.
    internal static class RescueGeometryPersistenceFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private static int Tile(int x,int y)=>y*800+x;
        private static RescueGeometryWitness Witness(int id)
        {
            var current=new RescueGeometryEndpoint(50,20,Tile(50,20),true,true,1);
            var next=new RescueGeometryEndpoint(51,20,Tile(51,20),true,true,2);
            Check(RescueGeometryWitness.TryCreate(id,(uint)(1000+id),(int)eChimps.CHIMP_TYPE_SPEARMAN,2,current,next,true,
                new[]{new RescueCandidate{X=50,Y=20,Tile=Tile(50,20),Distance=0},
                    new RescueCandidate{X=42,Y=20,Tile=Tile(42,20),Distance=8}},out var witness),"original accepted-step geometry is valid");
            return witness;
        }
        private static UnitSnapshot Joined(int id)
        {
            var type=eChimps.CHIMP_TYPE_SPEARMAN;
            Check(CommandJourneySnapshot.TryCapture(type,101,
                at=>at==0x6E4?(ushort)AliveState.IsAlive:at==0x6E6?(ushort)type:
                    at==0x6EE || at==0x930?(ushort)2:at==0x918?(ushort)101:at==0xA9E?(ushort)1:(ushort)0,
                at=>at==0x6F0?(uint)(1000+id):at==0x93C?33u:0,out var command),"fresh typed command capture");
            var fresh=new UnitSnapshot{Id=id,GlobalId=(uint)(1000+id),Type=(int)type,Owner=2,Tribe=2,TribeGlobalId=33,
                Health=100,AIState=101,Flags=2,Substep=8,X=51,Y=20,TileId=Tile(51,20),NextX=51,NextY=20,NextTileId=Tile(51,20),
                RequestedX=80,RequestedY=20,Command=command,CrewIdentity=Array.Empty<uint>()};
            Check(RescueGeometryWitness.TryJoin(Witness(id),fresh,out var joined),"original Next can be fresh Current without rewriting historical banks");
            return joined;
        }
        internal static void Run()
        {
            checks=0;var original=new RescuePendingGeometry("causal-replan",Witness(1),79,3,1);
            Check(RescueGeometryCodec.TryEncode(original,out var bytes) && RescueGeometryCodec.TryDecode(bytes,out _),"geometry-only record roundtrips at final admissible age and attempt");
            Check(RescueGeometryCodec.TryDecode(bytes,out var restored) && restored.Age==79 && restored.Attempts==3 && restored.RetryIn==1,
                "geometry record preserves original age and retry metadata");
            Check(restored.Witness.Current.X==50 && restored.Witness.Next.X==51 && restored.Witness.HasAcceptedNext,
                "geometry-only original coordinate frame is preserved");
            var joined=Joined(1);var pending=new RescuePendingCandidate<UnitSnapshot>("failed-existing-goal-replan",joined,20,1,0);
            Check(RescuePendingCodec.TryEncode(pending,out var encoded),"joined candidate with original radius9 bank encodes");
            Check(RescuePendingCodec.TryDecode(encoded,out var decoded) && decoded.Snapshot.GeometryWitness!=null,"joined candidate preserves explicit original geometry witness");
            var after=decoded.Snapshot;
            Check(after.X==51 && after.NextX==51 && after.RequestedX==80 && after.AIState==101 && after.Job==null && after.Command!=null,
                "fresh command and requested goal are retained independently of original geometry");
            Check(after.Candidates.Count==2 && after.Candidates[0].X==50 && after.Candidates[0].Distance==0 &&
                after.Candidates[1].X==42 && Math.Abs(after.Candidates[1].X-after.X)==9 && after.Candidates[1].Distance==8,
                "distance0 old origin and old radius8 bank are not reinterpreted around rebased Current");
            Console.WriteLine("PARTIAL: rescue geometry persistence "+checks+" checks; checkpoint fixture coverage incomplete; no game execution.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using TrappedUnitRescueTest;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static class CompactArchiveFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private static int Tile(int x,int y)=>x>=0 && x<800 && y>=0 && y<400?y*800+x:-1;
        private static UnitSnapshot Unit(int id,bool legacy,bool witness=true)
        {
            var type=eChimps.CHIMP_TYPE_SIEGE_TOWER;
            ushort Word(int at){if(at==0x6E4)return (ushort)AliveState.IsAlive;if(at==0x6E6)return (ushort)type;if(at==0x918)return 101;
                if(at==0x6EE || at==0xA9E)return 1;if(at==0xA0C)return 4;if(at>=0x96C && at<=0x972)return (ushort)(200+(at-0x96C)/2);return 0;}
            uint Dword(int at)=>at==0x6F0?(uint)(id+10000):at>=0x974 && at<=0x980?(uint)(300+(at-0x974)/4):0;
            Check(CommandJourneySnapshot.TryCapture(type,101,Word,Dword,out var command),"real typed tower command schema");
            var u=new UnitSnapshot{Id=id,GlobalId=(uint)(id+10000),Type=(int)type,Owner=1,Health=10000,AIState=101,
                X=100,Y=100,TileId=Tile(100,100),NextX=100,NextY=100,NextTileId=Tile(100,100),RequestedX=120,RequestedY=120,
                Flags=2,Substep=8,AffectedCurrent=true,CurrentWasOrdinary=true,NextWasOrdinary=true,Connections=255,
                CrewCount=4,CrewIdentity=new uint[]{200,300,201,301,202,302,203,303},Command=command};
            if(legacy){for(int y=92;y<=108;y++)for(int x=92;x<=108;x++)u.Candidates.Add(new RescueCandidate{X=x,Y=y,Tile=Tile(x,y),Distance=Math.Max(Math.Abs(x-100),Math.Abs(y-100))});}
            else{Check(RescueLocalField.TryCapture(100,100,Tile(100,100),Tile,_=>true,_=>255,out var field,out _),"old local field");u.LocalField=field;}
            if(witness){Check(RescueGeometryWitness.TryCapture(u,out var w) && RescueGeometryWitness.TryJoin(w,u,out var joined),"canonical P7 witness");
                RescueGeometryWitness.TryCapture(u,out w);RescueGeometryWitness.TryJoin(w,u,out u);}
            return u;
        }
        private static byte[] Owned(int count)
        {var ledger=new WorkerResumeLedger();for(int i=1;i<=count;i++){var u=Unit(i,false,false);
            var t=WorkerResumeTicket.ForCommand(i,u.GlobalId,(eChimps)u.Type,101,1,0,0,120,120,u.Command);
            Check(ledger.TryReserve(t,out var token) && ledger.CommitPark(token,0),"owned actual ledger reserve/park");}
            Check(ledger.TrySerialize(out var bytes),"actual ledger codec");return bytes;}
        private static Dictionary<string,byte[]> Copy(Dictionary<string,byte[]> source)
        {var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);foreach(var e in source)result.Add(e.Key,(byte[])e.Value.Clone());return result;}
        private static void Reject(Dictionary<string,byte[]> entries,string message)
        {Check(!RescueCompactArchiveCodec.TryDecode(entries,out var p,out var g,out _) && p==null && g==null,message+" rejects atomically");}
        internal static void Run()
        {
            checks=0;GoldenLegacy();byte[] empty=Owned(0),full=Owned(512);Check(full.Length==209930,"exact largest current ledger bytes");
            var records=new RescuePendingCandidate<UnitSnapshot>[9999];
            for(int i=0;i<records.Length;i++){
                bool old=i<512;var u=Unit(i+1,old);
                string source=old?i.ToString("D3")+new string('\u4E00',93):"AiWall";
                records[i]=old?new RescuePendingCandidate<UnitSnapshot>(source,u,79,3,1):
                    new RescuePendingCandidate<UnitSnapshot>(source,u,1250,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,false);
            }
            Check(RescueCompactArchiveCodec.TryEncode(records,Array.Empty<RescuePendingGeometry>(),full,out var maximum,out string failure),"9999 proofs+512 historical+512 actual tickets: "+failure);
            long wire=32;foreach(var entry in maximum){Check(entry.Value.Length<=1048576,"individual archive entry bounded");wire+=2+System.Text.Encoding.UTF8.GetByteCount(entry.Key)+4+entry.Value.Length;}
            Check(wire<=8388608 && RescueCompactArchiveCodec.WithinEnvelope(maximum),"uncompressed full population fits actual APSV envelope");
            Check(RescueCompactArchiveCodec.TryDecode(maximum,out var read,out var geometry,out _) && read.Length==9999 && geometry.Length==0,"whole maximal archive decoded");
            Check(read[0].Snapshot.Candidates.Count==289 && read[0].Snapshot.GeometryWitness.CandidateCount==289 &&
                read[9998].Age==1250 && read[9998].ActiveAge==-1 && ReferenceEquals(read[9998].Snapshot.LocalField,read[9998].Snapshot.GeometryWitness.LocalField),"legacy two banks+unstarted long wait+single shared modern field");
            var modern=new[]{new RescuePendingCandidate<UnitSnapshot>("AiWall",Unit(1,false),900,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,false)};
            Check(RescuePendingCodec.TryEncode(modern[0],out var single) && BitConverter.ToUInt16(single,4)==6 && RescuePendingCodec.TryDecode(single,out var one) && one.Age==900,"standalone v6 long waiting source age");
            for(int n=0;n<single.Length;n++)Check(!RescuePendingCodec.TryDecode(Sub(single,n),out _),"v6 truncation "+n);
            var invalid=(byte[])single.Clone();int body=8+BitConverter.ToUInt16(single,6);invalid[body+10]=99;Check(!RescuePendingCodec.TryDecode(invalid,out _),"unknown compact lifetime mode");
            invalid=(byte[])single.Clone();invalid[body+11]=2;Check(!RescuePendingCodec.TryDecode(invalid,out _),"noncanonical awaiting boolean");
            invalid=(byte[])single.Clone();Array.Copy(BitConverter.GetBytes(901),0,invalid,body+4,4);Check(!RescuePendingCodec.TryDecode(invalid,out _),"active age cannot exceed source age");
            Check(RescueCompactArchiveCodec.TryEncode(modern,Array.Empty<RescuePendingGeometry>(),empty,out var small,out _),"small actual archive");
            var bad=Copy(small);bad["compact/meta"][4]=99;Reject(bad,"unknown manifest version");
            bad=Copy(small);bad["compact/0000"][11]=255;bad["compact/0000"][12]=255;Reject(bad,"invalid source pool reference");
            bad=Copy(small);bad["compact/sources"][10]=(byte)'Z';Reject(bad,"unknown new native source");
            bad=Copy(small);bad["compact/0000"]=Sub(bad["compact/0000"],bad["compact/0000"].Length-1);Reject(bad,"truncated chunk");
            bad=Copy(small);bad.Add("compact/9999",new byte[0]);Reject(bad,"unexpected extra chunk");
            bad=Copy(small);bad["owned-workers"]=new byte[]{1};Reject(bad,"corrupt ownership ledger");
            var pair=new[]{modern[0],new RescuePendingCandidate<UnitSnapshot>("AiWall",Unit(2,false),0,0,0,RescueQueueLifetimeMode.ActiveWindow,-1,false)};
            Check(RescueCompactArchiveCodec.TryEncode(pair,Array.Empty<RescuePendingGeometry>(),empty,out bad,out _),"two records");
            var chunk=bad["compact/0000"];int second=10+5+BitConverter.ToUInt16(chunk,13);chunk[second+5+12]=1;chunk[second+5+13]=0;Reject(bad,"duplicate unit IDs");
            Check(!RescueCompactArchiveCodec.TryEncode(new[]{modern[0],modern[0]},Array.Empty<RescuePendingGeometry>(),empty,out _,out _),"writer rejects duplicate IDs");
            var tooMany=new RescuePendingCandidate<UnitSnapshot>[10000];Check(!RescueCompactArchiveCodec.TryEncode(tooMany,Array.Empty<RescuePendingGeometry>(),empty,out _,out _),"10000 records are not native9999");
            var oldOverflow=new RescuePendingCandidate<UnitSnapshot>[513];for(int i=0;i<513;i++)oldOverflow[i]=new RescuePendingCandidate<UnitSnapshot>("AiWall",Unit(i+1,true),0,0,0);
            Check(!RescueCompactArchiveCodec.TryEncode(oldOverflow,Array.Empty<RescuePendingGeometry>(),empty,out _,out _),"legacy bank allowance512");
            var over=new Dictionary<string,byte[]>();for(int i=0;i<8;i++)over.Add("x"+i,new byte[1048576]);Check(!RescueCompactArchiveCodec.WithinEnvelope(over),"eight MiB payload plus envelope exceeds SDK total");
            var geo=new RescuePendingGeometry("AiWall",modern[0].Snapshot.GeometryWitness,900,0,1,RescueQueueLifetimeMode.ActiveWindow,-1,true);
            Check(RescueCompactArchiveCodec.TryEncode(Array.Empty<RescuePendingCandidate<UnitSnapshot>>(),new[]{geo},empty,out var geodata,out _) &&
                RescueCompactArchiveCodec.TryDecode(geodata,out read,out geometry,out _) && read.Length==0 && geometry.Length==1 && geometry[0].AwaitingResources,"geometry-only active wait has no fabricated intent");
            Check(!RescueCompactArchiveCodec.TryEncode(modern,new[]{geo},empty,out _,out _),"cross-kind duplicate identity is atomic rejection");
            Console.WriteLine("PASS: compact archive checks="+checks+"; exact uncompressed maximal wire="+wire+"; no game execution.");
        }
        private static byte[] Sub(byte[] input,int count){var output=new byte[count];Array.Copy(input,output,count);return output;}
        private static void GoldenLegacy()
        {
            const string v4="Q1BTUgQABgBBaVdhbGwBAAAAAAAAAAAAAAABAAAAGAAAAAEAAAAAAAAAZAAAAGQAAADkOAEAZQAAAAIAAAAIAAAAZAAAAGQAAAAAAAAAZAAAAGQAAADkOAEAAAAAAAAAAADpAwAAECcAAAAAAAAAAAAAAAABAAEB/wAAAAAAAQAAAGQAAABkAAAA5DgBAAAAAAACGgAAAOQGAAACAgAAAOYGAAACGAAAAO4GAAACAQAAAPAGAAAE6QMAABgJAAACZQAAABoJAAACAAAAADAJAAACAAAAADwJAAAEAAAAADQJAAACAAAAADYJAAACAAAAAFYJAAACAAAAAFgJAAACAAAAAFoJAAACAAAAAPQJAAACAAAAAJYJAAACAAAAAJgJAAAEAAAAAJwJAAACAAAAAPgGAAAEAAAAAPYJAAACAAAAAPgJAAAEAAAAAAAKAAAEAAAAAEAKAAACAAAAAEIKAAACAAAAAIIKAAACAAAAAIYKAAACAAAAAJ4KAAACAQAAAA==",v5="Q1BTUgUABgBBaVdhbGwBAAAAAAAAAAAAAAABAAAAGAAAAAEAAAAAAAAAZAAAAGQAAADkOAEAZQAAAAIAAAAIAAAAZAAAAGQAAAAAAAAAZAAAAGQAAADkOAEAAAAAAAAAAADpAwAAECcAAAAAAAAAAAAAAAABAAEB/wAAAAAAAAAAAQAAAGQAAABkAAAA5DgBAAAAAAACGgAAAOQGAAACAgAAAOYGAAACGAAAAO4GAAACAQAAAPAGAAAE6QMAABgJAAACZQAAABoJAAACAAAAADAJAAACAAAAADwJAAAEAAAAADQJAAACAAAAADYJAAACAAAAAFYJAAACAAAAAFgJAAACAAAAAFoJAAACAAAAAPQJAAACAAAAAJYJAAACAAAAAJgJAAAEAAAAAJwJAAACAAAAAPgGAAAEAAAAAPYJAAACAAAAAPgJAAAEAAAAAAAKAAAEAAAAAEAKAAACAAAAAEIKAAACAAAAAIIKAAACAAAAAIYKAAACAAAAAJ4KAAACAQAAAA==";
            var four=Convert.FromBase64String(v4);var five=Convert.FromBase64String(v5);
            Check(RescuePendingCodec.TryDecode(four,out var old) && old.Snapshot.Type==(int)eChimps.CHIMP_TYPE_SPEARMAN && old.Snapshot.GlobalId==1001,"frozen independently specified v4 golden");
            Check(RescuePendingCodec.TryDecode(five,out var current) && current.LifetimeMode==RescueQueueLifetimeMode.AbsoluteSourceAge,"frozen v5 golden");
            Check(RescuePendingCodec.TryEncode(old,out var encoded) && Equal(encoded,five),"v4 meaning preserves original byte-identical v5 writer");
            Check(RescuePendingCodec.TryEncode(current,out encoded) && Equal(encoded,five),"v5 absolute+no field remains byte identical");
        }
        private static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
}

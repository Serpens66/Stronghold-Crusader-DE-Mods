using System;
using System.IO;
using System.Linq;
using EnemyBridgePathTest;
namespace BridgePlanningTests
{
    internal static partial class Program
    {
        // Offline-only helpers. No original command/search is replaced or stubbed.
        private static int MilitaryStrength(byte[] player)
        {
            int Read(int address)=>BitConverter.ToInt32(player,address-0x379AE00);
            unchecked
            {
                int age=Read(0x379E740),phase=Read(0x379D974),reserve=Read(0x379D994);
                int small=0,sum;
                if(phase==6)
                    sum=(age<17?reserve:reserve/8)+Read(0x379D990)+Read(0x379D984)+Read(0x379D980)+Read(0x379D97C);
                else
                {
                    small=Read(0x379D97C)/2;
                    sum=(age<13?reserve:reserve/8)+Read(0x379D990)+Read(0x379D984)+Read(0x379D980);
                }
                sum+=Read(0x379D9A0)+Read(0x379D99C)+Read(0x379D998);
                return sum>1&&(sum>4||small<=sum*2)?0:1;
            }
        }
        private static int[] MilitaryMetric(int x,int y,int tx,int ty)
        {
            // Native signed comparison followed by wrapping subtraction, including
            // INT_MIN cases; Math.Abs would have a different overflow contract.
            unchecked
            {
                int dx=x<=tx?tx-x:x-tx,dy=y<=ty?ty-y:y-ty;
                return new[]{dx,dy,Math.Min(dx,dy),Math.Max(dx,dy)};
            }
        }
        private static int CopiedLeader(short[] ids,int[] globals,byte[] units,int player)
        {
            if(player<0||player>=ids.Length||player>=globals.Length)throw new ArgumentException("leader-player-extent");
            int id=ids[player];if(id==0)return 0;
            long at=(long)id*0x490;
            if(id<0||at+0x8FA>units.Length)throw new ArgumentException("leader-unit-extent");
            return BitConverter.ToInt32(units,(int)at+0x6F0)==globals[player]&&
                BitConverter.ToInt16(units,(int)at+0x6E4)==2&&
                BitConverter.ToInt16(units,(int)at+0x8F8)==0?id:0;
        }
        private static void CopiedTribeCleanup(byte[] player,int[] classes,int[] globals)
        {
            if(classes.Length!=22||player.Length!=0x583C)throw new ArgumentException("tribe cleanup extent");
            if(BitConverter.ToInt32(player,0x379D0D0-0x379AE00)==0)return;
            for(int row=0;row<11;row++)
            {
                if(row==8)continue;
                int start=classes[row*2],count=classes[row*2+1];
                for(int slot=0;slot<count;slot++)
                {
                    int idAt=checked(0x379F670-0x379AE00+(start+slot)*2),globalAt=checked(0x379F8C8-0x379AE00+(start+slot)*4);
                    if(idAt<0||idAt+2>player.Length||globalAt<0||globalAt+4>player.Length)throw new ArgumentException("tribe class extent");
                    int id=BitConverter.ToInt16(player,idAt);if(id==0)continue;
                    if(id<0||id>=globals.Length)throw new ArgumentException("tribe identity extent");
                    if(globals[id]==BitConverter.ToInt32(player,globalAt))continue;
                    Array.Clear(player,idAt,2);Array.Clear(player,globalAt,4);
                }
            }
        }
        private static void MilitaryHelperCases(NativeImage m)
        {
            var random=new Random(1031);int[] boundary={int.MinValue,-99,-17,-1,0,1,2,4,12,13,16,17,199,int.MaxValue};
            int[] fields={0x379D994,0x379D990,0x379D984,0x379D980,0x379D97C,0x379D9A0,0x379D99C,0x379D998};
            var strength=m.Function<R2>(0x2FD40);
            for(int n=0;n<512;n++)
            {
                var p=new byte[0x583C];
                void Put(int address,int value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,p,address-0x379AE00,4);
                Put(0x379D974,n%10);Put(0x379E740,boundary[n%boundary.Length]);
                foreach(int address in fields)Put(address,n<128?boundary[random.Next(boundary.Length)]:random.Next(-100,100));
                m.Put(0x379AE00+8*0x583C,p);
                Check(strength(m.Ptr(0x366C210),8)==MilitaryStrength(p),"2FD40 full strength helper "+n);
            }
            var metric=m.Function<R5>(0x79C0);
            for(int n=0;n<256;n++)
            {
                int x=boundary[random.Next(boundary.Length)],y=boundary[random.Next(boundary.Length)],tx=boundary[random.Next(boundary.Length)],ty=boundary[random.Next(boundary.Length)];
                int[] expected=MilitaryMetric(x,y,tx,ty);
                Check(metric(m.Ptr(0x34A9F50),x,y,tx,ty)==unchecked(expected[0]+expected[1]),"79C0 return wrapping sum");
                Equal(m.Ints(0x34A9F50,4),expected,"79C0 all side effects");
            }
            var leader=m.Function<R2>(0x187E60);
            for(int n=0;n<8;n++)
            {
                var ids=new short[9];var globals=new int[9];var units=new byte[0x2000];
                ids[1]=(short)(n==0?0:2);globals[1]=701;
                int at=2*0x490;
                Buffer.BlockCopy(BitConverter.GetBytes(n==2?702:701),0,units,at+0x6F0,4);
                Buffer.BlockCopy(BitConverter.GetBytes((short)(n==3?1:2)),0,units,at+0x6E4,2);
                Buffer.BlockCopy(BitConverter.GetBytes((short)(n==4?1:0)),0,units,at+0x8F8,2);
                m.Put(0x3A0F9F0,ids);m.Put(0x3A0FA04,globals);m.Put(0x67E8400,units);
                Check(leader(m.Ptr(0x67E8400),1)==CopiedLeader(ids,globals,units,1),"187E60 identity/alive leader "+n);
            }
            bool rejected=false;try{CopiedLeader(new short[]{2},new[]{701},new byte[2],0);}catch(ArgumentException){rejected=true;}
            Check(rejected,"missing leader record never defaulted");
            for(int n=0;n<8;n++)
            {
                var p=new byte[0x583C];var classes=new int[22];var live=new int[4];
                Buffer.BlockCopy(BitConverter.GetBytes(n==0?0:1),0,p,0x379D0D0-0x379AE00,4);
                classes[0]=0;classes[1]=n==1?-1:2;classes[16]=7;classes[17]=1;
                foreach(int slot in new[]{0,1,7})
                {
                    short id=(short)(slot==7?3:slot+1);
                    Buffer.BlockCopy(BitConverter.GetBytes(id),0,p,0x379F670-0x379AE00+slot*2,2);
                    Buffer.BlockCopy(BitConverter.GetBytes(100+slot),0,p,0x379F8C8-0x379AE00+slot*4,4);
                    live[id]=n%2==0?100+slot:77;
                }
                m.Put(0x379AE00+8*0x583C,p);m.Put(0x2C80E0,classes);
                for(int id=0;id<live.Length;id++)m.Int(0x7CC6754+id*0x688,live[id]);
                var expected=(byte[])p.Clone();CopiedTribeCleanup(expected,classes,live);
                m.Function<V2>(0x2FC80)(m.Ptr(0x366C210),8);
                Equal(m.Bytes(0x379AE00+8*0x583C,0x583C),expected,"2FC80 full player identity cleanup, row8 exclusion "+n);
            }
            // These arrays are in WRITABLE .data, not immutable PE constants.
            foreach(int address in new[]{0x2C8040,0x2C80E0})
            {
                bool denied=false;try{m.ConstantBytes(address,88);}catch(Exception error){denied=error.Message.StartsWith("Unproven immutable native table");}
                Check(denied,"military class table requires own capture");
            }
            Console.WriteLine("PASS military helpers: 512 strength,256 metric,8 leader,8 full tribe-cleanup cases; writable class tables rejected; complete entry/formation NOT executed");
        }
        private static int HistoricalMilitaryCoverage(string path)
        {
            BridgePlanningImporter.Artifact a;string reason;
            if(!BridgePlanningImporter.TryRead(path,out a,out reason))throw new Exception(reason);
            if(!ApprovedPlanning(a.Hash)||a.Planning==null)throw new Exception("unapproved historical military input");
            var b=a.Planning;var entry=CopiedPlanningBundle.Resolve(b,"military/entryPlayer");
            if(entry.Length!=0x583C)throw new Exception("military entry extent");
            using(var m=new NativeImage(Native,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","native-contracts.tsv")))
            {
                m.Put(0x379AE00+b.Attacker*0x583C,entry);
                int actual=m.Function<R2>(0x2FD40)(m.Ptr(0x366C210),b.Attacker);
                Check(actual==MilitaryStrength(entry),"historical strength helper actual entry");
                Console.WriteLine("PASS own military entry strength; attacker="+b.Attacker+",result="+actual+",phase="+BitConverter.ToInt32(entry,0x379D974-0x379AE00)+",hash="+a.Hash);
            }
            // Presence and decision-time provenance are separate: consumer/pre
            // unitManager/tribes cannot authorize reads before that observation.
            var required=new[]{
                Tuple.Create("military/entryLeaderIds9",0x3A0F9F0,18),
                Tuple.Create("military/entryLeaderGlobals9",0x3A0FA04,36),
                Tuple.Create("military/entryUpdateClasses11",0x2C8040,88),
                Tuple.Create("military/entryMoveClasses11",0x2C80E0,88),
                Tuple.Create("military/entryConfig5",0,20),
                Tuple.Create("military/entryIdentity4",0,32),
                Tuple.Create("callerHeight",0x4DDD350,N),
                Tuple.Create("callerBaseHeight",0x4E2B870,N)};
            int missing=0;
            foreach(var item in required)
            {
                bool present=b.Sections.ContainsKey(item.Item1)||b.References.ContainsKey(item.Item1);
                if(present&&CopiedPlanningBundle.Resolve(b,item.Item1).Length!=item.Item3)throw new Exception("military input extent "+item.Item1);
                if(!present){missing++;Console.WriteLine("Unknown:missing-own-input="+item.Item1+",source="+(item.Item2==0?"assembled-observer-provenance-or-scattered-config-fields":"native-rva-"+item.Item2.ToString("X"))+",bytes="+item.Item3);}
            }
            Console.WriteLine("militaryEntryComplete=False,formationComplete=False,missingRequiredInputs="+missing+",consumerCopiesAreNotEntryCopies=True,negativePolicyEligible=False,behaviorFix=disabled");
            Console.WriteLine("scope=necessary-input-coverage-only; AI configuration and side-effect call closure still required; no later snapshot or PE default substituted");
            return 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AreaDemolitionTest;
using SHCDESE.Interop;

internal static unsafe class Program
{
    private static int checks;
    private static void Check(bool value,string message) { if (!value) throw new Exception(message); checks++; }
    private static int Main()
    {
        try
        {
            ScratchRestoration(); PatternBoundaries(); ClassificationAndRefunds();
            Console.WriteLine("PASS: native demolition " + checks + " checks (private-memory scratch, native layout, resolver boundaries, fear families, refund policy).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ScratchRestoration()
    {
        IntPtr allocation=Marshal.AllocHGlobal(NativeBindings.ActorRva+4096);
        try
        {
            byte* p=(byte*)allocation;
            var saved=new Dictionary<int,byte>();
            void Seed(int offset,int count)
            { for (int i=0;i<count;i++) { byte value=(byte)((offset+i)*37+11); p[offset+i]=value; saved[offset+i]=value; } }
            Seed(NativeBindings.CursorRva,0x80); Seed(NativeBindings.NeighboursRva,32); Seed(NativeBindings.ActorRva,4);
            foreach (int offset in NativeBindings.QueryOffsets) Seed(NativeBindings.TileManagerRva+offset,4);
            foreach (TargetKind kind in Enum.GetValues<TargetKind>())
            {
                var target=new DemolitionTarget { Kind=kind,Slot=17,TileId=991,X=100,Y=77,Owner=4 };
                foreach (int mode in new[]{0,1,2})
                {
                    var binding=new NativeBindings(allocation,new object(),(manager,actor,x,y)=>
                    {
                        Check(manager.ToInt64()==allocation.ToInt64()+NativeBindings.TileManagerRva,"Native manager argument");
                        Check(actor==4 && x==100 && y==77,"Native actor/coordinate arguments");
                        byte* cursor=p+NativeBindings.CursorRva, tiles=(byte*)manager;
                        Check(*(int*)(cursor+0x14)==991 && *(int*)(cursor+0x68)==991,"Packed target setup");
                        Check(*(int*)(p+NativeBindings.ActorRva)==4 && *(int*)(tiles+0x204E694)==0,"Scoped actor and ground targeting");
                        Check(*(int*)(cursor+0x24)==(kind==TargetKind.Building?17:0),"Building selector");
                        Check(*(int*)(cursor+0x4C)==(kind==TargetKind.Pitch?17:0),"Pitch selector");
                        Check(*(int*)(cursor+0x5C)==(kind==TargetKind.Wall?991:0),"Wall selector");
                        Check(*(uint*)(cursor+0x44)==(kind==TargetKind.DugMoat?0x40000000U:0),"Moat selector");
                        for (int i=0;i<0x80;i++) cursor[i]=0xAA;
                        for (int i=0;i<32;i++) p[NativeBindings.NeighboursRva+i]=0xBB;
                        foreach (int offset in NativeBindings.QueryOffsets) *(int*)(tiles+offset)=0x1234;
                        *(int*)(p+NativeBindings.ActorRva)=6;
                        if (mode==2) throw new InvalidOperationException("injected native-query failure");
                        *(int*)(tiles+0x204E6FC)=mode;
                        *(int*)(tiles+0x204E720)=kind==TargetKind.Pitch?-17:17;
                        *(int*)(tiles+0x204E76C)=kind==TargetKind.Wall?1:0;
                        *(int*)(tiles+0x204E770)=kind==TargetKind.DugMoat?1:0;
                    });
                    bool threw=false;
                    try { Check(binding.Allows(target,4)==(mode==0),"Native eligibility result"); }
                    catch (InvalidOperationException) { threw=true; }
                    Check(threw==(mode==2),"Only injected exception propagates");
                    foreach (var pair in saved) Check(p[pair.Key]==pair.Value,"Query leaked scratch at "+pair.Key.ToString("X"));
                }
            }
            Check(Marshal.OffsetOf<SHCDESE.Interop.MoatWorkTask>(nameof(SHCDESE.Interop.MoatWorkTask.r_Progress)).ToInt32()==0xD,"Chore41 zeroes progress, not work type");
            var task=(MoatWorkTask*)(p+NativeBindings.TileManagerRva+0x1F3EE30)+5;
            task->r_Progress=4; task->r_WorkType=SHCDESE.Interop.Enums.MoatWorkType.FillDugMoat;
            ushort* display=(ushort*)(p+NativeBindings.TileManagerRva+0xF05F40);
            display[991]=0xFFFF;
            int calls=0;
            var moatBinding=new NativeBindings(allocation,new object(),null,(manager,index)=>
            {
                calls++; Check(index==5,"Moat helper receives the task index, not packed tile");
                Check(task->r_Progress==0,"A complete moat must be zeroed before the single fill helper call");
                Check(task->r_WorkType==SHCDESE.Interop.Enums.MoatWorkType.FillDugMoat,"Work type is preserved");
                return 1;
            });
            moatBinding.RemoveDugMoat(5,991);
            Check(calls==1,"One native fill call for a completed moat");
            Check(display[991]==0xFBFF,"Only the pending-demolition display bit is cleared");
        }
        finally { Marshal.FreeHGlobal(allocation); }
    }

    private static void PatternBoundaries()
    {
        var image=new byte[0x1000];
        void I32(int offset,int value) => BitConverter.GetBytes(value).CopyTo(image,offset);
        void I16(int offset,ushort value) => BitConverter.GetBytes(value).CopyTo(image,offset);
        I32(0x3C,0x80); I16(0x86,2); I16(0x94,0xF0);
        int section=0x188;
        I32(section+8,0x300); I32(section+12,0x400); I32(section+36,0x20000000);
        I32(section+40+8,0x200); I32(section+40+12,0x800); I32(section+40+36,0x40000000);
        byte[] pattern={0x43,0x87,0x21,0x35};
        pattern.CopyTo(image,0x450); pattern.CopyTo(image,0x810);
        Check(NativeBindings.FindUniqueText(image,pattern)==0x450,"Data-section duplicate must not make a code target ambiguous");
        pattern.CopyTo(image,0x500);
        Check(NativeBindings.FindUniqueText(image,pattern)==-1,"Two executable matches are rejected");
        I32(section+8,int.MaxValue);
        Check(NativeBindings.FindUniqueText(image,pattern)==-1,"Out-of-image section rejected");
    }

    private static void ClassificationAndRefunds()
    {
        foreach (var type in new[]{eStructs.STRUCT_GARDEN,eStructs.STRUCT_GARDEN_SMALL,eStructs.STRUCT_GARDEN_MED,eStructs.STRUCT_GARDEN_LARGE,eStructs.STRUCT_POND,eStructs.STRUCT_POND_SMALL,eStructs.STRUCT_POND_LARGE,eStructs.STRUCT_POND5,eStructs.STRUCT_POND6,eStructs.STRUCT_POND7,eStructs.STRUCT_POND8})
            Check(NativeDemolition.Classify(type)==TargetGroup.PositiveFear,"Positive fear size/variant missing: "+type);
        Check(NativeDemolition.Classify(eStructs.STRUCT_RACK_FLOGGING)==TargetGroup.NegativeFear,"Flogging rack omitted by AIV subset must remain negative fear");
        Check(NativeDemolition.Classify(eStructs.STRUCT_HEAD_ON_SPIKE)==TargetGroup.NegativeFear,"Head on spike remains negative fear");
        Check(NativeDemolition.Classify(eStructs.STRUCT_DOG_CAGE)==TargetGroup.DogCages,"Dog cage structural trap precedence");
        Check(NativeDemolition.Classify(eStructs.STRUCT_KILLING_PIT)==TargetGroup.KillingPits,"Killing pit has its independent filter");
        Check(NativeDemolition.WallRefundPercent(0,60)==25,"Vanilla exact boundary lower side");
        Check(NativeDemolition.WallRefundPercent(0,61)==50,"Vanilla exact boundary upper side");
        Check(NativeDemolition.WallRefundPercent(1,60)==50,"Fixes bypasses low-wall halving");
        GameBuilding building=default; building.r_AliveState=SHCDESE.Interop.Enums.AliveState.IsAlive;
        building.N00001786=0x10000;
        Check(NativeDemolition.ShouldRefund(&building),"Refund exclusion reads only the low word");
        building.N00001786=1;
        Check(!NativeDemolition.ShouldRefund(&building),"Low-word exclusion suppresses the parent refund");
        building.N00001786=0; building.r_AliveState=SHCDESE.Interop.Enums.AliveState.MarkedForDeletion;
        Check(!NativeDemolition.ShouldRefund(&building),"Already marked objects are not refunded twice");
    }
}

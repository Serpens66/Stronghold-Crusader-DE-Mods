using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static unsafe class NativeVegetationAccessFixture
    {
        private static int checks;
        private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        private sealed class Source
        {
            internal GameVegetation* Array;
            internal int Count,Calls,LastId;
            internal bool TryGet(int publicId,out GameVegetation* tree)
            {
                Calls++;LastId=publicId;tree=null;if(publicId<1 || publicId>Count)return false;
                tree=Array+publicId-1;return true;
            }
            internal bool WrongZeroBased(int publicId,out GameVegetation* tree)
            {Calls++;LastId=publicId;tree=publicId>=0 && publicId<Count?Array+publicId:null;return tree!=null;}
            internal bool WrongPrevious(int publicId,out GameVegetation* tree)
            {Calls++;LastId=publicId;tree=Array+publicId-2;return true;}
        }
        internal static void Run()
        {
            checks=0;GameVegetation* records=stackalloc GameVegetation[4];
            for(int i=0;i<4;i++){records[i]=default;records[i].r_AliveState=AliveState.IsAlive;records[i].r_GlobalId=(uint)(111+i*111);}
            var source=new Source{Array=records,Count=4};
            Check(NativeVegetationAccess.TryGet(1,records,4,source.TryGet,out var selected) && selected==records+1 && source.LastId==2,
                "native selected index1 maps exactly to SDK id2 and second record");
            Check(NativeVegetationAccess.TryReadAliveGlobal(selected,out uint original) && original==222,"selected lifetime is read, not alive previous record111");
            Check(NativeVegetationAccess.TryReadCapturedAliveGlobal(selected,true,original,out uint matched) && matched==222,
                "the original pre-mutation tree identity admits the same selected lifetime");
            Check(!NativeVegetationAccess.TryReadCapturedAliveGlobal(selected,false,original,out _),"a fresh late read cannot substitute missing pre-mutation vegetation evidence");
            records[1].r_AliveState=default;
            Check(NativeVegetationAccess.TryGet(1,records,4,source.TryGet,out selected) &&
                !NativeVegetationAccess.TryReadAliveGlobal(selected,out _),"dead selected record is not masked by an alive previous record");
            Check(!NativeVegetationAccess.TryReadCapturedAliveGlobal(selected,true,original,out _),"deleted selected tree cannot inherit its original captured identity");
            records[1].r_AliveState=AliveState.IsAlive;records[1].r_GlobalId=999;
            Check(NativeVegetationAccess.TryGet(1,records,4,source.TryGet,out selected) &&
                NativeVegetationAccess.TryReadAliveGlobal(selected,out uint reused) && reused==999 && reused!=original,
                "selected slot reuse changes the actual binding despite unchanged previous record");
            Check(!NativeVegetationAccess.TryReadCapturedAliveGlobal(selected,true,original,out _),
                "same selected native index with a new global lifetime cannot be adopted after the mutation");
            Check(NativeVegetationAccess.TryReadCapturedAliveGlobal(selected,true,999,out _),"a distinct freshly observed job can capture its own new lifetime");
            Check(!NativeVegetationAccess.TryGet(1,records,4,source.WrongPrevious,out selected) && selected==null,
                "wrong previous-record SDK result cannot enter runtime binding");
            Check(!NativeVegetationAccess.TryGet(1,records,4,source.WrongZeroBased,out selected) && selected==null,
                "changed SDK indexing semantics fail the live array-address contract");
            Check(NativeVegetationAccess.TryGet(3,records,4,source.TryGet,out selected) && selected==records+3 && source.LastId==4,
                "last positive native index maps to last public SDK id");
            int calls=source.Calls;
            foreach(int index in new[]{int.MinValue,-1,0,4,int.MaxValue})
                Check(!NativeVegetationAccess.TryGet(index,records,4,source.TryGet,out selected) && selected==null,"native sentinel and out-of-range indices are rejected");
            Check(source.Calls==calls,"invalid native indices never reach the SDK getter");
            Check(!NativeVegetationAccess.TryGet(1,null,4,source.TryGet,out _) &&
                !NativeVegetationAccess.TryGet(1,records,0,source.TryGet,out _) &&
                !NativeVegetationAccess.TryGet(1,records,4,null,out _),"missing array or getter is unavailable");
            Check(!NativeVegetationAccess.TryReadAliveGlobal(null,out _),"null vegetation has no live identity");
            Check(records[0].r_GlobalId==111 && records[0].r_AliveState==AliveState.IsAlive,"control previous record remains alive and unchanged");
            Console.WriteLine("PASS: native vegetation access "+checks+" checks; native-index/public-ID translation, adjacent lifetimes and array-address contract; no game execution.");
        }
    }
}

using System;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // Native108040/135E30 use an array index (zero means no selected tree).
    // The public SDK lookup uses a one-based ID. Keep these domains separate.
    internal static unsafe class NativeVegetationAccess
    {
        internal delegate bool PublicLookup(int publicId,out GameVegetation* tree);
        private static readonly PublicLookup sdkLookup=LookupSdk;
        private static bool LookupSdk(int id,out GameVegetation* tree)=>GameVegetationManagerAPI.Instance.TryGetVegetationById(id,out tree);
        internal static bool TryGet(int nativeIndex,out GameVegetation* tree)
        {
            tree=null;if(nativeIndex<=0)return false;
            try
            {
                var span=GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
                fixed(GameVegetation* array=span)return TryGet(nativeIndex,array,span.Length,sdkLookup,out tree);
            }
            catch{tree=null;return false;}
        }
        // Also used by the controlled pointer fixture. Validate the SDK's result
        // against the actual array, so a changed getter cannot bind a neighbour.
        internal static bool TryGet(int nativeIndex,GameVegetation* array,int count,PublicLookup lookup,out GameVegetation* tree)
        {
            tree=null;if(array==null || lookup==null || nativeIndex<=0 || nativeIndex>=count)return false;
            try
            {
                if(!lookup(nativeIndex+1,out var found) || found==null || found!=array+nativeIndex)return false;
                tree=found;return true;
            }
            catch{return false;}
        }
        internal static bool TryReadAliveGlobal(GameVegetation* tree,out uint global)
        {
            global=0;if(tree==null || tree->r_AliveState!=AliveState.IsAlive)return false;
            global=tree->r_GlobalId;return true;
        }
        internal static bool TryReadCapturedAliveGlobal(GameVegetation* tree,bool hasCaptured,uint capturedGlobal,out uint global)
        {
            global=0;
            if(!hasCaptured || !TryReadAliveGlobal(tree,out uint live) || live!=capturedGlobal)return false;
            global=live;return true;
        }
    }
}

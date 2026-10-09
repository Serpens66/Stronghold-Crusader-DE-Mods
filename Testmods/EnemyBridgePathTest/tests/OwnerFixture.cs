using System;
using System.Collections.Generic;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
namespace BugfixesAndQoL.UnitCommands
{
    // Compiles the actual owner callback into a private fixture; no hook is installed.
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal static bool nativeManualProbe;
        private readonly IntPtr nativePathManager=IntPtr.Zero;
        private delegate void MaskRebuildDelegate(IntPtr manager);
        private readonly List<Delegate> connectivityDelegates=new List<Delegate>();
        private sealed class RedBirdDetour<T> where T:Delegate {internal T Original => null;}
        private RedBirdDetour<T> AddDetour<T>(HookTransaction tx,ulong address,T callback) where T:Delegate => throw new InvalidOperationException("Fixture never installs hooks");
        internal int Calls,ReturnValue;
        internal bool ThrowOriginal;
        internal UnitCommandPathRuntime(){originalProbePclRebuild=(manager,force)=>{Calls++;if(ThrowOriginal)throw new InvalidOperationException("original");return ReturnValue;};}
        internal int Invoke(bool probe){nativeManualProbe=probe;try{return ExecuteProbePclRebuild(IntPtr.Zero,0);}finally{nativeManualProbe=false;}}
    }
}

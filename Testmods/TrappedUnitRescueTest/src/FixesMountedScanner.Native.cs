using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SHCDESE.API.LowLevel;

namespace TrappedUnitRescueTest
{
    internal sealed class MountedScannerNativeObservation
    {
        internal ulong ModuleBase;internal int ImageSize;internal string FileHash;
        internal byte[] Snapshot,Live;internal bool Executable;
    }
    internal interface IMountedScannerNativeReader
    {bool TryRead(CrusaderLibraryLoadContext context,out MountedScannerNativeObservation observation,out string reason);}
    internal static class MountedScannerNativeContract
    {
        internal const int Rva=0x187110,Length=227;
        internal const string ImageHash="FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        // Entire audited187110 function. RIP-relative references/calls are unchanged by image relocation.
        private static readonly byte[] expected=Convert.FromBase64String("SIlcJAhIiWwkEEiJdCQYV0iD7CBIY/oz7bgIAAAAi9dIad+QBAAASIvxSAPZRIuDuAYAAEiJq7wGAABmiav2CAAAZomrtAkAAGaJg1AHAADoi/n//4vXiau4BgAASIvO6Fv5//8Pv5M4BwAATI0NqY3qA0wPv4M6BwAAi8JmiZMcBwAAZkSJgx4HAABmweIDS40MQGaDwgRBAwSJSIvOZkHB4ANmiZMOBwAAZkGDwASL12ZEiYMQBwAAiYMsBwAA6HtMAACL10iLzkiLXCQwSItsJDhIi3QkQEiDxCBf6T2Q//8=");
        internal static byte[] ReferenceBytes()=>(byte[])expected.Clone();
        internal static bool Validate(ulong contextBase,ulong address,MountedScannerNativeObservation data,out string reason)
        {
            reason=null;
            if(IntPtr.Size!=8 || contextBase==0 || contextBase>ulong.MaxValue-Rva || address!=contextBase+Rva)
            {reason="native-relocate-address-or-x64-ABI";return false;}
            if(data==null || data.ModuleBase!=contextBase || data.ImageSize<Rva+Length || data.FileHash!=ImageHash || !data.Executable)
            {reason="native-module-hash-bounds-or-executable-contract";return false;}
            if(!Same(data.Snapshot,expected)){reason="native-load-snapshot-function-contract";return false;}
            if(!Same(data.Live,expected)){reason="native-live-function-foreign-change";return false;}
            return true;
        }
        private static bool Same(byte[] a,byte[] b)
        {if(a==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
    // Read-only cold native-setup boundary, not a native call/hook and not a copied-buffer hook probe.
    internal sealed class MountedScannerNativeReader : IMountedScannerNativeReader
    {
        public bool TryRead(CrusaderLibraryLoadContext context,out MountedScannerNativeObservation observation,out string reason)
        {
            observation=null;reason=null;
            try
            {
                if(context==null || context.ModuleHandle==IntPtr.Zero || context.Memory.Length<MountedScannerNativeContract.Rva+MountedScannerNativeContract.Length)
                {reason="native-context-snapshot-unavailable";return false;}
                ulong moduleBase=unchecked((ulong)context.ModuleHandle.ToInt64());string path=null;int size=0;
                using(var process=Process.GetCurrentProcess())foreach(ProcessModule module in process.Modules)
                    if(module.BaseAddress==context.ModuleHandle && string.Equals(module.ModuleName,"CrusaderDE.dll",StringComparison.OrdinalIgnoreCase))
                    {if(path!=null){reason="native-module-ambiguous";return false;}path=module.FileName;size=module.ModuleMemorySize;}
                if(path==null || size<MountedScannerNativeContract.Rva+MountedScannerNativeContract.Length)
                {reason="native-loaded-module-unavailable";return false;}
                string hash;using(var file=File.OpenRead(path))using(var sha=SHA256.Create())
                    hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-",string.Empty);
                // Refuse before reading native memory on unknown file/context identity.
                if(hash!=MountedScannerNativeContract.ImageHash){reason="native-image-hash-unsupported";return false;}
                var address=new IntPtr(checked(context.ModuleHandle.ToInt64()+MountedScannerNativeContract.Rva));
                if(VirtualQuery(address,out var info,(UIntPtr)Marshal.SizeOf(typeof(MemoryInformation)))==UIntPtr.Zero ||
                    info.State!=0x1000 || info.AllocationBase!=context.ModuleHandle || (info.Protect&0x101)!=0 ||
                    (info.Protect&0xF0)!=0x20 && (info.Protect&0xF0)!=0x40 && (info.Protect&0xF0)!=0x80)
                {reason="native-live-region-not-readable-executable";return false;}
                ulong start=unchecked((ulong)address.ToInt64()),region=unchecked((ulong)info.BaseAddress.ToInt64()),length=info.RegionSize.ToUInt64();
                if(start<region || start-region>length || (ulong)MountedScannerNativeContract.Length>length-(start-region))
                {reason="native-live-region-span";return false;}
                var live=new byte[MountedScannerNativeContract.Length];Marshal.Copy(address,live,0,live.Length);
                observation=new MountedScannerNativeObservation{ModuleBase=moduleBase,ImageSize=size,FileHash=hash,Executable=true,
                    Snapshot=context.Memory.Slice(MountedScannerNativeContract.Rva,MountedScannerNativeContract.Length).ToArray(),Live=live};
                return true;
            }
            catch{reason="native-read-boundary-failed";return false;}
        }
        [StructLayout(LayoutKind.Sequential)]private struct MemoryInformation
        {internal IntPtr BaseAddress,AllocationBase;internal uint AllocationProtect;internal UIntPtr RegionSize;internal uint State,Protect,Type;}
        [DllImport("kernel32.dll",SetLastError=true)]private static extern UIntPtr VirtualQuery(IntPtr address,out MemoryInformation info,UIntPtr length);
    }
}

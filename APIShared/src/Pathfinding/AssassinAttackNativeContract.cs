using System;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
namespace APIShared
{
    internal static class AssassinAttackNativeContract
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void UpdateDelegate();
        internal static readonly NativeDetourBackend Backend=new NativeDetourBackend(
            new NativeDetourOptions { AllowedSchemes=DetourScheme.Indirect, FollowJumps=false });
        internal static void Validate(IDetour<UpdateDelegate> hook,IntPtr target)
        {
            var native=hook as NativeDetour<UpdateDelegate>;
            if(native==null || !native.IsInstalled || native.Scheme!=DetourScheme.Indirect || native.DisplacedByteCount!=10 ||
                native.TargetAddress!=unchecked((ulong)target.ToInt64()) || native.PointerSlot==IntPtr.Zero ||
                Marshal.ReadByte(target)!=0xFF || Marshal.ReadByte(target,1)!=0x25)
                throw new InvalidOperationException("Assassin update NativeX64 Indirect/10 contract mismatch.");
            int delta=Marshal.ReadInt32(target,2);
            if(target+6+delta!=native.PointerSlot || Marshal.ReadIntPtr(native.PointerSlot)!=native.HookEntryPointAddress)
                throw new InvalidOperationException("Assassin update pointer-slot/entry mismatch.");
        }
    }
}

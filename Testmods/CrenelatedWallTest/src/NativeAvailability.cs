using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace CrenelatedWallTest
{
    // Read-only call to Vanilla's own quota getter. No code patches, detours or cost
    // calculations. This includes branches absent from the public goods-count API.
    internal sealed class NativeAvailability
    {
        internal const int ReferenceRva = 0xCF640;
        internal const string ReferenceCode = "RTPJTIvRRDkNe2lZAw+FmgAAAIsFz2hZA4P4AQ+EiwAAAIP4BnUSSGPCSGnQPFgAAIuECjzwEgDDQYP4BHU1SGPCSGnIPFgAAEKLhBGg8BIAA8BCg7wREA0TAABEjUj/RA9EyLj6AAAARDvIRA9PyEGLwcNBg/gCdS9IY8JIacg8WAAARouMEZjwEgBFA8lCg7wRDA0TAAB0A0H/ybj6AAAARDvIRA9PyEGLwcO4ZAAAAMM=";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadQuota(IntPtr playerManager, int playerId, int good);
        private readonly ReadQuota read;
        internal NativeAvailability(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            byte[] expected = Convert.FromBase64String(ReferenceCode);
            if (context.ModuleHandle == IntPtr.Zero || context.Memory.Length < ReferenceRva + expected.Length)
                throw new InvalidOperationException("Native quota module bounds are invalid.");
            IntPtr address = IntPtr.Add(context.ModuleHandle, ReferenceRva);
            string method = "reference-rva";
            if (!MatchesLive(address, expected))
            {
                method = "executable-pattern";
                int found = AvailabilityPattern.FindUniqueExecutable(context.Memory, expected);
                // The reference image's relative globals are tied to this location.
                // A different RVA needs a fresh semantic audit, even with equal bytes.
                if (found != ReferenceRva || !MatchesLive(IntPtr.Add(context.ModuleHandle, found), expected))
                    throw new InvalidOperationException("Vanilla wall availability RVA/pattern/live bytes could not be validated.");
                address = IntPtr.Add(context.ModuleHandle, found);
            }
            read = Marshal.GetDelegateForFunctionPointer<ReadQuota>(address);
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_NATIVE_READ quota=" + method + "; RVA=0x" + ReferenceRva.ToString("X") + "; bytes=" + expected.Length + "; read-only; no hook.");
        }
        internal int Get(int player)
        {
            GamePlayerManagerAPI api = GamePlayerManagerAPI.Instance;
            IntPtr manager = api.GetPlayerManager();
            if (!api.IsPlayerIdValid(player) || manager == IntPtr.Zero) throw new InvalidOperationException("Invalid Vanilla quota context.");
            return read(manager, player, (int)eGoods.STORED_STONE_BLOCKS);
        }
        private static bool MatchesLive(IntPtr address, byte[] expected)
        {
            byte[] bytes = new byte[expected.Length]; Marshal.Copy(address, bytes, 0, bytes.Length);
            for (int i = 0; i < bytes.Length; i++) if (bytes[i] != expected[i]) return false;
            return true;
        }
    }
}

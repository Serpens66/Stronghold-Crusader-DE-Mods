using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace AIDefense
{
    internal sealed unsafe class AIDefenseTribeUnassignAdapter
    {
        internal const int RemoveUnitFromTribeRva = 0x123EA0;
        internal const int RemoveUnitFromTribeSize = 312;

        private static readonly byte[] Signature =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10, 0x48, 0x89,
            0x7C, 0x24, 0x18, 0x41, 0x56, 0x48, 0x83, 0xEC, 0x20, 0x4C, 0x63, 0xCA,
            0x4C, 0x8D, 0x35, 0x41, 0xC1, 0xED, 0xFF, 0x49, 0x63, 0xF8, 0x41, 0x8B,
            0xC1, 0x49, 0x69, 0xF1, 0x90, 0x04, 0x00, 0x00, 0x99, 0x48, 0x8B, 0xD9,
        };

        private static readonly byte[] Tail =
        {
            0xFF, 0x8B, 0xD7, 0x48, 0x8B, 0xCB, 0x48, 0x8B, 0x5C, 0x24, 0x30, 0x48,
            0x8B, 0x74, 0x24, 0x38, 0x48, 0x8B, 0x7C, 0x24, 0x40, 0x48, 0x83, 0xC4,
            0x20, 0x41, 0x5E, 0xE9, 0x48, 0xA8, 0xFF, 0xFF,
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long RemoveUnitFromTribeDelegate(IntPtr tribeManager, int unitId, int tribeId);

        private readonly ManualLogSource log;
        private readonly RemoveUnitFromTribeDelegate removeUnitFromTribe;

        private AIDefenseTribeUnassignAdapter(
            ManualLogSource log,
            RemoveUnitFromTribeDelegate removeUnitFromTribe)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.removeUnitFromTribe = removeUnitFromTribe ?? throw new ArgumentNullException(nameof(removeUnitFromTribe));
        }

        public static AIDefenseTribeUnassignAdapter Create(
            ManualLogSource log,
            CrusaderLibraryLoadContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.ModuleHandle == IntPtr.Zero)
                throw new InvalidOperationException("CrusaderDE module handle is null.");

            ReadOnlySpan<byte> memory = context.Memory;
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                Signature,
                RemoveUnitFromTribeRva,
                referenceHashMatches: true,
                name: "AIDefense remove-unit-from-tribe helper",
                log: log);
            if (resolution.Rva != RemoveUnitFromTribeRva)
                throw new InvalidOperationException($"Remove-unit helper resolved at unexpected RVA 0x{resolution.Rva:X}.");
            if (!MatchesAt(memory, RemoveUnitFromTribeRva + RemoveUnitFromTribeSize - Tail.Length, Tail) ||
                RemoveUnitFromTribeRva + RemoveUnitFromTribeSize >= memory.Length ||
                memory[RemoveUnitFromTribeRva + RemoveUnitFromTribeSize] != 0xCC)
            {
                throw new InvalidOperationException("Remove-unit helper tail or exact function boundary does not match SHCDE 2.0.2.");
            }

            var nativeCall = (RemoveUnitFromTribeDelegate)Marshal.GetDelegateForFunctionPointer(
                context.ModuleHandle + RemoveUnitFromTribeRva,
                typeof(RemoveUnitFromTribeDelegate));
            return new AIDefenseTribeUnassignAdapter(log, nativeCall);
        }

        public bool TryUnassign(int tribeId, int unitId)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            GameTribe* tribe = null;
            GameUnit* unit = null;
            if (!tribeApi.TryGetTribeById(tribeId, out tribe) || tribe == null ||
                !unitApi.TryGetUnitById(unitId, out unit) || unit == null ||
                unit->r_TribeId != tribeId)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"AIDefense tribe unassign rejected: tribeId={tribeId}, unitId={unitId}, " +
                    $"unitTribeId={(unit == null ? -1 : unit->r_TribeId)}.");
                return false;
            }

            IntPtr manager = new IntPtr(tribeApi.GetTribeManager().Pointer);
            if (manager == IntPtr.Zero)
            {
                Shared.DebugLogHelper.LogError(log, "AIDefense tribe unassign rejected because the native tribe manager is null.");
                return false;
            }

            try
            {
                // SHCDESE 2.0.2's public wrapper swaps these two IDs. The native contract is unit first.
                removeUnitFromTribe(manager, unitId, tribeId);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"AIDefense native tribe unassign failed: tribeId={tribeId}, unitId={unitId}, exception={exception}");
                return false;
            }

            if (unit->r_TribeId == tribeId)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"AIDefense native tribe unassign did not change membership: tribeId={tribeId}, unitId={unitId}.");
                return false;
            }

            return true;
        }

        private static bool MatchesAt(ReadOnlySpan<byte> memory, int offset, byte[] expected)
        {
            if (offset < 0 || offset > memory.Length - expected.Length)
                return false;
            for (int index = 0; index < expected.Length; index++)
            {
                if (memory[offset + index] != expected[index])
                    return false;
            }
            return true;
        }
    }
}

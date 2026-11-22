using System;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.NativeSites;
using BepInEx.Logging;
using RedBird.Backends.NativeX64;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using Shared;

namespace BugfixesAndQoL.AreaConstruction
{
    // The three audited call sites use the count only for a stockpile, granary,
    // or armoury placement limit. The native neighbor check remains downstream.
    internal sealed class StorageEmptyLimitPatch
    {
        internal const string Feature = "empty-storage-limits";
        private const int CountRva = 0xB8140;
        private const int BuildingManagerRva = 0x64CCBB0;
        private static readonly byte[] Entry = { 0x4C, 0x63, 0x51, 0x50, 0x45, 0x33, 0xC9 };
        private static readonly byte[] Continuation = { 0x44, 0x8B, 0xDA, 0x49, 0x83, 0xFA, 0x01, 0x7E, 0x41 };
        private static readonly int[] CallSites = { 0x789AF, 0x78A2F, 0x78AAF };
        private static readonly byte[][] CallChecks =
        {
            new byte[] { 0xE8, 0x8C, 0xF7, 0x03, 0x00, 0x83, 0xF8, 0x20, 0x0F, 0x8D, 0xB8, 0x02, 0x00, 0x00 },
            new byte[] { 0xE8, 0x0C, 0xF7, 0x03, 0x00, 0x83, 0xF8, 0x08, 0x0F, 0x8D, 0x38, 0x02, 0x00, 0x00 },
            new byte[] { 0xE8, 0x8C, 0xF6, 0x03, 0x00, 0x83, 0xF8, 0x08, 0x0F, 0x8D, 0xB8, 0x01, 0x00, 0x00 }
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Count(IntPtr manager, int actor, int structure);

        private readonly IntPtr image;
        private readonly IntPtr buildingManager;
        private readonly ManualLogSource log;
        private Count original;
        private Func<bool> integrity;
        private Limits limits;

        private sealed class Limits
        {
            internal Limits(int stockpile, int granaryArmoury)
            {
                StockpileRecords = StorageEmptyLimitPolicy.RecordLimit(stockpile, true);
                GranaryArmouryRecords = StorageEmptyLimitPolicy.RecordLimit(granaryArmoury, false);
            }

            internal int StockpileRecords { get; }
            internal int GranaryArmouryRecords { get; }
        }

        private StorageEmptyLimitPatch(IntPtr image, ManualLogSource log)
        {
            this.image = image;
            this.log = log;
            buildingManager = IntPtr.Add(image, BuildingManagerRva);
        }

        internal void ClearLimits() => Volatile.Write(ref limits, null);

        internal void SetLimits(int stockpile, int granaryArmoury) =>
            Volatile.Write(ref limits, new Limits(stockpile, granaryArmoury));

        internal int RecordLimit(bool stockpile)
        {
            var current = Volatile.Read(ref limits);
            if (current == null)
                return StorageEmptyLimitPolicy.RecordLimit(StorageEmptyLimitPolicy.Vanilla, stockpile);
            return stockpile ? current.StockpileRecords : current.GranaryArmouryRecords;
        }

        // The caller has verified the exact native hash before installation.
        internal static StorageEmptyLimitPatch TryInstall(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            var patch = new StorageEmptyLimitPatch(context.ModuleHandle, log);
            NativeDetour<Count> candidate = null;
            bool claimed = false;
            try
            {
                Shared.NativePatchGuard.Bind(context, log);
                RequireReference(context.Memory, CountRva + Entry.Length, Continuation);
                for (int i = 0; i < CallSites.Length; i++)
                    RequireReference(context.Memory, CallSites[i], CallChecks[i]);
                patch.RequireLiveContract();
                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                NativePatchGuard.RequireUnpatched(module + CountRva, Entry.Length, "empty storage count", Feature);

                var registration = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Feature, new[]
                {
                    new NativeSiteSpan(CountRva, Entry.Length, NativeSiteMode.Exclusive)
                });
                if (!registration.Succeeded) throw new InvalidOperationException(registration.Reason);
                claimed = true;

                Count callback = patch.CountForValidator;
                candidate = Shared.AuditedEntryDetour.Prepare(context.Memory,
                    module, CountRva, Entry, callback, Feature);
                patch.original = candidate.Original;
                Shared.AuditedEntryDetour.Publish(candidate, callback, Feature);
                var entryIntact = NativePatchGuard.CaptureIntegrity(new[] { candidate }, Feature);
                patch.integrity = () =>
                {
                    if (!entryIntact()) return false;
                    if (Marshal.ReadInt64(candidate.PointerSlot) != candidate.HookEntryPointAddress.ToInt64())
                        throw new InvalidOperationException("Published native entry pointer changed at RVA 0xB8140.");
                    return true;
                };
                log.LogInfo(Stamp(Feature + ": reference RVA 0xB8140, Indirect x64, 7 bytes (4+3), continuation 0xB8147."));
                return patch;
            }
            catch (Exception exception)
            {
                if (!AuditedEntryDetour.WasPublished(candidate))
                {
                    candidate?.Dispose();
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Feature);
                }
                log.LogError(Stamp(Feature + ": " + exception.Message));
                return null;
            }
        }

        internal bool CheckIntegrity()
        {
            try
            {
                if (!integrity()) return false;
                RequireLiveContract();
                return true;
            }
            catch (Exception exception)
            {
                log.LogError(Stamp(Feature + ": " + exception.Message));
                return false;
            }
        }

        private int CountForValidator(IntPtr manager, int actor, int structure)
        {
            int count = original(manager, actor, structure);
            var current = Volatile.Read(ref limits);
            if (current == null || manager != buildingManager || (uint)(actor - 1) >= 8)
                return count;

            var type = (eStructs)structure;
            if (type != eStructs.STRUCT_GOODS_YARD && type != eStructs.STRUCT_GRANARY && type != eStructs.STRUCT_ARMOURY)
                return count;

            bool stockpile = type == eStructs.STRUCT_GOODS_YARD;
            int recordLimit = stockpile ? current.StockpileRecords : current.GranaryArmouryRecords;
            int vanillaRecordLimit = StorageEmptyLimitPolicy.RecordLimit(StorageEmptyLimitPolicy.Vanilla, stockpile);
            return StorageEmptyLimitPolicy.CountForVanillaComparison(count, recordLimit, vanillaRecordLimit);
        }

        private static void RequireReference(ReadOnlySpan<byte> snapshot, int rva, byte[] expected)
        {
            if (rva > snapshot.Length - expected.Length ||
                !snapshot.Slice(rva, expected.Length).SequenceEqual(expected))
                throw new InvalidOperationException("Load-image contract differs at RVA 0x" + rva.ToString("X") + ".");
        }

        private void RequireLiveContract()
        {
            RequireLive(CountRva + Entry.Length, Continuation);
            for (int i = 0; i < CallSites.Length; i++)
                RequireLive(CallSites[i], CallChecks[i]);
        }

        private void RequireLive(int rva, byte[] expected)
        {
            var actual = new byte[expected.Length];
            Marshal.Copy(IntPtr.Add(image, rva), actual, 0, actual.Length);
            if (!new ReadOnlySpan<byte>(actual).SequenceEqual(expected))
                throw new InvalidOperationException("Live RVA 0x" + rva.ToString("X") +
                    " expected " + BitConverter.ToString(expected) + ", found " + BitConverter.ToString(actual) + ".");
        }

        private static string Stamp(string message) => $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
    }
}

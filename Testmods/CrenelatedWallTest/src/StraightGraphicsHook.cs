using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace CrenelatedWallTest
{
    // This predicate has exactly two callers, both in Vanilla's tile graphics builder.
    // The callback only reads a published assignment and records redraws of watched tiles;
    // it never chooses logical heights and writes no game data.
    internal sealed class StraightGraphicsHook
    {
        internal const int Rva = 0x5C2D0, BodySize = 237, Displaced = 15;
        internal const string BackendHash = "0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0";
        internal const string BodyHex = "48895C2408488974241048897C24184533DB8BF2488BD9418BC3418BFB458BD34585C9740C4183F902741B4183F904750FBF04000000448D5FFE448D5702EB154183F906750F41BA04000000B802000000418D7A02428D14C50000000003C24863C88B048B03C64898F78483008489000002000074658D043A4863C88B048B03C64898F784830084890000020000744B428D041A4863C88B048B03C64898F7848300848900000200007530428D04124863C88B048B03C64898F7848300848900000200007515B801000000488B5C2408488B742410488B7C2418C3488B5C240833C0488B742410488B7C2418C3";
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int Predicate(IntPtr manager, int tile, int row, int direction);
        // Updated per affected tile by the runtime (cost proportional to the change, never to the
        // number of managed crenels); lock-free reads in the render callback.
        private readonly ConcurrentDictionary<int, EndpointAssignment> endpoints = new ConcurrentDictionary<int, EndpointAssignment>();
        private readonly ManualLogSource log;
        private readonly Predicate callback;
        private NativeDetour<Predicate> hook;
        private bool confirmed;
        internal StraightGraphicsHook(ManualLogSource log) { this.log = log; callback = Invoke; }
        internal static NativeDetourBackend CreateBackend() => new NativeDetourBackend(new NativeDetourOptions {
            AllowedSchemes = DetourScheme.Absolute, FollowCalls = false, FollowJumps = false,
            Chaining = HookChainingPolicy.Refuse, FunctionScanLimit = BodySize });
        internal void Install(CrusaderLibraryLoadContext context)
        {
            using (var stream = File.OpenRead(typeof(NativeDetourBackend).Assembly.Location))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != BackendHash)
                    throw new InvalidOperationException("NativeX64 backend changed; graphics hook requires a new audit.");
            byte[] expected = DecodeBody();
            if (context.ModuleHandle == IntPtr.Zero || context.Memory.Length < Rva + expected.Length)
                throw new InvalidOperationException("Graphics predicate outside native module.");
            IntPtr target = IntPtr.Add(context.ModuleHandle, Rva);
            var live = new byte[expected.Length]; Marshal.Copy(target, live, 0, live.Length);
            for (int i = 0; i < live.Length; i++) if (live[i] != expected[i])
                throw new InvalidOperationException("Graphics predicate already modified or unknown at +" + i);
            var request = new DetourRequest<Predicate> { Name = "CrenelatedWallTest straight endpoints", TargetAddress = unchecked((ulong)target.ToInt64()), Callback = callback };
            hook = (NativeDetour<Predicate>)CreateBackend().CreateDetour(in request);
            // No executable publication yet: a failed prepared candidate may be released.
            try { Validate(hook, unchecked((ulong)target.ToInt64())); }
            catch { hook.Dispose(); hook = null; throw; }
            // The static runtime already roots this owner, callback, original and hook.
            // Once Enable starts there is deliberately no rollback or teardown path.
            hook.Enable(); Validate(hook, unchecked((ulong)target.ToInt64()));
            if (!hook.IsInstalled) throw new InvalidOperationException("Graphics hook publication incomplete.");
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_GRAPHICS_READY RVA=0x5C2D0; Absolute/15; permanent; endpoint assignments and redraw reports only; disabled=original.");
        }
        internal static byte[] DecodeBody()
        {
            var result = new byte[BodyHex.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(BodyHex.Substring(i * 2, 2), 16);
            return result;
        }
        internal static void Validate(NativeDetour<Predicate> value, ulong target)
        {
            if (value == null || value.Scheme != DetourScheme.Absolute || value.DisplacedByteCount != Displaced ||
                value.TargetAddress != target || value.PointerSlot != IntPtr.Zero || value.ChainDepth != 1 ||
                value.HookEntryPointAddress == IntPtr.Zero || value.TrampolineAddress == IntPtr.Zero || value.OriginalEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Graphics NativeX64 Absolute/15 contract mismatch.");
            if (!value.IsInstalled) return;
            var bytes = new byte[Displaced]; Marshal.Copy(new IntPtr(unchecked((long)target)), bytes, 0, bytes.Length);
            if (bytes[0] != 0xFF || bytes[1] != 0x25 || BitConverter.ToInt32(bytes, 2) != 0 ||
                BitConverter.ToInt64(bytes, 6) != value.HookEntryPointAddress.ToInt64() || bytes[14] != 0x90)
                throw new InvalidOperationException("Graphics hook entry patch differs from Absolute/15.");
        }
        // Null removes the assignment of this tile.
        internal void SetEndpoint(int tile, EndpointAssignment endpoint)
        {
            if (endpoint == null) endpoints.TryRemove(tile, out _); else endpoints[tile] = endpoint;
        }
        internal void ClearEndpoints() => endpoints.Clear();
        // Managed tiles whose redraw is reported. Vanilla redraws a crenel when it or a neighbour
        // changed; its only height change (sticky high at 65EF0) follows this predicate. Reporting
        // replaces a periodic scan: the runtime re-checks just these tiles on its next tick.
        private readonly ConcurrentDictionary<int, byte> watched = new ConcurrentDictionary<int, byte>();
        private readonly object reportLock = new object();
        private HashSet<int> reported = new HashSet<int>(), draining = new HashSet<int>();
        private int hasReports; // 1 while reported may be non-empty; lets an idle tick skip the lock
        internal void Watch(int tile, bool on)
        {
            if (on) watched[tile] = 0; else watched.TryRemove(tile, out _);
        }
        internal void ClearWatch() => watched.Clear();
        // Swaps the reported tiles into the caller's list (cleared first); an idle call is one
        // volatile read, and no call allocates.
        internal void TakeReports(List<int> into)
        {
            into.Clear();
            if (Volatile.Read(ref hasReports) == 0) return;
            HashSet<int> taken;
            lock (reportLock)
            {
                hasReports = 0;
                if (reported.Count == 0) return;
                taken = reported; reported = draining; draining = taken;
            }
            into.AddRange(taken); taken.Clear();
        }
        private int Invoke(IntPtr manager, int tile, int row, int direction)
        {
            int original = hook.Original(manager, tile, row, direction);
            // Only records the id; validation of the live tile happens on the next tick.
            if (watched.ContainsKey(tile)) lock (reportLock) { reported.Add(tile); hasReports = 1; }
            if (original != 0) return original;
            try
            {
                if (!endpoints.TryGetValue(tile, out EndpointAssignment endpoint)) return original;
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                if (manager != tiles.GetTileManager()) return original;
                if (!EndpointGraphicsPolicy.Matches(endpoint, tile, row, direction, tiles.GetLogicLayer(), tiles.GetPackedNeighborTileDeltas(),
                    tiles.GetDamageLayer(), tiles.GetStructureLayer(), tiles.GetWallOwnerLayer())) return original;
                if (!confirmed) { confirmed = true; Shared.DebugLogHelper.LogInfo(log, "CRENEL_GRAPHICS_CONFIRMED tile=" + tile + "; row=" + row + "; axis=" + direction + "; Vanilla low sprite selected; no logic writes from rendering."); }
                return 1;
            }
            catch (Exception ex) { Shared.DebugLogHelper.LogErrorLimited(log, "crenel-graphics", "CRENEL_GRAPHICS_ERROR", ex); return original; }
        }
    }
}

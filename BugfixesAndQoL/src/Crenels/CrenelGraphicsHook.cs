using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.NativeSites;
using BepInEx.Logging;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using Shared;

namespace BugfixesAndQoL.Crenels
{
    // A low managed crenel the hook draws as a straight piece. Only owner and row are stored: the answer
    // depends solely on synchronized tile state, never on a neighbourhood remembered on this peer, because
    // camera rotation and the flatten toggle re-run the renderer over all tiles on one peer only.
    internal sealed class StraightAssignment
    {
        internal readonly int Owner, Row;
        internal StraightAssignment(int owner, int row) { Owner = owner; Row = row; }
    }

    // Permanent detour on the straight-crenel predicate 0x5C2D0 (int(tileManager*, tile, row, direction)).
    // Its only callers are 0x65DD6 (direction 0) and 0x65E4F (direction 2) in the tile graphics builder
    // 0x65830. A 0 answer selects a special sprite and ORs the logic high bit 0x400000 into the tile
    // (0x65EF0), without a path refresh. The callback keeps every native positive answer, answers 1 only
    // for a published low managed crenel and records redraws of managed tiles. It never writes game data;
    // with no assignment or watch it is Vanilla's predicate.
    internal sealed class CrenelGraphicsHook
    {
        internal const string Feature = "crenel-checkerboard";
        private const int Rva = 0x5C2D0;
        // mov [rsp+8],rbx; mov [rsp+10h],rsi: the Indirect patch (6 bytes) rounded to whole instructions.
        private static readonly byte[] Entry = { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74, 0x24, 0x10 };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int Predicate(IntPtr manager, int tile, int row, int direction);

        private readonly ManualLogSource log;
        // Updated per normalized tile by the runtime; lock-free reads in the render callback.
        private readonly ConcurrentDictionary<int, StraightAssignment> assignments = new ConcurrentDictionary<int, StraightAssignment>();
        // Managed tiles whose redraws are reported to the runtime's next tick.
        private readonly ConcurrentDictionary<int, byte> watched = new ConcurrentDictionary<int, byte>();
        private readonly object reportLock = new object();
        private HashSet<int> reported = new HashSet<int>(), draining = new HashSet<int>();
        private int hasReports; // 1 while reported may be non-empty; lets an idle tick skip the lock
        private Predicate original;
        private Func<bool> integrity;

        internal CrenelGraphicsHook(ManualLogSource log) { this.log = log; }

        internal bool Intact => integrity != null && integrity();

        // Caller: BugfixesAndQoL library-load window, after the exact native hash was verified.
        internal void Install(CrusaderLibraryLoadContext context)
        {
            if (integrity != null) return;
            ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
            Predicate callback = Invoke;
            NativeDetour<Predicate> candidate = null;
            bool claimed = false;
            try
            {
                NativePatchGuard.Bind(context, log);
                var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Feature,
                    new[] { new NativeSiteSpan(Rva, Entry.Length, NativeSiteMode.Exclusive) });
                if (!claim.Succeeded) throw new InvalidOperationException(claim.Reason);
                claimed = true;
                candidate = AuditedEntryDetour.Prepare(context.Memory, module, Rva, Entry, callback, Feature);
                original = candidate.Original;
                AuditedEntryDetour.Publish(candidate, callback, Feature);
                Func<bool> entryIntact = NativePatchGuard.CaptureIntegrity(new[] { candidate }, Feature);
                NativeDetour<Predicate> published = candidate;
                integrity = () => entryIntact() &&
                    Marshal.ReadInt64(published.PointerSlot) == published.HookEntryPointAddress.ToInt64();
            }
            catch
            {
                // Only an unpublished candidate may be released; a published detour stays rooted.
                if (!AuditedEntryDetour.WasPublished(candidate))
                {
                    candidate?.Dispose();
                    if (claimed) NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Feature);
                }
                throw;
            }
        }

        // Null removes the assignment of this tile.
        internal void SetAssignment(int tile, StraightAssignment assignment)
        {
            if (assignment == null) assignments.TryRemove(tile, out _); else assignments[tile] = assignment;
        }

        internal void Watch(int tile, bool on)
        {
            if (on) watched[tile] = 0; else watched.TryRemove(tile, out _);
        }

        // Also drops pending reports: a report from an earlier session must not trigger a local
        // normalization in the next one (only this peer would have it).
        internal void Clear()
        {
            assignments.Clear();
            watched.Clear();
            lock (reportLock) { reported.Clear(); hasReports = 0; }
        }

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
            int answer = original(manager, tile, row, direction);
            // Only records the id; the runtime validates the live tile on its next tick.
            if (watched.ContainsKey(tile)) lock (reportLock) { reported.Add(tile); hasReports = 1; }
            if (answer != 0 || !assignments.TryGetValue(tile, out StraightAssignment assignment)) return answer;
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                return manager == tiles.GetTileManager() && Straight(assignment, tile, row, direction, tiles.GetLogicLayer(),
                    tiles.GetPackedNeighborTileDeltas(), tiles.GetDamageLayer(), tiles.GetStructureLayer(), tiles.GetWallOwnerLayer()) ? 1 : answer;
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/Crenels/graphics", "Crenel graphics callback failed; Vanilla drawing used: " + ex);
                return answer;
            }
        }

        // Validates a published assignment against the live tile; never chooses a height.
        internal static bool Straight(StraightAssignment assignment, int tile, int row, int direction,
            ReadOnlySpan<int> logic, ReadOnlySpan<int> deltas, ReadOnlySpan<byte> damage,
            ReadOnlySpan<ushort> structure, ReadOnlySpan<byte> owner)
        {
            if (assignment.Row != row || row < 0 || row >= 800 ||
                (direction != 0 && direction != 2 && direction != 4 && direction != 6) ||
                (uint)tile >= (uint)logic.Length || row * 8 + 7 >= deltas.Length || (uint)tile >= (uint)damage.Length ||
                (uint)tile >= (uint)structure.Length || (uint)tile >= (uint)owner.Length ||
                (logic[tile] & 0x400300) != 0x300 || damage[tile] != 0 || structure[tile] != 0 ||
                (owner[tile] & 7) + 1 != assignment.Owner) return false;
            // Native directions 0/2/4/6 are the axis neighbours; 0/4 and 2/6 are the two axes.
            int along = 0, across = 0;
            for (int d = 0; d < 8; d += 2)
            {
                long id = (long)tile + deltas[row * 8 + d];
                if (id < 0 || id >= logic.Length || (logic[(int)id] & 0x200) == 0) continue;
                if (((d - direction) & 3) == 0) along++; else across++;
            }
            // Straight sprite along the axis with more live crenel neighbours; a tie (diagonal or isolated
            // piece, corner, cross, block inside) takes the axis of direction 0, which the renderer asks first.
            return along > across || (along == across && (direction & 2) == 0);
        }
    }
}

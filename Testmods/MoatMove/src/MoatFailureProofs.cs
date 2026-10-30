using System;
using System.Collections.Generic;
using APIShared;

namespace MoatMove
{
    // Exact reuse of Vanilla's failed moat-mode builds (Vanilla audit 30.10.2026, FBCB9319,
    // GROUND_ROUTE_BUILDERS.md "Moat mode"). With +0x84 set and +0x80, +0x88 clear, F4930 skips its region
    // gate and straight attempt, writes +0x7C = 1, +0x155F68 = 0, +0xAC += 1, +0x7C = 0, calls 79C0
    // (0x34A9F50), writes +0x155F68 = 0, runs DAAC0 with 2*(d+4)^2 dequeues (d = 0x34A9F5C) and, if that
    // fails, DAFD0 with 400,000. DAFD0 is a FIFO breadth-first search whose ring of 320,800 entries never
    // reaches that budget, so a failure exhausts the start's component and leaves it stamped
    // (0x52C2550 == manager+4). It has no player input; an edge from v to its neighbour n reads v's direction
    // byte (0x51890D0) and, for v, n and v's two side tiles: flags 0x48F71B0, heights 0x4DDD350, the
    // building 0x4B6AA50 with its type word (0x64CCCDE + b*0x32C, also the height offset's only input) and
    // the special-structure state of 0x107160 (0x4ACE010[t] -> 0x32DE440 + idx*0x9C + 0x6A).
    // A proof keeps the stamped set R and a hash of those inputs over every tile of R that has a neighbour
    // outside R plus all their neighbours. While the hash matches, no edge leaves R, so DAFD0 fails from
    // every start in R to every target outside R. Such a request replays F4930 up to DAAC0 natively and skips
    // DAFD0. DAAC0's edges are not a subset of DAFD0's (direction bits without the height check): if it finds
    // a route, +0xAC is restored and Vanilla's builder runs as usual. APIShared's gate filter changes
    // direction bits per player inside BugfixesAndQoL's builder scope (around this whole call, also the replayed
    // DAAC0), so proofs are kept per player and gate policy, like GroundFailureProofs.
    // The bursts this answers: 109 of 128 moat-mode builds failed after the stress Spawn, 281 of 282 ms
    // (pf-iter-66).
    internal sealed unsafe class MoatFailureProofs
    {
        internal delegate void DistanceDelegate(IntPtr rectangle, int startX, int startY, int targetX, int targetY);
        internal delegate int BreadthSearchDelegate(IntPtr manager, int startX, int startY, int targetX, int targetY, int budget, int mode);
        internal const int TileCount = 320800, MapWidth = 800, MaximumProofs = 4, MaximumWatched = 200000;
        internal const int GenerationOffset = 0x04, StartXOffset = 0x08, StartYOffset = 0x0C, TargetXOffset = 0x10, TargetYOffset = 0x14,
            SearchFlagOffset = 0x7C, DirectModeOffset = 0x80, MoatModeOffset = 0x84, AssassinModeOffset = 0x88, FailureOffset = 0xAC,
            RouteLengthOffset = 0x155F68, DistanceOffset = 0x0C;
#if SERP_DEVDIAG
        internal const int CaptureAfterFailures = 1;
#else
        // A capture scans all stamps (about a DAFD0 run): only after repeated failures of a player in one tick.
        internal const int CaptureAfterFailures = 2;
#endif

        internal sealed class Proof
        {
            internal readonly ulong[] Reached = new ulong[(TileCount + 63) / 64];
            internal int[] Watched = new int[0];
            internal int WatchedCount, Player;
            internal ulong Hash;
            internal long Used;
            internal IEnemyGateRoutePolicySnapshot Policy;
            internal object Provider;
            internal bool Contains(int tile) => (uint)tile < TileCount && (Reached[tile >> 6] & (1UL << (tile & 63))) != 0;
        }

        private readonly Proof[] proofs = new Proof[MaximumProofs];
        private int proofCount;
        private readonly short* stamps;
        private readonly byte* valid, directions, heights, specialContext, typeWords;
        private readonly uint* flags;
        private readonly ushort* buildings, rows;
        private readonly short* specialTiles;
        private readonly int* rowStarts, neighborOffsets;
        private readonly IntPtr rectangle;
        private readonly DistanceDelegate distance;
        private readonly BreadthSearchDelegate firstSearch;
        private readonly Dictionary<int, int> failures = new Dictionary<int, int>();
        private int failureTick = int.MinValue;
        private long useClock;
        private readonly byte[] marks = new byte[TileCount];
        internal long Captures, CaptureRejects, Hits, Fallbacks, Stale, Verified, Mismatches, SelfChecks, SelfCheckFailures;
        internal string MismatchSample;
        // DEVDIAG only (null otherwise): DAFD0 itself, for the self-check after a capture.
        internal BreadthSearchDelegate VerifySearch;

        internal MoatFailureProofs(short* stamps, byte* valid, byte* directions, uint* flags, byte* heights, ushort* buildings, byte* typeWords,
            short* specialTiles, byte* specialContext, ushort* rows, int* rowStarts, int* neighborOffsets, IntPtr rectangle,
            DistanceDelegate distance, BreadthSearchDelegate firstSearch)
        {
            this.stamps = stamps; this.valid = valid; this.directions = directions; this.flags = flags; this.heights = heights;
            this.buildings = buildings; this.typeWords = typeWords; this.specialTiles = specialTiles; this.specialContext = specialContext;
            this.rows = rows; this.rowStarts = rowStarts; this.neighborOffsets = neighborOffsets; this.rectangle = rectangle;
            this.distance = distance; this.firstSearch = firstSearch;
            for (int i = 0; i < proofs.Length; i++) proofs[i] = new Proof();
        }

        internal int ProofCount => proofCount;
        internal void ResetCounters() { Captures = CaptureRejects = Hits = Fallbacks = Stale = Verified = Mismatches = SelfChecks = SelfCheckFailures = 0; MismatchSample = null; }
        // A new map: the old map's stamps and tiles no longer apply.
        internal void Reset() { proofCount = 0; failures.Clear(); }

        // F4930's moat-mode path with both endpoints on the map and valid for DAFD0 (otherwise DAFD0 returns
        // at once and nothing is worth answering).
        private bool TryReadRequest(byte* manager, out int sx, out int sy, out int tx, out int ty, out int start, out int target)
        {
            sx = *(int*)(manager + StartXOffset); sy = *(int*)(manager + StartYOffset);
            tx = *(int*)(manager + TargetXOffset); ty = *(int*)(manager + TargetYOffset);
            start = target = -1;
            if (*(int*)(manager + MoatModeOffset) == 0 || *(int*)(manager + DirectModeOffset) != 0 || *(int*)(manager + AssassinModeOffset) != 0 ||
                (uint)sx >= MapWidth || (uint)sy >= MapWidth || (uint)tx >= MapWidth || (uint)ty >= MapWidth ||
                valid[sy * MapWidth + sx] == 0 || valid[ty * MapWidth + tx] == 0) return false;
            start = rowStarts[sy * 3] + sx; target = rowStarts[ty * 3] + tx;
            return (uint)start < TileCount && (uint)target < TileCount;
        }

        private static bool PolicyMatches(Proof proof, int player)
        {
            if (proof.Policy == null) return true;
            return ReferenceEquals(EnemyGatePathPolicyBridge.Current, proof.Provider) && proof.Policy.IsCurrent &&
                proof.Provider is IEnemyGateRoutePolicyProvider provider && provider.TryCaptureRoutePolicy(player, out var policy) &&
                ReferenceEquals(policy, proof.Policy);
        }

        // The edge inputs of one tile (see the class comment), mixed into an FNV-1a hash.
        private ulong Mix(ulong hash, int tile)
        {
            ulong value = directions[tile] | (ulong)heights[tile] << 8 | (ulong)buildings[tile] << 16 | (ulong)flags[tile] << 32;
            hash = (hash ^ value) * 1099511628211UL;
            ushort building = buildings[tile];
            ulong type = building == 0 ? 0UL : *(ushort*)(typeWords + (short)building * 0x32C);
            short special = specialTiles[tile];
            ulong state = special == 0 ? 0UL : *(ushort*)(specialContext + special * 0x9C + 0x6A);
            return (hash ^ (type | (ulong)(ushort)special << 16 | state << 32)) * 1099511628211UL;
        }

        private ulong HashWatched(Proof proof)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < proof.WatchedCount; i++) hash = Mix(hash, proof.Watched[i]);
            return hash;
        }

        // DAFD0's neighbours of tile v in row y: offsets [y*8 + d] (d = 0..7); its diagonal side tests also read
        // [y*8 - 1] for d = 0, so the watched neighbourhood uses k = -1..7.
        private bool Neighbor(int tile, int k, out int next)
        {
            int y = rows[tile];
            next = tile + neighborOffsets[y * 8 + k];
            return (uint)next < TileCount;
        }

        // True: the builder call is answered (result is F4930's return value). verify (DEVDIAG): a covered start
        // is only predicted; Vanilla runs and Observe compares.
        internal bool TryAnswer(byte* manager, int player, bool verify, out int result, out bool predicted, out int generationBefore)
        {
            result = 0; predicted = false; generationBefore = 0;
            if (proofCount == 0 || !TryReadRequest(manager, out int sx, out int sy, out int tx, out int ty, out int start, out int target)) return false;
            Proof proof = null;
            for (int i = 0; i < proofCount && proof == null; i++)
                if (proofs[i].Player == player && proofs[i].Contains(start) && !proofs[i].Contains(target)) proof = proofs[i];
            if (proof == null || !PolicyMatches(proof, player)) return false;
            if (HashWatched(proof) != proof.Hash) { Stale++; Drop(proof); return false; }
            proof.Used = ++useClock;
            if (verify) { predicted = true; generationBefore = *(int*)(manager + GenerationOffset); return false; }
            int failuresBefore = *(int*)(manager + FailureOffset);
            *(int*)(manager + SearchFlagOffset) = 1; *(int*)(manager + RouteLengthOffset) = 0;
            *(int*)(manager + FailureOffset) = failuresBefore + 1; *(int*)(manager + SearchFlagOffset) = 0;
            distance(rectangle, sx, sy, tx, ty);
            int d = *(int*)((byte*)rectangle + DistanceOffset) + 4;
            *(int*)(manager + RouteLengthOffset) = 0;
            int budget = unchecked(d * d * 2);
            if (firstSearch((IntPtr)manager, sx, sy, tx, ty, budget, 0) != 0)
            {
                // DAAC0 found a route DAFD0 might not: Vanilla's builder repeats it and finishes the route.
                *(int*)(manager + FailureOffset) = failuresBefore;
                Fallbacks++;
                return false;
            }
            Hits++;
            return true;
        }

        private void Drop(Proof proof)
        {
            int index = Array.IndexOf(proofs, proof, 0, proofCount);
            if (index < 0) return;
            proofCount--;
            proofs[index] = proofs[proofCount]; proofs[proofCount] = proof;
        }

        // After a real F4930 call in moat mode. predicted (DEVDIAG): Vanilla must have failed, or succeeded
        // through DAAC0 alone (one generation step instead of two).
        internal void Observe(byte* manager, int player, int result, bool predicted, int generationBefore, int tick, IEnemyGatePathPolicy gate)
        {
            if (predicted)
            {
                int steps = *(int*)(manager + GenerationOffset) - generationBefore;
                if (result <= 0 || steps == 1) Verified++;
                else
                {
                    Mismatches++;
                    MismatchSample ??= $"player={player} start={*(int*)(manager + StartXOffset)},{*(int*)(manager + StartYOffset)} " +
                        $"target={*(int*)(manager + TargetXOffset)},{*(int*)(manager + TargetYOffset)} result={result} generationSteps={steps}";
                }
            }
            if (result > 0 || !TryReadRequest(manager, out _, out _, out _, out _, out int start, out int target)) return;
            short generation = (short)*(int*)(manager + GenerationOffset);
            // DAFD0 ran last and stamped the start, not the target.
            if (stamps[start] != generation || stamps[target] == generation) return;
            if (tick != failureTick) { failureTick = tick; failures.Clear(); }
            int count = failures.TryGetValue(player, out int seen) ? seen + 1 : 1;
            failures[player] = count;
            if (count < CaptureAfterFailures) return;
            for (int i = 0; i < proofCount; i++)
                if (proofs[i].Player == player && proofs[i].Contains(start)) return;
            IEnemyGateRoutePolicySnapshot policy = null;
            if (gate != null && gate.HasPublishedMask && gate is IEnemyGateRoutePolicyProvider provider &&
                provider.TryCaptureRoutePolicy(player, out var captured) && captured != null && captured.PlayerId == player && captured.IsCurrent)
                policy = captured;
            Proof proof = proofCount < MaximumProofs ? proofs[proofCount] : Oldest();
            if (!Capture(proof, generation)) { CaptureRejects++; return; }
            proof.Player = player; proof.Policy = policy; proof.Provider = policy == null ? null : gate; proof.Used = ++useClock;
            if (proofCount < MaximumProofs && ReferenceEquals(proof, proofs[proofCount])) proofCount++;
            Captures++;
            if (VerifySearch != null) SelfCheck(manager, proof, start, target);
        }

        // DEVDIAG: DAFD0 from up to 8 starts spread over R to the failed target must fail, and to a tile of R
        // must succeed. Runs right after the failed build, whose callers read no search state afterwards
        // (GROUND_ROUTE_BUILDERS.md), still inside BugfixesAndQoL's builder scope, so the gate filter applies.
        private void SelfCheck(byte* manager, Proof proof, int start, int target)
        {
            int ty = rows[target], tx = target - rowStarts[ty * 3];
            int found = 0, step = 0, inside = -1;
            for (int tile = 0; tile < TileCount && found < 8; tile++)
            {
                if (!proof.Contains(tile) || (step++ % 997) != 0) continue;
                int y = rows[tile], x = tile - rowStarts[y * 3];
                if ((uint)x >= MapWidth || valid[y * MapWidth + x] == 0) continue;
                if (tile != start) inside = tile;
                found++; SelfChecks++;
                if (VerifySearch((IntPtr)manager, x, y, tx, ty, 400000, 0) != 0)
                {
                    SelfCheckFailures++;
                    MismatchSample ??= $"selfCheck start={x},{y} target={tx},{ty} watched={proof.WatchedCount}";
                }
            }
            if (inside < 0) return;
            int sy = rows[start], sx = start - rowStarts[sy * 3], iy = rows[inside], ix = inside - rowStarts[iy * 3];
            SelfChecks++;
            if (VerifySearch((IntPtr)manager, sx, sy, ix, iy, 400000, 0) == 0)
            {
                SelfCheckFailures++;
                MismatchSample ??= $"selfCheck control start={sx},{sy} inside={ix},{iy}";
            }
        }

        private Proof Oldest()
        {
            Proof oldest = proofs[0];
            for (int i = 1; i < proofCount; i++) if (proofs[i].Used < oldest.Used) oldest = proofs[i];
            return oldest;
        }

        // R from the stamps; watched tiles: every tile of R with a neighbour outside R, and all their neighbours.
        private bool Capture(Proof proof, short generation)
        {
            Array.Clear(proof.Reached, 0, proof.Reached.Length);
            for (int tile = 0; tile < TileCount; tile++)
                if (stamps[tile] == generation) proof.Reached[tile >> 6] |= 1UL << (tile & 63);
            Array.Clear(marks, 0, marks.Length);
            int count = 0;
            for (int tile = 0; tile < TileCount; tile++)
            {
                if (!proof.Contains(tile)) continue;
                bool border = false;
                for (int k = -1; k < 8 && !border; k++)
                {
                    if (!Neighbor(tile, k, out int next)) return false;
                    if (!proof.Contains(next)) border = true;
                }
                if (!border) continue;
                if (!Watch(proof, tile, ref count)) return false;
                for (int k = -1; k < 8; k++)
                    if (!Neighbor(tile, k, out int next) || !Watch(proof, next, ref count)) return false;
            }
            proof.WatchedCount = count;
            proof.Hash = HashWatched(proof);
            return true;
        }

        private bool Watch(Proof proof, int tile, ref int count)
        {
            if (marks[tile] != 0) return true;
            if (count >= MaximumWatched) return false;
            marks[tile] = 1;
            if (count == proof.Watched.Length) Array.Resize(ref proof.Watched, Math.Max(1024, count * 2));
            proof.Watched[count++] = tile;
            return true;
        }
    }
}

extern alias BugfixesRuntime;
using System;
using System.Collections.Generic;
using APIShared;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;

namespace MoatMove
{
    // Exact reuse of Vanilla's failed ground builds inside one group move (Vanilla audit 30.10.2026,
    // FBCB9319, PathfindingRoadmap.md). In ground mode F4930 runs a straight attempt (F32B0 for profile 0,
    // F3060 otherwise; both step only over set direction bits), then DAAC0
    // (skipped with +0x80) and DA590, breadth-first searches over the direction bits of 0x51890D0
    // (DAAC0 a subset). A
    // closed set of tiles (CursorGroundRejectProof, BugfixesAndQoL's cursor proof, checked against the
    // current bytes and the gate policy) proves both searches fail from every start inside it to every
    // target outside it, so it serves the members' different slots alike. For such a request F4930 is replayed without DAAC0 and DA590: their search state
    // (generation, stamps, distances, queue) is read only by the caller that started a search, and
    // 79C0's 0x34A9F50 only after its own write. The straight attempt still runs natively, since it
    // writes the unit's route buffer. An AI move to a target that just became unreachable cost ~0.85 ms per member
    // (pf-iter-44/45: 85-99 failing members, 72-89 ms in one tick).
    // Proofs live for one group move and drop on any builder success or tile report.
    internal sealed unsafe class GroundFailureProofs
    {
        internal delegate int DirectAttemptDelegate(IntPtr manager, int startX, int startY, int targetX, int targetY);
        internal delegate int RegionsConnectedDelegate(IntPtr manager, int player, int startRegion, int targetRegion);
        // F4930's fields before a verified call (DEVDIAG): Vanilla must end in the failure path or the region gate.
        internal struct BuildState { internal int Failures, Flag, Length; }
        // Path manager fields written by F4930 (0xF4A35, 0xF4A2B, 0xF4A95) and its inputs.
        internal const int StartXOffset = 0x08, StartYOffset = 0x0C, TargetXOffset = 0x10, TargetYOffset = 0x14,
            SearchFlagOffset = 0x7C, DirectModeOffset = 0x80, MoatModeOffset = 0x84, AssassinModeOffset = 0x88,
            DirectSuccessOffset = 0xA8, DirectFailureOffset = 0xAC, RouteLengthOffset = 0x155F68, GenerationOffset = 4;
        // Capture costs two scans of all tiles: only after a player's builds failed this often in the move.
        // DEVDIAG builds capture at the first failure so that every further failure is verified.
        internal const int MaximumProofs = 4, MapWidth = 800;
#if SERP_DEVDIAG
        internal const int CaptureAfterFailures = 1;
#else
        internal const int CaptureAfterFailures = 3;
#endif
        private readonly CursorGroundRejectProof[] proofs = new CursorGroundRejectProof[MaximumProofs];
        private int proofCount;
        private readonly Dictionary<int, int> failures = new Dictionary<int, int>();
        private readonly short* stamps;
        private readonly byte* connections, directionBits;
        private readonly int* neighborOffsets;
        private readonly ushort* rows, regions;
        private readonly Func<int, int, int> tileId;
        // F4930's straight attempts: F32B0 (profile 0, "Large") and F3060 (any other profile).
        private readonly DirectAttemptDelegate directLarge, directDefault;
        // F4930's region gate (0xF49C8): E2F60, called once like Vanilla (it counts at manager+0xC8).
        private readonly RegionsConnectedDelegate regionsConnected;
        internal long Captures, CaptureRejects, Hits, GateHits, DirectSuccesses, Verified, Mismatches;
        internal string MismatchSample;

        internal GroundFailureProofs(short* stamps, byte* connections, byte* directionBits, int* neighborOffsets, ushort* rows,
            ushort* regions, Func<int, int, int> tileId, DirectAttemptDelegate directLarge, DirectAttemptDelegate directDefault,
            RegionsConnectedDelegate regionsConnected)
        {
            this.stamps = stamps; this.connections = connections; this.directionBits = directionBits;
            this.neighborOffsets = neighborOffsets; this.rows = rows; this.regions = regions; this.tileId = tileId;
            this.directLarge = directLarge; this.directDefault = directDefault; this.regionsConnected = regionsConnected;
            for (int i = 0; i < proofs.Length; i++) proofs[i] = new CursorGroundRejectProof();
        }

        internal int ProofCount => proofCount;
        internal void ResetCounters() { Captures = CaptureRejects = Hits = GateHits = DirectSuccesses = Verified = Mismatches = 0; MismatchSample = null; }

        internal void Reset()
        {
            if (proofCount == 0 && failures.Count == 0) return;
            for (int i = 0; i < proofCount; i++) proofs[i].Reset();
            proofCount = 0; failures.Clear();
        }

        // F4930's plain ground path: no moat or Assassin mode, both endpoints on the map. +0x80 (set by
        // 196280/11B520) only skips DAAC0 before DA590; the writes of the failure path are the same.
        private bool TryReadRequest(byte* manager, int profile, out int sx, out int sy, out int tx, out int ty, out int start, out int target)
        {
            sx = *(int*)(manager + StartXOffset); sy = *(int*)(manager + StartYOffset);
            tx = *(int*)(manager + TargetXOffset); ty = *(int*)(manager + TargetYOffset);
            start = target = -1;
            if (*(int*)(manager + MoatModeOffset) != 0 || *(int*)(manager + AssassinModeOffset) != 0 ||
                (uint)sx >= MapWidth || (uint)sy >= MapWidth || (uint)tx >= MapWidth || (uint)ty >= MapWidth) return false;
            start = tileId(sx, sy); target = tileId(tx, ty);
            return (uint)start < CursorGroundRejectProof.TileCount && (uint)target < CursorGroundRejectProof.TileCount;
        }


        // The same gate policy as at capture (BugfixesAndQoL's cursor rule); unfiltered proofs hold under any policy.
        private static bool PolicyMatches(CursorGroundRejectProof proof, int player)
        {
            if (proof.Policy == null) return true;
            return ReferenceEquals(EnemyGatePathPolicyBridge.Current, proof.Provider) && proof.Policy.IsCurrent &&
                proof.Provider is IEnemyGateRoutePolicyProvider provider && provider.TryCaptureRoutePolicy(player, out var policy) &&
                ReferenceEquals(policy, proof.Policy);
        }

        // True: the builder call is answered (result is F4930's return value). verify (DEVDIAG): a covered
        // start is only predicted; Vanilla runs and Observe compares its terminal state.
        internal bool TryAnswer(byte* manager, int player, int profile, bool verify, out int result, out bool predicted, out BuildState before)
        {
            result = 0; predicted = false; before = default;
            if (proofCount == 0 || !TryReadRequest(manager, profile, out int sx, out int sy, out int tx, out int ty, out int start, out int target)) return false;
            CursorGroundRejectProof proof = null;
            for (int i = 0; i < proofCount && proof == null; i++)
                if (proofs[i].Player == player && proofs[i].Contains(start) && !proofs[i].Contains(target)) proof = proofs[i];
            if (proof == null || !PolicyMatches(proof, player)) return false;
            if (verify)
            {
                predicted = true;
                before = new BuildState { Failures = *(int*)(manager + DirectFailureOffset), Flag = *(int*)(manager + SearchFlagOffset),
                    Length = *(int*)(manager + RouteLengthOffset) };
                return false;
            }
            // Region gate: different regions search only when E2F60 connects them; otherwise F4930
            // returns 0 at once without writing anything.
            short startRegion = (short)regions[start], targetRegion = (short)regions[target];
            if (startRegion != targetRegion &&
                (targetRegion == 0 || regionsConnected((IntPtr)manager, player, startRegion, targetRegion) == 0))
            { GateHits++; return true; }
            *(int*)(manager + SearchFlagOffset) = 1; *(int*)(manager + RouteLengthOffset) = 0;
            if ((profile == 0 ? directLarge : directDefault)((IntPtr)manager, sx, sy, tx, ty) != 0)
            {
                // Never seen: the straight attempt walks proven-closed edges. F4930's own success path.
                *(int*)(manager + DirectSuccessOffset) += 1; result = *(int*)(manager + RouteLengthOffset);
                DirectSuccesses++; Reset();
                return true;
            }
            *(int*)(manager + DirectFailureOffset) += 1; *(int*)(manager + SearchFlagOffset) = 0; *(int*)(manager + RouteLengthOffset) = 0;
            Hits++;
            return true;
        }

        // After a real F4930 call: a success drops every proof (E1640 may rebuild direction bytes), a
        // repeated failure captures the last search's stamps as a proof.
        internal void Observe(byte* manager, int player, int profile, int result, bool predicted, BuildState before, IEnemyGatePathPolicy gate)
        {
            if (predicted)
            {
                int failures = *(int*)(manager + DirectFailureOffset), flag = *(int*)(manager + SearchFlagOffset), length = *(int*)(manager + RouteLengthOffset);
                bool searched = failures == before.Failures + 1 && flag == 0 && length == 0;
                bool gated = failures == before.Failures && flag == before.Flag && length == before.Length;
                if (result == 0 && (searched || gated)) Verified++;
                else
                {
                    Mismatches++;
                    MismatchSample ??= $"player={player} start={*(int*)(manager + StartXOffset)},{*(int*)(manager + StartYOffset)} " +
                        $"target={*(int*)(manager + TargetXOffset)},{*(int*)(manager + TargetYOffset)} result={result} " +
                        $"directFailures={failures - before.Failures} flag={flag} length={length}";
                }
            }
            if (result > 0) { Reset(); return; }
            if (!TryReadRequest(manager, profile, out _, out _, out _, out _, out int start, out int target)) return;
            int count = failures.TryGetValue(player, out int seen) ? seen + 1 : 1;
            failures[player] = count;
            if (count < CaptureAfterFailures || proofCount >= MaximumProofs) return;
            for (int i = 0; i < proofCount; i++)
                if (proofs[i].Player == player && proofs[i].Contains(start)) return;
            IEnemyGateRoutePolicySnapshot policy = null;
            if (gate != null && gate.HasPublishedMask && gate is IEnemyGateRoutePolicyProvider provider &&
                provider.TryCaptureRoutePolicy(player, out var captured) && captured != null && captured.PlayerId == player && captured.IsCurrent)
                policy = captured;
            CursorGroundRejectProof proof = proofs[proofCount];
            proof.Reset();
            if (!proof.Capture(stamps, *(int*)(manager + GenerationOffset), start, target, connections, directionBits, neighborOffsets, rows, policy))
            { CaptureRejects++; return; }
            proof.Player = player; proof.Target = target; proof.Mode = 0; proof.Profile = profile;
            proof.Policy = policy; proof.Provider = policy == null ? null : gate;
            proofCount++; Captures++;
        }
    }
}

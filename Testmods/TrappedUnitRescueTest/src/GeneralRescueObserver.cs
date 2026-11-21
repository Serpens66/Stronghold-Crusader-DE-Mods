using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    // Deliberately limited to the audited single-building AI AIV drawbridge branch.
    // This does not observe human/editor placement, moat completion or existing save traps.
    internal sealed unsafe class GeneralRescueObserver : IAivBuildStepObserver
    {
        private readonly RescueRuntime runtime;
        private readonly Action<bool> suppression;
        private readonly Action<string> report;
        private long session;
        private int busy;
        private long captures, attempts, rescues;
        internal long CaptureCount => Interlocked.Read(ref captures);
        internal long AttemptCount => Interlocked.Read(ref attempts);
        internal long RescueCount => Interlocked.Read(ref rescues);

        internal GeneralRescueObserver(RescueRuntime runtime, Action<bool> suppression, Action<string> report)
        { this.runtime = runtime; this.suppression = suppression; this.report = report; }

        internal bool IsActive => Interlocked.Read(ref session) != 0;
        internal void SetSession(long id) => Interlocked.Exchange(ref session, id);

        public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
        {
            long observedSession = Interlocked.Read(ref session);
            if (observedSession == 0 || Volatile.Read(ref busy) != 0 || context == null ||
                context.PlayerId < 1 || context.PlayerId > 8 || (uint)context.FrameIndex >= 1000) return null;
            var aiv = GameAIVManagerAPI.Instance;
            if (context.AivStateAddress != (ulong)aiv.GetAIVSystemPointer() ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(context.PlayerId, out var player)) return null;
            int slot = checked((int)player->r_AIVVillageSlotIndex);
            if (slot < 1 || slot > 8 || !aiv.TryGetVillageBySlot(slot, out var village) ||
                village->OwnerPlayerId != context.PlayerId || !aiv.TryGetBuildStep(slot, context.FrameIndex, out var step) ||
                step->BuildingType != eMappers.MAPPER_DRAWBRIDGE || BuildingScales.GetScale(step->BuildingType) != 5) return null;
            var tiles = aiv.GetBuildStepMapTiles(slot, context.FrameIndex);
            if (tiles.Length != 1 || (uint)tiles[0] >= NativeRelocation.TileCapacity) return null;
            var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tiles[0]);
            int x = position.X, y = position.Y;
            if (runtime.Native.Tile(x, y) != tiles[0]) return null;
            if (!runtime.TryCaptureCurrentOccupants(x, y, 5, 5, out var before, out string reason))
            { report?.Invoke("aiv-drawbridge capture deferred: " + reason); return null; }
            before.RemoveAll(unit => !SupportsPilotUnit(unit, context.PlayerId));
            if (before.Count == 0) return null;
            Interlocked.Increment(ref captures);
            // No suppression here: all original Vanilla and Fixes callbacks run unchanged.
            report?.Invoke("aiv-drawbridge captured session=" + observedSession + " player=" + context.PlayerId +
                " frame=" + context.FrameIndex + " anchor=" + x + "," + y + " units=" + before.Count);
            return new Completion(context, observedSession, id => Interlocked.Read(ref session) == id,
                () => Rescue(before));
        }

        private bool SupportsPilotUnit(UnitSnapshot snapshot, int owner)
        {
            // Crew validation deliberately scans other engines for corrupt aliases. Keep that
            // broader experiment and civilian job continuation out of this bounded pilot.
            return snapshot.Owner == owner && snapshot.Unsupported == null && snapshot.CrewIdentity.Length == 0 &&
                runtime.Native.TryUnit(snapshot.Id, out var unit) && unit->N000000DB == 1;
        }

        private void Rescue(List<UnitSnapshot> before)
        {
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
            try
            {
                // Only our own synchronous relocation/reissue is guarded. Native 196280 does
                // not construct walls; the combined Fixes gate cannot affect original AIV work.
                suppression(true);
                foreach (var unit in before)
                {
                    Interlocked.Increment(ref attempts);
                    if (runtime.TryRescue(unit, "observed-ai-aiv-drawbridge", true, out _)) Interlocked.Increment(ref rescues);
                }
            }
            finally { suppression(false); Volatile.Write(ref busy, 0); }
        }

        // Shared by the live broker and focused tests: a receipt is single-use, session-bound,
        // and never treats a result code as evidence that geometry actually changed.
        internal sealed class Completion : IAivBuildStepInvocation
        {
            private readonly AivBuildStepContext context;
            private readonly long session;
            private readonly Func<long, bool> isCurrent;
            private readonly Action after;
            private int completed;
            internal Completion(AivBuildStepContext context, long session, Func<long, bool> isCurrent, Action after)
            { this.context = context; this.session = session; this.isCurrent = isCurrent; this.after = after; }
            public void Complete(AivBuildStepCompletion completion)
            {
                if (Interlocked.Exchange(ref completed, 1) != 0 || completion == null ||
                    !ReferenceEquals(context, completion.Context) || !completion.VanillaCompleted || !isCurrent(session)) return;
                after();
            }
        }
    }
}

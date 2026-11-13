using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal readonly struct KnightSelectionIdentity
    {
        internal readonly long SessionId;
        internal readonly int PlayerId, UnitId, GlobalId, UnitType;

        internal KnightSelectionIdentity(long sessionId, int playerId, int unitId, int globalId, int unitType)
        {
            SessionId = sessionId;
            PlayerId = playerId;
            UnitId = unitId;
            GlobalId = globalId;
            UnitType = unitType;
        }
    }

    internal enum KnightSelectionUnitState { Invalid, NeedsInit, Ready, Selected }

    // The runtime owns native identity/life checks and the existing Extender setter.
    internal interface IKnightSelectionTransferWorld
    {
        KnightSelectionUnitState Read(KnightSelectionIdentity identity);
        KnightSelectionIdentity CaptureIdentity(int unitId);
        void CaptureCurrentSelection(List<int> ids);
        // The caller reuses this buffer; copy the request before returning.
        bool TrySetSelection(List<int> ids);
        void Report(KnightSelectionIdentity identity, string reason);
    }

    // Engine-monitor owned. Retains only replacement identities, never a previous full selection.
    internal sealed class KnightSelectionTransferQueue
    {
        private const int InitializationLimit = 100;
        // SE consumes one Press/Drag/Release phase before each normal engine tick.
        private const int GestureSteps = 3;
        private readonly List<Entry> waiting = new List<Entry>();
        private readonly List<KnightSelectionIdentity> inFlight = new List<KnightSelectionIdentity>();
        private readonly Dictionary<int, KnightSelectionIdentity> gestureSelection =
            new Dictionary<int, KnightSelectionIdentity>();
        private readonly List<KnightSelectionIdentity> ready = new List<KnightSelectionIdentity>();
        private readonly List<int> selection = new List<int>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private long sessionId;
        private int playerId, flightSteps;

        internal bool HasWork => waiting.Count != 0 || inFlight.Count != 0;

        internal void SetContext(long session, int player)
        {
            if (sessionId == session && playerId == player) return;
            Clear();
            sessionId = session;
            playerId = player;
        }

        internal void Clear()
        {
            waiting.Clear();
            inFlight.Clear();
            gestureSelection.Clear();
            ready.Clear();
            selection.Clear();
            seen.Clear();
            flightSteps = 0;
            sessionId = 0;
            playerId = 0;
        }

        internal void Enqueue(KnightSelectionIdentity identity)
        {
            if (sessionId <= 0 || playerId <= 0 || identity.SessionId != sessionId ||
                identity.PlayerId != playerId || identity.UnitId <= 0 || identity.GlobalId <= 0)
                return;
            foreach (Entry entry in waiting)
                if (entry.Identity.GlobalId == identity.GlobalId) return;
            foreach (KnightSelectionIdentity pending in inFlight)
                if (pending.GlobalId == identity.GlobalId) return;
            waiting.Add(new Entry(identity));
        }

        internal bool IsSourceSelected(KnightSelectionIdentity source, bool nativeSelected)
        {
            if (inFlight.Count == 0) return nativeSelected;
            // Our own Press/Drag can transiently clear or expand local marks. This
            // provenance is valid only for this gesture, never for constructing another.
            return gestureSelection.TryGetValue(source.GlobalId, out KnightSelectionIdentity requested) &&
                requested.SessionId == source.SessionId && requested.PlayerId == source.PlayerId &&
                requested.UnitId == source.UnitId && requested.UnitType == source.UnitType;
        }

        internal void Advance(bool eligibleEngineTick, IKnightSelectionTransferWorld world)
        {
            if (!eligibleEngineTick || !HasWork) return;

            if (inFlight.Count != 0 && ++flightSteps >= GestureSteps)
            {
                try
                {
                    foreach (KnightSelectionIdentity identity in inFlight)
                    {
                        // A competing selection may win. Never resurrect the old request.
                        KnightSelectionUnitState state;
                        try { state = world.Read(identity); }
                        catch (Exception error)
                        {
                            world.Report(identity, "selection-readback-failed: " + error.GetType().Name);
                            continue;
                        }
                        if (state != KnightSelectionUnitState.Selected)
                            world.Report(identity, "selection-not-confirmed");
                    }
                }
                finally { inFlight.Clear(); gestureSelection.Clear(); flightSteps = 0; }
            }

            ready.Clear();
            for (int index = 0; index < waiting.Count;)
            {
                Entry entry = waiting[index];
                entry.Steps++;
                KnightSelectionUnitState state;
                try { state = world.Read(entry.Identity); }
                catch (Exception error)
                {
                    waiting.RemoveAt(index);
                    world.Report(entry.Identity, "replacement-read-failed: " + error.GetType().Name);
                    continue;
                }
                if (state == KnightSelectionUnitState.Invalid ||
                    (state == KnightSelectionUnitState.NeedsInit && entry.Steps >= InitializationLimit))
                {
                    waiting.RemoveAt(index);
                    world.Report(entry.Identity, state == KnightSelectionUnitState.Invalid
                        ? "replacement-invalid" : "replacement-initialization-timeout");
                    continue;
                }
                if (inFlight.Count == 0 &&
                    (state == KnightSelectionUnitState.Ready || state == KnightSelectionUnitState.Selected))
                {
                    ready.Add(entry.Identity);
                    waiting.RemoveAt(index);
                    continue;
                }
                index++;
            }
            if (ready.Count == 0) return;

            try
            {
                selection.Clear();
                seen.Clear();
                world.CaptureCurrentSelection(selection);
                for (int index = 0; index < selection.Count;)
                {
                    if (selection[index] <= 0 || !seen.Add(selection[index])) selection.RemoveAt(index);
                    else index++;
                }
                foreach (KnightSelectionIdentity identity in ready)
                    if (seen.Add(identity.UnitId)) selection.Add(identity.UnitId);

                gestureSelection.Clear();
                foreach (int unitId in selection)
                {
                    KnightSelectionIdentity identity = world.CaptureIdentity(unitId);
                    if (identity.GlobalId > 0 && identity.SessionId == sessionId && identity.PlayerId == playerId)
                        gestureSelection[identity.GlobalId] = identity;
                }

                if (world.TrySetSelection(selection))
                {
                    inFlight.AddRange(ready);
                    flightSteps = 0;
                }
                else
                    foreach (KnightSelectionIdentity identity in ready)
                        world.Report(identity, "selection-request-rejected");
            }
            catch (Exception error)
            {
                foreach (KnightSelectionIdentity identity in ready)
                    world.Report(identity, "selection-request-failed: " + error.GetType().Name);
            }
            finally
            {
                ready.Clear();
                selection.Clear();
                seen.Clear();
                if (inFlight.Count == 0) gestureSelection.Clear();
            }
        }

        private sealed class Entry
        {
            internal readonly KnightSelectionIdentity Identity;
            internal int Steps;
            internal Entry(KnightSelectionIdentity identity) { Identity = identity; }
        }
    }
}

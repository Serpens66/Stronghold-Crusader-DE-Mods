using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace APIShared
{
    /// <summary>An immutable list of one-based IDs carrying Vanilla's unit-hover marker.</summary>
    public sealed class MarkedUnitSelectionSnapshot
    {
        private readonly int[] ids;
        internal MarkedUnitSelectionSnapshot(long sessionId, int[] ids)
        { SessionId = sessionId; this.ids = ids; }
        /// <summary>The APIShared mission session that owns this selection.</summary>
        public long SessionId { get; }
        /// <summary>Number of marked units, including locally controlled units.</summary>
        public int Count => ids.Length;
        /// <summary>One-based unit Game-ID at the specified index.</summary>
        public int this[int index] => ids[index];
    }

    /// <summary>Publishes Vanilla's completed unit-hover selection without polling the full unit array.</summary>
    public static class MarkedUnitSelectionAPI
    {
        private delegate void TroopSelectionCall(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished, int[] underCursorChimps,
            int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps);
        private delegate int RunCall(bool mpFrameSkip);
        private delegate void ReturnBufferCall(MemoryBuffers self, MemoryBuffers.MemBuffer buffer);

        private static readonly object Sync = new object();
        private static readonly HashSet<int> selected = new HashSet<int>();
        private static readonly HashSet<int> candidates = new HashSet<int>();
        private static readonly Dictionary<string, Action<MarkedUnitSelectionSnapshot>> observers =
            new Dictionary<string, Action<MarkedUnitSelectionSnapshot>>(StringComparer.Ordinal);
        private static Hook selectionHook;
        private static Hook runHook;
        private static Hook bufferHook;
        private static TroopSelectionCall originalSelection;
        private static RunCall originalRun;
        private static ReturnBufferCall originalReturnBuffer;
        private static ManualLogSource log;
        private static MarkedUnitSelectionSnapshot snapshot;
        private static long sessionId;
        private static bool installed;
        private static bool pendingInput;
        private static int lastNativeCount = -1;
        private static long candidateChecks;
        private static long fullScans;
        private static int completedTick;

        internal static void Initialize(ManualLogSource logger)
        {
            if (installed) return;
            Hook selection = null, run = null, buffer = null;
            try
            {
                MethodInfo inputMethod = typeof(EngineInterface).GetMethod(nameof(EngineInterface.TroopSelection),
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo runMethod = typeof(EngineInterface).GetMethod(nameof(EngineInterface.run),
                    BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                MethodInfo returnMethod = typeof(MemoryBuffers).GetMethod(nameof(MemoryBuffers.returnBuffer),
                    BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(MemoryBuffers.MemBuffer) }, null);
                if (inputMethod == null || runMethod == null || returnMethod == null)
                    throw new MissingMethodException("Installed EngineInterface selection/run signature changed.");
                selection = new Hook(inputMethod, (TroopSelectionCall)OnTroopSelection,
                    new HookConfig { ManualApply = true, ID = "APIShared.MarkedSelection.Input" });
                run = new Hook(runMethod, (RunCall)OnRun,
                    new HookConfig { ManualApply = true, ID = "APIShared.MarkedSelection.PostRun" });
                buffer = new Hook(returnMethod, (ReturnBufferCall)OnReturnBuffer,
                    new HookConfig { ManualApply = true, ID = "APIShared.MarkedSelection.TickCompleted" });
                originalSelection = selection.GenerateTrampoline<TroopSelectionCall>();
                originalRun = run.GenerateTrampoline<RunCall>();
                originalReturnBuffer = buffer.GenerateTrampoline<ReturnBufferCall>();
                selection.Apply();
                buffer.Apply();
                run.Apply();
                selectionHook = selection;
                runHook = run;
                bufferHook = buffer;
                log = logger;
                installed = true;
                logger.LogInfo("MARKED_SELECTION_READY: managed input and post-tick hooks installed; Vanilla calls are passed through once.");
            }
            catch (Exception error)
            {
                // Only an unpublished initialization candidate may be rolled back.
                try { run?.Undo(); run?.Dispose(); } catch { }
                try { buffer?.Undo(); buffer?.Dispose(); } catch { }
                try { selection?.Undo(); selection?.Dispose(); } catch { }
                logger.LogError("MARKED_SELECTION_INSTALL_FAILED: " + error);
            }
        }

        /// <summary>Registers a process-lifetime observer. Its callback must defer UI work.</summary>
        public static bool TryRegisterObserver(string ownerAndId, Action<MarkedUnitSelectionSnapshot> observer)
        {
            if (!installed || string.IsNullOrWhiteSpace(ownerAndId) || observer == null) return false;
            lock (Sync)
            {
                if (observers.ContainsKey(ownerAndId)) return false;
                observers.Add(ownerAndId, observer);
                return true;
            }
        }

        /// <summary>Gets the current selection, scanning all slots once on first use of a ready session.</summary>
        public static bool TryCapture(long expectedSessionId, out MarkedUnitSelectionSnapshot result)
        {
            result = null;
            if (!installed || expectedSessionId <= 0 || MissionLifecycleService.ActiveContext?.SessionId != expectedSessionId)
                return false;
            MarkedUnitSelectionSnapshot publication = null;
            lock (Sync)
            {
                if (snapshot == null || sessionId != expectedSessionId)
                {
                    sessionId = expectedSessionId;
                    selected.Clear();
                    candidates.Clear();
                    pendingInput = false;
                    FullScan("initial-session");
                    publication = PublishIfChanged(force: true);
                }
                result = snapshot;
            }
            if (publication != null) Notify(publication);
            return result != null;
        }

        /// <summary>Requests a one-time resync if a cached ID is no longer marked.</summary>
        public static void RequestResync(long expectedSessionId)
        {
            if (!installed || expectedSessionId != sessionId) return;
            MarkedUnitSelectionSnapshot publication;
            lock (Sync)
            {
                FullScan("consumer-invalidated-id");
                publication = PublishIfChanged();
            }
            if (publication != null) Notify(publication);
        }

        private static void OnTroopSelection(int mouseState, bool rightDown, bool rightUp,
            int[] selectedChimps, bool selectionOn, bool selectionEstablished, int[] underCursorChimps,
            int mousePosX, int mousePosY, bool overTopHalf, int[] onScreenChimps)
        {
            originalSelection(mouseState, rightDown, rightUp, selectedChimps, selectionOn,
                selectionEstablished, underCursorChimps, mousePosX, mousePosY, overTopHalf, onScreenChimps);
            if (mouseState == 0 && !selectionOn && !selectionEstablished && !rightDown && !rightUp) return;
            try
            {
                lock (Sync)
                {
                    pendingInput = true;
                    AddCandidates(selectedChimps);
                    AddCandidates(underCursorChimps);
                    // Vanilla may use the visible list for same-type/double-click selection.
                    AddCandidates(onScreenChimps);
                }
            }
            catch (Exception error) { log?.LogError("MARKED_SELECTION_INPUT_ERROR: " + error); }
        }

        private static void AddCandidates(int[] ids)
        {
            if (ids == null) return;
            foreach (int id in ids) if (id > 0) candidates.Add(id);
        }

        private static int OnRun(bool mpFrameSkip)
        {
            Interlocked.Exchange(ref completedTick, 0);
            int result = originalRun(mpFrameSkip);
            if (Interlocked.Exchange(ref completedTick, 0) != 0)
            {
                try { ReconcileAfterRun(); }
                catch (Exception error) { log?.LogError("MARKED_SELECTION_POST_TICK_ERROR: " + error); }
            }
            return result;
        }

        private static void OnReturnBuffer(MemoryBuffers self, MemoryBuffers.MemBuffer buffer)
        {
            bool completed = buffer != null && buffer.beingFilled;
            originalReturnBuffer(self, buffer);
            if (completed) Interlocked.Exchange(ref completedTick, 1);
        }

        private static void ReconcileAfterRun()
        {
            MarkedUnitSelectionSnapshot publication = null;
            lock (Sync)
            {
                if (snapshot == null || MissionLifecycleService.ActiveContext?.SessionId != sessionId)
                {
                    candidates.Clear();
                    pendingInput = false;
                    return;
                }
                int nativeCount = GamePlayerManagerAPI.Instance.GetHoveredChimpsCount();
                if (!pendingInput && nativeCount == lastNativeCount) return;
                bool membershipChanged = false;
                if (pendingInput)
                {
                    Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                    int[] previousIds = new int[selected.Count];
                    selected.CopyTo(previousIds);
                    foreach (int id in previousIds)
                        if (!IsMarked(units, id)) membershipChanged |= selected.Remove(id);
                    foreach (int id in candidates)
                    {
                        candidateChecks++;
                        if (IsMarked(units, id)) membershipChanged |= selected.Add(id);
                    }
                    candidates.Clear();
                    pendingInput = false;
                }
                bool rescan = selected.Count != nativeCount &&
                    (nativeCount != lastNativeCount || membershipChanged);
                if (rescan) FullScan("native-count-mismatch");
                lastNativeCount = nativeCount;
                if (membershipChanged || rescan) publication = PublishIfChanged();
            }
            if (publication != null) Notify(publication);
        }

        private static bool IsMarked(Span<GameUnit> units, int id) =>
            id > 0 && id <= units.Length && units[id - 1].r_UnitHover != 0;

        private static void FullScan(string reason)
        {
            selected.Clear();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int index = 0; index < units.Length; index++)
                if (units[index].r_UnitHover != 0) selected.Add(index + 1);
            lastNativeCount = GamePlayerManagerAPI.Instance.GetHoveredChimpsCount();
            fullScans++;
            log?.LogInfo("MARKED_SELECTION_SCAN: reason=" + reason + ", session=" + sessionId +
                ", marked=" + selected.Count + ", nativeCount=" + lastNativeCount +
                ", fullScans=" + fullScans + ", candidateChecks=" + candidateChecks);
        }

        private static MarkedUnitSelectionSnapshot PublishIfChanged(bool force = false)
        {
            int[] ids = new int[selected.Count];
            selected.CopyTo(ids);
            Array.Sort(ids);
            if (!force && snapshot != null && snapshot.SessionId == sessionId && snapshot.Count == ids.Length)
            {
                bool same = true;
                for (int index = 0; index < ids.Length; index++)
                    if (snapshot[index] != ids[index]) { same = false; break; }
                if (same) return null;
            }
            snapshot = new MarkedUnitSelectionSnapshot(sessionId, ids);
            log?.LogInfo("MARKED_SELECTION_CHANGED: session=" + sessionId + ", marked=" + ids.Length +
                ", candidateChecks=" + candidateChecks + ", fullScans=" + fullScans);
            return snapshot;
        }

        private static void Notify(MarkedUnitSelectionSnapshot value)
        {
            Action<MarkedUnitSelectionSnapshot>[] listeners;
            lock (Sync)
            {
                listeners = new Action<MarkedUnitSelectionSnapshot>[observers.Count];
                observers.Values.CopyTo(listeners, 0);
            }
            foreach (var listener in listeners)
                try { listener(value); }
                catch (Exception error) { log?.LogError("MARKED_SELECTION_OBSERVER_ERROR: " + error); }
        }
    }
}

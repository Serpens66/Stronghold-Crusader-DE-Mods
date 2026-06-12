using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace UnitLimit
{
    internal sealed class DisbandGameActionHook : IDisposable
    {
        private const int MaxDisbandObservationTicks = 2400;

        private readonly object pendingDisbandLock = new object();
        private readonly Dictionary<int, PendingDisbandUnitObservation> pendingDisbandUnitObservations = new Dictionary<int, PendingDisbandUnitObservation>();
        private readonly ManualLogSource log;
        private readonly Action<int, ActiveUnitCache.ActiveUnitChangeReason, bool> notifyUnitNativeSnapshotChanged;
        private readonly Hook hook;
        private readonly EngineInterfaceGameActionDelegate trampoline;
        private bool disposed;

        private delegate int EngineInterfaceGameActionDelegate(Enums.GameActionCommand command, int structureID, int state, int value2);

        public DisbandGameActionHook(
            ManualLogSource log,
            Action<int, ActiveUnitCache.ActiveUnitChangeReason, bool> notifyUnitNativeSnapshotChanged)
        {
            this.log = log;
            this.notifyUnitNativeSnapshotChanged = notifyUnitNativeSnapshotChanged;

            MethodInfo gameActionMethod = typeof(EngineInterface).GetMethod(
                nameof(EngineInterface.GameAction),
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                null);

            if (gameActionMethod == null)
                throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.GameAction));

            hook = new Hook(gameActionMethod, (EngineInterfaceGameActionDelegate)EngineInterfaceGameActionHook);
            trampoline = hook.GenerateTrampoline<EngineInterfaceGameActionDelegate>();
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            log.LogDebug("UnitLimit Disband GameAction hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            lock (pendingDisbandLock)
                pendingDisbandUnitObservations.Clear();

            hook?.Undo();
            hook?.Dispose();
            log.LogDebug("UnitLimit Disband GameAction hook disposed.");
        }

        private int EngineInterfaceGameActionHook(Enums.GameActionCommand command, int structureID, int state, int value2)
        {
            if (command != Enums.GameActionCommand.Troops_Disband)
                return trampoline(command, structureID, state, value2);

            int currentTick = GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;
            LogDebug(
                "UnitLimit Disband GameAction fired:",
                "tick", currentTick,
                "structureID", structureID,
                "state", state,
                "value2", value2);

            DisbandGameActionObservation observation = null;
            try
            {
                observation = BeginTroopsDisbandGameAction();
            }
            catch (Exception ex)
            {
                log.LogDebug("Unit limit disband begin hook failed: " + ex.Message);
            }

            int result = trampoline(command, structureID, state, value2);
            LogDebug(
                "UnitLimit Disband GameAction trampoline returned:",
                "tick", GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick,
                "result", result,
                "observedUnits", observation == null ? 0 : observation.BeforeSnapshots.Count,
                "selected", observation == null ? "none" : observation.Selected);

            try
            {
                EndTroopsDisbandGameAction(observation);
            }
            catch (Exception ex)
            {
                log.LogDebug("Unit limit disband end hook failed: " + ex.Message);
            }

            return result;
        }

        private DisbandGameActionObservation BeginTroopsDisbandGameAction()
        {
            Dictionary<int, UnitSnapshot> beforeSnapshots = new Dictionary<int, UnitSnapshot>();
            int[] selectedUnitIds = GetSelectedUnitIds(out string selected);
            for (int i = 0; i < selectedUnitIds.Length; i++)
            {
                int unitId = selectedUnitIds[i];
                beforeSnapshots[unitId] = CaptureUnit(unitId);
                LogDebug(
                    "UnitLimit Disband pre-action snapshot:",
                    "unitId", unitId,
                    "before", beforeSnapshots[unitId]);
            }

            LogDebug(
                "UnitLimit Disband pre-action summary:",
                "selected", selected,
                "capturedSnapshots", beforeSnapshots.Count);

            return new DisbandGameActionObservation(
                beforeSnapshots,
                selected,
                GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick);
        }

        private void EndTroopsDisbandGameAction(DisbandGameActionObservation observation)
        {
            if (observation == null)
                return;

            List<PendingDisbandUnitObservation> pendingObservations = null;
            int currentTick = GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;
            int immediateResolved = 0;
            int unchangedPending = 0;
            int intermediatePending = 0;

            foreach (KeyValuePair<int, UnitSnapshot> pair in observation.BeforeSnapshots)
            {
                int unitId = pair.Key;
                UnitSnapshot beforeSnapshot = pair.Value;
                UnitSnapshot afterSnapshot = CaptureUnit(unitId);
                LogDebug(
                    "UnitLimit Disband post-action snapshot:",
                    "unitId", unitId,
                    "before", beforeSnapshot,
                    "after", afterSnapshot);

                if (beforeSnapshot.HasSameState(afterSnapshot))
                {
                    unchangedPending++;
                    LogDebug(
                        "UnitLimit Disband pending unchanged:",
                        "unitId", unitId,
                        "tick", currentTick);
                    AddPendingObservation(ref pendingObservations, new PendingDisbandUnitObservation(
                        unitId,
                        beforeSnapshot,
                        beforeSnapshot,
                        currentTick,
                        currentTick,
                        observation.Selected,
                        false,
                        false));
                    continue;
                }

                if (afterSnapshot.IsAlive)
                {
                    immediateResolved++;
                    LogDebug(
                        "UnitLimit Disband resolved immediately:",
                        "unitId", unitId,
                        "tick", currentTick,
                        "notifyingCache", true);
                    NotifyDisbandSnapshotChanged(unitId);
                    continue;
                }

                intermediatePending++;
                LogDebug(
                    "UnitLimit Disband pending intermediate:",
                    "unitId", unitId,
                    "tick", currentTick,
                    "after", afterSnapshot);
                AddPendingObservation(ref pendingObservations, new PendingDisbandUnitObservation(
                    unitId,
                    beforeSnapshot,
                    afterSnapshot,
                    currentTick,
                    currentTick,
                    observation.Selected,
                    true,
                    false));
            }

            if (pendingObservations != null)
            {
                lock (pendingDisbandLock)
                {
                    for (int i = 0; i < pendingObservations.Count; i++)
                        pendingDisbandUnitObservations[pendingObservations[i].UnitId] = pendingObservations[i];
                }
            }

            LogDebug(
                "UnitLimit Disband post-action summary:",
                "selected", observation.Selected,
                "observedUnits", observation.BeforeSnapshots.Count,
                "immediateResolved", immediateResolved,
                "pendingUnchanged", unchangedPending,
                "pendingIntermediate", intermediatePending,
                "pendingTotal", pendingObservations == null ? 0 : pendingObservations.Count);
        }

        private void OnGameTick(int tick)
        {
            List<PendingDisbandUnitObservation> observations;
            lock (pendingDisbandLock)
            {
                if (pendingDisbandUnitObservations.Count == 0)
                    return;

                observations = new List<PendingDisbandUnitObservation>(pendingDisbandUnitObservations.Values);
            }

            List<int> completedUnitIds = null;
            foreach (PendingDisbandUnitObservation observation in observations)
            {
                UnitSnapshot after = CaptureUnit(observation.UnitId);
                if (!observation.LastSnapshot.HasSameState(after))
                {
                    if (after.IsAlive)
                    {
                        AddCompletedUnitId(ref completedUnitIds, observation.UnitId);
                        LogDebug(
                            "UnitLimit Disband deferred resolved:",
                            "unitId", observation.UnitId,
                            "elapsedTicks", tick - observation.StartTick,
                            "before", observation.BeforeSnapshot,
                            "last", observation.LastSnapshot,
                            "after", after,
                            "notifyingCache", true);
                        NotifyDisbandSnapshotChanged(observation.UnitId);
                        continue;
                    }

                    PendingDisbandUnitObservation updated = observation.WithSnapshot(after, tick, true, false);
                    LogDebug(
                        "UnitLimit Disband deferred intermediate:",
                        "unitId", observation.UnitId,
                        "elapsedTicks", tick - observation.StartTick,
                        "last", observation.LastSnapshot,
                        "after", after);
                    lock (pendingDisbandLock)
                        pendingDisbandUnitObservations[observation.UnitId] = updated;
                    continue;
                }

                if (tick - observation.StartTick < MaxDisbandObservationTicks)
                    continue;

                AddCompletedUnitId(ref completedUnitIds, observation.UnitId);
                LogDebug(
                    "UnitLimit Disband deferred expired:",
                    "unitId", observation.UnitId,
                    "elapsedTicks", tick - observation.StartTick,
                    "stableTicks", tick - observation.LastChangeTick,
                    "observedChange", observation.HasObservedChange,
                    "cacheUpdated", observation.CacheUpdated,
                    "last", observation.LastSnapshot,
                    "after", after,
                    "notifyingCache", observation.HasObservedChange && !observation.CacheUpdated);
                if (observation.HasObservedChange && !observation.CacheUpdated)
                    NotifyDisbandSnapshotChanged(observation.UnitId);
            }

            if (completedUnitIds == null)
                return;

            lock (pendingDisbandLock)
            {
                for (int i = 0; i < completedUnitIds.Count; i++)
                    pendingDisbandUnitObservations.Remove(completedUnitIds[i]);
            }
        }

        private void NotifyDisbandSnapshotChanged(int unitId)
        {
            LogDebug(
                "UnitLimit Disband notifying ActiveUnitCache:",
                "unitId", unitId,
                "reason", ActiveUnitCache.ActiveUnitChangeReason.Disbanded,
                "preferFallbackReason", true);
            notifyUnitNativeSnapshotChanged(unitId, ActiveUnitCache.ActiveUnitChangeReason.Disbanded, true);
        }

        private static void AddPendingObservation(ref List<PendingDisbandUnitObservation> observations, PendingDisbandUnitObservation observation)
        {
            if (observations == null)
                observations = new List<PendingDisbandUnitObservation>();

            observations.Add(observation);
        }

        private static void AddCompletedUnitId(ref List<int> completedUnitIds, int unitId)
        {
            if (completedUnitIds == null)
                completedUnitIds = new List<int>();

            completedUnitIds.Add(unitId);
        }

        private static unsafe UnitSnapshot CaptureUnit(int unitId)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                return UnitSnapshot.Missing;

            return UnitSnapshot.From(*unit);
        }

        private static int[] GetSelectedUnitIds(out string selected)
        {
            try
            {
                int count = GamePlayerManagerAPI.Instance.GetSelectedChimpsCount();
                selected = "count=0";
                if (count <= 0)
                    return new int[0];

                int[] selectedChimps = GamePlayerManagerAPI.Instance.GetSelectedChimps();
                int maxCount = Math.Min(count, selectedChimps.Length);
                List<int> unitIds = new List<int>(maxCount);
                StringBuilder builder = new StringBuilder();
                builder.Append("count=");
                builder.Append(count);
                builder.Append(", units=[");

                for (int i = 0; i < maxCount && i < 32; i++)
                {
                    int unitId = selectedChimps[i];
                    if (unitId > 0)
                        unitIds.Add(unitId);

                    if (i > 0)
                        builder.Append(';');

                    builder.Append(unitId);
                }

                if (maxCount > 32)
                    builder.Append(";...");

                builder.Append(']');
                selected = builder.ToString();
                selected = selected + ", apiArrayLength=" + selectedChimps.Length + ", capturedUnits=" + unitIds.Count;
                return unitIds.ToArray();
            }
            catch (Exception ex)
            {
                selected = "error=" + ex.GetType().Name;
                return new int[0];
            }
        }

        private sealed class DisbandGameActionObservation
        {
            public readonly Dictionary<int, UnitSnapshot> BeforeSnapshots;
            public readonly string Selected;
            public readonly int StartTick;

            public DisbandGameActionObservation(Dictionary<int, UnitSnapshot> beforeSnapshots, string selected, int startTick)
            {
                BeforeSnapshots = beforeSnapshots;
                Selected = selected;
                StartTick = startTick;
            }
        }

        private struct PendingDisbandUnitObservation
        {
            public readonly int UnitId;
            public readonly UnitSnapshot BeforeSnapshot;
            public readonly UnitSnapshot LastSnapshot;
            public readonly int StartTick;
            public readonly int LastChangeTick;
            public readonly string Selected;
            public readonly bool HasObservedChange;
            public readonly bool CacheUpdated;

            public PendingDisbandUnitObservation(
                int unitId,
                UnitSnapshot beforeSnapshot,
                UnitSnapshot lastSnapshot,
                int startTick,
                int lastChangeTick,
                string selected,
                bool hasObservedChange,
                bool cacheUpdated)
            {
                UnitId = unitId;
                BeforeSnapshot = beforeSnapshot;
                LastSnapshot = lastSnapshot;
                StartTick = startTick;
                LastChangeTick = lastChangeTick;
                Selected = selected;
                HasObservedChange = hasObservedChange;
                CacheUpdated = cacheUpdated;
            }

            public PendingDisbandUnitObservation WithSnapshot(UnitSnapshot snapshot, int tick, bool hasObservedChange, bool cacheUpdated)
            {
                return new PendingDisbandUnitObservation(UnitId, BeforeSnapshot, snapshot, StartTick, tick, Selected, HasObservedChange || hasObservedChange, CacheUpdated || cacheUpdated);
            }
        }

        private struct UnitSnapshot
        {
            private readonly bool found;
            private readonly AliveState aliveState;
            private readonly eChimps unitType;
            private readonly int ownerId;

            private UnitSnapshot(bool found, AliveState aliveState, eChimps unitType, int ownerId)
            {
                this.found = found;
                this.aliveState = aliveState;
                this.unitType = unitType;
                this.ownerId = ownerId;
            }

            public static UnitSnapshot Missing
            {
                get { return new UnitSnapshot(false, default(AliveState), default(eChimps), default(int)); }
            }

            public static UnitSnapshot From(GameUnit unit)
            {
                return new UnitSnapshot(true, unit.r_AliveState, unit.r_UnitChimp, unit.r_ControllableForPlayerId);
            }

            public bool IsAlive
            {
                get { return found && aliveState == AliveState.IsAlive; }
            }

            public bool HasSameState(UnitSnapshot other)
            {
                return found == other.found &&
                    aliveState == other.aliveState &&
                    unitType == other.unitType &&
                    ownerId == other.ownerId;
            }

            public override string ToString()
            {
                if (!found)
                    return "found=false";

                return "found=true,alive=" + aliveState + ",type=" + unitType + ",owner=" + ownerId;
            }
        }

        private void LogDebug(params object[] parts)
        {
            if (log == null)
                return;

            log.LogDebug(string.Join(" ", parts));
        }
    }
}

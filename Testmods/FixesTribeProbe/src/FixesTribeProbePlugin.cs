using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;

namespace FixesTribeProbe
{
    [BepInDependency("000shcdese", "2.10.1")]
    [BepInDependency("fixes", "1.19.1")]
    [BepInPlugin(Guid, "Fixes Tribe Probe", Version)]
    public sealed unsafe class FixesTribeProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "FixesTribeProbe_Serp";
        public const string Version = "0.1.0";
        private const string Marker = "FIXES_TRIBE_PROBE";
        private const int RecentCreateTicks = 5;
        private const int FollowupTicks = 200;

        // SHCDE destroys the early plugin component. All long-lived callbacks and state
        // are rooted here and invoked by Script Extender publishers after startup.
        private static ManualLogSource log;
        private static IDisposable createSubscription;
        private static IDisposable assignSubscription;
        private static readonly Dictionary<int, int> recentCreates = new Dictionary<int, int>();
        private static readonly Dictionary<long, Assignment> pendingAssignments = new Dictionary<long, Assignment>();
        private static readonly List<Assignment> followups = new List<Assignment>();
        private static bool initialized;
        private static bool tickMarkerLogged;
        private static int currentTick;

        private sealed class Assignment
        {
            internal int UnitId;
            internal int TargetTribeId;
            internal int PreviousTribeId;
            internal int UnitOwner;
            internal int TargetOwner;
            internal uint UnitGlobalId;
            internal int CreatedAtTick;
            internal bool FirstSnapshotLogged;
        }

        private void Awake()
        {
            log = Logger;
            Write("AWAKE", "version=" + Version + " observerOnly=true");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (initialized)
                return;

            try
            {
                // These subscriptions only read game state. In particular, they never
                // set SkipOriginalFunction or modify mutable event arguments.
                createSubscription = TribeR3EventHooks.OnTribeCreate.Observable.Subscribe(OnTribeCreate);
                assignSubscription = TribeR3EventHooks.OnTribeAssignUnit.Observable.Subscribe(OnTribeAssignUnit);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                initialized = true;
                Write("READY", "publisher=ScriptExtender localPlayer=" + LocalPlayer());
            }
            catch (Exception exception)
            {
                Write("INIT_ERROR", exception.ToString());
            }
        }

        private static void OnTribeCreate(TribeCreateEventArgs args)
        {
            try
            {
                int selectedId = SelectedTribe();
                int selectedOwner = TribeOwner(selectedId);
                if (args.Phase == EventHookPhase.Pre)
                {
                    Write("CREATE_PRE", "tick=" + currentTick + " localPlayer=" + LocalPlayer() +
                        " requestedOwner=" + args.PlayerIdOwner + " selectedTribe=" + selectedId +
                        " selectedOwner=" + selectedOwner);
                    return;
                }
                if (args.Phase != EventHookPhase.Post)
                    return;

                int createdId = checked((int)args.ReturnValue);
                if (ValidTribe(createdId))
                    recentCreates[createdId] = currentTick;
                Write("CREATE_POST", "tick=" + currentTick + " localPlayer=" + LocalPlayer() +
                    " requestedOwner=" + args.PlayerIdOwner + " createdTribe=" + createdId +
                    " createdOwner=" + TribeOwner(createdId) + " selectedTribe=" + selectedId +
                    " selectedOwner=" + selectedOwner);
            }
            catch (Exception exception)
            {
                Write("CREATE_ERROR", exception.ToString());
            }
        }

        private static void OnTribeAssignUnit(TribeAssignUnitEventArgs args)
        {
            try
            {
                if (!recentCreates.TryGetValue(args.TribeId, out int createdAt) ||
                    currentTick - createdAt > RecentCreateTicks)
                    return;
                long key = ((long)args.TribeId << 32) | (uint)args.UnitId;
                if (args.Phase == EventHookPhase.Pre)
                {
                    if (!TryReadUnit(args.UnitId, out GameUnit* unit))
                        return;
                    int previous = unit->r_TribeId;
                    Assignment assignment = new Assignment
                    {
                        UnitId = args.UnitId,
                        TargetTribeId = args.TribeId,
                        PreviousTribeId = previous,
                        UnitOwner = unit->r_ControllableForPlayerId,
                        TargetOwner = TribeOwner(args.TribeId),
                        UnitGlobalId = unit->r_GlobalId,
                        CreatedAtTick = currentTick
                    };
                    pendingAssignments[key] = assignment;
                    Write("ASSIGN_PRE", Describe(assignment, unit) +
                        " previousMember=" + HasMember(previous, args.UnitId) +
                        " targetMember=" + HasMember(args.TribeId, args.UnitId) +
                        " previousGroup=" + TribeSummary(previous) +
                        " targetGroup=" + TribeSummary(args.TribeId));
                    return;
                }
                if (args.Phase != EventHookPhase.Post || !pendingAssignments.TryGetValue(key, out Assignment pending))
                    return;

                pendingAssignments.Remove(key);
                if (!TryReadUnit(args.UnitId, out GameUnit* after))
                    return;
                bool ownerMismatch = pending.UnitOwner != pending.TargetOwner;
                bool previousMember = HasMember(pending.PreviousTribeId, args.UnitId);
                Write(ownerMismatch ? "CROSS_OWNER_ASSIGN" : "ASSIGN_POST",
                    Describe(pending, after) + " previousMember=" + previousMember +
                    " targetMember=" + HasMember(args.TribeId, args.UnitId) +
                    " previousGroup=" + TribeSummary(pending.PreviousTribeId) +
                    " targetGroup=" + TribeSummary(args.TribeId) +
                    " ownerMismatch=" + ownerMismatch + " return=" + args.ReturnValue);
                if (ownerMismatch)
                    followups.Add(pending);
            }
            catch (Exception exception)
            {
                Write("ASSIGN_ERROR", exception.ToString());
            }
        }

        private static void OnTick(int tick)
        {
            try
            {
                if (tick < currentTick)
                {
                    recentCreates.Clear();
                    pendingAssignments.Clear();
                    followups.Clear();
                    Write("SESSION_RESET", "newTick=" + tick);
                }
                currentTick = tick;
                if (!tickMarkerLogged && tick > 0)
                {
                    tickMarkerLogged = true;
                    Write("POST_STARTUP_TICK", "tick=" + tick + " localPlayer=" + LocalPlayer());
                }

                // Keep only the short event-correlation window. Nothing here changes
                // the native tribe array or the game's synchronized state.
                List<int> expired = null;
                foreach (KeyValuePair<int, int> entry in recentCreates)
                {
                    if (tick - entry.Value <= RecentCreateTicks)
                        continue;
                    if (expired == null)
                        expired = new List<int>();
                    expired.Add(entry.Key);
                }
                if (expired != null)
                    foreach (int id in expired)
                        recentCreates.Remove(id);
                // Pre/Post assignment events are synchronous; an unmatched Pre
                // observation has no useful role beyond its original tick.
                pendingAssignments.Clear();

                for (int index = followups.Count - 1; index >= 0; index--)
                {
                    Assignment observation = followups[index];
                    if (tick <= observation.CreatedAtTick)
                        continue;
                    if (!observation.FirstSnapshotLogged)
                    {
                        Snapshot("SNAPSHOT_NEXT_TICK", observation);
                        observation.FirstSnapshotLogged = true;
                    }
                    if (tick - observation.CreatedAtTick >= FollowupTicks)
                    {
                        Snapshot("SNAPSHOT_200_TICKS", observation);
                        followups.RemoveAt(index);
                    }
                }
            }
            catch (Exception exception)
            {
                Write("TICK_ERROR", exception.ToString());
            }
        }

        private static void Snapshot(string phase, Assignment assignment)
        {
            if (!TryReadUnit(assignment.UnitId, out GameUnit* unit))
                return;
            Write(phase, Describe(assignment, unit) +
                " sameGlobalId=" + (unit->r_GlobalId == assignment.UnitGlobalId) +
                " previousMember=" + HasMember(assignment.PreviousTribeId, assignment.UnitId) +
                " targetMember=" + HasMember(assignment.TargetTribeId, assignment.UnitId) +
                " previousGroup=" + TribeSummary(assignment.PreviousTribeId) +
                " targetGroup=" + TribeSummary(assignment.TargetTribeId));
        }

        private static string Describe(Assignment assignment, GameUnit* unit)
        {
            return "tick=" + currentTick + " localPlayer=" + LocalPlayer() +
                " selectedTribe=" + SelectedTribe() + " unit=" + assignment.UnitId +
                " global=" + assignment.UnitGlobalId + " unitOwner=" + assignment.UnitOwner +
                " unitType=" + unit->r_UnitChimp + " previousTribe=" + assignment.PreviousTribeId +
                " targetTribe=" + assignment.TargetTribeId + " targetOwner=" + assignment.TargetOwner +
                " unitTribeNow=" + unit->r_TribeId;
        }

        private static int LocalPlayer() => GamePlayerManagerAPI.Instance.GetLocalPlayerId();
        private static int SelectedTribe() => checked((int)GameTribeManagerAPI.Instance.GetTribeManager().Pointer->CurrentSelectedTribeId);
        private static bool ValidTribe(int id) => GameTribeManagerAPI.Instance.IsValidId(id);

        private static int TribeOwner(int id)
        {
            if (!ValidTribe(id) || !GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* tribe))
                return -1;
            return tribe->r_PlayerIdOwner;
        }

        private static string TribeSummary(int id)
        {
            if (!ValidTribe(id) || !GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* tribe))
                return "invalid";
            return tribe->r_AliveState + "/owner=" + tribe->r_PlayerIdOwner +
                "/members=" + tribe->r_UnitsInGroup;
        }

        private static bool TryReadUnit(int id, out GameUnit* unit)
        {
            unit = null;
            return GameUnitManagerAPI.Instance.IsValidId(id) &&
                GameUnitManagerAPI.Instance.TryGetUnitById(id, out unit);
        }

        private static bool HasMember(int tribeId, int unitId)
        {
            if (!ValidTribe(tribeId) || !GameUnitManagerAPI.Instance.IsValidId(unitId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe))
                return false;
            ushort* words = &tribe->r_UnitIdsInGroupBitfield;
            return (words[unitId >> 4] & (1 << (unitId & 15))) != 0;
        }

        private static void Write(string phase, string details)
        {
            log?.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " +
                Marker + " " + phase + " " + details);
        }
    }
}

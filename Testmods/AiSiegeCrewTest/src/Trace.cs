using System;
using System.Diagnostics;
using APIShared;
#if SERP_DEVDIAG
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using APIShared.Simulation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
#endif

namespace AiSiegeCrewTest
{
    internal static class Trace
    {
        internal static bool Available
        {
            get {
#if SERP_DEVDIAG
                return true;
#else
                return false;
#endif
            }
        }
        internal static int NewAliases
        {
            get {
#if SERP_DEVDIAG
                return Observer.NewAliasCount;
#else
                return -1;
#endif
            }
        }
        internal static bool Healthy
        {
            get {
#if SERP_DEVDIAG
                return Observer.Healthy;
#else
                return false;
#endif
            }
        }
        [Conditional("SERP_DEVDIAG")]
        internal static void Start(MissionContext mission)
        {
#if SERP_DEVDIAG
            Observer.Start(mission);
#endif
        }
        [Conditional("SERP_DEVDIAG")]
        internal static void Capture(string reason)
        {
#if SERP_DEVDIAG
            Observer.Capture(reason, true);
#endif
        }
        [Conditional("SERP_DEVDIAG")]
        internal static void Stop()
        {
#if SERP_DEVDIAG
            Observer.Stop();
#endif
        }
    }
#if SERP_DEVDIAG
    // Observation only. No new native hook, command, repair or native write.
    // The publisher retains this observer after BepInEx's startup cleanup.
    internal static class Observer
    {
        private static MissionContext mission;
        private static object monitor;
        private static IntPtr image;
        private static bool registered;
        private static int tick, lastSample, lines;
        private static readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private static readonly Dictionary<string, string> prior = new Dictionary<string, string>();
        private static readonly Dictionary<string, int> stationary = new Dictionary<string, int>();
        private static readonly Dictionary<int, uint> watchedSlots = new Dictionary<int, uint>();
        private static readonly System.Collections.Generic.HashSet<string> initialAliases = new System.Collections.Generic.HashSet<string>();
        private static readonly System.Collections.Generic.HashSet<string> newAliases = new System.Collections.Generic.HashSet<string>();
        private static bool initialCapture;
        private static bool faulted;
        private static int samples;
        internal static bool Healthy => !faulted && mission != null && samples >= 32;
        internal static int NewAliasCount => newAliases.Count;
        private static readonly List<int> members = new List<int>();
        private const int PlayerStride = 0x583C, NativeRole = 22;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        private static void Log(string text)
        {
            if (lines++ < 12000) Shared.DevDiag.Log(() => "AI_CREW_TRACE tick=" + tick + " " + text);
            else if (lines == 12001) Shared.DevDiag.Log(() => "AI_CREW_TRACE LIMIT: detailed observation budget exhausted");
        }
        internal static void Start(MissionContext current)
        {
            string path = Path.Combine(BepInEx.Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var input = File.OpenRead(path)) using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "") != "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2")
                    throw new InvalidOperationException("Diagnostic native layout is unknown");
            image = GetModuleHandle("CrusaderDE.dll");
            var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            if (image == IntPtr.Zero || field == null || field.FieldType != typeof(object))
                throw new InvalidOperationException("Diagnostic simulation boundary unavailable");
            monitor = field.GetValue(null);
            if (monitor == null) throw new InvalidOperationException("No simulation monitor");
            lock (monitor)
            {
                mission = current; prior.Clear(); stationary.Clear(); watchedSlots.Clear(); initialAliases.Clear(); newAliases.Clear(); faulted = false; samples = 0; lines = 0; lastSample = int.MinValue;
                if (!registered)
                {
                    if (!EngineRunEvents.TryRegister(Plugin.Guid, "crew-diagnostic", () => { }, AfterRun, out string why))
                        throw new InvalidOperationException(why);
                    subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(args => {
                        if (mission == null || (args.AICommand != TribeAICommand.ManPitchCauldronOrBuildTent &&
                            args.AICommand != TribeAICommand.ManSiegeEquipment && args.AICommand != TribeAICommand.DissolveSiegeEquipment)) return;
                        lock (monitor)
                        {
                            Log("ORDER_REQUEST phase=" + args.Phase + " command=" + args.AICommand + " tribe=" + args.TribeId + " target=" + args.TargetValue1 + "/" + args.TargetValue2 + " a6=" + args.a6);
                            DescribeGroup(args.TribeId, "ordered", true);
                        }
                    }));
                    subscriptions.Add(UnitR3EventHooks.OnUnitTransition.Observable.Subscribe(args => {
                        if (mission == null || args.NextUnitType != eChimps.CHIMP_TYPE_ENGINEER) return;
                        Log("TRANSITION_REQUEST phase=" + args.Phase + " id=" + args.UnitId + " owner=" + args.PlayerOwnerId + " next=" + args.NextUnitType + " source=" + args.Source);
                    }));
                    registered = true;
                }
                Log("START session=" + current.SessionId + " gates=" + Gates());
                initialCapture = true;
                try { Capture("initial", true); } finally { initialCapture = false; }
            }
        }
        internal static void Stop()
        {
            if (monitor == null) return;
            lock (monitor) { Log("END observedLines=" + lines + " newAliasPairs=" + newAliases.Count); mission = null; }
        }
        private static void AfterRun()
        {
            if (mission == null || !Monitor.TryEnter(monitor)) return;
            try
            {
                if (!SimulationClock.TryGetCurrentTick(mission, out int now) || now == lastSample) return;
                tick = now; lastSample = now;
                samples++;
                Capture("run-end", false);
            }
            catch (Exception error) { Log("OBSERVATION_ERROR " + error); faulted = true; mission = null; }
            finally { Monitor.Exit(monitor); }
        }
        private static string Gates()
        {
            Type runtime = BepInEx.Bootstrap.Chainloader.PluginInfos["BugfixesAndQoL_Serp"].Instance.GetType().Assembly.GetType("BugfixesAndQoL.BugfixesAndQoLRuntime");
            object fix = runtime.GetField("processSiegeCrewFixRuntime", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (fix == null) return "unavailable";
            string text = "";
            foreach (string name in new[] { "aiReadinessActivation", "timeoutActivation", "fireActivation", "autoBindingActivation" })
            {
                IntPtr word = (IntPtr)fix.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fix);
                text += name + "=" + (word == IntPtr.Zero ? "missing" : Marshal.ReadInt32(word).ToString()) + ",";
            }
            return text;
        }
        private static void Changed(string key, string core, string detail, bool force)
        {
            if (force || !prior.TryGetValue(key, out string old) || old != core) Log(key + " " + detail);
            prior[key] = core;
        }
        internal static unsafe void Capture(string reason, bool force)
        {
            if (mission == null || !SimulationClock.TryGetCurrentTick(mission, out tick)) return;
            bool summary = force || (tick & 127) == 0;
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int unitLimit = Math.Min(units.Length, 9999); // Audited one-based native ID ceiling.
            var claims = new Dictionary<string, string>();
            var physical = new int[9];
            for (int spanIndex = 0; spanIndex < unitLimit; spanIndex++)
            {
                GameUnit unit = units[spanIndex]; int id = spanIndex + 1;
                int capacity = Capacity(unit.r_UnitChimp);
                if (capacity == 0 || unit.r_AliveState != AliveState.IsAlive || unit.r_IsKilledByProjectile != 0 || unit.r_CurrentHealth == 0) continue;
                watchedSlots[id] = unit.r_GlobalId;
                int count = U16(id, 0xA0C);
                string crew = "";
                if (count <= capacity)
                    for (int n = 0; n < count; n++)
                    {
                        int worker = U16(id, 0x96C + n * 2); uint global = unchecked((uint)I32(id, 0x974 + n * 4));
                        string identity = worker + "/" + global;
                        crew += identity + ",";
                        if (worker <= 0 || worker > unitLimit || global == 0 || units[worker - 1].r_GlobalId != global)
                            Changed("STALE_CREW:" + id + ":" + n, identity, "crew=" + identity + " device=" + id + "/" + unit.r_GlobalId, force);
                        else if (claims.TryGetValue(identity, out string other))
                        {
                            string pair = identity + "@" + other + "," + id + "/" + unit.r_GlobalId;
                            if (initialCapture) initialAliases.Add(pair);
                            else if (!initialAliases.Contains(pair) && newAliases.Add(pair)) Log("NEW_ALIAS_PAIR " + pair);
                            Changed("MULTIPLE_CLAIMS:" + pair, pair, "devices=" + other + "," + id + "/" + unit.r_GlobalId, force);
                        }
                        else claims.Add(identity, id + "/" + unit.r_GlobalId);
                    }
                Changed("DEVICE:" + id + "/" + unit.r_GlobalId, count + ":" + crew,
                    "type=" + unit.r_UnitChimp + " owner=" + unit.r_ControllableForPlayerId + " count=" + count + " crew=" + crew, force);
            }
            for (int spanIndex = 0; spanIndex < unitLimit; spanIndex++)
            {
                GameUnit unit = units[spanIndex]; int id = spanIndex + 1;
                if (watchedSlots.TryGetValue(id, out uint oldGlobal))
                {
                    string watchCore = unit.r_GlobalId + ":" + unit.r_UnitChimp + ":" + unit.r_AliveState + ":" + unit.r_IsKilledByProjectile + ":" + (unit.r_CurrentHealth > 0);
                    Changed("WATCHED_SLOT:" + id + "/" + oldGlobal, watchCore, watchCore, force);
                    if (oldGlobal != unit.r_GlobalId || unit.r_AliveState == AliveState.None) watchedSlots.Remove(id);
                }
                if (unit.r_AliveState == AliveState.None || unit.r_AITribeRole != NativeRole) continue;
                watchedSlots[id] = unit.r_GlobalId;
                string key = id + "/" + unit.r_GlobalId;
                int recruit = U16(id, 0x9CA), pending = U16(id, 0x922), packed = I32(id, 0x918);
                bool living = unit.r_AliveState == AliveState.IsAlive && unit.r_IsKilledByProjectile == 0;
                if (living && unit.r_UnitChimp == eChimps.CHIMP_TYPE_ENGINEER && unit.r_ControllableForPlayerId <= 8) physical[unit.r_ControllableForPlayerId]++;
                string core = unit.r_AliveState + ":" + unit.r_UnitChimp + ":" + packed + ":" + recruit + ":" + pending + ":" + unit.r_TribeId + ":" + I32(id, 0x93C);
                string detail = "owner=" + unit.r_ControllableForPlayerId + " type=" + unit.r_UnitChimp + " life=" + unit.r_AliveState +
                    " death=" + unit.r_IsKilledByProjectile + " state=0x" + packed.ToString("X8") + " recruit=" + recruit +
                    " pending=" + pending + "/" + U16(id, 0x924) + " tribe=" + unit.r_TribeId + "/" + unchecked((uint)I32(id, 0x93C)) +
                    " timeout=" + I32(id, 0x974) + " pos=" + unit.r_CurrentTilePositionX + "," + unit.r_CurrentTilePositionY +
                    " target=" + unit.r_TargetTilePositionX + "," + unit.r_TargetTilePositionY + " path=" + unit.r_PathPlanLength +
                    " mountRaw=" + U16(id, 0x9B0) + " command=" + unit.r_AI_LastIssuedTribeCommand +
                    " context=" + unit.r_AI_ContextTargetUnitId + "/" + unit.r_AI_ContextTargetUnitGlobalId +
                    " device=" + (claims.TryGetValue(key, out string device) ? device : "none");
                Changed("ENGINEER:" + key, core, detail, force);
                if (living && claims.ContainsKey(key) && unit.r_AIState != 5 && packed != 0x0005006D)
                    Changed("CREW_STATE_MISMATCH:" + key, packed.ToString(), detail, force);
                string idle = core + ":" + unit.r_CurrentTilePositionX + ":" + unit.r_CurrentTilePositionY;
                if (!prior.TryGetValue("idle:" + key, out string before) || before != idle) stationary[key] = tick;
                prior["idle:" + key] = idle;
                if (summary && living && unit.r_AIState <= 2 && stationary.TryGetValue(key, out int since) &&
                    SimulationClock.TryGetElapsedTicks(since, tick, out int elapsed) && elapsed >= 180)
                    Log("STATIONARY_ROLE22 candidateOnly=True duration=" + elapsed + " id=" + key + " " + detail);
            }
            if (!summary) return;
            Log("SNAPSHOT reason=" + reason + " gates=" + Gates());
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < Math.Min(buildings.Length, 3999); spanIndex++)
            {
                GameBuilding building = buildings[spanIndex];
                if (building.r_AliveState == AliveState.None || !IsTent(building.r_BuildingType)) continue;
                int id = spanIndex + 1;
                IntPtr anchor = IntPtr.Add(image, 0x64CCBB0 + id * 0x32C);
                int count = (short)Marshal.ReadInt16(anchor, 0x1F6);
                string crew = "";
                if (count >= 0 && count <= 4)
                    for (int n = 0; n < count; n++) crew += (ushort)Marshal.ReadInt16(anchor, 0x1FA + n * 2) + "/" + unchecked((uint)Marshal.ReadInt32(anchor, 0x204 + n * 4)) + ",";
                string detail = "type=" + building.r_BuildingType + " owner=" + building.r_PlayerIdOwner + " life=" + building.r_AliveState +
                    " count=" + count + " crew=" + crew + " progressRaw=" + Marshal.ReadInt32(anchor, 0x2D4) +
                    " pos=" + building.r_TilePositionXBegin + "," + building.r_TilePositionYBegin;
                Changed("TENT:" + id + "/" + building.r_GlobalId, detail, detail, force);
            }
            for (int player = 1; player <= 8; player++)
            {
                int offset = player * PlayerStride;
                if (R32(0x379D0D0 + offset) == 0) continue;
                int pool = (ushort)Marshal.ReadInt16(image, 0x379F7BA + offset);
                Log("PLAYER owner=" + player + " physicalRole22Engineers=" + physical[player] + " nativeRole22=" + R32(0x379E79C + offset) +
                    " nativeDevices=" + R32(0x379E6EC + offset) + " cooldown=" + R32(0x379E6E8 + offset) + " gold=" + R32(0x379B2DC + offset) +
                    " pool=" + pool + "/" + unchecked((uint)R32(0x379FB5C + offset)));
                DescribeGroup(pool, "reserve", force);
            }
        }
        private static unsafe void DescribeGroup(int id, string reason, bool force)
        {
            if (id <= 0 || id >= 4500 || !GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* group)) return;
            members.Clear(); GameTribeManagerAPI.Instance.GetUnits(id, members);
            string ids = string.Join(",", members);
            Changed("GROUP:" + id + "/" + group->r_GlobalId, group->r_UnitsInGroup + ":" + ids,
                "reason=" + reason + " owner=" + group->r_PlayerIdOwner + " life=" + group->r_AliveState + " count=" + group->r_UnitsInGroup + " members=" + ids, force);
        }
        private static int Capacity(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_CATAPULT: case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_BALLISTA: case eChimps.CHIMP_TYPE_ARAB_BALLISTA: return 2;
                case eChimps.CHIMP_TYPE_TREBUCHET: return 3;
                case eChimps.CHIMP_TYPE_SIEGE_TOWER: case eChimps.CHIMP_TYPE_BATTERING_RAM: return 4;
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD: return 1;
                default: return 0;
            }
        }
        private static bool IsTent(eStructs type) => type == eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA ||
            type == eStructs.STRUCT_SIEGE_TENT_CATAPULT || type == eStructs.STRUCT_SIEGE_TENT_TREBUCHET ||
            type == eStructs.STRUCT_SIEGE_TENT_SIEGE_TOWER || type == eStructs.STRUCT_SIEGE_TENT_BATTERING_RAM ||
            type == eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD;
        private static IntPtr A(int id) => IntPtr.Add(image, 0x67E8400 + id * 0x490);
        private static int U16(int id, int offset) => (ushort)Marshal.ReadInt16(A(id), offset);
        private static int I32(int id, int offset) => Marshal.ReadInt32(A(id), offset);
        private static int R32(int rva) => Marshal.ReadInt32(image, rva);
    }
#endif
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AiGroupAdmissionTest
{
    // Test-only evidence, deliberately kept out of APIShared: the order ledger timeline of the
    // probe groups (which APIShared ledger operation changed or removed a group's current order,
    // and what survives save/load) and the castle-defense allocator inputs of a class.
    internal sealed partial class Fixture
    {
        private const int TraceLineLimit = 800;
        private static readonly object traceSync = new object();
        private static readonly HashSet<int> watchedGroups = new HashSet<int>();
        private static Action<string> traceLog;
        private static int traceLines;
        private static Harmony harmony;
        [System.Runtime.InteropServices.DllImport("kernel32", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        private void InstallLedgerTrace()
        {
            traceLog = log;
            if (harmony != null) return;
            Type ledger = AccessTools.TypeByName("APIShared.Units.AiMilitary.Orders.AiOrderLedger");
            Require(ledger != null, "AiOrderLedger type not found for the ledger trace");
            harmony = new Harmony(Plugin.Guid + ".ledger-trace");
            var prefix = new HarmonyMethod(typeof(Fixture), nameof(TracePrefix));
            int patched = 0;
            foreach (string name in new[] { "Invalidate", "ObserveMove", "ObserveTarget", "ObserveAccepted", "RecordAdmissionOrder", "ObserveStrategic", "Unknown" })
            {
                MethodInfo method = AccessTools.Method(ledger, name);
                if (method == null) { log("LEDGER-TRACE missing method " + name); continue; }
                harmony.Patch(method, prefix: prefix); patched++;
            }
            log("LEDGER-TRACE installed methods=" + patched);
        }

        private void WatchProbeGroups(string round)
        {
            lock (traceSync)
                foreach (var probe in Current(round))
                    if (probe.Checked && probe.Final != null && probe.Final.Group.TribeId > 0) watchedGroups.Add(probe.Final.Group.TribeId);
            log("LEDGER-TRACE watching groups=" + watchedGroups.Count);
        }

        private static void TracePrefix(object __instance, MethodBase __originalMethod, object[] __args)
        {
            try
            {
                int group = -1;
                foreach (object argument in __args)
                    if (argument != null && argument.GetType().Name == "AiNativeGroup") { group = GroupId(argument); break; }
                lock (traceSync)
                {
                    if (!watchedGroups.Contains(group) || traceLines >= TraceLineLimit) return;
                    traceLines++;
                }
                string args = string.Join(",", __args.Select(argument => argument == null ? "null" :
                    argument.GetType().Name == "AiNativeGroup" ? "g" + GroupId(argument) :
                    argument.GetType().Name == "AiCurrentOrder" ? DescribeOrder(argument) : argument.ToString()));
                string caller = string.Empty;
                var frames = new StackTrace(1, false).GetFrames();
                if (frames != null)
                    caller = string.Join("<", frames.Take(4).Select(frame => frame.GetMethod()?.DeclaringType?.Name + "." + frame.GetMethod()?.Name));
                traceLog?.Invoke("LEDGER op=" + __originalMethod.Name + " group=" + group + " tick=" + Tick() + " before=" +
                    LedgerEntry(__instance, group) + " args=" + args + " caller=" + caller);
            }
            catch (Exception error) { traceLog?.Invoke("LEDGER-TRACE error " + error.GetType().Name + ": " + error.Message); }
        }

        // Ledger contents of every watched group: APIShared private state read by reflection.
        private void LogLedger(string point)
        {
            object ledger = LedgerInstance();
            if (ledger == null) { log("LEDGER-SNAPSHOT " + point + " ledger=null"); return; }
            var text = new List<string>();
            lock (traceSync)
                foreach (int group in watchedGroups.OrderBy(value => value))
                    text.Add(group + ":" + LedgerEntry(ledger, group) + "/m" + Members(group));
            log("LEDGER-SNAPSHOT " + point + " tick=" + Tick() + " " + string.Join(" ", text));
        }

        private static object LedgerInstance()
        {
            Type service = AccessTools.TypeByName("APIShared.Units.AiMilitary.AiMilitaryService");
            object instance = service == null ? null : AccessTools.Field(service, "Instance")?.GetValue(null);
            return instance == null ? null : AccessTools.Field(service, "orders")?.GetValue(instance);
        }

        private static string LedgerEntry(object ledger, int group)
        {
            if (!(AccessTools.Field(ledger.GetType(), "orders")?.GetValue(ledger) is IDictionary orders)) return "?";
            foreach (DictionaryEntry entry in orders)
                if (GroupId(entry.Key) == group) return DescribeOrder(entry.Value);
            return "none";
        }

        private static int GroupId(object group) => (int)AccessTools.Property(group.GetType(), "Id").GetValue(group);

        private static string DescribeOrder(object order)
        {
            Type type = order.GetType();
            object Get(string name) => AccessTools.Property(type, name)?.GetValue(order);
            return Get("Kind") + "(" + Get("X") + "," + Get("Y") + ",c" + Get("Command") + ",a" + Get("Action") + ")";
        }

        private static unsafe int Members(int group) =>
            GameTribeManagerAPI.Instance.TryGetTribeById(group, out GameTribe* tribe) && tribe != null ? tribe->r_UnitsInGroup : -1;

        private static string Tick() => APIShared.Simulation.SimulationTickEvents.TryGetCurrentTick(out int tick) ? tick.ToString() : "?";

        // Castle-defense allocator inputs (2BC40) of one class, read from both candidate AIC bases.
        private unsafe string DefenseClassContext(int cls)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner, out GamePlayerResources* resources) || resources == null)
                return "class=" + cls + " resources=null";
            int identity = (int)resources->r_AILordMinusOne;
            byte* module = (byte*)GetModuleHandle("CrusaderDE.dll");
            if (module == null) return "class=" + cls + " module=null";
            var aics = GameAIManagerAPI.Instance.GetAICArray();
            string extender = identity > 0 && identity <= aics.Length ? aics.GetValuePointer(identity - 1)->max_mobile_groups.ToString() : "?";
            int player = identity > 0 ? *(int*)(module + 0x366C210 + identity * 0x5E4 - 0x4CC) : 0;
            int ai = identity > 0 ? *(int*)(module + 0x404C950 + identity * 0x5E4 - 0x4CC) : 0;
            int roleBase = *(int*)(module + 0x2CEE80 + cls * 4);
            var slots = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                int id = resources->r_AITribeIdsByStorageRole[roleBase + i];
                if (id == 0) continue;
                string record = GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* tribe) && tribe != null
                    ? "s" + (int)tribe->r_AliveState + "/o" + tribe->r_PlayerIdOwner + "/g" + tribe->r_GlobalId + "/m" + tribe->r_UnitsInGroup : "absent";
                slots.Add((roleBase + i) + "=" + id + "/" + resources->r_AITribeGlobalIdsByStorageRole[roleBase + i] + ":" + record);
            }
            return "class=" + cls + " count=" + resources->r_AIDefensivePositionCountsByClass.r_Counts[cls] + " identity=" + identity +
                " maxMobile(extender)=" + extender + " at404C950=" + ai + " at366C210=" + player +
                " rotation=" + *(int*)((byte*)resources + 0x2B54) + " alert=" + *(int*)((byte*)resources + 0x38F0) +
                " slots=[" + string.Join(" ", slots) + "]";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace PerfProbe
{
    // "Find all problems" probes (2026-10-19): Update sweep, engine marker discovery, batching
    // census, GC and processTestMap record breakdown. Observation only; Vanilla runs unchanged.
    // All work is main-thread bound (Unity callbacks and onBeforeRender) unless noted.
    internal static class PerfProbeExtras
    {
        private const int TopPerWindow = 10, TopPerSpike = 3, MaxMarkerRecorders = 48, RecordTypes = 64;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static ManualLogSource log;
        internal static bool UpdateSweep = true, EngineMarkers = true, BatchCensus = true, GcTracking = true, RecordBreakdown = true,
            EngineCalls = true, TextureInventory = true;

        internal static void BindConfig(ConfigFile config)
        {
            UpdateSweep = config.Bind("Extras", "UpdateSweep", true,
                "Time every Update/LateUpdate/FixedUpdate/OnGUI of Assembly-CSharp and Noesis MonoBehaviours; log the top entries.").Value;
            EngineMarkers = config.Bind("Extras", "EngineMarkers", true,
                "Discover Unity profiler markers/counters available in this player and record render/wait related ones.").Value;
            BatchCensus = config.Bind("Extras", "BatchCensus", true,
                "Count active renderers, distinct materials/textures/sorting orders and estimated batch breaks per snapshot.").Value;
            GcTracking = config.Bind("Extras", "GcTracking", true,
                "Track GC collections and managed allocation per frame and attach them to spike lines.").Value;
            RecordBreakdown = config.Bind("Extras", "RecordBreakdown", true,
                "Histogram processTestMap records by type, time addUpdateChimp/Org/Pixie and count interpolating units.").Value;
            EngineCalls = config.Bind("Extras", "EngineCalls", true,
                "Time main-thread EngineInterface calls (threadLock waits while a sim tick runs) and log blocking calls.").Value;
            TextureInventory = config.Bind("Extras", "TextureInventory", true,
                "Log size/format/filter of every texture and team mask used by visible sprites (once per texture).").Value;
        }

        internal static void Install(ManualLogSource logger, Harmony harmony)
        {
            log = logger;
            if (RecordBreakdown)
            {
                PatchUnique(harmony, typeof(GameMap), nameof(GameMap.addUpdateChimp), nameof(EndChimp));
                PatchUnique(harmony, typeof(GameMap), nameof(GameMap.addUpdateOrg), nameof(EndOrg));
                PatchUnique(harmony, typeof(GameMap), nameof(GameMap.addUpdatePixie), nameof(EndPixie));
            }
            if (GcTracking) InitThreadAlloc();
            if (UpdateSweep) InstallSweep(harmony);
            if (EngineCalls) InstallEngineCalls(harmony);
            if (RecordBreakdown)
            {
                // GameMap.chimps is private: read-only reflection, validated against the audited type.
                chimpsField = AccessTools.Field(typeof(GameMap), "chimps");
                if (chimpsField != null && chimpsField.FieldType != typeof(Dictionary<int, Chimp>)) chimpsField = null;
                if (chimpsField == null) Shared.DebugLogHelper.LogWarning(log, "PERFPROBE GameMap.chimps differs from the audit; interp counts disabled.");
            }
        }

        private static FieldInfo chimpsField;

        // ---------- thread-exact allocation counter ----------

        // GC.GetAllocatedBytesForCurrentThread exists in Unity's mscorlib but not in the net481
        // reference profile; bound by reflection and validated by a 1 MB self-test (Boehm may return 0).
        private static Func<long> threadAllocated;
        internal static bool ThreadAllocValid;
        private static long simAllocBytes, simAllocTicks, simAllocMax;

        private static void InitThreadAlloc()
        {
            try
            {
                MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(long))
                {
                    Shared.DevDiag.Log(() => "threadAlloc unavailable: GC.GetAllocatedBytesForCurrentThread missing; using process-wide heap growth.");
                    return;
                }
                threadAllocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                long before = threadAllocated();
                byte[] probe = new byte[1 << 20];
                long after = threadAllocated();
                GC.KeepAlive(probe);
                long delta = after - before;
                ThreadAllocValid = delta >= 1 << 20 && delta < 4 << 20;
                Shared.DevDiag.Log(() => "threadAlloc selfTest delta=" + delta.ToString(C) + " valid=" + ThreadAllocValid);
            }
            catch (Exception exception)
            {
                ThreadAllocValid = false;
                Shared.DebugLogHelper.LogWarning(log, "PERFPROBE thread allocation counter unavailable: " + exception.Message);
            }
        }

        internal static long ThreadAllocated() => ThreadAllocValid ? threadAllocated() : -1;

        // Sim thread: allocations of one EngineInterface.run (managed side of DLL_RunTick and buffer copies).
        internal static void AddSimAlloc(long bytes)
        {
            if (bytes < 0) return;
            System.Threading.Interlocked.Add(ref simAllocBytes, bytes);
            System.Threading.Interlocked.Increment(ref simAllocTicks);
            long observed;
            while (bytes > (observed = System.Threading.Volatile.Read(ref simAllocMax)) &&
                System.Threading.Interlocked.CompareExchange(ref simAllocMax, bytes, observed) != observed) { }
        }

        // ---------- sheet occupancy (V7) ----------

        private static bool sheetOccupancyDone;

        // Once per process after sprites are loaded: per texture the summed sprite rect area
        // (BC7 4-px aligned) versus texture area, and per material class the 16384² pages needed.
        private static void LogSheetOccupancy()
        {
            if (sheetOccupancyDone || spriteLoader.instance == null) return;
            FieldInfo allField = AccessTools.Field(typeof(spriteLoader), "allSprites");
            FieldInfo gmField = AccessTools.Field(typeof(spriteLoader), "gmSprites");
            FieldInfo altField = AccessTools.Field(typeof(spriteLoader), "gmAltSprites");
            FieldInfo matField = AccessTools.Field(typeof(spriteLoader), "gmMaterials");
            if (allField?.FieldType != typeof(Dictionary<string, Sprite>) || gmField?.FieldType != typeof(Sprite[][]) ||
                altField?.FieldType != typeof(Sprite[][]) || matField?.FieldType != typeof(Material[][]))
            {
                sheetOccupancyDone = true;
                Shared.DebugLogHelper.LogWarning(log, "PERFPROBE spriteLoader fields differ from the audit; sheet occupancy skipped.");
                return;
            }
            var all = (Dictionary<string, Sprite>)allField.GetValue(spriteLoader.instance);
            var gm = (Sprite[][])gmField.GetValue(spriteLoader.instance);
            var alt = (Sprite[][])altField.GetValue(spriteLoader.instance);
            var materials = (Material[][])matField.GetValue(spriteLoader.instance);
            if (all == null || all.Count == 0) return;
            sheetOccupancyDone = true;
            var sprites = new HashSet<Sprite>(all.Values.Where(s => s != null));
            var textureClass = new Dictionary<int, string>();
            for (int file = 0; file < gm.Length; file++)
            {
                Material material = materials != null && file < materials.Length && materials[file] != null && materials[file].Length > 0 ? materials[file][0] : null;
                string cls = material != null && material.shader != null ? material.shader.name : "default";
                foreach (Sprite[] array in new[] { gm[file], alt != null && file < alt.Length ? alt[file] : null })
                {
                    if (array == null) continue;
                    foreach (Sprite sprite in array)
                    {
                        if (sprite == null || sprite.texture == null) continue;
                        sprites.Add(sprite);
                        textureClass[sprite.texture.GetInstanceID()] = cls;
                    }
                }
            }
            var perTexture = new Dictionary<int, (Texture2D Texture, int Count, long Area, int Tight, int Packed)>();
            foreach (Sprite sprite in sprites)
            {
                Texture2D texture = sprite.texture;
                if (texture == null) continue;
                Rect rect = sprite.textureRect;
                long area = (long)(Math.Ceiling(rect.width / 4.0) * 4) * (long)(Math.Ceiling(rect.height / 4.0) * 4);
                int id = texture.GetInstanceID();
                perTexture.TryGetValue(id, out var entry);
                entry.Texture = texture; entry.Count++; entry.Area += area;
                if (sprite.vertices.Length > 4) entry.Tight++;
                if (sprite.packed) entry.Packed++;
                perTexture[id] = entry;
            }
            const double Page = 16384.0 * 16384.0;
            var lines = new List<string>();
            var classArea = new Dictionary<string, (long Used, long Sheets, int Count)>();
            foreach (var entry in perTexture.Values.OrderByDescending(e => (long)e.Texture.width * e.Texture.height))
            {
                long size = (long)entry.Texture.width * entry.Texture.height;
                string cls = textureClass.TryGetValue(entry.Texture.GetInstanceID(), out string known) ? known : "unmapped";
                lines.Add(string.Format(C, "{0} {1}x{2} {3} sprites={4} used={5:0.0}% tightMesh={6} packed={7} class={8}",
                    entry.Texture.name, entry.Texture.width, entry.Texture.height, entry.Texture.format, entry.Count,
                    100.0 * entry.Area / size, entry.Tight, entry.Packed, cls));
                classArea.TryGetValue(cls, out var sum);
                classArea[cls] = (sum.Used + entry.Area, sum.Sheets + size, sum.Count + 1);
            }
            for (int start = 0; start < lines.Count; start += 10)
            {
                string chunk = string.Join(" | ", lines.Skip(start).Take(10));
                Shared.DevDiag.Log(() => "sheets: " + chunk);
            }
            // Pages assume 85 % packing efficiency for rect packing without rotation.
            string classes = string.Join(" | ", classArea.Select(e => string.Format(C,
                "{0}: sheets={1} sheetAreaPages={2:0.00} usedAreaPages={3:0.00} pagesAt85%={4}", e.Key, e.Value.Count,
                e.Value.Sheets / Page, e.Value.Used / Page, (int)Math.Ceiling(e.Value.Used / (Page * 0.85)))));
            Shared.DevDiag.Log(() => "sheetClasses (16384² pages) " + classes);
        }

        // ---------- static sprite share (V8) ----------

        private static Dictionary<int, long> previousSignatures = new Dictionary<int, long>();
        private static Dictionary<int, long> currentSignatures = new Dictionary<int, long>();
        private static int staticCompared, staticUnchanged;
        private static readonly Dictionary<int, int> staticComparedByClass = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> staticUnchangedByClass = new Dictionary<int, int>();

        private static long Signature(SpriteRenderer renderer)
        {
            Vector3 position = renderer.transform.position;
            Color32 color = renderer.color;
            unchecked
            {
                long hash = renderer.sprite != null ? renderer.sprite.GetInstanceID() : 0;
                hash = hash * 31 + (renderer.sharedMaterial != null ? renderer.sharedMaterial.GetInstanceID() : 0);
                hash = hash * 31 + renderer.sortingOrder;
                hash = hash * 31 + (long)Math.Round(position.x * 256f);
                hash = hash * 31 + (long)Math.Round(position.y * 256f);
                hash = hash * 31 + (long)Math.Round(position.z * 256f);
                hash = hash * 31 + ((color.r << 24) | (color.g << 16) | (color.b << 8) | color.a);
                hash = hash * 31 + (renderer.flipX ? 1 : 0) + (renderer.flipY ? 2 : 0);
                return hash;
            }
        }

        // ---------- main-thread EngineInterface calls (threadLock contention) ----------

        private static readonly Dictionary<MethodBase, int> engineIds = new Dictionary<MethodBase, int>();
        private static string[] engineNames = new string[0];
        private static long[] engineCalls = new long[0], engineTicks = new long[0], engineMax = new long[0],
            engineSimCalls = new long[0], engineSimTicks = new long[0];
        private static long[] engineFrameTicks = new long[0];
        private static bool[] engineLockMeasured = new bool[0];
        private static bool engineDiagnosticError;
        private static int mainThreadId = -1;

        private static void InstallEngineCalls(Harmony harmony)
        {
            mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var targets = new List<MethodInfo>();
            foreach (MethodInfo method in typeof(EngineInterface).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                // run is measured separately; extern DLL_* methods have no IL body.
                if (method.Name == nameof(EngineInterface.run) || method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
                targets.Add(method);
            }
            engineNames = new string[targets.Count];
            engineCalls = new long[targets.Count]; engineTicks = new long[targets.Count]; engineMax = new long[targets.Count];
            engineSimCalls = new long[targets.Count]; engineSimTicks = new long[targets.Count];
            engineFrameTicks = new long[targets.Count]; engineLockMeasured = new bool[targets.Count];
            var prefix = new HarmonyMethod(typeof(PerfProbeExtras), nameof(EngineBegin));
            var postfix = new HarmonyMethod(typeof(PerfProbeExtras), nameof(EngineEnd));
            var finalizer = new HarmonyMethod(typeof(PerfProbeExtras), nameof(EngineFailed));
            int patched = 0, failed = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                engineNames[index] = "EngineInterface." + targets[index].Name;
                engineIds[targets[index]] = index;
                try
                {
                    bool measureLock = targets[index].Name == "GetMeritData" || targets[index].Name == "SetAchData";
                    harmony.Patch(targets[index], prefix: prefix, postfix: postfix, finalizer: finalizer,
                        transpiler: measureLock ? new HarmonyMethod(typeof(PerfProbeLockTiming), nameof(PerfProbeLockTiming.Transpile)) : null,
                        ilmanipulator: null);
                    engineLockMeasured[index] = measureLock;
                    patched++;
                }
                catch (Exception exception)
                {
                    failed++;
                    Shared.DebugLogHelper.LogWarning(log, "PERFPROBE engine call skipped " + engineNames[index] + ": " + exception.Message);
                }
            }
            Shared.DevDiag.Log(() => "PERFPROBE_ENGINECALLS: " + patched + " EngineInterface methods timed on the main thread, " + failed +
                " skipped; exact Monitor timing=" + string.Join(",", Enumerable.Range(0, targets.Count).Where(i => engineLockMeasured[i]).Select(i => engineNames[i])));
        }

        private struct EngineState
        {
            public long Started, RunsCompleted, Cpu;
            public int Gc;
            public bool SimActive, Completed;
            public PerfProbeLockTiming.Snapshot Locks;
        }

        private static void EngineBegin(MethodBase __originalMethod, out EngineState __state)
        {
            __state = default;
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != mainThreadId) return;
            __state.Started = Stopwatch.GetTimestamp();
            __state.SimActive = PerfProbeRuntime.SimTickActive;
            __state.RunsCompleted = PerfProbeRuntime.SimRunsCompleted;
            __state.Gc = GcTracking ? GC.CollectionCount(0) : -1;
            __state.Locks = PerfProbeLockTiming.Capture();
            __state.Cpu = engineIds.TryGetValue(__originalMethod, out int id) && engineLockMeasured[id]
                ? PerfProbeLockTiming.ThreadCpuTime() : -1;
        }

        private static void EngineEnd(MethodBase __originalMethod, ref EngineState __state)
        {
            if (__state.Completed) return;
            __state.Completed = true;
            EngineComplete(__originalMethod, __state, null);
        }

        private static Exception EngineFailed(MethodBase __originalMethod, ref EngineState __state, Exception __exception)
        {
            if (__exception != null && !__state.Completed)
            {
                __state.Completed = true;
                EngineComplete(__originalMethod, __state, __exception);
            }
            return __exception;
        }

        private static void EngineComplete(MethodBase method, EngineState state, Exception error)
        {
            // A diagnostic failure must not replace an original result or exception.
            try { EngineCompleteCore(method, state, error); }
            catch (Exception exception)
            {
                if (engineDiagnosticError) return;
                engineDiagnosticError = true;
                Shared.DebugLogHelper.LogWarning(log, "PERFPROBE engine detail unavailable: " + exception.GetType().Name);
            }
        }

        private static void EngineCompleteCore(MethodBase __originalMethod, EngineState __state, Exception error)
        {
            if (__state.Started == 0) return;
            long ended = Stopwatch.GetTimestamp(), elapsed = ended - __state.Started;
            if (__originalMethod == null || !engineIds.TryGetValue(__originalMethod, out int id)) return;
            engineCalls[id]++; engineTicks[id] += elapsed;
            engineFrameTicks[id] += elapsed;
            if (elapsed > engineMax[id]) engineMax[id] = elapsed;
            if (__state.SimActive) { engineSimCalls[id]++; engineSimTicks[id] += elapsed; }
            if (PerfProbeRuntime.SpikeLines && elapsed >= PerfProbeRuntime.MainThreadSpikeTicks)
            {
                PerfProbeLockTiming.Snapshot after = PerfProbeLockTiming.Capture();
                long cpu = __state.Cpu < 0 ? -1 : PerfProbeLockTiming.ThreadCpuTime();
                string detail = string.Format(C,
                    "simTickActiveAtStart={0} simTickActiveAtEnd={1} simRunsCompletedDuring={2} lockMeasured={3} " +
                    "lockEntries={4} lockWaitMs={5:F3} lockScopeMs={6:F3} outsideLockMs={7:F3} threadCpuMs={8:F3} gcDuring={9} error={10} caller={11}",
                    __state.SimActive, PerfProbeRuntime.SimTickActive, PerfProbeRuntime.SimRunsCompleted - __state.RunsCompleted,
                    engineLockMeasured[id], after.Acquisitions - __state.Locks.Acquisitions,
                    engineLockMeasured[id] ? Ms(after.Wait - __state.Locks.Wait) : -1,
                    engineLockMeasured[id] ? Ms(after.Held - __state.Locks.Held) : -1,
                    engineLockMeasured[id] ? Ms(Math.Max(0, elapsed - (after.Wait - __state.Locks.Wait) - (after.Held - __state.Locks.Held))) : -1,
                    cpu < 0 ? -1 : (cpu - __state.Cpu) / 10000.0,
                    __state.Gc < 0 ? -1 : GC.CollectionCount(0) - __state.Gc, error?.GetType().Name ?? "none", SlowCaller());
                PerfProbeRuntime.ReportSpike("main:" + engineNames[id], __state.Started, ended, detail);
            }
        }

        private static string SlowCaller()
        {
            var names = new List<string>(8);
            foreach (StackFrame frame in new StackTrace(false).GetFrames() ?? new StackFrame[0])
            {
                MethodBase method = frame.GetMethod();
                if (method?.DeclaringType == null || method.DeclaringType.Namespace == "PerfProbe") continue;
                names.Add(method.DeclaringType.FullName + "." + method.Name);
                if (names.Count == 8) break;
            }
            return string.Join("<-", names);
        }

        private static void LogEngineCalls(double windowSeconds)
        {
            var ids = new List<int>();
            for (int id = 0; id < engineCalls.Length; id++) if (engineCalls[id] > 0) ids.Add(id);
            double total = ids.Sum(id => Ms(engineTicks[id])), duringSim = ids.Sum(id => Ms(engineSimTicks[id]));
            string top = string.Join(" | ", ids.OrderByDescending(id => engineTicks[id]).Take(TopPerWindow).Select(id => string.Format(C,
                "{0} msPerS={1:0.00} calls/s={2:0} maxUs={3:0} duringSimCalls={4} duringSimMs={5:0.0}", engineNames[id].Substring(16),
                Ms(engineTicks[id]) / windowSeconds, engineCalls[id] / windowSeconds, Ms(engineMax[id]) * 1000.0,
                engineSimCalls[id], Ms(engineSimTicks[id]))));
            foreach (int id in ids) { engineCalls[id] = engineTicks[id] = engineMax[id] = engineSimCalls[id] = engineSimTicks[id] = 0; }
            string message = string.Format(C, "engineCalls(main) active={0} totalMsPerS={1:0.00} duringSimMsTotal={2:0.0} top: {3}",
                ids.Count, total / windowSeconds, duringSim, top.Length == 0 ? "none" : top);
            Shared.DevDiag.Log(() => message);
        }

        // ---------- processTestMap record breakdown ----------

        private static readonly long[] recordTypeCounts = new long[RecordTypes + 1];

        // Main thread (processTestMap prefix). Reads the record type word only; record stride is 21 shorts.
        internal static void CountRecordTypes(short[] sourceMap, int numElements)
        {
            if (!RecordBreakdown || sourceMap == null || numElements <= 0) return;
            int limit = Math.Min(numElements, sourceMap.Length / 21);
            for (int index = 0; index < limit; index++)
            {
                int type = sourceMap[index * 21];
                recordTypeCounts[type >= 0 && type < RecordTypes ? type : RecordTypes]++;
            }
        }

        private static void PatchUnique(Harmony harmony, Type type, string name, string postfix)
        {
            try
            {
                MethodInfo[] candidates = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => method.Name == name).ToArray();
                if (candidates.Length != 1) throw new MissingMethodException(type.Name + "." + name + " overload count " + candidates.Length + " differs from the audit.");
                harmony.Patch(candidates[0], prefix: new HarmonyMethod(typeof(PerfProbeExtras), nameof(BeginTimed)),
                    postfix: new HarmonyMethod(typeof(PerfProbeExtras), postfix));
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, "PERFPROBE patch skipped for " + type.Name + "." + name + ": " + exception.Message);
            }
        }

        private static void BeginTimed(out long __state) { __state = 0; Shared.DevDiag.Start(ref __state); }
        private static void EndChimp(long __state) => Shared.DevDiag.Stop(ref __state, "main:GameMap.addUpdateChimp");
        private static void EndOrg(long __state) => Shared.DevDiag.Stop(ref __state, "main:GameMap.addUpdateOrg");
        private static void EndPixie(long __state) => Shared.DevDiag.Stop(ref __state, "main:GameMap.addUpdatePixie");

        // ---------- Update sweep ----------

        private static readonly Dictionary<MethodBase, int> sweepIds = new Dictionary<MethodBase, int>();
        private static string[] sweepNames = new string[0];
        private static long[] sweepCalls = new long[0], sweepTicks = new long[0], sweepMax = new long[0], sweepFrameTicks = new long[0];
        private static readonly List<int> sweepTouchedThisFrame = new List<int>();

        private static void InstallSweep(Harmony harmony)
        {
            var targets = new List<MethodInfo>();
            var assemblies = new List<Assembly> { typeof(Director).Assembly };
            Assembly noesis = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Noesis.NoesisGUI");
            if (noesis != null) assemblies.Add(noesis);
            string[] names = { "Update", "LateUpdate", "FixedUpdate", "OnGUI" };
            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException exception) { types = exception.Types.Where(t => t != null).ToArray(); }
                foreach (Type type in types)
                {
                    if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                    foreach (string name in names)
                    {
                        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                        if (method != null && !method.IsAbstract && !method.ContainsGenericParameters && method.GetMethodBody() != null) targets.Add(method);
                    }
                }
            }
            // Nested probes inside the hot callbacks, so time and allocation can be narrowed down.
            var nested = new (Type Type, string Name, Type[] Signature)[]
            {
                (typeof(GameMap), "lerpChimps", Type.EmptyTypes),
                (typeof(GameMap), nameof(GameMap.processTestMap), new[] { typeof(short[]), typeof(int), typeof(EngineInterface.PlayState), typeof(byte[]) }),
                (typeof(GameMap), nameof(GameMap.ApplyRadarMap), Type.EmptyTypes),
                (typeof(GameMap), nameof(GameMap.experimentalRowManager), Type.EmptyTypes),
                (typeof(TilemapManager), nameof(TilemapManager.endTileRefresh), new[] { typeof(bool) }),
                (typeof(CrusaderDE.MainViewModel), nameof(CrusaderDE.MainViewModel.TestRadarMap), Type.EmptyTypes),
                (typeof(FatControler), nameof(FatControler.NoesisGUIUpdateChecksInGame), Type.EmptyTypes),
            };
            foreach (var probe in nested)
            {
                MethodInfo method = AccessTools.Method(probe.Type, probe.Name, probe.Signature);
                if (method == null) Shared.DebugLogHelper.LogWarning(log, "PERFPROBE nested probe missing: " + probe.Type.Name + "." + probe.Name);
                else if (!targets.Contains(method)) targets.Add(method);
            }
            sweepNames = new string[targets.Count];
            sweepCalls = new long[targets.Count]; sweepTicks = new long[targets.Count];
            sweepMax = new long[targets.Count]; sweepFrameTicks = new long[targets.Count]; sweepAlloc = new long[targets.Count]; sweepCleanTicks = new long[targets.Count];
            int patched = 0, failed = 0;
            var prefix = new HarmonyMethod(typeof(PerfProbeExtras), nameof(SweepBegin));
            var postfix = new HarmonyMethod(typeof(PerfProbeExtras), nameof(SweepEnd));
            for (int index = 0; index < targets.Count; index++)
            {
                MethodInfo method = targets[index];
                sweepNames[index] = method.DeclaringType.FullName + "." + method.Name;
                sweepIds[method] = index;
                try { harmony.Patch(method, prefix: prefix, postfix: postfix); patched++; }
                catch (Exception exception)
                {
                    failed++;
                    Shared.DebugLogHelper.LogWarning(log, "PERFPROBE sweep skipped " + sweepNames[index] + ": " + exception.Message);
                }
            }
            Shared.DevDiag.Log(() => "PERFPROBE_SWEEP: " + patched + " Unity callbacks timed, " + failed + " skipped, assemblies=" +
                string.Join(",", assemblies.Select(a => a.GetName().Name)) + ".");
        }

        private struct SweepState { public long Started, MonoUsed, SimRuns; public int Gc; public bool SimActive; }

        // Allocation = growth of the Mono heap during the callback; discarded if a GC ran meanwhile.
        // Includes nested callees (inclusive), like the timing.
        private static void SweepBegin(out SweepState __state)
        {
            long allocated = !GcTracking ? 0 : ThreadAllocValid ? threadAllocated() : UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            __state = new SweepState
            {
                MonoUsed = allocated, Gc = GC.CollectionCount(0),
                SimActive = PerfProbeRuntime.SimTickActive, SimRuns = PerfProbeRuntime.SimRunsCompleted
            };
            __state.Started = Stopwatch.GetTimestamp();
        }

        private static void SweepEnd(MethodBase __originalMethod, SweepState __state)
        {
            long elapsed = Stopwatch.GetTimestamp() - __state.Started;
            if (__originalMethod == null || !sweepIds.TryGetValue(__originalMethod, out int id)) return;
            sweepCalls[id]++;
            sweepTicks[id] += elapsed;
            if (elapsed > sweepMax[id]) sweepMax[id] = elapsed;
            if (sweepFrameTicks[id] == 0) sweepTouchedThisFrame.Add(id);
            sweepFrameTicks[id] += elapsed;
            if (GcTracking && ThreadAllocValid)
            {
                // Exact for this thread; a GC in between does not matter.
                long grown = threadAllocated() - __state.MonoUsed;
                if (grown > 0) sweepAlloc[id] += grown;
            }
            else if (GcTracking)
            {
                // Process-wide heap growth: only samples without GC and without any overlapping sim
                // tick are attributed (the sim thread is the only other busy managed thread).
                bool clean = GC.CollectionCount(0) == __state.Gc && !__state.SimActive && !PerfProbeRuntime.SimTickActive &&
                    PerfProbeRuntime.SimRunsCompleted == __state.SimRuns;
                if (clean)
                {
                    long grown = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - __state.MonoUsed;
                    if (grown > 0) sweepAlloc[id] += grown;
                    sweepCleanTicks[id] += elapsed;
                }
            }
        }

        private static long[] sweepAlloc = new long[0], sweepCleanTicks = new long[0];

        private static string FrameTop()
        {
            if (sweepTouchedThisFrame.Count == 0) return "";
            IEnumerable<string> top = sweepTouchedThisFrame.OrderByDescending(id => sweepFrameTicks[id]).Take(TopPerSpike)
                .Select(id => Short(sweepNames[id]) + "=" + Ms(sweepFrameTicks[id]).ToString("0.0", C));
            return " frameTop=" + string.Join(",", top);
        }

        private static void ResetFrame()
        {
            foreach (int id in sweepTouchedThisFrame) sweepFrameTicks[id] = 0;
            sweepTouchedThisFrame.Clear();
        }

        // ---------- per frame / spike context ----------

        private static int gcAtFrameStart = -1;
        private static long allocSum, allocMax, allocFrames, lastFrameMonoUsed;
        private static long mainAllocSum, mainAllocMax, mainAllocFrames, lastFrameMainAlloc;
        private static ProfilerRecorder gcAlloc;
        private static bool frameRecordersStarted;
        private static readonly FrameTiming[] timings = new FrameTiming[1];
        private static double cpuTimingSum, gpuTimingSum, cpuTimingMax, gpuTimingMax;
        private static int timingSamples;

        // Called from onBeforeRender once per frame, before the existing spike check.
        internal static string FrameContext()
        {
            var text = new StringBuilder();
            if (GcTracking && gcAtFrameStart >= 0)
            {
                int gc = GC.CollectionCount(0) - gcAtFrameStart;
                text.Append(" gcDuring=").Append(gc.ToString(C));
                long used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                text.Append(" monoUsedDeltaKB=").Append(((used - lastFrameMonoUsed) / 1024.0).ToString("F1", C));
                text.Append(" allocSource=").Append(gcAlloc.Valid ? "frameRecorder" : "heapDelta-not-allocation");
                if (gcAlloc.Valid) text.Append(" lastRecordedAllocKB=").Append((gcAlloc.LastValue / 1024.0).ToString("F1", C));
            }
            if (EngineCalls)
                text.Append(" engineFrameInclusive=").Append(string.Join(",", Enumerable.Range(0, engineFrameTicks.Length)
                    .Where(i => engineFrameTicks[i] > 0).OrderByDescending(i => engineFrameTicks[i]).Take(5)
                    .Select(i => engineNames[i] + ":" + Ms(engineFrameTicks[i]).ToString("F2", C))));
            if (UpdateSweep) text.Append(FrameTop());
            return text.ToString();
        }

        // Called after FrameContext; closes the finished frame and opens the next one.
        internal static void EndFrame()
        {
            if (!frameRecordersStarted) StartFrameRecorders();
            if (mainThreadId < 0) mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (GcTracking && ThreadAllocValid)
            {
                long mainNow = threadAllocated();
                if (lastFrameMainAlloc > 0)
                {
                    long frameAlloc = mainNow - lastFrameMainAlloc;
                    mainAllocSum += frameAlloc; mainAllocFrames++;
                    if (frameAlloc > mainAllocMax) mainAllocMax = frameAlloc;
                }
                lastFrameMainAlloc = mainNow;
            }
            if (GcTracking)
            {
                int gc = GC.CollectionCount(0);
                long used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                long value = -1;
                if (gcAlloc.Valid) value = gcAlloc.LastValue;
                // Fallback: Mono heap growth across the frame (the recorder is unavailable in this player).
                else if (lastFrameMonoUsed > 0 && gc == gcAtFrameStart && used >= lastFrameMonoUsed) value = used - lastFrameMonoUsed;
                if (value >= 0)
                {
                    allocSum += value; allocFrames++;
                    if (value > allocMax) allocMax = value;
                }
                gcAtFrameStart = gc;
                lastFrameMonoUsed = used;
            }
            if (EngineMarkers)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    double cpu = timings[0].cpuFrameTime, gpu = timings[0].gpuFrameTime;
                    cpuTimingSum += cpu; gpuTimingSum += gpu; timingSamples++;
                    if (cpu > cpuTimingMax) cpuTimingMax = cpu;
                    if (gpu > gpuTimingMax) gpuTimingMax = gpu;
                }
            }
            if (UpdateSweep) ResetFrame();
            Array.Clear(engineFrameTicks, 0, engineFrameTicks.Length);
        }

        private static void StartFrameRecorders()
        {
            frameRecordersStarted = true;
            try { if (GcTracking) gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); }
            catch (Exception exception) { Shared.DebugLogHelper.LogWarning(log, "PERFPROBE GC recorder unavailable: " + exception.Message); }
        }

        // ---------- snapshot (every 5 s while a map runs) ----------

        private static bool discoveryDone, environmentLogged;
        private static int lastScreenWidth, lastScreenHeight, gc0Window = -1, gc1Window, gc2Window;
        private static readonly List<KeyValuePair<string, ProfilerRecorder>> markerRecorders = new List<KeyValuePair<string, ProfilerRecorder>>();
        private static readonly List<KeyValuePair<string, ProfilerRecorder>> counterRecorders = new List<KeyValuePair<string, ProfilerRecorder>>();
        private static long lastSnapshotStamp;
        private static long lastDiagnosticSummary;

        // GC/engine summaries are essential to spike analysis even when Zoom snapshots are off.
        internal static void DiagnosticHeartbeat(long now)
        {
            if (lastDiagnosticSummary == 0) { lastDiagnosticSummary = now; return; }
            if (now - lastDiagnosticSummary < Stopwatch.Frequency * 5) return;
            double seconds = (now - lastDiagnosticSummary) / (double)Stopwatch.Frequency;
            lastDiagnosticSummary = now;
            EmitDiagnosticSummary(seconds, now);
        }

        // Keep captured summary arguments out of the per-frame heartbeat: C# allocates a
        // display class on method entry, even when an early return precedes the lambda.
        private static void EmitDiagnosticSummary(double seconds, long now)
        {
            if (GcTracking) Guard("gc", LogGc);
            if (EngineCalls) Guard("engineCalls", () => LogEngineCalls(seconds));
            Shared.DevDiagRuntime.Add("probe.summary", 1, Stopwatch.GetTimestamp() - now);
        }

        internal static void Snapshot()
        {
            long now = Stopwatch.GetTimestamp();
            double windowSeconds = lastSnapshotStamp == 0 ? 5.0 : (now - lastSnapshotStamp) / (double)Stopwatch.Frequency;
            lastSnapshotStamp = now;
            Guard("environment", LogEnvironmentIfChanged);
            if (EngineMarkers && !discoveryDone) Guard("discovery", DiscoverMarkers);
            if (EngineMarkers) Guard("markers", () => LogMarkers());
            if (BatchCensus) Guard("census", LogCensus);
            if (RecordBreakdown) Guard("records", LogRecordTypes);
            if (RecordBreakdown && chimpsField != null) Guard("chimps", LogChimps);
            if (TextureInventory && !sheetOccupancyDone) Guard("sheets", LogSheetOccupancy);
            if (UpdateSweep) Guard("sweep", () => LogSweep(windowSeconds));
        }

        // Instantaneous counts at the snapshot frame: lerpChimps visits every entry each frame,
        // interpChimp only does work while interp is set.
        private static void LogChimps()
        {
            if (!(chimpsField.GetValue(GameMap.instance) is Dictionary<int, Chimp> chimps)) return;
            int interpolating = 0, withRenderer = 0;
            foreach (KeyValuePair<int, Chimp> entry in chimps)
            {
                Chimp chimp = entry.Value;
                if (chimp == null) continue;
                if (chimp.interp) interpolating++;
                if (chimp.sprRenderer != null && chimp.sprRenderer.isVisible) withRenderer++;
            }
            string message = string.Format(C, "chimps total={0} interpolating={1} bodyVisible={2}", chimps.Count, interpolating, withRenderer);
            Shared.DevDiag.Log(() => message);
        }

        private static readonly HashSet<string> failedSections = new HashSet<string>();

        private static void Guard(string section, Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                if (failedSections.Add(section))
                    Shared.DebugLogHelper.LogError(log, "PERFPROBE " + section + " failed (logged once): " + exception);
            }
        }

        private static void LogEnvironmentIfChanged()
        {
            if (environmentLogged && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height) return;
            environmentLogged = true; lastScreenWidth = Screen.width; lastScreenHeight = Screen.height;
            var text = new StringBuilder("environment ");
            text.Append("gpu=").Append(SystemInfo.graphicsDeviceName).Append(" api=").Append(SystemInfo.graphicsDeviceType)
                .Append(" gfxMultiThreaded=").Append(SystemInfo.graphicsMultiThreaded)
                .Append(" cpu=").Append(SystemInfo.processorType).Append(" cores=").Append(SystemInfo.processorCount)
                .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height).Append(' ').Append(Screen.fullScreenMode)
                .Append(" refresh=").Append(Screen.currentResolution.refreshRate)
                .Append(" quality=").Append(QualitySettings.names[QualitySettings.GetQualityLevel()])
                .Append(" aa=").Append(QualitySettings.antiAliasing);
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            text.Append(" pipeline=").Append(pipeline != null ? pipeline.GetType().Name + "/" + pipeline.name : "builtin");
            if (pipeline != null)
                foreach (string property in new[] { "renderScale", "msaaSampleCount", "supportsHDR", "useSRPBatcher", "supportsDynamicBatching" })
                {
                    PropertyInfo info = pipeline.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
                    if (info != null) text.Append(' ').Append(property).Append('=').Append(Convert.ToString(info.GetValue(pipeline), C));
                }
            Camera[] cameras = Camera.allCameras;
            text.Append(" cameras=").Append(cameras.Length);
            foreach (Camera camera in cameras)
                text.Append(" [").Append(camera.name).Append(" ortho=").Append(camera.orthographic)
                    .Append(" size=").Append(camera.orthographicSize.ToString("0.##", C)).Append(" depth=").Append(camera.depth)
                    .Append(" mask=0x").Append(camera.cullingMask.ToString("X", C)).Append(" rt=").Append(camera.targetTexture != null).Append(']');
            Type light2D = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEngine.Rendering.Universal.Light2D", false))
                .FirstOrDefault(t => t != null);
            text.Append(" light2D=").Append(light2D != null ? UnityEngine.Object.FindObjectsOfType(light2D).Length.ToString(C) : "type-missing");
            string message = text.ToString();
            Shared.DevDiag.Log(() => message);
        }

        private static readonly string[] markerFilter = { "Gfx", "Render", "Cull", "Batch", "Sort", "Wait", "Tilemap", "Sprite", "2D", "Light", "Noesis", "GC", "Present", "PlayerLoop", "Camera", "Draw", "Frame" };

        private static void DiscoverMarkers()
        {
            discoveryDone = true;
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var matches = new List<ProfilerRecorderDescription>();
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                string name = description.Name ?? "";
                if (markerFilter.Any(f => name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)) matches.Add(description);
            }
            int total = handles.Count;
            Shared.DevDiag.Log(() => "markers available=" + total.ToString(C) + " matchingFilter=" + matches.Count.ToString(C));
            // Chunked so a single log line stays readable.
            for (int start = 0; start < matches.Count; start += 40)
            {
                string chunk = string.Join("; ", matches.Skip(start).Take(40)
                    .Select(d => d.Category.Name + "/" + d.Name + "(" + d.UnitType + ")"));
                Shared.DevDiag.Log(() => "markers list: " + chunk);
            }
            foreach (ProfilerRecorderDescription description in matches)
            {
                bool timing = description.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
                List<KeyValuePair<string, ProfilerRecorder>> target = timing ? markerRecorders : counterRecorders;
                if (target.Count >= MaxMarkerRecorders) continue;
                ProfilerRecorder recorder = ProfilerRecorder.StartNew(description.Category, description.Name, 32);
                if (recorder.Valid) target.Add(new KeyValuePair<string, ProfilerRecorder>(description.Name, recorder));
            }
            int timingCount = markerRecorders.Count, counterCount = counterRecorders.Count;
            Shared.DevDiag.Log(() => "markers recording timing=" + timingCount.ToString(C) + " counters=" + counterCount.ToString(C));
        }

        private static void LogMarkers()
        {
            var entries = new List<KeyValuePair<string, double>>();
            foreach (KeyValuePair<string, ProfilerRecorder> entry in markerRecorders)
            {
                ProfilerRecorder recorder = entry.Value;
                if (!recorder.Valid || recorder.Count == 0) continue;
                double sum = 0;
                for (int index = 0; index < recorder.Count; index++) sum += recorder.GetSample(index).Value;
                double ms = sum / recorder.Count / 1e6;
                if (ms >= 0.01) entries.Add(new KeyValuePair<string, double>(entry.Key, ms));
            }
            string timing = string.Join(" | ", entries.OrderByDescending(e => e.Value).Select(e => e.Key + "=" + e.Value.ToString("0.00", C)));
            string counters = string.Join(" | ", counterRecorders.Where(e => e.Value.Valid && e.Value.LastValue != 0)
                .Select(e => e.Key + "=" + e.Value.LastValue.ToString(C)));
            string frameTiming = timingSamples == 0 ? "unavailable" : string.Format(C, "cpuAvg={0:0.00} cpuMax={1:0.00} gpuAvg={2:0.00} gpuMax={3:0.00} samples={4}",
                cpuTimingSum / timingSamples, cpuTimingMax, gpuTimingSum / timingSamples, gpuTimingMax, timingSamples);
            cpuTimingSum = gpuTimingSum = cpuTimingMax = gpuTimingMax = 0; timingSamples = 0;
            Shared.DevDiag.Log(() => "engine frameTimingMs " + frameTiming + " || markerMsPerFrame " + (timing.Length == 0 ? "none" : timing) +
                " || counters " + (counters.Length == 0 ? "none" : counters));
        }

        // Kind 0 = SpriteRenderer, 1 = TilemapRenderer (always its own batch; texture unknown per chunk).
        private struct RendererKey { public int Kind, Layer, Order, Material, MaterialClass, Texture; public float Z; }

        private static readonly Dictionary<int, int> materialClassIds = new Dictionary<int, int>();
        private static readonly Dictionary<string, int> materialClassKeys = new Dictionary<string, int>();
        private static readonly HashSet<int> inventoryLogged = new HashSet<int>();

        // Material class = what would remain if all sheets and masks shared one atlas page:
        // shader + _SpriteCutoff (TeamColour has 7 cutoff variants, Foliage/Plain have none).
        private static int MaterialClass(Material material)
        {
            if (material == null) return 0;
            int id = material.GetInstanceID();
            if (materialClassIds.TryGetValue(id, out int known)) return known;
            string shader = material.shader != null ? material.shader.name : "null";
            string key = shader + (material.HasProperty("_SpriteCutoff") ? "|" + material.GetFloat("_SpriteCutoff").ToString("0.###", C) : "");
            if (!materialClassKeys.TryGetValue(key, out int classId)) materialClassKeys[key] = classId = materialClassKeys.Count + 1;
            materialClassIds[id] = classId;
            return classId;
        }

        private static void LogCensus()
        {
            long started = Stopwatch.GetTimestamp();
            SpriteRenderer[] sprites = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            TilemapRenderer[] tilemaps = UnityEngine.Object.FindObjectsOfType<TilemapRenderer>();
            var visible = new List<RendererKey>(sprites.Length + tilemaps.Length);
            var materials = new HashSet<int>(); var classes = new HashSet<int>(); var shaders = new HashSet<int>();
            var spriteTextures = new HashSet<int>(); var orders = new HashSet<long>();
            var newTextures = new List<string>();
            int enabledSprites = 0, visibleSprites = 0, visibleTilemaps = 0, nullSprites = 0;
            foreach (SpriteRenderer renderer in sprites)
            {
                if (!renderer.enabled) continue;
                enabledSprites++;
                if (!renderer.isVisible) continue;
                visibleSprites++;
                Material material = renderer.sharedMaterial;
                Sprite sprite = renderer.sprite;
                if (sprite == null) nullSprites++;
                Texture2D texture = sprite != null ? sprite.texture : null;
                int textureId = texture != null ? texture.GetInstanceID() : 0;
                if (texture != null)
                {
                    spriteTextures.Add(textureId);
                    // Sheet weight in 8192² units: a 16384² page holds weight 4.
                    if (!sheetWeights.ContainsKey(textureId))
                    {
                        sheetWeights[textureId] = texture.width * (float)texture.height / (8192f * 8192f);
                        sheetNames[textureId] = texture.name;
                    }
                }
                if (TextureInventory) Inventory(texture, material, newTextures);
                Add(renderer, 0, material, textureId);
                // Static = identical sprite, material, order, world position, colour and flip as 5 s ago.
                int rendererId = renderer.GetInstanceID();
                long signature = Signature(renderer);
                currentSignatures[rendererId] = signature;
                if (previousSignatures.TryGetValue(rendererId, out long before))
                {
                    int cls = MaterialClass(material);
                    staticCompared++;
                    staticComparedByClass.TryGetValue(cls, out int compared);
                    staticComparedByClass[cls] = compared + 1;
                    if (before == signature)
                    {
                        staticUnchanged++;
                        staticUnchangedByClass.TryGetValue(cls, out int unchanged);
                        staticUnchangedByClass[cls] = unchanged + 1;
                    }
                }
            }
            foreach (TilemapRenderer renderer in tilemaps)
            {
                if (!renderer.enabled || !renderer.isVisible) continue;
                visibleTilemaps++;
                Add(renderer, 1, renderer.sharedMaterial, 0);
            }
            void Add(Renderer renderer, int kind, Material material, int textureId)
            {
                int materialId = material != null ? material.GetInstanceID() : 0;
                materials.Add(materialId);
                int materialClass = MaterialClass(material);
                classes.Add(materialClass);
                if (material != null && material.shader != null) shaders.Add(material.shader.GetInstanceID());
                int layer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                orders.Add(((long)layer << 32) | (uint)renderer.sortingOrder);
                visible.Add(new RendererKey
                {
                    Kind = kind, Layer = layer, Order = renderer.sortingOrder, Material = materialId,
                    MaterialClass = materialClass, Texture = textureId, Z = renderer.transform.position.z
                });
            }
            // Approximation of Unity's transparent 2D order: sorting layer, order, then depth.
            visible.Sort((a, b) => a.Layer != b.Layer ? a.Layer.CompareTo(b.Layer) : a.Order != b.Order ? a.Order.CompareTo(b.Order) : b.Z.CompareTo(a.Z));
            // Break causes between sorted neighbours (first renderer starts a batch).
            int tilemapBreaks = 0, classBreaks = 0, sameClassMaterialBreaks = 0, textureBreaks = 0;
            for (int index = 1; index < visible.Count; index++)
            {
                RendererKey previous = visible[index - 1], current = visible[index];
                if (current.Kind == 1 || previous.Kind == 1) tilemapBreaks++;
                else if (current.MaterialClass != previous.MaterialClass) classBreaks++;
                else if (current.Material != previous.Material) sameClassMaterialBreaks++;
                else if (current.Texture != previous.Texture) textureBreaks++;
            }
            int first = visible.Count == 0 ? 0 : 1;
            int now = first + tilemapBreaks + classBreaks + sameClassMaterialBreaks + textureBreaks;
            // Atlas scenarios (estimates): A = textures sharing a material packed together (plain sheets);
            // B = additionally all sheets/masks of one material class on one page (TeamColour/Foliage).
            int atlasPerMaterial = now - textureBreaks;
            int atlasPerClass = atlasPerMaterial - sameClassMaterialBreaks;
            string pageSimulation = SimulatePages(visible, first + tilemapBreaks + classBreaks + textureBreaks);
            double censusMs = Ms(Stopwatch.GetTimestamp() - started);
            string message = string.Format(C,
                "census spritesActive={0} spritesEnabled={1} spritesVisible={2} nullSprite={3} tilemapsActive={4} tilemapsVisible={5} " +
                "distinctMaterials={6} materialClasses={7} distinctShaders={8} spriteTextures={9} distinctSortOrders={10} " +
                "estBatches={11} breaks(tilemap={12} materialClass={13} sameClassMaterial={14} texture={15}) " +
                "estBatchesIfAtlasPerMaterial={16} estBatchesIfAtlasPerClass={17} censusMs={18:0.0}",
                sprites.Length, enabledSprites, visibleSprites, nullSprites, tilemaps.Length, visibleTilemaps,
                materials.Count, classes.Count, shaders.Count, spriteTextures.Count, orders.Count, now,
                tilemapBreaks, classBreaks, sameClassMaterialBreaks, textureBreaks, atlasPerMaterial, atlasPerClass, censusMs);
            Shared.DevDiag.Log(() => message);
            if (pageSimulation != null) Shared.DevDiag.Log(() => pageSimulation);
            string byClass = string.Join(" ", staticComparedByClass.OrderBy(e => e.Key).Select(e => string.Format(C, "class{0}={1}/{2}",
                e.Key, staticUnchangedByClass.TryGetValue(e.Key, out int unchanged) ? unchanged : 0, e.Value)));
            string staticMessage = string.Format(C, "static visibleBoth={0} unchanged5s={1} share={2:0.0}% byClass(unchanged/compared) {3}",
                staticCompared, staticUnchanged, staticCompared == 0 ? 0 : 100.0 * staticUnchanged / staticCompared, byClass);
            Shared.DevDiag.Log(() => staticMessage);
            previousSignatures = currentSignatures;
            currentSignatures = new Dictionary<int, long>(previousSignatures.Count);
            staticCompared = staticUnchanged = 0;
            staticComparedByClass.Clear(); staticUnchangedByClass.Clear();
            if (newTextures.Count > 0)
            {
                for (int start = 0; start < newTextures.Count; start += 12)
                {
                    string chunk = string.Join(" | ", newTextures.Skip(start).Take(12));
                    Shared.DevDiag.Log(() => "textures new: " + chunk);
                }
                string classesText = string.Join(" | ", materialClassKeys.OrderBy(e => e.Value).Select(e => e.Value.ToString(C) + "=" + e.Key));
                Shared.DevDiag.Log(() => "materialClasses " + classesText);
            }
        }

        private static readonly Dictionary<int, float> sheetWeights = new Dictionary<int, float>();
        private static readonly Dictionary<int, string> sheetNames = new Dictionary<int, string>();
        // Adjacency of different sheets of one mask class in sort order, accumulated over the whole run.
        private static readonly Dictionary<long, long> sheetAdjacency = new Dictionary<long, long>();
        private const float PageCapacity = 4f;
        private static readonly HashSet<int> maskSheets = new HashSet<int>();

        private static bool IsMaskClass(int classId) => materialClassKeys.Any(e => e.Value == classId &&
            (e.Key.StartsWith("Unlit/TeamColour", StringComparison.Ordinal) || e.Key.StartsWith("Unlit/Foliage", StringComparison.Ordinal)));

        // V7 scenario: the sheets of a mask class (each with its own _TeamMask) are merged as whole
        // sheets onto 16384² pages (capacity 4 × 8192²). Sheets that most often follow each other in
        // sort order share a page (greedy, from the accumulated adjacency). The result counts the
        // batch breaks that remain; non-sheet causes (tilemap, class, plain texture) are passed in.
        private static string SimulatePages(List<RendererKey> sorted, int otherBreaks)
        {
            var maskClasses = new HashSet<int>(sorted.Where(k => k.Kind == 0).Select(k => k.MaterialClass).Distinct().Where(IsMaskClass));
            if (maskClasses.Count == 0) return null;
            for (int index = 1; index < sorted.Count; index++)
            {
                RendererKey previous = sorted[index - 1], current = sorted[index];
                if (previous.Kind != 0 || current.Kind != 0 || previous.MaterialClass != current.MaterialClass ||
                    !maskClasses.Contains(current.MaterialClass) || previous.Texture == current.Texture || previous.Texture == 0 || current.Texture == 0) continue;
                long key = PairKey(previous.Texture, current.Texture);
                sheetAdjacency.TryGetValue(key, out long count);
                sheetAdjacency[key] = count + 1;
            }
            // Greedy grouping by adjacency with page capacity.
            var group = new Dictionary<int, int>();
            var groupWeight = new Dictionary<int, float>();
            foreach (KeyValuePair<int, float> sheet in sheetWeights) { group[sheet.Key] = sheet.Key; groupWeight[sheet.Key] = sheet.Value; }
            foreach (KeyValuePair<long, long> pair in sheetAdjacency.OrderByDescending(e => e.Value))
            {
                int a = (int)(pair.Key >> 32), b = (int)(pair.Key & 0xFFFFFFFF);
                if (!group.ContainsKey(a) || !group.ContainsKey(b)) continue;
                int ga = group[a], gb = group[b];
                if (ga == gb || groupWeight[ga] + groupWeight[gb] > PageCapacity) continue;
                foreach (int sheet in group.Keys.Where(k => group[k] == gb).ToList()) group[sheet] = ga;
                groupWeight[ga] += groupWeight[gb];
                groupWeight.Remove(gb);
            }
            int GroupOf(int texture) => group.TryGetValue(texture, out int g) ? g : texture;
            int pageBreaks = 0, otherSameClass = 0;
            for (int index = 1; index < sorted.Count; index++)
            {
                RendererKey previous = sorted[index - 1], current = sorted[index];
                if (previous.Kind != 0 || current.Kind != 0 || previous.MaterialClass != current.MaterialClass) continue;
                if (previous.Material == current.Material) continue;
                if (!maskClasses.Contains(current.MaterialClass)) { otherSameClass++; continue; }
                // Same mask class: a break remains only if the two sheets end up on different pages.
                if (GroupOf(previous.Texture) != GroupOf(current.Texture)) pageBreaks++;
                maskSheets.Add(previous.Texture); maskSheets.Add(current.Texture);
            }
            // Plain material changes (e.g. Vanilla's defaultMaterial instance) stay unchanged.
            int estimate = otherBreaks + otherSameClass + pageBreaks;
            foreach (RendererKey key in sorted) if (key.Kind == 0 && maskClasses.Contains(key.MaterialClass) && key.Texture != 0) maskSheets.Add(key.Texture);
            string pages = string.Join(" ; ", groupWeight.Keys.Where(g => group.Any(e => e.Value == g && maskSheets.Contains(e.Key)))
                .Select(g => string.Join("+", group.Where(e => e.Value == g)
                .Select(e => sheetNames.TryGetValue(e.Key, out string n) ? n : e.Key.ToString(C))) + "(" + groupWeight[g].ToString("0.#", C) + ")"));
            int pageCount = groupWeight.Keys.Count(g => group.Any(e => e.Value == g && maskSheets.Contains(e.Key)));
            return string.Format(C, "pageSim estBatchesIf16kPages={0} sheetBreaksRemaining={1} plainMaterialBreaks={2} maskPages={3}: {4}",
                estimate, pageBreaks, otherSameClass, pageCount, pages);
        }

        private static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        // Logs each sprite texture and team mask once: identity, size, format, filter, mips, readability.
        private static void Inventory(Texture2D texture, Material material, List<string> output)
        {
            if (texture != null && inventoryLogged.Add(texture.GetInstanceID()))
                output.Add(Describe("sprite", texture) + " material=" + (material != null ? material.name + "/" + (material.shader != null ? material.shader.name : "null") : "null"));
            if (material != null && material.HasProperty("_TeamMask") && material.GetTexture("_TeamMask") is Texture2D mask &&
                inventoryLogged.Add(mask.GetInstanceID()))
                output.Add(Describe("mask", mask));
        }

        private static string Describe(string role, Texture2D texture) => string.Format(C,
            "{0}:{1} {2}x{3} {4} filter={5} mips={6} readable={7} MB={8:0.0}", role, texture.name, texture.width, texture.height,
            texture.format, texture.filterMode, texture.mipmapCount, texture.isReadable,
            UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture) / 1048576.0);

        private static void LogGc()
        {
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            if (gc0Window < 0) { gc0Window = gc0; gc1Window = gc1; gc2Window = gc2; }
            string alloc = allocFrames == 0 ? "allocPerFrame=unavailable" : string.Format(C, "allocPerFrameAvgKB={0:0.0} allocPerFrameMaxKB={1:0.0}",
                allocSum / (double)allocFrames / 1024.0, allocMax / 1024.0);
            string message = string.Format(C, "gc gen0={0} gen1={1} gen2={2} heapMB={3:0.0} {4} allocSource={5} threadAllocValid={6}",
                gc0 - gc0Window, gc1 - gc1Window, gc2 - gc2Window,
                GC.GetTotalMemory(false) / 1048576.0, alloc, gcAlloc.Valid ? "frameRecorder" : "heapGrowthLowerBound", ThreadAllocValid);
            gc0Window = gc0; gc1Window = gc1; gc2Window = gc2; allocSum = allocMax = allocFrames = 0;
            Shared.DevDiag.Log(() => message);
            if (!ThreadAllocValid) return;
            // Thread-exact split: main thread per frame, sim thread per EngineInterface.run.
            long simBytes = System.Threading.Interlocked.Exchange(ref simAllocBytes, 0);
            long simTicks = System.Threading.Interlocked.Exchange(ref simAllocTicks, 0);
            long simMax = System.Threading.Interlocked.Exchange(ref simAllocMax, 0);
            string threads = string.Format(C,
                "allocThreads mainPerFrameAvgKB={0:0.0} mainPerFrameMaxKB={1:0.0} mainFrames={2} simPerTickAvgKB={3:0.0} simPerTickMaxKB={4:0.0} simTicks={5} simTotalKB={6:0}",
                mainAllocFrames == 0 ? 0 : mainAllocSum / (double)mainAllocFrames / 1024.0, mainAllocMax / 1024.0, mainAllocFrames,
                simTicks == 0 ? 0 : simBytes / (double)simTicks / 1024.0, simMax / 1024.0, simTicks, simBytes / 1024.0);
            mainAllocSum = mainAllocMax = mainAllocFrames = 0;
            Shared.DevDiag.Log(() => threads);
        }

        private static void LogRecordTypes()
        {
            var parts = new List<string>();
            for (int type = 0; type <= RecordTypes; type++)
            {
                if (recordTypeCounts[type] == 0) continue;
                parts.Add((type == RecordTypes ? "other" : type.ToString(C)) + "=" + recordTypeCounts[type].ToString(C));
                recordTypeCounts[type] = 0;
            }
            string message = "records byType(window) " + (parts.Count == 0 ? "none" : string.Join(" ", parts));
            Shared.DevDiag.Log(() => message);
        }

        private static void LogSweep(double windowSeconds)
        {
            var ids = new List<int>();
            for (int id = 0; id < sweepCalls.Length; id++) if (sweepCalls[id] > 0) ids.Add(id);
            double totalMs = ids.Sum(id => Ms(sweepTicks[id]));
            string top = string.Join(" | ", ids.OrderByDescending(id => sweepTicks[id]).Take(TopPerWindow).Select(id => string.Format(C,
                "{0} msPerS={1:0.00} calls/s={2:0} maxUs={3:0}", Short(sweepNames[id]), Ms(sweepTicks[id]) / windowSeconds,
                sweepCalls[id] / windowSeconds, Ms(sweepMax[id]) * 1000.0)));
            // Heap-growth mode only counts clean samples (no GC, no overlapping sim tick); the rate is
            // extrapolated from the clean share of the callback's own run time.
            double Estimated(int id) => ThreadAllocValid || sweepCleanTicks[id] == 0 ? sweepAlloc[id]
                : sweepAlloc[id] * (double)sweepTicks[id] / sweepCleanTicks[id];
            double allocTotal = ids.Sum(id => Estimated(id));
            string allocTop = string.Join(" | ", ids.Where(id => sweepAlloc[id] > 0).OrderByDescending(id => Estimated(id)).Take(TopPerWindow)
                .Select(id => string.Format(C, "{0} KBperS={1:0.0} cleanShare={2:0}%", Short(sweepNames[id]), Estimated(id) / 1024.0 / windowSeconds,
                    sweepTicks[id] == 0 ? 0 : 100.0 * sweepCleanTicks[id] / sweepTicks[id])));
            foreach (int id in ids) { sweepCalls[id] = 0; sweepTicks[id] = 0; sweepMax[id] = 0; sweepAlloc[id] = 0; sweepCleanTicks[id] = 0; }
            string message = string.Format(C, "sweep active={0} totalMsPerS={1:0.00} top: {2}", ids.Count, totalMs / windowSeconds, top.Length == 0 ? "none" : top);
            Shared.DevDiag.Log(() => message);
            if (GcTracking)
            {
                string allocMessage = string.Format(C, "sweepAlloc source={0} totalKBperS={1:0.0} (inclusive incl. nested probes) top: {2}",
                    ThreadAllocValid ? "thread" : "heapGrowth", allocTotal / 1024.0 / windowSeconds, allocTop.Length == 0 ? "none" : allocTop);
                Shared.DevDiag.Log(() => allocMessage);
            }
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private static string Short(string fullName)
        {
            int dot = fullName.LastIndexOf('.', fullName.Length - 1);
            int previous = dot > 0 ? fullName.LastIndexOf('.', dot - 1) : -1;
            return previous >= 0 ? fullName.Substring(previous + 1) : fullName;
        }
    }
}

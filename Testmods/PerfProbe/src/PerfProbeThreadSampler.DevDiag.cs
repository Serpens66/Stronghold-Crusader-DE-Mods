using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Configuration;

namespace PerfProbe
{
    // Attribution profiler for managed mod code (no hook, no patch): a background thread suspends
    // the Unity main thread and (while EngineInterface.run is active) the simulation thread at a
    // fixed cadence and records raw instruction pointers of the call stack. Frames with unwind data
    // (game DLL, Unity, Mono, system modules) are unwound with RtlVirtualUnwind; JIT code has no
    // registered unwind data here, so it is followed through the rbp frame chain with fault-safe
    // reads (ReadProcessMemory) bounded by the thread's stack. Nothing is resolved, allocated or
    // logged while a thread is suspended. After resuming, addresses are mapped to:
    //   CDE:0x<function RVA> (CrusaderDE.dll), <module> (other images), [Assembly]Namespace.Class:Method (Mono JIT)
    // and aggregated per window:
    //   threadSample thread=<main|sim> samples=<n> self=<top leaf labels>
    //   threadSample thread=<..> asm=<assembly:inclusive samples>   (sample counted once per assembly)
    //   threadSample thread=<..> incl=<top managed methods, inclusive>
    //   threadSample thread=<..> entry=<outermost method of each mod assembly, inclusive>
    internal static partial class PerfProbeThreadSampler
    {
        private const int MaxFrames = 40;
        private const uint ContextFull = 0x10000B;
        private const int ContextSize = 1232, OffFlags = 0x30, OffRsp = 0x98, OffRbp = 0xA0, OffRip = 0xF8;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        // Assemblies that are not mods; their methods appear in incl/self but not in entry.
        private static readonly string[] HostAssemblies = { "mscorlib", "System", "UnityEngine", "Unity.RenderPipelines.", "Assembly-CSharp", "Noesis", "SHCDESE", "R3", "0Harmony", "BepInEx", "Mono.", "netstandard", "RedBird", "Microsoft." };

        private static bool enabled, sampleMain = true, sampleSim = true;
        internal static bool IsEnabled => enabled;
        private static bool longRunOnly;
        private static PerfProbeCadence cadence;
        internal static void InstallLongRunSampler()
        {
            if (worker != null) return;
            enabled = true; sampleMain = false; sampleSim = true; longRunOnly = true;
            Install();
        }
        internal static string Configuration => "enabled:" + enabled + ",intervalUs:" + intervalUs + ",windowS:" + windowSeconds + ",main:" + sampleMain + ",sim:" + sampleSim + ",longRunOnly:" + longRunOnly;
        private static int intervalUs = 1000, windowSeconds = 2;
        private static int mainThreadId;
        private static ulong gameBase, gameEnd;
        private static Thread worker, analysisWorker;
        private static readonly AutoResetEvent analysisReady = new AutoResetEvent(false);
        private static PerfProbeAnalysisJob[] analysisJobs;
        private static long jobSequence, suspendedTicks, maxSuspendedTicks;
        private static int windowDrops;

        private sealed class Lane
        {
            internal string Name;
            internal int ThreadId;
            internal IntPtr Handle;
            internal ulong StackBase;
            internal ulong[] Frames;

            internal PerfProbeSampleHistory History;
        }

        private static Lane main, sim;
        private static long imagesRefreshed;
        private static bool moduleErrorLogged;
        private static int capacity;

        internal static void BindConfig(ConfigFile config)
        {
            enabled = config.Bind("Sampler", "Enabled", false,
                "Sampling profiler: attribute main/simulation thread time to managed methods and mod assemblies (adds some overhead).").Value;
            intervalUs = Math.Max(200, config.Bind("Sampler", "IntervalUs", 1000, "Sampling interval per thread in microseconds.").Value);
            windowSeconds = Math.Max(2, config.Bind("Sampler", "WindowSeconds", 2, "Aggregation window in seconds.").Value);
            sampleMain = config.Bind("Sampler", "MainThread", true, "Sample the Unity main thread.").Value;
            sampleSim = config.Bind("Sampler", "SimThread", true, "Sample the simulation thread while EngineInterface.run is active.").Value;
            // Awake runs on the Unity main thread: sampling starts here so plugin startup is covered too.
            if (enabled) mainThreadId = (int)GetCurrentThreadId();
        }

        private static void ResolveGameImage()
        {
            if (gameBase != 0) return;
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            if (module == IntPtr.Zero) return;
            int peOffset = Marshal.ReadInt32(module, 0x3C);
            gameEnd = (ulong)module.ToInt64() + (uint)Marshal.ReadInt32(module, peOffset + 0x18 + 0x38);
            gameBase = (ulong)module.ToInt64();
        }

        // Started from Awake (before the extender library is ready); DevDiag output appears once attached.
        internal static void Install()
        {
            if (!enabled || worker != null) return;
            if (!MonoNames.Init(out string monoError)) { Shared.DevDiag.Log(() => "threadSampler disabled: " + monoError); return; }
            ResolveGameImage();
            try { RefreshImages(); }
            catch (Exception exception) { string detail = exception.ToString(); Shared.DevDiag.Log(() => "threadSampler disabled: module list " + detail); return; }
            capacity = windowSeconds * 1000000 / intervalUs + 64;
            main = NewLane("main");
            sim = NewLane("sim");
            worker = new Thread(Loop) { IsBackground = true, Name = "PerfProbe.ThreadSampler", Priority = ThreadPriority.Normal };
            analysisJobs = new[] { new PerfProbeAnalysisJob(false, capacity, MaxFrames), new PerfProbeAnalysisJob(false, capacity, MaxFrames),
                new PerfProbeAnalysisJob(true, Math.Max(capacity, 8000000 / intervalUs), MaxFrames), new PerfProbeAnalysisJob(true, Math.Max(capacity, 8000000 / intervalUs), MaxFrames) };
            analysisWorker = new Thread(AnalysisLoop) { IsBackground = true, Name = "PerfProbe.Analysis", Priority = ThreadPriority.BelowNormal };
            analysisWorker.Start();
            worker.Start();
            Shared.DevDiag.Log(() => string.Format(C, "threadSampler ready intervalUs={0} windowS={1} main={2} sim={3}", intervalUs, windowSeconds, sampleMain, sampleSim));
        }

        private static Lane NewLane(string name) => new Lane
        {
            Name = name, Frames = new ulong[MaxFrames],
            History = new PerfProbeSampleHistory(Math.Max(capacity, 8000000 / intervalUs), MaxFrames)
        };

        // Called from the render callback (Unity main thread).
        internal static void NoteMainThread()
        {
            if (enabled && mainThreadId == 0) mainThreadId = (int)GetCurrentThreadId();
        }

        private static void Loop()
        {
            IntPtr raw = Marshal.AllocHGlobal(ContextSize + 16);
            IntPtr context = new IntPtr((raw.ToInt64() + 15) & ~15L);
            IntPtr scratch = Marshal.AllocHGlobal(16);
            long windowStart = Stopwatch.GetTimestamp();
            captureStarted = windowStart; samplerCpu = PerfProbeLockTiming.ThreadCpuTime();
            long interval = Math.Max(1, Stopwatch.Frequency * intervalUs / 1000000);
            cadence = new PerfProbeCadence(interval);
            string initialMode = cadence.Mode;
            Shared.DevDiag.Log(() => "samplerCadence mode=" + initialMode + " intervalUs=" + intervalUs + " longRunOnly=" + longRunOnly);
            WarmUp(context, scratch);
            while (true)
            {
                try
                {
                    if (sampleMain && mainThreadId != 0) SampleLane(main, mainThreadId, context, scratch);
                    int simId = PerfProbeSimSampler.SimThreadId;
                    long simStarted = PerfProbeSimSampler.RunStart;
                    if (sampleSim && simId != 0 && simStarted != 0 &&
                        (!longRunOnly || Stopwatch.GetTimestamp() - simStarted >= Stopwatch.Frequency * 80 / 1000))
                        SampleLane(sim, simId, context, scratch);
                    long now = Stopwatch.GetTimestamp();
                    if (now - windowStart >= Stopwatch.Frequency * windowSeconds)
                    {
                        QueueWindow(windowStart, now);
                        windowStart = now;
                    }
                    DrainSpikeRequests();
                    cadence.Wait();
                }
                catch (Exception exception)
                {
                    cadence.Dispose(); Marshal.FreeHGlobal(scratch); Marshal.FreeHGlobal(raw);
                    Shared.DevDiag.Log(() => "threadSampler stopped: " + exception.GetType().Name + " " + exception.Message);
                    return;
                }
            }
        }

        // Startup hang (runs X1OFF, G1SE, PPFULL1, T1BFQ and a user start on 2026-10-23): the first
        // sample suspended the Unity main thread and then reached code that Mono still had to compile
        // or link (P/Invoke stubs of GetThreadContext/RtlVirtualUnwind/ResumeThread, Marshal helpers,
        // call-site trampolines). During plugin loading the main thread holds Mono's loader/JIT locks,
        // so the sampler waited for the suspended thread forever. The whole suspended-window path is
        // therefore run once against a parked helper thread, which holds no lock, before any real lane.
        private static void WarmUp(IntPtr context, IntPtr scratch)
        {
            int helperId = 0;
            using (var ready = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            {
                var helper = new Thread(() => { helperId = (int)GetCurrentThreadId(); ready.Set(); release.WaitOne(); })
                    { IsBackground = true, Name = "PerfProbe.SamplerWarmUp" };
                helper.Start();
                var lane = new Lane { Name = "warmup", Frames = new ulong[MaxFrames], History = new PerfProbeSampleHistory(4, MaxFrames) };
                try
                {
                    ready.WaitOne();
                    Thread.Sleep(20); // Let the helper enter its kernel wait.
                    for (int i = 0; i < 3; i++) SampleLane(lane, helperId, context, scratch);
                }
                finally
                {
                    if (lane.Handle != IntPtr.Zero) CloseThreadHandle(lane.Handle);
                    release.Set();
                    helper.Join(1000);
                }
            }
            Shared.DevDiag.Log(() => "threadSampler warm-up completed on a parked helper thread");
        }

        // Start deferred to the rendered frames after plugin startup (DeferUntilPatched): no plugin or
        // assembly loading on the main thread any more. Called by PerfProbe's render callback.
        internal static bool DeferStart;
        private static bool deferredStartAttempted;
        private const int DeferredStartFrame = 120;
        internal static void StartAfterStartup(int frame)
        {
            if (!DeferStart || deferredStartAttempted || frame < DeferredStartFrame) return;
            deferredStartAttempted = true;
            Install();
        }

        private static void SampleLane(Lane lane, int threadId, IntPtr context, IntPtr scratch)
        {
            if (lane.ThreadId != threadId)
            {
                if (lane.Handle != IntPtr.Zero) CloseThreadHandle(lane.Handle);
                lane.ThreadId = threadId;
                lane.Handle = OpenThread(0x0002 | 0x0008 | 0x0040, false, (uint)threadId);
                lane.StackBase = ReadStackBase(lane.Handle, scratch);
                if (lane.Handle == IntPtr.Zero || lane.StackBase == 0)
                {
                    Shared.DevDiag.Log(() => "threadSampler lane " + lane.Name + " unavailable (OpenThread/TEB)");
                    // Retain the attempted ID: retry only if the OS thread changes.
                    return;
                }
            }
            if (lane.ThreadId < 0 || lane.Handle == IntPtr.Zero || lane.StackBase == 0) return;
            int offset = 0;
            int depth = 0;
            long sampledAt = Stopwatch.GetTimestamp();
            if (SuspendThread(lane.Handle) == uint.MaxValue) return;
            long suspendedAt = Stopwatch.GetTimestamp();
            try
            {
                Marshal.WriteInt32(context, OffFlags, unchecked((int)ContextFull));
                if (!GetThreadContext(lane.Handle, context)) return;
                ulong stackBase = lane.StackBase;
                bool heuristic = false;
                for (int i = 0; i < MaxFrames; i++)
                {
                    ulong rip = (ulong)Marshal.ReadInt64(context, OffRip);
                    ulong rsp = (ulong)Marshal.ReadInt64(context, OffRsp);
                    if (rip == 0 || rsp == 0 || rsp >= stackBase) break;
                    lane.Frames[offset + depth++] = rip;
                    // Own lock-free .pdata lookup: RtlLookupFunctionEntry takes the loader's function
                    // table lock, which a suspended thread may hold during DLL loading (startup deadlock).
                    IntPtr entry = FindFunctionEntry(rip, out ulong moduleBase);
                    // After an rbp-chain step the register context is only partly reconstructed;
                    // RtlVirtualUnwind would read the stack through a wrong frame and fault. The walk
                    // ends at the first native frame above managed code (enough for attribution).
                    if (entry != IntPtr.Zero && heuristic) break;
                    if (entry != IntPtr.Zero)
                    {
                        RtlVirtualUnwind(0, moduleBase, rip, entry, context, out _, out _, IntPtr.Zero);
                        if ((ulong)Marshal.ReadInt64(context, OffRsp) <= rsp) break;
                        continue;
                    }
                    // JIT/stub code: follow the rbp chain (Mono x64 keeps rbp frames), fault-safe.
                    ulong rbp = (ulong)Marshal.ReadInt64(context, OffRbp);
                    if (rbp < rsp || rbp + 16 > stackBase) break;
                    if (!SafeRead(rbp, scratch, out ulong savedRbp) || !SafeRead(rbp + 8, scratch, out ulong returnAddress)) break;
                    Marshal.WriteInt64(context, OffRip, (long)returnAddress);
                    Marshal.WriteInt64(context, OffRsp, (long)(rbp + 16));
                    Marshal.WriteInt64(context, OffRbp, (long)savedRbp);
                    heuristic = true;
                }
            }
            finally
            {
                ResumeThread(lane.Handle);
                long duration = Stopwatch.GetTimestamp() - suspendedAt;
                suspendedTicks += duration; if (duration > maxSuspendedTicks) maxSuspendedTicks = duration;
            }
            if (depth > 0)
            {
                lane.History.Add(sampledAt, lane.Frames, offset, depth);
            }
        }

        private static bool SafeRead(ulong address, IntPtr buffer, out ulong value)
        {
            value = 0;
            if (!ReadProcessMemory(GetCurrentProcess(), new IntPtr((long)address), buffer, new IntPtr(8), out IntPtr read) || read.ToInt64() != 8) return false;
            value = (ulong)Marshal.ReadInt64(buffer);
            return true;
        }

        // Module ranges and their exception directories (.pdata) are refreshed outside any suspension
        // and published as one immutable snapshot.
        private sealed class ImageTable
        {
            internal ulong[] Starts, Ends, Pdata;
            internal int[] PdataCount;
            internal string[] Names;
        }

        private static volatile ImageTable images = new ImageTable { Starts = new ulong[0], Ends = new ulong[0], Pdata = new ulong[0], PdataCount = new int[0], Names = new string[0] };

        private static int FindImage(ulong address, ImageTable table)
        {
            for (int i = 0; i < table.Starts.Length; i++) if (address >= table.Starts[i] && address < table.Ends[i]) return i;
            return -1;
        }

        // Binary search over the image's RUNTIME_FUNCTION array (BeginAddress, EndAddress, UnwindData RVAs).
        private static IntPtr FindFunctionEntry(ulong address, out ulong imageBase)
        {
            imageBase = 0;
            ImageTable table = images;
            int image = FindImage(address, table);
            if (image < 0 || table.PdataCount[image] <= 0) return IntPtr.Zero;
            imageBase = table.Starts[image];
            uint rva = (uint)(address - imageBase);
            int low = 0, high = table.PdataCount[image] - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                IntPtr entry = new IntPtr((long)(table.Pdata[image] + (ulong)middle * 12));
                uint begin = (uint)Marshal.ReadInt32(entry), end = (uint)Marshal.ReadInt32(entry, 4);
                if (rva < begin) high = middle - 1;
                else if (rva >= end) low = middle + 1;
                else return entry;
            }
            return IntPtr.Zero;
        }

        private static void RefreshImages()
        {
            var starts = new List<ulong>(); var ends = new List<ulong>(); var names = new List<string>();
            var pdata = new List<ulong>(); var counts = new List<int>();
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                try
                {
                    IntPtr basePtr = module.BaseAddress;
                    ulong start = (ulong)basePtr.ToInt64();
                    if (start == 0) continue;
                    ulong end = start + (ulong)module.ModuleMemorySize;
                    string name = module.ModuleName ?? module.FileName ?? ("0x" + start.ToString("X", C));
                    // PE32+ only: IMAGE_DIRECTORY_ENTRY_EXCEPTION is data directory 3 (OptionalHeader + 0x70 + 3*8).
                    int pe = Marshal.ReadInt32(basePtr, 0x3C);
                    bool pe64 = Marshal.ReadInt16(basePtr, pe + 0x18) == 0x20B;
                    uint exceptionRva = pe64 ? (uint)Marshal.ReadInt32(basePtr, pe + 0x18 + 0x70 + 3 * 8) : 0;
                    uint exceptionSize = pe64 ? (uint)Marshal.ReadInt32(basePtr, pe + 0x18 + 0x70 + 3 * 8 + 4) : 0;
                    starts.Add(start); ends.Add(end);
                    names.Add(System.IO.Path.GetFileNameWithoutExtension(name));
                    pdata.Add(start + exceptionRva);
                    counts.Add(exceptionRva == 0 ? 0 : (int)(exceptionSize / 12));
                }
                catch (Exception exception)
                {
                    // One unreadable module entry must not disable the sampler; report the first one.
                    if (!moduleErrorLogged)
                    {
                        moduleErrorLogged = true;
                        string detail = exception.ToString();
                        Shared.DevDiag.Log(() => "threadSampler skipped a module entry: " + detail);
                    }
                }
            }
            images = new ImageTable { Starts = starts.ToArray(), Ends = ends.ToArray(), Pdata = pdata.ToArray(), PdataCount = counts.ToArray(), Names = names.ToArray() };
        }

        private static readonly Dictionary<ulong, string> labelCache = new Dictionary<ulong, string>();

        private static string Label(ulong ip, out string assembly)
        {
            assembly = null;
            if (gameBase != 0 && ip >= gameBase && ip < gameEnd)
            {
                IntPtr entry = FindFunctionEntry(ip, out _);
                return entry != IntPtr.Zero ? "CDE:0x" + ((uint)Marshal.ReadInt32(entry)).ToString("X", C) : "CDE:~0x" + (ip - gameBase).ToString("X", C);
            }
            ImageTable table = images;
            int image = FindImage(ip, table);
            if (image >= 0) return table.Names[image];
            string name = MonoNames.Resolve(ip, out assembly);
            // Exact IP: rounding could merge two adjacent stubs or cross a logged range.
            return name ?? "dyn@0x" + ip.ToString("X", C);
        }

        private static bool IsMod(string assembly)
        {
            if (string.IsNullOrEmpty(assembly)) return false;
            foreach (string host in HostAssemblies) if (assembly.StartsWith(host, StringComparison.Ordinal)) return false;
            return true;
        }

        private static void Publish(PerfProbeSampleBatch lane, double seconds, long start, long end)
        {
            var self = new Dictionary<string, int>();
            var incl = new Dictionary<string, int>();
            var asm = new Dictionary<string, int>();
            var entry = new Dictionary<string, int>();
            var seenMethods = new HashSet<string>();
            var seenAsm = new HashSet<string>();
            var entryOfAsm = new Dictionary<string, string>();
            // inner: innermost mod frame of a sample = the mod method whose own work or native callees
            // the sample hit (outer whole-tick wrappers only win when nothing deeper belongs to a mod).
            // guard: method directly called by a RedBird context-hook guard (native hook callbacks).
            var inner = new Dictionary<string, int>();
            var guard = new Dictionary<string, int>();
            var orphan = new Dictionary<string, int>();
            for (int s = 0; s < lane.Count; s++)
            {
                int offset = s * MaxFrames, depth = lane.Depth[s];
                seenMethods.Clear(); seenAsm.Clear(); entryOfAsm.Clear();
                string innerLabel = null, previousManaged = null;
                for (int f = 0; f < depth; f++)
                {
                    string label = Label(lane.Frames[offset + f], out string assembly);
                    if (f == 0) Bump(self, label);
                    if (assembly != null)
                    {
                        if (seenMethods.Add(label)) Bump(incl, label);
                        if (seenAsm.Add(assembly)) Bump(asm, assembly);
                        // Frames go inner -> outer: the last seen frame of a mod assembly is its entry point.
                        if (IsMod(assembly))
                        {
                            entryOfAsm[assembly] = label;
                            if (innerLabel == null) innerLabel = label;
                        }
                        if (label.Contains("ContextStub") && label.Contains("Guard") && previousManaged != null &&
                            !previousManaged.Contains("ContextStub")) Bump(guard, previousManaged);
                        previousManaged = label;
                    }
                }
                foreach (string label in entryOfAsm.Values) Bump(entry, label);
                Bump(inner, innerLabel ?? "(no mod frame)");
                // Samples without any mod frame: which code (stub, runtime, native) they ended in,
                // and the first non-system frame above the leaf.
                if (innerLabel == null)
                {
                    string owner = null;
                    for (int f = 0; f < depth && owner == null; f++)
                    {
                        string label = Label(lane.Frames[offset + f], out _);
                        if (label != "ntdll" && label != "KERNELBASE" && label != "KERNEL32" && label != "mono-2.0-bdwgc") owner = label;
                    }
                    Bump(orphan, owner ?? "(system only)");
                }
            }
            int count = lane.Count, dropped = lane.Dropped;
            string Top(Dictionary<string, int> map, int n) => string.Join(" | ", map.OrderByDescending(kv => kv.Value).Take(n)
                .Select(kv => kv.Key + "=" + kv.Value.ToString(C) + "(" + (kv.Value * 100.0 / Math.Max(1, count)).ToString("0.0", C) + "%)"));
            long previous = start, maxGap = 0;
            for (int i = 0; i < count; i++) { maxGap = Math.Max(maxGap, lane.Stamps[i] - previous); previous = lane.Stamps[i]; }
            maxGap = Math.Max(maxGap, end - previous);
            string head = string.Format(C, "threadSample thread={0} windowS={1:0.0} samples={2} dropped={3} osThread={4} startStamp={5} endStamp={6} maxSampleGapMs={7:F3} simOnlyWhileRunActive={8}",
                lane.Name, seconds, count, dropped, lane.OsThreadId, start, end, maxGap * 1000.0 / Stopwatch.Frequency, lane.Name == "sim");
            string selfText = Top(self, 15), asmText = Top(asm, 12), inclText = Top(incl, 25), entryText = Top(entry, 25);
            Shared.DevDiag.Log(() => head + " self: " + selfText);
            Shared.DevDiag.Log(() => head + " asm: " + asmText);
            Shared.DevDiag.Log(() => head + " incl: " + inclText);
            Shared.DevDiag.Log(() => head + " entry: " + entryText);
            string innerText = Top(inner, 25), guardText = Top(guard, 20);
            Shared.DevDiag.Log(() => head + " inner: " + innerText);
            if (guard.Count > 0) Shared.DevDiag.Log(() => head + " guard: " + guardText);
            string orphanText = Top(orphan, 30);
            if (orphan.Count > 0) Shared.DevDiag.Log(() => head + " nomod: " + orphanText);
            // The consumer owns this immutable batch until the job is released.
        }

        private static void Bump(Dictionary<string, int> map, string key) => map[key] = map.TryGetValue(key, out int value) ? value + 1 : 1;

        private static ulong ReadStackBase(IntPtr thread, IntPtr scratch)
        {
            if (thread == IntPtr.Zero) return 0;
            IntPtr info = Marshal.AllocHGlobal(48);
            try
            {
                // THREAD_BASIC_INFORMATION: ExitStatus(4)+pad, TebBaseAddress at 8; NT_TIB.StackBase at TEB+8.
                if (NtQueryInformationThread(thread, 0, info, 48, out _) != 0) return 0;
                ulong teb = (ulong)Marshal.ReadInt64(info, 8);
                return teb != 0 && SafeRead(teb + 8, scratch, out ulong stackBase) ? stackBase : 0;
            }
            finally { Marshal.FreeHGlobal(info); }
        }

        // Mono embedding API (exported by mono-2.0-bdwgc.dll), used only outside suspensions.
        private static class MonoNames
        {
            private delegate IntPtr PtrFn();
            private delegate IntPtr FindFn(IntPtr domain, IntPtr address);
            private delegate IntPtr PtrPtrFn(IntPtr value);
            private delegate IntPtr NameFn(IntPtr method, int signature);
            private delegate void FreeFn(IntPtr value);
            private static FindFn find;
            private static PtrPtrFn getMethod, getClass, getImage, imageName;
            private static NameFn fullName;
            private static FreeFn free;
            private static IntPtr domain;
            private static readonly Dictionary<IntPtr, KeyValuePair<string, string>> byMethod = new Dictionary<IntPtr, KeyValuePair<string, string>>();

            internal static bool Init(out string error)
            {
                error = null;
                IntPtr mono = GetModuleHandle("mono-2.0-bdwgc.dll");
                if (mono == IntPtr.Zero) { error = "mono-2.0-bdwgc.dll not loaded"; return false; }
                T Get<T>(string name) where T : class
                {
                    IntPtr address = GetProcAddress(mono, name);
                    if (address == IntPtr.Zero) throw new MissingMethodException("mono export " + name);
                    return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
                }
                try
                {
                    domain = Get<PtrFn>("mono_get_root_domain")();
                    find = Get<FindFn>("mono_jit_info_table_find");
                    getMethod = Get<PtrPtrFn>("mono_jit_info_get_method");
                    getClass = Get<PtrPtrFn>("mono_method_get_class");
                    getImage = Get<PtrPtrFn>("mono_class_get_image");
                    imageName = Get<PtrPtrFn>("mono_image_get_name");
                    fullName = Get<NameFn>("mono_method_full_name");
                    free = Get<FreeFn>("mono_free");
                    return domain != IntPtr.Zero;
                }
                catch (Exception exception) { error = exception.Message; return false; }
            }

            internal static string Resolve(ulong ip, out string assembly)
            {
                assembly = null;
                IntPtr info = find(domain, new IntPtr((long)ip));
                if (info == IntPtr.Zero) return null;
                IntPtr method = getMethod(info);
                if (method == IntPtr.Zero) return null;
                if (!byMethod.TryGetValue(method, out KeyValuePair<string, string> resolved))
                {
                    string asmName = "?";
                    IntPtr klass = getClass(method);
                    if (klass != IntPtr.Zero)
                    {
                        IntPtr image = getImage(klass);
                        if (image != IntPtr.Zero) asmName = Marshal.PtrToStringAnsi(imageName(image)) ?? "?";
                    }
                    IntPtr text = fullName(method, 0);
                    string name = text == IntPtr.Zero ? "?" : Marshal.PtrToStringAnsi(text);
                    if (text != IntPtr.Zero) free(text);
                    resolved = new KeyValuePair<string, string>("[" + asmName + "]" + name, asmName);
                    byMethod[method] = resolved;
                }
                assembly = resolved.Value;
                return resolved.Key;
            }
        }

        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module, string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenThread(uint access, bool inherit, uint threadId);
        [DllImport("kernel32.dll", EntryPoint = "CloseHandle")] private static extern bool CloseThreadHandle(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern uint SuspendThread(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern bool GetThreadContext(IntPtr thread, IntPtr context);
        [DllImport("kernel32.dll")] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, IntPtr buffer, IntPtr size, out IntPtr read);
        [DllImport("ntdll.dll")] private static extern int NtQueryInformationThread(IntPtr thread, int infoClass, IntPtr info, int length, out int returned);
        [DllImport("kernel32.dll")]
        private static extern IntPtr RtlVirtualUnwind(uint handlerType, ulong imageBase, ulong controlPc, IntPtr functionEntry,
            IntPtr context, out IntPtr handlerData, out ulong establisherFrame, IntPtr contextPointers);
    }
}

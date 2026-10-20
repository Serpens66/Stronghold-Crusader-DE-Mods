using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PerfProbe
{
    // Sampling profiler for long native simulation ticks (no hook, no patch of native code):
    // while one EngineInterface.run has been running longer than StartAfterMs, a background thread
    // briefly suspends the simulation thread about once per millisecond, reads its register
    // context and unwinds the stack with the x64 unwind tables (RtlLookupFunctionEntry /
    // RtlVirtualUnwind). Frames inside CrusaderDE.dll are reduced to the RVA of the containing
    // function (RUNTIME_FUNCTION.BeginAddress) and aggregated per long tick:
    //   simSample: ms=<tick> samples=<n> leaf=<rva:count,...> incl=<rva:count,...>
    // The RVAs are resolved offline with the native baseline (query.ps1 function <RVA>).
    // While the target is suspended the sampler neither allocates nor logs (no heap/GC lock).
    internal static class PerfProbeSimSampler
    {
        private const int StartAfterMs = 80, MaxFrames = 48, MaxSamples = 4000;
        private const uint ContextFull = 0x10000B;
        private const int ContextSize = 1232, OffFlags = 0x30, OffRsp = 0x98, OffRip = 0xF8;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        private static int simThreadId;
        private static long runStart;
        private static ulong imageBase, imageEnd;
        private static Thread worker;

        // Per-sample frame buffer (function RVAs inside the game DLL, 0 = foreign frame), preallocated.
        private static readonly uint[] frames = new uint[MaxFrames];
        private static readonly uint[][] samples = new uint[MaxSamples][];
        private static readonly int[] sampleDepth = new int[MaxSamples];

        internal static void Install()
        {
            IntPtr module = GetModuleHandle("CrusaderDE.dll");
            if (module == IntPtr.Zero) { Shared.DevDiag.Log(() => "simSampler disabled: CrusaderDE.dll not loaded"); return; }
            imageBase = (ulong)module.ToInt64();
            int peOffset = Marshal.ReadInt32(module, 0x3C);
            imageEnd = imageBase + (uint)Marshal.ReadInt32(module, peOffset + 0x18 + 0x38); // OptionalHeader.SizeOfImage
            for (int i = 0; i < MaxSamples; i++) samples[i] = new uint[MaxFrames];
            worker = new Thread(Loop) { IsBackground = true, Name = "PerfProbe.SimSampler", Priority = ThreadPriority.Highest };
            worker.Start();
            Shared.DevDiag.Log(() => string.Format(C, "simSampler ready image=0x{0:X}-0x{1:X} startAfterMs={2}", imageBase, imageEnd, StartAfterMs));
        }

        // Called on the simulation thread from the EngineInterface.run prefix/postfix.
        internal static void OnRunBegin(long stamp)
        {
            if (simThreadId == 0) simThreadId = (int)GetCurrentThreadId();
            Volatile.Write(ref runStart, stamp);
        }

        internal static void OnRunEnd() => Volatile.Write(ref runStart, 0);

        private static void Loop()
        {
            IntPtr context = Marshal.AllocHGlobal(ContextSize + 16);
            IntPtr aligned = new IntPtr((context.ToInt64() + 15) & ~15L);
            IntPtr thread = IntPtr.Zero;
            int count = 0;
            long sampledRun = 0, firstSample = 0, lastSample = 0;
            while (true)
            {
                try
                {
                    long started = Volatile.Read(ref runStart);
                    if (sampledRun != 0 && started != sampledRun)
                    {
                        Publish(count, (lastSample - sampledRun) * 1000.0 / Stopwatch.Frequency, (firstSample - sampledRun) * 1000.0 / Stopwatch.Frequency);
                        sampledRun = 0; count = 0;
                    }
                    if (started == 0 || (Stopwatch.GetTimestamp() - started) * 1000 / Stopwatch.Frequency < StartAfterMs)
                    {
                        Thread.Sleep(started == 0 ? 5 : 1);
                        continue;
                    }
                    if (thread == IntPtr.Zero)
                    {
                        thread = OpenThread(0x0002 | 0x0008 | 0x0040, false, (uint)simThreadId); // SUSPEND_RESUME | GET_CONTEXT | QUERY_INFORMATION
                        if (thread == IntPtr.Zero) { Shared.DevDiag.Log(() => "simSampler disabled: OpenThread failed " + Marshal.GetLastWin32Error()); return; }
                    }
                    if (sampledRun != started) { sampledRun = started; count = 0; firstSample = Stopwatch.GetTimestamp(); }
                    if (count < MaxSamples)
                    {
                        int depth = Sample(thread, aligned);
                        if (depth > 0)
                        {
                            Array.Copy(frames, samples[count], depth);
                            sampleDepth[count] = depth;
                            count++;
                            lastSample = Stopwatch.GetTimestamp();
                        }
                    }
                    // ~1 ms cadence without depending on the system timer resolution.
                    long until = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 1000;
                    while (Stopwatch.GetTimestamp() < until) Thread.SpinWait(50);
                }
                catch (Exception exception)
                {
                    Shared.DevDiag.Log(() => "simSampler stopped: " + exception.GetType().Name + " " + exception.Message);
                    return;
                }
            }
        }

        // Returns the number of frames written to `frames`; the target is resumed before returning.
        private static int Sample(IntPtr thread, IntPtr context)
        {
            if (SuspendThread(thread) == uint.MaxValue) return 0;
            int depth = 0;
            try
            {
                Marshal.WriteInt32(context, OffFlags, unchecked((int)ContextFull));
                if (!GetThreadContext(thread, context)) return 0;
                for (int i = 0; i < MaxFrames; i++)
                {
                    ulong rip = (ulong)Marshal.ReadInt64(context, OffRip);
                    if (rip == 0) break;
                    ulong moduleBase;
                    IntPtr entry = RtlLookupFunctionEntry(rip, out moduleBase, IntPtr.Zero);
                    bool inGame = rip >= imageBase && rip < imageEnd;
                    frames[depth++] = inGame ? (entry != IntPtr.Zero ? (uint)Marshal.ReadInt32(entry) : (uint)(rip - imageBase) | 0x80000000u) : 0u;
                    if (entry == IntPtr.Zero)
                    {
                        // Leaf function without unwind data: return address on top of the stack.
                        ulong rsp = (ulong)Marshal.ReadInt64(context, OffRsp);
                        if (rsp == 0) break;
                        Marshal.WriteInt64(context, OffRip, Marshal.ReadInt64(new IntPtr((long)rsp)));
                        Marshal.WriteInt64(context, OffRsp, (long)(rsp + 8));
                        // Leaving the game DLL without unwind data: stop (managed/JIT frames follow).
                        if (!inGame) break;
                        continue;
                    }
                    RtlVirtualUnwind(0, moduleBase, rip, entry, context, out _, out _, IntPtr.Zero);
                }
            }
            finally { ResumeThread(thread); }
            return depth;
        }

        private static void Publish(int count, double tickMsAtLastSample, double firstSampleMs)
        {
            if (count == 0) return;
            var leaf = new Dictionary<uint, int>();
            var inclusive = new Dictionary<uint, int>();
            var seen = new HashSet<uint>();
            var paths = new Dictionary<string, int>();
            for (int s = 0; s < count; s++)
            {
                uint[] stack = samples[s];
                int depth = sampleDepth[s];
                uint first = 0;
                for (int f = 0; f < depth; f++) if (stack[f] != 0) { first = stack[f]; break; }
                if (first != 0) leaf[first] = leaf.TryGetValue(first, out int l) ? l + 1 : 1;
                seen.Clear();
                var path = new StringBuilder();
                int pathLen = 0;
                for (int f = depth - 1; f >= 0; f--)
                {
                    uint rva = stack[f];
                    if (rva == 0) continue;
                    if (seen.Add(rva)) inclusive[rva] = inclusive.TryGetValue(rva, out int n) ? n + 1 : 1;
                    if (pathLen < 8) { path.Append(pathLen == 0 ? "" : ">").Append(Rva(rva)); pathLen++; }
                }
                string key = path.ToString();
                paths[key] = paths.TryGetValue(key, out int p) ? p + 1 : 1;
            }
            string Top(Dictionary<uint, int> map, int n) => string.Join(",", map.OrderByDescending(kv => kv.Value).Take(n).Select(kv => Rva(kv.Key) + ":" + kv.Value.ToString(C)));
            Shared.DevDiag.Log(() => string.Format(C, "simSample: tickMs>={0:F0} firstSampleMs={1:F0} samples={2} leaf={3}", tickMsAtLastSample, firstSampleMs, count, Top(leaf, 15)));
            Shared.DevDiag.Log(() => "simSample incl=" + Top(inclusive, 30));
            foreach (var entry in paths.OrderByDescending(kv => kv.Value).Take(6))
                Shared.DevDiag.Log(() => "simSample path(outer>inner, 8 deep) n=" + entry.Value.ToString(C) + " " + entry.Key);
        }

        // High bit marks an address without unwind data (raw RVA instead of a function start).
        private static string Rva(uint value) => (value & 0x80000000u) != 0 ? "~0x" + (value & 0x7FFFFFFFu).ToString("X", C) : "0x" + value.ToString("X", C);

        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenThread(uint access, bool inherit, uint threadId);
        [DllImport("kernel32.dll")] private static extern uint SuspendThread(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern bool GetThreadContext(IntPtr thread, IntPtr context);
        [DllImport("kernel32.dll")] private static extern IntPtr RtlLookupFunctionEntry(ulong controlPc, out ulong imageBase, IntPtr history);
        [DllImport("kernel32.dll")]
        private static extern IntPtr RtlVirtualUnwind(uint handlerType, ulong imageBase, ulong controlPc, IntPtr functionEntry,
            IntPtr context, out IntPtr handlerData, out ulong establisherFrame, IntPtr contextPointers);
    }
}

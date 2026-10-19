// TEMP_GATE_MIGRATION_DIAGNOSTICS: retained for old/new acceptance; see removal inventory.
using APIShared.GateRouting.Native;
// TEMP_GATE_ROUTE_ACCEPTANCE: private gate-test probes; never owns another mod's function entry.
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;

namespace BugfixesAndQoL.GateRouting
{
    internal sealed unsafe class NativeAssassinDiagnosticHooks
    {
        internal static readonly int[] Sites = { 0xF4944, 0xF4C30, 0xF4BCB, 0xF4BF0,
            0xE1640, 0xE1A1F, 0xE1A5A, 0xE1ABE, 0xE1BB4, 0xE1BE3,
            0x185D55, 0x18595F, 0x185C5A, 0x185CAF, 0x16DB22, 0x16DC9D, 0x185FD0, 0x16DFC6, 0x16D0BF };
        internal static readonly string[] Bytes = {
            "4863410c488bd9418bf0448bd24c8d05a8b6f0ff", "488b5c2450488b6c2458488b742460",
            "e8806afeff8b4b14448b4b10448b430c", "896c2430c7442428801a0600894c2420",
            "48894c2408534881ec800000004c63d2", "488b4424400fb74c242c498d04c6",
            "448bd2895424208bf889442424458bec", "8bb424b0000000458bc24963c54489542428",
            "4c8b7424584c8b6c24604c8b642468", "488b9c2490000000488bcbff4378",
            "8bd3488bcfe8713101008bd3488bcfe8e71b0100", "89442430895424288bd34489742420e8ed74f5ff",
            "8bd34189b43c40070000498bcae8a4410100", "664189b43c2a09000041c7843c4e07000000000800",
            "4863059b277c004869c890040000b865000000664289843918090000", "4689a43908090000b865000000664289843918090000",
            "b808000000664189843c50070000b86a000000664189843c18090000", "4689a439080900008bd7498bcfe818740100",
            "85c00f84841200004689a43b16090000" };
        [ThreadStatic] private static NativeAssassinTrace current;
        private static readonly System.Collections.Generic.List<NativeAssassinDiagnosticHooks> ProcessRoots = new System.Collections.Generic.List<NativeAssassinDiagnosticHooks>();
        private readonly CapturerComparisonCallback[] callbacks = new CapturerComparisonCallback[Sites.Length * 2];
        private readonly HookHandle<X64InlineHook>[] hooks = new HookHandle<X64InlineHook>[Sites.Length];
        private readonly TemporaryGateRouteAcceptance observer;
        private readonly ulong module;
        private readonly int* enabled = (int*)Marshal.AllocHGlobal(4);
        private readonly HookTransaction transaction;
        private long sequence;
        internal readonly NativeAssassinMovementDiagnostics Execution;
        private readonly Action<int, IntPtr, bool> executionCallback;
        private readonly Action executionBuilder;
        internal long Failures, UnscopedReconstructions, AbandonedBuilders;
        internal long CallbackCount, CallbackTicks, MaximumCallbackTicks;
        internal long BuilderEntries, BuilderExits;
        internal bool Installed { get; private set; }

        internal NativeAssassinDiagnosticHooks(CrusaderLibraryLoadContext context, TemporaryGateRouteAcceptance observer)
        {
            this.observer = observer; module = (ulong)context.ModuleHandle.ToInt64(); *enabled = 0;
            Execution = new NativeAssassinMovementDiagnostics(module, id => { var sample = NativeAssassinMovementDiagnostics.ReadUnit(module, id); observer.StampExecution(ref sample); return sample; });
            executionCallback = Execution.Observe; executionBuilder = Execution.NoteBuilder;
            // Exact bytes + installed backend decoding before the first patch. Full containing-body
            // and external incoming-edge checks are also run by the native trace contract tests.
            for (int i = 0; i < Sites.Length; i++)
            {
                if (i == 10) continue; // functional AssassinGateDescentRuntime owns this span
                byte[] expected = Hex(Bytes[i]);
                for (int b = 0; b < expected.Length; b++)
                    if (context.Memory[Sites[i] + b] != expected[b])
                        throw new InvalidOperationException("Native trace span changed at 0x" + Sites[i].ToString("X"));
                var probe = new X64InlineHook(module + (uint)Sites[i], expected.Length);
                try { if (probe.DisplacedByteCount != expected.Length) throw new InvalidOperationException("Native trace displacement mismatch"); }
                finally { probe.Dispose(); } // unpublished probe, no patch ever enabled
            }
            transaction = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = false });
            for (int i = 0; i < Sites.Length; i++)
            {
                if (i == 10) continue; // functional AssassinGateDescentRuntime owns this span
                int site = i;
                callbacks[i] = p => Observe(site, (X64SmartCPUContext*)p);
                ulong callback = (ulong)Marshal.GetFunctionPointerForDelegate(callbacks[i]).ToInt64();
                callbacks[Sites.Length + i] = p => Observe(site + 100, (X64SmartCPUContext*)p);
                ulong after = (ulong)Marshal.GetFunctionPointerForDelegate(callbacks[Sites.Length + i]).ToInt64();
                hooks[i] = new HookHandle<X64InlineHook>();
                transaction.AddInline(hooks[i], HookTarget.FromAddress(module + (uint)Sites[i]),
                    (a, original, ret) =>
                    {
                        if (site < 10) NativeCallbackAdapterEmitter.Emit(a, original, callback, (ulong)enabled);
                        else NativeCallbackAdapterEmitter.EmitExecution(a, original, callback, site == 18 ? 0 : after,
                            (ulong)enabled, module, site == 14 || site == 15 || site == 17 || site == 18);
                    }, Bytes[i].Length / 2);
            }
            var result = transaction.Commit();
            if (!result.IsCompleteSuccess) throw new InvalidOperationException("Native trace transaction failed: " + result);
            for (int i = 0; i < Sites.Length; i++)
                if (i != 10 && (!hooks[i].Success || hooks[i].Hook.DisplacedByteCount != Bytes[i].Length / 2))
                { transaction.DisableAll(); throw new InvalidOperationException("Unpublished native trace transaction mismatch"); }
            lock (ProcessRoots) ProcessRoots.Add(this);
            Installed = true; // no path can undo a published transaction
        }
        internal void ObserveClosedGate(IntPtr p, bool post) => Observe(post ? 110 : 10, (X64SmartCPUContext*)p);
        internal void ObserveDescentException(int unit)
        { try { Execution.NoteDescentException(unit); } catch (Exception) { Interlocked.Increment(ref Failures); } }
        internal void SetActive(bool active)
        {
            Volatile.Write(ref *enabled, active && Installed ? 1 : 0);
            if (active) Execution.Reset(); else Execution.Close();
            if (!active)
                while (current != null)
                {
                    NativeAssassinTrace abandoned = current; current = abandoned.Previous;
                    abandoned.Invalid = true; Interlocked.Increment(ref AbandonedBuilders);
                    observer.EndNativeTrace(abandoned);
                }
        }
        internal static byte[] Hex(string value)
        { var bytes = new byte[value.Length / 2]; for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return bytes; }
        private void Observe(int site, X64SmartCPUContext* c)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (site >= 10)
                { executionCallback?.Invoke(site >= 100 ? site - 100 : site, (IntPtr)c, site >= 100); return; }
                if (site == 0)
                {
                    executionBuilder?.Invoke();
                    Interlocked.Increment(ref BuilderEntries);
                    // A native/managed unwind can bypass the ordinary return site. Its
                    // frame must never be reused by a later invocation at the same stack.
                    while (current != null && c->RSP >= current.Stack)
                    {
                        NativeAssassinTrace abandoned = current; current = abandoned.Previous;
                        abandoned.Invalid = true; Interlocked.Increment(ref AbandonedBuilders);
                        observer.EndNativeTrace(abandoned);
                    }
                    var trace = new NativeAssassinTrace { Previous = current, Sequence = Interlocked.Increment(ref sequence),
                        Manager = c->RCX, Stack = c->RSP };
                    // A nested builder can overwrite the shared field/buffer even when its
                    // final direction bytes happen to be identical. Never inherit that proof.
                    if (current != null && current.Manager == trace.Manager) current.Invalid = true;
                    current = trace; // every entry has a frame, including unassigned nested builders
                    if (trace.Manager != module + 0x60AD660) { trace.Invalid = true; return; }
                    byte* manager = (byte*)trace.Manager;
                    trace.Buffer = *(ulong*)(manager + 0x155F60);
                    trace.X = *(int*)(manager + 8); trace.Y = *(int*)(manager + 12);
                    trace.TX = *(int*)(manager + 16); trace.TY = *(int*)(manager + 20);
                    long slot = unchecked((long)(trace.Buffer - (module + 0x67E8400 + 0xB4FE78)));
                    trace.Unit = slot > 0 && slot % 1000 == 0 && slot / 1000 <= 10000 ? (int)(slot / 1000) : 0;
                    trace.BufferKind = trace.Unit > 0 ? "unit-slot" : "non-unit-buffer-unassigned";
                    observer.BeginNativeTrace(trace);
                    trace.CollectEdges = *(int*)(manager + 0x88) != 0 || trace.Publication != null;
                    return;
                }
                NativeAssassinTrace t = current;
                if (site == 1) Interlocked.Increment(ref BuilderExits);
                if (t == null) { if (site == 4) Interlocked.Increment(ref UnscopedReconstructions); return; }
                if (site == 1)
                {
                    try
                    {
                        t.Result = (int)c->RAX; t.Closed = true;
                        if (c->RSP != t.Stack || c->RBX != t.Manager) t.Invalid = true;
                        if (!t.Invalid && (*(ulong*)((byte*)t.Manager + 0x155F60) != t.Buffer ||
                            *(int*)((byte*)t.Manager + 0x155F68) != t.Result)) t.Invalid = true;
                        observer.EndNativeTrace(t);
                    }
                    finally { current = t.Previous; }
                    return;
                }
                if (t.Invalid || !t.CollectEdges) return;
                if (site == 2 || site == 3)
                {
                    if (c->RSP != t.Stack || c->RBX != t.Manager) { t.Invalid = true; return; }
                    if (site == 2) t.Secondary = true; else t.SecondaryFallback = true;
                    return;
                }
                if (site == 4)
                {
                    if (c->RCX != t.Manager) { t.Invalid = true; return; }
                    t.ReconstructionStack = c->RSP;
                    t.BeginReconstruction(*(int*)(c->RSP + 0x30)); return;
                }
                if (t.Reconstructions == 0 || c->RSP + 0x88 != t.ReconstructionStack) { t.Invalid = true; return; }
                if (site == 5) { t.Candidate(); return; }
                if (site == 6)
                {
                    int from = (int)c->RDX, to = (int)c->R8, d = (int)c->R12;
                    if ((uint)from >= 320800 || (uint)to >= 320800 || (uint)d > 7) { t.Invalid = true; return; }
                    t.Improve(to, from, d, *(byte*)(c->RSP + 0x98),
                        *(uint*)(module + 0x48F71B0 + (uint)from * 4UL), *(uint*)(module + 0x48F71B0 + (uint)to * 4UL));
                    return;
                }
                if (site == 7) { t.Select((int)c->R8, (int)c->R10, (int)c->R13); return; }
                if (site == 8) { t.FinishReconstruction(); return; }
                if (site == 9) t.FailReconstruction();
            }
            catch (Exception error)
            {
                if (current != null) current.Invalid = true;
                Interlocked.Increment(ref Failures);
                APIShared.TemporaryGateRouteAcceptanceBridge.ReportFailure(site >= 10 ? "native-execution-observer" : "native-reconstruction-observer", error);
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                Interlocked.Increment(ref CallbackCount); Interlocked.Add(ref CallbackTicks, elapsed);
                long maximum = Volatile.Read(ref MaximumCallbackTicks);
                while (elapsed > maximum)
                {
                    long seen = Interlocked.CompareExchange(ref MaximumCallbackTicks, elapsed, maximum);
                    if (seen == maximum) break;
                    maximum = seen;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using BepInEx.Bootstrap;

namespace AIBuildDiagnoseTest
{
    // Sole diagnostic owner of these function entries. Existing price/fix hooks stay untouched.
    internal sealed class EconomyNativeHooks
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int PairResult(ulong state, int player);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void PairAction(ulong state, int player);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int QuadResult(ulong state, int a, int b, int c);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void QuadAction(ulong state, int player, int kind, byte flag);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void TradeAction(ulong state, int player, int good, int amount);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SettlementAction(ulong state);
        private readonly EconomyDiagnostics owner;
        private readonly List<object> roots = new List<object>();
        private static EconomyNativeHooks published;
        internal EconomyNativeHooks(EconomyDiagnostics owner) { this.owner = owner; }

        internal void Install(CrusaderLibraryLoadContext context, string hash)
        {
            published = this;
            if (!string.Equals(hash, NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase))
            { owner.Coverage("native-decisions", false, "unknown-native-hash; events remain active"); return; }
            Pair(context, 0x2E1E0, "wood-eligibility");
            Pair(context, 0x2DBA0, "farm-eligibility");
            Pair(context, 0x3B360, "wood-retirement");
            ActionPair(context, 0x3EC90, "purchase-decision");
            ActionPair(context, 0x40740, "ai-recruitment");
            var settlement = new Node<SettlementAction>();
            Add(context, 0xCCBB0, settlement, (SettlementAction)(state =>
            {
                var operations = settlement.Ready ? owner.BeginSettlement() : null;
                try { settlement.Handle.Original(state); }
                finally { owner.EndSettlement(operations); }
            }));
            if (Chainloader.PluginInfos.ContainsKey("SpectatorEditorBuildTest_Serp"))
            {
                // Existing owner wins even if its initialization later fails. Never install a competing hook.
                owner.Coverage("build-resource-native-observation", owner.ResourceOwnerReady,
                    "entry owned by SpectatorEditorBuildTest_Serp; passive owner connection; readiness rechecked at session start");
            }
            else
            {
                var resources = new Node<QuadResult>();
                Add(context, 0xCC420, resources, (QuadResult)((state, mapper, player, feedback) =>
                {
                    var op = resources.Ready ? owner.SafeBegin("build-resources", player, mapper, feedback) : null;
                    int? result = null;
                    try { int value = resources.Handle.Original(state, mapper, player, feedback); result = value; return value; }
                    finally { owner.SafeEnd(op, result); }
                }));
            }
            var buy = new Node<QuadResult>();
            Add(context, 0x29650, buy, (QuadResult)((state, player, good, amount) =>
            {
                var op = buy.Ready ? owner.SafeBegin("ai-buy", player, good, amount) : null; int? result = null;
                try { int value = buy.Handle.Original(state, player, good, amount); result = value; return value; }
                finally { owner.SafeEnd(op, result); }
            }));
            var sell = new Node<TradeAction>();
            Add(context, 0x29700, sell, (TradeAction)((state, player, good, amount) =>
            {
                var op = sell.Ready ? owner.SafeBegin("ai-sell", player, good, amount) : null;
                try { sell.Handle.Original(state, player, good, amount); }
                finally { owner.SafeEnd(op, null); }
            }));
            var emergency = new Node<QuadAction>();
            Add(context, 0x2D810, emergency, (QuadAction)((state, player, category, flag) =>
            {
                var op = emergency.Ready ? owner.SafeBegin("emergency-demolition", player, category, flag) : null;
                try { emergency.Handle.Original(state, player, category, flag); }
                finally { owner.SafeEnd(op, null); }
            }));
        }
        private void Pair(CrusaderLibraryLoadContext context, int rva, string kind)
        {
            var node = new Node<PairResult>();
            Add(context, rva, node, (PairResult)((state, player) =>
            {
                var op = node.Ready ? owner.SafeBegin(kind, player, 0, 0) : null; int? result = null;
                try { int value = node.Handle.Original(state, player); result = value; return value; }
                finally { owner.SafeEnd(op, result); }
            }));
        }
        private void ActionPair(CrusaderLibraryLoadContext context, int rva, string kind)
        {
            var node = new Node<PairAction>();
            Add(context, rva, node, (PairAction)((state, player) =>
            {
                var op = node.Ready ? owner.SafeBegin(kind, player, 0, 0) : null;
                try { node.Handle.Original(state, player); }
                finally { owner.SafeEnd(op, null); }
            }));
        }
        private sealed class Node<T> where T : Delegate
        {
            internal readonly DetourHandle<T> Handle = new DetourHandle<T>();
            internal HookTransaction Transaction;
            internal volatile bool Ready;
        }
        private void Add<T>(CrusaderLibraryLoadContext context, int rva, Node<T> node, T callback) where T : Delegate
        {
            // Root before publication, including a failed post-commit validation.
            roots.Add(node); roots.Add(callback);
            HookTransaction pending = null;
            try
            {
                EconomyHookContract contract = EconomyHookContract.Find(rva);
                ulong target = unchecked((ulong)context.ModuleHandle.ToInt64()) + (uint)rva;
                contract.ValidateBytes(context.Memory);
                var request = new DetourRequest<T> { Name = "AI economy preflight " + rva.ToString("X"), TargetAddress = target, Callback = callback };
                NativeDetour<T> probe = null;
                try
                {
                    probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<T>;
                    Validate(probe, target, contract.Length, false);
                }
                finally { probe?.Dispose(); } // Unpublished, never enabled.
                pending = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { OwnsHooks = false, FailureMode = TransactionFailureMode.RollbackAndThrow });
                pending.AddDetour(node.Handle, HookTarget.FromAddress(target), callback);
                var result = pending.Commit();
                node.Transaction = pending;
                if (!result.IsCompleteSuccess || !node.Handle.Success) throw new InvalidOperationException("Commit incomplete");
                Validate(node.Handle.Hook as NativeDetour<T>, target, contract.Length, true);
                node.Ready = true;
                owner.Coverage("native-" + rva.ToString("X"), true,
                    "method=reference-rva, fallback=unique-entry-pattern-on-same-hash, displaced=" + contract.Length);
            }
            catch (Exception ex)
            {
                if (!node.Handle.Success) pending?.Dispose(); // Only unpublished failures.
                owner.Coverage("native-" + rva.ToString("X"), false, ex.Message);
            }
        }
        internal static void Validate<T>(NativeDetour<T> hook, ulong target, int length, bool installed) where T : Delegate
        {
            if (hook == null || hook.Scheme != DetourScheme.Indirect || hook.DisplacedByteCount != length ||
                hook.TargetAddress != target || hook.IsInstalled != installed || hook.PointerSlot == IntPtr.Zero ||
                hook.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("NativeX64 Indirect contract mismatch");
            if (!installed) return;
            IntPtr entry = unchecked((IntPtr)(long)target);
            if (Marshal.ReadByte(entry) != 0xFF || Marshal.ReadByte(entry, 1) != 0x25 ||
                entry.ToInt64() + 6 + Marshal.ReadInt32(entry, 2) != hook.PointerSlot.ToInt64() ||
                Marshal.ReadIntPtr(hook.PointerSlot) != hook.HookEntryPointAddress)
                throw new InvalidOperationException("Installed pointer slot/entry mismatch");
            for (int i = 6; i < length; i++)
                if (Marshal.ReadByte(entry, i) != 0x90) throw new InvalidOperationException("Unexpected detour padding");
        }
        // Test/preflight only. The caller supplies a private executable copy; never a published target.
        internal static void ProbeBackend(ulong copy, int length)
        {
            PairResult callback = (state, player) => 0;
            var request = new DetourRequest<PairResult> { Name = "AI economy copied entry", TargetAddress = copy, Callback = callback };
            NativeDetour<PairResult> probe = null;
            try
            {
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<PairResult>;
                Validate(probe, copy, length, false);
                probe.Enable();
                Validate(probe, copy, length, true);
            }
            finally { probe?.Dispose(); GC.KeepAlive(callback); }
        }
    }
}

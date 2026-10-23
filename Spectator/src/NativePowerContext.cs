using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;

namespace Spectator
{
    internal sealed class NativePowerContext
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Query(int site);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SelectCall(int power, int actor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void CancelCall();
        private readonly Query query;
        private readonly HookTransaction transaction;
        private readonly HookHandle<X64InlineHook>[] hooks = new HookHandle<X64InlineHook>[4];
        private readonly SelectCall select;
        private readonly CancelCall cancel;
        private readonly IntPtr module;
        private static readonly List<NativePowerContext> Roots = new List<NativePowerContext>();
        internal bool Ready { get; private set; }

        internal unsafe NativePowerContext(CrusaderLibraryLoadContext context, Query callback, byte[] fileImage)
        {
            Roots.Add(this); // Retain callback storage even when final installation validation fails.
            query = callback; module = context.ModuleHandle;
            ulong image = unchecked((ulong)module.ToInt64());
            RequireLive(fileImage, image, 0x105541, 0x115);
            RequireLive(fileImage, image, 0x8CAD2, 0x443);
            RequireLive(fileImage, image, 0x897F0, 119);
            // Caller has verified the canonical on-disk SHA-256. All live spans must still be unowned.
            for (int i = 0; i < hooks.Length; i++)
            {
                byte[] expected = NativePowerAssembly.Bytes(i);
                if (!new ReadOnlySpan<byte>((void*)(image + (uint)NativePowerAssembly.Sites[i]), expected.Length).SequenceEqual(expected))
                    throw new InvalidOperationException("Unknown power hook owner at 0x" + NativePowerAssembly.Sites[i].ToString("X"));
                using (var probe = new X64InlineHook(image + (uint)NativePowerAssembly.Sites[i], expected.Length))
                    if (probe.DisplacedByteCount != expected.Length) throw new InvalidOperationException("Power backend span changed.");
                var a = new Assembler(64);
                NativePowerAssembly.Hook(a, NativePowerAssembly.Decode(expected, image + (uint)NativePowerAssembly.Sites[i]),
                    image + (uint)NativePowerAssembly.Sites[i] + (uint)expected.Length, i,
                    unchecked((ulong)Marshal.GetFunctionPointerForDelegate(query).ToInt64()));
                NativePowerAssembly.Assemble(a, image + 0x10000000);
            }
            // Construct once, before publication. This process-lived stub is never rewritten or freed.
            IntPtr gateway = NativeMemoryManager.AllocateStub(image + 0x105548, 128);
            if (gateway == IntPtr.Zero) throw new InvalidOperationException("Power gateway allocation failed.");
            var assembler = new Assembler(64); NativePowerAssembly.SelectionGateway(assembler, image);
            byte[] code = NativePowerAssembly.Assemble(assembler, unchecked((ulong)gateway.ToInt64()));
            Marshal.Copy(code, 0, gateway, code.Length);
            if (!FlushInstructionCache(GetCurrentProcess(), gateway, (UIntPtr)code.Length))
                throw new InvalidOperationException("Power gateway instruction publication failed.");
            select = (SelectCall)Marshal.GetDelegateForFunctionPointer(gateway, typeof(SelectCall));
            cancel = (CancelCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(module.ToInt64() + 0x897F0), typeof(CancelCall));
            transaction = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
            for (int i = 0; i < hooks.Length; i++)
            {
                int site = i; hooks[i] = new HookHandle<X64InlineHook>();
                transaction.AddInline(hooks[i], HookTarget.FromAddress(image + (uint)NativePowerAssembly.Sites[i]),
                    (a, original, back) => NativePowerAssembly.Hook(a, original, back, site,
                        unchecked((ulong)Marshal.GetFunctionPointerForDelegate(query).ToInt64())),
                    hookSize: NativePowerAssembly.Bytes(i).Length);
            }
            // Retain even a failed/partial candidate: callbacks remain inert until Ready. Never undo published code.
            if (!transaction.Commit().IsCompleteSuccess) throw new InvalidOperationException("Power transaction failed.");
            for (int i = 0; i < hooks.Length; i++)
                if (!hooks[i].Success || hooks[i].Hook.DisplacedByteCount != NativePowerAssembly.Bytes(i).Length)
                    throw new InvalidOperationException("Power installed span changed.");
            Ready = true;
        }
        internal void Select(int power, int actor) => select(power, actor);
        internal void Cancel() => cancel();
        internal int Mode => Marshal.ReadInt32(new IntPtr(module.ToInt64() + 0x60AD56C));
        internal unsafe void SetSelectedPower(int power) => *(int*)(module.ToInt64() + 0x60AD574) = power;
        internal int SelectedPower => Marshal.ReadInt32(new IntPtr(module.ToInt64() + 0x60AD574));
        internal int TargetTile => Marshal.ReadInt32(new IntPtr(module.ToInt64() + 0x86C1330));
        private static unsafe void RequireLive(byte[] file, ulong module, int rva, int length)
        {
            int pe = BitConverter.ToInt32(file, 60), count = BitConverter.ToUInt16(file, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(file, pe + 20);
            for (int i = 0; i < count; i++)
            {
                int row = table + i * 40, start = BitConverter.ToInt32(file, row + 12);
                int size = BitConverter.ToInt32(file, row + 16), raw = BitConverter.ToInt32(file, row + 20);
                if (rva < start || (long)rva + length > (long)start + size) continue;
                if ((BitConverter.ToUInt32(file, row + 36) & 0x20000000) == 0 ||
                    !new ReadOnlySpan<byte>((void*)(module + (uint)rva), length).SequenceEqual(new ReadOnlySpan<byte>(file, raw + rva - start, length)))
                    throw new InvalidOperationException("Unknown native power code owner at 0x" + rva.ToString("X"));
                return;
            }
            throw new InvalidOperationException("Power code is outside the executable image.");
        }
        [DllImport("kernel32")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr length);
    }
}

using System;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Extensions;
using SHCDESE.API.LowLevel;
using static Iced.Intel.AssemblerRegisters;

namespace KeepCampfireGroundPreserveTest
{
    // Three read-only hit maps identify which native zero-store reached each tile.
    // The displaced instructions still run on every path, including when disarmed.
    internal sealed unsafe class TerrainStoreTrace
    {
        internal const int TileCapacity = 320800;
        internal const int StateBytes = TileCapacity + 8;
        internal static readonly int[] Sites = { 0x68638, 0x68FB6, 0x6901D };
        internal static readonly int[] Spans = { 21, 17, 15 };
        internal static readonly byte[][] Prefixes = {
            new byte[] { 0x45, 0x33, 0xDB, 0xBE, 0xC0, 0, 0, 0,
                0x44, 0x89, 0x9C, 0x83, 0, 0x09, 0x14, 0, 0xE9, 0xD4, 0xEF, 0xFF, 0xFF },
            new byte[] { 0xA8, 0x20, 0x74, 0x0D, 0x46, 0x89, 0x9C, 0x83,
                0, 0x09, 0x14, 0, 0xE9, 0xB7, 0xF6, 0xFF, 0xFF },
            new byte[] { 0x73, 0x0D, 0x46, 0x89, 0x9C, 0x83,
                0, 0x09, 0x14, 0, 0xE9, 0x52, 0xF6, 0xFF, 0xFF }
        };
        private readonly HookHandle<X64InlineHook>[] hooks = {
            new HookHandle<X64InlineHook>(), new HookHandle<X64InlineHook>(),
            new HookHandle<X64InlineHook>()
        };
        private readonly IntPtr[] state = new IntPtr[3];
        private HookTransaction transaction;
        private bool published;

        internal static TerrainStoreTrace TryCreate(CrusaderLibraryLoadContext context,
            ManualLogSource logger, Action<string> log)
        {
            var candidate = new TerrainStoreTrace();
            try { candidate.Install(context, logger, log); return candidate; }
            catch { candidate.RollbackUnpublished(); throw; }
        }

        internal void Arm()
        {
            if (!published) return;
            byte[] empty = new byte[StateBytes];
            for (int site = 0; site < state.Length; site++) {
                Marshal.Copy(empty, 0, state[site], empty.Length);
                Interlocked.Exchange(ref *(int*)state[site].ToPointer(), 1);
            }
        }

        internal void Disarm()
        {
            if (!published) return;
            foreach (IntPtr pointer in state)
                Interlocked.Exchange(ref *(int*)pointer.ToPointer(), 0);
        }

        internal bool Hit(int siteIndex, int tileId) => published &&
            siteIndex >= 0 && siteIndex < state.Length &&
            tileId >= 0 && tileId < TileCapacity &&
            Marshal.ReadByte(state[siteIndex], 8 + tileId) != 0;

        private void Install(CrusaderLibraryLoadContext context,
            ManualLogSource logger, Action<string> log)
        {
            ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
            for (int site = 0; site < Sites.Length; site++)
            {
                byte[] actual = new byte[Spans[site]];
                Marshal.Copy((IntPtr)(image + (uint)Sites[site]), actual, 0, actual.Length);
                if (!Equal(actual, Prefixes[site]))
                    throw new InvalidOperationException("Terrain store bytes changed at 0x" +
                        Sites[site].ToString("X"));
                using (var probe = new X64InlineHook(image + (uint)Sites[site], Spans[site]))
                    if (probe.DisplacedByteCount != Spans[site])
                        throw new InvalidOperationException("RedBird terrain span changed at 0x" +
                            Sites[site].ToString("X"));
            }
            for (int site = 0; site < Sites.Length; site++) {
                state[site] = Marshal.AllocHGlobal(StateBytes);
                Marshal.Copy(new byte[StateBytes], 0, state[site], StateBytes);
            }
            transaction = new HookTransaction(context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions {
                    FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true
                });
            for (int site = 0; site < Sites.Length; site++)
            {
                int index = site;
                ulong stateAddress = unchecked((ulong)state[site].ToInt64());
                transaction.AddInline(hooks[site],
                    HookTarget.FromAddress(image + (uint)Sites[site]),
                    (assembler, original, continuation) =>
                        Generate(assembler, original, continuation, index, stateAddress),
                    hookSize: Spans[site]);
            }
            CommitResult result = transaction.Commit();
            if (!result.IsCompleteSuccess) throw new InvalidOperationException(
                "Terrain store trace transaction failed: " + result);
            for (int site = 0; site < Sites.Length; site++)
                if (!hooks[site].Success || !hooks[site].IsInstalled ||
                    hooks[site].Hook.DisplacedByteCount != Spans[site])
                    throw new InvalidOperationException("Terrain store hook contract changed at 0x" +
                        Sites[site].ToString("X"));
            published = true;
            log("terrain-store probes ready: 0x68638/0x68FB6/0x6901D spans=21/17/15 " +
                "mode=read-only, disarmed until Keep spawn");
        }

        internal static void Generate(Assembler assembler,
            ReadOnlySpan<Instruction> original, ulong continuation,
            int siteIndex, ulong stateAddress)
        {
            int count = siteIndex == 2 ? 3 : 4;
            int store = siteIndex == 2 ? 1 : 2;
            if (siteIndex < 0 || siteIndex >= Sites.Length ||
                original.Length != count || original[count - 1].NextIP != continuation ||
                original[store].Mnemonic != Mnemonic.Mov ||
                original[store].MemoryDisplacement64 != 0x140900 ||
                original[count - 1].Mnemonic != Mnemonic.Jmp)
                throw new InvalidOperationException("Terrain store instruction contract changed at 0x" +
                    Sites[siteIndex].ToString("X"));
            for (int i = 0; i < original.Length; i++)
            {
                if (i == store) EmitHit(assembler, stateAddress, siteIndex == 0);
                assembler.AddInstruction(original[i]);
            }
            assembler.AddUnrestrictedJmp(continuation);
        }

        private static void EmitHit(Assembler assembler, ulong stateAddress, bool useRax)
        {
            Label done = assembler.CreateLabel("terrainHitDone");
            assembler.pushfq();
            assembler.push(rdx);
            assembler.mov(rdx, stateAddress);
            assembler.cmp(__dword_ptr[rdx], 0);
            assembler.je(done);
            if (useRax) {
                assembler.cmp(rax, TileCapacity);
                assembler.jae(done);
                assembler.mov(__byte_ptr[rdx + rax + 8], 1);
            } else {
                assembler.cmp(r8, TileCapacity);
                assembler.jae(done);
                assembler.mov(__byte_ptr[rdx + r8 + 8], 1);
            }
            assembler.Label(ref done);
            assembler.pop(rdx);
            assembler.popfq();
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void RollbackUnpublished()
        {
            if (published) throw new InvalidOperationException("Published terrain probes remain installed.");
            transaction?.Dispose();
            transaction = null;
            for (int site = 0; site < state.Length; site++)
                if (state[site] != IntPtr.Zero) {
                    Marshal.FreeHGlobal(state[site]);
                    state[site] = IntPtr.Zero;
                }
        }
    }
}

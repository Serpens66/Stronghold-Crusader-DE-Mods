using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace APIShared
{
    internal sealed class GatehousePermanentRuntimeState
    {
        internal const int DistanceHookRva = 0xB7B70;
        internal const int DistanceDisplacedBytes = 75;
        internal const int DecisionHookRva = 0xB7BBB;
        internal const int DecisionDisplacedBytes = 37;
        internal const int DecisionReturnRva = 0xB7BE0;
        internal const int ClosePathRva = 0xB7C39;

        private const int UnitXOffset = 0x67E8B0E;
        private const int UnitYOffset = 0x67E8B10;
        private const int BuildingEndXOffset = 0x64CCD0A;
        private const int BuildingEndYOffset = 0x64CCD0C;

        private readonly IntPtr originFlag = Marshal.AllocHGlobal(sizeof(int));
        private readonly IntPtr publishedTiming = Marshal.AllocHGlobal(IntPtr.Size);
        private readonly object publicationSync = new object();
        private readonly List<IntPtr> timingSnapshots = new List<IntPtr>();
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> distanceHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> decisionHook = new HookHandle<X64InlineHook>();
        private readonly bool testOnly;

        private GatehousePermanentRuntimeState()
        {
            testOnly = true;
            InitializeState();
        }

        internal GatehousePermanentRuntimeState(
            ScanRegion region,
            ulong moduleBase,
            bool installDistance,
            bool installTiming)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (moduleBase == 0) throw new ArgumentOutOfRangeException(nameof(moduleBase));
            if (!installDistance && !installTiming)
                throw new ArgumentException("At least one gatehouse hook must be requested.");
            InitializeState();
            try
            {
                transaction = new HookTransaction(
                    region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                ulong originAddress = unchecked((ulong)originFlag.ToInt64());
                ulong timingPointerAddress = unchecked((ulong)publishedTiming.ToInt64());
                if (installDistance)
                {
                    transaction.AddInline(
                    distanceHook,
                    HookTarget.FromAddress(moduleBase + DistanceHookRva),
                    (assembler, instructions, returnAddress) =>
                            GenerateDistanceOrigin(assembler, instructions.ToArray(), originAddress),
                        hookSize: DistanceDisplacedBytes);
                }
                if (installTiming)
                {
                    transaction.AddInline(
                        decisionHook,
                        HookTarget.FromAddress(moduleBase + DecisionHookRva),
                        (assembler, instructions, returnAddress) =>
                            GenerateDecision(
                                assembler,
                                instructions.ToArray(),
                                timingPointerAddress,
                                moduleBase,
                                moduleBase + DecisionReturnRva,
                                moduleBase + ClosePathRva),
                        hookSize: DecisionDisplacedBytes);
                }
                CommitResult result = transaction.Commit();
                bool distanceValid = !installDistance ||
                    (distanceHook.Success && distanceHook.IsInstalled &&
                     distanceHook.Hook.DisplacedByteCount == DistanceDisplacedBytes);
                bool timingValid = !installTiming ||
                    (decisionHook.Success && decisionHook.IsInstalled &&
                     decisionHook.Hook.DisplacedByteCount == DecisionDisplacedBytes);
                if (!result.IsCompleteSuccess || !distanceValid || !timingValid)
                {
                    throw new InvalidOperationException(
                        $"Gatehouse permanent hooks failed validation: result={result}, " +
                        $"distance={distanceHook.Hook?.DisplacedByteCount}, decision={decisionHook.Hook?.DisplacedByteCount}.");
                }
            }
            catch
            {
                transaction?.Dispose();
                FreeUnpublishedState();
                throw;
            }
        }

        internal static GatehousePermanentRuntimeState CreateTestState(
            bool distanceInstalled = true,
            bool timingInstalled = true)
        {
            var state = new GatehousePermanentRuntimeState();
            state.testDistanceInstalled = distanceInstalled;
            state.testTimingInstalled = timingInstalled;
            return state;
        }

        private bool testDistanceInstalled = true;
        private bool testTimingInstalled = true;

        internal bool IsDistanceInstalled => testOnly
            ? testDistanceInstalled
            : distanceHook.Success && distanceHook.IsInstalled;

        internal bool IsTimingInstalled => testOnly
            ? testTimingInstalled
            : decisionHook.Success && decisionHook.IsInstalled;

        internal GatehouseDistanceOrigin Origin =>
            Marshal.ReadInt32(originFlag) == 0
                ? GatehouseDistanceOrigin.VanillaBuildingBegin
                : GatehouseDistanceOrigin.BuildingBoundsCenter;

        internal void PublishOrigin(GatehouseDistanceOrigin origin)
        {
            if (!IsDistanceInstalled)
                throw new InvalidOperationException("The permanent gatehouse distance hook is unavailable.");
            Thread.MemoryBarrier();
            Marshal.WriteInt32(originFlag,
                origin == GatehouseDistanceOrigin.BuildingBoundsCenter ? 1 : 0);
            Thread.MemoryBarrier();
        }

        internal void PublishTiming(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
        {
            if (!IsTimingInstalled)
                throw new InvalidOperationException("The permanent gatehouse timing hook is unavailable.");
            IntPtr snapshot = AllocateTiming(aiDistance, aiDelay, humanDistance, humanDelay);
            lock (publicationSync)
            {
                timingSnapshots.Add(snapshot);
                Thread.MemoryBarrier();
                Marshal.WriteIntPtr(publishedTiming, snapshot);
                Thread.MemoryBarrier();
            }
        }

        internal void ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay)
        {
            IntPtr current = Marshal.ReadIntPtr(publishedTiming);
            Thread.MemoryBarrier();
            aiDistance = Marshal.ReadInt32(current, 0);
            aiDelay = Marshal.ReadInt32(current, 4);
            humanDistance = Marshal.ReadInt32(current, 8);
            humanDelay = Marshal.ReadInt32(current, 12);
        }

        private void InitializeState()
        {
            Marshal.WriteInt32(originFlag, 0);
            IntPtr vanilla = AllocateTiming(
                GatehouseTimingTarget.VanillaAiDistance,
                GatehouseTimingTarget.VanillaAiDelay,
                GatehouseTimingTarget.VanillaHumanDistance,
                GatehouseTimingTarget.VanillaHumanDelay);
            timingSnapshots.Add(vanilla);
            Marshal.WriteIntPtr(publishedTiming, vanilla);
        }

        private static IntPtr AllocateTiming(
            int aiDistance,
            int aiDelay,
            int humanDistance,
            int humanDelay)
        {
            IntPtr target = Marshal.AllocHGlobal(4 * sizeof(int));
            Marshal.WriteInt32(target, 0, aiDistance);
            Marshal.WriteInt32(target, 4, aiDelay);
            Marshal.WriteInt32(target, 8, humanDistance);
            Marshal.WriteInt32(target, 12, humanDelay);
            return target;
        }

        private void FreeUnpublishedState()
        {
            Marshal.FreeHGlobal(originFlag);
            foreach (IntPtr snapshot in timingSnapshots)
                Marshal.FreeHGlobal(snapshot);
            Marshal.FreeHGlobal(publishedTiming);
        }

        internal static void GenerateDistanceOrigin(
            Assembler assembler,
            Instruction[] instructions,
            ulong originAddress)
        {
            if (instructions.Length == 0)
                throw new InvalidOperationException("The gatehouse distance hook displaced no instructions.");
            Label vanilla = assembler.CreateLabel("gatehouseDistanceVanilla");
            Label done = assembler.CreateLabel("gatehouseDistanceDone");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, originAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();

            assembler.movsx(r8d, __word_ptr[rdx + rbp + UnitXOffset]);
            assembler.movsx(ecx, __word_ptr[rdx + rbp + UnitYOffset]);
            assembler.movsx(eax, __word_ptr[rbx + rbp + BuildingEndXOffset]);
            assembler.add(eax, r15d);
            assembler.shl(eax, 2);
            assembler.sub(eax, r8d);
            assembler.cdq();
            assembler.xor(eax, edx);
            assembler.sub(eax, edx);
            assembler.mov(r8d, eax);
            assembler.movsx(eax, __word_ptr[rbx + rbp + BuildingEndYOffset]);
            assembler.add(eax, r12d);
            assembler.shl(eax, 2);
            assembler.sub(eax, ecx);
            assembler.cdq();
            assembler.xor(eax, edx);
            assembler.sub(eax, edx);
            assembler.cmp(r8d, eax);
            assembler.cmovl(r8d, eax);
            assembler.jmp(done);

            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in instructions)
                assembler.AddInstruction(instruction);
            assembler.Label(ref done);
            assembler.nop();
        }

        internal static void GenerateDecision(
            Assembler assembler,
            Instruction[] instructions,
            ulong timingPointerAddress,
            ulong moduleBase,
            ulong returnAddress,
            ulong closePathAddress)
        {
            if (instructions.Length == 0)
                throw new InvalidOperationException("The gatehouse decision hook displaced no instructions.");
            Label human = assembler.CreateLabel("gatehouseHumanDecision");
            Label noClose = assembler.CreateLabel("gatehouseNoClose");
            assembler.mov(rax, timingPointerAddress);
            assembler.mov(rax, __qword_ptr[rax]);
            assembler.test(sil, sil);
            assembler.jne(human);
            assembler.cmp(r8d, __dword_ptr[rax]);
            assembler.jge(noClose);
            assembler.mov(eax, __dword_ptr[rax + 4]);
            assembler.AddUnrestrictedJmp(closePathAddress);

            assembler.Label(ref human);
            assembler.cmp(r8d, __dword_ptr[rax + 8]);
            assembler.jge(noClose);
            assembler.mov(eax, __dword_ptr[rax + 12]);
            assembler.AddUnrestrictedJmp(closePathAddress);

            assembler.Label(ref noClose);
            assembler.mov(rax, moduleBase);
            assembler.mov(rbp, rax);
            assembler.AddUnrestrictedJmp(returnAddress);
        }
    }
}

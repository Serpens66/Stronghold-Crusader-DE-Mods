// Process-lifetime, host-controlled baker/miller pause transitions.
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class WorkerBreakPauseHook
    {
        internal const string NativeSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int DisplacedLength = 18;
        private const int MillerRva = 0x1383F3;
        private const int MillerTargetRva = 0x138467;
        private const int BakerRva = 0x139596;
        private const int BakerTargetRva = 0x139651;
        private static readonly byte[] MillerOriginal =
        {
            0x74, 0x72, 0x8B, 0x15, 0xC9, 0x7E, 0x7F, 0x00, 0x49,
            0x8B, 0xCF, 0x48, 0x69, 0xDF, 0x2C, 0x03, 0x00, 0x00
        };
        private static readonly byte[] BakerOriginal =
        {
            0x0F, 0x84, 0xB5, 0x00, 0x00, 0x00, 0x48, 0x69, 0xFD,
            0x2C, 0x03, 0x00, 0x00, 0x44, 0x89, 0x64, 0x24, 0x20
        };

        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private readonly IntPtr enabledFlag;
        private bool afterStartupLogged;

        internal WorkerBreakPauseHook(ManualLogSource log, ScanRegion region,
            ReadOnlySpan<byte> memory, ulong imageBase, bool nativeHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!nativeHashMatches || imageBase == 0)
                throw new InvalidOperationException("Worker-break hooks require the audited native DLL hash.");
            ValidateSite(memory, imageBase, MillerRva, MillerTargetRva, MillerOriginal);
            ValidateSite(memory, imageBase, BakerRva, BakerTargetRva, BakerOriginal);

            enabledFlag = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(enabledFlag, 0);
            HookTransaction candidate = null;
            try
            {
                candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                ulong flagAddress = unchecked((ulong)enabledFlag.ToInt64());
                var miller = new HookHandle<X64InlineHook>();
                var baker = new HookHandle<X64InlineHook>();
                candidate.AddInline(miller, HookTarget.FromAddress(imageBase + MillerRva),
                    (assembler, instructions, _) => EmitConditionalBranch(
                        assembler, instructions, flagAddress, imageBase + MillerTargetRva),
                    hookSize: 14);
                candidate.AddInline(baker, HookTarget.FromAddress(imageBase + BakerRva),
                    (assembler, instructions, _) => EmitConditionalBranch(
                        assembler, instructions, flagAddress, imageBase + BakerTargetRva),
                    hookSize: 14);
                CommitResult result = candidate.Commit();
                if (!result.IsCompleteSuccess || !miller.Success || !baker.Success ||
                    !miller.IsInstalled || !baker.IsInstalled ||
                    miller.Hook.DisplacedByteCount != DisplacedLength ||
                    baker.Hook.DisplacedByteCount != DisplacedLength)
                    throw new InvalidOperationException("Worker-break hooks failed the atomic install or displacement contract.");
                transaction = candidate;
                candidate = null;
            }
            catch
            {
                // Only an unpublished initialization candidate may be rolled back.
                candidate?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
                throw;
            }
        }

        internal void SetEnabled(bool enabled)
        {
            Thread.MemoryBarrier();
            Marshal.WriteInt32(enabledFlag, enabled ? 1 : 0);
            Thread.MemoryBarrier();
        }

        internal void LogAfterStartup(int tick)
        {
            if (afterStartupLogged) return;
            afterStartupLogged = true;
            Shared.DebugLogHelper.LogInfo(log,
                "BUGFIXES_AND_QOL_WORKER_BREAK_READY: baker/miller hooks active after startup; tick=" + tick);
        }

        internal static void ValidateSite(ReadOnlySpan<byte> memory, ulong imageBase,
            int rva, int targetRva, byte[] expected)
        {
            if (memory.Length < rva + expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
                throw new InvalidOperationException($"Worker-break Vanilla bytes changed at RVA 0x{rva:X}.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(expected));
            decoder.IP = imageBase + (uint)rva;
            Instruction branch = decoder.Decode();
            if (branch.IsInvalid || branch.FlowControl != FlowControl.ConditionalBranch ||
                branch.NearBranchTarget != imageBase + (uint)targetRva)
                throw new InvalidOperationException($"Worker-break branch target changed at RVA 0x{rva:X}.");
            int decoded = branch.Length;
            while (decoded < expected.Length)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid) throw new InvalidOperationException("Invalid displaced worker instruction.");
                decoded += instruction.Length;
            }
            if (decoded != DisplacedLength)
                throw new InvalidOperationException("Worker-break hook does not end on an instruction boundary.");
        }

        internal static void EmitConditionalBranch(Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions, ulong enabledFlagAddress, ulong target)
        {
            NativeInstructionReplayEmitter.EmitConditionalWorkerBreak(
                assembler, overwrittenInstructions, enabledFlagAddress, target);
        }
    }
}

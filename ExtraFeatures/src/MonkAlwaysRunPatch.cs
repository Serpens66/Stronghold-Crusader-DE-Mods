// Feature: Let monks use the ordinary troop walk/run decision when configured.
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal sealed class MonkAlwaysRunPatch
    {
        // Monk handler movement decision. Both the Fighting Monk and Temple
        // Guard skin use this same native unit handler; only their material is
        // selected separately before the movement state machine runs.
        private const string MovementDecisionPattern =
            "66 46 39 B4 2B 14 09 00 00 75 22 " +
            "66 46 39 B4 2B 9E 09 00 00 74 17 " +
            "42 C7 84 2B 60 06 00 00 81 00 00 00 " +
            "66 42 89 84 2B 16 09 00 00 EB 11 " +
            "42 89 84 2B 60 06 00 00 " +
            "66 46 89 B4 2B 16 09 00 00";
        private const int MovementDecisionRva = 0x151436;
        private const int InlineDetourSize = 14;
        private const int HookSize = 20;
        private const int MonkHandlerRva = 0x151090;
        private const int MonkHandlerLength = 0xF81;
        private const ulong RunningTargetFromReturnAddress = 0x02;
        private const ulong WalkingTargetFromReturnAddress = 0x19;

        private readonly ManualLogSource log;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> movementDecisionHook =
            new HookHandle<X64InlineHook>();
        private IntPtr enabledFlag;
        private bool enabled;
        private bool disposed;

        public MonkAlwaysRunPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");
            // Hash-bound: the hook span, its branch targets and the absence of incoming branches
            // into the span are audited for this build (ExtraFeaturesNativeTests); an unknown
            // CrusaderDE.dll is not scanned at startup.
            if (!referenceHashMatches)
                throw new InvalidOperationException("Monks Always Run requires the audited CrusaderDE.dll build.");

            int decisionRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MovementDecisionPattern,
                MovementDecisionRva,
                referenceHashMatches,
                "Monk movement decision",
                log).Rva;
            ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
            ValidateHookSpan(memory, imageBase, decisionRva);

            HookTransaction pendingTransaction = null;
            bool commitAttempted = false;
            try
            {
                enabledFlag = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(enabledFlag, 0);

                pendingTransaction = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(region);
                pendingTransaction.AddInline(
                    movementDecisionHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)decisionRva)),
                    (assembler, instructions, returnAddress) =>
                        GenerateMovementDecision(
                            assembler,
                            instructions,
                            returnAddress,
                            unchecked((ulong)enabledFlag.ToInt64())),
                    hookSize: HookSize);
                ExtraFeaturesHookInfrastructure.RootBeforeCommit(this, pendingTransaction);
                commitAttempted = true;
                CommitResult commitResult = pendingTransaction.Commit();

                // The span length is enforced inside Commit: the generator rejects anything but the
                // audited 9/2/9-byte instructions.
                if (!commitResult.IsCompleteSuccess || !movementDecisionHook.Success ||
                    !movementDecisionHook.IsInstalled)
                    throw new InvalidOperationException("The Monk movement decision hook was not installed.");

                transaction = pendingTransaction;
                pendingTransaction = null;
            }
            catch
            {
                if (pendingTransaction != null)
                {
                    try { pendingTransaction.Dispose(); } catch { }
                }
                // After a commit attempt a stub may still read the flag (see RootBeforeCommit).
                if (!commitAttempted && enabledFlag != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(enabledFlag);
                    enabledFlag = IntPtr.Zero;
                }
                throw;
            }

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features Monk movement hook installed disabled: " +
                    $"startRva=0x{decisionRva:X}, endRva=0x{decisionRva + HookSize:X}, " +
                    "instructionLengths=9,2,9, nextRva=0x15144A, " +
                    "incomingInteriorTargets=0, skins=FightingMonk/TempleGuard.");
            Shared.CrashBreadcrumbDiagnostics.Record(
                "MonkMovementHookInstalled",
                decisionRva,
                HookSize);
        }

        public void SetEnabled(bool value)
        {
            ThrowIfDisposed();
            if (enabled == value)
                return;

            Marshal.WriteInt32(enabledFlag, value ? 1 : 0);
            enabled = value;
            Shared.CrashBreadcrumbDiagnostics.Record(
                "MonkMovementSetting",
                value ? 1 : 0);
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/src/MonkAlwaysRunPatch.cs:127", $"Extra Features Monks Always Run is now {(value ? "enabled" : "disabled")}; " +
                    "the native Monk movement decision applies to both skins.");
        }

        // Permanent logical switch-off for this process (later calls throw); the published hook
        // stays installed until process exit.
        public void Deactivate()
        {
            if (disposed)
                return;

            if (enabledFlag != IntPtr.Zero)
                Marshal.WriteInt32(enabledFlag, 0);
            enabled = false;
            disposed = true;
        }

        private static void GenerateMovementDecision(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong enabledFlagAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 9 ||
                overwrittenInstructions[1].Length != 2 ||
                overwrittenInstructions[2].Length != 9)
            {
                throw new InvalidOperationException("Unexpected Monk movement hook boundary.");
            }

            Label vanilla = assembler.CreateLabel("monkVanillaMovement");
            Label walking = assembler.CreateLabel("monkWalking");
            Label running = assembler.CreateLabel("monkRunning");

            // POP preserves CMP flags. RAX must otherwise remain 1 because the
            // native walking and running blocks both consume EAX/AX.
            assembler.push(rax);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.pop(rax);
            assembler.je(vanilla);

            // Same ordinary Archer decision used by the official Improved
            // Spearmen path, translated to the Monk handler's registers.
            assembler.cmp(__word_ptr[rbx + r13 + 0x914], r14w);
            assembler.jne(walking);
            assembler.movsx(rax, __word_ptr[rbx + r13 + 0x930]);
            assembler.imul(rcx, rax, 0x688);
            assembler.cmp(__word_ptr[rcx + r15 + 0x582], r14w);
            assembler.jne(walking);
            assembler.cmp(__byte_ptr[rbx + r13 + 0xA64], r14b);
            assembler.je(running);
            assembler.jmp(walking);

            // Reproduce the complete overwritten Vanilla decision while the
            // setting is off; no movement-state value is changed by the mod.
            assembler.Label(ref vanilla);
            assembler.cmp(__word_ptr[rbx + r13 + 0x914], r14w);
            assembler.jne(walking);
            assembler.cmp(__word_ptr[rbx + r13 + 0x99E], r14w);
            assembler.je(walking);
            assembler.jmp(running);

            assembler.Label(ref walking);
            assembler.mov(eax, 1);
            assembler.AddUnrestrictedJmp(
                returnAddress + WalkingTargetFromReturnAddress);

            assembler.Label(ref running);
            assembler.mov(eax, 1);
            assembler.AddUnrestrictedJmp(
                returnAddress + RunningTargetFromReturnAddress);
        }

        private static void ValidateHookSpan(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int hookRva)
        {
            if (hookRva < MonkHandlerRva ||
                hookRva + HookSize + 34 > MonkHandlerRva + MonkHandlerLength ||
                MonkHandlerRva + MonkHandlerLength > memory.Length)
            {
                throw new InvalidOperationException("The Monk movement hook is outside its audited handler.");
            }

            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, 64).ToArray()));
            decoder.IP = imageBase + unchecked((ulong)hookRva);
            decoder.Decode(out Instruction first);
            decoder.Decode(out Instruction second);
            decoder.Decode(out Instruction third);
            decoder.Decode(out Instruction following);

            ulong hookStart = imageBase + unchecked((ulong)hookRva);
            ulong hookEnd = hookStart + HookSize;
            bool expected =
                !first.IsInvalid && !second.IsInvalid && !third.IsInvalid && !following.IsInvalid &&
                HookSize >= InlineDetourSize &&
                first.Length == 9 && second.Length == 2 && third.Length == 9 &&
                following.IP == hookEnd && following.Length == 2 &&
                first.Mnemonic == Mnemonic.Cmp &&
                first.FlowControl == FlowControl.Next &&
                first.MemoryBase == Register.RBX && first.MemoryIndex == Register.R13 &&
                first.MemoryDisplacement64 == 0x914 &&
                second.Mnemonic == Mnemonic.Jne &&
                second.FlowControl == FlowControl.ConditionalBranch &&
                second.NearBranchTarget == imageBase + 0x151463UL &&
                third.Mnemonic == Mnemonic.Cmp &&
                third.FlowControl == FlowControl.Next &&
                third.MemoryBase == Register.RBX && third.MemoryIndex == Register.R13 &&
                third.MemoryDisplacement64 == 0x99E &&
                following.Mnemonic == Mnemonic.Je &&
                following.FlowControl == FlowControl.ConditionalBranch &&
                following.NearBranchTarget == imageBase + 0x151463UL &&
                !first.IsIPRelativeMemoryOperand && !third.IsIPRelativeMemoryOperand;
            if (!expected)
                throw new InvalidOperationException("The Monk movement hook span no longer matches its audited semantics.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MonkAlwaysRunPatch));
        }
    }
}

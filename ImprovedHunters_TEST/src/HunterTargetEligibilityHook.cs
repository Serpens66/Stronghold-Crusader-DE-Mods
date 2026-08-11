using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;

namespace ImprovedHunters
{
    internal sealed class HunterTargetEligibilityHook : IDisposable
    {
        // c_game_unit_hunter_query_for_target at RVA 0x18AF00. The anchor
        // includes AliveState and corpse checks so it cannot match the nearby
        // corpse-only query, whose second branch has the opposite condition.
        private const string HunterQueryFlagsComparisonPattern =
            "66 83 BB EC FD FF FF 02 0F 85 ?? ?? ?? ?? 66 83 3B 00 0F 85 ?? ?? ?? ?? " +
            "66 83 BB F6 FD FF FF 00 0F 85 ?? ?? ?? ??";
        private const int HunterQueryLiveCandidatePatternRva = 0x18AF70;
        private const int FlagsComparisonOffsetFromPattern = 0x18;
        private const int HookSize = 14;
        private const int TypeCheckOffsetFromPattern = 0x26;
        private const int ReservationComparisonOffsetFromPattern = 0x3D;
        private const int ReservationAllowOffsetFromPattern = 0x4B;
        private const int VanillaRejectOffsetFromPattern = 0x11D;
        private const int CandidateFlagsOffsetFromRbx = -0x20A;
        private const int CandidateTypeOffsetFromRbx = -0x212;
        private const int CandidateGlobalIdOffsetFromRbx = -0x208;
        private const int CandidateReservationOffsetFromRbx = 0x1AC;
        private const int CandidateGameUnitOffsetFromRbx = -0x29C;
        private const int HunterAiStateOffsetFromR13 = 0x918;
        private const int HunterTargetUnitIdOffsetFromR13 = 0x9F6;
        private const int HunterTargetGlobalIdOffsetFromR13 = 0x9F8;
        private const int ChickenType = (int)eChimps.CHIMP_TYPE_CHICKEN;
        // State 1 calls the generic unit-order routine immediately before it
        // chooses state 9 (success) or state 6 (failure). This hook observes
        // only the failure branch; it does not alter the branch decision.
        private const string HunterState6TransitionPattern =
            "48 69 CA 90 04 00 00 41 BF 14 00 00 00 B8 06 00 00 00 BE 01 00 00 00 " +
            "66 46 89 BC 29 20 09 00 00 66 42 89 84 29 18 09 00 00";
        private const int HunterState6TransitionRva = 0x130171;
        private const int HunterState6HookSize = 14;
        private const int HunterState6OverwrittenSize = 18;
        private const int HunterPathStateOffset = 0xF2;
        private const int HunterPathState2Offset = 0xF4;
        private const int HunterLastCommandOffset = 0x398;
        private const int HunterOrderBlockedOffset = 0x3FE;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int CandidateReservationOffset = 0x448;
        // c_game_unit_issue_order routes same-owner targets into its generic
        // friendly-unit path before reaching the native Hunter special case.
        // Only a Hunter's own chicken needs the native Hunter-path exception.
        private const string HunterOrderRelationPattern =
            "49 0F BF 8C 28 EE 06 00 00 4C 8D 0D ?? ?? ?? ?? " +
            "43 8B 84 A9 3C CF 7E 03 41 39 84 89 3C CF 7E 03 " +
            "0F 84 ?? ?? ?? ?? 41 83 FC 06 74 ??";
        private const int HunterOrderRelationPatternRva = 0x18EB72;
        private const int HunterOrderRelationHookOffset = 0x10;
        private const int HunterOrderRelationHookSize = 16;
        private const int HunterOrderRelationReturnOffset = 0x20;
        private const int HunterOrderRelationContinueOffset = 0x26;
        private const int HunterOrderRelationVanillaEquivalentOffset = 0xE5;
        private const int HunterOrderRelationHuntPathOffset = 0x60;
        // c_game_unit_issue_order reaches this block after its internal helper at
        // RVA 0xA06F0 returned <= 0. Hunters then return zero to their state-1 AI.
        private const string HunterOrderHelperFailurePattern =
            "66 42 83 BC 26 E6 06 00 00 06 0F 84 ?? ?? ?? ?? B8 FE FF FF FF";
        private const int HunterOrderHelperFailureRva = 0x18EE14;
        private const int HunterOrderHelperFailureHookSize = 14;
        private const int HunterOrderHelperFailureOverwrittenSize = 16;
        private const int HunterOrderZeroReturnRva = 0x18F928;
        private const int HunterOrderInternalHelperRva = 0xA06F0;
        private const int NativeUnitTypeOffsetFromManagerSlot = 0x6E6;
        private const int NativeUnitOwnerOffsetFromManagerSlot = 0x6EE;
        private const int NativeUnitRawBound0Offset = 0xB2;
        private const int NativeUnitRawBound1Offset = 0xB4;
        private const int NativeUnitRawBound2Offset = 0xB6;
        private const int NativeUnitRawBound3Offset = 0xB8;
        private const int HunterType = (int)eChimps.CHIMP_TYPE_HUNTER;
        private const int NativeContextToGameUnitOffset = 0x65C;
        private static readonly long RecentChickenDiagnosticLifetime = Stopwatch.Frequency * 10;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HookTransaction transaction;
        private readonly IntPtr enabledFlagAddress;
        private readonly object recentChickenTargetsLock = new object();
        private readonly Dictionary<int, RecentChickenTarget> recentChickenTargets = new Dictionary<int, RecentChickenTarget>();
        private HookRef<X64InlineHook> flagsComparisonHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> reservationComparisonHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> orderRelationHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> orderHelperFailureDiagnosticHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> state6DiagnosticHook = new HookRef<X64InlineHook>();
        private readonly ReservationDiagnosticDelegate reservationDiagnosticCallback;
        private readonly OrderRelationBypassDiagnosticDelegate orderRelationBypassDiagnosticCallback;
        private readonly OrderHelperFailureDiagnosticDelegate orderHelperFailureDiagnosticCallback;
        private readonly State6DiagnosticDelegate state6DiagnosticCallback;
        private int reservationDiagnosticLogs;
        private bool reservationDiagnosticFailureLogged;
        private int orderRelationBypassDiagnosticLogs;
        private bool orderRelationBypassDiagnosticFailureLogged;
        private int orderHelperFailureDiagnosticLogs;
        private bool orderHelperFailureDiagnosticFailureLogged;
        private int state6DiagnosticLogs;
        private bool state6DiagnosticFailureLogged;
        private bool disposed;

        private readonly struct RecentChickenTarget
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly long Timestamp;

            public RecentChickenTarget(int unitId, uint globalId, long timestamp)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Timestamp = timestamp;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void State6DiagnosticDelegate(ulong hunterUnitId, ulong issueOrderResult);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ReservationDiagnosticDelegate(
            ulong hunterUnitAddress,
            ulong candidateUnitAddress,
            ulong candidateUnitId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OrderRelationBypassDiagnosticDelegate(ulong hunterUnitId, ulong chickenUnitId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OrderHelperFailureDiagnosticDelegate(
            ulong hunterUnitId,
            ulong hunterUnitAddress,
            ulong chickenUnitAddress,
            ulong helperResult);

        public HunterTargetEligibilityHook(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            reservationDiagnosticCallback = LogReservedChickenFilterContext;
            orderRelationBypassDiagnosticCallback = LogOrderRelationBypass;
            orderHelperFailureDiagnosticCallback = LogOrderHelperFailure;
            state6DiagnosticCallback = LogChickenState6Transition;

            if (!referenceHashMatches)
                throw new InvalidOperationException("The Hunter target eligibility hook requires the audited CrusaderDE.dll hash.");

            int patternRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterQueryFlagsComparisonPattern,
                HunterQueryLiveCandidatePatternRva,
                referenceHashMatches,
                "hunter query live-candidate filter",
                log).Rva;
            int hookRva = checked(patternRva + FlagsComparisonOffsetFromPattern);
            ulong hookAddress = imageBase + unchecked((ulong)hookRva);
            ulong typeCheckAddress = imageBase + unchecked((ulong)(patternRva + TypeCheckOffsetFromPattern));
            int reservationHookRva = checked(patternRva + ReservationComparisonOffsetFromPattern);
            ulong reservationHookAddress = imageBase + unchecked((ulong)reservationHookRva);
            ulong reservationAllowAddress = imageBase + unchecked((ulong)(patternRva + ReservationAllowOffsetFromPattern));
            ulong vanillaRejectAddress = imageBase + unchecked((ulong)(patternRva + VanillaRejectOffsetFromPattern));
            ulong reservationDiagnosticCallbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                reservationDiagnosticCallback).ToInt64());
            int orderRelationPatternRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterOrderRelationPattern,
                HunterOrderRelationPatternRva,
                referenceHashMatches,
                "Hunter order same-relation branch",
                log).Rva;
            int orderRelationHookRva = checked(orderRelationPatternRva + HunterOrderRelationHookOffset);
            ulong orderRelationHookAddress = imageBase + unchecked((ulong)orderRelationHookRva);
            ulong orderRelationReturnAddress = imageBase + unchecked((ulong)(
                orderRelationPatternRva + HunterOrderRelationReturnOffset));
            ulong orderRelationContinueAddress = imageBase + unchecked((ulong)(
                orderRelationPatternRva + HunterOrderRelationContinueOffset));
            ulong orderRelationVanillaEquivalentAddress = imageBase + unchecked((ulong)(
                orderRelationPatternRva + HunterOrderRelationVanillaEquivalentOffset));
            ulong orderRelationHuntPathAddress = imageBase + unchecked((ulong)(
                orderRelationPatternRva + HunterOrderRelationHuntPathOffset));
            ulong orderRelationBypassDiagnosticCallbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                orderRelationBypassDiagnosticCallback).ToInt64());
            int orderHelperFailureHookRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterOrderHelperFailurePattern,
                HunterOrderHelperFailureRva,
                referenceHashMatches,
                "Hunter issue-order helper failure block",
                log).Rva;
            ulong orderHelperFailureHookAddress = imageBase + unchecked((ulong)orderHelperFailureHookRva);
            ulong orderHelperFailureReturnAddress = orderHelperFailureHookAddress + HunterOrderHelperFailureOverwrittenSize;
            ulong orderZeroReturnAddress = imageBase + unchecked((ulong)HunterOrderZeroReturnRva);
            ulong orderHelperFailureDiagnosticCallbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                orderHelperFailureDiagnosticCallback).ToInt64());
            int state6HookRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterState6TransitionPattern,
                HunterState6TransitionRva,
                referenceHashMatches,
                "hunter state-6 transition diagnostic",
                log).Rva;
            ulong state6HookAddress = imageBase + unchecked((ulong)state6HookRva);
            ulong state6ReturnAddress = state6HookAddress + HunterState6OverwrittenSize;
            ulong state6DiagnosticCallbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                state6DiagnosticCallback).ToInt64());

            IntPtr pendingEnabledFlagAddress = Marshal.AllocHGlobal(1);
            HookTransaction pendingTransaction = null;
            try
            {
                Marshal.WriteByte(pendingEnabledFlagAddress, GetEnabledFlagValue());
                pendingTransaction = new HookTransaction(
                    memory,
                    imageBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);

                pendingTransaction.AddInline(
                    ref flagsComparisonHook,
                    hookAddress,
                    (assembler, instructions, returnAddress) => GenerateEligibilityFilter(
                        assembler,
                        instructions,
                        returnAddress,
                        typeCheckAddress,
                        vanillaRejectAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64())),
                    hookSize: HookSize);

                pendingTransaction.AddInline(
                    ref reservationComparisonHook,
                    reservationHookAddress,
                    (assembler, instructions, returnAddress) => GenerateReservationFilter(
                        assembler,
                        instructions,
                        returnAddress,
                        reservationAllowAddress,
                        vanillaRejectAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64()),
                        reservationDiagnosticCallbackAddress),
                    hookSize: HookSize);

                pendingTransaction.AddInline(
                    ref orderRelationHook,
                    orderRelationHookAddress,
                    (assembler, instructions, returnAddress) => GenerateOrderRelationFix(
                        assembler,
                        instructions,
                        returnAddress,
                        orderRelationReturnAddress,
                        orderRelationContinueAddress,
                        orderRelationVanillaEquivalentAddress,
                        orderRelationHuntPathAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64()),
                        orderRelationBypassDiagnosticCallbackAddress),
                    hookSize: HunterOrderRelationHookSize);

                pendingTransaction.AddInline(
                    ref orderHelperFailureDiagnosticHook,
                    orderHelperFailureHookAddress,
                    (assembler, instructions, returnAddress) => GenerateOrderHelperFailureDiagnostic(
                        assembler,
                        instructions,
                        returnAddress,
                        orderHelperFailureReturnAddress,
                        orderZeroReturnAddress,
                        orderHelperFailureDiagnosticCallbackAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64())),
                    hookSize: HunterOrderHelperFailureHookSize);

                pendingTransaction.AddInline(
                    ref state6DiagnosticHook,
                    state6HookAddress,
                    (assembler, instructions, returnAddress) => GenerateState6Diagnostic(
                        assembler,
                        instructions,
                        returnAddress,
                        state6ReturnAddress,
                        state6DiagnosticCallbackAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64())),
                    hookSize: HunterState6HookSize);

                pendingTransaction.Commit();
                if (!flagsComparisonHook.Success ||
                    !reservationComparisonHook.Success ||
                    !orderRelationHook.Success ||
                    !orderHelperFailureDiagnosticHook.Success ||
                    !state6DiagnosticHook.Success)
                {
                    throw new InvalidOperationException("The Hunter query eligibility hooks were not installed.");
                }

                enabledFlagAddress = pendingEnabledFlagAddress;
                transaction = pendingTransaction;
                settings.SettingChanged += OnSettingChanged;
            }
            catch
            {
                pendingTransaction?.Unload();
                pendingTransaction?.Dispose();
                Marshal.FreeHGlobal(pendingEnabledFlagAddress);
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters Hunter-query eligibility hook initialized: hookRva=0x{hookRva:X}, " +
                $"hookSize={HookSize}, typeCheckRva=0x{patternRva + TypeCheckOffsetFromPattern:X}, " +
                $"reservationHookRva=0x{reservationHookRva:X}, " +
                $"orderRelationHookRva=0x{orderRelationHookRva:X}, " +
                $"orderHelperFailureDiagnosticHookRva=0x{orderHelperFailureHookRva:X}, " +
                $"state6DiagnosticHookRva=0x{state6HookRva:X}, " +
                $"vanillaRejectRva=0x{patternRva + VanillaRejectOffsetFromPattern:X}, " +
                $"enabled={Marshal.ReadByte(enabledFlagAddress) != 0}.");
        }

        private byte GetEnabledFlagValue()
        {
            return settings.EnableMod && settings.HuntChicken ? (byte)1 : (byte)0;
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(ImprovedHuntersViewModel.EnableMod) &&
                propertyName != nameof(ImprovedHuntersViewModel.HuntChicken))
            {
                return;
            }

            // A naturally aligned single-byte write is atomic on x64. The
            // native stub reads this byte on every non-Vanilla-flags candidate.
            Marshal.WriteByte(enabledFlagAddress, GetEnabledFlagValue());
        }

        private static void GenerateEligibilityFilter(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong typeCheckAddress,
            ulong vanillaRejectAddress,
            ulong enabledFlagAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[0].Length != 8 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Jne ||
                overwrittenInstructions[1].Length != 6 ||
                returnAddress != typeCheckAddress)
            {
                throw new InvalidOperationException("Unexpected Hunter query flags-filter hook boundary.");
            }

            Label allowTypeCheck = assembler.CreateLabel("hunterAllowTypeCheck");
            Label rejectCandidate = assembler.CreateLabel("hunterRejectCandidate");

            // Preserve Vanilla exactly for candidates whose flags word is zero.
            assembler.cmp(__word_ptr[rbx + CandidateFlagsOffsetFromRbx], 0);
            assembler.je(allowTypeCheck);

            // Non-neutral live chickens need to reach the managed type callback
            // before their exact owner relation is known. Later exceptions are
            // restricted to an exact Hunter/chicken owner match.
            assembler.cmp(__word_ptr[rbx + CandidateTypeOffsetFromRbx], ChickenType);
            assembler.jne(rejectCandidate);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__byte_ptr[rax], 0);
            assembler.je(rejectCandidate);

            // Owner 0 must retain the exact pre-Feature-01 eligibility behavior.
            assembler.cmp(__byte_ptr[rbx + CandidateFlagsOffsetFromRbx], 0);
            assembler.je(rejectCandidate);

            assembler.Label(ref allowTypeCheck);
            assembler.AddUnrestrictedJmp(typeCheckAddress);

            assembler.Label(ref rejectCandidate);
            assembler.AddUnrestrictedJmp(vanillaRejectAddress);
        }

        private static void GenerateReservationFilter(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong reservationAllowAddress,
            ulong vanillaRejectAddress,
            ulong enabledFlagAddress,
            ulong diagnosticCallbackAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[0].Length != 8 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Jne ||
                overwrittenInstructions[1].Length != 6 ||
                returnAddress != reservationAllowAddress)
            {
                throw new InvalidOperationException("Unexpected Hunter query reservation-filter hook boundary.");
            }

            Label allowCandidate = assembler.CreateLabel("hunterAllowReservedCurrentChicken");
            Label rejectCandidate = assembler.CreateLabel("hunterRejectReservedCandidate");

            // Vanilla remains unchanged for every unreserved candidate.
            assembler.cmp(__word_ptr[rbx + CandidateReservationOffsetFromRbx], 0);
            assembler.je(allowCandidate);

            // Chickens do not clear reservation 2 before the Hunter's close-range
            // re-query. Admit only the exact unit/global pair already owned by this
            // Hunter; a different Hunter or a recycled unit slot still fails.
            assembler.cmp(__word_ptr[rbx + CandidateReservationOffsetFromRbx], 2);
            assembler.jne(rejectCandidate);
            assembler.cmp(__word_ptr[rbx + CandidateTypeOffsetFromRbx], ChickenType);
            assembler.jne(rejectCandidate);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__byte_ptr[rax], 0);
            assembler.je(rejectCandidate);

            // Owner 0 and every foreign owner retain the proven Vanilla path.
            // Apply Feature 01's reservation-2 exception only to the Hunter's
            // own chicken.
            assembler.cmp(__byte_ptr[rbx + CandidateFlagsOffsetFromRbx], 0);
            assembler.je(rejectCandidate);
            assembler.movzx(eax, __byte_ptr[r13 + NativeUnitOwnerOffsetFromManagerSlot]);
            assembler.cmp(__byte_ptr[rbx + CandidateFlagsOffsetFromRbx], al);
            assembler.jne(rejectCandidate);

            // This managed call is limited to enabled reservation-2 chickens.
            // It records the exact transient values before the allow decision.
            assembler.X64FastcallSafeEx(
                diagnosticCallbackAddress,
                totalArgumentCount: 3,
                prepareArgumentsAction: static a =>
                {
                    a.lea(rcx, __qword_ptr[r13 + NativeContextToGameUnitOffset]);
                    a.lea(rdx, __qword_ptr[rbx + CandidateGameUnitOffsetFromRbx]);
                    a.mov(r8d, esi);
                });

            assembler.cmp(__word_ptr[r13 + HunterAiStateOffsetFromR13], 1);
            assembler.jne(rejectCandidate);
            assembler.movzx(eax, __word_ptr[r13 + HunterTargetUnitIdOffsetFromR13]);
            assembler.cmp(esi, eax);
            assembler.jne(rejectCandidate);
            assembler.mov(eax, __dword_ptr[r13 + HunterTargetGlobalIdOffsetFromR13]);
            assembler.cmp(__dword_ptr[rbx + CandidateGlobalIdOffsetFromRbx], eax);
            assembler.jne(rejectCandidate);

            assembler.Label(ref allowCandidate);
            assembler.AddUnrestrictedJmp(reservationAllowAddress);

            assembler.Label(ref rejectCandidate);
            assembler.AddUnrestrictedJmp(vanillaRejectAddress);
        }

        private static void GenerateOrderRelationFix(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong expectedReturnAddress,
            ulong relationDiffersAddress,
            ulong vanillaEquivalentAddress,
            ulong huntPathAddress,
            ulong enabledFlagAddress,
            ulong diagnosticCallbackAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[0].Length != 8 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[1].Length != 8 ||
                returnAddress != expectedReturnAddress)
            {
                throw new InvalidOperationException("Unexpected Hunter order relation hook boundary.");
            }

            Label relationDiffers = assembler.CreateLabel("hunterOrderRelationDiffers");
            Label vanillaEquivalent = assembler.CreateLabel("hunterOrderVanillaEquivalent");

            // Replay the two overwritten relation-table instructions exactly.
            assembler.mov(eax, __dword_ptr[r9 + r13 * 4 + 0x37ECF3C]);
            assembler.cmp(__dword_ptr[r9 + rcx * 4 + 0x37ECF3C], eax);
            assembler.jne(relationDiffers);

            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__byte_ptr[rax], 0);
            assembler.je(vanillaEquivalent);
            assembler.cmp(r12d, HunterType);
            assembler.jne(vanillaEquivalent);
            assembler.cmp(__word_ptr[r8 + rbp + NativeUnitTypeOffsetFromManagerSlot], ChickenType);
            assembler.jne(vanillaEquivalent);

            // Neutral and foreign-owner chickens retain Vanilla's relation
            // handling. Redirect only an exact, nonzero owner match.
            assembler.cmp(__byte_ptr[r8 + rbp + NativeUnitOwnerOffsetFromManagerSlot], 0);
            assembler.je(vanillaEquivalent);
            assembler.movzx(eax, __byte_ptr[r8 + rsi + NativeUnitOwnerOffsetFromManagerSlot]);
            assembler.cmp(__byte_ptr[r8 + rbp + NativeUnitOwnerOffsetFromManagerSlot], al);
            assembler.jne(vanillaEquivalent);

            // R15 and R14 are the stable Hunter and target unit IDs in this
            // function. The helper preserves all volatile state.
            assembler.X64FastcallSafeEx(
                diagnosticCallbackAddress,
                totalArgumentCount: 2,
                prepareArgumentsAction: static a =>
                {
                    a.mov(rcx, r15);
                    a.mov(rdx, r14);
                });
            assembler.AddUnrestrictedJmp(huntPathAddress);

            assembler.Label(ref relationDiffers);
            assembler.AddUnrestrictedJmp(relationDiffersAddress);

            assembler.Label(ref vanillaEquivalent);
            assembler.AddUnrestrictedJmp(vanillaEquivalentAddress);
        }

        private static void GenerateState6Diagnostic(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong expectedReturnAddress,
            ulong diagnosticCallbackAddress,
            ulong enabledFlagAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Imul ||
                overwrittenInstructions[0].Length != 7 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[1].Length != 6 ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[2].Length != 5 ||
                returnAddress != expectedReturnAddress)
            {
                throw new InvalidOperationException("Unexpected Hunter state-6 diagnostic hook boundary.");
            }

            Label skipDiagnostic = assembler.CreateLabel("hunterSkipState6Diagnostic");

            // Keep the disabled path free of managed calls. POP does not alter
            // flags, so the branch still consumes the preceding byte compare.
            assembler.push(r11);
            assembler.mov(r11, enabledFlagAddress);
            assembler.cmp(__byte_ptr[r11], 0);
            assembler.pop(r11);
            assembler.je(skipDiagnostic);

            // At this branch EDX is the Hunter ID and EAX still contains the
            // generic issue-order result. Preserve both and every volatile
            // register around the managed diagnostic callback.
            assembler.X64FastcallSafeEx(
                diagnosticCallbackAddress,
                totalArgumentCount: 2,
                prepareArgumentsAction: static a =>
                {
                    a.mov(rcx, rdx);
                    a.mov(rdx, rax);
                });

            assembler.Label(ref skipDiagnostic);
            assembler.imul(rcx, rdx, 0x490);
            assembler.mov(r15d, 20);
            assembler.mov(eax, 6);
        }

        private static void GenerateOrderHelperFailureDiagnostic(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong expectedReturnAddress,
            ulong zeroReturnAddress,
            ulong diagnosticCallbackAddress,
            ulong enabledFlagAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[0].Length != 10 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Je ||
                overwrittenInstructions[1].Length != 6 ||
                returnAddress != expectedReturnAddress)
            {
                throw new InvalidOperationException("Unexpected Hunter issue-order helper-failure hook boundary.");
            }

            Label skipDiagnostic = assembler.CreateLabel("hunterSkipOrderHelperFailureDiagnostic");
            Label returnZero = assembler.CreateLabel("hunterOrderHelperFailureReturnZero");

            assembler.push(r11);
            assembler.mov(r11, enabledFlagAddress);
            assembler.cmp(__byte_ptr[r11], 0);
            assembler.pop(r11);
            assembler.je(skipDiagnostic);
            assembler.cmp(__word_ptr[r12 + rsi + NativeUnitTypeOffsetFromManagerSlot], HunterType);
            assembler.jne(skipDiagnostic);
            assembler.cmp(__word_ptr[r12 + rbp + NativeUnitTypeOffsetFromManagerSlot], ChickenType);
            assembler.jne(skipDiagnostic);

            // EDX is the signed result returned by the internal helper. R12 is
            // the manager base; RSI/RBP are the source/target slot offsets.
            assembler.X64FastcallSafeEx(
                diagnosticCallbackAddress,
                totalArgumentCount: 4,
                prepareArgumentsAction: static a =>
                {
                    a.mov(r9, rdx);
                    a.mov(rcx, r15);
                    a.lea(rdx, __qword_ptr[r12 + rsi + NativeContextToGameUnitOffset]);
                    a.lea(r8, __qword_ptr[r12 + rbp + NativeContextToGameUnitOffset]);
                });

            assembler.Label(ref skipDiagnostic);
            assembler.cmp(__word_ptr[r12 + rsi + NativeUnitTypeOffsetFromManagerSlot], HunterType);
            assembler.je(returnZero);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref returnZero);
            assembler.AddUnrestrictedJmp(zeroReturnAddress);
        }

        private unsafe void LogReservedChickenFilterContext(
            ulong hunterUnitAddress,
            ulong candidateUnitAddress,
            ulong candidateUnitIdValue)
        {
            if (!settings.EnableMod || !settings.HuntChicken || reservationDiagnosticLogs >= 16)
                return;

            try
            {
                if (hunterUnitAddress == 0 ||
                    candidateUnitAddress == 0 ||
                    candidateUnitIdValue == 0 ||
                    candidateUnitIdValue > int.MaxValue)
                {
                    return;
                }

                GameUnit* hunter = (GameUnit*)hunterUnitAddress;
                GameUnit* candidate = (GameUnit*)candidateUnitAddress;
                if (hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    candidate->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
                {
                    return;
                }

                int candidateUnitId = (int)candidateUnitIdValue;
                byte* hunterBytes = (byte*)hunter;
                byte* candidateBytes = (byte*)candidate;
                ushort hunterState = *(ushort*)(hunterBytes + 0x2BC);
                ushort hunterTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
                uint hunterTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
                ushort reservation = *(ushort*)(candidateBytes + CandidateReservationOffset);
                bool ownersMatch = candidate->r_ControllableForPlayerId != 0 &&
                    candidate->r_ControllableForPlayerId == hunter->r_ControllableForPlayerId;
                bool stateMatches = hunterState == 1;
                bool unitIdMatches = hunterTargetUnitId == candidateUnitId;
                bool globalIdMatches = hunterTargetGlobalId == candidate->r_GlobalId;
                bool willAllow = reservation == 2 && ownersMatch && stateMatches && unitIdMatches && globalIdMatches;
                reservationDiagnosticLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters reserved-chicken filter: candidate={candidateUnitId}/{candidate->r_GlobalId}, " +
                    $"owner={candidate->r_ControllableForPlayerId}, reservation={reservation}, " +
                    $"hunter={hunter->r_GlobalId}, hunterOwner={hunter->r_ControllableForPlayerId}, hunterState=0x{hunterState:X}, " +
                    $"hunterTarget={hunterTargetUnitId}/{hunterTargetGlobalId}, stateMatches={stateMatches}, " +
                    $"ownersMatch={ownersMatch}, unitIdMatches={unitIdMatches}, globalIdMatches={globalIdMatches}, willAllow={willAllow} " +
                    $"({reservationDiagnosticLogs}/16).");
            }
            catch (Exception exception)
            {
                if (reservationDiagnosticFailureLogged)
                    return;

                reservationDiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters reserved-chicken filter diagnostic failed; eligibility behavior is unchanged: {exception}");
            }
        }

        private unsafe void LogOrderRelationBypass(ulong hunterUnitIdValue, ulong chickenUnitIdValue)
        {
            if (!settings.EnableMod || !settings.HuntChicken || orderRelationBypassDiagnosticLogs >= 24)
                return;

            try
            {
                if (hunterUnitIdValue == 0 || hunterUnitIdValue > int.MaxValue ||
                    chickenUnitIdValue == 0 || chickenUnitIdValue > int.MaxValue)
                {
                    return;
                }

                int hunterUnitId = (int)hunterUnitIdValue;
                int chickenUnitId = (int)chickenUnitIdValue;
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    !unitApi.TryGetUnitById(chickenUnitId, out GameUnit* chicken) ||
                    hunter == null || chicken == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                    hunter->r_ControllableForPlayerId == 0 ||
                    hunter->r_ControllableForPlayerId != chicken->r_ControllableForPlayerId)
                {
                    return;
                }

                byte* chickenBytes = (byte*)chicken;
                int distance = Math.Max(
                    Math.Abs(chicken->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                    Math.Abs(chicken->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                orderRelationBypassDiagnosticLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters same-owner chicken order redirected to native Hunter path: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, hunterOwner={hunter->r_ControllableForPlayerId}, " +
                    $"chicken={chickenUnitId}/{chicken->r_GlobalId}, chickenOwner={chicken->r_ControllableForPlayerId}, " +
                    $"aliveState={(short)chicken->r_AliveState}, reservation={*(ushort*)(chickenBytes + CandidateReservationOffset)}, " +
                    $"distance={distance} ({orderRelationBypassDiagnosticLogs}/24)." );
            }
            catch (Exception exception)
            {
                if (orderRelationBypassDiagnosticFailureLogged)
                    return;

                orderRelationBypassDiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters same-owner chicken-order diagnostic failed; the native redirect remains active: {exception}");
            }
        }

        private unsafe void LogChickenState6Transition(ulong hunterUnitIdValue, ulong issueOrderResult)
        {
            if (!settings.EnableMod || !settings.HuntChicken || state6DiagnosticLogs >= 12)
                return;

            try
            {
                if (hunterUnitIdValue == 0 || hunterUnitIdValue > int.MaxValue)
                    return;

                int hunterUnitId = (int)hunterUnitIdValue;
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    return;
                }

                byte* hunterBytes = (byte*)hunter;
                if (!TryResolveDiagnosticChicken(
                        hunterUnitId,
                        hunter,
                        unitApi,
                        out int targetUnitId,
                        out uint storedTargetGlobalId,
                        out GameUnit* target,
                        out string targetSource))
                    return;

                byte* targetBytes = (byte*)target;
                int distance = Math.Max(
                    Math.Abs(target->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                    Math.Abs(target->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                state6DiagnosticLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters chicken state-6 branch: hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"issueOrderResult={unchecked((int)issueOrderResult)}, aiState=0x{hunter->r_AIState:X}, " +
                    $"pathState={*(ushort*)(hunterBytes + HunterPathStateOffset)}, " +
                    $"pathState2={*(ushort*)(hunterBytes + HunterPathState2Offset)}, " +
                    $"lastCommand={*(ushort*)(hunterBytes + HunterLastCommandOffset)}, " +
                    $"orderBlocked={*(byte*)(hunterBytes + HunterOrderBlockedOffset)}, " +
                    $"target={targetUnitId}/{storedTargetGlobalId}/{target->r_GlobalId}, targetSource={targetSource}, " +
                    $"identityMatches={storedTargetGlobalId == target->r_GlobalId}, owner={target->r_ControllableForPlayerId}, " +
                    $"aliveState={(short)target->r_AliveState}, reservation={*(ushort*)(targetBytes + CandidateReservationOffset)}, " +
                    $"distance={distance} ({state6DiagnosticLogs}/12).");
            }
            catch (Exception exception)
            {
                if (state6DiagnosticFailureLogged)
                    return;

                state6DiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters chicken state-6 diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        private unsafe void LogOrderHelperFailure(
            ulong hunterUnitIdValue,
            ulong hunterUnitAddress,
            ulong chickenUnitAddress,
            ulong helperResultValue)
        {
            if (!settings.EnableMod || !settings.HuntChicken || orderHelperFailureDiagnosticLogs >= 24)
                return;

            try
            {
                if (hunterUnitIdValue == 0 || hunterUnitIdValue > int.MaxValue ||
                    hunterUnitAddress == 0 || chickenUnitAddress == 0)
                {
                    return;
                }

                GameUnit* hunter = (GameUnit*)hunterUnitAddress;
                GameUnit* chicken = (GameUnit*)chickenUnitAddress;
                if (hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
                {
                    return;
                }

                int hunterUnitId = (int)hunterUnitIdValue;
                int helperResult = unchecked((int)helperResultValue);
                byte* hunterBytes = (byte*)hunter;
                byte* chickenBytes = (byte*)chicken;
                ushort storedTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
                uint storedTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
                int distance = Math.Max(
                    Math.Abs(chicken->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                    Math.Abs(chicken->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                orderHelperFailureDiagnosticLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters Hunter-order internal helper rejected chicken: " +
                    $"helperRva=0x{HunterOrderInternalHelperRva:X}, helperResult={helperResult}, " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, hunterOwner={hunter->r_ControllableForPlayerId}, " +
                    $"hunterAiState=0x{hunter->r_AIState:X}, pathState={*(ushort*)(hunterBytes + HunterPathStateOffset)}, " +
                    $"pathState2={*(ushort*)(hunterBytes + HunterPathState2Offset)}, " +
                    $"lastCommand={*(ushort*)(hunterBytes + HunterLastCommandOffset)}, " +
                    $"orderBlocked={*(byte*)(hunterBytes + HunterOrderBlockedOffset)}, " +
                    $"storedTarget={storedTargetUnitId}/{storedTargetGlobalId}, chicken={chicken->r_GlobalId}, " +
                    $"identityMatches={storedTargetGlobalId == chicken->r_GlobalId}, " +
                    $"chickenOwner={chicken->r_ControllableForPlayerId}, chickenColor={chicken->r_SpritePlayerColorId}, " +
                    $"aliveState={(short)chicken->r_AliveState}, health={chicken->r_CurrentHealth}/{chicken->r_MaxHealth}, " +
                    $"flags92={*(ushort*)(chickenBytes + 0x92)}, aiState=0x{chicken->r_AIState:X}, " +
                    $"reservation={*(ushort*)(chickenBytes + CandidateReservationOffset)}, " +
                    $"hunterTile={hunter->r_CurrentTilePositionX},{hunter->r_CurrentTilePositionY}, " +
                    $"chickenTile={chicken->r_CurrentTilePositionX},{chicken->r_CurrentTilePositionY}, distance={distance}, " +
                    $"hunterRawBounds={*(short*)(hunterBytes + NativeUnitRawBound0Offset)}/" +
                    $"{*(short*)(hunterBytes + NativeUnitRawBound1Offset)}/" +
                    $"{*(short*)(hunterBytes + NativeUnitRawBound2Offset)}/" +
                    $"{*(short*)(hunterBytes + NativeUnitRawBound3Offset)}, " +
                    $"chickenRawBounds={*(short*)(chickenBytes + NativeUnitRawBound0Offset)}/" +
                    $"{*(short*)(chickenBytes + NativeUnitRawBound1Offset)}/" +
                    $"{*(short*)(chickenBytes + NativeUnitRawBound2Offset)}/" +
                    $"{*(short*)(chickenBytes + NativeUnitRawBound3Offset)} " +
                    $"({orderHelperFailureDiagnosticLogs}/24).");
            }
            catch (Exception exception)
            {
                if (orderHelperFailureDiagnosticFailureLogged)
                    return;

                orderHelperFailureDiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Hunter-order internal-helper diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        internal void RecordAcceptedChickenTarget(int hunterUnitId, int chickenUnitId, uint chickenGlobalId)
        {
            if (hunterUnitId <= 0 || chickenUnitId <= 0 || chickenGlobalId == 0)
                return;

            lock (recentChickenTargetsLock)
            {
                recentChickenTargets[hunterUnitId] = new RecentChickenTarget(
                    chickenUnitId,
                    chickenGlobalId,
                    Stopwatch.GetTimestamp());
            }
        }

        private unsafe bool TryResolveDiagnosticChicken(
            int hunterUnitId,
            GameUnit* hunter,
            GameUnitManagerAPI unitApi,
            out int targetUnitId,
            out uint storedTargetGlobalId,
            out GameUnit* target,
            out string targetSource)
        {
            targetUnitId = 0;
            storedTargetGlobalId = 0;
            target = null;
            targetSource = null;

            byte* hunterBytes = (byte*)hunter;
            ushort liveTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint liveTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            if (liveTargetUnitId != 0 &&
                unitApi.TryGetUnitById(liveTargetUnitId, out GameUnit* liveTarget) &&
                liveTarget != null &&
                liveTarget->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN)
            {
                targetUnitId = liveTargetUnitId;
                storedTargetGlobalId = liveTargetGlobalId;
                target = liveTarget;
                targetSource = "native-target";
                return true;
            }

            RecentChickenTarget recent;
            lock (recentChickenTargetsLock)
            {
                if (!recentChickenTargets.TryGetValue(hunterUnitId, out recent) ||
                    Stopwatch.GetTimestamp() - recent.Timestamp > RecentChickenDiagnosticLifetime)
                {
                    recentChickenTargets.Remove(hunterUnitId);
                    return false;
                }
            }

            if (!unitApi.TryGetUnitById(recent.UnitId, out GameUnit* recentTarget) ||
                recentTarget == null ||
                recentTarget->r_GlobalId != recent.GlobalId ||
                recentTarget->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
            {
                return false;
            }

            targetUnitId = recent.UnitId;
            storedTargetGlobalId = recent.GlobalId;
            target = recentTarget;
            targetSource = "recent-query-cache-after-native-clear";
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            settings.SettingChanged -= OnSettingChanged;
            lock (recentChickenTargetsLock)
                recentChickenTargets.Clear();
            transaction.Unload();
            transaction.Dispose();
            Marshal.FreeHGlobal(enabledFlagAddress);
        }
    }
}

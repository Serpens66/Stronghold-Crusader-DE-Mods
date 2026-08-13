using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Zhuqiaomon.Extensions;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using static Iced.Intel.AssemblerRegisters;

namespace ImprovedHunters
{
    /// <summary>
    /// Temporary, behavior-neutral diagnostics for Hunter/chicken visibility and
    /// order failures. Keep this isolated so the complete probe can be removed
    /// after the native movement transition has been understood.
    /// </summary>
    internal sealed unsafe class HunterVisibilityDiagnostic : IDisposable
    {
        // c_game_unit_issue_order reaches this basic block after its internal
        // geometry/order helper at RVA 0xA06F0 returned <= 0.
        private const string HunterOrderHelperFailurePattern =
            "66 42 83 BC 26 E6 06 00 00 06 0F 84 ?? ?? ?? ?? B8 FE FF FF FF";
        private const int HunterOrderHelperFailureRva = 0x18EE14;
        private const int HunterOrderHelperFailureHookSize = 14;
        private const int HunterOrderHelperFailureOverwrittenSize = 16;
        private const int HunterOrderZeroReturnRva = 0x18F928;
        private const int HunterOrderInternalHelperRva = 0xA06F0;

        // State 1 calls c_game_unit_issue_order immediately before choosing
        // state 9 (success) or state 6 (failure). This is the safe state-6 block.
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
        private const int ChickenReservationOffset = 0x448;
        private const int NativeUnitTypeOffsetFromManagerSlot = 0x6E6;
        private const int NativeContextToGameUnitOffset = 0x65C;
        private const int HunterType = (int)eChimps.CHIMP_TYPE_HUNTER;
        private const int ChickenType = (int)eChimps.CHIMP_TYPE_CHICKEN;
        private const int MaxOrderHelperLogs = 80;
        private const int MaxState6Logs = 80;
        private const int MaxProjectileLogs = 80;
        private const int MaxLineTiles = 160;
        private static readonly long RecentTargetLifetime = Stopwatch.Frequency * 10;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly object recentTargetsLock = new object();
        private readonly Dictionary<int, RecentChickenTarget> recentAssignedTargets =
            new Dictionary<int, RecentChickenTarget>();
        private readonly Dictionary<int, RecentChickenTarget> recentAcceptedTargets =
            new Dictionary<int, RecentChickenTarget>();
        private readonly OrderHelperFailureDiagnosticDelegate orderHelperFailureCallback;
        private readonly State6DiagnosticDelegate state6Callback;
        private HookRef<X64InlineHook> orderHelperFailureHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> state6Hook = new HookRef<X64InlineHook>();
        private HookTransaction transaction;
        private IntPtr enabledFlagAddress;
        private int orderHelperLogs;
        private int state6Logs;
        private int projectileLogs;
        private bool actorMatchLogged;
        private bool actorMissLogged;
        private bool orderHelperHookConfirmed;
        private bool state6HookConfirmed;
        private bool orderHelperFailureLogged;
        private bool state6FailureLogged;
        private bool projectileFailureLogged;
        private bool disposed;

        private readonly struct RecentChickenTarget
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly long Timestamp;
            public readonly string Source;

            public RecentChickenTarget(int unitId, uint globalId, long timestamp, string source)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Timestamp = timestamp;
                Source = source;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OrderHelperFailureDiagnosticDelegate(
            ulong hunterUnitId,
            ulong hunterUnitAddress,
            ulong chickenUnitAddress,
            ulong helperResult);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void State6DiagnosticDelegate(ulong hunterUnitId, ulong issueOrderResult);

        public HunterVisibilityDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (memory.Length == 0 || imageBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            orderHelperFailureCallback = LogOrderHelperFailure;
            state6Callback = LogState6Transition;

            int orderHelperRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterOrderHelperFailurePattern,
                HunterOrderHelperFailureRva,
                referenceHashMatches,
                "Hunter visibility order-helper failure block",
                log).Rva;
            int state6Rva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HunterState6TransitionPattern,
                HunterState6TransitionRva,
                referenceHashMatches,
                "Hunter visibility state-6 transition",
                log).Rva;

            ulong imageEnd = checked(imageBase + (ulong)memory.Length);
            ulong orderHelperAddress = checked(imageBase + (ulong)orderHelperRva);
            ulong state6Address = checked(imageBase + (ulong)state6Rva);
            ulong expectedOrderReturn = checked(orderHelperAddress + HunterOrderHelperFailureOverwrittenSize);
            ulong expectedState6Return = checked(state6Address + HunterState6OverwrittenSize);
            ulong expectedReferenceZeroReturn = referenceHashMatches
                ? checked(imageBase + HunterOrderZeroReturnRva)
                : 0;
            ulong orderHelperCallbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                orderHelperFailureCallback).ToInt64());
            ulong state6CallbackAddress = unchecked((ulong)Marshal.GetFunctionPointerForDelegate(
                state6Callback).ToInt64());

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
                    ref orderHelperFailureHook,
                    orderHelperAddress,
                    (assembler, instructions, returnAddress) => GenerateOrderHelperFailureDiagnostic(
                        assembler,
                        instructions,
                        returnAddress,
                        expectedOrderReturn,
                        imageBase,
                        imageEnd,
                        expectedReferenceZeroReturn,
                        orderHelperCallbackAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64())),
                    hookSize: HunterOrderHelperFailureHookSize);

                pendingTransaction.AddInline(
                    ref state6Hook,
                    state6Address,
                    (assembler, instructions, returnAddress) => GenerateState6Diagnostic(
                        assembler,
                        instructions,
                        returnAddress,
                        expectedState6Return,
                        state6CallbackAddress,
                        unchecked((ulong)pendingEnabledFlagAddress.ToInt64())),
                    hookSize: HunterState6HookSize);

                pendingTransaction.Commit();
                if (!orderHelperFailureHook.Success || !state6Hook.Success)
                    throw new InvalidOperationException("One or more Hunter visibility diagnostic hooks were not installed.");

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
                $"Improved Hunters temporary visibility diagnostic initialized: " +
                $"orderHelperFailureRva=0x{orderHelperRva:X}, state6Rva=0x{state6Rva:X}, " +
                $"behaviorNeutral=True, referenceHashMatches={referenceHashMatches}.");
        }

        public bool IsAvailable =>
            !disposed &&
            transaction != null &&
            orderHelperFailureHook.Success &&
            state6Hook.Success;

        public void RecordActorResolution(
            int reportedHunterUnitId,
            int reconstructedHunterUnitId,
            int queryUnitId,
            bool captureMatched)
        {
            if (!settings.EnableMod)
                return;

            if (captureMatched)
            {
                if (actorMatchLogged)
                    return;

                actorMatchLogged = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters visibility diagnostic actor capture confirmed: " +
                    $"reported={reportedHunterUnitId}, reconstructed={reconstructedHunterUnitId}, " +
                    $"query={queryUnitId}, idsMatch={reportedHunterUnitId == reconstructedHunterUnitId}.");
                return;
            }

            if (actorMissLogged)
                return;

            actorMissLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Improved Hunters visibility diagnostic observed a query without matching native actor capture: " +
                $"reported={reportedHunterUnitId}, query={queryUnitId}; runtime validation decides whether Vanilla is left unchanged.");
        }

        public void RecordAcceptedChickenTarget(int hunterUnitId, int chickenUnitId, uint chickenGlobalId)
        {
            RecordRecentTarget(recentAcceptedTargets, hunterUnitId, chickenUnitId, chickenGlobalId, "accepted-query");
        }

        public void RecordAssignedChickenTarget(int hunterUnitId, int chickenUnitId, uint chickenGlobalId)
        {
            RecordRecentTarget(recentAssignedTargets, hunterUnitId, chickenUnitId, chickenGlobalId, "native-assigned-target");
        }

        public void RecordProjectileSpawn(
            int hunterUnitId,
            int chickenUnitId,
            uint chickenGlobalId,
            long projectileReturnValue,
            string hunterSource)
        {
            if (!settings.EnableMod || !settings.HuntChicken || projectileLogs >= MaxProjectileLogs)
                return;

            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    !TryResolveChicken(unitApi, chickenUnitId, chickenGlobalId, out GameUnit* chicken))
                {
                    return;
                }

                projectileLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters visibility projectile path accepted: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, chicken={chickenUnitId}/{chickenGlobalId}, " +
                    $"hunterSource={hunterSource}, projectileReturnValue={projectileReturnValue}, " +
                    $"hunterPosition={DescribeUnitPosition(hunter)}, chickenPosition={DescribeUnitPosition(chicken)}, " +
                    $"{DescribeLineContext(hunter, chicken)} ({projectileLogs}/{MaxProjectileLogs}).");
            }
            catch (Exception exception)
            {
                if (projectileFailureLogged)
                    return;

                projectileFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters visibility projectile diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        public void ResetForMap()
        {
            lock (recentTargetsLock)
            {
                recentAssignedTargets.Clear();
                recentAcceptedTargets.Clear();
            }

            orderHelperLogs = 0;
            state6Logs = 0;
            projectileLogs = 0;
            actorMatchLogged = false;
            actorMissLogged = false;
            orderHelperHookConfirmed = false;
            state6HookConfirmed = false;
            orderHelperFailureLogged = false;
            state6FailureLogged = false;
            projectileFailureLogged = false;
        }

        private void RecordRecentTarget(
            Dictionary<int, RecentChickenTarget> targets,
            int hunterUnitId,
            int chickenUnitId,
            uint chickenGlobalId,
            string source)
        {
            if (!settings.EnableMod ||
                !settings.HuntChicken ||
                hunterUnitId <= 0 ||
                chickenUnitId <= 0 ||
                chickenGlobalId == 0)
            {
                return;
            }

            lock (recentTargetsLock)
            {
                targets[hunterUnitId] = new RecentChickenTarget(
                    chickenUnitId,
                    chickenGlobalId,
                    Stopwatch.GetTimestamp(),
                    source);
            }
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

            if (enabledFlagAddress != IntPtr.Zero)
                Marshal.WriteByte(enabledFlagAddress, GetEnabledFlagValue());
        }

        private static void GenerateOrderHelperFailureDiagnostic(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong expectedReturnAddress,
            ulong imageBase,
            ulong imageEnd,
            ulong expectedReferenceZeroReturn,
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

            ulong zeroReturnAddress = overwrittenInstructions[1].NearBranchTarget;
            if (zeroReturnAddress < imageBase ||
                zeroReturnAddress >= imageEnd ||
                (expectedReferenceZeroReturn != 0 && zeroReturnAddress != expectedReferenceZeroReturn))
            {
                throw new InvalidOperationException(
                    $"Unexpected Hunter issue-order zero-return target 0x{zeroReturnAddress:X}.");
            }

            Label skipDiagnostic = assembler.CreateLabel("hunterVisibilitySkipOrderHelperDiagnostic");
            Label returnZero = assembler.CreateLabel("hunterVisibilityOrderHelperReturnZero");

            assembler.push(r11);
            assembler.mov(r11, enabledFlagAddress);
            assembler.cmp(__byte_ptr[r11], 0);
            assembler.pop(r11);
            assembler.je(skipDiagnostic);
            assembler.cmp(__word_ptr[r12 + rsi + NativeUnitTypeOffsetFromManagerSlot], HunterType);
            assembler.jne(skipDiagnostic);
            assembler.cmp(__word_ptr[r12 + rbp + NativeUnitTypeOffsetFromManagerSlot], ChickenType);
            assembler.jne(skipDiagnostic);

            // EDX is the signed helper result. R12 is the native manager base;
            // RSI/RBP address the source and target slots.
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

            // Reproduce the overwritten comparison and branch exactly.
            assembler.Label(ref skipDiagnostic);
            assembler.cmp(__word_ptr[r12 + rsi + NativeUnitTypeOffsetFromManagerSlot], HunterType);
            assembler.je(returnZero);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref returnZero);
            assembler.AddUnrestrictedJmp(zeroReturnAddress);
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

            Label skipDiagnostic = assembler.CreateLabel("hunterVisibilitySkipState6Diagnostic");
            assembler.push(r11);
            assembler.mov(r11, enabledFlagAddress);
            assembler.cmp(__byte_ptr[r11], 0);
            assembler.pop(r11);
            assembler.je(skipDiagnostic);

            // EDX is the Hunter ID and EAX is the generic issue-order result.
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

        private void LogOrderHelperFailure(
            ulong hunterUnitIdValue,
            ulong hunterUnitAddress,
            ulong chickenUnitAddress,
            ulong helperResultValue)
        {
            if (!settings.EnableMod || !settings.HuntChicken || orderHelperLogs >= MaxOrderHelperLogs)
                return;

            try
            {
                if (hunterUnitIdValue == 0 ||
                    hunterUnitIdValue > int.MaxValue ||
                    hunterUnitAddress == 0 ||
                    chickenUnitAddress == 0)
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
                ushort targetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
                uint targetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
                int distance = Math.Max(
                    Math.Abs(chicken->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                    Math.Abs(chicken->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                string lineContext = DescribeLineContext(hunter, chicken);

                if (!orderHelperHookConfirmed)
                {
                    orderHelperHookConfirmed = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters visibility diagnostic order-helper hook confirmed: " +
                        $"hunter={hunterUnitId}, chicken={targetUnitId}, helperResult={helperResult}.");
                }

                orderHelperLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters visibility order helper rejected chicken: " +
                    $"helperRva=0x{HunterOrderInternalHelperRva:X}, helperResult={helperResult}, " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, owner={hunter->r_ControllableForPlayerId}, " +
                    $"aiState=0x{hunter->r_AIState:X}, pathState={*(ushort*)(hunterBytes + HunterPathStateOffset)}, " +
                    $"pathState2={*(ushort*)(hunterBytes + HunterPathState2Offset)}, " +
                    $"lastCommand={*(ushort*)(hunterBytes + HunterLastCommandOffset)}, " +
                    $"orderBlocked={*(byte*)(hunterBytes + HunterOrderBlockedOffset)}, " +
                    $"storedTarget={targetUnitId}/{targetGlobalId}, chicken={chicken->r_GlobalId}, " +
                    $"identityMatches={targetGlobalId == chicken->r_GlobalId}, " +
                    $"chickenOwner={chicken->r_ControllableForPlayerId}, color={chicken->r_SpritePlayerColorId}, " +
                    $"aliveState={(short)chicken->r_AliveState}, health={chicken->r_CurrentHealth}/{chicken->r_MaxHealth}, " +
                    $"reservation={*(ushort*)(chickenBytes + ChickenReservationOffset)}, distance={distance}, " +
                    $"hunterPosition={DescribeUnitPosition(hunter)}, chickenPosition={DescribeUnitPosition(chicken)}, " +
                    $"{lineContext} ({orderHelperLogs}/{MaxOrderHelperLogs}).");
            }
            catch (Exception exception)
            {
                if (orderHelperFailureLogged)
                    return;

                orderHelperFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters visibility order-helper diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        private void LogState6Transition(ulong hunterUnitIdValue, ulong issueOrderResult)
        {
            if (!settings.EnableMod || !settings.HuntChicken || state6Logs >= MaxState6Logs)
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

                if (!TryResolveDiagnosticChicken(
                        hunterUnitId,
                        hunter,
                        unitApi,
                        out int targetUnitId,
                        out uint expectedTargetGlobalId,
                        out GameUnit* chicken,
                        out string targetSource))
                {
                    return;
                }

                byte* hunterBytes = (byte*)hunter;
                byte* chickenBytes = (byte*)chicken;
                int distance = Math.Max(
                    Math.Abs(chicken->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                    Math.Abs(chicken->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                string lineContext = DescribeLineContext(hunter, chicken);

                if (!state6HookConfirmed)
                {
                    state6HookConfirmed = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters visibility diagnostic state-6 hook confirmed: " +
                        $"hunter={hunterUnitId}, chicken={targetUnitId}, targetSource={targetSource}.");
                }

                state6Logs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters visibility chicken state-6 transition: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, issueOrderResult={unchecked((int)issueOrderResult)}, " +
                    $"aiState=0x{hunter->r_AIState:X}, pathState={*(ushort*)(hunterBytes + HunterPathStateOffset)}, " +
                    $"pathState2={*(ushort*)(hunterBytes + HunterPathState2Offset)}, " +
                    $"lastCommand={*(ushort*)(hunterBytes + HunterLastCommandOffset)}, " +
                    $"orderBlocked={*(byte*)(hunterBytes + HunterOrderBlockedOffset)}, " +
                    $"target={targetUnitId}/{expectedTargetGlobalId}/{chicken->r_GlobalId}, targetSource={targetSource}, " +
                    $"identityMatches={expectedTargetGlobalId == chicken->r_GlobalId}, " +
                    $"owner={chicken->r_ControllableForPlayerId}, aliveState={(short)chicken->r_AliveState}, " +
                    $"reservation={*(ushort*)(chickenBytes + ChickenReservationOffset)}, distance={distance}, " +
                    $"hunterPosition={DescribeUnitPosition(hunter)}, chickenPosition={DescribeUnitPosition(chicken)}, " +
                    $"{lineContext} ({state6Logs}/{MaxState6Logs}).");
            }
            catch (Exception exception)
            {
                if (state6FailureLogged)
                    return;

                state6FailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters visibility state-6 diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        private bool TryResolveDiagnosticChicken(
            int hunterUnitId,
            GameUnit* hunter,
            GameUnitManagerAPI unitApi,
            out int targetUnitId,
            out uint expectedTargetGlobalId,
            out GameUnit* chicken,
            out string targetSource)
        {
            targetUnitId = 0;
            expectedTargetGlobalId = 0;
            chicken = null;
            targetSource = null;

            byte* hunterBytes = (byte*)hunter;
            ushort nativeTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint nativeTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            if (TryResolveChicken(
                    unitApi,
                    nativeTargetUnitId,
                    nativeTargetGlobalId,
                    out chicken))
            {
                targetUnitId = nativeTargetUnitId;
                expectedTargetGlobalId = nativeTargetGlobalId;
                targetSource = "native-target";
                return true;
            }

            if (TryResolveRecentTarget(
                    recentAssignedTargets,
                    hunterUnitId,
                    unitApi,
                    out RecentChickenTarget recentAssigned,
                    out chicken))
            {
                targetUnitId = recentAssigned.UnitId;
                expectedTargetGlobalId = recentAssigned.GlobalId;
                targetSource = $"{recentAssigned.Source}/ageMs={GetAgeMilliseconds(recentAssigned.Timestamp)}";
                return true;
            }

            if (TryResolveRecentTarget(
                    recentAcceptedTargets,
                    hunterUnitId,
                    unitApi,
                    out RecentChickenTarget recentAccepted,
                    out chicken))
            {
                targetUnitId = recentAccepted.UnitId;
                expectedTargetGlobalId = recentAccepted.GlobalId;
                targetSource = $"{recentAccepted.Source}/ageMs={GetAgeMilliseconds(recentAccepted.Timestamp)}";
                return true;
            }

            return false;
        }

        private bool TryResolveRecentTarget(
            Dictionary<int, RecentChickenTarget> targets,
            int hunterUnitId,
            GameUnitManagerAPI unitApi,
            out RecentChickenTarget recent,
            out GameUnit* chicken)
        {
            recent = default;
            chicken = null;
            lock (recentTargetsLock)
            {
                if (!targets.TryGetValue(hunterUnitId, out recent) ||
                    Stopwatch.GetTimestamp() - recent.Timestamp > RecentTargetLifetime)
                {
                    targets.Remove(hunterUnitId);
                    return false;
                }
            }

            return TryResolveChicken(unitApi, recent.UnitId, recent.GlobalId, out chicken);
        }

        private static bool TryResolveChicken(
            GameUnitManagerAPI unitApi,
            int unitId,
            uint globalId,
            out GameUnit* chicken)
        {
            chicken = null;
            return unitId > 0 &&
                globalId != 0 &&
                unitApi.TryGetUnitById(unitId, out chicken) &&
                chicken != null &&
                chicken->r_GlobalId == globalId &&
                chicken->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN;
        }

        private static long GetAgeMilliseconds(long timestamp)
        {
            long elapsed = Math.Max(0, Stopwatch.GetTimestamp() - timestamp);
            return elapsed * 1000 / Stopwatch.Frequency;
        }

        private static string DescribeUnitPosition(GameUnit* unit)
        {
            return $"tile:{unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}/" +
                $"world:{unit->r_CurrentWorldPositionX},{unit->r_CurrentWorldPositionY}/" +
                $"elevation:{unit->r_HeightElevation}/" +
                $"lookAt:{unit->r_LookAtWorldPositionX},{unit->r_LookAtWorldPositionY},{unit->r_LookAtHeight}";
        }

        private static string DescribeLineContext(GameUnit* hunter, GameUnit* chicken)
        {
            try
            {
                int startX = hunter->r_CurrentTilePositionX;
                int startY = hunter->r_CurrentTilePositionY;
                int endX = chicken->r_CurrentTilePositionX;
                int endY = chicken->r_CurrentTilePositionY;
                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                SortedDictionary<int, int> buildingTileCounts = new SortedDictionary<int, int>();
                int sampledTiles = 0;
                int minimumHeight = int.MaxValue;
                int maximumHeight = int.MinValue;

                int x = startX;
                int y = startY;
                int deltaX = Math.Abs(endX - startX);
                int stepX = startX < endX ? 1 : -1;
                int deltaY = -Math.Abs(endY - startY);
                int stepY = startY < endY ? 1 : -1;
                int error = deltaX + deltaY;

                while (sampledTiles < MaxLineTiles)
                {
                    if (tileApi.IsTileInsideMapBounds(x, y))
                    {
                        int tileId = tileApi.GetTileId(x, y);
                        int height = tileApi.GetTileHeight(tileId);
                        minimumHeight = Math.Min(minimumHeight, height);
                        maximumHeight = Math.Max(maximumHeight, height);
                        int buildingId = tileApi.GetTileBuildingId(tileId);
                        if (buildingId > 0)
                        {
                            buildingTileCounts.TryGetValue(buildingId, out int count);
                            buildingTileCounts[buildingId] = count + 1;
                        }
                    }

                    sampledTiles++;
                    if (x == endX && y == endY)
                        break;

                    int twiceError = error * 2;
                    if (twiceError >= deltaY)
                    {
                        error += deltaY;
                        x += stepX;
                    }

                    if (twiceError <= deltaX)
                    {
                        error += deltaX;
                        y += stepY;
                    }
                }

                StringBuilder buildings = new StringBuilder();
                foreach (KeyValuePair<int, int> pair in buildingTileCounts)
                {
                    if (buildings.Length > 0)
                        buildings.Append(';');

                    if (buildingApi.TryGetBuildingById(pair.Key, out GameBuilding* building) && building != null)
                    {
                        buildings.Append(pair.Key)
                            .Append('/')
                            .Append(building->r_BuildingType)
                            .Append("/owner:")
                            .Append(building->r_PlayerIdOwner)
                            .Append("/tiles:")
                            .Append(pair.Value)
                            .Append("/baseElevation:")
                            .Append(building->r_HeightElevation)
                            .Append("/bounds:")
                            .Append(building->r_TilePositionXBegin)
                            .Append(',')
                            .Append(building->r_TilePositionYBegin)
                            .Append('-')
                            .Append(building->r_TilePositionXEnd)
                            .Append(',')
                            .Append(building->r_TilePositionYEnd)
                            .Append("/grid:")
                            .Append(building->r_OccupyTileGridSize);
                    }
                    else
                    {
                        buildings.Append(pair.Key)
                            .Append("/unresolved/tiles:")
                            .Append(pair.Value);
                    }
                }

                string heightRange = minimumHeight == int.MaxValue
                    ? "none"
                    : $"{minimumHeight}-{maximumHeight}";
                return $"line=start:{startX},{startY}/end:{endX},{endY}/" +
                    $"sampledTiles:{sampledTiles}/terrainHeight:{heightRange}/" +
                    $"truncated:{x != endX || y != endY}/buildings:{(buildings.Length == 0 ? "none" : buildings.ToString())}";
            }
            catch (Exception exception)
            {
                return $"line=analysis-failed:{exception.GetType().Name}:{exception.Message}";
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            settings.SettingChanged -= OnSettingChanged;
            lock (recentTargetsLock)
            {
                recentAssignedTargets.Clear();
                recentAcceptedTargets.Clear();
            }

            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            if (enabledFlagAddress != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(enabledFlagAddress);
                enabledFlagAddress = IntPtr.Zero;
            }
        }
    }
}

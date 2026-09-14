// Feature: Validate the native notification queue contract before writing its command slot.
using System;

namespace BugfixesAndQoL
{
    internal static class NotificationQueueNativeContract
    {
        public const int IsQueueActiveOffset = 0x00;
        public const int ImmediateCommandIdOffset = 0x04;
        public const int ImmediatePresentationIdOffset = 0x08;
        public const int ImmediateVideoPathOffset = 0x0C;
        public const int QueuedCountOffset = 0x94C;

        private const int CompletionCallOffset = 62;
        private const int CompletionTailFromUpdateOffset = 0x59;
        private const int PendingFlagClearOffset = 55;
        private const int PendingFlagClearLength = 7;
        private const int PromotionCallOffset = 47;
        private const int RunTickCallOffset = 21;
        private const int RunTickCallSiteFromEntryOffset = 0x411;
        private const int StartPendingFlagPatternFromEntryOffset = 0x61;
        private const int StartPendingFlagSetOffset = 19;
        private const int CompletionCallLength = 5;

        private const string RunTickProloguePattern =
            "48 89 5C 24 08 48 89 54 24 10 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 70 8B 84 24 88 01 00 00 33 D2 0F B6 AC 24 A8 01 00 00";

        private const string RunTickNotificationCallPattern =
            "44 38 25 ? ? ? ? 74 11 45 33 C0 48 8D 0D ? ? ? ? 33 D2 E8 ? ? ? ? " +
            "48 8D 0D ? ? ? ? E8 ? ? ? ? E8 ? ? ? ?";

        private const string UpdateProloguePattern =
            "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 FF 15 ? ? ? ? " +
            "48 8D 0D ? ? ? ? 8B F8 E8 ? ? ? ? 85 C0 74 77";

        private const string CompletionTailPattern =
            "2B BB D8 00 00 00 81 FF 10 27 00 00 77 06 83 7B 04 00 75 2F " +
            "80 7B 0C 00 74 0B 80 3D ? ? ? ? 00 75 20 EB 0F 80 7B 70 00 74 09 " +
            "83 BB 54 09 00 00 00 74 0F 48 8B CB C6 05 ? ? ? ? 00 E8 ? ? ? ?";

        private const string FinalizeProloguePattern =
            "40 53 48 83 EC 20 80 79 0C 00 48 8B D9 74 0C 48 8D 0D ? ? ? ? " +
            "E8 ? ? ? ? 33 C0 89 83 50 09 00 00";

        private const string PromotionPattern =
            "8B 83 DC 00 00 00 4C 8D 83 3C 05 00 00 89 43 04 " +
            "48 8D 93 54 01 00 00 8B 83 04 01 00 00 48 8B CB 89 43 08 " +
            "8B 83 24 09 00 00 89 83 D4 00 00 00 E8 ? ? ? ?";

        private const string StartProloguePattern =
            "48 89 5C 24 18 55 56 57 48 81 EC 80 00 00 00 " +
            "48 8B 05 ? ? ? ? 48 33 C4 48 89 44 24 70 48 8B D9 48 8B F2";

        private const string StartPendingFlagPattern =
            "8B 83 D4 00 00 00 48 8D 0D ? ? ? ? 89 83 50 09 00 00 " +
            "C6 05 ? ? ? ? 01 80 7B 0C 00 44 8B 43 08 8B 53 04";

        public static NotificationQueueNativeResolution Validate(ReadOnlySpan<byte> memory)
        {
            int runTick = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                RunTickProloguePattern,
                "DLL_RunTick entry");
            int runTickNotificationCall = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                RunTickNotificationCallPattern,
                "DLL_RunTick notification-update call");
            int update = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                UpdateProloguePattern,
                "notification queue update entry");
            int completionTail = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                CompletionTailPattern,
                "notification completion tail");
            int finalize = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                FinalizeProloguePattern,
                "notification queue finalizer");
            int promotion = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                PromotionPattern,
                "notification queue promotion");
            int start = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                StartProloguePattern,
                "notification presentation entry");
            int startPendingFlag = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                StartPendingFlagPattern,
                "notification presentation pending-flag set");

            if (runTickNotificationCall != runTick + RunTickCallSiteFromEntryOffset)
            {
                throw new InvalidOperationException(
                    $"The notification-update call at 0x{runTickNotificationCall:X} does not belong " +
                    $"to the validated DLL_RunTick entry at 0x{runTick:X}.");
            }

            int runTickTarget = ResolveNearCallTarget(
                memory,
                runTickNotificationCall + RunTickCallOffset);
            if (runTickTarget != update)
            {
                throw new InvalidOperationException(
                    $"DLL_RunTick calls 0x{runTickTarget:X}, but the validated notification " +
                    $"update entry is 0x{update:X}.");
            }

            if (completionTail != update + CompletionTailFromUpdateOffset)
            {
                throw new InvalidOperationException(
                    $"The notification completion tail at 0x{completionTail:X} does not belong to " +
                    $"the validated update entry at 0x{update:X}.");
            }

            int completionCall = completionTail + CompletionCallOffset;
            int completionTarget = ResolveNearCallTarget(memory, completionCall);

            if (completionTarget != finalize)
            {
                throw new InvalidOperationException(
                    $"The notification completion tail calls 0x{completionTarget:X}, " +
                    $"but the validated queue finalizer is 0x{finalize:X}.");
            }

            int pendingFlagClear = completionTail + PendingFlagClearOffset;
            if (memory[pendingFlagClear] != 0xC6 ||
                memory[pendingFlagClear + 1] != 0x05 ||
                memory[pendingFlagClear + 6] != 0x00)
            {
                throw new InvalidOperationException(
                    $"The notification pending-flag clear at 0x{pendingFlagClear:X} is invalid.");
            }
            int pendingFlag = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                pendingFlagClear + 2,
                pendingFlagClear + PendingFlagClearLength);
            if (pendingFlag < 0 || pendingFlag >= memory.Length)
                throw new InvalidOperationException("The notification pending flag lies outside the native image.");

            int promotionCall = promotion + PromotionCallOffset;
            int promotionTarget = ResolveNearCallTarget(memory, promotionCall);
            if (promotionTarget != start)
            {
                throw new InvalidOperationException(
                    $"The notification finalizer promotion calls 0x{promotionTarget:X}, " +
                    $"but the validated presentation entry is 0x{start:X}.");
            }

            if (promotion < finalize || promotion >= finalize + 0x14F)
            {
                throw new InvalidOperationException(
                    $"The notification promotion at 0x{promotion:X} does not belong to " +
                    $"the validated finalizer at 0x{finalize:X}.");
            }

            if (startPendingFlag != start + StartPendingFlagPatternFromEntryOffset)
            {
                throw new InvalidOperationException(
                    $"The notification pending-flag set at 0x{startPendingFlag:X} does not belong " +
                    $"to the validated presentation entry at 0x{start:X}.");
            }
            int startPendingFlagSet = startPendingFlag + StartPendingFlagSetOffset;
            int startPendingFlagTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                startPendingFlagSet + 2,
                startPendingFlagSet + PendingFlagClearLength);
            if (startPendingFlagTarget != pendingFlag)
            {
                throw new InvalidOperationException(
                    $"The notification presentation sets 0x{startPendingFlagTarget:X}, but the " +
                    $"validated updater clears 0x{pendingFlag:X}.");
            }

            return new NotificationQueueNativeResolution(
                runTick,
                update,
                finalize,
                start,
                pendingFlag);
        }

        internal static int ResolveNearCallTarget(
            ReadOnlySpan<byte> memory,
            int callOpcodeOffset)
        {
            if (callOpcodeOffset < 0 || callOpcodeOffset > memory.Length - CompletionCallLength)
            {
                throw new InvalidOperationException(
                    $"The near call at 0x{callOpcodeOffset:X} is truncated or outside the native image.");
            }

            if (memory[callOpcodeOffset] != 0xE8)
            {
                throw new InvalidOperationException(
                    $"Expected near-call opcode E8 at 0x{callOpcodeOffset:X}, " +
                    $"but found {memory[callOpcodeOffset]:X2}.");
            }

            return Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                callOpcodeOffset + 1,
                callOpcodeOffset + CompletionCallLength);
        }
    }

    internal readonly struct NotificationQueueNativeResolution
    {
        public NotificationQueueNativeResolution(
            int runTickRva,
            int updateRva,
            int finalizerRva,
            int startRva,
            int pendingFlagRva)
        {
            RunTickRva = runTickRva;
            UpdateRva = updateRva;
            FinalizerRva = finalizerRva;
            StartRva = startRva;
            PendingFlagRva = pendingFlagRva;
        }

        public int RunTickRva { get; }
        public int UpdateRva { get; }
        public int FinalizerRva { get; }
        public int StartRva { get; }
        public int PendingFlagRva { get; }
    }
}

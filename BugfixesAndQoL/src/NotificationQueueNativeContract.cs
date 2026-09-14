// Feature: Validate the native notification queue contract before writing its command slot.
using System;

namespace BugfixesAndQoL
{
    internal static class NotificationQueueNativeContract
    {
        public const int IsQueueActiveOffset = 0x00;
        public const int ImmediateCommandIdOffset = 0x04;
        public const int ImmediateVideoPathOffset = 0x0C;

        private const int CompletionCallOffset = 62;
        private const int CompletionCallLength = 5;

        private const string CompletionTailPattern =
            "2B BB D8 00 00 00 81 FF 10 27 00 00 77 06 83 7B 04 00 75 2F " +
            "80 7B 0C 00 74 0B 80 3D ? ? ? ? 00 75 20 EB 0F 80 7B 70 00 74 09 " +
            "83 BB 54 09 00 00 00 74 0F 48 8B CB C6 05 ? ? ? ? 00 E8 ? ? ? ?";

        private const string FinalizeProloguePattern =
            "40 53 48 83 EC 20 80 79 0C 00 48 8B D9 74 0C 48 8D 0D ? ? ? ? " +
            "E8 ? ? ? ? 33 C0 89 83 50 09 00 00";

        public static void Validate(ReadOnlySpan<byte> memory)
        {
            int completionTail = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                CompletionTailPattern,
                "notification completion tail");
            int finalize = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                FinalizeProloguePattern,
                "notification queue finalizer");
            int completionCall = completionTail + CompletionCallOffset;
            int completionTarget = ResolveNearCallTarget(memory, completionCall);

            if (completionTarget != finalize)
            {
                throw new InvalidOperationException(
                    $"The notification completion tail calls 0x{completionTarget:X}, " +
                    $"but the validated queue finalizer is 0x{finalize:X}.");
            }
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
}

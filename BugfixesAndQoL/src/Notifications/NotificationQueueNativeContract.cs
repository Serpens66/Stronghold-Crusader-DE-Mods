// Feature: Validate the hash-bound notification branch that compares the presentation age.
using System;

namespace BugfixesAndQoL
{
    internal static class NotificationQueueNativeContract
    {
        private const int AgeBranchRva = 0xFE5C9;
        internal const int AgeBranchLength = 14;
        internal const int StartTimeOffset = 0xD8;
        internal const uint AgeThresholdMs = 10000;

        private static readonly byte[] Reference = {
            0x2B, 0xBB, 0xD8, 0x00, 0x00, 0x00, // sub edi,[rbx+0xd8]
            0x81, 0xFF, 0x10, 0x27, 0x00, 0x00, // cmp edi,10000
            0x77, 0x06,                         // ja first media gate
            0x83, 0x7B, 0x04, 0x00, 0x75, 0x2F, // normal zero-selector shortcut
            0x80, 0x7B, 0x0C, 0x00              // first media gate
        };

        internal static int ValidateAgeBranch(ReadOnlySpan<byte> memory)
        {
            if (memory.Length < AgeBranchRva + Reference.Length ||
                !memory.Slice(AgeBranchRva, Reference.Length).SequenceEqual(Reference.AsSpan()))
                throw new InvalidOperationException("The notification age branch or its continuation has changed.");
            return AgeBranchRva;
        }
    }
}

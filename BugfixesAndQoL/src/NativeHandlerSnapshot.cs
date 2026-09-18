using System;

namespace BugfixesAndQoL
{
    internal static class NativeHandlerSnapshot
    {
        internal static byte[] Copy(
            ReadOnlySpan<byte> memory,
            ulong handlerStart,
            int handlerLength,
            ulong libraryBase)
        {
            if (handlerLength <= 0 || handlerStart < libraryBase)
            {
                throw new InvalidOperationException(
                    "The native unit handler has an invalid snapshot range.");
            }

            ulong handlerRva = handlerStart - libraryBase;
            if (handlerRva > int.MaxValue ||
                handlerRva + unchecked((ulong)handlerLength) >
                    unchecked((ulong)memory.Length))
            {
                throw new InvalidOperationException(
                    "The native unit handler is outside the load-time snapshot.");
            }

            return memory.Slice(
                checked((int)handlerRva),
                handlerLength).ToArray();
        }
    }
}

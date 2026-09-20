using System;
using System.Runtime.InteropServices;

namespace StartConditions
{
    internal static class StartTroopSpawnCompletionContract
    {
        internal const int CompletionStateRva = 0x37EDBD0;
        internal const int CompleteValue = -1;

        internal static bool TryInterpret(int value, out bool complete)
        {
            complete = value == CompleteValue;
            return complete || value >= 0;
        }
    }

    internal sealed class NativeInt32StateReader
    {
        private readonly Func<IntPtr, int> readInt32;
        private IntPtr address;

        internal NativeInt32StateReader()
            : this(Marshal.ReadInt32)
        {
        }

        internal NativeInt32StateReader(Func<IntPtr, int> readInt32)
        {
            this.readInt32 = readInt32 ?? throw new ArgumentNullException(nameof(readInt32));
        }

        internal bool IsAvailable => address != IntPtr.Zero;

        internal bool TryInitialize(IntPtr moduleHandle, int moduleLength, int rva)
        {
            address = IntPtr.Zero;
            if (moduleHandle == IntPtr.Zero || moduleLength < 0 || rva < 0)
                return false;

            long endExclusive = (long)rva + sizeof(int);
            if (endExclusive > moduleLength)
                return false;

            address = IntPtr.Add(moduleHandle, rva);
            return true;
        }

        internal bool TryRead(out int value, out Exception failure)
        {
            value = 0;
            failure = null;
            if (address == IntPtr.Zero)
                return false;

            try
            {
                value = readInt32(address);
                return true;
            }
            catch (Exception ex)
            {
                address = IntPtr.Zero;
                failure = ex;
                return false;
            }
        }
    }

    internal sealed class VanillaStartTroopCompletionReader
    {
        private readonly NativeInt32StateReader nativeReader;

        internal VanillaStartTroopCompletionReader()
            : this(new NativeInt32StateReader())
        {
        }

        internal VanillaStartTroopCompletionReader(NativeInt32StateReader nativeReader)
        {
            this.nativeReader = nativeReader ?? throw new ArgumentNullException(nameof(nativeReader));
        }

        internal bool TryInitialize(IntPtr moduleHandle, int moduleLength, bool referenceHashMatches)
        {
            return referenceHashMatches &&
                nativeReader.TryInitialize(
                    moduleHandle,
                    moduleLength,
                    StartTroopSpawnCompletionContract.CompletionStateRva);
        }

        internal bool TryGetIsComplete(out bool complete, out int rawValue, out Exception failure)
        {
            complete = false;
            rawValue = 0;
            if (!nativeReader.TryRead(out rawValue, out failure))
                return false;

            return StartTroopSpawnCompletionContract.TryInterpret(rawValue, out complete);
        }
    }

    internal enum StartTroopCompletionWaitResult
    {
        Waiting,
        Settling,
        Ready,
    }

    internal sealed class StartTroopCompletionWaitState
    {
        private int? completionObservedTick;

        internal StartTroopCompletionWaitResult Observe(bool complete, int gameTick)
        {
            if (!complete)
            {
                completionObservedTick = null;
                return StartTroopCompletionWaitResult.Waiting;
            }

            if (!completionObservedTick.HasValue)
            {
                completionObservedTick = gameTick;
                return StartTroopCompletionWaitResult.Settling;
            }

            return gameTick != completionObservedTick.Value
                ? StartTroopCompletionWaitResult.Ready
                : StartTroopCompletionWaitResult.Settling;
        }

        internal void Reset()
        {
            completionObservedTick = null;
        }
    }
}

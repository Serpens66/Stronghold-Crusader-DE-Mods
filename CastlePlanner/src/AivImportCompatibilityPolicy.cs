using System;

namespace CastlePlanner
{
    internal static class AivImportCompatibilityPolicy
    {
        internal const int BankCount = 8;
        internal const int CandidatesPerBank = 1_000;
        internal const int CandidateTableEntryCount = BankCount * CandidatesPerBank;

        internal static T SelectBackend<T>(
            Func<T> officialFactory,
            Func<T> workaroundFactory,
            out bool workaroundActive,
            out Exception knownScriptExtenderFailure)
        {
            if (officialFactory == null)
                throw new ArgumentNullException(nameof(officialFactory));
            if (workaroundFactory == null)
                throw new ArgumentNullException(nameof(workaroundFactory));

            try
            {
                T official = officialFactory();
                workaroundActive = false;
                knownScriptExtenderFailure = null;
                return official;
            }
            catch (Exception ex) when (IsKnownCoarseGridBufferFailure(ex))
            {
                T workaround = workaroundFactory();
                workaroundActive = true;
                knownScriptExtenderFailure = ex;
                return workaround;
            }
        }

        internal static bool IsKnownCoarseGridBufferFailure(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is TypeLoadException &&
                    Contains(current.Message, "CoarseGridBuffer") &&
                    Contains(current.Message, "FixedBuffer") &&
                    (Contains(current.Message, "1228816") ||
                     Contains(current.Message, "bigger than 1Mb")))
                {
                    return true;
                }
            }

            return false;
        }

        internal static void ValidateImportArguments(
            int bankIndex,
            int candidateId,
            short[] data)
        {
            if ((uint)bankIndex >= BankCount)
                throw new ArgumentOutOfRangeException(nameof(bankIndex), bankIndex, "The AIV bank must be in the range 0..7.");
            if ((uint)candidateId >= CandidatesPerBank)
                throw new ArgumentOutOfRangeException(nameof(candidateId), candidateId, "The AIV candidate must be in the range 0..999.");
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (data.Length == 0)
                throw new ArgumentException("The native AIV payload must not be empty.", nameof(data));
        }

        internal static int GetBankOffset(int bankIndex)
        {
            if ((uint)bankIndex >= BankCount)
                throw new ArgumentOutOfRangeException(nameof(bankIndex), bankIndex, "The AIV bank must be in the range 0..7.");

            return checked(bankIndex * CandidatesPerBank);
        }

        internal static int CountDenseCandidatePrefix(ReadOnlySpan<ulong> bank)
        {
            if (bank.Length != CandidatesPerBank)
            {
                throw new ArgumentException(
                    $"An imported AIV bank must contain exactly {CandidatesPerBank} pointer slots.",
                    nameof(bank));
            }

            int count = 0;
            while (count < bank.Length && bank[count] != 0)
                count++;
            return count;
        }

        internal static bool IsTableRangeInsideModule(
            ulong tableAddress,
            ulong moduleAddress,
            int moduleLength)
        {
            if (tableAddress == 0 || moduleAddress == 0 || moduleLength <= 0 ||
                (tableAddress & (ulong)(IntPtr.Size - 1)) != 0)
            {
                return false;
            }

            ulong tableByteCount = checked((ulong)CandidateTableEntryCount * sizeof(ulong));
            ulong moduleEnd = checked(moduleAddress + (ulong)moduleLength);
            return tableAddress >= moduleAddress &&
                   tableAddress <= moduleEnd &&
                   tableByteCount <= moduleEnd - tableAddress;
        }

        private static bool Contains(string value, string expected)
        {
            return value?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

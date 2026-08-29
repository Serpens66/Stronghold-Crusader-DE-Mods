using System;
using System.IO;
using System.Security.Cryptography;

namespace RandomEvents
{
    internal sealed class RandomEventsConfigurationSnapshot
    {
        public bool Enabled;
        public int IntervalMonths;
        public int CooldownMonths;
        public int MultiplayerMode;
        public bool IncludeAIPlayers;
        public int[] Chances = Array.Empty<int>();
        public int[] StrengthMinimums = Array.Empty<int>();
        public int[] StrengthMaximums = Array.Empty<int>();

        public byte[] GetDigest()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            using (SHA256 sha256 = SHA256.Create())
            {
                writer.Write(Enabled);
                writer.Write(IntervalMonths);
                writer.Write(CooldownMonths);
                writer.Write(MultiplayerMode);
                writer.Write(IncludeAIPlayers);
                WriteArray(writer, Chances);
                WriteArray(writer, StrengthMinimums);
                WriteArray(writer, StrengthMaximums);
                writer.Flush();
                return sha256.ComputeHash(stream.ToArray());
            }
        }

        public void ApplyTo(RandomEventsRuntimeState state)
        {
            state.EffectiveEnabled = Enabled;
            state.IntervalMonths = IntervalMonths;
            state.CooldownMonths = CooldownMonths;
            state.MultiplayerMode = MultiplayerMode;
            state.IncludeAIPlayers = IncludeAIPlayers;
            state.Chances = (int[])Chances.Clone();
            state.StrengthMinimums = (int[])StrengthMinimums.Clone();
            state.StrengthMaximums = (int[])StrengthMaximums.Clone();
            state.ConfigurationDigest = GetDigest();
        }

        private static void WriteArray(BinaryWriter writer, int[] values)
        {
            writer.Write(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

    }

    internal sealed class RandomEventsRuntimeState
    {
        public bool EffectiveEnabled;
        public int IntervalMonths;
        public int CooldownMonths;
        public int MultiplayerMode;
        public bool IncludeAIPlayers;
        public int[] Chances = Array.Empty<int>();
        public int[] StrengthMinimums = Array.Empty<int>();
        public int[] StrengthMaximums = Array.Empty<int>();
        public byte[] ConfigurationDigest = Array.Empty<byte>();
        public ulong PrngState0;
        public ulong PrngState1;
        public int NextDueAbsoluteMonth;
        public int StartAbsoluteMonth;
        public int[] SharedCooldownUntilAbsoluteMonths = Array.Empty<int>();
        public int[] IndividualCooldownUntilAbsoluteMonths = Array.Empty<int>();
        public bool BatchPrepared;
        public int[] PreparedDirectKinds = Array.Empty<int>();
        public int[] PreparedDirectStrengths = Array.Empty<int>();
        public int[] PreparedDirectTargetPlayerIds = Array.Empty<int>();
        public bool SignpostsInitialized;
        public int[] SignpostBuildingIds = new[] { -1, -1, -1, -1 };
    }

    internal struct SavedPrng
    {
        public SavedPrng(ulong state0, ulong state1)
        {
            State0 = state0;
            State1 = state1;
            if ((State0 | State1) == 0)
                State1 = 0x9E3779B97F4A7C15UL;
        }

        public ulong State0;
        public ulong State1;

        public ulong NextUInt64()
        {
            ulong first = State0;
            ulong second = State1;
            ulong result = first + second;
            second ^= first;
            State0 = RotateLeft(first, 55) ^ second ^ (second << 14);
            State1 = RotateLeft(second, 36);
            return result;
        }

        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            ulong bound = (uint)exclusiveMaximum;
            ulong threshold = unchecked(0UL - bound) % bound;
            ulong value;
            do value = NextUInt64(); while (value < threshold);
            return (int)(value % bound);
        }

        public int NextInclusive(int minimum, int maximum)
        {
            if (maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            return minimum + Next(checked(maximum - minimum + 1));
        }

        private static ulong RotateLeft(ulong value, int count) =>
            (value << count) | (value >> (64 - count));
    }
}

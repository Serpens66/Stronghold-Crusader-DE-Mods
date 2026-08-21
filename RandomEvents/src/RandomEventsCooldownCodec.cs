using System;
using System.Collections.Generic;

namespace RandomEvents
{
    internal sealed class RandomEventsCooldownPayload
    {
        public RandomEventsCooldownEncoding Encoding;
        public int[] Data = Array.Empty<int>();
    }

    internal static class RandomEventsCooldownCodec
    {
        internal const int EventCount = 15;
        internal const int MaximumPlayers = 8;
        internal const int FullIndividualLength = (MaximumPlayers + 1) * EventCount;

        public static RandomEventsCooldownPayload[] CreateCandidates(RandomEventsRuntimeState state)
        {
            if (state.MultiplayerMode == 0)
            {
                if (AllZero(state.SharedCooldownUntilAbsoluteMonths)) return None();
                return new[] { new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.SharedDense, Data = (int[])state.SharedCooldownUntilAbsoluteMonths.Clone() } };
            }
            if (AllZero(state.IndividualCooldownUntilAbsoluteMonths)) return None();
            var sparse = new List<int>();
            for (int index = EventCount; index < FullIndividualLength; index++)
            {
                int value = state.IndividualCooldownUntilAbsoluteMonths[index];
                if (value == 0) continue;
                sparse.Add(index); sparse.Add(value);
            }
            int[] dense = new int[MaximumPlayers * EventCount];
            Array.Copy(state.IndividualCooldownUntilAbsoluteMonths, EventCount, dense, 0, dense.Length);
            return new[]
            {
                new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.IndividualSparse, Data = sparse.ToArray() },
                new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.IndividualDense, Data = dense }
            };
        }

        public static void Decode(int multiplayerMode, int encodingValue, int[] data, out int[] shared, out int[] individual)
        {
            shared = new int[EventCount]; individual = new int[FullIndividualLength];
            int[] values = data ?? Array.Empty<int>();
            var encoding = (RandomEventsCooldownEncoding)encodingValue;
            if (encoding == RandomEventsCooldownEncoding.None)
            {
                if (values.Length != 0) throw new InvalidOperationException("None cooldown encoding contains data.");
                return;
            }
            if (multiplayerMode == 0)
            {
                if (encoding != RandomEventsCooldownEncoding.SharedDense || values.Length != EventCount)
                    throw new InvalidOperationException("Shared mode requires exactly 15 dense cooldown values.");
                ValidateNonNegative(values); Array.Copy(values, shared, EventCount); return;
            }
            if (multiplayerMode != 1)
                throw new InvalidOperationException("Unknown multiplayer mode.");
            if (encoding == RandomEventsCooldownEncoding.IndividualDense)
            {
                if (values.Length != MaximumPlayers * EventCount)
                    throw new InvalidOperationException("Individual dense cooldown data has the wrong length.");
                ValidateNonNegative(values); Array.Copy(values, 0, individual, EventCount, values.Length); return;
            }
            if (encoding != RandomEventsCooldownEncoding.IndividualSparse || (values.Length & 1) != 0)
                throw new InvalidOperationException("Individual sparse cooldown data is malformed.");
            var seen = new HashSet<int>();
            for (int offset = 0; offset < values.Length; offset += 2)
            {
                int index = values[offset]; int month = values[offset + 1];
                if (index < EventCount || index >= FullIndividualLength || !seen.Add(index) || month <= 0)
                    throw new InvalidOperationException("Individual sparse cooldown entry is invalid or duplicated.");
                individual[index] = month;
            }
        }

        private static RandomEventsCooldownPayload[] None() => new[] { new RandomEventsCooldownPayload { Encoding = RandomEventsCooldownEncoding.None } };
        private static bool AllZero(int[] values)
        {
            if (values == null) return true;
            for (int index = 0; index < values.Length; index++) if (values[index] != 0) return false;
            return true;
        }
        private static void ValidateNonNegative(int[] values)
        {
            for (int index = 0; index < values.Length; index++) if (values[index] < 0) throw new InvalidOperationException("Cooldown values cannot be negative.");
        }
    }
}

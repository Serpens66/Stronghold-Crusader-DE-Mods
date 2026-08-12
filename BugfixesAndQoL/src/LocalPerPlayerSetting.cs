// Feature: Keeps a local preference independent from its synchronized player slots.
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class LocalPerPlayerSetting<T>
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;

        private readonly T[] data = new T[LastPlayerId + 1];
        private T localValue;
        private int validatedLocalPlayerId;

        public LocalPerPlayerSetting(T defaultValue)
        {
            localValue = defaultValue;
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
                data[playerId] = defaultValue;
        }

        public T Value => localValue;

        public T[] Data => data;

        public bool SetValue(T value)
        {
            if (EqualityComparer<T>.Default.Equals(localValue, value))
                return false;

            localValue = value;
            if (IsValidPlayerId(validatedLocalPlayerId))
                data[validatedLocalPlayerId] = value;
            return true;
        }

        public bool TrySetLocalPlayerId(int playerId)
        {
            if (!IsValidPlayerId(playerId))
                return false;

            validatedLocalPlayerId = playerId;
            data[playerId] = localValue;
            return true;
        }

        private static bool IsValidPlayerId(int playerId) =>
            playerId >= FirstPlayerId && playerId <= LastPlayerId;
    }
}

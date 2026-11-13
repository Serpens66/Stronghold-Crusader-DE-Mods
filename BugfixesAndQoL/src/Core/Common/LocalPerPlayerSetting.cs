// Feature: Keeps a local preference independent from its synchronized player slots.
using System.Collections.Generic;
using System;

namespace BugfixesAndQoL
{
    internal sealed class LocalPerPlayerSetting<T>
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;

        private readonly T[] data = new T[LastPlayerId + 1];
        private readonly Func<T, T> copyValue;
        private T localValue;
        private int validatedLocalPlayerId;

        public LocalPerPlayerSetting(T defaultValue, Func<T, T> copyValue = null)
        {
            this.copyValue = copyValue;
            localValue = Copy(defaultValue);
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
                data[playerId] = Copy(defaultValue);
        }

        public T Value => localValue;

        public T[] Data => data;

        public bool SetValue(T value)
        {
            if (EqualityComparer<T>.Default.Equals(localValue, value))
                return false;

            localValue = Copy(value);
            if (IsValidPlayerId(validatedLocalPlayerId))
                data[validatedLocalPlayerId] = Copy(localValue);
            return true;
        }

        public bool TrySetLocalPlayerId(int playerId)
        {
            if (!IsValidPlayerId(playerId))
                return false;

            validatedLocalPlayerId = playerId;
            data[playerId] = Copy(localValue);
            return true;
        }

        private T Copy(T value) => copyValue == null ? value : copyValue(value);

        private static bool IsValidPlayerId(int playerId) =>
            playerId >= FirstPlayerId && playerId <= LastPlayerId;
    }
}

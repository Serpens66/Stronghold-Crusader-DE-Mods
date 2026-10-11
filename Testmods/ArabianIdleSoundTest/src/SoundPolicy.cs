using System;
using System.Threading;

namespace ArabianIdleSoundTest
{
    internal static class SoundPolicy
    {
        internal const short Marker = short.MinValue;
        internal const short SwordDraw = 180;
        internal const int RecordWords = 21;
        private static int enabled, ready;
        internal static bool Active => Volatile.Read(ref enabled) != 0 && Volatile.Read(ref ready) != 0;
        internal static void SetEnabled(bool value) => Volatile.Write(ref enabled, value ? 1 : 0);
        internal static void SetReady(bool value) => Volatile.Write(ref ready, value ? 1 : 0);

        internal static bool Eligible(int scopeId, int currentId, int unitType, int state, int sound, int flag)
            => Active && scopeId > 0 && scopeId == currentId && unitType == 75 && state == 1 && sound == SwordDraw && flag == 0;

        internal static bool TryMark(Span<short> record, bool eligible)
        {
            if (!eligible || record.Length != RecordWords || record[0] != 16 || record[3] != SwordDraw ||
                record[11] != -1 || record[12] != -1 || record[13] != 0) return false;
            record[11] = Marker;
            return true;
        }

        internal static float Scale(short[] buffer, int recordIndex, int sound, float volume)
        {
            if (!Active || sound != SwordDraw || buffer == null || recordIndex < 0 ||
                recordIndex >= buffer.Length / RecordWords) return volume;
            int offset = recordIndex * RecordWords;
            return buffer[offset] == 16 && buffer[offset + 3] == SwordDraw &&
                buffer[offset + 11] == Marker && buffer[offset + 12] == -1 && buffer[offset + 13] == 0
                ? volume * 0.25f : volume;
        }
    }
}

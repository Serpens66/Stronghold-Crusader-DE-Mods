using System;
using System.IO;

namespace AutoSendTest
{
    internal static class AutoSendSaveCodec
    {
        // These numbers identify our binary format, not game-domain values.
        private const int Magic = 0x41535431, FormatVersion = 1, MaximumBytes = 1024 * 1024;
        internal static byte[] Encode(AutoSendState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic); writer.Write(FormatVersion); writer.Write(state.GoodCount);
                writer.Write(state.SenderCursor); writer.Write(state.HasProcessedTick); writer.Write(state.LastProcessedTick);
                for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                { writer.Write(state.IsPaused(player)); writer.Write(state.GetRevision(player)); }
                for (int g = 0; g < state.GoodCount; g++)
                {
                    int good = state.GetGood(g); writer.Write(good);
                    for (int sender = 1; sender <= AutoSendState.PlayerCount; sender++)
                    {
                        writer.Write(state.GetReserve(sender, good)); writer.Write(state.GetCursor(sender, good));
                        for (int recipient = 1; recipient <= AutoSendState.PlayerCount; recipient++)
                        {
                            AutoSendRule rule = state.GetRule(sender, recipient, good);
                            writer.Write(rule.Minimum); writer.Write(rule.Maximum); writer.Write(rule.Enabled);
                        }
                    }
                }
                writer.Flush(); return stream.ToArray();
            }
        }
        internal static bool TryDecode(byte[] bytes, int[] allowedGoods, int[] batchLimits,
            out AutoSendState state, out string reason)
        {
            state = null; reason = string.Empty;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
            { reason = "Save data is missing or exceeds its size limit."; return false; }
            try
            {
                var candidate = new AutoSendState(allowedGoods, batchLimits);
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != FormatVersion)
                    { reason = "Unknown AutoSend save format."; return false; }
                    if (reader.ReadInt32() != candidate.GoodCount) throw new InvalidDataException("Goods set changed.");
                    int senderCursor = reader.ReadInt32();
                    if (senderCursor < 0 || senderCursor >= AutoSendState.PlayerCount) throw new InvalidDataException("Invalid sender cursor.");
                    candidate.SenderCursor = senderCursor;
                    bool hasTick = ReadBoolean(reader); int tick = reader.ReadInt32();
                    if (hasTick) candidate.MarkProcessedTick(tick);
                    var paused = new bool[AutoSendState.PlayerCount + 1];
                    var revision = new long[AutoSendState.PlayerCount + 1];
                    for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                    {
                        paused[player] = ReadBoolean(reader); revision[player] = reader.ReadInt64();
                        if (revision[player] < 0) throw new InvalidDataException("Invalid revision.");
                    }
                    var seen = new bool[candidate.GoodCount];
                    for (int g = 0; g < candidate.GoodCount; g++)
                    {
                        int good = reader.ReadInt32(), mapped = -1;
                        for (int index = 0; index < candidate.GoodCount; index++) if (candidate.GetGood(index) == good) mapped = index;
                        if (mapped < 0 || seen[mapped]) throw new InvalidDataException("Unknown or duplicated good.");
                        seen[mapped] = true;
                        for (int sender = 1; sender <= AutoSendState.PlayerCount; sender++)
                        {
                            int reserve = reader.ReadInt32(), cursor = reader.ReadInt32();
                            if (!candidate.TrySetReserve(sender, good, reserve, out reason)) throw new InvalidDataException(reason);
                            candidate.SetCursor(sender, good, cursor);
                            for (int recipient = 1; recipient <= AutoSendState.PlayerCount; recipient++)
                            {
                                int minimum = reader.ReadInt32(), maximum = reader.ReadInt32(); bool enabled = ReadBoolean(reader);
                                if (recipient == sender)
                                {
                                    if (minimum != 0 || maximum != 0 || enabled) throw new InvalidDataException("Self-delivery rule.");
                                    continue;
                                }
                                if (!candidate.TrySetRule(sender, recipient, good, minimum, maximum, enabled, out reason))
                                    throw new InvalidDataException(reason);
                            }
                        }
                    }
                    if (stream.Position != stream.Length) throw new InvalidDataException("Trailing save bytes.");
                    for (int player = 1; player <= AutoSendState.PlayerCount; player++) candidate.RestoreMeta(player, paused[player], revision[player]);
                }
                state = candidate; reason = string.Empty; return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is OverflowException)
            { reason = "AutoSend save rejected: " + error.Message; return false; }
        }
        private static bool ReadBoolean(BinaryReader reader)
        {
            byte value = reader.ReadByte();
            if (value > 1) throw new InvalidDataException("Noncanonical Boolean.");
            return value != 0;
        }
    }
}

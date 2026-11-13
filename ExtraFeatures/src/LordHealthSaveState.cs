using MessagePack;
using MessagePack.Formatters;
using System;

namespace ExtraFeatures
{
    internal readonly struct LordHealthBasis
    {
        internal readonly int PlayerId;
        internal readonly uint GlobalId;
        internal readonly uint VanillaMaximum;
        internal LordHealthBasis(int playerId, uint globalId, uint vanillaMaximum)
        { PlayerId = playerId; GlobalId = globalId; VanillaMaximum = vanillaMaximum; }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(LordHealthSaveStateFormatter))]
    internal sealed class LordHealthSaveState
    {
        internal const uint Version = 1;
        internal const int MaximumRecords = 8;
        // Literal on purpose: _inspect/ExtraFeaturesSessionTests compiles this file against a
        // Script Extender stub. Value of GamePlayerManagerAPI.MAX_PLAYERS.
        internal const int MaximumPlayerId = 8;
        [IgnoreMember] internal LordHealthBasis[] Records;

        internal static LordHealthSaveState Decode(byte[] bytes)
        {
            // Flat array: version followed by at most eight player/global-ID/maximum triples.
            // Bound input and array header BEFORE allocating; never use reflection formatting.
            if (bytes == null || bytes.Length == 0 || bytes.Length > 128)
                throw new InvalidOperationException("Lord HP basis payload has an invalid length.");
            var reader = new MessagePackReader(bytes);
            LordHealthSaveState result = new LordHealthSaveStateFormatter().Deserialize(
                ref reader, MessagePackSerializerOptions.Standard);
            if (!reader.End) throw new InvalidOperationException("Trailing Lord HP basis data.");
            return result;
        }
    }

    internal sealed class LordHealthSaveStateFormatter : IMessagePackFormatter<LordHealthSaveState>
    {
        public void Serialize(ref MessagePackWriter writer, LordHealthSaveState value, MessagePackSerializerOptions options)
        {
            throw new NotSupportedException("Legacy Lord HP data is read-only; APIShared owns new health saves.");
        }

        public LordHealthSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count < 1 || count > 1 + LordHealthSaveState.MaximumRecords * 3 || (count - 1) % 3 != 0 ||
                reader.ReadUInt32() != LordHealthSaveState.Version)
                throw new MessagePackSerializationException("Unsupported Lord HP basis schema.");
            var records = new LordHealthBasis[(count - 1) / 3];
            int players = 0;
            for (int index = 0; index < records.Length; index++)
            {
                var basis = new LordHealthBasis(reader.ReadInt32(), reader.ReadUInt32(), reader.ReadUInt32());
                Validate(basis, ref players);
                records[index] = basis;
            }
            return new LordHealthSaveState { Records = records };
        }

        private static void Validate(LordHealthBasis basis, ref int players)
        {
            if (basis.PlayerId < 1 || basis.PlayerId > LordHealthSaveState.MaximumPlayerId || basis.GlobalId == 0 ||
                basis.GlobalId > int.MaxValue || basis.VanillaMaximum == 0 || (players & (1 << basis.PlayerId)) != 0)
                throw new MessagePackSerializationException("Invalid or duplicate Lord HP basis identity.");
            players |= 1 << basis.PlayerId;
        }
    }
}

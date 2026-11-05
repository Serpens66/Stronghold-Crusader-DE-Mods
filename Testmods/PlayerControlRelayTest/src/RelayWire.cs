using System;
using System.IO;
using System.Text;
using MessagePack;
using MessagePack.Formatters;

namespace PlayerControlRelayTest
{
    [MessagePackFormatter(typeof(RelayEnvelopeFormatter))]
    internal sealed class RelayEnvelope
    {
        internal const int Hello = 1, Offer = 2, Ack = 3, Ready = 4, Request = 5, Reply = 6, Execute = 7;
        internal int Kind;
        internal string Epoch, Nonce;
        internal int Player;
        internal RelayCommand Command;
        internal int Outcome;
        internal string Text;
        internal int MessagePacketId, ChorePacketId;

        // Explicit deep snapshot for the raw network callback; no roster/game lookups.
        internal RelayEnvelope Clone() => new RelayEnvelope {
            Kind = Kind, Epoch = Epoch, Nonce = Nonce, Player = Player, Outcome = Outcome, Text = Text,
            MessagePacketId = MessagePacketId, ChorePacketId = ChorePacketId,
            Command = Command == null ? null : new RelayCommand(Command.Epoch, Command.Owner, Command.Requester,
                Command.RequestId, Command.Action, Command.TargetX, Command.TargetY, Command.MaxGlobalId, Command.Slots)
        };
    }

    // Same bytes as RelayEnvelope. A separate packet type keeps Steam deliveries away
    // from the Chore-only receiver and Chore deliveries away from the message receiver.
    [MessagePackFormatter(typeof(RelayChoreEnvelopeFormatter))]
    internal sealed class RelayChoreEnvelope
    {
        internal RelayChoreEnvelope(RelayEnvelope inner) { Inner = inner; }
        internal RelayEnvelope Inner { get; }
    }

    internal sealed class RelayChoreEnvelopeFormatter : IMessagePackFormatter<RelayChoreEnvelope>
    {
        private static readonly RelayEnvelopeFormatter Inner = new RelayEnvelopeFormatter();
        public RelayChoreEnvelopeFormatter() { }
        public void Serialize(ref MessagePackWriter writer, RelayChoreEnvelope value, MessagePackSerializerOptions options) =>
            Inner.Serialize(ref writer, value?.Inner, options);
        public RelayChoreEnvelope Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) =>
            new RelayChoreEnvelope(Inner.Deserialize(ref reader, options));
    }

    // Exact schema, no contractless/reflection serialization. Wire limits are checked
    // independently of local channel send limits before allocating strings or arrays.
    internal sealed class RelayEnvelopeFormatter : IMessagePackFormatter<RelayEnvelope>
    {
        private static readonly Encoding WireText = new UTF8Encoding(false, true);
        public RelayEnvelopeFormatter() { }
        public void Serialize(ref MessagePackWriter writer, RelayEnvelope value, MessagePackSerializerOptions options)
        {
            Validate(value);
            writer.WriteArrayHeader(9);
            writer.Write(value.Kind); writer.Write(value.Epoch ?? string.Empty); writer.Write(value.Nonce ?? string.Empty);
            writer.Write(value.Player);
            WriteCommand(ref writer, value.Command);
            writer.Write(value.Outcome); writer.Write(value.Text ?? string.Empty);
            writer.Write(value.MessagePacketId); writer.Write(value.ChorePacketId);
        }
        public RelayEnvelope Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);
            try
            {
                if (reader.ReadArrayHeader() != 9) throw Bad("Relay envelope must have exactly nine fields.");
                var value = new RelayEnvelope {
                    Kind = reader.ReadInt32(), Epoch = ReadText(ref reader, 64), Nonce = ReadText(ref reader, 64),
                    Player = reader.ReadInt32(), Command = ReadCommand(ref reader), Outcome = reader.ReadInt32(),
                    Text = ReadText(ref reader, 512), MessagePacketId = reader.ReadInt32(), ChorePacketId = reader.ReadInt32()
                };
                if (!reader.End) throw Bad("Unexpected trailing relay bytes.");
                Validate(value); return value;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException ||
                error is OverflowException || error is EndOfStreamException)
            { throw new MessagePackSerializationException("Malformed relay envelope.", error); }
            finally { reader.Depth--; }
        }
        private static void WriteCommand(ref MessagePackWriter writer, RelayCommand command)
        {
            if (command == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(9); writer.Write(command.Epoch); writer.Write(command.Owner); writer.Write(command.Requester);
            writer.Write(command.RequestId); writer.Write((int)command.Action); writer.Write(command.TargetX); writer.Write(command.TargetY);
            writer.Write(command.MaxGlobalId); writer.Write(command.EncodedSlots);
        }
        private static RelayCommand ReadCommand(ref MessagePackReader reader)
        {
            if (reader.TryReadNil()) return null;
            if (reader.ReadArrayHeader() != 9) throw Bad("Relay command must have exactly nine fields.");
            string epoch = ReadText(ref reader, 64);
            int owner = reader.ReadInt32(); ulong requester = reader.ReadUInt64(); long requestId = reader.ReadInt64();
            var action = (RelayAction)reader.ReadInt32(); int x = reader.ReadInt32(), y = reader.ReadInt32();
            uint maxGlobalId = reader.ReadUInt32();
            var encoded = reader.ReadBytes();
            if (!encoded.HasValue || encoded.Value.Length > RelaySlotCodec.MaximumEncodedBytes) throw Bad("Relay unit list is missing or exceeds its size limit.");
            int[] slots = RelaySlotCodec.Decode(System.Buffers.BuffersExtensions.ToArray(encoded.Value));
            return new RelayCommand(epoch, owner, requester, requestId, action, x, y, maxGlobalId, slots);
        }
        private static string ReadText(ref MessagePackReader reader, int maximumCharacters)
        {
            var sequence = reader.ReadStringSequence();
            if (!sequence.HasValue) return string.Empty;
            // UTF-8 uses at most four bytes per scalar, so this bounds the allocation
            // even before the strict decoder and UTF-16 character count are checked.
            if (sequence.Value.Length > maximumCharacters * 4L) throw Bad("Relay string exceeds its byte budget.");
            string result = WireText.GetString(System.Buffers.BuffersExtensions.ToArray(sequence.Value));
            CheckText(result, maximumCharacters); return result;
        }
        private static void Validate(RelayEnvelope value)
        {
            if (value == null || value.Kind < RelayEnvelope.Hello || value.Kind > RelayEnvelope.Execute)
                throw Bad("Unknown relay envelope kind.");
            CheckText(value.Epoch, 64); CheckText(value.Nonce, 64); CheckText(value.Text, 512);
            if (value.Player < 0 || value.Player > 8 || value.Outcome < (int)RelayOutcome.Accepted || value.Outcome > (int)RelayOutcome.Uncertain ||
                value.MessagePacketId < 0 || value.MessagePacketId > short.MaxValue || value.ChorePacketId < 0 || value.ChorePacketId > short.MaxValue)
                throw Bad("Relay envelope metadata is outside its range.");
            bool needsCommand = value.Kind >= RelayEnvelope.Request;
            if (needsCommand != (value.Command != null)) throw Bad("Unexpected or missing relay command.");
            if (value.Command != null)
            {
                CheckText(value.Command.Epoch, 64);
                if (!StringComparer.Ordinal.Equals(value.Command.Epoch, value.Epoch) ||
                    value.Player != 0 && value.Player != value.Command.Owner)
                    throw Bad("Relay envelope and command identities differ.");
            }
        }
        private static void CheckText(string value, int maximumCharacters)
        {
            if (value == null) return;
            if (value.Length > maximumCharacters) throw Bad("Relay string exceeds its character budget.");
            try { WireText.GetByteCount(value); }
            catch (EncoderFallbackException error) { throw new MessagePackSerializationException("Relay string is not valid UTF-8 text.", error); }
        }
        private static MessagePackSerializationException Bad(string reason) => new MessagePackSerializationException(reason);
    }
}

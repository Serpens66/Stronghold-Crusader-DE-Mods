using System;
using System.Buffers;
using System.IO;
using AutoSendTest;
using MessagePack;

internal static class EditTests
{
    private static int checks;
    internal static int Run()
    {
        AtomicBounds(); CoalescedQueue(); LegacyPauseMigration(); ChatMessagesStateAndSave(); ChatMessagesQueue(); PacketRoundTrip(); return checks;
    }
    private static void Check(bool value) { checks++; if (!value) throw new Exception("Edit regression check " + checks + " failed."); }
    private static void Rule(AutoSendRule rule, int min, int max, bool enabled)
    { Check(rule.Minimum == min && rule.Maximum == max && rule.Enabled == enabled); }
    private static void AtomicBounds()
    {
        var state = new AutoSendState(new[] { 901 }, new[] { 100 });
        Check(state.TrySetRule(1, 2, 901, 10, 100, true, out _)); long revision = state.GetRevision(1);
        Check(state.TryEditRule(1, 2, 901, AutoSendEdit.Minimum, 10000, false, out _));
        Rule(state.GetRule(1, 2, 901), 10000, 10000, true); Check(state.GetRevision(1) == revision + 1);
        Check(state.TryEditRule(1, 2, 901, AutoSendEdit.Maximum, 4, false, out _));
        Rule(state.GetRule(1, 2, 901), 4, 4, true);
        Check(state.TryEditRule(1, 2, 901, AutoSendEdit.Maximum, 0, false, out _));
        Rule(state.GetRule(1, 2, 901), 0, 0, false); Check(!state.HasActiveRules(1, 901)); revision = state.GetRevision(1);
        Check(!state.TryEditRule(1, 2, 901, AutoSendEdit.Enabled, 0, true, out _));
        Check(!state.TryEditRule(1, 2, 901, AutoSendEdit.Minimum, -1, false, out _)); Check(state.GetRevision(1) == revision);
        Check(state.TryEditRule(1, 2, 901, AutoSendEdit.Minimum, int.MaxValue, false, out _));
        Rule(state.GetRule(1, 2, 901), int.MaxValue, int.MaxValue, false);
        Check(state.TryEditRule(1, 2, 901, AutoSendEdit.Enabled, 0, true, out _));
        Check(!state.TrySetRule(2, 1, 901, 0, 10, true, out _));
        // A rejected atomic update changes neither graph, bounds nor revision.
        Check(state.TrySetRule(2, 1, 901, 0, 1, false, out _)); revision = state.GetRevision(2);
        Check(!state.TryEditRule(2, 1, 901, AutoSendEdit.Enabled, 0, true, out _));
        Rule(state.GetRule(2, 1, 901), 0, 1, false); Check(state.GetRevision(2) == revision);
    }
    private static AutoSendEditIntent Intent(AutoSendEdit operation, int amount, int recipient = 2, int good = 901, long session = 10)
        => new AutoSendEditIntent(session, 1, operation, recipient, good, amount, false);
    private static void CoalescedQueue()
    {
        var queue = new AutoSendEditQueue();
        var first = Intent(AutoSendEdit.Minimum, 200); queue.Enqueue(first);
        Check(queue.TryBegin(10, 1, 55, 7, out var flight)); Check(ReferenceEquals(first, flight));
        queue.Enqueue(Intent(AutoSendEdit.Minimum, 300)); queue.Enqueue(Intent(AutoSendEdit.Maximum, 20));
        queue.Enqueue(Intent(AutoSendEdit.Minimum, 500)); Check(queue.WaitingCount == 2);
        queue.Enqueue(Intent(AutoSendEdit.Reserve, 40, 2)); queue.Enqueue(Intent(AutoSendEdit.Reserve, 90, 3));
        Check(queue.WaitingCount == 3); // own reserve is shared across recipients
        var other = Intent(AutoSendEdit.Maximum, 700, 3); queue.Enqueue(other);
        queue.Enqueue(Intent(AutoSendEdit.Minimum, 800, 2, 41)); Check(queue.WaitingCount == 5);
        int reserve = 0; var rule = new AutoSendRule(0, 100, false);
        Check(queue.Project(10, 1, 2, 901, ref reserve, ref rule)); Rule(rule, 500, 500, false); Check(reserve == 90);
        reserve = 0; rule = new AutoSendRule(0, 100, false);
        queue.Project(10, 1, 3, 901, ref reserve, ref rule); Rule(rule, 0, 700, false); Check(reserve == 90);
        reserve = 0; rule = new AutoSendRule(0, 100, false);
        Check(!queue.Project(11, 1, 2, 901, ref reserve, ref rule)); Rule(rule, 0, 100, false); Check(reserve == 0);
        Check(!queue.TryBegin(10, 1, 55, 8, out _));
        Check(!queue.Acknowledge(11, 1, 55, 7)); Check(!queue.Acknowledge(10, 2, 55, 7));
        Check(!queue.Acknowledge(10, 1, 56, 7)); Check(!queue.Acknowledge(10, 1, 55, 8)); Check(queue.Pending);
        Check(queue.Acknowledge(10, 1, 55, 7));
        Check(queue.TryBegin(10, 1, 55, 8, out flight)); Check(flight.Operation == AutoSendEdit.Maximum && flight.Amount == 20);
        queue.Failed(first); Check(queue.InFlight == flight); queue.Failed(flight);
        Check(queue.TryBegin(10, 1, 55, 8, out flight)); Check(flight.Operation == AutoSendEdit.Minimum && flight.Amount == 500);
        queue.Reset(); Check(!queue.Pending && queue.WaitingCount == 0);
        queue.Enqueue(Intent(AutoSendEdit.Maximum, 20));
        Check(!queue.TryBegin(11, 1, 66, 1, out _)); Check(!queue.Pending); // old map never inherits queued edits
    }
    private static void LegacyPauseMigration()
    {
        var original = new AutoSendState(new[] { 41, 901 }, new[] { 100, 1000 });
        Check(original.TrySetRule(1, 2, 41, 12, 100, true, out _));
        Check(original.TrySetRule(1, 3, 901, 21, 200, false, out _));
        Check(original.TrySetRule(2, 3, 901, 6, 300, true, out _));
        Check(original.TrySetReserve(1, 41, 999, out _)); Check(original.TrySetPaused(1, true, out _));
        original.RestoreMeta(1, true, long.MaxValue); original.SetCursor(1, 41, 5); original.MarkProcessedTick(123);
        byte[] bytes = IntervalTests.EncodeLegacy(original, 1);
        Check(AutoSendSaveCodec.TryDecode(bytes, new[] { 901, 41 }, new[] { 1000, 100 }, out var migrated, out _));
        Rule(migrated.GetRule(1, 2, 41), 12, 100, false); Rule(migrated.GetRule(1, 3, 901), 21, 200, false);
        Rule(migrated.GetRule(2, 3, 901), 6, 300, true); Check(!migrated.IsPaused(1)); Check(!migrated.HasActiveRules(1, 41));
        Check(migrated.GetReserve(1, 41) == 999 && migrated.GetRevision(1) == long.MaxValue && migrated.GetCursor(1, 41) == 5);
        Check(migrated.HasProcessedTick && migrated.LastProcessedTick == 123);
        Check(AutoSendSaveCodec.TryDecode(AutoSendSaveCodec.Encode(migrated), new[] { 41, 901 }, new[] { 100, 1000 }, out var again, out _));
        Rule(again.GetRule(1, 2, 41), 12, 100, false); Rule(again.GetRule(2, 3, 901), 6, 300, true); Check(!again.IsPaused(1));
        // Nonpaused V1 records retain active rules and all their values.
        Check(original.TrySetPaused(2, false, out _)); bytes = IntervalTests.EncodeLegacy(original, 1);
        Check(AutoSendSaveCodec.TryDecode(bytes, new[] { 41, 901 }, new[] { 100, 1000 }, out migrated, out _));
        Rule(migrated.GetRule(2, 3, 901), 6, 300, true);
    }
    private static void PacketRoundTrip()
    {
        var packet = new AutoSendPacket { Protocol = 4, Epoch = long.MinValue, Revision = long.MaxValue, Operation = (int)AutoSendEdit.SetRule,
            Recipient = 8, Good = 901, Amount = 300, Enabled = true, Minimum = 10000, Maximum = int.MaxValue };
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer); var formatter = new AutoSendPacketFormatter();
        formatter.Serialize(ref writer, packet, null); writer.Flush();
        var reader = new MessagePackReader(new ReadOnlySequence<byte>(buffer.WrittenMemory));
        var restored = formatter.Deserialize(ref reader, null);
        Check(reader.End && restored.Protocol == 4 && restored.Epoch == long.MinValue && restored.Revision == long.MaxValue);
        Check(restored.Operation == (int)AutoSendEdit.SetRule && restored.Recipient == 8 && restored.Good == 901 && restored.Enabled);
        Check(restored.Minimum == 10000 && restored.Maximum == int.MaxValue && restored.Amount == 300);
        foreach (int length in new[] { 0, 1, buffer.WrittenCount - 1 })
        {
            bool rejected = false;
            try { reader = new MessagePackReader(new ReadOnlySequence<byte>(buffer.WrittenMemory.Slice(0, length))); formatter.Deserialize(ref reader, null); }
            catch (Exception) { rejected = true; }
            Check(rejected);
        }
        buffer = new ArrayBufferWriter<byte>(); writer = new MessagePackWriter(buffer); writer.WriteArrayHeader(8);
        writer.Write(1); writer.Write(55L); writer.Write(1L); writer.Write((int)AutoSendEdit.Maximum);
        writer.Write(2); writer.Write(901); writer.Write(100); writer.Write(false); writer.Flush();
        bool oldShapeRejected = false;
        try { reader = new MessagePackReader(new ReadOnlySequence<byte>(buffer.WrittenMemory)); formatter.Deserialize(ref reader, null); }
        catch (InvalidOperationException) { oldShapeRejected = true; }
        Check(oldShapeRejected);
        packet = new AutoSendPacket { Protocol = 4, Epoch = 55, Revision = 9, Operation = (int)AutoSendEdit.ChatMessages, Enabled = true };
        buffer = new ArrayBufferWriter<byte>(); writer = new MessagePackWriter(buffer);
        formatter.Serialize(ref writer, packet, null); writer.Flush();
        reader = new MessagePackReader(new ReadOnlySequence<byte>(buffer.WrittenMemory)); restored = formatter.Deserialize(ref reader, null);
        Check(reader.End && restored.Protocol == 4 && restored.Operation == (int)AutoSendEdit.ChatMessages && restored.Enabled);
        Check(restored.Recipient == 0 && restored.Good == 0 && restored.Amount == 0 && restored.Minimum == 0 && restored.Maximum == 0);
    }
    private static void ChatMessagesStateAndSave()
    {
        var state = new AutoSendState(new[] { 41 }, new[] { 100 });
        for (int player = 1; player <= 8; player++) Check(!state.ShowVanillaChatMessages(player));
        Check(state.TrySetRule(1, 2, 41, 12, 100, true, 7, 17, out _));
        long revision = state.GetRevision(1); int due = state.CollectDueRecipients(1, 41, 18); Check(due == 0);
        Check(state.TrySetShowVanillaChatMessages(1, true, out _));
        Check(state.GetRevision(1) == revision + 1 && state.ShowVanillaChatMessages(1) && !state.ShowVanillaChatMessages(2));
        Check(state.GetRule(1, 2, 41).LastCheckTick == 17 && state.GetRule(1, 2, 41).IntervalSeconds == 7);
        Check(state.CollectDueRecipients(1, 41, 296) == 0 && state.CollectDueRecipients(1, 41, 297) == (1 << 2));
        byte[] bytes = AutoSendSaveCodec.Encode(state);
        Check(AutoSendSaveCodec.TryDecode(bytes, new[] { 41 }, new[] { 100 }, out var restored, out _));
        Check(restored.ShowVanillaChatMessages(1) && !restored.ShowVanillaChatMessages(2));
        Check(restored.GetRevision(1) == state.GetRevision(1) && restored.GetRule(1, 2, 41).LastCheckTick == 297);
        // Boolean fields are canonical bytes; corrupt settings never publish state.
        bytes[4 * 4 + 1 + 4 + 1 + 8] = 2;
        Check(!AutoSendSaveCodec.TryDecode(bytes, new[] { 41 }, new[] { 100 }, out restored, out _) && restored == null);
        foreach (int version in new[] { 1, 2, 3 })
        {
            Check(AutoSendSaveCodec.TryDecode(IntervalTests.EncodeLegacy(state, version), new[] { 41 }, new[] { 100 }, out restored, out _));
            for (int player = 1; player <= 8; player++) Check(!restored.ShowVanillaChatMessages(player));
            Check(restored.GetRule(1, 2, 41).Enabled);
            Check(version < 3 ? !restored.GetRule(1, 2, 41).HasCheckTick : restored.GetRule(1, 2, 41).LastCheckTick == 297);
        }
        revision = state.GetRevision(1);
        foreach (int player in new[] { 0, 9, int.MinValue, int.MaxValue }) Check(!state.TrySetShowVanillaChatMessages(player, true, out _));
        Check(state.GetRevision(1) == revision && state.ShowVanillaChatMessages(1));
        state.RestoreMeta(1, false, long.MaxValue, true);
        Check(!state.TrySetShowVanillaChatMessages(1, false, out _) && state.ShowVanillaChatMessages(1));
    }
    private static void ChatMessagesQueue()
    {
        var queue = new AutoSendEditQueue();
        queue.Enqueue(new AutoSendEditIntent(10, 1, AutoSendEdit.ChatMessages, 2, 41, 0, false));
        queue.Enqueue(new AutoSendEditIntent(10, 1, AutoSendEdit.ChatMessages, 8, 901, 0, true));
        Check(queue.WaitingCount == 1); // Selection changes cannot split a sender-wide key.
        int reserve = 7; var rule = new AutoSendRule(10, 100, true, 7, true, 17);
        Check(!queue.Project(10, 1, 8, 901, ref reserve, ref rule));
        Check(reserve == 7 && rule.Minimum == 10 && rule.IntervalSeconds == 7 && rule.LastCheckTick == 17);
        Check(queue.TryBegin(10, 1, 55, 4, out var flight));
        Check(flight.Enabled && flight.Good == 0 && flight.Recipient == 0 && flight.Operation == AutoSendEdit.ChatMessages);
        Check(!queue.Acknowledge(10, 2, 55, 4) && !queue.Acknowledge(11, 1, 55, 4));
        Check(queue.Acknowledge(10, 1, 55, 4) && !queue.Pending);
        queue.Enqueue(new AutoSendEditIntent(10, 1, AutoSendEdit.ChatMessages, 0, 0, 0, true));
        Check(!queue.TryBegin(11, 1, 66, 1, out _) && !queue.Pending);
    }
}

using System;
using System.Buffers;
using System.IO;
using BugfixesAndQoL.AutoSend;
using MessagePack;

internal static class EditTests
{
    private static int checks;
    internal static int Run()
    {
        DisclosureState(); PendingDisclosureClick(); SliderCompletion(); AtomicBounds(); CoalescedQueue(); LegacyPauseMigration(); ChatMessagesStateAndSave(); ChatMessagesQueue(); PacketRoundTrip(); return checks;
    }
    private static void Check(bool value) { checks++; if (!value) throw new Exception("Edit regression check " + checks + " failed."); }
    private static void Rule(AutoSendRule rule, int min, int max, bool enabled)
    { Check(rule.Minimum == min && rule.Maximum == max && rule.Enabled == enabled); }
    private static void DisclosureState()
    {
        var ui = new AutoSendDisclosureState();
        Check(!ui.Expanded && !ui.IsVisible(true));
        ui.Toggle(0); ui.ObserveGoodsClick(true); Check(!ui.Expanded);
        ui.ObserveMenu(0, true, 10); ui.Toggle(0); ui.ObserveGoodsClick(true); Check(!ui.Expanded);
        // Merely opening on a preselected active rule never generates a goods click.
        ui.ObserveMenu(1, true, 10); Check(!ui.Expanded && !ui.IsVisible(true));
        ui.ObserveGoodsClick(false); Check(!ui.Expanded);
        ui.ObserveGoodsClick(true); Check(ui.Expanded && ui.IsVisible(true));
        ui.ObserveGoodsClick(false); Check(ui.Expanded);
        // Selection changes repeat the same menu identity, not a menu reset.
        ui.ObserveMenu(1, true, 10); Check(ui.IsVisible(true));
        ui.ObserveMenu(1, true, 10); Check(ui.IsVisible(true));
        // Temporarily unavailable recipients/layout only hide the preserved state.
        Check(!ui.IsVisible(true, false) && ui.Expanded);
        Check(!ui.IsVisible(true, true, false) && ui.Expanded);
        Check(ui.IsVisible(true, true, true));
        ui.Toggle(1); Check(!ui.Expanded && !ui.IsVisible(true));
        ui.ObserveMenu(1, true, 10); ui.ObserveGoodsClick(false); Check(!ui.Expanded);
        // Reclicking the same active good is a new user action and opens again.
        ui.ObserveGoodsClick(true); Check(ui.Expanded);
        ui.Toggle(0); ui.Toggle(2); Check(ui.Expanded); // zero/stale session cannot toggle
        ui.ObserveMenu(1, false, 10); Check(!ui.Expanded && !ui.IsVisible(false));
        ui.Toggle(1); ui.ObserveGoodsClick(true); Check(!ui.Expanded); // closure or request mode
        ui.ObserveMenu(1, true, 10); Check(!ui.Expanded); // reopening retains no old expansion
        ui.Toggle(1); Check(ui.Expanded); // manual opening needs no active rule
        ui.ObserveMenu(1, false, 10); ui.ObserveMenu(1, true, 10); Check(!ui.Expanded);
        ui.ObserveGoodsClick(true); ui.ObserveMenu(1, true, 11); Check(!ui.Expanded); // new HUD
        ui.Toggle(1); Check(ui.Expanded);
        ui.ObserveMenu(2, true, 11); Check(!ui.Expanded); // new mission or loaded save
        ui.Toggle(2); Check(ui.Expanded);
        ui.ObserveMenu(0, true, 11); ui.ObserveGoodsClick(true); ui.Toggle(0); Check(!ui.Expanded);
        ui.ObserveMenu(3, true, 11); Check(!ui.Expanded);
        Check(AutoSendDisclosureState.UnavailableReason(false, false, false, true, AutoSendRecipientReadiness.NotAllied, true) == "NotReady");
        Check(AutoSendDisclosureState.UnavailableReason(true, false, false, true, AutoSendRecipientReadiness.Unknown, true) == "NotReady");
        Check(AutoSendDisclosureState.UnavailableReason(true, false, false, true, AutoSendRecipientReadiness.NotAllied, true) == "RecipientNotAllied");
        Check(AutoSendDisclosureState.UnavailableReason(true, false, false, true, AutoSendRecipientReadiness.NotLiving, true) == "RecipientNotLiving");
        Check(AutoSendDisclosureState.UnavailableReason(true, false, false, false, AutoSendRecipientReadiness.NotAllied, true) == "OwnView");
        Check(AutoSendDisclosureState.UnavailableReason(true, false, false, true, AutoSendRecipientReadiness.AlliedLiving, false) == "LayoutUnavailable");
        Check(AutoSendDisclosureState.UnavailableReason(true, false, false, true, AutoSendRecipientReadiness.AlliedLiving, true) == null);
    }
    private static void PendingDisclosureClick()
    {
        var pending = new AutoSendPendingDisclosureClick();
        var original = new AutoSendDisclosureTarget(10, 20, 1, 1, 2, 41);
        var same = new AutoSendDisclosureTarget(10, 20, 1, 1, 2, 41);
        Check(!pending.HasPending && !pending.TryConsume(original));
        pending.Record(original); Check(pending.HasPending);
        Check(pending.TryConsume(same) && !pending.HasPending);
        Check(!pending.TryConsume(same)); // a click is consumed at most once
        foreach (var changed in new[] {
            new AutoSendDisclosureTarget(11, 20, 1, 1, 2, 41),
            new AutoSendDisclosureTarget(10, 21, 1, 1, 2, 41),
            new AutoSendDisclosureTarget(10, 20, 3, 1, 2, 41),
            new AutoSendDisclosureTarget(10, 20, 1, 3, 2, 41),
            new AutoSendDisclosureTarget(10, 20, 1, 1, 3, 41),
            new AutoSendDisclosureTarget(10, 20, 1, 1, 2, 901) })
        {
            pending.Record(original);
            Check(!pending.TryConsume(changed) && !pending.HasPending);
            Check(!pending.TryConsume(original)); // mismatch also discards the stale token
            pending.Record(original); pending.Record(changed);
            Check(pending.TryConsume(changed) && !pending.HasPending); // newest click wins
        }
        pending.Record(original); pending.Cancel(); pending.Cancel();
        Check(!pending.HasPending && !pending.TryConsume(original));
        pending.Record(original); Check(pending.TryConsume(same)); // cancellation does not disable future clicks
    }
    private static void SliderCompletion()
    {
        var gesture = new AutoSendSliderGesture();
        // Track clicks can change Value without an observed ValueChanged callback.
        gesture.Begin(13); long first = gesture.Generation;
        Check(gesture.TryComplete(first, 50, out bool submit) && submit);
        Check(!gesture.TryComplete(first, 50, out submit) && !submit);
        // Completion reads the final position even if the last notification lagged.
        gesture.Begin(50); gesture.ValueChanged();
        Check(gesture.TryComplete(gesture.Generation, 87, out submit) && submit);
        // A clamped thumb alone must not overwrite an out-of-range textbox value.
        gesture.Begin(100);
        Check(gesture.TryComplete(gesture.Generation, 100, out submit) && !submit);
        // Deliberate Home/End or dragging away and back still selects that bound.
        gesture.Begin(100); gesture.ValueChanged();
        Check(gesture.TryComplete(gesture.Generation, 100, out submit) && submit);
        // A queued capture-loss callback cannot finish a subsequent drag.
        gesture.Begin(25); first = gesture.Generation;
        gesture.Begin(40); long second = gesture.Generation;
        Check(!gesture.TryComplete(first, 60, out submit) && !submit);
        Check(gesture.TryComplete(second, 75, out submit) && submit);
        // Escape, context changes and unloading invalidate all deferred callbacks.
        gesture.Begin(10); first = gesture.Generation; gesture.Cancel();
        gesture.ValueChanged();
        Check(!gesture.TryComplete(first, 80, out submit) && !submit);
        gesture.Begin(10000);
        Check(gesture.TryComplete(gesture.Generation, 4970, out submit) && submit);
        gesture.Begin(30);
        Check(gesture.TryComplete(gesture.Generation, 300, out submit) && submit);
    }
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

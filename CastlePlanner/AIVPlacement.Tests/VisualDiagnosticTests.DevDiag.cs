using CastlePlanner;

internal static class VisualDiagnosticTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        Check(VisualDiagnosticSavePlayers.Select(false, 1, null).SequenceEqual(new[] { 1 }), "Singleplayer save selects lifecycle local player with missing multiplayer roster (observed failure)");
        Check(VisualDiagnosticSavePlayers.Select(false, 3, new[] { 1, 2 }).SequenceEqual(new[] { 3 }), "Unrelated or stale lobby members cannot override the loaded local player");
        Check(VisualDiagnosticSavePlayers.Select(true, 3, new[] { 2, 2, 1 }).SequenceEqual(new[] { 1, 2, 3 }), "Multiplayer preserves human roster plus confirmed local identity without duplicates");
        Check(VisualDiagnosticSavePlayers.Select(false, null, new[] { 1 }).Length == 0, "Unassigned local identity is explicit, never guessed from a stale singleplayer roster");
        Check(VisualDiagnosticSavePlayers.Select(false, 0, null).Length == 0, "Unassigned zero is not a player");
        var load = new VisualDiagnosticSessionState(12, true);
        Check(!load.Active && !load.ConfirmSave(11), "Save observation waits for successful start of its own session");
        Check(load.ConfirmSave(12) && load.Active && !load.ConfirmSave(12), "Successful save start delivered exactly once");
        load.End();
        Check(!load.Active && !load.ConfirmSave(12), "Late callbacks cannot revive an ended map");
        Check(new VisualDiagnosticSessionState(13, false).Active, "New castle keeps early native captures");
        var packets = new short[21 * 3]; packets[42] = -1;
        Check(VisualDiagnosticPackets.Count(packets, -1, out bool packetBounded) == 2 && !packetBounded, "Native packet sentinel respected");
        Check(VisualDiagnosticPackets.Count(packets, 20, out packetBounded) == 3 && packetBounded, "Truncated packet cannot escape input span");
        Check(VisualDiagnosticPackets.Count(new short[22], -1, out packetBounded) == 1 && packetBounded, "Missing sentinel reported within actual capacity");
        Check(VisualDiagnosticPackets.Count(null, 3, out packetBounded) == 0 && packetBounded, "Null packet observation isolated");
        packets[1] = 7; packets[22] = 7;
        short[] raw1 = VisualDiagnosticPackets.Copy(packets, 0), raw2 = VisualDiagnosticPackets.Copy(packets, 1);
        packets[1] = 99;
        Check(raw1[1] == 7 && raw2[1] == 7 && !ReferenceEquals(raw1, raw2), "Repeated same-tile records preserved as independent immutable copies");
        Check(VisualDiagnosticPackets.Key(-1, 1) != VisualDiagnosticPackets.Key(1, -1), "Signed coordinates cannot alias");
        var trace = new VisualDiagnosticTraceBuffer(1024);
        trace.Add("frame=1", "hover", "tile", "false", true);
        trace.Add("frame=1", "hover", "tile", "true", true);
        trace.Add("frame=1", "hover", "tile", "false", true);
        trace.Add("frame=1", "state", "tile", "same"); trace.Add("frame=2", "state", "tile", "same");
        string events = trace.Drain();
        Check(events.Split('\n').Count(x => x.Length != 0) == 4 && events.Contains("true"), "Sub-frame hover flicker retained while unchanged states deduplicate");
        Check(trace.Bytes == 0, "Flush retires buffered immutable events");
        var ring = new VisualDiagnosticTraceBuffer(80);
        for (int n = 0; n < 20; n++) ring.Add("t", "packet", "tile", "éééééééé", true);
        Check(ring.Bytes <= 80 && ring.Dropped > 0 && ring.Drain().Contains("20\t"), "Ring drops oldest data with UTF8 byte accounting and visible sequence gap");
        int originals = 0, observations = 0;
        VisualDiagnosticCall.Around(() => throw new Exception("before"), () => originals++, () => throw new Exception("after"), _ => observations++);
        Check(originals == 1 && observations == 2, "Diagnostic failures never suppress or duplicate Vanilla");
        bool originalFailure = false;
        try { VisualDiagnosticCall.Around(() => { }, () => { originals++; throw new InvalidOperationException("Vanilla"); }, () => observations++, _ => { }); }
        catch (InvalidOperationException) { originalFailure = true; }
        Check(originalFailure && originals == 2 && observations == 2, "Original exceptions propagate without a second original or false after-result");
        var region = new VisualDiagnosticRegion(-2, -2, 101, 101);
        Check(region.Valid && region.Contains(0, 0) && !region.Contains(102, 0), "Source edge region preserves unavailable coordinates");
        Check(region.Intersects(100, 100, int.MaxValue), "Unknown oversized footprint remains evidence without overflow");
        Check(!new VisualDiagnosticRegion(0, 0, int.MaxValue, 3).Valid, "Unbounded capture region rejected");
        Check(!VisualDiagnosticRegion.IsReadableTile(-1, 320800) &&
            !VisualDiagnosticRegion.IsReadableTile(320800, 320800) &&
            VisualDiagnosticRegion.IsReadableTile(320799, 320800), "Native span limits checked before access");
        var identities = new VisualDiagnosticIdentities();
        Check(!identities.Observe(10, 100, out _) && !identities.Observe(10, 100, out _), "Stable identity is not a finding");
        Check(identities.Observe(10, 101, out uint oldGlobal) && oldGlobal == 100, "Slot reuse keeps both global identities");
        Check(!new VisualDiagnosticIdentities().Observe(10, 101, out _), "New session does not inherit old identities");
        var window = new VisualDiagnosticWindow(100);
        Check(window.Take(100, 0, false, 5, 6, 10) == "first-ui-frame", "initial snapshot");
        Check(window.Take(100.2, 2, false, 5, 6, 10) == null, "change capture rate limited");
        Check(window.Take(100.5, 2, false, 5, 6, 10) == "camera-rotation", "rotation persists until sampled");
        Check(window.Take(101, 2, false, 5, 6, 10) == "after-one-second", "one-second snapshot");
        Check(window.Take(101.5, 2, true, 5, 6, 10) == "blueprint-visibility", "blueprint visibility captured");
        Check(window.Take(102, 2, true, 5, 6, 11) == "hover-change", "reused hover slot/new building detected");
        Check(window.Take(102.5, 2, true, 5, 6, 11) == "hover-observe", "stationary hover flicker sampled without requiring cursor movement");
        Check(window.Take(105, 2, true, 5, 6, 11) == "after-five-seconds", "five-second snapshot");
        Check(window.Take(285, 4, false, 0, 0, 0) == null && window.Expired(285), "bounded observation window");
        var replacement = new VisualDiagnosticWindow(300);
        Check(replacement.Take(300, 0, false, 0, 0, 0) == "first-ui-frame", "replacement session owns fresh observation state");

        string directory = TestWorkDirectory.New("castle-visual");
        var messages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var writer = new VisualDiagnosticWriter(directory, messages.Enqueue, maximumBytes: 4);
        Check(writer.Enqueue("first", "éé"), "exact UTF8 byte budget accepted");
        Check(!writer.Enqueue("over-limit", "x") && writer.Dropped == 1, "UTF8 budget not character count");
        writer.Complete();
        Check(!writer.Enqueue("old-session", ""), "ended session rejects queued producers");
        Check(writer.Completion.Wait(5000), "background writer drains without Unity callbacks");
        Check(Directory.GetFiles(directory, "*.tsv").Length == 1 &&
            File.ReadAllText(Directory.GetFiles(directory, "*.tsv")[0]) == "éé", "immutable capture preserved exactly");
        Check(messages.Any(x => x.Contains("capture-dropped")) && messages.Any(x => x.Contains("writer-drained")), "limits and completion explicitly reported");
        string invalid = Path.Combine(directory, "blocked"); File.WriteAllText(invalid, "regular file");
        var failed = new VisualDiagnosticWriter(invalid, messages.Enqueue);
        Check(failed.Completion.Wait(5000), "writer error cannot hang shutdown");
        Check(!failed.Enqueue("after-failure", "data") && messages.Any(x => x.Contains("writer-failed")), "I/O failure is reported and later writes rejected");
        failed.Complete();
        using var entered = new System.Threading.ManualResetEventSlim();
        using var release = new System.Threading.ManualResetEventSlim();
        var bounded = new VisualDiagnosticWriter(Path.Combine(directory, "bounded"), message => {
            if (message.StartsWith("snapshot-written")) { entered.Set(); release.Wait(5000); }
        });
        Check(bounded.Enqueue("active", "first") && entered.Wait(5000), "writer active before queue bound test");
        for (int index = 0; index < 8; index++) Check(bounded.Enqueue("pending", "copy " + index), "bounded pending capture accepted");
        Check(!bounded.Enqueue("overflow", "extra"), "pending queue remains bounded during slow I/O");
        bounded.Complete(); release.Set();
        Check(bounded.Completion.Wait(5000), "bounded writer drains after map end");
    }
}

namespace Spectator
{
    public static class PowerSelectionCancellationTests
    {
        private static int checks;
        private sealed class Fixture
        {
            internal readonly object Gate = new object();
            internal readonly System.Threading.ManualResetEvent Done = new System.Threading.ManualResetEvent(false);
            internal object Pending;
            internal long Generation = 10;
            internal int NativeCalls;
            internal System.Exception Error;
            internal System.Action<object> Native;

            internal System.Threading.Thread Start()
            {
                var thread = new System.Threading.Thread(() => {
                    try { PowerSelectionCancellation.Cancel(ref Pending, ref Generation, Gate, Native); }
                    catch (System.Exception error) { Error = error; }
                    finally { Done.Set(); }
                });
                thread.IsBackground = true;
                thread.Start();
                return thread;
            }
        }

        private static void Expect(bool value, string name)
        { checks++; if (!value) throw new System.Exception("Power cancellation: " + name); }

        public static int Run()
        {
            checks = 0;
            // The success criterion is return BEFORE release, not an operation-speed benchmark.
            var empty = new Fixture();
            empty.Native = value => { empty.NativeCalls++; };
            bool returned;
            System.Threading.Thread worker;
            lock (empty.Gate)
            {
                worker = empty.Start();
                returned = empty.Done.WaitOne(2000);
            }
            Expect(worker.Join(5000), "empty worker finishes");
            Expect(returned && empty.Error == null, "idle cancellation never waits for another engine owner");
            Expect(empty.Generation == 11 && empty.NativeCalls == 0, "idle cancellation still invalidates queued generation");
            Expect(!ToolPolicy.PowerContextMatches(1, 1, 2, 2, 10, empty.Generation, true), "consumed old action is rejected even without pending selection");

            var owned = new Fixture { Pending = new object() };
            object selection = owned.Pending;
            owned.Native = value => {
                Expect(System.Threading.Monitor.IsEntered(owned.Gate), "native cleanup owns engine monitor");
                Expect(ReferenceEquals(selection, value) && owned.Pending == null, "captured selection is detached before native cleanup");
                owned.NativeCalls++;
            };
            bool invalidated, completedWhileHeld;
            lock (owned.Gate)
            {
                worker = owned.Start();
                invalidated = System.Threading.SpinWait.SpinUntil(() => System.Threading.Interlocked.Read(ref owned.Generation) != 10, 2000);
                completedWhileHeld = owned.Done.WaitOne(0);
            }
            Expect(worker.Join(5000), "owned worker finishes");
            Expect(invalidated && !completedWhileHeld, "generation invalidates before waiting but native cleanup remains serialized");
            Expect(owned.Error == null && owned.NativeCalls == 1, "owned selection cancels exactly once");

            var consumed = new Fixture { Pending = new object() };
            selection = consumed.Pending;
            consumed.Native = value => { consumed.NativeCalls++; };
            bool confirmationWon;
            lock (consumed.Gate)
            {
                worker = consumed.Start();
                invalidated = System.Threading.SpinWait.SpinUntil(() => System.Threading.Interlocked.Read(ref consumed.Generation) != 10, 2000);
                // Simulate native confirmation consuming an already captured old selection
                // while cancellation waits. It never publishes a new non-null selection.
                confirmationWon = ReferenceEquals(System.Threading.Interlocked.CompareExchange(ref consumed.Pending, null, selection), selection);
            }
            Expect(worker.Join(5000), "confirmation worker finishes");
            Expect(invalidated && confirmationWon && consumed.Error == null, "native confirmation and cancellation safely overlap");
            Expect(consumed.NativeCalls == 0, "consumed selection is not canceled again after monitor acquisition");
            Expect(!ToolPolicy.PowerContextMatches(3, 3, 2, 2, 10, consumed.Generation, true), "confirmation queued with old generation cannot execute");

            lock (owned.Gate)
            {
                PowerSelectionCancellation.Cancel(ref owned.Pending, ref owned.Generation, owned.Gate, owned.Native);
                owned.Pending = new object(); selection = owned.Pending;
                PowerSelectionCancellation.Cancel(ref owned.Pending, ref owned.Generation, owned.Gate, owned.Native);
            }
            Expect(owned.Generation == 13 && owned.NativeCalls == 2, "repeat cancellation and reentrant selection keep exact generations");
            Expect(!ToolPolicy.PowerContextMatches(1, 2, 2, 2, owned.Generation, owned.Generation, true), "new generation does not revive an old session");
            Expect(!ToolPolicy.PowerContextMatches(2, 2, 2, 3, owned.Generation, owned.Generation, true), "new generation does not revive another player's action");
            Expect(ToolPolicy.PowerContextMatches(2, 2, 3, 3, owned.Generation, owned.Generation, true), "fresh matching request remains valid");

            object pending = new object(); long generation = 4; int calls = 0;
            PowerSelectionCancellation.Cancel(ref pending, ref generation, null, value => { calls++; });
            Expect(pending == null && generation == 5 && calls == 0, "startup without engine monitor clears only managed state");
            pending = new object();
            var gate = new object();
            try
            {
                PowerSelectionCancellation.Cancel(ref pending, ref generation, gate, value => { throw new System.InvalidOperationException("fixture"); });
                Expect(false, "native error propagates");
            }
            catch (System.InvalidOperationException) { Expect(pending == null && generation == 6, "native error cannot restore canceled state"); }
            Expect(!System.Threading.Monitor.IsEntered(gate), "native failure releases monitor");
            return checks;
        }
    }
}

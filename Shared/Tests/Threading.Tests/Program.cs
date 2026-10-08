using System.Collections.Concurrent;
using System.Threading;

internal static class Program
{
    private static int Main()
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            Assert(!Shared.UnityMainThreadDispatch.TryEnqueue(
                () => throw new InvalidOperationException("must not execute")),
                "uninitialized dispatch did not fail closed");
            AssertThrows<InvalidOperationException>(
                Shared.UnityMainThreadDispatch.InitializeForCurrentThread,
                "invalid synchronization context was accepted");

            var context = new UnityEngine.UnitySynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();

            var executed = new List<(int Producer, int Item)>();
            object gate = new object();
            const int producerCount = 8;
            const int itemsPerProducer = 200;
            Parallel.For(0, producerCount, producer =>
            {
                for (int item = 0; item < itemsPerProducer; item++)
                {
                    int producerSnapshot = producer;
                    int itemSnapshot = item;
                    Assert(Shared.UnityMainThreadDispatch.TryEnqueue(() =>
                    {
                        lock (gate)
                            executed.Add((producerSnapshot, itemSnapshot));
                    }), "concurrent enqueue was rejected");
                }
            });

            Assert(context.PendingCount == 1, "parallel producers scheduled more than one drain");
            context.RunOne();
            Assert(executed.Count == producerCount * itemsPerProducer,
                "parallel producer work was lost or duplicated");
            for (int producer = 0; producer < producerCount; producer++)
            {
                int[] items = executed
                    .Where(entry => entry.Producer == producer)
                    .Select(entry => entry.Item)
                    .ToArray();
                Assert(items.SequenceEqual(Enumerable.Range(0, itemsPerProducer)),
                    $"FIFO order changed for producer {producer}");
            }

            var reentrant = new List<int>();
            Shared.UnityMainThreadDispatch.TryEnqueue(() =>
            {
                reentrant.Add(1);
                Shared.UnityMainThreadDispatch.TryEnqueue(() => reentrant.Add(3));
            });
            Shared.UnityMainThreadDispatch.TryEnqueue(() =>
            {
                reentrant.Add(2);
                throw new InvalidOperationException("intentional callback failure");
            });
            context.RunOne();
            Assert(reentrant.SequenceEqual(new[] { 1, 2, 3 }),
                "reentrant FIFO drain or callback isolation failed");

            Shared.UnityMainThreadDispatch.TryEnqueue(() => reentrant.Add(4));
            Assert(context.PendingCount == 1,
                "idle-to-active transition lost its wake-up");
            context.RunOne();
            Assert(reentrant.SequenceEqual(new[] { 1, 2, 3, 4 }),
                "post-drain work was lost");

            bool ranInline = false;
            Shared.UnityMainThreadDispatch.TryEnqueue(() => ranInline = true);
            Assert(!ranInline, "TryEnqueue executed inline on the main thread");
            context.RunOne();
            Assert(ranInline, "deferred main-thread work did not run");

            bool wrongThreadWorkRan = false;
            Shared.UnityMainThreadDispatch.TryEnqueue(() => wrongThreadWorkRan = true);
            context.RunOneOnWorker();
            Assert(!wrongThreadWorkRan,
                "a callback posted onto the wrong thread was executed");
            Assert(Shared.UnityMainThreadDispatch.TryEnqueue(() => ranInline = false),
                "dispatcher did not recover after a wrong-thread post");
            context.RunOne();
            Assert(!ranInline,
                "main-thread work was lost after rejecting a wrong-thread post");

            context.ThrowOnNextPost = true;
            bool staleWorkRan = false;
            Assert(!Shared.UnityMainThreadDispatch.TryEnqueue(() => staleWorkRan = true),
                "failed SynchronizationContext.Post was reported as scheduled");
            Assert(Shared.UnityMainThreadDispatch.TryEnqueue(() => ranInline = false),
                "dispatcher did not recover after a failed post");
            context.RunOne();
            Assert(!staleWorkRan && !ranInline,
                "work from a failed post was retained or recovery work was lost");

            Console.WriteLine("PASS main-thread dispatcher concurrency and liveness contracts");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL " + ex);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertThrows<T>(Action action, string message)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}

namespace UnityEngine
{
    internal sealed class UnitySynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> queue =
            new ConcurrentQueue<(SendOrPostCallback Callback, object State)>();

        public int PendingCount => queue.Count;
        public bool ThrowOnNextPost { get; set; }

        public override void Post(SendOrPostCallback d, object state)
        {
            if (ThrowOnNextPost)
            {
                ThrowOnNextPost = false;
                throw new InvalidOperationException("intentional post failure");
            }
            queue.Enqueue((d, state));
        }

        public void RunOne()
        {
            if (!queue.TryDequeue(out var work))
                throw new InvalidOperationException("No posted callback was available.");
            work.Callback(work.State);
        }

        public void RunOneOnWorker()
        {
            if (!queue.TryDequeue(out var work))
                throw new InvalidOperationException("No posted callback was available.");
            Task.Run(() => work.Callback(work.State)).GetAwaiter().GetResult();
        }
    }
}

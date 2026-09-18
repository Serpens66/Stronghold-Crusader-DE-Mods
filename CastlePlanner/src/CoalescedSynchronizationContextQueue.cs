using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CastlePlanner
{
    internal sealed class CoalescedSynchronizationContextQueue<T>
    {
        private readonly ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
        private readonly SynchronizationContext context;
        private readonly Action<T> dispatch;
        private int dispatchScheduled;

        public CoalescedSynchronizationContextQueue(
            SynchronizationContext context,
            Action<T> dispatch)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        }

        public void Enqueue(T item)
        {
            queue.Enqueue(item);
            EnsureDispatchScheduled();
        }

        private void EnsureDispatchScheduled()
        {
            if (Interlocked.CompareExchange(ref dispatchScheduled, 1, 0) != 0)
                return;

            try
            {
                context.Post(Drain, null);
            }
            catch
            {
                Interlocked.Exchange(ref dispatchScheduled, 0);
                throw;
            }
        }

        private void Drain(object state)
        {
            try
            {
                while (queue.TryDequeue(out T item))
                    dispatch(item);
            }
            finally
            {
                Interlocked.Exchange(ref dispatchScheduled, 0);
                if (!queue.IsEmpty)
                    EnsureDispatchScheduled();
            }
        }
    }
}

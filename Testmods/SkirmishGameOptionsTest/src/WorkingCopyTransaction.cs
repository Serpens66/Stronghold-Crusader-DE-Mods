using System;

namespace SkirmishGameOptionsTest
{
    internal sealed class WorkingCopyTransaction<T> where T : class
    {
        private readonly Func<T, T> clone;

        internal WorkingCopyTransaction(Func<T, T> clone)
        {
            this.clone = clone ?? throw new ArgumentNullException(nameof(clone));
        }

        internal T Working { get; private set; }

        internal T Begin(T committed)
        {
            if (committed == null)
                throw new ArgumentNullException(nameof(committed));

            Working = clone(committed) ??
                throw new InvalidOperationException("The working-copy clone returned null.");
            return Working;
        }

        internal void Attach(T working)
        {
            Working = working ?? throw new ArgumentNullException(nameof(working));
        }

        internal void ApplyTo(T committed, Action<T, T> copy)
        {
            if (Working == null)
                throw new InvalidOperationException("No working copy is active.");
            if (committed == null)
                throw new ArgumentNullException(nameof(committed));
            if (copy == null)
                throw new ArgumentNullException(nameof(copy));

            copy(Working, committed);
            Working = null;
        }

        internal void Cancel()
        {
            Working = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using RedBird.Abstractions.Hooks;

namespace AISellThresholdFixTest
{
    // Standalone process-lifetime boundary. Activation may write a jump and then
    // throw, so neither a successful return nor IsInstalled defines publication.
    internal sealed class HookPublication
    {
        private static readonly List<HookPublication> Roots = new List<HookPublication>();
        private object owner;
        private int state;
        internal bool HasStarted => Volatile.Read(ref state) != 0;
        internal bool IsReady => Volatile.Read(ref state) == 2;

        internal void Publish(object candidate, IEnumerable<IHook> hooks, Action validate)
        {
            if (candidate == null || hooks == null || validate == null) throw new ArgumentNullException();
            lock (Roots)
            {
                if (HasStarted) throw new InvalidOperationException("Native publication cannot be retried.");
                owner = candidate;
                Roots.Add(this);
                Volatile.Write(ref state, 1);
            }
            try
            {
                foreach (var hook in hooks) hook.Enable();
                validate();
                Volatile.Write(ref state, 2);
            }
            catch { Volatile.Write(ref state, 3); throw; }
        }

        internal void RollbackUnpublished(Action rollback)
        {
            lock (Roots)
            {
                if (HasStarted) return;
                rollback();
            }
        }
    }
}

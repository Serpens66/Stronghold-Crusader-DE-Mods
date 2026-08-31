using System.Collections.Generic;

namespace ExtremePowers.API
{
    public sealed class ExtremePowerOperationTracker
    {
        private readonly object gate = new object();
        private readonly HashSet<string> completed = new HashSet<string>(System.StringComparer.Ordinal);

        public bool TryBegin(int playerId, ulong operationId)
        {
            if (playerId < 1 || playerId > 8 || operationId == 0) return false;
            lock (gate) return completed.Add(playerId + ":" + operationId);
        }

        public void Reset()
        {
            lock (gate) completed.Clear();
        }
    }
}

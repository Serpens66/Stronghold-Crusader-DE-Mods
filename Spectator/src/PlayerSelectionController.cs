using System;

namespace Spectator
{
    // Production transition boundary: every origin shares cancellation and publication semantics.
    internal sealed class PlayerSelectionController
    {
        private readonly Action cancel;
        private readonly Action<int> publish;
        internal int PlayerId { get; private set; } = -1;
        internal PlayerSelectionController(Action cancel, Action<int> publish)
        { this.cancel = cancel; this.publish = publish; }
        internal bool Select(int player)
        {
            if (player < 1 || player > 8) player = -1;
            if (PlayerId == player) return false;
            cancel();
            PlayerId = player;
            publish(player);
            return true;
        }
    }
}

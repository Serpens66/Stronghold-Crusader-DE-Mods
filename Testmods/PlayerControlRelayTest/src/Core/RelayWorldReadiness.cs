using System;

namespace PlayerControlRelayTest
{
    // Publication is separate from preparing Unity-owned handshake state. Token
    // reference identity prevents a delayed callback from preparing a replaced world,
    // including when a caller accidentally presents the same numeric ID again.
    internal sealed class RelayWorldReadiness
    {
        internal sealed class Token
        {
            internal Token(long world) { World = world; }
            internal long World { get; }
        }
        private readonly object gate = new object();
        private Token current;
        private bool prepared;
        internal Token Begin(long world)
        {
            if (world <= 0) throw new ArgumentOutOfRangeException(nameof(world));
            lock (gate)
            {
                current = new Token(world); prepared = false; return current;
            }
        }
        internal void Reset()
        {
            lock (gate) { current = null; prepared = false; }
        }
        internal bool Prepare(Token token)
        {
            lock (gate)
            {
                if (token == null || !ReferenceEquals(current, token)) return false;
                prepared = true; return true;
            }
        }
        internal bool IsPrepared(Token token)
        {
            lock (gate) return token != null && ReferenceEquals(current, token) && prepared;
        }
    }
}

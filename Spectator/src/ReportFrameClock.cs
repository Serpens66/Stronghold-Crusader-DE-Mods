using System;
using System.Collections.Generic;
using System.Reflection;

namespace Spectator
{
    // Encapsulated, validated read-only access. Never consume/flush Vanilla's render queue.
    internal sealed class ReportFrameClock
    {
        private static readonly FieldInfo counter = typeof(MemoryBuffers).GetField("commonFrameID", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo pool = typeof(MemoryBuffers).GetField("bufferPool", BindingFlags.Instance | BindingFlags.NonPublic);
        private MemoryBuffers owner;
        private List<MemoryBuffers.MemBuffer> buffers;
        internal static void Validate()
        {
            if (counter?.FieldType != typeof(int) || pool?.FieldType != typeof(List<MemoryBuffers.MemBuffer>))
                throw new MissingMemberException("Vanilla report buffer clock contract changed.");
        }
        internal int Capture(object engineGate)
        {
            Validate();
            if (engineGate == null) throw new InvalidOperationException("Report engine gate unavailable.");
            lock (engineGate)
            {
                if (MemoryBuffers.instance == null) throw new InvalidOperationException("Report buffers unavailable.");
                if (!ReferenceEquals(owner, MemoryBuffers.instance))
                { owner = MemoryBuffers.instance; buffers = (List<MemoryBuffers.MemBuffer>)pool.GetValue(owner); }
                return (int)counter.GetValue(null);
            }
        }
        internal bool TryRead(EngineInterface.PlayState snapshot, out int frame)
        {
            frame = 0;
            if (snapshot == null || buffers == null || !ReferenceEquals(owner, MemoryBuffers.instance)) return false;
            // Called by the existing setGameState postfix while the rendered buffer is still locked.
            foreach (var buffer in buffers)
                if (ReferenceEquals(buffer.gameState, snapshot)) { frame = unchecked((int)buffer.frameID); return true; }
            return false;
        }
        internal void Clear() { owner = null; buffers = null; }
    }
}

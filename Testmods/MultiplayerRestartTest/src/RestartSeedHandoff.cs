using System;

namespace MultiplayerRestartTest
{
    // Main-thread only. Hold Vanilla's one-shot seed until every peer has left
    // its lobby update. The original sender and packet framing remain unchanged.
    internal sealed class RestartSeedHandoff
    {
        private Platform_Multiplayer.MPData pending;
        private bool released;
        internal bool InstantMessage { get; private set; }

        internal void Hold(Platform_Multiplayer.MPData packet, bool instantMessage)
        {
            if (released || pending != null || packet == null ||
                packet.packetType != (short)Enums.MPFlags.InitialSeedPacket ||
                packet.dataOffset != 0 || packet.dataLength != sizeof(int) || packet.data == null || packet.data.Length != sizeof(int))
                throw new InvalidOperationException("Unexpected or repeated target seed packet.");
            pending = new Platform_Multiplayer.MPData {
                packetType = packet.packetType, dataLength = packet.dataLength, data = (byte[])packet.data.Clone()
            };
            InstantMessage = instantMessage;
        }

        internal Platform_Multiplayer.MPData Take()
        {
            if (released || pending == null) throw new InvalidOperationException("Target seed is missing or already released.");
            var packet = pending; pending = null; released = true; return packet;
        }

        internal void Discard() { pending = null; released = true; }
    }
}

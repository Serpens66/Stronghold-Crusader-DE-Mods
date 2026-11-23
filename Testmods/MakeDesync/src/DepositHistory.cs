using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.Interop.Enums;
using Shared;

namespace MakeDesync
{
    // Private test-only binding. The standalone Deposit Restoration mod owns the live buffer.
    internal sealed class DepositHistory
    {
        private readonly FieldInfo runtime, state, session, multiplayer, agreed, repairing, tileManager;
        private readonly int capacity, historyOffset, storedMask;

        internal DepositHistory()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Plugin.DepositGuid, out var plugin) ||
                !Equals(plugin.Metadata.Version, new Version(0, 3, 1)) || ReferenceEquals(plugin.Instance, null))
                throw new InvalidOperationException("Deposit Restoration 0.3.1 is required for the private test binding.");

            var type = plugin.Instance.GetType();
            var owner = type.Assembly.GetType("DepositRestoration.DepositRestorationRuntime", true);
            var hooks = type.Assembly.GetType("DepositRestoration.DepositHooks", true);
            runtime = RequireField(type, "runtime", owner, BindingFlags.Static | BindingFlags.NonPublic);
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            state = RequireField(owner, "state", typeof(IntPtr), instance);
            session = RequireField(owner, "activeSession", typeof(long), instance);
            multiplayer = RequireField(owner, "multiplayerWorld", typeof(bool), instance);
            agreed = RequireField(owner, "agreed", typeof(bool), instance);
            repairing = RequireField(owner, "repairing", typeof(bool), instance);
            tileManager = RequireField(owner, "tileManager", typeof(ulong), instance);
            capacity = Constant(hooks, "Capacity");
            historyOffset = Constant(hooks, "HistoryOffset");
            storedMask = Constant(hooks, "StoredMask");
            if (capacity != 320800 || historyOffset != 16 || storedMask != 10)
                throw new InvalidOperationException("Unexpected wall deposit buffer layout.");
        }

        private static FieldInfo RequireField(Type owner, string name, Type type, BindingFlags flags)
        {
            var field = owner.GetField(name, flags | BindingFlags.DeclaredOnly);
            if (field == null || field.FieldType != type) throw new MissingFieldException(owner.FullName, name);
            return field;
        }

        private static int Constant(Type type, string name) =>
            (int)RequireField(type, name, typeof(int), BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();

        private IntPtr Resolve(long sessionId)
        {
            var owner = runtime.GetValue(null);
            if (owner == null || (long)session.GetValue(owner) != sessionId || !(bool)multiplayer.GetValue(owner) ||
                !(bool)agreed.GetValue(owner) || (bool)repairing.GetValue(owner))
                return IntPtr.Zero;
            var block = (IntPtr)state.GetValue(owner);
            var tiles = GameTileManagerAPI.Instance;
            if (block == IntPtr.Zero || (ulong)tileManager.GetValue(owner) != unchecked((ulong)tiles.GetTileManager().ToInt64()) ||
                tiles.GetLogicLayer().Length != capacity || Marshal.ReadByte(block) != 1) return IntPtr.Zero;
            return IntPtr.Add(block, historyOffset);
        }

        internal bool TryFind(long sessionId, out IntPtr address, out int tile, out byte value)
        {
            address = IntPtr.Zero;
            tile = -1;
            value = 0;
            var history = Resolve(sessionId);
            if (history == IntPtr.Zero) return false;
            byte[] snapshot = CommittedMemory.ReadUpTo(history, capacity);
            if (snapshot.Length != capacity) return false;
            var logic = GameTileManagerAPI.Instance.GetLogicLayer();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] == 0 || (snapshot[i] & ~storedMask) != 0 ||
                    (logic[i] & (int)TilePropertyFlag.IsWall) == 0) continue;
                address = IntPtr.Add(history, i);
                tile = i;
                value = snapshot[i];
                return true;
            }
            return false;
        }

        internal byte? Read(long sessionId, int tile)
        {
            var history = Resolve(sessionId);
            return history != IntPtr.Zero && tile >= 0 && tile < capacity ? Marshal.ReadByte(history, tile) : (byte?)null;
        }
    }
}

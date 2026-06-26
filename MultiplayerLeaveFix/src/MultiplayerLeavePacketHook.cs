using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;

namespace MultiplayerLeaveFix
{
    internal sealed class MultiplayerLeavePacketHook : IDisposable
    {
        private const short LeaveGamePacketType = 8;

        private delegate bool ProcessMessageDelegate(Platform_Multiplayer self, Platform_Multiplayer.MPData data, Platform_Multiplayer.MPGameMember fromMember, bool fromThread);

        private readonly ManualLogSource log;
        private readonly MultiplayerSystemMessageLimiterHook messageLimiterHook;
        private readonly Hook hook;
        private readonly ProcessMessageDelegate trampoline;
        private bool disposed;

        public MultiplayerLeavePacketHook(ManualLogSource log, MultiplayerSystemMessageLimiterHook messageLimiterHook)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.messageLimiterHook = messageLimiterHook ?? throw new ArgumentNullException(nameof(messageLimiterHook));

            hook = new Hook(FindProcessMessageMethod(), (ProcessMessageDelegate)ProcessMessageHook);
            trampoline = hook.GenerateTrampoline<ProcessMessageDelegate>();
            log.LogDebug("Multiplayer Leave Fix leave packet hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            log.LogDebug("Multiplayer Leave Fix leave packet hook disposed.");
        }

        private static MethodInfo FindProcessMessageMethod()
        {
            MethodInfo method = typeof(Platform_Multiplayer).GetMethod(
                "processMessage",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Platform_Multiplayer.MPData), typeof(Platform_Multiplayer.MPGameMember), typeof(bool) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(Platform_Multiplayer).FullName, "processMessage");

            return method;
        }

        private bool ProcessMessageHook(Platform_Multiplayer self, Platform_Multiplayer.MPData data, Platform_Multiplayer.MPGameMember fromMember, bool fromThread)
        {
            if (data != null && data.packetType == LeaveGamePacketType && fromMember != null)
            {
                messageLimiterHook.RecordIntentionalLeave(fromMember.playerID, fromMember.playerName);
                log.LogDebug($"Multiplayer Leave Fix detected intentional leave packet: playerId={fromMember.playerID}, playerName={fromMember.playerName}.");
            }

            return trampoline(self, data, fromMember, fromThread);
        }
    }
}

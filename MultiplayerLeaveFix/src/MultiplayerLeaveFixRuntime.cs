using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;

namespace MultiplayerLeaveFix
{
    internal sealed class MultiplayerLeaveFixRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private MultiplayerSystemMessageLimiterHook messageLimiterHook;
        private MultiplayerLeavePacketHook leavePacketHook;
        private bool applied;

        public MultiplayerLeaveFixRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply()
        {
            if (applied)
                return;

            try
            {
                messageLimiterHook = new MultiplayerSystemMessageLimiterHook(log);
                leavePacketHook = new MultiplayerLeavePacketHook(log, messageLimiterHook);
            }
            catch (Exception ex)
            {
                log.LogError($"Multiplayer Leave Fix hooks could not be installed: {ex}");
            }

            if (messageLimiterHook != null)
            {
                subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => messageLimiterHook.ClearSeenMessages()));
            }

            applied = true;
            log.LogDebug("Multiplayer Leave Fix hooks applied.");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            leavePacketHook?.Dispose();
            leavePacketHook = null;
            messageLimiterHook?.Dispose();
            messageLimiterHook = null;
            applied = false;
        }
    }
}

using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MultiplayerLeaveFix
{
    internal sealed class MultiplayerSystemMessageLimiterHook : IDisposable
    {
        private delegate void ReceiveIngameChatDelegate(HUD_MPChatMessages self, string fromName, int fromPlayerId, string message, int duration = 20);

        private static readonly string[] LimitedSystemMessagePrefixes =
        {
            "Removing Player :",
            "Player Connection Issue :"
        };

        private readonly ManualLogSource log;
        private readonly Hook hook;
        private readonly ReceiveIngameChatDelegate trampoline;
        private readonly HashSet<string> seenMessages = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, LeftPlayerInfo> intentionalLeavesByPlayerId = new Dictionary<int, LeftPlayerInfo>();
        private readonly Dictionary<string, LeftPlayerInfo> intentionalLeavesByPlayerName = new Dictionary<string, LeftPlayerInfo>(StringComparer.OrdinalIgnoreCase);
        private bool disposed;

        public MultiplayerSystemMessageLimiterHook(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));

            hook = new Hook(FindReceiveIngameChatMethod(), (ReceiveIngameChatDelegate)ReceiveIngameChatHook);
            trampoline = hook.GenerateTrampoline<ReceiveIngameChatDelegate>();
            log.LogDebug("Multiplayer Leave Fix message limiter hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            log.LogDebug("Multiplayer Leave Fix message limiter hook disposed.");
        }

        public void ClearSeenMessages()
        {
            seenMessages.Clear();
            intentionalLeavesByPlayerId.Clear();
            intentionalLeavesByPlayerName.Clear();
        }

        public void RecordIntentionalLeave(int playerId, string playerName)
        {
            LeftPlayerInfo info = new LeftPlayerInfo
            {
                PlayerId = playerId,
                PlayerName = NormalizeSystemMessage(playerName)
            };

            if (playerId > 0)
                intentionalLeavesByPlayerId[playerId] = info;

            if (!string.IsNullOrEmpty(info.PlayerName))
                intentionalLeavesByPlayerName[info.PlayerName] = info;
        }

        private static MethodInfo FindReceiveIngameChatMethod()
        {
            MethodInfo method = typeof(HUD_MPChatMessages).GetMethod(
                nameof(HUD_MPChatMessages.recieveIngameChat),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method == null)
                throw new MissingMethodException(typeof(HUD_MPChatMessages).FullName, nameof(HUD_MPChatMessages.recieveIngameChat));

            return method;
        }

        private void ReceiveIngameChatHook(HUD_MPChatMessages self, string fromName, int fromPlayerId, string message, int duration = 20)
        {
            try
            {
                if (ShouldSuppress(fromName, fromPlayerId, message))
                    return;
            }
            catch (Exception ex)
            {
                log.LogError($"Multiplayer Leave Fix message limiter failed: {ex}");
            }

            trampoline(self, fromName, fromPlayerId, message, duration);
        }

        private bool ShouldSuppress(string fromName, int fromPlayerId, string message)
        {
            if (!TryGetLimitedMessageKey(fromName, fromPlayerId, message, out string key))
                return false;

            if (seenMessages.Add(key))
            {
                log.LogDebug($"Multiplayer Leave Fix allowed first system message: fromName={fromName}, fromPlayerId={fromPlayerId}, message={message}.");
                return false;
            }

            log.LogDebug($"Multiplayer Leave Fix suppressed repeated system message: fromName={fromName}, fromPlayerId={fromPlayerId}, message={message}.");
            return true;
        }

        private bool TryGetLimitedMessageKey(string fromName, int fromPlayerId, string message, out string key)
        {
            key = null;
            string normalizedMessage = NormalizeSystemMessage(message);
            string normalizedFromName = NormalizeSystemMessage(fromName);
            bool fromSystem = string.Equals(fromName, "SYSTEM", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < LimitedSystemMessagePrefixes.Length; i++)
            {
                string prefix = LimitedSystemMessagePrefixes[i];
                if (fromSystem && normalizedMessage.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string playerName = NormalizeSystemMessage(normalizedMessage.Substring(prefix.Length));
                    if (WasIntentionalLeave(fromPlayerId, playerName))
                    {
                        key = BuildMessageKey(fromPlayerId, prefix, playerName);
                        return true;
                    }

                    return false;
                }

                string combinedPrefix = "SYSTEM " + prefix;
                if (normalizedMessage.StartsWith(combinedPrefix, StringComparison.Ordinal))
                {
                    string playerName = NormalizeSystemMessage(normalizedMessage.Substring(combinedPrefix.Length));
                    if (WasIntentionalLeave(fromPlayerId, playerName))
                    {
                        key = BuildMessageKey(fromPlayerId, prefix, playerName);
                        return true;
                    }

                    return false;
                }

                if (!fromSystem && normalizedMessage.StartsWith(prefix, StringComparison.Ordinal))
                {
                    if (WasIntentionalLeave(fromPlayerId, normalizedFromName))
                    {
                        key = BuildMessageKey(fromPlayerId, prefix, normalizedFromName);
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        private bool WasIntentionalLeave(int fromPlayerId, string playerName)
        {
            if (fromPlayerId > 0 && intentionalLeavesByPlayerId.ContainsKey(fromPlayerId))
                return true;

            return !string.IsNullOrEmpty(playerName) && intentionalLeavesByPlayerName.ContainsKey(playerName);
        }

        private static string BuildMessageKey(int fromPlayerId, string prefix, string remainder)
        {
            return fromPlayerId + ":" + prefix + ":" + NormalizeSystemMessage(remainder);
        }

        private static string NormalizeSystemMessage(string message)
        {
            return (message ?? string.Empty).Trim();
        }

        private sealed class LeftPlayerInfo
        {
            public int PlayerId;
            public string PlayerName;
        }
    }
}

using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
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
        private readonly MultiplayerLeaveMessagePolicy policy = new MultiplayerLeaveMessagePolicy();
        private bool disposed;

        public MultiplayerSystemMessageLimiterHook(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));

            hook = new Hook(FindReceiveIngameChatMethod(), (ReceiveIngameChatDelegate)ReceiveIngameChatHook);
            trampoline = hook.GenerateTrampoline<ReceiveIngameChatDelegate>();
            Shared.DebugLogHelper.LogDebug(log, "Multiplayer Leave Fix message limiter hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "Multiplayer Leave Fix message limiter hook disposed.");
        }

        public void ClearSeenMessages()
        {
            policy.Clear();
        }

        public bool RecordIntentionalLeave(int playerId, string playerName, ulong steamId)
        {
            return policy.RecordProcessedLeave(playerId, playerName, steamId);
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
                Shared.DebugLogHelper.LogError(log, $"Multiplayer Leave Fix message limiter failed: {ex}");
            }

            trampoline(self, fromName, fromPlayerId, message, duration);
        }

        private bool ShouldSuppress(string fromName, int fromPlayerId, string message)
        {
            DiscardIntentionsForActiveRoster();
            if (!TryClassifyLimitedMessage(fromName, fromPlayerId, message, out LeaveMessageDisposition disposition))
                return false;

            if (disposition == LeaveMessageDisposition.AllowFirst)
            {
                Shared.DebugLogHelper.LogDebug(log, $"Multiplayer Leave Fix allowed first system message in duplicate window: fromName={fromName}, fromPlayerId={fromPlayerId}, message={message}.");
                return false;
            }

            if (disposition == LeaveMessageDisposition.SuppressDuplicate)
            {
                Shared.DebugLogHelper.LogDebug(log, $"Multiplayer Leave Fix suppressed repeated system message in duplicate window: fromName={fromName}, fromPlayerId={fromPlayerId}, message={message}.");
                return true;
            }

            return false;
        }

        private bool TryClassifyLimitedMessage(string fromName, int fromPlayerId, string message, out LeaveMessageDisposition disposition)
        {
            disposition = LeaveMessageDisposition.NotLimited;
            string normalizedMessage = NormalizeSystemMessage(message);
            string normalizedFromName = NormalizeSystemMessage(fromName);
            bool fromSystem = string.Equals(fromName, "SYSTEM", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < LimitedSystemMessagePrefixes.Length; i++)
            {
                string prefix = LimitedSystemMessagePrefixes[i];
                if (fromSystem && normalizedMessage.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string playerName = NormalizeSystemMessage(normalizedMessage.Substring(prefix.Length));
                    disposition = policy.Classify(fromPlayerId, playerName, prefix);
                    return disposition != LeaveMessageDisposition.NotLimited;
                }

                string combinedPrefix = "SYSTEM " + prefix;
                if (normalizedMessage.StartsWith(combinedPrefix, StringComparison.Ordinal))
                {
                    string playerName = NormalizeSystemMessage(normalizedMessage.Substring(combinedPrefix.Length));
                    disposition = policy.Classify(fromPlayerId, playerName, prefix);
                    return disposition != LeaveMessageDisposition.NotLimited;
                }

                if (!fromSystem && normalizedMessage.StartsWith(prefix, StringComparison.Ordinal))
                {
                    disposition = policy.Classify(fromPlayerId, normalizedFromName, prefix);
                    return disposition != LeaveMessageDisposition.NotLimited;
                }
            }

            return false;
        }

        private void DiscardIntentionsForActiveRoster()
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            if (platform?.gameMembers == null)
                return;

            foreach (Platform_Multiplayer.MPGameMember member in platform.gameMembers)
            {
                if (member != null && !member.kicked && !member.skirmishAI)
                    policy.DiscardForActiveMember(member.playerID, member.playerName, member.steamID);
            }
        }

        private static string NormalizeSystemMessage(string message)
        {
            return (message ?? string.Empty).Trim();
        }
    }
}

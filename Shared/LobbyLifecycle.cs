using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Shared
{
    /// <summary>
    /// Exposes successful multiplayer lobby joins without requiring a permanent frame callback.
    /// Shared is source-linked into multiple mods, so process-wide state is stored in AppDomain data.
    /// </summary>
    public static class LobbyLifecycle
    {
        private const string AnchorKey = "SerpsMods.Shared.LobbyLifecycle.v1.Anchor";
        private const string SubscribersKey = "SerpsMods.Shared.LobbyLifecycle.v1.Subscribers";
        private const string GateKey = "SerpsMods.Shared.LobbyLifecycle.v1.Gate";

#if SHARED_PRESET_TESTS
        private const string TestInstallCountKey = "SerpsMods.Shared.LobbyLifecycle.v1.TestInstallCount";
#endif

        public static IDisposable SubscribeJoined(
            ManualLogSource log,
            Action<Platform_Multiplayer.MPLobby> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            object gate = string.Intern(GateKey);
            lock (gate)
            {
                List<KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>> subscribers =
                    GetOrCreateSubscribers();
                EnsureInstalled(log);
                subscribers.Add(new KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>(
                    callback,
                    log));
            }

            return new Subscription(callback);
        }

        private static List<KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>>
            GetOrCreateSubscribers()
        {
            object existing = AppDomain.CurrentDomain.GetData(SubscribersKey);
            if (existing is List<KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>> subscribers)
                return subscribers;

            var created = new List<KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>>();
            AppDomain.CurrentDomain.SetData(SubscribersKey, created);
            return created;
        }

        private static void EnsureInstalled(ManualLogSource log)
        {
            if (AppDomain.CurrentDomain.GetData(AnchorKey) != null)
                return;

#if SHARED_PRESET_TESTS
            int count = (AppDomain.CurrentDomain.GetData(TestInstallCountKey) as int?) ?? 0;
            AppDomain.CurrentDomain.SetData(TestInstallCountKey, count + 1);
            AppDomain.CurrentDomain.SetData(AnchorKey, new object());
#else
            HookAnchor anchor = new HookAnchor(log);
            try
            {
                anchor.Install();
                // The AppDomain reference keeps the detour alive and prevents another
                // source-linked Shared copy from installing the same process-wide hook.
                AppDomain.CurrentDomain.SetData(AnchorKey, anchor);
                DebugLogHelper.LogInfo(log, "Shared lobby-join lifecycle hook installed.");
            }
            catch (Exception ex)
            {
                anchor.RollBack();
                throw new InvalidOperationException(
                    "The shared lobby-join lifecycle hook could not be installed.",
                    Unwrap(ex));
            }
#endif
        }

        private static void NotifyJoined(Platform_Multiplayer.MPLobby requestedLobby)
        {
            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby activeLobby = multiplayer?.activeLobby;
            if (!IsRequestedLobbyActive(requestedLobby, activeLobby))
                return;

            KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>[] snapshot;
            object gate = string.Intern(GateKey);
            lock (gate)
                snapshot = GetOrCreateSubscribers().ToArray();

            foreach (KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource> subscriber in snapshot)
            {
                try
                {
                    subscriber.Key(activeLobby);
                }
                catch (Exception ex)
                {
                    DebugLogHelper.LogError(
                        subscriber.Value,
                        $"Shared lobby-join subscriber failed: {ex}");
                }
            }
        }

        private static bool IsRequestedLobbyActive(
            Platform_Multiplayer.MPLobby requestedLobby,
            Platform_Multiplayer.MPLobby activeLobby)
        {
            if (requestedLobby == null || activeLobby == null)
                return false;
            if (ReferenceEquals(requestedLobby, activeLobby))
                return true;

            return TryReadLobbyId(requestedLobby, out ulong requestedId) &&
                TryReadLobbyId(activeLobby, out ulong activeId) &&
                requestedId != 0 && requestedId == activeId;
        }

        private static bool TryReadLobbyId(Platform_Multiplayer.MPLobby lobby, out ulong id)
        {
            id = 0;
            try
            {
                FieldInfo lobbyIdField = typeof(Platform_Multiplayer.MPLobby).GetField(
                    "id",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object steamId = lobbyIdField?.GetValue(lobby);
                FieldInfo valueField = steamId?.GetType().GetField(
                    "m_SteamID",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object value = valueField?.GetValue(steamId);
                if (value is ulong parsed)
                {
                    id = parsed;
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : ex;

        private sealed class Subscription : IDisposable
        {
            private Action<Platform_Multiplayer.MPLobby> callback;

            internal Subscription(Action<Platform_Multiplayer.MPLobby> callback)
            {
                this.callback = callback;
            }

            public void Dispose()
            {
                Action<Platform_Multiplayer.MPLobby> removed =
                    Interlocked.Exchange(ref callback, null);
                if (removed == null)
                    return;

                object gate = string.Intern(GateKey);
                lock (gate)
                {
                    List<KeyValuePair<Action<Platform_Multiplayer.MPLobby>, ManualLogSource>> subscribers =
                        GetOrCreateSubscribers();
                    int index = subscribers.FindIndex(item => ReferenceEquals(item.Key, removed));
                    if (index >= 0)
                        subscribers.RemoveAt(index);
                }
            }
        }

#if SHARED_PRESET_TESTS
        internal static int System_TestInstallCount =>
            (AppDomain.CurrentDomain.GetData(TestInstallCountKey) as int?) ?? 0;

        internal static void System_TestCompleteJoin(
            Platform_Multiplayer.MPLobby requestedLobby,
            Action vanillaCallback)
        {
            vanillaCallback?.Invoke();
            NotifyJoined(requestedLobby);
        }

        internal static void System_TestReset()
        {
            object gate = string.Intern(GateKey);
            lock (gate)
            {
                AppDomain.CurrentDomain.SetData(AnchorKey, null);
                AppDomain.CurrentDomain.SetData(SubscribersKey, null);
                AppDomain.CurrentDomain.SetData(TestInstallCountKey, 0);
            }
        }
#else
        private sealed class HookAnchor
        {
            private delegate void JoinLobbyDelegate(
                Platform_Multiplayer instance,
                Platform_Multiplayer.MPLobby lobbyToJoin,
                Action lobbyJoinedDelegate,
                Action<string, string, int> lobbyChatDelegate,
                bool keepAutoJoinLobby);

            private readonly ManualLogSource log;
            private object detour;
            private JoinLobbyDelegate trampoline;

            internal HookAnchor(ManualLogSource log)
            {
                this.log = log;
            }

            internal void Install()
            {
                MethodInfo joinLobbyMethod = typeof(Platform_Multiplayer).GetMethod(
                    nameof(Platform_Multiplayer.JoinLobby),
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(Platform_Multiplayer.MPLobby),
                        typeof(Action),
                        typeof(Action<string, string, int>),
                        typeof(bool)
                    },
                    null) ?? throw new MissingMethodException(
                        typeof(Platform_Multiplayer).FullName,
                        nameof(Platform_Multiplayer.JoinLobby));

                Type openType = typeof(GameNetworkAPI).Assembly.GetType(
                    "SHCDESE.ManagedHooks.ManagedDetour`1",
                    true);
                Type closedType = openType.MakeGenericType(typeof(JoinLobbyDelegate));
                detour = Activator.CreateInstance(
                    closedType,
                    joinLobbyMethod,
                    new JoinLobbyDelegate(JoinLobbyHook));
                trampoline = detour.GetType()
                    .GetProperty("Trampoline", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(detour) as JoinLobbyDelegate
                    ?? throw new InvalidOperationException("JoinLobby detour trampoline is unavailable.");
            }

            internal void RollBack()
            {
                if (detour != null)
                {
                    object hook = detour.GetType()
                        .GetProperty("Hook", BindingFlags.Instance | BindingFlags.Public)
                        ?.GetValue(detour);
                    (hook as IDisposable)?.Dispose();
                }
                detour = null;
                trampoline = null;
            }

            private void JoinLobbyHook(
                Platform_Multiplayer instance,
                Platform_Multiplayer.MPLobby lobbyToJoin,
                Action lobbyJoinedDelegate,
                Action<string, string, int> lobbyChatDelegate,
                bool keepAutoJoinLobby)
            {
                Action wrappedCallback = () =>
                {
                    lobbyJoinedDelegate?.Invoke();
                    NotifyJoined(lobbyToJoin);
                };

                try
                {
                    trampoline(
                        instance,
                        lobbyToJoin,
                        wrappedCallback,
                        lobbyChatDelegate,
                        keepAutoJoinLobby);
                }
                catch (Exception ex)
                {
                    DebugLogHelper.LogError(log, $"Shared JoinLobby detour failed: {ex}");
                    throw;
                }
            }
        }
#endif
    }
}

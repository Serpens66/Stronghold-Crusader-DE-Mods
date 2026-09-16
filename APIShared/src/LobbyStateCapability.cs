using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace APIShared
{
    internal sealed class LobbyStateService
    {
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private const int FallbackFrames = 15;

        private delegate bool GetActiveLobbyMembersDelegate(
            Platform_Multiplayer self,
            bool coopGame);
        private delegate void LeaveLobbyDelegate(
            Platform_Multiplayer self,
            bool startGame);

        private readonly object sync = new object();
        private readonly List<Registration> registrations = new List<Registration>();
        private readonly ManualLogSource log;
        private Hook getActiveLobbyMembersHook;
        private Hook leaveLobbyHook;
        private GetActiveLobbyMembersDelegate getActiveLobbyMembersOriginal;
        private LeaveLobbyDelegate leaveLobbyOriginal;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private LobbyStateSnapshot current;
        private readonly Queue<LobbyStateSnapshot> pendingPublications =
            new Queue<LobbyStateSnapshot>();
        private bool notificationActive;
        private bool observationActive;
        private bool dirty = true;
        private bool mapStarted;
        private int lastObservedFrame = -1;

        internal LobbyStateService(ManualLogSource logger)
        {
            log = logger;
        }

        internal static bool TryCreate(
            ManualLogSource log,
            out LobbyStateService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            var installed = new List<Hook>();
            LobbyStateService candidate = null;
            try
            {
                candidate = new LobbyStateService(log);
                candidate.Install(installed);
                service = candidate;
                diagnostic = candidate.Available("Process-wide managed lobby observer is active.");
                NativeApiLog.Info(log, "Process-wide lobby-state observer installed.");
                return true;
            }
            catch (Exception ex)
            {
                if (candidate != null)
                {
                    Application.onBeforeRender -= candidate.OnBeforeRender;
                    candidate.mapStartSubscription?.Dispose();
                    candidate.mapUnloadSubscription?.Dispose();
                }
                for (int index = installed.Count - 1; index >= 0; index--)
                {
                    try { installed[index].Undo(); } catch { }
                    try { installed[index].Dispose(); } catch { }
                }
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.LobbyState,
                    NativeCapabilityState.Faulted,
                    string.Empty,
                    ex.Message);
                NativeApiLog.Error(log, $"Lobby-state observer failed before publication: {ex}");
                return false;
            }
        }

        internal ILobbyStateCapability Bind(string ownerGuid) =>
            new Binding(this, ownerGuid);

        private void Install(List<Hook> installed)
        {
            getActiveLobbyMembersHook = PrepareHook(
                RequireMethod(
                    typeof(Platform_Multiplayer),
                    nameof(Platform_Multiplayer.GetActiveLobbyMembers),
                    new[] { typeof(bool) }),
                (GetActiveLobbyMembersDelegate)GetActiveLobbyMembersHook,
                "APIShared.LobbyState.GetActiveLobbyMembers",
                installed);
            getActiveLobbyMembersOriginal =
                getActiveLobbyMembersHook.GenerateTrampoline<GetActiveLobbyMembersDelegate>();
            getActiveLobbyMembersHook.Apply();

            leaveLobbyHook = PrepareHook(
                RequireMethod(
                    typeof(Platform_Multiplayer),
                    nameof(Platform_Multiplayer.LeaveLobby),
                    new[] { typeof(bool) }),
                (LeaveLobbyDelegate)LeaveLobbyHook,
                "APIShared.LobbyState.LeaveLobby",
                installed);
            leaveLobbyOriginal = leaveLobbyHook.GenerateTrampoline<LeaveLobbyDelegate>();
            leaveLobbyHook.Apply();

            // SaveLifecycle: this is lobby-state invalidation, not gameplay initialization;
            // saved-game loads have no active lobby state to preserve or initialize here.
            mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => OnMapStarting());
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => OnMapUnloaded());
            if (mapStartSubscription == null || mapUnloadSubscription == null)
                throw new InvalidOperationException("Lobby-state map subscriptions could not be created.");

            Application.onBeforeRender += OnBeforeRender;
            ObserveDirtyNow();
        }

        private static Hook PrepareHook(
            MethodInfo method,
            Delegate callback,
            string id,
            ICollection<Hook> installed)
        {
            var hook = new Hook(method, callback, new HookConfig
            {
                ManualApply = true,
                ID = id
            });
            installed.Add(hook);
            return hook;
        }

        private static MethodInfo RequireMethod(Type type, string name, Type[] arguments) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                arguments,
                null) ?? throw new MissingMethodException(type.FullName, name);

        private bool GetActiveLobbyMembersHook(
            Platform_Multiplayer self,
            bool coopGame)
        {
            try
            {
                return getActiveLobbyMembersOriginal(self, coopGame);
            }
            finally
            {
                MarkDirty();
            }
        }

        private void LeaveLobbyHook(
            Platform_Multiplayer self,
            bool startGame)
        {
            try
            {
                leaveLobbyOriginal(self, startGame);
            }
            finally
            {
                MarkDirty();
            }
        }

        private void MarkDirty()
        {
            lock (sync)
                dirty = true;
            ObserveDirtyNow();
        }

        private void ObserveDirtyNow()
        {
            lock (sync)
            {
                if (mapStarted || !dirty || observationActive)
                    return;
                dirty = false;
                observationActive = true;
                lastObservedFrame = Time.frameCount;
            }
            while (true)
            {
                ObserveAndPublish();
                lock (sync)
                {
                    if (mapStarted || !dirty)
                    {
                        observationActive = false;
                        return;
                    }
                    dirty = false;
                    lastObservedFrame = Time.frameCount;
                }
            }
        }

        private void OnMapStarting()
        {
            lock (sync)
                dirty = true;
            ObserveDirtyNow();
            lock (sync)
            {
                mapStarted = true;
                dirty = false;
            }
        }

        private void OnMapUnloaded()
        {
            lock (sync)
            {
                mapStarted = false;
                dirty = true;
            }
            ObserveDirtyNow();
        }

        private void OnBeforeRender()
        {
            int frame = Time.frameCount;
            lock (sync)
            {
                if (!ShouldObserve(mapStarted, dirty, lastObservedFrame, frame))
                    return;
                dirty = true;
            }
            ObserveDirtyNow();
        }

        private void ObserveAndPublish()
        {
            LobbyStateSnapshot snapshot;
            try
            {
                snapshot = Capture();
            }
            catch (Exception ex)
            {
                snapshot = new LobbyStateSnapshot(
                    null,
                    null,
                    true,
                    0,
                    false,
                    "The lobby roster could not be observed; waiting for a successful retry.",
                    ex.ToString());
            }
            PublishIfChanged(snapshot);
        }

        private static LobbyStateSnapshot Capture()
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (lobby == null)
            {
                bool preserve = platform?.gameMembers != null &&
                    platform.gameMembers.Any(member =>
                        member != null && !member.skirmishAI && !member.kicked);
                return new LobbyStateSnapshot(
                    null,
                    null,
                    false,
                    0,
                    preserve,
                    string.Empty,
                    string.Empty);
            }

            var players = new Dictionary<int, ulong>();
            var diagnostics = new List<string>();
            bool unresolved = false;
            IEnumerable<Platform_Multiplayer.MPLobbyMember> lobbyMembers =
                lobby.members ?? Enumerable.Empty<Platform_Multiplayer.MPLobbyMember>();
            foreach (Platform_Multiplayer.MPLobbyMember member in lobbyMembers)
            {
                if (member == null || member.dummyToBeKicked ||
                    (member.SkirmishMember && !member.SkirmishHumanMember))
                {
                    continue;
                }

                ulong steamId = member.id.m_SteamID;
                int vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
                int networkPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
                if (!IsValidPlayerId(vanillaPlayerId))
                {
                    unresolved = true;
                    continue;
                }
                if (IsValidPlayerId(networkPlayerId) && networkPlayerId != vanillaPlayerId)
                {
                    diagnostics.Add(
                        $"steamId={steamId}: networkLobby={networkPlayerId}, finalLobby={vanillaPlayerId}");
                }
                if (!TryAddPlayer(players, vanillaPlayerId, steamId, out string playerError))
                {
                    unresolved = true;
                    diagnostics.Add(playerError);
                }
            }
            if (players.Count == 0)
                unresolved = true;

            ulong localSteamId = 0;
            try { localSteamId = SteamUser.GetSteamID().m_SteamID; }
            catch { }
            int localPlayerId = localSteamId == 0
                ? 0
                : lobby.getThisPlayerFromSteamID(localSteamId);
            if (!IsValidPlayerId(localPlayerId) || !players.ContainsKey(localPlayerId))
            {
                unresolved = true;
                localPlayerId = 0;
            }

            ulong lobbyId = lobby.id.m_SteamID;
            if (lobbyId == 0)
                unresolved = true;
            string diagnostic = diagnostics.Count == 0
                ? string.Empty
                : "Lobby player-ID source differences: " +
                  string.Join("; ", diagnostics) + ".";
            return new LobbyStateSnapshot(
                lobbyId == 0 ? (ulong?)null : lobbyId,
                players,
                unresolved,
                localPlayerId,
                false,
                string.Empty,
                diagnostic);
        }

        private static bool TryAddPlayer(
            IDictionary<int, ulong> players,
            int playerId,
            ulong steamId,
            out string error)
        {
            if (!IsValidPlayerId(playerId) || steamId == 0)
            {
                error = $"A human player has an invalid final identity: playerId={playerId}, steamId={steamId}.";
                return false;
            }
            if (players.TryGetValue(playerId, out ulong existingSteamId) &&
                existingSteamId != steamId)
            {
                error = $"Final player slot {playerId} is assigned to multiple Steam identities.";
                return false;
            }
            if (players.Any(player =>
                player.Key != playerId && player.Value == steamId))
            {
                error = $"Steam identity {steamId} is assigned to multiple final player slots.";
                return false;
            }
            players[playerId] = steamId;
            error = string.Empty;
            return true;
        }

        private static bool IsValidPlayerId(int playerId) =>
            playerId >= FirstPlayerId && playerId <= LastPlayerId;

        private void PublishIfChanged(LobbyStateSnapshot snapshot)
        {
            Registration[] observers;
            lock (sync)
            {
                if (ValueEquals(current, snapshot))
                    return;
                current = snapshot;
                if (notificationActive)
                {
                    pendingPublications.Enqueue(snapshot);
                    return;
                }
                notificationActive = true;
                observers = registrations.ToArray();
            }
            while (true)
            {
                Notify(observers, snapshot);
                lock (sync)
                {
                    if (pendingPublications.Count == 0)
                    {
                        notificationActive = false;
                        return;
                    }
                    snapshot = pendingPublications.Dequeue();
                    observers = registrations.ToArray();
                }
            }
        }

        private bool Register(
            string owner,
            string id,
            Action<LobbyStateSnapshot> observer,
            out NativeCapabilityDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(id) || observer == null)
            {
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.LobbyState,
                    NativeCapabilityState.ValidationFailed,
                    string.Empty,
                    "Registration ID and observer are required.");
                return false;
            }

            LobbyStateSnapshot replay;
            lock (sync)
            {
                if (registrations.Any(item =>
                    string.Equals(item.Owner, owner, StringComparison.Ordinal) &&
                    string.Equals(item.Id, id, StringComparison.Ordinal)))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.LobbyState,
                        NativeCapabilityState.ValidationFailed,
                        string.Empty,
                        "The owner already registered this lobby observer ID.");
                    return false;
                }
                registrations.Add(new Registration(owner, id, observer));
                registrations.Sort((left, right) =>
                {
                    int result = string.CompareOrdinal(left.Owner, right.Owner);
                    return result != 0
                        ? result
                        : string.CompareOrdinal(left.Id, right.Id);
                });
                replay = current;
            }

            if (replay != null)
                Notify(new[] { new Registration(owner, id, observer) }, replay);
            diagnostic = Available("Lobby observer registered for the process lifetime.");
            return true;
        }

        private void Notify(
            IEnumerable<Registration> observers,
            LobbyStateSnapshot snapshot)
        {
            foreach (Registration observer in observers)
            {
                try { observer.Observer(snapshot); }
                catch (Exception ex)
                {
                    NativeApiLog.Error(
                        log,
                        $"Lobby-state observer failed: owner={observer.Owner}, " +
                        $"registration={observer.Id}, error={ex}");
                }
            }
        }

        internal static bool ValueEquals(
            LobbyStateSnapshot left,
            LobbyStateSnapshot right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null ||
                left.LobbyId != right.LobbyId ||
                left.HasUnresolvedPlayers != right.HasUnresolvedPlayers ||
                left.LocalPlayerId != right.LocalPlayerId ||
                left.PreserveForMapTransition != right.PreserveForMapTransition ||
                !string.Equals(left.Error, right.Error, StringComparison.Ordinal) ||
                !string.Equals(left.Diagnostic, right.Diagnostic, StringComparison.Ordinal) ||
                left.Players.Count != right.Players.Count)
            {
                return false;
            }
            foreach (KeyValuePair<int, ulong> player in left.Players)
            {
                if (!right.Players.TryGetValue(player.Key, out ulong steamId) ||
                    steamId != player.Value)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool ShouldObserve(
            bool isMapStarted,
            bool isDirty,
            int lastFrame,
            int currentFrame) =>
            !isMapStarted &&
            (isDirty || lastFrame < 0 || currentFrame - lastFrame >= FallbackFrames);

        internal void System_TestPublish(LobbyStateSnapshot snapshot) =>
            PublishIfChanged(snapshot);

        private NativeCapabilityDiagnostic Available(string reason) =>
            new NativeCapabilityDiagnostic(
                NativeCapabilityIds.LobbyState,
                NativeCapabilityState.Available,
                string.Empty,
                reason);

        private sealed class Registration
        {
            internal Registration(
                string owner,
                string id,
                Action<LobbyStateSnapshot> observer)
            {
                Owner = owner;
                Id = id;
                Observer = observer;
            }

            internal string Owner { get; }
            internal string Id { get; }
            internal Action<LobbyStateSnapshot> Observer { get; }
        }

        private sealed class Binding : ILobbyStateCapability
        {
            private readonly LobbyStateService owner;
            private readonly string ownerGuid;

            internal Binding(LobbyStateService owner, string ownerGuid)
            {
                this.owner = owner;
                this.ownerGuid = ownerGuid;
            }

            public bool TryRegisterObserver(
                string registrationId,
                Action<LobbyStateSnapshot> observer,
                out NativeCapabilityDiagnostic diagnostic) =>
                owner.Register(ownerGuid, registrationId, observer, out diagnostic);
        }
    }
}

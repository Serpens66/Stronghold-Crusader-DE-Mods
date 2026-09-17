using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace APIShared
{
    internal sealed unsafe class PlayerDefeatService
    {
        private const int LastPlayerId = 8;
        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly PlayerDefeatState state = new PlayerDefeatState();
        private readonly List<Registration> registrations = new List<Registration>();
        private readonly Queue<Publication> pendingPublications = new Queue<Publication>();
        private bool notificationActive;

        private PlayerDefeatService(ManualLogSource logger)
        {
            log = logger;
        }

        internal static bool TryCreate(
            ManualLogSource log,
            MissionLifecycleService missionLifecycle,
            out PlayerDefeatService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            PlayerDefeatService candidate = null;
            bool subscribed = false;
            try
            {
                if (missionLifecycle == null)
                    throw new InvalidOperationException("The managed mission lifecycle is unavailable.");
                candidate = new PlayerDefeatService(log);
                GameTimeManagerAPI.Instance.OnTick += candidate.OnTick;
                subscribed = true;
                if (!missionLifecycle.Bind("APIShared.player-defeat").TryRegisterObserver(
                        "session-reset",
                        candidate.OnMissionStarted,
                        candidate.OnMissionEnded,
                        null,
                        out NativeCapabilityDiagnostic lifecycleDiagnostic))
                {
                    throw new InvalidOperationException(
                        "Mission-lifecycle registration failed: " + lifecycleDiagnostic.Reason);
                }
                service = candidate;
                diagnostic = candidate.Available("Process-wide managed player-defeat observer is active.");
                return true;
            }
            catch (Exception ex)
            {
                if (subscribed && candidate != null && GameTimeManagerAPI.Instance != null)
                    GameTimeManagerAPI.Instance.OnTick -= candidate.OnTick;
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.PlayerDefeat,
                    NativeCapabilityState.Faulted,
                    string.Empty,
                    "Managed player-defeat initialization failed: " + ex.Message);
                return false;
            }
        }

        internal IPlayerDefeatCapability Bind(string owner) => new Binding(this, owner);

        private void OnMissionStarted(MissionLifecycleNotification notification) =>
            state.SynchronizeSession(notification?.Context?.SessionId);

        private void OnMissionEnded(MissionLifecycleNotification notification) =>
            state.SynchronizeSession(null);

        private void OnTick(int simulationTick)
        {
            try
            {
                MissionContext context = MissionLifecycleService.ActiveContext;
                state.SynchronizeSession(context?.SessionId);
                if (context == null)
                    return;

                var observations = new PlayerDefeatObservation[LastPlayerId];
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
                if (players == null || units == null)
                    return;

                for (int playerId = 1; playerId <= LastPlayerId; playerId++)
                    observations[playerId - 1] = Capture(players, units, playerId);

                state.Observe(simulationTick, observations, PublishLordDied, PublishDefeated);
            }
            catch (Exception ex)
            {
                try { NativeApiLog.Error(log, "Player-defeat observation failed closed: " + ex); }
                catch { }
            }
        }

        private static PlayerDefeatObservation Capture(
            GamePlayerManagerAPI players,
            GameUnitManagerAPI units,
            int playerId)
        {
            if (!players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                resources == null)
            {
                return default;
            }

            int lordUnitId = checked((int)resources->r_LordUnitId);
            GameUnit* unit = null;
            bool livingLord = lordUnitId > 0 &&
                units.TryGetUnitById(lordUnitId, out unit) &&
                unit != null &&
                unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD &&
                unit->r_CurrentHealth > 0 &&
                unit->r_ControllableForPlayerId == playerId &&
                unit->r_GlobalId != 0;
            int lordGlobalId = livingLord ? checked((int)unit->r_GlobalId) : 0;
            return new PlayerDefeatObservation(
                true,
                resources->r_WinLossState,
                livingLord,
                livingLord ? lordUnitId : 0,
                lordGlobalId);
        }

        private bool Register(
            string owner,
            string id,
            Action<PlayerLordDeathNotification> onLordDied,
            Action<PlayerDefeatNotification> onDefeated,
            out NativeCapabilityDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(id) || (onLordDied == null && onDefeated == null))
            {
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.PlayerDefeat,
                    NativeCapabilityState.ValidationFailed,
                    string.Empty,
                    "A non-empty registration ID and at least one callback are required.");
                return false;
            }

            lock (sync)
            {
                if (registrations.Exists(registration =>
                    string.Equals(registration.Owner, owner, StringComparison.Ordinal) &&
                    string.Equals(registration.Id, id, StringComparison.Ordinal)))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.PlayerDefeat,
                        NativeCapabilityState.Conflict,
                        string.Empty,
                        "The owner already registered this player-defeat observer ID.",
                        owner);
                    return false;
                }

                registrations.Add(new Registration(owner, id, onLordDied, onDefeated));
                registrations.Sort(Registration.Compare);
            }
            diagnostic = Available("The process-lifetime player-defeat observer was registered.");
            return true;
        }

        private void PublishLordDied(PlayerLordDeathNotification notification) =>
            Publish(new Publication(notification));

        private void PublishDefeated(PlayerDefeatNotification notification) =>
            Publish(new Publication(notification));

        private void Publish(Publication publication)
        {
            lock (sync)
            {
                pendingPublications.Enqueue(publication);
                if (notificationActive)
                    return;
                notificationActive = true;
            }

            while (true)
            {
                Publication current;
                Registration[] observers;
                lock (sync)
                {
                    if (pendingPublications.Count == 0)
                    {
                        notificationActive = false;
                        return;
                    }
                    current = pendingPublications.Dequeue();
                    observers = registrations.ToArray();
                }

                foreach (Registration observer in observers)
                {
                    try { current.Invoke(observer); }
                    catch (Exception ex)
                    {
                        try
                        {
                            NativeApiLog.Error(
                                log,
                                $"Player-defeat observer failed: owner={observer.Owner}, registration={observer.Id}, error={ex}");
                        }
                        catch { }
                    }
                }
            }
        }

        private NativeCapabilityDiagnostic Available(string reason) =>
            new NativeCapabilityDiagnostic(
                NativeCapabilityIds.PlayerDefeat,
                NativeCapabilityState.Available,
                string.Empty,
                reason);

        internal void TestPublish(PlayerLordDeathNotification notification) => PublishLordDied(notification);
        internal void TestPublish(PlayerDefeatNotification notification) => PublishDefeated(notification);
        internal static PlayerDefeatService TestCreate() => new PlayerDefeatService(null);

        private sealed class Binding : IPlayerDefeatCapability
        {
            private readonly PlayerDefeatService service;
            private readonly string owner;

            internal Binding(PlayerDefeatService service, string owner)
            {
                this.service = service;
                this.owner = owner;
            }

            public bool TryRegisterObserver(
                string registrationId,
                Action<PlayerLordDeathNotification> onLordDied,
                Action<PlayerDefeatNotification> onDefeated,
                out NativeCapabilityDiagnostic diagnostic) =>
                service.Register(owner, registrationId, onLordDied, onDefeated, out diagnostic);
        }

        private sealed class Registration
        {
            internal Registration(
                string owner,
                string id,
                Action<PlayerLordDeathNotification> onLordDied,
                Action<PlayerDefeatNotification> onDefeated)
            {
                Owner = owner;
                Id = id;
                OnLordDied = onLordDied;
                OnDefeated = onDefeated;
            }

            internal string Owner { get; }
            internal string Id { get; }
            internal Action<PlayerLordDeathNotification> OnLordDied { get; }
            internal Action<PlayerDefeatNotification> OnDefeated { get; }

            internal static int Compare(Registration left, Registration right)
            {
                int owner = string.CompareOrdinal(left.Owner, right.Owner);
                return owner != 0 ? owner : string.CompareOrdinal(left.Id, right.Id);
            }
        }

        private readonly struct Publication
        {
            private readonly PlayerLordDeathNotification lordDied;
            private readonly PlayerDefeatNotification defeated;

            internal Publication(PlayerLordDeathNotification notification)
            {
                lordDied = notification;
                defeated = null;
            }

            internal Publication(PlayerDefeatNotification notification)
            {
                lordDied = null;
                defeated = notification;
            }

            internal void Invoke(Registration observer)
            {
                if (lordDied != null)
                    observer.OnLordDied?.Invoke(lordDied);
                else
                    observer.OnDefeated?.Invoke(defeated);
            }
        }
    }
}

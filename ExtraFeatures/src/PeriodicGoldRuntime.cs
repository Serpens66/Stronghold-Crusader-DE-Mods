using APIShared;
using BepInEx.Logging;
using R3;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    // The engine boundary is also used by the offline lifecycle/accounting fixture.
    internal interface IPeriodicGoldWorld
    {
        bool TryGetCurrentTick(out int tick);
        bool TryRegisterTick(Action<int> callback, out string reason);
        void VisitAlivePlayers(Action<int, bool, int, int> visitor);
        void AddIncomingGold(int playerId, int amount);
        void SetGold(int playerId, int gold);
    }

    internal sealed class PeriodicGoldRuntime
    {
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly IPeriodicGoldWorld world;
        private readonly object sync = new object();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Action<int, bool, int, int> applyPlayer;
        private long session;
        private int lastTick, human, ai;
        private bool started, active, registered;

        internal PeriodicGoldRuntime(ManualLogSource log, ExtraFeaturesViewModel settings, IPeriodicGoldWorld world)
        {
            this.log = log;
            this.settings = settings;
            this.world = world;
            applyPlayer = ApplyPlayer;
        }

        internal void Register()
        {
            if (registered) return;
            registered = true;
            if (!world.TryRegisterTick(OnTick, out string reason))
            {
                Error(new InvalidOperationException("Native simulation tick registration failed; periodic gold disabled: " + reason));
                return;
            }
            // Mission/activation publishers root this object for the entire process.
            subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(Initializing));
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(Started));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(Ended));
            Shared.GameplayModActivationGate.StateChanged += RefreshActivation;
            settings.SettingChanged += SettingChanged;
        }

        private void Initializing(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (session == note.Context.SessionId) return;
                EndSession();
                session = note.Context.SessionId;
            }
        }

        private void Started(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                // A replay for a late subscriber must not restart an already active session.
                if (started && session == note.Context.SessionId) return;
                EndSession();
                session = note.Context.SessionId;
                if (!world.TryGetCurrentTick(out lastTick))
                {
                    Error(new InvalidOperationException("Native map tick unavailable at mission start; periodic gold disabled for this session."));
                    return;
                }
                human = settings.PeriodicGoldHuman;
                ai = settings.PeriodicGoldAI;
                started = true;
                settings.SetPeriodicGoldMissionLocked(true);
                ReconcileTick();
            }
        }

        private void Ended(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (session != note.Context.SessionId) return;
                EndSession();
                session = 0;
            }
        }

        private void EndSession()
        {
            started = false;
            human = ai = 0;
            ReconcileTick();
            settings.SetPeriodicGoldMissionLocked(false);
        }

        private void SettingChanged(string name)
        {
            if (name == nameof(ExtraFeaturesViewModel.EnableMod) ||
                name == nameof(ExtraFeaturesViewModel.PeriodicGoldHuman) ||
                name == nameof(ExtraFeaturesViewModel.PeriodicGoldAI)) RefreshActivation();
        }

        private void RefreshActivation(bool allowed) => RefreshActivation();
        private void RefreshActivation()
        {
            lock (sync) ReconcileTick();
        }

        private void ReconcileTick()
        {
            bool needed = started && (human != 0 || ai != 0) &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            if (needed == active) return;
            if (needed)
            {
                // Do not catch up ticks elapsed while disabled.
                if (!world.TryGetCurrentTick(out int tick))
                {
                    Error(new InvalidOperationException("Native map tick unavailable during activation; periodic gold remains inactive."));
                    return;
                }
                lastTick = tick;
            }
            active = needed;
        }

        private void OnTick(int tick)
        {
            lock (sync)
            {
                uint elapsed = unchecked((uint)(tick - lastTick));
                if (!active || elapsed == 0 || elapsed > int.MaxValue) return;
                lastTick = tick;
                // A publisher may already have captured this delegate while activation
                // is changing; the current gate remains authoritative at execution.
                // Schedule from the saved 32-bit counter bits, including its signed Int32 wrap.
                if (unchecked((uint)tick) % 400 != 0 || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod)) return;
                try { world.VisitAlivePlayers(applyPlayer); }
                catch (Exception ex) { Error(ex); }
            }
        }

        private void ApplyPlayer(int playerId, bool isAI, int gold, int incomingGold)
        {
            int amount = isAI ? ai : human;
            if (amount == 0) return;
            try
            {
                if (amount < 0)
                    world.SetGold(playerId, (int)Math.Max(0L, (long)gold + amount));
                else
                {
                    // Vanilla reads pending gold as signed and adds it to a signed balance.
                    long capacity = Math.Max(0L, (long)int.MaxValue - Math.Max(0, gold) - Math.Max(0, incomingGold));
                    int credit = (int)Math.Min(amount, capacity);
                    if (credit > 0) world.AddIncomingGold(playerId, credit);
                }
            }
            catch (Exception ex) { Error(ex); }
        }

        private void Error(Exception ex) => Shared.DebugLogHelper.LogErrorLimited(log,
            "ExtraFeatures.PeriodicGold", "Periodic gold operation failed: " + ex);
    }
}

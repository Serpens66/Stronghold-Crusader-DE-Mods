using APIShared;
using APIShared.Economy;
using APIShared.GameModes;
using APIShared.Lobby;
using BepInEx.Logging;
using R3;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    // Mission-scoped contribution only. APIShared owns native calculation, zero-output
    // completion, stone transport, rendering and process-lifetime hooks.
    internal sealed class WorkerProductionBonusRuntime
    {
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly object sync = new object();
        private WorkerProductionBonusOverrideRegistration registration;
        private long session;
        private int capturedPercent = 50;
        private GameModeSnapshot missionMode;
        private bool registered, started, peersCompatible;

        internal WorkerProductionBonusRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Register()
        {
            if (registered) return;
            registered = true;
            // Admission must exist even when the local native service fails. Otherwise
            // this peer could silently use Vanilla while another peer uses a penalty.
            if (!LobbyLaunchGuards.TryRegister(ExtraFeaturesPlugin.PluginGuid,
                "fixed-production-bonus", GuardLaunch, out string reason))
                Error("Worker production bonus launch admission is unavailable: " + reason);
            else if (!WorkerProductionBonusOverrides.TryRegister(ExtraFeaturesPlugin.PluginGuid,
                "fixed-production-bonus", out registration, out reason))
                Error("Worker production bonus is unavailable: " + reason);
            // Native history guards are eager and remain relevant at 50/disabled.
            // A failed consumer admission/registration must not hide their presence.
            settings.WorkerProductionBonusCompatibilityKey = ExtraFeaturesViewModel.CreateWorkerProductionBonusReport(
                WorkerProductionBonusOverrides.CompatibilityKey, registration != null);

            // These publishers retain the adapter after the startup plugin component is destroyed.
            subscriptions.Add(Shared.MissionEvents.Initialization.Subscribe(Initializing));
            subscriptions.Add(Shared.MissionEvents.Started.Subscribe(Started));
            subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(Ended));
            Shared.GameplayModActivationGate.StateChanged += RefreshActivation;
            settings.SettingChanged += SettingChanged;
            settings.WorkerProductionBonusPeerReportsChanged += LobbyLaunchStatus.RequestRefresh;
        }

        private string GuardLaunch(LobbyLaunchGuardContext context)
        {
            // The mission activation gate is intentionally closed in the lobby.
            // Admission follows the host setting, not that mission-only gate.
            // In-game MP flags are still false in a fresh real lobby. Only the
            // shared command's proven Vanilla local branch can bypass peer checks.
            if (!context.RequiresPeerAgreement) return null;
            if (settings.TryGetWorkerProductionBonusPeerReadiness(out string reason,
                settings.EnableMod && settings.FixedProductionBonusPercent != 50)) return null;
            return string.IsNullOrEmpty(reason)
                ? SerpLocalization.Get("ExtraFeatures.ProductionBonus.PeerWaiting") : reason;
        }

        private void Initializing(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (session != note.Context.SessionId)
                {
                    EndSession();
                    session = note.Context.SessionId;
                }
                // Save settings are prepared before BeforeLoad. postLoading starts the
                // independent simulation timer before the enclosing managed root returns,
                // so waiting for Started could process saved empty batches as Vanilla.
                if (!started && (note.IsBeforeInitialization ||
                    note.Phase == MissionInitializationPhase.NativeLoaded))
                    CaptureSession();
                // A save's allowed mode can first become known at NativeLoaded. The
                // relay updates the activation gate before delivering this checkpoint.
                UpdateAdmission(note.Context.Mode);
                Reconcile();
            }
        }

        private void Started(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (!started || session != note.Context.SessionId)
                {
                    EndSession();
                    session = note.Context.SessionId;
                    CaptureSession(); // Late registration receives only the ready replay.
                }
                UpdateAdmission(note.Context.Mode);
                Reconcile();
            }
        }

        private void CaptureSession()
        {
            capturedPercent = settings.FixedProductionBonusPercent;
            started = true;
            settings.SetFixedProductionBonusMissionLocked(true);
        }

        private void UpdateAdmission(GameModeSnapshot mode)
        {
            missionMode = mode;
            // Unknown evidence is not a single-player admission. Save metadata can
            // become precise only after native load; reevaluate each checkpoint.
            peersCompatible = mode.Kind != GameModeKind.Unknown &&
                (!mode.IsRealMultiplayer || settings.TryGetWorkerProductionBonusPeerReadiness(out _,
                    settings.EnableMod && capturedPercent != 50));
            if (!peersCompatible && mode.IsRealMultiplayer && mode.Kind != GameModeKind.Unknown)
                Error("Worker production bonus was not admitted on every multiplayer peer.");
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
            started = peersCompatible = false;
            missionMode = default;
            capturedPercent = 50;
            registration?.SetPercent(null);
            settings.SetFixedProductionBonusMissionLocked(false);
        }

        private void SettingChanged(string name)
        {
            if (name == nameof(ExtraFeaturesViewModel.EnableMod)) RefreshActivation();
            if (name == nameof(ExtraFeaturesViewModel.EnableMod) ||
                name == nameof(ExtraFeaturesViewModel.FixedProductionBonusPercent))
                LobbyLaunchStatus.RequestRefresh();
            // The percent is frozen before native initialization, including while
            // the final managed save snapshot is being applied.
        }

        private void RefreshActivation(bool allowed)
        {
            // Gate updates precede initialization delivery. Opening here could use
            // admission from the previous checkpoint before its MP evidence updates.
            if (!allowed) RefreshActivation();
        }
        private void RefreshActivation()
        {
            lock (sync)
            {
                UpdateAdmission(missionMode);
                Reconcile();
            }
        }

        private void Reconcile()
        {
            registration?.SetPercent(started && peersCompatible &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && capturedPercent != 50
                ? (int?)capturedPercent : null);
        }

        private void Error(string message) => Shared.DebugLogHelper.LogErrorLimited(log,
            "ExtraFeatures.WorkerProductionBonus", message);
    }
}

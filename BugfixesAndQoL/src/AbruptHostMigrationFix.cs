// Feature: Preserve Vanilla host migration after an abrupt two-player disconnect.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AbruptHostMigrationFix : IDisposable
    {
        private delegate void KickPlayerFromGameDelegate(
            Platform_Multiplayer self,
            Platform_Multiplayer.MPGameMember kickMember,
            bool forceKickFromHost);
        private delegate void SendChoresDelegate(
            Platform_Multiplayer self,
            byte[] choreBuffer);
        private delegate void StartMultiplayerGameDelegate(Director self);

        private enum RecoveryPhase
        {
            Idle,
            WaitingForSave,
            Releasing
        }

        private readonly struct SaveFileSignature
        {
            internal SaveFileSignature(bool exists, long length, long lastWriteUtcTicks)
            {
                Exists = exists;
                Length = length;
                LastWriteUtcTicks = lastWriteUtcTicks;
            }

            internal bool Exists { get; }
            internal long Length { get; }
            internal long LastWriteUtcTicks { get; }
        }

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MultiplayerFeatureGate multiplayerFeatureGate;
        private readonly MethodInfo promoteNewHostMethod;
        private readonly HashSet<int> recoveryTargets = new HashSet<int>();
        private Hook kickHook;
        private Hook sendChoresHook;
        private Hook startMultiplayerGameHook;
        private KickPlayerFromGameDelegate kickOriginal;
        private SendChoresDelegate sendChoresOriginal;
        private StartMultiplayerGameDelegate startMultiplayerGameOriginal;
        private RecoveryPhase recoveryPhase;
        private DateTime recoveryDeadlineUtc;
        private SaveFileSignature recoveryBaseline;
        private bool recoverySaveChoreStarted;
        private bool recoverySaveChoreCompleted;
        private bool recoveryLocalIsHost;
        private bool recoveryResultLogged;
        private string lastCompatibilityFailure;
        private bool disposed;

        internal AbruptHostMigrationFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            MultiplayerFeatureGate multiplayerFeatureGate)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.multiplayerFeatureGate = multiplayerFeatureGate ??
                throw new ArgumentNullException(nameof(multiplayerFeatureGate));

            MethodInfo kickMethod = FindPrivateInstanceMethod(
                "kickPlayerFromGame",
                new[] { typeof(Platform_Multiplayer.MPGameMember), typeof(bool) });
            MethodInfo sendChoresMethod = FindPublicInstanceMethod(
                nameof(Platform_Multiplayer.SendChores),
                new[] { typeof(byte[]) });
            MethodInfo startMultiplayerGameMethod = FindDirectorInstanceMethod(
                nameof(Director.StartMultiplayerGame));
            promoteNewHostMethod = FindPrivateInstanceMethod(
                "promoteNewHost",
                new[] { typeof(Platform_Multiplayer.MPGameMember) });

            try
            {
                kickHook = new Hook(kickMethod, (KickPlayerFromGameDelegate)KickPlayerFromGameHook);
                kickOriginal = kickHook.GenerateTrampoline<KickPlayerFromGameDelegate>();
                sendChoresHook = new Hook(sendChoresMethod, (SendChoresDelegate)SendChoresHook);
                sendChoresOriginal = sendChoresHook.GenerateTrampoline<SendChoresDelegate>();
                startMultiplayerGameHook = new Hook(
                    startMultiplayerGameMethod,
                    (StartMultiplayerGameDelegate)StartMultiplayerGameHook);
                startMultiplayerGameOriginal =
                    startMultiplayerGameHook.GenerateTrampoline<StartMultiplayerGameDelegate>();
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL abrupt host-migration hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            startMultiplayerGameHook?.Undo();
            startMultiplayerGameHook?.Dispose();
            startMultiplayerGameHook = null;
            startMultiplayerGameOriginal = null;
            sendChoresHook?.Undo();
            sendChoresHook?.Dispose();
            sendChoresHook = null;
            sendChoresOriginal = null;
            kickHook?.Undo();
            kickHook?.Dispose();
            kickHook = null;
            kickOriginal = null;
            ResetMapState();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL abrupt host-migration hook disposed.");
        }

        internal void ResetMapState()
        {
            ResetRecoveryState();
        }

        private void ResetRecoveryState()
        {
            recoveryPhase = RecoveryPhase.Idle;
            recoveryDeadlineUtc = DateTime.MinValue;
            recoveryBaseline = default;
            recoverySaveChoreStarted = false;
            recoverySaveChoreCompleted = false;
            recoveryLocalIsHost = false;
            recoveryResultLogged = false;
            lastCompatibilityFailure = null;
            recoveryTargets.Clear();
        }

        private void StartMultiplayerGameHook(Director self)
        {
            try
            {
                if (settings.EnableMod && self != null && !self.SimRunning &&
                    GameData.Instance?.lastGameState != null)
                {
                    GameData.Instance.lastGameState = null;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Cleared the previous multiplayer play state before Vanilla enabled lost-player monitoring for a new game.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not clear the previous multiplayer play state; Vanilla startup continues: {ex}");
            }

            startMultiplayerGameOriginal(self);
        }

        private void KickPlayerFromGameHook(
            Platform_Multiplayer self,
            Platform_Multiplayer.MPGameMember kickMember,
            bool forceKickFromHost)
        {
            if (TryDelayNativeLagKick(self, kickMember, forceKickFromHost))
                return;

            try
            {
                if (TrySelectLocalSuccessor(self, kickMember, out int successorPlayerId))
                {
                    // Vanilla's zero-voter branch returns before this call. Reuse its own
                    // promotion routine so native state and localized chat stay authoritative.
                    promoteNewHostMethod.Invoke(self, new object[] { kickMember });
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Promoted the sole local survivor after an abrupt host disconnect: " +
                        $"departingPlayerId={kickMember.playerID}, successorPlayerId={successorPlayerId}.");
                }
            }
            catch (Exception ex)
            {
                // Host migration is optional; never prevent Vanilla from removing a stale peer.
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Abrupt host migration failed; Vanilla removal continues: {ex}");
            }

            kickOriginal(self, kickMember, forceKickFromHost);
        }

        private void SendChoresHook(Platform_Multiplayer self, byte[] choreBuffer)
        {
            sendChoresOriginal(self, choreBuffer);

            try
            {
                SurrenderDiagnosticBridge.PublishChores(choreBuffer);
                ObserveRecoverySaveChores(choreBuffer);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Connection-recovery save Chore observation failed; the 30-second fail-open timeout remains active: {ex}");
            }
        }

        private bool TryDelayNativeLagKick(
            Platform_Multiplayer multiplayer,
            Platform_Multiplayer.MPGameMember kickMember,
            bool forceKickFromHost)
        {
            if (kickMember == null)
                return false;

            int connectedHumanCount = CountConnectedHumans(multiplayer);
            bool shouldDelay;
            try
            {
                Director director = Director.instance;
                bool multiplayerSimulationRunning =
                    director != null &&
                    director.MultiplayerGame &&
                    director.SimRunning &&
                    Platform_Multiplayer.MPGameActive;
                bool participantsCompatible =
                    TryConfirmRecoveryCompatibility(multiplayer, out string compatibilityFailure);
                if (!participantsCompatible &&
                    !string.Equals(lastCompatibilityFailure, compatibilityFailure, StringComparison.Ordinal))
                {
                    lastCompatibilityFailure = compatibilityFailure;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Connection-recovery save remains fail-open because participant compatibility is unconfirmed: {compatibilityFailure}.");
                }

                shouldDelay = MultiplayerSafetyPolicy.ShouldDelayNativeLagKick(
                    settings.EnableMod,
                    settings.EnableConnectionRecoverySave,
                    multiplayerFeatureGate.BlocksLocalStateChanges,
                    multiplayerSimulationRunning,
                    participantsCompatible,
                    forceKickFromHost,
                    kickMember.steamID,
                    kickMember.skirmishAI,
                    kickMember.kicked,
                    connectedHumanCount);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Connection-recovery save eligibility failed; Vanilla removal continues: {ex}");
                ResetRecoveryState();
                return false;
            }

            if (!shouldDelay)
            {
                if (forceKickFromHost && recoveryPhase != RecoveryPhase.Idle)
                    ResetRecoveryState();
                return false;
            }

            recoveryTargets.Add(kickMember.playerID);
            DateTime now = DateTime.UtcNow;
            if (recoveryPhase == RecoveryPhase.Idle)
            {
                if (!BeginRecoverySave(now))
                    return false;
                return true;
            }

            if (recoveryPhase == RecoveryPhase.WaitingForSave &&
                (recoverySaveChoreCompleted || now >= recoveryDeadlineUtc))
            {
                CompleteRecoveryAttempt(now >= recoveryDeadlineUtc);
            }

            if (recoveryPhase != RecoveryPhase.Releasing ||
                !recoveryTargets.Remove(kickMember.playerID))
            {
                return true;
            }

            if (recoveryTargets.Count == 0)
                ResetRecoveryState();
            return false;
        }

        private bool BeginRecoverySave(DateTime now)
        {
            recoveryPhase = RecoveryPhase.WaitingForSave;
            recoveryDeadlineUtc = now.AddSeconds(MultiplayerSafetyPolicy.RecoveryTimeoutSeconds);
            recoveryBaseline = ReadRecoverySaveSignature();
            recoverySaveChoreStarted = false;
            recoverySaveChoreCompleted = false;
            recoveryResultLogged = false;

            try
            {
                recoveryLocalIsHost = GameNetworkAPI.IsLocalHost();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not identify the host for the connection-recovery save; Vanilla removal continues: {ex}");
                ResetRecoveryState();
                return false;
            }

            if (!recoveryLocalIsHost)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Delaying native lag removal for up to {MultiplayerSafetyPolicy.RecoveryTimeoutSeconds} seconds while the host creates [{MultiplayerSafetyPolicy.RecoverySaveFileName}].");
                return true;
            }

            try
            {
                EngineInterface.TriggerMPSave(MultiplayerSafetyPolicy.RecoverySaveFileName);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Host queued connection-recovery save [{MultiplayerSafetyPolicy.RecoverySaveFileName}] before native lag removal; targets={string.Join(",", recoveryTargets)}.");
                return true;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Host could not queue [{MultiplayerSafetyPolicy.RecoverySaveFileName}]; Vanilla removal continues immediately: {ex}");
                ResetRecoveryState();
                return false;
            }
        }

        private void CompleteRecoveryAttempt(bool timedOut)
        {
            recoveryPhase = RecoveryPhase.Releasing;
            if (recoveryResultLogged)
                return;

            recoveryResultLogged = true;
            bool saveVerified = !timedOut &&
                (!recoveryLocalIsHost || HasRecoverySaveChanged());
            if (saveVerified)
            {
                string outcome = recoveryLocalIsHost
                    ? $"host file=[{GetRecoverySavePath()}] was updated"
                    : "the synchronized Chore 94 completion was observed";
                Shared.DebugLogHelper.LogInfo(log,
                    $"Connection-recovery save completed before native lag removal: {outcome}, targets={string.Join(",", recoveryTargets)}.");
            }
            else
            {
                string reason = timedOut
                    ? $"the {MultiplayerSafetyPolicy.RecoveryTimeoutSeconds}-second timeout expired"
                    : "Chore 94 completed but the host recovery file was not updated";
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Connection-recovery save was not verified because {reason}; Vanilla removal continues fail-open, targets={string.Join(",", recoveryTargets)}.");
            }
        }

        private void ObserveRecoverySaveChores(byte[] choreBuffer)
        {
            if (recoveryPhase != RecoveryPhase.WaitingForSave || choreBuffer == null)
                return;

            int offset = 0;
            for (int recordCount = 0; recordCount < 10000; recordCount++)
            {
                if (offset < 0 || offset + sizeof(int) > choreBuffer.Length)
                    return;

                int payloadLength = BitConverter.ToInt32(choreBuffer, offset);
                if (payloadLength < 0)
                    return;
                if (payloadLength < 1 || payloadLength > choreBuffer.Length - offset - 5)
                    return;

                byte opcode = choreBuffer[offset + 5];
                if (opcode == 39)
                {
                    recoverySaveChoreStarted = true;
                }
                else if (opcode == 94 && recoverySaveChoreStarted)
                {
                    recoverySaveChoreCompleted = true;
                }

                offset += payloadLength + 5;
            }
        }

        private bool HasRecoverySaveChanged()
        {
            SaveFileSignature current = ReadRecoverySaveSignature();
            return current.Exists &&
                (!recoveryBaseline.Exists ||
                 current.Length != recoveryBaseline.Length ||
                 current.LastWriteUtcTicks != recoveryBaseline.LastWriteUtcTicks);
        }

        private static SaveFileSignature ReadRecoverySaveSignature()
        {
            string path = GetRecoverySavePath();
            if (!File.Exists(path))
                return new SaveFileSignature(false, 0, 0);

            var info = new FileInfo(path);
            return new SaveFileSignature(true, info.Length, info.LastWriteTimeUtc.Ticks);
        }

        private static string GetRecoverySavePath() =>
            Path.Combine(
                ConfigSettings.GetSavesPath(),
                MultiplayerSafetyPolicy.RecoverySaveFileName);

        private static int CountConnectedHumans(Platform_Multiplayer multiplayer)
        {
            int count = 0;
            if (multiplayer?.gameMembers == null)
                return count;

            foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
            {
                if (member != null && MultiplayerSafetyPolicy.IsConnectedHuman(
                        member.steamID,
                        member.skirmishAI,
                        member.kicked))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryConfirmRecoveryCompatibility(
            Platform_Multiplayer multiplayer,
            out string failure)
        {
            var participantIds = new List<int>();
            if (multiplayer?.gameMembers != null)
            {
                foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
                {
                    if (member != null && MultiplayerSafetyPolicy.IsConnectedHuman(
                            member.steamID,
                            member.skirmishAI,
                            member.kicked))
                    {
                        participantIds.Add(member.playerID);
                    }
                }
            }

            if (participantIds.Count < 2)
            {
                failure = "fewer than two connected human participants";
                return false;
            }

            if (!settings.System_ArePerPlayerSettingsReady(participantIds, out failure))
            {
                failure = string.IsNullOrWhiteSpace(failure)
                    ? "compatibility reports are incomplete"
                    : failure;
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private bool TrySelectLocalSuccessor(
            Platform_Multiplayer multiplayer,
            Platform_Multiplayer.MPGameMember departingMember,
            out int successorPlayerId)
        {
            successorPlayerId = -1;
            List<AbruptHostMigrationCandidate> candidates = null;
            if (multiplayer?.gameMembers != null)
            {
                candidates = new List<AbruptHostMigrationCandidate>(multiplayer.gameMembers.Count);
                foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
                {
                    if (member == null)
                        continue;

                    candidates.Add(new AbruptHostMigrationCandidate(
                        member.playerID,
                        member.isSelf,
                        member.isHost,
                        member.steamID > 1000 && !member.skirmishAI,
                        member.kicked,
                        member.pendingKick));
                }
            }

            return departingMember != null &&
                   AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                       settings.EnableMod && settings.EnableAbruptHostMigrationFix,
                       departingMember.playerID,
                       departingMember.isSelf,
                       departingMember.isHost,
                       departingMember.steamID > 1000 && !departingMember.skirmishAI,
                       departingMember.kicked,
                       departingMember.pendingKick,
                       candidates,
                       out successorPlayerId);
        }

        private static MethodInfo FindPrivateInstanceMethod(string name, Type[] parameterTypes)
        {
            MethodInfo method = typeof(Platform_Multiplayer).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(Platform_Multiplayer).FullName, name);
            return method;
        }

        private static MethodInfo FindPublicInstanceMethod(string name, Type[] parameterTypes)
        {
            MethodInfo method = typeof(Platform_Multiplayer).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(Platform_Multiplayer).FullName, name);
            return method;
        }

        private static MethodInfo FindDirectorInstanceMethod(string name)
        {
            MethodInfo method = typeof(Director).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(Director).FullName, name);
            return method;
        }
    }
}

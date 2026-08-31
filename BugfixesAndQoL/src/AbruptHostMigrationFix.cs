// Feature: Preserve Vanilla host migration after an abrupt two-player disconnect.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class AbruptHostMigrationFix : IDisposable
    {
        private delegate void KickPlayerFromGameDelegate(
            Platform_Multiplayer self,
            Platform_Multiplayer.MPGameMember kickMember,
            bool forceKickFromHost);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MethodInfo promoteNewHostMethod;
        private Hook hook;
        private KickPlayerFromGameDelegate original;
        private bool disposed;

        internal AbruptHostMigrationFix(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            MethodInfo kickMethod = FindPrivateInstanceMethod(
                "kickPlayerFromGame",
                new[] { typeof(Platform_Multiplayer.MPGameMember), typeof(bool) });
            promoteNewHostMethod = FindPrivateInstanceMethod(
                "promoteNewHost",
                new[] { typeof(Platform_Multiplayer.MPGameMember) });

            try
            {
                hook = new Hook(kickMethod, (KickPlayerFromGameDelegate)KickPlayerFromGameHook);
                original = hook.GenerateTrampoline<KickPlayerFromGameDelegate>();
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
            hook?.Undo();
            hook?.Dispose();
            hook = null;
            original = null;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL abrupt host-migration hook disposed.");
        }

        private void KickPlayerFromGameHook(
            Platform_Multiplayer self,
            Platform_Multiplayer.MPGameMember kickMember,
            bool forceKickFromHost)
        {
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

            original(self, kickMember, forceKickFromHost);
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
    }
}

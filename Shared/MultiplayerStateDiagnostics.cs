using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Shared
{
    /// <summary>
    /// Logs transitions of the base game's native multiplayer/resynchronization state.
    /// The monitor only observes state and is safe to reuse from any mod.
    /// </summary>
    internal sealed class MultiplayerStateDiagnostics : IDisposable
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo ConnectionPauseEngineStateField =
            typeof(Platform_Multiplayer).GetField("connectionPauseEngineState", InstanceFieldFlags);
        private static readonly FieldInfo ConnectionPauseReasonLostConnectionField =
            typeof(Platform_Multiplayer).GetField("connectionPauseReasonLostConnection", InstanceFieldFlags);
        private static readonly FieldInfo ConnectionPauseStartTimeField =
            typeof(Platform_Multiplayer).GetField("connectionPauseStartTime", InstanceFieldFlags);
        private static readonly FieldInfo ResyncingOrSavingResumeTimeField =
            typeof(Platform_Multiplayer).GetField("resyncingOrSavingResumeTime", InstanceFieldFlags);

        private readonly ManualLogSource log;
        private readonly string scopeId;
        private string previousComparisonKey;
        private bool initialized;
        private bool disposed;

        public MultiplayerStateDiagnostics(ManualLogSource log, string scopeId)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("A non-empty diagnostics scope is required.", nameof(scopeId));

            this.scopeId = scopeId;
        }

        public void Initialize()
        {
            if (initialized)
                return;

            disposed = false;
            GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick += OnGameTick;
            initialized = true;
            LogCurrentState("monitor-initialized", GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick, force: true);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            initialized = false;
            GameTimeManagerAPI.Instance.GetFrameProvider().OnGameTick -= OnGameTick;
            previousComparisonKey = null;
        }

        private void OnGameTick(int tick)
        {
            if (!initialized || disposed)
                return;

            LogCurrentState("native-state-transition", tick, force: false);
        }

        private void LogCurrentState(string reason, int tick, bool force)
        {
            try
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                if (multiplayer == null)
                {
                    const string missingKey = "instance=null";
                    if (force || !string.Equals(previousComparisonKey, missingKey, StringComparison.Ordinal))
                    {
                        previousComparisonKey = missingKey;
                        DebugLogHelper.LogWarning(log, $"Multiplayer native state changed: scope={scopeId}, reason={reason}, tick={tick}, instance=null.");
                    }
                    return;
                }

                bool connectionPauseEngineState = ReadField(ConnectionPauseEngineStateField, multiplayer, false);
                bool connectionPauseReasonLostConnection = ReadField(ConnectionPauseReasonLostConnectionField, multiplayer, false);
                DateTime resyncingOrSavingResumeTime = ReadField(ResyncingOrSavingResumeTimeField, multiplayer, DateTime.MinValue);
                DateTime connectionPauseStartTime = ReadField(ConnectionPauseStartTimeField, multiplayer, DateTime.MinValue);
                string memberState = BuildMemberState(multiplayer.gameMembers, includePacketTimes: false);
                string comparisonKey =
                    $"active={Platform_Multiplayer.MPGameActive};host={multiplayer.IsHost};" +
                    $"resyncOrSave={multiplayer.resyncingOrSaving};resync={multiplayer.resyncing};" +
                    $"section={multiplayer.resyncingCurrentSection};layer={multiplayer.resyncingCurrentLayer};" +
                    $"connectionPause={connectionPauseEngineState};lostConnection={connectionPauseReasonLostConnection};" +
                    $"members={memberState}";
                if (!force && string.Equals(previousComparisonKey, comparisonKey, StringComparison.Ordinal))
                    return;

                string previous = previousComparisonKey ?? "none";
                previousComparisonKey = comparisonKey;
                string details =
                    $"scope={scopeId}, reason={reason}, tick={tick}, previous=[{previous}], current=[{comparisonKey}], " +
                    $"resyncStartUtc={FormatUtc(multiplayer.resyncingStart)}, resumeUtc={FormatUtc(resyncingOrSavingResumeTime)}, " +
                    $"connectionPauseStartUtc={FormatUtc(connectionPauseStartTime)}, memberPacketTimes={BuildMemberState(multiplayer.gameMembers, includePacketTimes: true)}.";

                bool problemState = multiplayer.resyncing || multiplayer.resyncingOrSaving ||
                    connectionPauseEngineState || HasMemberProblem(multiplayer.gameMembers);
                if (problemState)
                    DebugLogHelper.LogWarning(log, "Multiplayer native state changed: " + details);
                else
                    DebugLogHelper.LogInfo(log, "Multiplayer native state changed: " + details);
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(log, $"Multiplayer native state diagnostics failed: scope={scopeId}, reason={reason}, tick={tick}, error={ex}.");
            }
        }

        private static string BuildMemberState(
            List<Platform_Multiplayer.MPGameMember> members,
            bool includePacketTimes)
        {
            if (members == null)
                return "none";

            List<string> states = new List<string>(members.Count);
            for (int i = 0; i < members.Count; i++)
            {
                Platform_Multiplayer.MPGameMember member = members[i];
                if (member == null)
                {
                    states.Add("null");
                    continue;
                }

                string state =
                    $"id={member.playerID}:host={member.isHost}:self={member.isSelf}:ai={member.skirmishAI}:" +
                    $"connected={member.stillWithSteamConnection}:kicked={member.kicked}:pendingKick={member._pendingKick}:" +
                    $"errors={member.errorCount}";
                if (includePacketTimes)
                {
                    state +=
                        $":packetsSent={member.packetsSent}:packetsReceived={member.packetsReceived}:" +
                        $"lastPacketUtc={FormatUtc(member.lastTimePacketRecieved)}";
                }

                states.Add(state);
            }

            return states.Count == 0 ? "empty" : string.Join("|", states);
        }

        private static bool HasMemberProblem(List<Platform_Multiplayer.MPGameMember> members)
        {
            if (members == null)
                return false;

            for (int i = 0; i < members.Count; i++)
            {
                Platform_Multiplayer.MPGameMember member = members[i];
                if (member != null && (!member.stillWithSteamConnection || member.kicked || member._pendingKick || member.errorCount > 0))
                    return true;
            }

            return false;
        }

        private static string FormatUtc(DateTime value)
        {
            return value == DateTime.MinValue || value == DateTime.MaxValue
                ? value.ToString("O")
                : value.ToUniversalTime().ToString("O");
        }

        private static T ReadField<T>(FieldInfo field, Platform_Multiplayer instance, T fallback)
        {
            if (field == null || instance == null)
                return fallback;

            object value = field.GetValue(instance);
            return value is T typedValue ? typedValue : fallback;
        }
    }
}

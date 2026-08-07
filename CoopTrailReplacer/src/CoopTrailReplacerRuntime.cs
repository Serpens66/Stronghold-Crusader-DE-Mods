using BepInEx.Logging;
using CoopTrailReplacer.Core;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CoopTrailReplacer
{
    internal sealed class CoopTrailReplacerRuntime : IDisposable
    {
        private delegate void InitCoopMissionsDelegate(FRONT_Multiplayer self);
        private delegate void CoopMissionChangedDelegate(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped);
        private delegate void ButtonClickedDelegate(FRONT_Multiplayer self, string command);

        private static readonly FieldInfo[] CoopTrailFields = Enumerable.Range(1, 4)
            .Select(index => typeof(FRONT_Multiplayer).GetField("CoopTrail" + index, BindingFlags.Static | BindingFlags.NonPublic))
            .ToArray();
        private static readonly MethodInfo UpdateHostInfoMethod = typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MpSetupDataField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ManualLogSource log;
        private readonly string coopTrailsRoot;
        private readonly MissionCatalog catalog = new MissionCatalog();
        private readonly Dictionary<int, ResolvedMission> resolved = new Dictionary<int, ResolvedMission>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private Hook initHook;
        private Hook missionHook;
        private Hook buttonHook;
        private InitCoopMissionsDelegate initTrampoline;
        private CoopMissionChangedDelegate missionTrampoline;
        private ButtonClickedDelegate buttonTrampoline;
        private StartConditionsBridge startConditions;
        private ExtraFeaturesBridge extraFeatures;
        private short packetId;
        private ResolvedMission selected;
        private int selectedTrailId = -1;
        private int selectedMissionId = -1;
        private bool remoteAcknowledged;
        private bool hashMismatch;

        public CoopTrailReplacerRuntime(ManualLogSource log, string pluginRoot)
        {
            this.log = log;
            coopTrailsRoot = Path.Combine(pluginRoot, "CoopTrails");
        }

        public void Initialize()
        {
            startConditions = new StartConditionsBridge(log);
            extraFeatures = new ExtraFeaturesBridge(OnCustomizedLobby);

            var packetHook = GameNetworkAPI.Instance.GetPacketEventFor<MissionHashPacket>();
            packetId = packetHook.GetPacketId();
            subscriptions.Add(packetHook.GetBaseHook().Observable.Subscribe(OnHashPacket));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ClearLaunchState()));

            MethodInfo initMethod = RequireMethod("InitCoopMissions");
            initHook = new Hook(initMethod, (InitCoopMissionsDelegate)InitCoopMissionsHook);
            initTrampoline = initHook.GenerateTrampoline<InitCoopMissionsDelegate>();
            MethodInfo missionMethod = RequireMethod("CoopMissionChanged", typeof(int), typeof(int), typeof(bool));
            missionHook = new Hook(missionMethod, (CoopMissionChangedDelegate)CoopMissionChangedHook);
            missionTrampoline = missionHook.GenerateTrampoline<CoopMissionChangedDelegate>();
            MethodInfo buttonMethod = RequireMethod("ButtonClicked", typeof(string));
            buttonHook = new Hook(buttonMethod, (ButtonClickedDelegate)ButtonClickedHook);
            buttonTrampoline = buttonHook.GenerateTrampoline<ButtonClickedDelegate>();

            LogInfo("Runtime initialized; packetId=" + packetId + ", installedStartConditions=" + startConditions.UsesInstalledPlugin + ".");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            initHook?.Dispose();
            missionHook?.Dispose();
            buttonHook?.Dispose();
            extraFeatures?.Dispose();
            startConditions?.Dispose();
        }

        private void InitCoopMissionsHook(FRONT_Multiplayer self)
        {
            initTrampoline(self);
            try
            {
                ReloadAndReplace();
            }
            catch (Exception ex)
            {
                LogError("Could not replace Coop missions: " + ex);
            }
        }

        private void CoopMissionChangedHook(FRONT_Multiplayer self, int trailId, int missionId, bool resetOrderSwapped)
        {
            extraFeatures?.Clear();
            missionTrampoline(self, trailId, missionId, resetOrderSwapped);
            selectedTrailId = trailId;
            selectedMissionId = missionId;
            resolved.TryGetValue(MissionCatalog.ToKey(trailId + 1, missionId), out selected);
            if (selected == null)
            {
                startConditions.Clear();
                return;
            }

            try
            {
                ApplySelectedMission(self, true);
                startConditions.Apply(selected.Loaded.Definition.StartConditions);
                BeginHashVerification(self);
            }
            catch (Exception ex)
            {
                selected = null;
                startConditions.Clear();
                LogError("Could not activate replacement Trail" + (trailId + 1) + "/" + missionId.ToString("00") + ": " + ex);
            }
        }

        private void ButtonClickedHook(FRONT_Multiplayer self, string command)
        {
            bool launchCommand = string.Equals(command, "Ready", StringComparison.Ordinal) ||
                string.Equals(command, "ReadyLock", StringComparison.Ordinal) ||
                string.Equals(command, "Play", StringComparison.Ordinal);
            if (selected != null && launchCommand && !IsVerificationComplete(self))
            {
                LogError("Blocked '" + command + "': replacement mission assets are not confirmed by the Coop partner.");
                UpdateMissionText();
                return;
            }

            if (selected != null && string.Equals(command, "Play", StringComparison.Ordinal))
                ApplySelectedMission(self, false);
            buttonTrampoline(self, command);
        }

        private void ReloadAndReplace()
        {
            catalog.Load(coopTrailsRoot, LogInfo, LogError);
            resolved.Clear();
            var resolver = new MissionAssetResolver();
            foreach (KeyValuePair<int, LoadedMission> entry in catalog.Missions)
            {
                try
                {
                    if (CoopTrailFields[entry.Value.TrailNumber - 1] == null)
                    {
                        LogError("Trail" + entry.Value.TrailNumber + " is reserved but unavailable in this game build; its replacement was ignored.");
                        continue;
                    }
                    ResolvedMission mission = resolver.Resolve(entry.Value);
                    resolved[entry.Key] = mission;
                    FRONT_Multiplayer.CoopMissionSetupData[] trail = GetTrail(entry.Value.TrailNumber);
                    trail[entry.Value.MissionNumber - 1] = mission.CoopData;
                    LogInfo("Replaced Trail" + entry.Value.TrailNumber + "/" + entry.Value.MissionNumber.ToString("00") + " hash=" + mission.RuntimeHash + ".");
                }
                catch (Exception ex)
                {
                    LogError("Kept Vanilla Trail" + entry.Value.TrailNumber + "/" + entry.Value.MissionNumber.ToString("00") + ": " + ex.Message);
                }
            }
        }

        private void ApplySelectedMission(FRONT_Multiplayer self, bool updateHost)
        {
            if (selected == null)
                return;
            if (self.AIVs == null || self.AIVs.Length != 8)
                self.AIVs = Enumerable.Range(0, 8).Select(_ => new FRONT_Multiplayer.MPAIVInfo()).ToArray();

            EngineInterface.MultiplayerSetupData setupData = (EngineInterface.MultiplayerSetupData)MpSetupDataField.GetValue(self);
            foreach (KeyValuePair<int, FRONT_Multiplayer.MPAIVInfo> entry in selected.AiInfoByPlayerIndex)
            {
                self.AIVs[entry.Key] = entry.Value;
                setupData.preferredAIVs[entry.Key] = -entry.Value.rotation - 1;
            }

            List<PlayerDefinition> players = selected.Loaded.Definition.Players.Where(player => player != null && player.Active).ToList();
            for (int index = 0; index < players.Count; index++)
            {
                Platform_Multiplayer.MPLobbyMember member = self.currentLobby?.GetLobbyMemberFromThis_PlayerID(index + 1);
                if (member != null)
                    member.colourID = players[index].Colour;
            }

            MainViewModel.Instance.CoopMissionTitle = selected.Loaded.Definition.DisplayName;
            MainViewModel.Instance.StandaloneMissionText = selected.Loaded.Definition.Description ?? string.Empty;
            if (updateHost && self.currentLobby != null && self.currentLobby.isHost)
                UpdateHostInfoMethod?.Invoke(self, null);
        }

        private void BeginHashVerification(FRONT_Multiplayer self)
        {
            hashMismatch = false;
            remoteAcknowledged = CountRemoteHumans(self) == 0;
            SendHashPacket(true);
            UpdateMissionText();
        }

        private void OnHashPacket(ReceiveCustomPacketEventArgs<MissionHashPacket> args)
        {
            MissionHashPacket packet = args?.Packet;
            if (packet == null || selected == null || packet.TrailId != selectedTrailId || packet.MissionId != selectedMissionId)
                return;
            bool matches = packet.SchemaVersion == MissionLoader.CurrentSchemaVersion &&
                string.Equals(packet.Hash, selected.RuntimeHash, StringComparison.OrdinalIgnoreCase);
            if (matches)
                remoteAcknowledged = true;
            else
                hashMismatch = true;
            LogInfo("Partner hash " + (matches ? "confirmed" : "mismatch") + " for Trail" + (selectedTrailId + 1) + "/" + selectedMissionId.ToString("00") + ".");
            if (packet.RequestReply)
                SendHashPacket(false);
            UpdateMissionText();
        }

        private void SendHashPacket(bool requestReply)
        {
            if (selected == null)
                return;
            var packet = new MissionHashPacket
            {
                SchemaVersion = MissionLoader.CurrentSchemaVersion,
                TrailId = selectedTrailId,
                MissionId = selectedMissionId,
                Hash = selected.RuntimeHash,
                RequestReply = requestReply,
            };
            byte[] bytes = GameNetworkAPI.Serialize(packet);
            GameNetworkAPI.SendPacketToAllLobby(new Platform_Multiplayer.MPData
            {
                data = bytes,
                dataLength = bytes.Length,
                dataOffset = 0,
                packetType = packetId,
            });
        }

        private bool IsVerificationComplete(FRONT_Multiplayer self) =>
            !hashMismatch && (remoteAcknowledged || CountRemoteHumans(self) == 0);

        private static int CountRemoteHumans(FRONT_Multiplayer self)
        {
            if (self?.currentLobby?.members == null)
                return 0;
            return self.currentLobby.members.Count(member => !member.IsSelf() && !member.SkirmishMember);
        }

        private void UpdateMissionText()
        {
            if (selected == null)
                return;
            string suffix = hashMismatch ? " [asset mismatch]" : (remoteAcknowledged ? string.Empty : " [checking files]");
            MainViewModel.Instance.CoopMissionTitle = selected.Loaded.Definition.DisplayName + suffix;
            MainViewModel.Instance.StandaloneMissionText = selected.Loaded.Definition.Description ?? string.Empty;
        }

        private void OnCustomizedLobby()
        {
            startConditions?.Clear();
            LogInfo("Extra Features customized Coop lobby detected; user-selected StartConditions remain active.");
        }

        private void ClearLaunchState()
        {
            startConditions?.Clear();
            extraFeatures?.Clear();
            selected = null;
            selectedTrailId = -1;
            selectedMissionId = -1;
            remoteAcknowledged = false;
            hashMismatch = false;
        }

        private static FRONT_Multiplayer.CoopMissionSetupData[] GetTrail(int trailNumber)
        {
            FieldInfo field = CoopTrailFields[trailNumber - 1] ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, "CoopTrail" + trailNumber);
            return (FRONT_Multiplayer.CoopMissionSetupData[])field.GetValue(null);
        }

        private static MethodInfo RequireMethod(string name, params Type[] parameterTypes)
        {
            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            return method ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name);
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
    }
}

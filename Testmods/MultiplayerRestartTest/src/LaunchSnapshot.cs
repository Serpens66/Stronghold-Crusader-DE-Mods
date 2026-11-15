using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CrusaderDE;
using Steamworks;

namespace MultiplayerRestartTest
{
    // Only launch inputs are retained. Steam lobby IDs, readiness and network state belong
    // to the replacement lobby and must never be copied from the old session.
    internal sealed class LaunchSnapshot
    {
        private sealed class Member
        {
            internal ulong Id;
            internal int Colour, Lord, Team;
            internal bool Ai;
            internal string CustomName, HumanLordData;
        }

        private readonly Member[] members;
        private readonly ulong[] mapping;
        private readonly string[] aivs;
        private readonly string mapPath, mapHash;
        private readonly FileHeader map;
        private readonly bool coopSwapped;
        private readonly int coopSelected, coopProgress;
        private readonly int[] coopTrailProgress;
        private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private LaunchSnapshot(EngineInterface.MultiplayerSetupData setup, FileHeader map,
            Platform_Multiplayer.MPLobby lobby, int trail, int mission)
        {
            if (setup == null || map == null || lobby == null || lobby.members == null)
                throw new InvalidOperationException("Complete launch data is required.");
            if (lobby.this_player_to_SteamID_mapping == null || lobby.this_player_to_SteamID_mapping.Length != 8)
                throw new InvalidOperationException("Vanilla player-slot mapping is unavailable.");
            this.map = map;
            mapPath = Path.GetFullPath(map.filePath);
            mapHash = HashFile(mapPath);
            SetupText = setup.ToString();
            // Parsing the real serialized setup checks that no launch data is lost by its codec.
            var roundtrip = new EngineInterface.MultiplayerSetupData();
            roundtrip.FromString(SetupText);
            if (!string.Equals(SetupText, roundtrip.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException("Vanilla setup is not round-trip stable.");
            CoopTrailId = trail;
            CoopMissionId = mission;
            GameName = lobby.gameName ?? string.Empty;
            MapName = lobby.mapName ?? string.Empty;
            MapFileName = map.fileName;
            MaxPlayers = Parse(lobby.maxPlayers, "maxPlayers");
            GameTypeCoop = Parse(lobby.gameTypeCoop, "gameTypeCoop");
            Crc = unchecked((int)map.crc);
            LobbySettings = SetupText;
            LobbyCoopTrailId = lobby.coopTrailGame ? lobby.coopTrailID : -1;
            coopSwapped = lobby.coopOrderSwapped;
            coopSelected = lobby.coopSelectedMission;
            coopProgress = lobby.coopTrailFullProgress;
            // SetCoopTrailProgress publishes Steam metadata but does not fill the host's
            // local progress array. Parse the same authoritative string clients consume.
            if (lobby.coopTrailGame)
            {
                var progress = new Platform_Multiplayer.MPLobby();
                progress.coopTrailGameSetup(SteamMatchmaking.GetLobbyData(lobby.id, "cooptrail"));
                if (!progress.coopTrailGame || progress.coopTrailProgress == null ||
                    progress.coopTrailProgress.Length != 10 || progress.coopTrailID != LobbyCoopTrailId ||
                    progress.coopSelectedMission != coopSelected || progress.coopOrderSwapped != coopSwapped ||
                    trail != LobbyCoopTrailId + 1 || mission != coopSelected - 1)
                    throw new InvalidOperationException("Original Coop launch/progress metadata is incomplete or inconsistent.");
                coopTrailProgress = (int[])progress.coopTrailProgress.Clone();
            }
            else coopTrailProgress = null;
            mapping = (ulong[])lobby.this_player_to_SteamID_mapping.Clone();
            aivs = GetAivs(lobby);
            if (aivs.Any(value => value == null))
                throw new InvalidOperationException("The original AI launch configuration is incomplete.");
            var records = new List<Member>();
            var ids = new HashSet<ulong>();
            foreach (var value in lobby.members)
            {
                if (value == null || value.dummyToBeKicked || value.SkirmishHumanMember)
                    throw new InvalidOperationException("Pending or local-skirmish lobby members cannot be restarted.");
                ulong id = value.id.m_SteamID;
                if (id == 0 || !ids.Add(id) || Array.IndexOf(mapping, id) < 0)
                    throw new InvalidOperationException("Every launch member requires a unique mapped slot.");
                records.Add(new Member {
                    Id = id, Colour = value.colourID, Lord = value.lordType,
                    Team = lobby.getTeam(value), Ai = value.SkirmishMember,
                    CustomName = value.customLordName ?? string.Empty,
                    HumanLordData = value.SkirmishMember ? string.Empty :
                        SteamMatchmaking.GetLobbyMemberData(lobby.id, value.id, "lordtype") ?? string.Empty
                });
            }
            members = records.OrderBy(value => Array.IndexOf(mapping, value.Id)).ToArray();
            if (members.Count(value => !value.Ai) < 2 || members.Length > mapping.Length)
                throw new InvalidOperationException("A real multiplayer roster with at least two humans is required.");
            foreach (ulong id in mapping)
                if (id != 0 && !ids.Contains(id))
                    throw new InvalidOperationException("A mapped launch slot has no lobby member.");
            Digest = ComputeDigest();
        }

        internal static LaunchSnapshot Capture(EngineInterface.MultiplayerSetupData setup, FileHeader map,
            int coopTrailId, int coopMissionId) =>
            new LaunchSnapshot(setup, map, Platform_Multiplayer.Instance.activeLobby, coopTrailId, coopMissionId);

        internal string Digest { get; }
        internal string SetupText { get; }
        internal ulong[] HumanIds => members.Where(value => !value.Ai).Select(value => value.Id).ToArray();
        internal FileHeader Map => map;
        internal int CoopTrailId { get; }
        internal int CoopMissionId { get; }
        internal string GameName { get; }
        internal string MapName { get; }
        internal string MapFileName { get; }
        internal int MaxPlayers { get; }
        internal int GameTypeCoop { get; }
        internal int Crc { get; }
        internal string LobbySettings { get; }
        internal int LobbyCoopTrailId { get; }

        internal string ValidateSource()
        {
            try
            {
                if (!string.Equals(map.fileName, MapFileName, StringComparison.Ordinal) ||
                    !string.Equals(Path.GetFullPath(map.filePath), mapPath, StringComparison.OrdinalIgnoreCase) ||
                    unchecked((int)map.crc) != Crc)
                    return "The captured map header changed.";
                return string.Equals(mapHash, HashFile(mapPath), StringComparison.Ordinal) ? null : "The original map file changed.";
            }
            catch (Exception error) { return "The original map cannot be verified: " + error.Message; }
        }

        internal string ValidateHumanRoster(Platform_Multiplayer.MPLobby lobby)
        {
            if (lobby == null || lobby.members == null) return "The replacement lobby is unavailable.";
            var actual = lobby.members.Where(value => value != null && !value.SkirmishMember && !value.dummyToBeKicked)
                .Select(value => value.id.m_SteamID).OrderBy(value => value).ToArray();
            var expected = HumanIds.OrderBy(value => value).ToArray();
            return actual.SequenceEqual(expected) ? null : "Not all original humans, or additional humans, are in the lobby.";
        }

        internal void RestoreLocalMember(Platform_Multiplayer.MPLobby lobby)
        {
            if (lobby == null) throw new ArgumentNullException(nameof(lobby));
            ulong self = SteamUser.GetSteamID().m_SteamID;
            Member record = members.Single(value => !value.Ai && value.Id == self);
            // These keys are client-owned. Host-local edits to a member would be overwritten
            // by GetActiveLobbyMembers on the next refresh.
            SteamMatchmaking.SetLobbyMemberData(lobby.id, "colour", record.Colour.ToString(CultureInfo.InvariantCulture));
            SteamMatchmaking.SetLobbyMemberData(lobby.id, "lordtype", record.HumanLordData);
        }

        internal void RestoreHost(Platform_Multiplayer.MPLobby lobby)
        {
            string error = ValidateHumanRoster(lobby);
            if (error != null) throw new InvalidOperationException(error);
            if (!lobby.isHost || SteamMatchmaking.GetLobbyOwner(lobby.id) != SteamUser.GetSteamID())
                throw new InvalidOperationException("Only the actual replacement-lobby owner can restore launch state.");
            error = ValidateSource();
            if (error != null) throw new InvalidOperationException(error);

            var restored = new List<Platform_Multiplayer.MPLobbyMember>();
            foreach (Member record in members)
            {
                Platform_Multiplayer.MPLobbyMember value;
                if (record.Ai)
                {
                    value = new Platform_Multiplayer.MPLobbyMember();
                    if (record.CustomName.Length == 0) value.SetSkirmishPlayer(record.Id);
                    else value.SetCustmSkirmishPlayer(record.CustomName, record.Id);
                }
                else value = lobby.members.Single(member => !member.SkirmishMember && member.id.m_SteamID == record.Id);
                value.colourID = record.Colour;
                value.lordType = record.Lord;
                value.customLordName = record.CustomName;
                restored.Add(value);
            }
            lobby.members.Clear();
            lobby.members.AddRange(restored);
            lobby.numLobbyMembers = restored.Count;
            Array.Copy(mapping, lobby.this_player_to_SteamID_mapping, mapping.Length);
            foreach (Member record in members)
                lobby.setTeam(restored.Single(value => value.id.m_SteamID == record.Id), record.Team);
            lobby.validateTeams();
            lobby.hostMemberOrder.Clear();
            lobby.hostMemberOrder.AddRange(members.Select(value => value.Id));
            lobby.coopTrailGame = LobbyCoopTrailId >= 0;
            lobby.coopTrailID = LobbyCoopTrailId;
            lobby.coopSelectedMission = coopSelected;
            lobby.coopOrderSwapped = coopSwapped;
            lobby.coopTrailFullProgress = coopProgress;
            lobby.coopTrailProgress = coopTrailProgress == null ? null : (int[])coopTrailProgress.Clone();
            SetAivs(lobby, aivs);

            FRONT_Multiplayer front = Front();
            front.currentLobby = lobby;
            var setup = new EngineInterface.MultiplayerSetupData();
            setup.FromString(SetupText);
            SetFront(front, "MPsetupData", setup);
            SetFront(front, "selectedMPHeader", map);
            SetFront(front, "MPHostLobbyname", GameName);
            SetFront(front, "selectedCoopMissionID", CoopMissionId);
            SetFront(front, "coopOrderSwapped", coopSwapped);
            front.AIVs = new FRONT_Multiplayer.MPAIVInfo[8];
            for (int i = 0; i < front.AIVs.Length; i++)
            {
                front.AIVs[i] = new FRONT_Multiplayer.MPAIVInfo();
                if (i > 0) front.AIVs[i].decode(aivs[i - 1]);
            }
            GameData.Instance.setKeepLocationsFromHeader(map);
            GameData.Instance.setKeepOrder(setup.start_keep_location_order);
            Platform_Multiplayer.Instance.UpdateHostLobbyInfo(GameName, MapName, MapFileName, MaxPlayers,
                GameTypeCoop, SetupText, Crc, front.AIVs);
            if (lobby.coopTrailGame)
            {
                if (coopTrailProgress == null || coopTrailProgress.Length != 10)
                    throw new InvalidOperationException("Original Coop progress is unavailable.");
                Platform_Multiplayer.Instance.SetCoopTrailProgress(LobbyCoopTrailId,
                    (int[])coopTrailProgress.Clone(), coopSelected, coopProgress, coopSwapped);
            }
        }

        internal string ValidateLobby(Platform_Multiplayer.MPLobby lobby, bool host)
        {
            string error = ValidateHumanRoster(lobby);
            if (error != null) return error;
            if (lobby.isHost != host) return "Replacement lobby role differs.";
            if (lobby.members.Count != members.Length || !mapping.SequenceEqual(lobby.this_player_to_SteamID_mapping))
                return "Player-slot mapping differs from the original launch.";
            if (lobby.mapFileName != MapFileName ||
                !int.TryParse(lobby.crc, NumberStyles.Integer, CultureInfo.InvariantCulture, out int crc) || crc != Crc ||
                lobby.settings != SetupText || !GetAivs(lobby).SequenceEqual(aivs))
                return "Map, settings or AI configuration differs from the original launch.";
            if (lobby.coopTrailGame != (LobbyCoopTrailId >= 0) ||
                (lobby.coopTrailGame && (lobby.coopTrailID != LobbyCoopTrailId ||
                 lobby.coopSelectedMission != coopSelected || lobby.coopOrderSwapped != coopSwapped)))
                return "Coop mission selection differs from the original launch.";
            foreach (Member record in members)
            {
                var value = lobby.members.SingleOrDefault(member => member.id.m_SteamID == record.Id);
                if (value == null || value.SkirmishMember != record.Ai || value.colourID != record.Colour ||
                    value.lordType != record.Lord || lobby.getTeam(value) != record.Team ||
                    (record.Ai && (value.customLordName ?? string.Empty) != record.CustomName))
                    return "A player, AI, colour, Lord or team differs from the original launch.";
            }
            object setup = GetFront(Front(), "MPsetupData");
            if (setup == null || setup.ToString() != SetupText)
                return "The local frontend has not received the original starting setup.";
            return null;
        }

        private string ComputeDigest()
        {
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write(1); writer.Write(mapHash); writer.Write(MapFileName); writer.Write(Crc);
                writer.Write(SetupText); writer.Write(CoopTrailId); writer.Write(CoopMissionId);
                writer.Write(LobbyCoopTrailId); writer.Write(coopSelected); writer.Write(coopSwapped);
                writer.Write(coopProgress);
                writer.Write(coopTrailProgress == null ? -1 : coopTrailProgress.Length);
                if (coopTrailProgress != null) foreach (int value in coopTrailProgress) writer.Write(value);
                foreach (ulong value in mapping) writer.Write(value);
                foreach (string value in aivs) writer.Write(value);
                writer.Write(members.Length);
                foreach (Member member in members)
                {
                    writer.Write(member.Id); writer.Write(member.Colour); writer.Write(member.Lord);
                    writer.Write(member.Team); writer.Write(member.Ai); writer.Write(member.CustomName);
                    writer.Write(member.HumanLordData);
                }
                writer.Flush();
                using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(bytes.ToArray()));
            }
        }

        private static FRONT_Multiplayer Front()
        {
            if (!MainViewModel.viewModelLoaded || MainViewModel.Instance.FRONTMultiplayer == null)
                throw new InvalidOperationException("The multiplayer frontend is unavailable.");
            return MainViewModel.Instance.FRONTMultiplayer;
        }

        private static FieldInfo FrontField(string name) => typeof(FRONT_Multiplayer).GetField(name, Fields)
            ?? throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, name);
        private static object GetFront(FRONT_Multiplayer front, string name) => FrontField(name).GetValue(front);
        private static void SetFront(FRONT_Multiplayer front, string name, object value)
        {
            FieldInfo field = FrontField(name);
            if (value != null && !field.FieldType.IsInstanceOfType(value))
                throw new InvalidOperationException("Frontend field contract changed: " + name);
            field.SetValue(front, value);
        }
        private static int Parse(string value, string name)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                throw new InvalidOperationException("Invalid original lobby value: " + name);
            return number;
        }
        private static string[] GetAivs(Platform_Multiplayer.MPLobby lobby) => new[] {
            lobby.AIVDataPlayer2, lobby.AIVDataPlayer3, lobby.AIVDataPlayer4, lobby.AIVDataPlayer5,
            lobby.AIVDataPlayer6, lobby.AIVDataPlayer7, lobby.AIVDataPlayer8 };
        private static void SetAivs(Platform_Multiplayer.MPLobby lobby, string[] values)
        {
            lobby.AIVDataPlayer2 = values[0]; lobby.AIVDataPlayer3 = values[1]; lobby.AIVDataPlayer4 = values[2];
            lobby.AIVDataPlayer5 = values[3]; lobby.AIVDataPlayer6 = values[4]; lobby.AIVDataPlayer7 = values[5];
            lobby.AIVDataPlayer8 = values[6];
        }
        private static string HashFile(string path)
        {
            using (var input = File.OpenRead(path))
            using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(input));
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);
    }
}

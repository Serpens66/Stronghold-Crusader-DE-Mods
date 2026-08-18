using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CrusaderDE;
using Noesis;
using Steamworks;
using UnityEngine;

public class Platform_Multiplayer
{
	public class MPData
	{
		public short packetType;

		public int dataLength = -1;

		public byte[] data;

		public int dataOffset;

		public byte[] ToBytes()
		{
			int num = dataLength;
			if (dataLength < 0)
			{
				num = data.Length;
			}
			byte[] array = new byte[6 + num];
			if (num > 0)
			{
				Array.Copy(data, dataOffset, array, 6, num);
			}
			byte[] bytes = BitConverter.GetBytes(packetType);
			byte[] bytes2 = BitConverter.GetBytes(num);
			array[0] = bytes[0];
			array[1] = bytes[1];
			array[2] = bytes2[0];
			array[3] = bytes2[1];
			array[4] = bytes2[2];
			array[5] = bytes2[3];
			return array;
		}

		public static MPData FromBytes(byte[] source)
		{
			MPData mPData = new MPData();
			mPData.packetType = BitConverter.ToInt16(source, 0);
			mPData.dataLength = BitConverter.ToInt32(source, 2);
			mPData.data = new byte[mPData.dataLength];
			if (mPData.dataLength > 0)
			{
				Array.Copy(source, 6, mPData.data, 0, mPData.dataLength);
			}
			return mPData;
		}
	}

	public class TrailMissionInfo
	{
		public int[] lordTypes = new int[8];

		public int[] locations = new int[8];

		public int[] teams = new int[8];

		public int[] aiv_type = new int[8];

		public int fairness;

		public int starting_level;

		public int num_players;

		public int barracks;

		public int merc_post;

		public int stockade;

		public TrailMissionInfo(int[] data)
		{
			for (int i = 0; i < 8; i++)
			{
				lordTypes[i] = data[i];
				locations[i] = data[i + 8];
				teams[i] = data[i + 16];
				aiv_type[i] = data[i + 24];
			}
			fairness = data[32];
			starting_level = data[33];
			num_players = data[34];
			barracks = data[35];
			merc_post = data[36];
			stockade = data[37];
		}
	}

	public class MPLobby
	{
		public CSteamID id;

		public int numLobbyMembers;

		public string gameName;

		public string mapName;

		public string mapFileName;

		public string maxPlayers;

		public string AIPlayers;

		public string gameTypeCoop;

		public string settings;

		public string country;

		public bool isHost;

		public string crc;

		public string startGame;

		public bool coopTrailGame;

		public int coopTrailID;

		public int coopSelectedMission;

		public int coopTrailFullProgress;

		public bool coopOrderSwapped;

		public int[] coopTrailProgress;

		public bool clientFound;

		public string AIVDataPlayer2 = "0:0:0:0";

		public string AIVDataPlayer3 = "0:0:0:0";

		public string AIVDataPlayer4 = "0:0:0:0";

		public string AIVDataPlayer5 = "0:0:0:0";

		public string AIVDataPlayer6 = "0:0:0:0";

		public string AIVDataPlayer7 = "0:0:0:0";

		public string AIVDataPlayer8 = "0:0:0:0";

		public string sentAIVDataPlayer2 = "0:0:0:0";

		public string sentAIVDataPlayer3 = "0:0:0:0";

		public string sentAIVDataPlayer4 = "0:0:0:0";

		public string sentAIVDataPlayer5 = "0:0:0:0";

		public string sentAIVDataPlayer6 = "0:0:0:0";

		public string sentAIVDataPlayer7 = "0:0:0:0";

		public string sentAIVDataPlayer8 = "0:0:0:0";

		public ulong sentClientInfo;

		public Dictionary<ulong, int> teams = new Dictionary<ulong, int>();

		public List<ulong> hostMemberOrder = new List<ulong>();

		public List<MPLobbyMember> members = new List<MPLobbyMember>();

		public ulong[] this_player_to_SteamID_mapping = new ulong[8];

		public int iMaxPlayers => EditorDirector.getIntFromString(maxPlayers);

		public ulong identifier => (ulong)id;

		public string setTeams
		{
			get
			{
				bool flag = true;
				string text = "";
				foreach (KeyValuePair<ulong, int> team in teams)
				{
					if (flag)
					{
						flag = false;
					}
					else
					{
						text += ",";
					}
					text = text + team.Key + "," + team.Value;
				}
				return text;
			}
			set
			{
				if (value.Length <= 0)
				{
					return;
				}
				string[] array = value.Split(",", StringSplitOptions.None);
				teams.Clear();
				List<ulong> list = new List<ulong>();
				if (array.Length >= 2)
				{
					for (int i = 0; i < array.Length; i += 2)
					{
						ulong num = EditorDirector.getuLongFromString(array[i]);
						teams[num] = EditorDirector.getIntFromString(array[i + 1]);
						list.Add(num);
					}
				}
				if (isHost || list.Count != members.Count)
				{
					return;
				}
				bool flag = true;
				for (int j = 0; j < members.Count; j++)
				{
					if (list[j] != members[j].id.m_SteamID)
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					return;
				}
				List<MPLobbyMember> list2 = new List<MPLobbyMember>();
				for (int k = 0; k < list.Count; k++)
				{
					ulong num2 = list[k];
					foreach (MPLobbyMember member in members)
					{
						if (num2 == member.id.m_SteamID)
						{
							list2.Add(member);
							break;
						}
					}
				}
				members.Clear();
				foreach (MPLobbyMember item in list2)
				{
					members.Add(item);
				}
			}
		}

		public string AIVDataChecksum()
		{
			string s = AIVDataPlayer2 + AIVDataPlayer3 + AIVDataPlayer4 + AIVDataPlayer5 + AIVDataPlayer6 + AIVDataPlayer7 + AIVDataPlayer8;
			using MD5 mD = MD5.Create();
			return BitConverter.ToString(mD.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", string.Empty);
		}

		public void coopTrailGameSetup(string coopStr)
		{
			if (coopStr == "-1")
			{
				coopTrailGame = false;
				coopTrailProgress = null;
				return;
			}
			coopTrailGame = true;
			string[] array = coopStr.Split(',', StringSplitOptions.None);
			if (array.Length == 0)
			{
				return;
			}
			int.TryParse(array[0], out coopTrailID);
			if (array.Length != 14)
			{
				coopTrailProgress = null;
				return;
			}
			coopTrailProgress = new int[10];
			for (int i = 0; i < 10; i++)
			{
				int.TryParse(array[i + 1], out coopTrailProgress[i]);
			}
			int.TryParse(array[11], out coopSelectedMission);
			int.TryParse(array[12], out coopTrailFullProgress);
			int result = 0;
			int.TryParse(array[13], out result);
			coopOrderSwapped = result == 1;
		}

		public int getTeam(MPLobbyMember member)
		{
			if (member == null)
			{
				return -1;
			}
			if (teams.TryGetValue(member.id.m_SteamID, out var value))
			{
				return value;
			}
			return -1;
		}

		public int CountTeamMembers(int team)
		{
			int num = 0;
			foreach (KeyValuePair<ulong, int> team2 in teams)
			{
				if (team2.Value == team)
				{
					num++;
				}
			}
			return num;
		}

		public void setTeam(MPLobbyMember member, int newTeam)
		{
			teams[member.id.m_SteamID] = newTeam;
		}

		public void switchTeamID(ulong origID, ulong newID)
		{
			int value = teams[origID];
			teams.Remove(origID);
			teams[newID] = value;
		}

		public int getFreeTeam()
		{
			Dictionary<int, ulong> dictionary = new Dictionary<int, ulong>();
			foreach (KeyValuePair<ulong, int> team in teams)
			{
				dictionary[team.Value] = team.Key;
			}
			for (int i = 1; i <= 8; i++)
			{
				if (!dictionary.ContainsKey(i))
				{
					return i;
				}
			}
			return 1;
		}

		public void validateTeams()
		{
			Dictionary<ulong, int> dictionary = new Dictionary<ulong, int>();
			bool flag = false;
			foreach (MPLobbyMember member in members)
			{
				if (teams.ContainsKey(member.id.m_SteamID))
				{
					dictionary[member.id.m_SteamID] = teams[member.id.m_SteamID];
				}
				else
				{
					flag = true;
				}
			}
			if (!flag && dictionary.Count != teams.Count)
			{
				flag = true;
			}
			if (flag)
			{
				teams = dictionary;
			}
		}

		public void forceCoopTeams()
		{
			foreach (MPLobbyMember member in members)
			{
				if (teams.ContainsKey(member.id.m_SteamID))
				{
					teams[member.id.m_SteamID] = 1;
				}
			}
		}

		public int findCustomCoopEnemyTeam()
		{
			int num = -1;
			int num2 = -1;
			foreach (MPLobbyMember member in members)
			{
				if (!teams.ContainsKey(member.id.m_SteamID))
				{
					continue;
				}
				if (member.SkirmishMember)
				{
					int num3 = teams[member.id.m_SteamID];
					if (num2 == -1)
					{
						num2 = num3;
					}
				}
				else
				{
					int num4 = teams[member.id.m_SteamID];
					if (num == -1)
					{
						num = num4;
					}
				}
			}
			if (num2 == -1)
			{
				if (num == -1)
				{
					return 2;
				}
				num2 = num + 1;
				if (num2 > 8)
				{
					num2 = 1;
				}
			}
			return num2;
		}

		public bool getEnoughTeams()
		{
			int num = -1;
			foreach (MPLobbyMember member in members)
			{
				if (teams.TryGetValue(member.id.m_SteamID, out var value))
				{
					if (num < 0)
					{
						num = value;
					}
					else if (num != value)
					{
						return true;
					}
				}
			}
			return false;
		}

		public string getHostMemberOrder()
		{
			bool flag = true;
			string text = "";
			foreach (MPLobbyMember member in members)
			{
				if (flag)
				{
					flag = false;
				}
				else
				{
					text += ",";
				}
				text = text + member.id.m_SteamID + "," + member.colourID;
			}
			for (int i = 0; i < 8; i++)
			{
				text = text + "," + this_player_to_SteamID_mapping[i];
			}
			return text;
		}

		public void setHostMemberOrder(string value)
		{
			List<ulong> list = new List<ulong>();
			foreach (MPLobbyMember member in members)
			{
				if (member.id.m_SteamID < 1000)
				{
					list.Add(member.id.m_SteamID);
				}
			}
			string[] array = value.Split(",", StringSplitOptions.None);
			hostMemberOrder.Clear();
			for (int i = 0; i < array.Length - 8; i += 2)
			{
				ulong num = EditorDirector.getuLongFromString(array[i]);
				hostMemberOrder.Add(num);
				int intFromString = EditorDirector.getIntFromString(array[i + 1]);
				if (num < 1000)
				{
					list.Remove(num);
					AddSkirmishPlayer(num);
				}
				foreach (MPLobbyMember member2 in members)
				{
					if (member2.id.m_SteamID == num)
					{
						member2.colourID = intFromString;
						break;
					}
				}
			}
			int num2 = 0;
			int num3 = array.Length - 8;
			while (num2 < 8)
			{
				ulong num4 = EditorDirector.getuLongFromString(array[num3]);
				this_player_to_SteamID_mapping[num2] = num4;
				num2++;
				num3++;
			}
			foreach (ulong item in list)
			{
				foreach (MPLobbyMember member3 in members)
				{
					if (member3.id.m_SteamID == item)
					{
						members.Remove(member3);
						break;
					}
				}
			}
		}

		public void AddSkirmishPlayer(ulong AILordFullType)
		{
			foreach (MPLobbyMember member in members)
			{
				if (member.SkirmishMember && !member.SkirmishHumanMember && member.id.m_SteamID == AILordFullType)
				{
					return;
				}
			}
			MPLobbyMember mPLobbyMember = new MPLobbyMember();
			mPLobbyMember.SetSkirmishPlayer(AILordFullType);
			members.Add(mPLobbyMember);
			numLobbyMembers = members.Count;
		}

		public int getThisPlayerFromSteamID(ulong steamID)
		{
			for (int i = 0; i < 8; i++)
			{
				if (this_player_to_SteamID_mapping[i] == steamID)
				{
					return i + 1;
				}
			}
			return -1;
		}

		public MPLobbyMember GetLobbyMemberFromThis_PlayerID(int playerID)
		{
			if (playerID > 0)
			{
				foreach (MPLobbyMember member in members)
				{
					if (member.id.m_SteamID == this_player_to_SteamID_mapping[playerID - 1])
					{
						return member;
					}
				}
			}
			return null;
		}

		public int CountAIPlayers()
		{
			int num = 0;
			foreach (MPLobbyMember member in members)
			{
				if (member.id.m_SteamID < 1000)
				{
					num++;
				}
			}
			return num;
		}

		public int CountHumanPlayers()
		{
			return numLobbyMembers - CountAIPlayers();
		}

		public MPLobby()
		{
		}

		public MPLobby(TrailMissionInfo tmi)
		{
			int[] array = new int[50];
			isHost = true;
			for (int i = 0; i < tmi.num_players; i++)
			{
				MPLobbyMember mPLobbyMember = new MPLobbyMember();
				if (i == 0)
				{
					mPLobbyMember.colourID = ConfigSettings.Settings_PlayerColour + 1;
				}
				else if (i == ConfigSettings.Settings_PlayerColour)
				{
					mPLobbyMember.colourID = 1;
				}
				else
				{
					mPLobbyMember.colourID = i + 1;
				}
				if (i == 0)
				{
					mPLobbyMember.id.m_SteamID = Instance.GetLocalSteamID();
					mPLobbyMember.Name = ConfigSettings.Settings_UserName;
					mPLobbyMember.SkirmishHumanMember = (mPLobbyMember.SkirmishMember = true);
				}
				else
				{
					mPLobbyMember.SkirmishHumanMember = false;
					mPLobbyMember.SkirmishMember = true;
					mPLobbyMember.SetSkirmishPlayer(tmi.lordTypes[i] - 1, array[tmi.lordTypes[i] - 1]);
					array[tmi.lordTypes[i] - 1]++;
				}
				setTeam(mPLobbyMember, tmi.teams[i]);
				members.Add(mPLobbyMember);
			}
			numLobbyMembers = tmi.num_players;
		}

		public MPLobby(HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
		{
			isHost = true;
			for (int i = 0; i < restartInfo.lordTypes.Count; i++)
			{
				MPLobbyMember mPLobbyMember = new MPLobbyMember();
				if (restartInfo.lordTypes[i] == -9999)
				{
					mPLobbyMember.dummyToBeKicked = true;
					mPLobbyMember.SkirmishHumanMember = false;
					mPLobbyMember.SkirmishMember = true;
					mPLobbyMember.SetSkirmishPlayer((ulong)i + 500uL);
					members.Add(mPLobbyMember);
					continue;
				}
				mPLobbyMember.colourID = restartInfo.colours[i];
				if (restartInfo.lordTypes[i] < 0)
				{
					mPLobbyMember.id.m_SteamID = Instance.GetLocalSteamID();
					mPLobbyMember.Name = ConfigSettings.Settings_UserName;
					mPLobbyMember.SkirmishHumanMember = (mPLobbyMember.SkirmishMember = true);
				}
				else
				{
					mPLobbyMember.SkirmishHumanMember = false;
					mPLobbyMember.SkirmishMember = true;
					if (restartInfo.aivs != null && restartInfo.aivs[i] != null && restartInfo.aivs[i].lordName.Length > 0)
					{
						if (!CustomisationFileManager.Instance.doesCustomLordExist(restartInfo.aivs[i].lordName))
						{
							mPLobbyMember.SkirmishCustomLordExistsLocally = false;
						}
						mPLobbyMember.SetCustmSkirmishPlayer(restartInfo.aivs[i].lordName, (ulong)restartInfo.lordTypes[i]);
					}
					else
					{
						mPLobbyMember.SetSkirmishPlayer((ulong)restartInfo.lordTypes[i]);
					}
				}
				setTeam(mPLobbyMember, restartInfo.teams[i]);
				members.Add(mPLobbyMember);
			}
			numLobbyMembers = restartInfo.lordTypes.Count;
		}

		public bool kickEmptySlots()
		{
			bool flag = true;
			bool result = false;
			while (flag)
			{
				flag = false;
				foreach (MPLobbyMember member in members)
				{
					if (member.dummyToBeKicked)
					{
						members.Remove(member);
						result = (flag = true);
						break;
					}
				}
			}
			numLobbyMembers = members.Count;
			return result;
		}
	}

	public class MPLobbyMember
	{
		public CSteamID id;

		public bool dummyToBeKicked;

		public string name;

		public bool ready;

		public int mapStatus;

		public string mapRequested = "";

		public int colourID = -1;

		public int lordType;

		public string customLordName = "";

		public int teamShield = -1;

		public DateTime pingSent = DateTime.MinValue;

		public int lastPingDuration = -1;

		public bool SkirmishMember;

		public bool SkirmishHumanMember;

		public bool SkirmishCustomLordExistsLocally = true;

		public Dictionary<string, bool> mapsSent = new Dictionary<string, bool>();

		public string Name
		{
			get
			{
				if (!SkirmishMember || SkirmishHumanMember)
				{
					return name;
				}
				int lord = GetLordType();
				int lordSubType = GetLordSubType();
				return GetName(lord, lordSubType);
			}
			set
			{
				name = value;
			}
		}

		public string CombinedName
		{
			get
			{
				if (!SkirmishMember || SkirmishHumanMember)
				{
					return name;
				}
				int num = GetLordType();
				int lordSubType = GetLordSubType();
				return OnScreenText.getComputerName(num + 1, lordSubType);
			}
			set
			{
				name = value;
			}
		}

		public string AITypeName
		{
			get
			{
				if (SkirmishMember && !SkirmishHumanMember)
				{
					switch (GetLordType())
					{
					case 0:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_SELECT_SWORDSMEN);
					case 1:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_XBOWMEN);
					case 2:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_REPAIR);
					case 3:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_FRONTEND_BUILDER_SHIELD1);
					case 4:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_STRETCHING_RACK);
					case 5:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_POND);
					case 6:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_SELECT_CATAPULTS);
					case 7:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_BRIEF_STARTGAME);
					case 8:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_FE_SECTION_2);
					case 9:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_ARAB_SWORDSMAN);
					case 10:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_SELECT_ARAB_SWORDSMAN);
					case 11:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_ADD_CPU);
					case 12:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_FRONTEND_SHIELDX3);
					case 13:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.BHELP_TEXT_BEDOUIN_DEMOLISHER);
					case 14:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_AI_TYPE15);
					case 15:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, Enums.eTextValues.TEXT_WR_AI_TYPE16);
					case 16:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 221);
					case 17:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 222);
					case 18:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 223);
					case 19:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 224);
					case 20:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 225);
					case 21:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 226);
					case 22:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 227);
					case 23:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 228);
					case 24:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 229);
					case 25:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 453);
					case 26:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 470);
					case 27:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 487);
					case 28:
						return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, 504);
					case 29:
					case 30:
					case 31:
					case 32:
					case 33:
					case 34:
					case 35:
					case 36:
					case 37:
						return MapFileManager.SplitCustomTrailName(customLordName);
					}
				}
				return "";
			}
		}

		public static string GetName(int lord, int subType)
		{
			switch (lord)
			{
			case 0:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(240 + subType));
			case 1:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(249 + subType));
			case 2:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(258 + subType));
			case 3:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(267 + subType));
			case 4:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(276 + subType));
			case 5:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(285 + subType));
			case 6:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(294 + subType));
			case 7:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(303 + subType));
			case 8:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(312 + subType));
			case 9:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(321 + subType));
			case 10:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(330 + subType));
			case 11:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(339 + subType));
			case 12:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(348 + subType));
			case 13:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(357 + subType));
			case 14:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(366 + subType));
			case 15:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_XPLAY_WAITING_ROOM, (Enums.eTextValues)(375 + subType));
			case 16:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(89 + subType));
			case 17:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(98 + subType));
			case 18:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(107 + subType));
			case 19:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(116 + subType));
			case 20:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(125 + subType));
			case 21:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(134 + subType));
			case 22:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(143 + subType));
			case 23:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(152 + subType));
			case 24:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(161 + subType));
			case 25:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(462 + subType));
			case 26:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(479 + subType));
			case 27:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(496 + subType));
			case 28:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_CTEXT, (Enums.eTextValues)(513 + subType));
			case 29:
			case 30:
			case 31:
			case 32:
			case 33:
			case 34:
			case 35:
			case 36:
			case 37:
				return Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 23 + subType);
			default:
				return "";
			}
		}

		public int GetLordType()
		{
			return (int)((id.m_SteamID - 1) / 8);
		}

		public int GetLordSubType()
		{
			return (int)((id.m_SteamID - 1) % 8);
		}

		public void SetSkirmishPlayer(int AILordType, int subType)
		{
			SkirmishMember = true;
			id.m_SteamID = (ulong)(AILordType * 8 + subType + 1);
			customLordName = "";
		}

		public void SetSkirmishPlayer(ulong AILordTypeFullType)
		{
			SkirmishMember = true;
			id.m_SteamID = AILordTypeFullType;
			customLordName = "";
		}

		public void SetCustmSkirmishPlayer(string name, ulong AILordTypeFullType)
		{
			SkirmishMember = true;
			id.m_SteamID = AILordTypeFullType;
			customLordName = name;
		}

		public void SetCustmSkirmishPlayerTemp(string name, int subType)
		{
			SkirmishMember = true;
			id.m_SteamID = (ulong)(304 + subType + 1);
			customLordName = name;
		}

		public void SetValidCustomLordType(int slot, int subType)
		{
			id.m_SteamID = (ulong)((29 + slot) * 8 + subType + 1);
		}

		public bool IsSelf()
		{
			//IL_0010: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			if (SkirmishMember)
			{
				return SkirmishHumanMember;
			}
			return id == SteamUser.GetSteamID();
		}

		public ulong GetSteamID()
		{
			return id.m_SteamID;
		}
	}

	public class MapSendQueueItem
	{
		public byte[] data;

		public SteamNetworkingIdentity NetID;

		public DateTime sendTime;
	}

	public class MPGameMember
	{
		public MPLobbyMember lobbyData;

		public SteamNetworkingIdentity SNI;

		public bool stillWithSteamConnection = true;

		public ulong steamID;

		public int playerID;

		public bool isHost;

		public bool isSelf;

		public bool acknowledged;

		public string playerName;

		public bool muted;

		public int colourID;

		public bool skirmishAI;

		public int lordType;

		public int packetsSent;

		public int packetsReceived;

		public string quality = "";

		public DateTime lastTimePacketRecieved = DateTime.MaxValue;

		public int errorCount;

		public bool kicked;

		public bool _pendingKick;

		public DateTime pendingKickTime = DateTime.MinValue;

		public int kickCounter;

		public DateTime[] kickVoteTime = new DateTime[9];

		public bool pendingKick
		{
			get
			{
				if (_pendingKick && pendingKickTime > DateTime.UtcNow.AddSeconds(-45.0))
				{
					return true;
				}
				_pendingKick = false;
				return false;
			}
			set
			{
				_pendingKick = value;
				pendingKickTime = DateTime.UtcNow;
			}
		}

		public MPGameMember()
		{
			for (int i = 0; i < 9; i++)
			{
				kickVoteTime[i] = DateTime.MinValue;
			}
		}

		public bool DoVoteKick(int voterID, int otherActivePlayers)
		{
			DateTime utcNow = DateTime.UtcNow;
			DateTime dateTime = utcNow.AddMinutes(-2.0);
			kickVoteTime[voterID] = utcNow;
			int num = 0;
			for (int i = 0; i < 9; i++)
			{
				if (kickVoteTime[i] > dateTime)
				{
					num++;
				}
			}
			if (num > (otherActivePlayers + 1) / 2)
			{
				return true;
			}
			return false;
		}
	}

	public class MessageData
	{
		public MPData data;

		public MPGameMember fromMember;
	}

	public static readonly Platform_Multiplayer instance;

	public bool PendingMPLobby;

	public bool PendingMPLobby_DelayedMPEnter;

	public int lastLobbyMode;

	public string ShareCodeString = "ABCD";

	public bool IsHost;

	public bool seedReceived;

	public int randSeedReceived;

	public bool mapLoaded;

	public bool resyncingOrSaving;

	public bool resyncing;

	public DateTime resyncingStart = DateTime.MinValue;

	public int resyncingCurrentSection;

	public int resyncingCurrentLayer;

	public DateTime resyncingOrSavingResumeTime = DateTime.MinValue;

	public bool monitoringForGameStart;

	public DateTime monitoringForGameStartTime = DateTime.MinValue;

	public int localPlayerID;

	public EngineInterface.LoadMapReturnData lastRetData;

	public bool loadingFromSave;

	public bool newGameNotLoad = true;

	public int achFood;

	public int achWood;

	public int achWeapons;

	public List<MPGameMember> gameMembers;

	public int[] loadPlayerRemapping = new int[9];

	public DateTime connectionCheckTime = DateTime.MinValue;

	public DateTime connectionPauseStartTime = DateTime.MinValue;

	public bool connectionPauseEngineState;

	public bool connectionPauseReasonLostConnection = true;

	public const bool HAS_MULTIPLAYER = true;

	public const string MP_VERSION = "23";

	public int[] UIColourRemap = new int[9] { 0, 1, 4, 2, 3, 6, 5, 7, 8 };

	public uint numLobbies;

	public List<MPLobby> lobbies = new List<MPLobby>();

	public Action LobbyListPopulatedDelegate;

	public Action LobbyCreatedDelegate;

	public Action LobbyJoinedDelegate;

	public Action<string, string, int> LobbyChatDelegate;

	public DateTime lastLobbyDataRefresh = DateTime.MinValue;

	public DateTime kickMemberTime = DateTime.MinValue;

	public ulong coopPartnerID;

	public ulong CoopContinuationLobbyID;

	public MPLobby activeLobby;

	public ulong inviteLobbyID;

	public Callback<GameLobbyJoinRequested_t> m_JoinLobbyRequested;

	public Callback<GameRichPresenceJoinRequested_t> m_GameRichPresenceJoinRequested;

	public MPLobby autoJoinLobby;

	public CSteamID lobbyJoiningID;

	public Avatars.AvatarDesign tempAD = new Avatars.AvatarDesign();

	public string LastCoAString = "";

	public Callback<LobbyChatMsg_t> IncomingMessage;

	public Callback<SteamNetworkingMessagesSessionRequest_t> NetworkUserListener;

	public Action MapSendDelegate;

	public Queue<MapSendQueueItem> mapSendQueue = new Queue<MapSendQueueItem>();

	public DateTime lastMapSendQueueItemTime = DateTime.MinValue;

	public int lastCrc;

	public int incomingSize;

	public int incomingOfset;

	public string lastReceivedFileName = "";

	public Action MapReceivedDelegate;

	public Action MapProgressDelegate;

	public int receivingMode;

	public byte[] receiveBuffer;

	public int MapReceiveProgress = 1;

	public ConcurrentQueue<MessageData> threadedMessages = new ConcurrentQueue<MessageData>();

	public Dictionary<CSteamID, ImageSource> _Cache = new Dictionary<CSteamID, ImageSource>();

	public ImageSource _localUserAvatar;

	public ImageSource _localUserCoatOfArms;

	public Callback<AvatarImageLoaded_t> m_AvatarLoadedRequested;

	public static Platform_Multiplayer Instance => instance;

	public static bool Initialised => SteamManager.Initialized;

	public static bool MPGameActive { get; set; }

	public static bool MPChatMuted { get; set; }

	static Platform_Multiplayer()
	{
		instance = new Platform_Multiplayer();
	}

	public void initCommon()
	{
		connectionPauseEngineState = false;
	}

	public void SendSaveCRCs(bool coopGame)
	{
		foreach (FileHeader mPSafe in MapFileManager.Instance.GetMPSaves(0, sortAscend: true))
		{
			mPSafe.retrieveCRCChecks = 0;
			if ((coopGame && mPSafe.coopTrailID > 0) || (!coopGame && mPSafe.coopTrailID <= 0))
			{
				SendSaveCRC(mPSafe.fileName, mPSafe.xPlaySaveChecksum ^ mPSafe.xPlaySaveTime);
			}
		}
	}

	public void SendGamePacketToAll(byte[] gameData, int len, int offset = 0)
	{
		MPData mPData = new MPData();
		mPData.data = gameData;
		mPData.dataLength = len;
		mPData.dataOffset = offset;
		mPData.packetType = 1;
		SendPacketToAll(mPData);
	}

	public void SendEmptyPacketTypeToAll(Enums.MPFlags packetType)
	{
		MPData mPData = new MPData();
		mPData.data = new byte[0];
		mPData.dataLength = 0;
		mPData.packetType = (short)packetType;
		SendPacketToAll(mPData);
	}

	public void SendPacketToAll(MPData data, bool instantMessage = false)
	{
		if (gameMembers == null)
		{
			return;
		}
		byte[] dataToSend = data.ToBytes();
		foreach (MPGameMember gameMember in gameMembers)
		{
			if (!gameMember.isSelf && gameMember.steamID > 1000 && !gameMember.skirmishAI)
			{
				SendGameData(gameMember, dataToSend, instantMessage);
			}
		}
	}

	public void SendGamePacketToPlayerID(int playerID, byte[] gameData, int len, int offset = 0)
	{
		MPData mPData = new MPData();
		mPData.data = gameData;
		mPData.dataLength = len;
		mPData.dataOffset = offset;
		mPData.packetType = 1;
		SendPacketToPlayerID(playerID, mPData);
	}

	public void SendPacketToPlayerID(int playerID, MPData data)
	{
		if (gameMembers == null)
		{
			return;
		}
		byte[] dataToSend = data.ToBytes();
		foreach (MPGameMember gameMember in gameMembers)
		{
			if (!gameMember.isSelf && gameMember.playerID == playerID && gameMember.steamID > 1000 && !gameMember.skirmishAI)
			{
				SendGameData(gameMember, dataToSend);
			}
		}
	}

	public void SendPacketToPlayers(List<int> recipients, MPData data)
	{
		byte[] dataToSend = data.ToBytes();
		if (gameMembers == null)
		{
			return;
		}
		foreach (MPGameMember gameMember in gameMembers)
		{
			if (!gameMember.isSelf && recipients.Contains(gameMember.playerID) && gameMember.steamID > 1000 && !gameMember.skirmishAI)
			{
				SendGameData(gameMember, dataToSend);
			}
		}
	}

	public bool remapMPGameMembers()
	{
		if (gameMembers == null)
		{
			return false;
		}
		MPGameMember[] array = new MPGameMember[9];
		foreach (MPGameMember gameMember in gameMembers)
		{
			array[gameMember.playerID] = gameMember;
		}
		int num = localPlayerID;
		bool[] array2 = new bool[9];
		for (int i = 1; i < 9; i++)
		{
			array2[i] = loadPlayerRemapping[i] >= 1000;
		}
		for (int j = 1; j < 9; j++)
		{
			if (loadPlayerRemapping[j] >= 1000)
			{
				MPGameMember mPGameMember = new MPGameMember();
				mPGameMember.colourID = SpriteMapping.defaultRemapColours[j];
				mPGameMember.playerID = loadPlayerRemapping[j];
				mPGameMember.playerName = MPLobbyMember.GetName((loadPlayerRemapping[j] - 1000) / 8, loadPlayerRemapping[j] % 8);
				mPGameMember.skirmishAI = true;
				gameMembers.Add(mPGameMember);
			}
			else if (j != loadPlayerRemapping[j])
			{
				if (j == num)
				{
					GameData.Instance.playerID = (localPlayerID = loadPlayerRemapping[j]);
				}
				MPGameMember mPGameMember2 = array[j];
				if (mPGameMember2 != null)
				{
					mPGameMember2.playerID = loadPlayerRemapping[j];
				}
			}
		}
		if (localPlayerID < 0 || localPlayerID > 8)
		{
			return false;
		}
		EngineInterface.RemapPlayers(loadPlayerRemapping, localPlayerID);
		for (int k = 1; k < 9; k++)
		{
			if (array2[k])
			{
				loadPlayerRemapping[k] = k;
			}
		}
		EditorDirector.instance.SetLocalPlayer(localPlayerID);
		MainViewModel.Instance.UpdateUITroopSprites(UIColourRemap[SpriteMapping.remapColours[localPlayerID]], lastRetData.arabicLord > 0);
		return true;
	}

	public void monitorHostGameStart()
	{
		if (!monitoringForGameStart)
		{
			return;
		}
		if ((DateTime.UtcNow - monitoringForGameStartTime).TotalSeconds > 60.0)
		{
			Debug.Log((object)"Game initialization failed");
			monitoringForGameStart = false;
			LeaveLobby();
			return;
		}
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (!gameMember.acknowledged && !gameMember.isHost && gameMember.steamID > 1000 && !gameMember.skirmishAI)
				{
					return;
				}
			}
		}
		monitoringForGameStart = false;
		LeaveLobby();
		if (gameMembers == null)
		{
			return;
		}
		if (!newGameNotLoad)
		{
			MPData mPData = new MPData();
			mPData.packetType = 4;
			mPData.dataLength = 36;
			mPData.data = new byte[mPData.dataLength * 4];
			for (int i = 0; i < 9; i++)
			{
				byte[] bytes = BitConverter.GetBytes(loadPlayerRemapping[i]);
				for (int j = 0; j < 4; j++)
				{
					mPData.data[i * 4 + j] = bytes[j];
				}
			}
			if (!remapMPGameMembers())
			{
				FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
				return;
			}
			SendPacketToAll(mPData);
		}
		else
		{
			SendEmptyPacketTypeToAll(Enums.MPFlags.StartGamePacket);
		}
		Director.instance.startSimThread();
		EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep);
		if (ConfigSettings.Settings_ShowPings)
		{
			OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_PINGS, 1);
		}
		EngineInterface.StartMultiplayerGameSynced();
		Director.instance.DelayHideConnectionScreen();
		MPGameActive = true;
	}

	public void PauseEngine(bool state)
	{
		if (connectionPauseEngineState == state)
		{
			return;
		}
		connectionPauseEngineState = state;
		if (state || gameMembers == null)
		{
			return;
		}
		foreach (MPGameMember gameMember in gameMembers)
		{
			gameMember.pendingKick = false;
		}
	}

	public void monitorForLostPlayers()
	{
		try
		{
			if (GameData.scenario.InGameoverSituation)
			{
				return;
			}
			if (GameData.Instance.lastGameState != null && gameMembers != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (GameData.Instance.lastGameState.mpkick[i] <= 0)
					{
						continue;
					}
					int num = i + 1;
					foreach (MPGameMember gameMember in gameMembers)
					{
						if (gameMember.playerID == num && !gameMember.kicked && gameMember.steamID > 1000 && !gameMember.skirmishAI)
						{
							kickPlayerFromGame(gameMember, forceKickFromHost: true);
						}
					}
				}
			}
			if (connectionPauseEngineState && gameMembers != null)
			{
				if (connectionPauseReasonLostConnection)
				{
					if (!(DateTime.UtcNow > connectionCheckTime))
					{
						return;
					}
					connectionCheckTime = DateTime.UtcNow.AddSeconds(1.0);
					if (MonitorNetworkConnectivity())
					{
						PauseEngine(state: false);
						resyncingOrSavingResumeTime = DateTime.UtcNow.AddSeconds(5.0);
						MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 45), 0, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 47));
						MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError("", kickNotLeave: false, -1);
					}
					else
					{
						if (!((DateTime.UtcNow - connectionPauseStartTime).TotalSeconds > 15.0))
						{
							return;
						}
						foreach (MPGameMember gameMember2 in gameMembers)
						{
							if (gameMember2.playerID != localPlayerID && !gameMember2.kicked && gameMember2.steamID > 1000 && !gameMember2.skirmishAI)
							{
								gameMember2.kicked = true;
								EngineInterface.KickMPPlayer(gameMember2.playerID, kickImmediate: true);
							}
						}
						PauseEngine(state: false);
						MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 45), 0, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 48));
						MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError("", kickNotLeave: false, -1);
					}
					return;
				}
				if (DateTime.UtcNow > connectionCheckTime)
				{
					connectionCheckTime = DateTime.UtcNow.AddSeconds(2.0);
					if (!MonitorNetworkConnectivity())
					{
						MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 45), 0, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 44));
						connectionPauseReasonLostConnection = true;
						MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 44), kickNotLeave: false, -1);
						return;
					}
				}
				DateTime dateTime = DateTime.UtcNow.AddSeconds(-5.0);
				int num2 = 0;
				foreach (MPGameMember gameMember3 in gameMembers)
				{
					if (!gameMember3.isSelf && !gameMember3.kicked && gameMember3.steamID > 1000 && !gameMember3.skirmishAI && gameMember3.lastTimePacketRecieved != DateTime.MaxValue && gameMember3.lastTimePacketRecieved < dateTime)
					{
						num2++;
					}
				}
				if (num2 == 0)
				{
					PauseEngine(state: false);
					resyncingOrSavingResumeTime = DateTime.UtcNow.AddSeconds(5.0);
					MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError("", kickNotLeave: false, -1);
				}
				else
				{
					if (!((DateTime.UtcNow - connectionPauseStartTime).TotalSeconds > 15.0))
					{
						return;
					}
					foreach (MPGameMember gameMember4 in gameMembers)
					{
						if (!gameMember4.isSelf && !gameMember4.kicked && gameMember4.steamID > 1000 && !gameMember4.skirmishAI && gameMember4.lastTimePacketRecieved != DateTime.MaxValue && gameMember4.lastTimePacketRecieved < dateTime)
						{
							MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 45), 0, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 49) + " : " + gameMember4.playerName);
							kickPlayerFromGame(gameMember4);
						}
					}
					PauseEngine(state: false);
					resyncingOrSavingResumeTime = DateTime.UtcNow.AddSeconds(5.0);
					MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError("", kickNotLeave: false, -1);
				}
			}
			else
			{
				if (gameMembers == null || resyncingOrSaving || (!(resyncingOrSavingResumeTime == DateTime.MinValue) && !(DateTime.UtcNow > resyncingOrSavingResumeTime)))
				{
					return;
				}
				if (DateTime.UtcNow > connectionCheckTime)
				{
					connectionCheckTime = DateTime.UtcNow.AddSeconds(2.0);
					if (!MonitorNetworkConnectivity())
					{
						MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 45), 0, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 44));
						connectionPauseReasonLostConnection = true;
						connectionPauseStartTime = DateTime.UtcNow;
						PauseEngine(state: true);
						MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 44), kickNotLeave: false, -1);
					}
				}
				resyncingOrSavingResumeTime = DateTime.MinValue;
				DateTime dateTime2 = DateTime.UtcNow.AddSeconds(-5.0);
				{
					foreach (MPGameMember gameMember5 in gameMembers)
					{
						if (!gameMember5.isSelf && !gameMember5.kicked && gameMember5.steamID > 1000 && !gameMember5.skirmishAI && gameMember5.lastTimePacketRecieved != DateTime.MaxValue && gameMember5.lastTimePacketRecieved < dateTime2)
						{
							connectionPauseReasonLostConnection = false;
							connectionPauseStartTime = DateTime.UtcNow;
							PauseEngine(state: true);
							MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 45), 0, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 46) + " : " + gameMember5.playerName);
							MainViewModel.Instance.HUDMPConnectionIssue.ShowMultiplayerConnectionError(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 46) + " : " + gameMember5.playerName, kickNotLeave: true, gameMember5.playerID);
						}
					}
					return;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public void kickPlayerFromGame(int playerID)
	{
		MPGameMember player = getPlayer(playerID);
		if (player != null)
		{
			kickPlayerFromGame(player);
		}
	}

	public void kickPlayerFromGame(MPGameMember kickMember, bool forceKickFromHost = false)
	{
		if (!(!kickMember.pendingKick || forceKickFromHost))
		{
			return;
		}
		kickMember.pendingKick = true;
		int num = countOtherPlayers(kickMember);
		if (num == 0)
		{
			kickMember.kicked = true;
			EngineInterface.KickMPPlayer(kickMember.playerID, kickImmediate: true);
			MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(kickMember.playerName, kickMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 21));
			return;
		}
		if (!forceKickFromHost)
		{
			byte[] dataToSend = new MPData
			{
				packetType = 7,
				dataLength = 4,
				data = BitConverter.GetBytes(kickMember.playerID)
			}.ToBytes();
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (!gameMember.isSelf && gameMember.steamID > 1000 && !gameMember.skirmishAI)
				{
					SendGameData(gameMember, dataToSend);
				}
			}
		}
		if (forceKickFromHost || kickMember.DoVoteKick(localPlayerID, num))
		{
			if (kickMember.isHost)
			{
				promoteNewHost(kickMember);
			}
			kickMember.kickCounter++;
			kickMember.kicked = true;
			EngineInterface.KickMPPlayer(kickMember.playerID, kickImmediate: false);
			MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(kickMember.playerName, kickMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 21));
			if (kickMember.playerID == localPlayerID)
			{
				exitMP();
				EditorDirector.instance.stopGameSim();
				MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
				MainViewModel.Instance.FrontEndMenu.ShowMPConnectionPopup();
			}
		}
	}

	public void promoteNewHost(MPGameMember kickMember)
	{
		if (!kickMember.isHost || gameMembers == null)
		{
			return;
		}
		foreach (MPGameMember gameMember in gameMembers)
		{
			if (gameMember.playerID != kickMember.playerID && !gameMember.kicked && !gameMember.pendingKick && gameMember.steamID > 1000 && !gameMember.skirmishAI)
			{
				gameMember.isHost = true;
				EngineInterface.PromoteMPHost(gameMember.playerID);
				if (gameMember.isSelf)
				{
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(gameMember.playerName, gameMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 60));
				}
				else
				{
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(gameMember.playerName, gameMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 22));
				}
				break;
			}
		}
	}

	public int countOtherPlayers(MPGameMember otherMember)
	{
		int num = 0;
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (!gameMember.isSelf && gameMember.playerID != otherMember.playerID && !gameMember.kicked && !gameMember.skirmishAI)
				{
					num++;
				}
			}
		}
		return num;
	}

	public string getPlayerName(int playerID, bool activeOnly = false, bool excludeSkirmish = false)
	{
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (gameMember.playerID == playerID)
				{
					if ((!excludeSkirmish || !gameMember.skirmishAI) && (!activeOnly || !gameMember.kicked))
					{
						return gameMember.playerName;
					}
					break;
				}
			}
		}
		else if (playerID == GameData.Instance.playerID)
		{
			return ConfigSettings.Settings_UserName;
		}
		return "";
	}

	public string getSkirmishName(int this_player, bool activeOnly = false)
	{
		if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.is_skirmish_player(this_player))
		{
			return OnScreenText.getComputerName(GameData.Instance.lastGameState.computer_register[this_player], GameData.Instance.lastGameState.computer_names[this_player]);
		}
		return getPlayerName(this_player, activeOnly);
	}

	public MPGameMember getPlayer(int playerID)
	{
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (gameMember.playerID == playerID)
				{
					return gameMember;
				}
			}
		}
		return null;
	}

	public bool IsGameMemberHost()
	{
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (gameMember.isSelf && gameMember.isHost)
				{
					return true;
				}
			}
		}
		return false;
	}

	public void createLoadedSkirmishMembers(int[] colourMapping)
	{
		gameMembers = new List<MPGameMember>();
		for (int i = 0; i < 8; i++)
		{
			MPGameMember mPGameMember = new MPGameMember();
			mPGameMember.colourID = colourMapping[i + 1];
			mPGameMember.playerID = i + 1;
			if (i == 0)
			{
				mPGameMember.playerName = ConfigSettings.Settings_UserName;
			}
			gameMembers.Add(mPGameMember);
		}
	}

	public int getPlayerColour(int playerID)
	{
		return getPlayer(playerID)?.colourID ?? 1;
	}

	public int GetNumActivePlayers()
	{
		int num = 0;
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (!gameMember.kicked)
				{
					num++;
				}
			}
		}
		return num;
	}

	public void LeaveGame()
	{
		MPData mPData = new MPData();
		mPData.packetType = 8;
		mPData.dataLength = 0;
		SendPacketToAll(mPData, instantMessage: true);
	}

	public void SendChores(byte[] choreBuffer)
	{
		if (!MPGameActive)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		while (true)
		{
			num2++;
			if (num2 > 10000)
			{
				break;
			}
			int num3 = BitConverter.ToInt32(choreBuffer, num);
			if (num3 < 0)
			{
				break;
			}
			bool flag = true;
			switch (choreBuffer[num + 5])
			{
			case 54:
				resyncing = (resyncingOrSaving = true);
				resyncingStart = DateTime.UtcNow;
				resyncingCurrentSection = 0;
				resyncingCurrentLayer = 0;
				break;
			case 67:
				resyncing = (resyncingOrSaving = false);
				resyncingOrSavingResumeTime = DateTime.UtcNow.AddSeconds(5.0);
				break;
			case 39:
				resyncingOrSaving = true;
				MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(getPlayerName(localPlayerID), localPlayerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 32));
				break;
			case 94:
				resyncingOrSaving = false;
				resyncingOrSavingResumeTime = DateTime.UtcNow.AddSeconds(5.0);
				break;
			case 56:
			case 57:
			case 58:
			case 59:
			case 60:
			case 61:
			case 62:
			case 80:
			case 81:
			case 82:
			case 84:
			case 114:
			case 115:
				resyncingCurrentSection = choreBuffer[num + 5];
				resyncingCurrentLayer = 0;
				break;
			case 63:
			case 64:
			case 65:
				resyncingCurrentSection = choreBuffer[num + 5];
				resyncingCurrentLayer = BitConverter.ToInt32(choreBuffer, num + 6 + 3 + 4);
				break;
			}
			if (flag)
			{
				int num4 = choreBuffer[num + 4];
				if (num4 == 0)
				{
					SendGamePacketToAll(choreBuffer, num3, num + 4 + 1);
				}
				else
				{
					SendGamePacketToPlayerID(num4, choreBuffer, num3, num + 4 + 1);
				}
			}
			num += num3 + 4 + 1;
		}
	}

	public static byte[] Compress(byte[] data, int offset)
	{
		MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(data, 0, 1);
		using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionLevel.Optimal))
		{
			deflateStream.Write(data, offset, data.Length - offset);
		}
		return memoryStream.ToArray();
	}

	public static byte[] Decompress(byte[] data)
	{
		MemoryStream stream = new MemoryStream(data, 1, data.Length - 1);
		MemoryStream memoryStream = new MemoryStream();
		using (DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
		{
			deflateStream.CopyTo(memoryStream);
		}
		return memoryStream.ToArray();
	}

	public void SendIngameChat(List<int> recipients, string message)
	{
		if (!MPChatMuted)
		{
			MPData mPData = new MPData();
			mPData.packetType = 5;
			mPData.data = Encoding.UTF8.GetBytes(message);
			mPData.dataLength = -1;
			SendPacketToPlayers(recipients, mPData);
		}
	}

	public void SendIngameChatInsult(List<int> recipients, int insult)
	{
		if (!MPChatMuted)
		{
			MPData mPData = new MPData();
			mPData.packetType = 6;
			mPData.dataLength = 4;
			mPData.data = BitConverter.GetBytes(insult);
			SendPacketToPlayers(recipients, mPData);
		}
	}

	public void SetChatMute(int playerID, bool muted)
	{
		MPGameMember player = getPlayer(playerID);
		if (player != null)
		{
			player.muted = muted;
		}
	}

	public void ToggleChatMute(int playerID)
	{
		MPGameMember player = getPlayer(playerID);
		if (player != null)
		{
			player.muted = !player.muted;
		}
	}

	public bool IsChatMute(int playerID)
	{
		return getPlayer(playerID)?.muted ?? false;
	}

	public void SendCoopContinuationLobby(ulong lobbyID)
	{
		MPData mPData = new MPData();
		mPData.packetType = 10;
		mPData.dataLength = 8;
		mPData.data = BitConverter.GetBytes(lobbyID);
		SendPacketToAll(mPData);
	}

	public MPLobbyMember AddSkirmishPlayerLocal(int AILordType, int forcedTeam = -1, bool notRandom = false)
	{
		bool[] array = new bool[8];
		for (int i = 0; i < 8; i++)
		{
			array[i] = false;
		}
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.SkirmishMember && !member.SkirmishHumanMember && member.GetLordType() == AILordType)
			{
				array[member.GetLordSubType()] = true;
			}
		}
		int subType = 0;
		if (array[0])
		{
			List<int> list = new List<int>(0);
			for (int j = 0; j < 8; j++)
			{
				if (!array[j])
				{
					if (notRandom)
					{
						subType = j;
						break;
					}
					list.Add(j);
				}
			}
			if (list.Count > 0)
			{
				Random random = new Random();
				subType = list[random.Next(list.Count)];
			}
		}
		MPLobbyMember mPLobbyMember = new MPLobbyMember();
		List<int> usedColours = GetUsedColours(-1);
		bool[] array2 = new bool[9];
		foreach (int item in usedColours)
		{
			array2[item] = true;
		}
		for (int k = 1; k < 9; k++)
		{
			if (!array2[k])
			{
				mPLobbyMember.colourID = k;
				break;
			}
		}
		mPLobbyMember.SetSkirmishPlayer(AILordType, subType);
		if (forcedTeam < 0)
		{
			activeLobby.setTeam(mPLobbyMember, activeLobby.getFreeTeam());
		}
		else
		{
			activeLobby.setTeam(mPLobbyMember, forcedTeam);
		}
		activeLobby.members.Add(mPLobbyMember);
		activeLobby.numLobbyMembers = activeLobby.members.Count;
		return mPLobbyMember;
	}

	public MPLobbyMember AddCustomSkirmishPlayerLocal(CustomisationFileManager.CustomLord customLord, int forcedTeam = -1)
	{
		bool[] array = new bool[8];
		for (int i = 0; i < 8; i++)
		{
			array[i] = false;
		}
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.SkirmishMember && !member.SkirmishHumanMember && member.customLordName == customLord.lordName)
			{
				array[member.GetLordSubType()] = true;
			}
		}
		int subType = 0;
		if (array[0])
		{
			List<int> list = new List<int>(0);
			for (int j = 0; j < 8; j++)
			{
				if (!array[j])
				{
					list.Add(j);
				}
			}
			if (list.Count > 0)
			{
				Random random = new Random();
				subType = list[random.Next(list.Count)];
			}
		}
		MPLobbyMember mPLobbyMember = new MPLobbyMember();
		List<int> usedColours = GetUsedColours(-1);
		bool[] array2 = new bool[9];
		foreach (int item in usedColours)
		{
			array2[item] = true;
		}
		for (int k = 1; k < 9; k++)
		{
			if (!array2[k])
			{
				mPLobbyMember.colourID = k;
				break;
			}
		}
		mPLobbyMember.SetCustmSkirmishPlayerTemp(customLord.lordName, subType);
		if (forcedTeam < 0)
		{
			activeLobby.setTeam(mPLobbyMember, activeLobby.getFreeTeam());
		}
		else
		{
			activeLobby.setTeam(mPLobbyMember, forcedTeam);
		}
		activeLobby.members.Add(mPLobbyMember);
		activeLobby.numLobbyMembers = activeLobby.members.Count;
		return mPLobbyMember;
	}

	public void kickSkirmishPlayer(ulong steamIDToKick)
	{
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id.m_SteamID == steamIDToKick)
			{
				activeLobby.members.Remove(member);
				break;
			}
		}
		activeLobby.numLobbyMembers = activeLobby.members.Count;
	}

	public ulong GetCoopPartnerID()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		ulong steamID = SteamUser.GetSteamID().m_SteamID;
		if (activeLobby != null && activeLobby.members != null)
		{
			foreach (MPLobbyMember member in activeLobby.members)
			{
				if (member.GetSteamID() != steamID && !member.SkirmishMember)
				{
					return member.GetSteamID();
				}
			}
		}
		return 0uL;
	}

	public ulong CoopPartnerID()
	{
		return coopPartnerID;
	}

	public void Initialise()
	{
		SteamNetworkingUtils.InitRelayNetworkAccess();
	}

	public void init()
	{
		initCommon();
		MPGameActive = false;
		mapLoaded = false;
		seedReceived = false;
		autoJoinLobby = null;
		mapSendQueue.Clear();
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (gameMember.stillWithSteamConnection)
				{
					SteamNetworkingMessages.CloseSessionWithUser(ref gameMember.SNI);
					gameMember.stillWithSteamConnection = false;
				}
			}
		}
		gameMembers = null;
		IsHost = false;
		monitoringForGameStart = false;
		resyncingOrSaving = false;
		resyncing = false;
		resyncingOrSavingResumeTime = DateTime.MinValue;
	}

	public void initFast()
	{
		initCommon();
		mapLoaded = false;
		seedReceived = false;
		autoJoinLobby = null;
		mapSendQueue.Clear();
		monitoringForGameStart = false;
		resyncingOrSaving = false;
		resyncing = false;
		resyncingOrSavingResumeTime = DateTime.MinValue;
	}

	public void initFastFollowOn()
	{
		MPGameActive = false;
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (gameMember.stillWithSteamConnection)
				{
					SteamNetworkingMessages.CloseSessionWithUser(ref gameMember.SNI);
					gameMember.stillWithSteamConnection = false;
				}
			}
		}
		gameMembers = null;
	}

	public void exitMP()
	{
		EndDataConnection();
		LeaveChat();
		numLobbies = 0u;
		if (lobbies != null)
		{
			lobbies.Clear();
		}
		activeLobby = null;
		IsHost = false;
		seedReceived = false;
		randSeedReceived = 0;
		mapLoaded = false;
		resyncing = (resyncingOrSaving = false);
		resyncingOrSavingResumeTime = DateTime.MinValue;
		monitoringForGameStart = false;
		localPlayerID = 0;
		init();
	}

	public void GetLobbies(int defaultMatchmaking, Action lobbyListCompletedelegate)
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		numLobbies = 0u;
		LobbyListPopulatedDelegate = lobbyListCompletedelegate;
		lastLobbyDataRefresh = DateTime.UtcNow;
		SteamMatchmaking.AddRequestLobbyListStringFilter("version", "23", (ELobbyComparison)0);
		switch (defaultMatchmaking)
		{
		case 1:
			SteamMatchmaking.AddRequestLobbyListDistanceFilter((ELobbyDistanceFilter)1);
			break;
		case 0:
			SteamMatchmaking.AddRequestLobbyListDistanceFilter((ELobbyDistanceFilter)0);
			break;
		case 2:
			SteamMatchmaking.AddRequestLobbyListDistanceFilter((ELobbyDistanceFilter)3);
			break;
		}
		SteamMatchmaking.AddRequestLobbyListResultCountFilter(500);
		SteamAPICall_t val = SteamMatchmaking.RequestLobbyList();
		CallResult<LobbyMatchList_t>.Create((APIDispatchDelegate<LobbyMatchList_t>)null).Set(val, (APIDispatchDelegate<LobbyMatchList_t>)RequestLobbyListResult);
	}

	public void RequestLobbyListResult(LobbyMatchList_t param, bool bIOFailure)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		numLobbies = param.m_nLobbiesMatching;
		List<MPLobby> list = new List<MPLobby>();
		for (int i = 0; i < numLobbies; i++)
		{
			CSteamID lobbyByIndex = SteamMatchmaking.GetLobbyByIndex(i);
			MPLobby item = new MPLobby
			{
				id = lobbyByIndex,
				numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(lobbyByIndex),
				gameName = SteamMatchmaking.GetLobbyData(lobbyByIndex, "name"),
				mapName = SteamMatchmaking.GetLobbyData(lobbyByIndex, "map"),
				mapFileName = SteamMatchmaking.GetLobbyData(lobbyByIndex, "mapfile"),
				maxPlayers = SteamMatchmaking.GetLobbyData(lobbyByIndex, "max"),
				AIPlayers = SteamMatchmaking.GetLobbyData(lobbyByIndex, "aiplayers"),
				gameTypeCoop = SteamMatchmaking.GetLobbyData(lobbyByIndex, "type"),
				settings = SteamMatchmaking.GetLobbyData(lobbyByIndex, "settings"),
				country = SteamMatchmaking.GetLobbyData(lobbyByIndex, "country"),
				crc = SteamMatchmaking.GetLobbyData(lobbyByIndex, "crc"),
				setTeams = SteamMatchmaking.GetLobbyData(lobbyByIndex, "teams"),
				isHost = false
			};
			string lobbyData = SteamMatchmaking.GetLobbyData(lobbyByIndex, "time");
			bool flag = true;
			if (lobbyData.Length == 0)
			{
				flag = false;
			}
			else
			{
				try
				{
					long num = long.Parse(lobbyData);
					long ticks = DateTime.UtcNow.AddHours(-1.0).Ticks;
					if (num < ticks)
					{
						flag = false;
					}
				}
				catch (Exception)
				{
					flag = false;
				}
			}
			if (SteamMatchmaking.GetLobbyData(lobbyByIndex, "start").Length > 0)
			{
				flag = false;
			}
			if (SteamMatchmaking.GetLobbyData(lobbyByIndex, "closed") == "0" && flag)
			{
				list.Add(item);
			}
		}
		lobbies = list;
		if (LobbyListPopulatedDelegate != null)
		{
			LobbyListPopulatedDelegate();
		}
	}

	public List<MPLobby> ReadLobbies()
	{
		return lobbies;
	}

	public MPLobby FindLobby(CSteamID id)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		foreach (MPLobby lobby in lobbies)
		{
			if (lobby.id == id)
			{
				return lobby;
			}
		}
		return null;
	}

	public void CreateLobby(string _gameName, string _mapName, string _mapFileName, int _maxPlayers, int _typeCoop, int _lobbyMode, string _settings, int _crc, Action lobbyCreatedDelegate, Action<string, string, int> lobbyChatDelegate, int _coopTrailGame = -1, bool clearGameMembers = true)
	{
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (clearGameMembers)
		{
			init();
		}
		else
		{
			initFast();
		}
		LobbyCreatedDelegate = lobbyCreatedDelegate;
		LobbyChatDelegate = lobbyChatDelegate;
		activeLobby = new MPLobby
		{
			numLobbyMembers = 1,
			gameName = _gameName,
			mapName = _mapName,
			mapFileName = _mapFileName,
			maxPlayers = _maxPlayers.ToString(),
			gameTypeCoop = _typeCoop.ToString(),
			settings = _settings,
			country = SteamUtils.GetIPCountry(),
			crc = _crc.ToString(),
			isHost = true
		};
		activeLobby.coopTrailGame = _coopTrailGame >= 0;
		activeLobby.coopTrailID = _coopTrailGame;
		lastLobbyDataRefresh = DateTime.MinValue;
		lastLobbyMode = _lobbyMode;
		ELobbyType val = (ELobbyType)2;
		switch (_lobbyMode)
		{
		case 2:
			val = (ELobbyType)1;
			break;
		case 4:
			val = (ELobbyType)0;
			break;
		}
		SteamAPICall_t val2 = SteamMatchmaking.CreateLobby(val, _maxPlayers);
		CallResult<LobbyCreated_t>.Create((APIDispatchDelegate<LobbyCreated_t>)null).Set(val2, (APIDispatchDelegate<LobbyCreated_t>)CreateLobbyResult);
	}

	public void CreateLobbyResult(LobbyCreated_t param, bool bIOFailure)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		if ((int)param.m_eResult == 1)
		{
			ulong ulSteamIDLobby = param.m_ulSteamIDLobby;
			CSteamID val = default(CSteamID);
			((CSteamID)(ref val))._002Ector(ulSteamIDLobby);
			activeLobby.id = val;
			CreateShareCode(ulSteamIDLobby, activeLobby.coopTrailGame, activeLobby.coopTrailID);
			SteamMatchmaking.SetLobbyData(val, "settings", activeLobby.settings);
			SteamMatchmaking.SetLobbyData(val, "name", activeLobby.gameName);
			SteamMatchmaking.SetLobbyData(val, "map", activeLobby.mapName);
			SteamMatchmaking.SetLobbyData(val, "mapFile", activeLobby.mapFileName);
			SteamMatchmaking.SetLobbyData(val, "max", activeLobby.maxPlayers);
			SteamMatchmaking.SetLobbyData(val, "aiplayers", "0");
			SteamMatchmaking.SetLobbyData(val, "type", activeLobby.gameTypeCoop);
			SteamMatchmaking.SetLobbyData(val, "crc", activeLobby.crc);
			SteamMatchmaking.SetLobbyData(val, "teams", activeLobby.setTeams);
			SteamMatchmaking.SetLobbyData(val, "time", DateTime.UtcNow.Ticks.ToString());
			SteamMatchmaking.SetLobbyData(val, "version", "23");
			SteamMatchmaking.SetLobbyData(val, "country", SteamUtils.GetIPCountry());
			SteamMatchmaking.SetLobbyData(val, "closed", "0");
			SteamMatchmaking.SetLobbyData(val, "start", "");
			if (activeLobby.coopTrailGame)
			{
				SteamMatchmaking.SetLobbyData(val, "cooptrail", activeLobby.coopTrailID.ToString());
			}
			else
			{
				SteamMatchmaking.SetLobbyData(val, "cooptrail", "-1");
			}
			if (lastLobbyMode == 2)
			{
				SteamFriends.ActivateGameOverlayInviteDialog(val);
			}
			SetPlayerColour(ConfigSettings.Settings_PlayerColour + 1);
			string text = ConfigSettings.Settings_LordType.ToString();
			if (!ConfigSettings.Settings_UseSteamAvatar)
			{
				text = text + "|" + ConfigSettings.getAvatar().ToString();
			}
			SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "lordtype", text);
			GetActiveLobbyMembers();
			string hostMemberOrder = activeLobby.getHostMemberOrder();
			SteamMatchmaking.SetLobbyData(val, "hostorder", hostMemberOrder);
			InitChat();
			InitDataConnection();
			EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
			if (LobbyCreatedDelegate != null)
			{
				LobbyCreatedDelegate();
			}
			flushGameMessages();
		}
	}

	public void ChangeLobbyType(int _lobbyMode)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		lastLobbyMode = _lobbyMode;
		ELobbyType val = (ELobbyType)2;
		if (_lobbyMode != 0)
		{
			val = (ELobbyType)0;
		}
		SteamMatchmaking.SetLobbyType(activeLobby.id, val);
	}

	public void InviteOverlay()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null)
		{
			SteamFriends.ActivateGameOverlayInviteDialog(activeLobby.id);
		}
	}

	public void SetInviteLobbyID(ulong id)
	{
		inviteLobbyID = id;
	}

	public void ResumeCoop()
	{
		inviteLobbyID = CoopContinuationLobbyID;
		ResumeInvite();
	}

	public void HandleCommandline()
	{
		if (SteamManager.Initialized)
		{
			string text = default(string);
			if (SteamApps.GetLaunchCommandLine(ref text, 260) > 0)
			{
				string[] array = text.Split(' ', StringSplitOptions.None);
				if (text.Length == 0)
				{
					array = Environment.GetCommandLineArgs();
				}
				if (array != null && array.Length != 0)
				{
					for (int i = 0; i < array.Length; i++)
					{
						string text2 = array[i];
						if (text2.ToLowerInvariant() == "+connect_lobby" && i + 1 < array.Length)
						{
							inviteLobbyID = EditorDirector.getuLongFromString(array[i + 1]);
							PendingMPLobby = true;
							break;
						}
						if (text2.ToLowerInvariant() == "-skipvid")
						{
							IntroSequence.forceSkipIntro = true;
						}
					}
				}
			}
			m_JoinLobbyRequested = Callback<GameLobbyJoinRequested_t>.Create((DispatchDelegate<GameLobbyJoinRequested_t>)OnJoinLobbyRequested);
		}
		else
		{
			Debug.Log((object)"Steam Not Initialised for command line read");
		}
	}

	public void OnJoinLobbyRequested(GameLobbyJoinRequested_t param)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		inviteLobbyID = param.m_steamIDLobby.m_SteamID;
		ResumeInvite();
	}

	public void ResumeInvite()
	{
		SFXManager.instance.init2();
		Avatars.InitAvatars();
		if (FatControler.currentScene == Enums.SceneIDS.FrontEnd)
		{
			PendingMPLobby = true;
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");
		}
		else if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
		{
			PendingMPLobby = true;
			MainViewModel.Instance.Intro_Sequence.ForceStopVideo();
			FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");
		}
		else if (!Director.instance.SimRunning)
		{
			PendingMPLobby = true;
			MainViewModel.Instance.Show_HUD_MissionOver = false;
			FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
			MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");
		}
		else
		{
			MainViewModel.Instance.HUDMPInviteWarning.ShowInviteWarning();
		}
	}

	public void AutoJoinPendingLobby(ref MPLobby joiningLobby, Action lobbyJoinedDelegate, Action<string, string, int> lobbyChatDelegate)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		CSteamID val = default(CSteamID);
		((CSteamID)(ref val))._002Ector(inviteLobbyID);
		MPLobby mPLobby = new MPLobby
		{
			id = val,
			numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(val),
			gameName = SteamMatchmaking.GetLobbyData(val, "name"),
			mapName = SteamMatchmaking.GetLobbyData(val, "map"),
			mapFileName = SteamMatchmaking.GetLobbyData(val, "mapfile"),
			maxPlayers = SteamMatchmaking.GetLobbyData(val, "max"),
			AIPlayers = SteamMatchmaking.GetLobbyData(val, "aiplayers"),
			gameTypeCoop = SteamMatchmaking.GetLobbyData(val, "type"),
			settings = SteamMatchmaking.GetLobbyData(val, "settings"),
			country = SteamMatchmaking.GetLobbyData(val, "country"),
			crc = SteamMatchmaking.GetLobbyData(val, "crc"),
			setTeams = SteamMatchmaking.GetLobbyData(val, "teams"),
			isHost = false
		};
		mPLobby.coopTrailGameSetup(SteamMatchmaking.GetLobbyData(val, "cooptrail"));
		lobbies.Add(mPLobby);
		autoJoinLobby = (joiningLobby = mPLobby);
		JoinLobby(mPLobby, lobbyJoinedDelegate, lobbyChatDelegate, keepAutoJoinLobby: true);
	}

	public void JoinLobby(MPLobby lobbyToJoin, Action lobbyJoinedDelegate, Action<string, string, int> lobbyChatDelegate, bool keepAutoJoinLobby = false)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		MPLobby mPLobby = autoJoinLobby;
		init();
		if (keepAutoJoinLobby)
		{
			autoJoinLobby = mPLobby;
		}
		if (lobbyToJoin != null)
		{
			_ = lobbyToJoin.id;
			lobbyJoiningID = lobbyToJoin.id;
			LobbyJoinedDelegate = lobbyJoinedDelegate;
			LobbyChatDelegate = lobbyChatDelegate;
			SteamAPICall_t val = SteamMatchmaking.JoinLobby(lobbyToJoin.id);
			CallResult<LobbyEnter_t>.Create((APIDispatchDelegate<LobbyEnter_t>)null).Set(val, (APIDispatchDelegate<LobbyEnter_t>)JoinLobbyResult);
		}
	}

	public void JoinLobbyResult(LobbyEnter_t param, bool bIOFailure)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		ulong ulSteamIDLobby = param.m_ulSteamIDLobby;
		CSteamID val = default(CSteamID);
		((CSteamID)(ref val))._002Ector(ulSteamIDLobby);
		if (val == lobbyJoiningID)
		{
			MPLobby mPLobby = ((autoJoinLobby == null || !(autoJoinLobby.id == val)) ? FindLobby(val) : autoJoinLobby);
			if ("23" == SteamMatchmaking.GetLobbyData(val, "version"))
			{
				if (mPLobby != null)
				{
					if (mPLobby.maxPlayers == "")
					{
						mPLobby.gameName = SteamMatchmaking.GetLobbyData(val, "name");
						mPLobby.mapName = SteamMatchmaking.GetLobbyData(val, "map");
						mPLobby.mapFileName = SteamMatchmaking.GetLobbyData(val, "mapfile");
						mPLobby.maxPlayers = SteamMatchmaking.GetLobbyData(val, "max");
						mPLobby.AIPlayers = SteamMatchmaking.GetLobbyData(val, "aiplayers");
						mPLobby.gameTypeCoop = SteamMatchmaking.GetLobbyData(val, "type");
						mPLobby.settings = SteamMatchmaking.GetLobbyData(val, "settings");
						mPLobby.country = SteamMatchmaking.GetLobbyData(val, "country");
						mPLobby.crc = SteamMatchmaking.GetLobbyData(val, "crc");
						mPLobby.setTeams = SteamMatchmaking.GetLobbyData(val, "teams");
						mPLobby.coopTrailGameSetup(SteamMatchmaking.GetLobbyData(val, "cooptrail"));
					}
					CreateShareCode(ulSteamIDLobby, mPLobby.coopTrailGame, mPLobby.coopTrailID);
					mPLobby.numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(val);
					bool flag = false;
					for (int i = 0; i < mPLobby.numLobbyMembers; i++)
					{
						if (SteamMatchmaking.GetLobbyMemberByIndex(val, i) == SteamUser.GetSteamID())
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						activeLobby = null;
						if (LobbyJoinedDelegate != null)
						{
							LobbyJoinedDelegate();
						}
						return;
					}
				}
				activeLobby = mPLobby;
				SetPlayerColour(ConfigSettings.Settings_PlayerColour + 1);
				string text = ConfigSettings.Settings_LordType.ToString();
				if (!ConfigSettings.Settings_UseSteamAvatar)
				{
					text = text + "|" + ConfigSettings.getAvatar().ToString();
				}
				SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "lordtype", text);
				GetActiveLobbyMembers();
				InitChat();
				InitDataConnection();
				flushGameMessages();
				EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
				if (mPLobby != null)
				{
					if (LobbyJoinedDelegate != null)
					{
						LobbyJoinedDelegate();
					}
				}
				else
				{
					activeLobby = null;
				}
			}
			else if (mPLobby != null)
			{
				activeLobby = mPLobby;
				LeaveLobby();
			}
		}
		else
		{
			activeLobby = null;
			if (LobbyJoinedDelegate != null)
			{
				LobbyJoinedDelegate();
			}
		}
	}

	public MPLobby GetActiveLobby()
	{
		return activeLobby;
	}

	public void SetActiveLobby(MPLobby lobby)
	{
		activeLobby = lobby;
	}

	public void LeaveLobby(bool startGame = false)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null)
		{
			_ = activeLobby.id;
			if (activeLobby.isHost)
			{
				SteamMatchmaking.SetLobbyData(activeLobby.id, "closed", "1");
			}
			LeaveChat();
			SteamMatchmaking.LeaveLobby(activeLobby.id);
			activeLobby = null;
			EndDataConnection();
		}
	}

	public void UpdateHostLobbyInfo(string _gameName, string _mapName, string _mapFileName, int _maxPlayers, int _typeCoop, string _settings, int _crc, FRONT_Multiplayer.MPAIVInfo[] AIVs)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0264: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null)
		{
			return;
		}
		_ = activeLobby.id;
		if (activeLobby.isHost)
		{
			activeLobby.gameName = _gameName;
			activeLobby.mapName = _mapName;
			activeLobby.mapFileName = _mapFileName;
			activeLobby.maxPlayers = _maxPlayers.ToString();
			activeLobby.gameTypeCoop = _typeCoop.ToString();
			activeLobby.settings = _settings;
			activeLobby.crc = _crc.ToString();
			activeLobby.AIVDataPlayer2 = AIVs[1].encode();
			activeLobby.AIVDataPlayer3 = AIVs[2].encode();
			activeLobby.AIVDataPlayer4 = AIVs[3].encode();
			activeLobby.AIVDataPlayer5 = AIVs[4].encode();
			activeLobby.AIVDataPlayer6 = AIVs[5].encode();
			activeLobby.AIVDataPlayer7 = AIVs[6].encode();
			activeLobby.AIVDataPlayer8 = AIVs[7].encode();
			SendCustomInfoToAll(force: false);
			int num = activeLobby.CountAIPlayers();
			SteamMatchmaking.SetLobbyData(activeLobby.id, "name", activeLobby.gameName);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "map", activeLobby.mapName);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "mapFile", activeLobby.mapFileName);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "max", activeLobby.maxPlayers);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "aiplayers", num.ToString());
			SteamMatchmaking.SetLobbyData(activeLobby.id, "type", activeLobby.gameTypeCoop);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "settings", activeLobby.settings);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "crc", activeLobby.crc);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "teams", activeLobby.setTeams);
			string hostMemberOrder = activeLobby.getHostMemberOrder();
			SteamMatchmaking.SetLobbyData(activeLobby.id, "hostorder", hostMemberOrder);
			int num2 = _maxPlayers - num;
			if (num2 < 2)
			{
				num2 = 2;
			}
			SteamMatchmaking.SetLobbyMemberLimit(activeLobby.id, num2);
		}
	}

	public void SetCoopTrailProgress(int trailID, int[] progress, int selectedMission, int fullProgress, bool orderswapped)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null)
		{
			return;
		}
		_ = activeLobby.id;
		if (activeLobby.isHost)
		{
			activeLobby.coopTrailID = trailID;
			activeLobby.coopSelectedMission = selectedMission;
			activeLobby.coopOrderSwapped = orderswapped;
			activeLobby.coopTrailFullProgress = fullProgress;
			string text = trailID + ",";
			for (int i = 0; i < 10; i++)
			{
				text += progress[i];
				text += ",";
			}
			text = ((!orderswapped) ? (text + selectedMission + "," + fullProgress + ",0") : (text + selectedMission + "," + fullProgress + ",1"));
			SteamMatchmaking.SetLobbyData(activeLobby.id, "cooptrail", text);
		}
	}

	public void HostStartGame()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null)
		{
			_ = activeLobby.id;
			if (activeLobby.isHost)
			{
				IsHost = true;
				SteamMatchmaking.SetLobbyData(activeLobby.id, "start", "GO!" + activeLobby.AIVDataChecksum());
			}
		}
	}

	public void HostLoadGame(string filename)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null)
		{
			return;
		}
		_ = activeLobby.id;
		if (activeLobby.isHost)
		{
			IsHost = true;
			for (int i = 1; i < 9; i++)
			{
				loadPlayerRemapping[i] = -1;
			}
			SteamMatchmaking.SetLobbyData(activeLobby.id, "start", filename);
		}
	}

	public unsafe bool GetActiveLobbyMembers(bool coopGame = false)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_020e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_051f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0686: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_028f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_024d: Unknown result type (might be due to invalid IL or missing references)
		//IL_033f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_039a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_03de: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0411: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null)
		{
			_ = activeLobby.id;
			bool result = false;
			List<MPLobbyMember> list = new List<MPLobbyMember>();
			int num = SteamMatchmaking.GetNumLobbyMembers(activeLobby.id);
			activeLobby.numLobbyMembers = num;
			if (coopGame && num > 2)
			{
				if (activeLobby.isHost)
				{
					for (int i = 2; i < num; i++)
					{
						CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(activeLobby.id, i);
						SteamMatchmaking.SetLobbyData(activeLobby.id, "kick", ((object)(*(CSteamID*)(&lobbyMemberByIndex))/*cast due to constrained. prefix*/).ToString());
					}
				}
				else
				{
					for (int j = 2; j < num; j++)
					{
						if (SteamMatchmaking.GetLobbyMemberByIndex(activeLobby.id, j) == SteamUser.GetSteamID())
						{
							LeaveLobby();
							return false;
						}
					}
				}
				num = 2;
			}
			for (int k = 0; k < num; k++)
			{
				CSteamID lobbyMemberByIndex2 = SteamMatchmaking.GetLobbyMemberByIndex(activeLobby.id, k);
				string friendPersonaName = SteamFriends.GetFriendPersonaName(lobbyMemberByIndex2);
				if (!(friendPersonaName != "") || !(friendPersonaName != "[unknown]"))
				{
					continue;
				}
				string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(activeLobby.id, lobbyMemberByIndex2, "ready");
				bool ready = false;
				if (lobbyMemberData != null)
				{
					ready = lobbyMemberData.Length > 0;
				}
				string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(activeLobby.id, lobbyMemberByIndex2, "map");
				string lobbyMemberData3 = SteamMatchmaking.GetLobbyMemberData(activeLobby.id, lobbyMemberByIndex2, "request");
				int intFromString = EditorDirector.getIntFromString(SteamMatchmaking.GetLobbyMemberData(activeLobby.id, lobbyMemberByIndex2, "colour"));
				string text = SteamMatchmaking.GetLobbyMemberData(activeLobby.id, lobbyMemberByIndex2, "lordtype");
				bool flag = false;
				string text2 = "";
				if (text != null && text.Contains("|"))
				{
					string[] array = text.Split("|", StringSplitOptions.None);
					text = array[0];
					text2 = array[1];
					tempAD.FromString(array[1]);
					flag = true;
					if (lobbyMemberByIndex2 != SteamUser.GetSteamID())
					{
						LastCoAString = text2;
					}
				}
				else if (lobbyMemberByIndex2 != SteamUser.GetSteamID())
				{
					LastCoAString = "";
				}
				if (text != null && text.Length > 0)
				{
					if (tempAD == null || !flag)
					{
						RequestUserAvatar(lobbyMemberByIndex2);
					}
					else
					{
						CreateCoAAvatar(lobbyMemberByIndex2, tempAD);
					}
				}
				int intFromString2 = EditorDirector.getIntFromString(text);
				int intFromString3 = EditorDirector.getIntFromString(lobbyMemberData2);
				bool flag2 = false;
				foreach (MPLobbyMember member in activeLobby.members)
				{
					if (!(member.id == lobbyMemberByIndex2))
					{
						continue;
					}
					member.Name = friendPersonaName;
					member.ready = ready;
					member.mapStatus = intFromString3;
					member.mapRequested = lobbyMemberData3;
					member.lordType = intFromString2;
					if (activeLobby.isHost && intFromString >= 0 && intFromString != member.colourID)
					{
						int memberColor = GetMemberColor(intFromString, lobbyMemberByIndex2);
						if (memberColor != member.colourID)
						{
							result = true;
						}
						member.colourID = memberColor;
					}
					list.Add(member);
					flag2 = true;
					break;
				}
				if (flag2)
				{
					continue;
				}
				MPLobbyMember mPLobbyMember = new MPLobbyMember
				{
					id = lobbyMemberByIndex2,
					Name = friendPersonaName,
					ready = ready,
					mapStatus = intFromString3,
					mapRequested = lobbyMemberData3,
					colourID = intFromString,
					lordType = intFromString2
				};
				if (activeLobby.isHost)
				{
					bool flag3 = mPLobbyMember.id == SteamMatchmaking.GetLobbyOwner(activeLobby.id);
					mPLobbyMember.colourID = GetMemberColor(intFromString, lobbyMemberByIndex2);
					if (activeLobby.gameTypeCoop == "1" && !flag3)
					{
						CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(activeLobby.id);
						int num2 = -1;
						foreach (MPLobbyMember member2 in activeLobby.members)
						{
							if (member2.id == lobbyOwner)
							{
								num2 = activeLobby.getTeam(member2);
								break;
							}
						}
						if (num2 != -1)
						{
							activeLobby.setTeam(mPLobbyMember, num2);
						}
						else
						{
							activeLobby.setTeam(mPLobbyMember, activeLobby.getFreeTeam());
						}
					}
					else
					{
						activeLobby.setTeam(mPLobbyMember, activeLobby.getFreeTeam());
					}
					if (!flag3)
					{
						result = true;
						SendCustomInfoToMember(mPLobbyMember);
					}
				}
				list.Add(mPLobbyMember);
			}
			foreach (MPLobbyMember member3 in activeLobby.members)
			{
				if (member3.id.m_SteamID < 1000)
				{
					list.Add(member3);
				}
			}
			if (!activeLobby.isHost)
			{
				List<ulong> hostMemberOrder = activeLobby.hostMemberOrder;
				ulong steamID = SteamUser.GetSteamID().m_SteamID;
				bool flag4 = false;
				foreach (ulong item in hostMemberOrder)
				{
					if (item == steamID)
					{
						flag4 = true;
						activeLobby.clientFound = true;
						break;
					}
				}
				if (flag4)
				{
					activeLobby.members.Clear();
					foreach (ulong item2 in hostMemberOrder)
					{
						foreach (MPLobbyMember item3 in list)
						{
							if (item3.id.m_SteamID == item2)
							{
								activeLobby.members.Add(item3);
								break;
							}
						}
					}
					activeLobby.numLobbyMembers = activeLobby.members.Count;
				}
				else
				{
					activeLobby.members = list;
					activeLobby.numLobbyMembers = list.Count;
				}
			}
			else
			{
				activeLobby.members = list;
				activeLobby.numLobbyMembers = list.Count;
			}
			if (activeLobby.isHost)
			{
				activeLobby.validateTeams();
				string hostMemberOrder2 = activeLobby.getHostMemberOrder();
				SteamMatchmaking.SetLobbyData(activeLobby.id, "hostorder", hostMemberOrder2);
			}
			return result;
		}
		return false;
	}

	public List<int> GetUsedColours(int ignoredColour)
	{
		List<int> list = new List<int>();
		if (activeLobby != null)
		{
			foreach (MPLobbyMember member in activeLobby.members)
			{
				if (member.colourID > 0 && member.colourID != ignoredColour)
				{
					list.Add(member.colourID);
				}
			}
		}
		return list;
	}

	public int GetMemberColor(int requestedColour, CSteamID memberID)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		bool[] array = new bool[9];
		for (int i = 0; i < 9; i++)
		{
			array[i] = false;
		}
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id != memberID && member.colourID > 0)
			{
				array[member.colourID] = true;
			}
		}
		if (requestedColour > 0 && requestedColour < 9 && !array[requestedColour])
		{
			return requestedColour;
		}
		for (int j = 1; j < 9; j++)
		{
			if (!array[j])
			{
				return j;
			}
		}
		return 1;
	}

	public void SetPlayerColour(int colourID)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null)
		{
			return;
		}
		SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "colour", colourID.ToString());
		CSteamID steamID = SteamUser.GetSteamID();
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id == steamID)
			{
				member.colourID = colourID;
				break;
			}
		}
	}

	public void SetMemberReadyState(bool state)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null)
		{
			return;
		}
		if (state)
		{
			SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "ready", "ready");
		}
		else
		{
			SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "ready", "");
		}
		CSteamID steamID = SteamUser.GetSteamID();
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id == steamID)
			{
				member.ready = state;
				break;
			}
		}
	}

	public void ClearAIsFromLobby()
	{
		List<MPLobbyMember> list = new List<MPLobbyMember>();
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.SkirmishMember && !member.SkirmishHumanMember)
			{
				list.Add(member);
			}
		}
		foreach (MPLobbyMember item in list)
		{
			activeLobby.members.Remove(item);
		}
	}

	public void KickMemberFromLobby(MPLobbyMember member)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null && activeLobby.isHost)
		{
			kickMemberTime = DateTime.UtcNow.AddSeconds(2.0);
			SteamMatchmaking.SetLobbyData(activeLobby.id, "kick", ((object)System.Runtime.CompilerServices.Unsafe.As<CSteamID, CSteamID>(ref member.id)/*cast due to constrained. prefix*/).ToString());
		}
	}

	public bool RefreshLobbyList(ref EngineInterface.MultiplayerSetupData MPSetupData, ref bool refreshTeams, ref bool settingsChanged, bool coopgame = false)
	{
		//IL_026a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0511: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0409: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_045b: Unknown result type (might be due to invalid IL or missing references)
		//IL_047f: Unknown result type (might be due to invalid IL or missing references)
		//IL_04cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_056f: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		refreshTeams = false;
		settingsChanged = false;
		if (lastLobbyDataRefresh != DateTime.MinValue && (DateTime.UtcNow - lastLobbyDataRefresh).TotalSeconds > 5.0)
		{
			lastLobbyDataRefresh = DateTime.UtcNow;
			List<MPLobby> list = new List<MPLobby>();
			foreach (MPLobby lobby in lobbies)
			{
				if (lobby.isHost || (activeLobby != null && !(lobby.id != activeLobby.id)))
				{
					continue;
				}
				if (SteamMatchmaking.RequestLobbyData(lobby.id))
				{
					lobby.numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(lobby.id);
					lobby.gameName = SteamMatchmaking.GetLobbyData(lobby.id, "name");
					lobby.mapName = SteamMatchmaking.GetLobbyData(lobby.id, "map");
					lobby.mapFileName = SteamMatchmaking.GetLobbyData(lobby.id, "mapFile");
					lobby.maxPlayers = SteamMatchmaking.GetLobbyData(lobby.id, "max");
					lobby.AIPlayers = SteamMatchmaking.GetLobbyData(lobby.id, "aiplayers");
					lobby.gameTypeCoop = SteamMatchmaking.GetLobbyData(lobby.id, "type");
					lobby.settings = SteamMatchmaking.GetLobbyData(lobby.id, "settings");
					lobby.country = SteamMatchmaking.GetLobbyData(lobby.id, "country");
					lobby.setTeams = SteamMatchmaking.GetLobbyData(lobby.id, "teams");
					lobby.crc = SteamMatchmaking.GetLobbyData(lobby.id, "crc");
					lobby.startGame = SteamMatchmaking.GetLobbyData(lobby.id, "start");
					lobby.coopTrailGameSetup(SteamMatchmaking.GetLobbyData(lobby.id, "cooptrail"));
					if (SteamMatchmaking.GetLobbyData(lobby.id, "closed") != "0")
					{
						list.Add(lobby);
					}
				}
				else
				{
					list.Add(lobby);
				}
			}
			foreach (MPLobby item in list)
			{
				lobbies.Remove(item);
			}
		}
		if (activeLobby != null && !activeLobby.isHost)
		{
			activeLobby.numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(activeLobby.id);
			activeLobby.gameName = SteamMatchmaking.GetLobbyData(activeLobby.id, "name");
			activeLobby.mapName = SteamMatchmaking.GetLobbyData(activeLobby.id, "map");
			activeLobby.mapFileName = SteamMatchmaking.GetLobbyData(activeLobby.id, "mapFile");
			activeLobby.maxPlayers = SteamMatchmaking.GetLobbyData(activeLobby.id, "max");
			activeLobby.AIPlayers = SteamMatchmaking.GetLobbyData(activeLobby.id, "aiplayers");
			activeLobby.gameTypeCoop = SteamMatchmaking.GetLobbyData(activeLobby.id, "type");
			activeLobby.settings = SteamMatchmaking.GetLobbyData(activeLobby.id, "settings");
			activeLobby.country = SteamMatchmaking.GetLobbyData(activeLobby.id, "country");
			activeLobby.crc = SteamMatchmaking.GetLobbyData(activeLobby.id, "crc");
			string setTeams = activeLobby.setTeams;
			activeLobby.setTeams = SteamMatchmaking.GetLobbyData(activeLobby.id, "teams");
			string setTeams2 = activeLobby.setTeams;
			if (setTeams != setTeams2)
			{
				refreshTeams = true;
			}
			activeLobby.startGame = SteamMatchmaking.GetLobbyData(activeLobby.id, "start");
			activeLobby.coopTrailGameSetup(SteamMatchmaking.GetLobbyData(activeLobby.id, "cooptrail"));
			if (SteamMatchmaking.GetLobbyData(activeLobby.id, "closed") != "0")
			{
				return true;
			}
			settingsChanged = MPSetupData.FromString(activeLobby.settings);
			string lobbyData = SteamMatchmaking.GetLobbyData(activeLobby.id, "hostorder");
			activeLobby.setHostMemberOrder(lobbyData);
			string lobbyData2 = SteamMatchmaking.GetLobbyData(activeLobby.id, "kick");
			if (lobbyData2.Length > 0)
			{
				ulong num = EditorDirector.getuLongFromString(lobbyData2);
				if (num == SteamUser.GetSteamID().m_SteamID)
				{
					return true;
				}
				if (num < 1000)
				{
					kickSkirmishPlayer(num);
				}
			}
			CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(activeLobby.id);
			if (lobbyOwner.m_SteamID == 0L || lobbyOwner == SteamUser.GetSteamID())
			{
				return true;
			}
		}
		else if (activeLobby != null && activeLobby.isHost)
		{
			activeLobby.startGame = SteamMatchmaking.GetLobbyData(activeLobby.id, "start");
		}
		if (activeLobby != null)
		{
			if (activeLobby.isHost && kickMemberTime != DateTime.MinValue && kickMemberTime < DateTime.UtcNow)
			{
				kickMemberTime = DateTime.MinValue;
				SteamMatchmaking.SetLobbyData(activeLobby.id, "kick", "");
			}
			refreshTeams = GetActiveLobbyMembers(coopgame) | refreshTeams;
			receiveLobbyMessages();
			ReceiveGameMessages();
		}
		return false;
	}

	public void InitChat()
	{
		if (IncomingMessage == null)
		{
			IncomingMessage = Callback<LobbyChatMsg_t>.Create((DispatchDelegate<LobbyChatMsg_t>)HandleIncomingMessage);
		}
	}

	public void LeaveChat()
	{
		if (IncomingMessage != null)
		{
			IncomingMessage.Dispose();
			IncomingMessage = null;
		}
	}

	public void HandleIncomingMessage(LobbyChatMsg_t callback)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = new byte[4096];
		int num = 4096;
		int iChatID = (int)callback.m_iChatID;
		CSteamID id = default(CSteamID);
		EChatEntryType val = default(EChatEntryType);
		int lobbyChatEntry = SteamMatchmaking.GetLobbyChatEntry(new CSteamID(callback.m_ulSteamIDLobby), iChatID, ref id, array, num, ref val);
		if (lobbyChatEntry > 0)
		{
			byte[] array2 = new byte[lobbyChatEntry];
			Array.Copy(array, array2, lobbyChatEntry);
			string message = Encoding.UTF8.GetString(array2);
			ChatHandle(message, id);
		}
	}

	public void ChatHandle(string message, CSteamID Id)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		int arg = -1;
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id == Id)
			{
				arg = member.colourID;
				break;
			}
		}
		string friendPersonaName = SteamFriends.GetFriendPersonaName(Id);
		if (LobbyChatDelegate != null)
		{
			LobbyChatDelegate(friendPersonaName, message, arg);
		}
	}

	public void SendLobbyChatMessage(string Message)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null)
		{
			CSteamID id = activeLobby.id;
			byte[] bytes = Encoding.UTF8.GetBytes(Message);
			SteamMatchmaking.SendLobbyChatMsg(id, bytes, bytes.Length);
		}
	}

	public void SetMapStatus(int status)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null && !activeLobby.isHost)
		{
			SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "map", status.ToString());
		}
	}

	public void InitDataConnection()
	{
		EndDataConnection();
		NetworkUserListener = Callback<SteamNetworkingMessagesSessionRequest_t>.Create((DispatchDelegate<SteamNetworkingMessagesSessionRequest_t>)HandleGameIncomingConnection);
	}

	public void EndDataConnection()
	{
		if (NetworkUserListener != null)
		{
			NetworkUserListener.Dispose();
			NetworkUserListener = null;
		}
	}

	public void SendCustomInfoToAll(bool force)
	{
		if (activeLobby == null || !activeLobby.isHost)
		{
			return;
		}
		ulong num = 0uL;
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (!member.IsSelf() && !member.SkirmishMember)
			{
				num += member.id.m_SteamID;
			}
		}
		if (!force && activeLobby.AIVDataPlayer2 == activeLobby.sentAIVDataPlayer2 && activeLobby.AIVDataPlayer3 == activeLobby.sentAIVDataPlayer3 && activeLobby.AIVDataPlayer4 == activeLobby.sentAIVDataPlayer4 && activeLobby.AIVDataPlayer5 == activeLobby.sentAIVDataPlayer5 && activeLobby.AIVDataPlayer6 == activeLobby.sentAIVDataPlayer6 && activeLobby.AIVDataPlayer7 == activeLobby.sentAIVDataPlayer7 && activeLobby.AIVDataPlayer8 == activeLobby.sentAIVDataPlayer8 && activeLobby.sentClientInfo == num)
		{
			return;
		}
		activeLobby.sentAIVDataPlayer2 = activeLobby.AIVDataPlayer2;
		activeLobby.sentAIVDataPlayer3 = activeLobby.AIVDataPlayer3;
		activeLobby.sentAIVDataPlayer4 = activeLobby.AIVDataPlayer4;
		activeLobby.sentAIVDataPlayer5 = activeLobby.AIVDataPlayer5;
		activeLobby.sentAIVDataPlayer6 = activeLobby.AIVDataPlayer6;
		activeLobby.sentAIVDataPlayer7 = activeLobby.AIVDataPlayer7;
		activeLobby.sentAIVDataPlayer8 = activeLobby.AIVDataPlayer8;
		activeLobby.sentClientInfo = num;
		byte[] data = EncodeAIVData();
		foreach (MPLobbyMember member2 in activeLobby.members)
		{
			if (!member2.IsSelf() && !member2.SkirmishMember)
			{
				SendCustomInfo(member2, data);
			}
		}
	}

	public void SendCustomInfoToMember(MPLobbyMember member)
	{
		if (activeLobby != null && activeLobby.isHost)
		{
			byte[] data = EncodeAIVData();
			if (!member.IsSelf() && !member.SkirmishMember)
			{
				SendCustomInfo(member, data);
			}
		}
	}

	public byte[] EncodeAIVData()
	{
		List<byte> list = new List<byte>();
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer2);
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer3);
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer4);
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer5);
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer6);
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer7);
		EncodeAIVDataAddString(list, activeLobby.AIVDataPlayer8);
		return list.ToArray();
	}

	public void EncodeAIVDataAddString(List<byte> dataList, string str)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(str);
		dataList.AddRange(BitConverter.GetBytes(bytes.Length));
		dataList.AddRange(bytes);
	}

	public void DecodeAIVData(byte[] data)
	{
		int offset = 0;
		activeLobby.AIVDataPlayer2 = DecodeAIVDataString(data, ref offset);
		activeLobby.AIVDataPlayer3 = DecodeAIVDataString(data, ref offset);
		activeLobby.AIVDataPlayer4 = DecodeAIVDataString(data, ref offset);
		activeLobby.AIVDataPlayer5 = DecodeAIVDataString(data, ref offset);
		activeLobby.AIVDataPlayer6 = DecodeAIVDataString(data, ref offset);
		activeLobby.AIVDataPlayer7 = DecodeAIVDataString(data, ref offset);
		activeLobby.AIVDataPlayer8 = DecodeAIVDataString(data, ref offset);
	}

	public string DecodeAIVDataString(byte[] data, ref int offset)
	{
		int num = BitConverter.ToInt32(data, offset);
		offset += 4;
		string result = Encoding.UTF8.GetString(data, offset, num);
		offset += num;
		return result;
	}

	public unsafe void SendCustomInfo(MPLobbyMember member, byte[] data)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Invalid comparison between Unknown and I4
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity val = default(SteamNetworkingIdentity);
		((SteamNetworkingIdentity)(ref val)).SetSteamID(member.id);
		int num = 40;
		fixed (byte* ptr = data)
		{
			IntPtr intPtr = (IntPtr)ptr;
			EResult val2 = SteamNetworkingMessages.SendMessageToUser(ref val, intPtr, (uint)data.Length, num, 6);
			if ((int)val2 != 1)
			{
				Debug.Log((object)val2);
			}
		}
	}

	public bool SendMap(MPLobbyMember member, string mapFileName, string fullPath, Action mapSendDelegate)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		string key = mapFileName.ToLower() + ":" + member.id.m_SteamID;
		if (!member.mapsSent.ContainsKey(key))
		{
			member.mapsSent[key] = true;
			MapSendDelegate = mapSendDelegate;
			byte[] mapData = File.ReadAllBytes(fullPath);
			CSteamID id = member.id;
			SendMapInternal(id, mapData, mapFileName);
			return true;
		}
		return false;
	}

	public byte[] createMapSendData(int mode, int crc, byte[] data)
	{
		byte[] array = new byte[data.Length + 8];
		Array.Copy(BitConverter.GetBytes(mode), 0, array, 0, 4);
		Array.Copy(BitConverter.GetBytes(crc), 0, array, 4, 4);
		if (data.Length != 0)
		{
			Array.Copy(data, 0, array, 8, data.Length);
		}
		return array;
	}

	public byte[] decodeMapData(byte[] message, ref int mode, ref int crc)
	{
		byte[] array = new byte[message.Length - 8];
		mode = BitConverter.ToInt32(message, 0);
		crc = BitConverter.ToInt32(message, 4);
		Array.Copy(message, 8, array, 0, array.Length);
		return array;
	}

	public unsafe void SendMapInternal(CSteamID targetID, byte[] mapData, string mapFileName)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Invalid comparison between Unknown and I4
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Invalid comparison between Unknown and I4
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity netID = default(SteamNetworkingIdentity);
		((SteamNetworkingIdentity)(ref netID)).SetSteamID(targetID);
		int num = 40;
		int num2 = (int)EngineInterface.crc(mapData);
		byte[] array = createMapSendData(0, num2, BitConverter.GetBytes(mapData.Length));
		fixed (byte* ptr = array)
		{
			IntPtr intPtr = (IntPtr)ptr;
			EResult val = SteamNetworkingMessages.SendMessageToUser(ref netID, intPtr, (uint)array.Length, num, 1);
			if ((int)val != 1)
			{
				Debug.Log((object)val);
			}
		}
		byte[] array2 = createMapSendData(1, num2, Encoding.UTF8.GetBytes(mapFileName));
		fixed (byte* ptr2 = array2)
		{
			IntPtr intPtr2 = (IntPtr)ptr2;
			EResult val2 = SteamNetworkingMessages.SendMessageToUser(ref netID, intPtr2, (uint)array2.Length, num, 1);
			if ((int)val2 != 1)
			{
				Debug.Log((object)val2);
			}
		}
		int num3;
		for (int i = 0; i < mapData.Length; i += num3)
		{
			MapSendQueueItem mapSendQueueItem = new MapSendQueueItem();
			mapSendQueueItem.NetID = netID;
			num3 = Math.Min(250000, mapData.Length - i);
			mapSendQueueItem.data = new byte[num3 + 8];
			Array.Copy(BitConverter.GetBytes(2), 0, mapSendQueueItem.data, 0, 4);
			Array.Copy(BitConverter.GetBytes(num2), 0, mapSendQueueItem.data, 4, 4);
			Array.Copy(mapData, i, mapSendQueueItem.data, 8, num3);
			if (lastMapSendQueueItemTime < DateTime.UtcNow)
			{
				mapSendQueueItem.sendTime = DateTime.UtcNow.AddSeconds(1.0);
			}
			else
			{
				mapSendQueueItem.sendTime = lastMapSendQueueItemTime.AddSeconds(1.0);
			}
			lastMapSendQueueItemTime = mapSendQueueItem.sendTime;
			mapSendQueue.Enqueue(mapSendQueueItem);
		}
	}

	public unsafe void ProcessMapSendQueue()
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Invalid comparison between Unknown and I4
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		if (mapSendQueue.Count <= 0)
		{
			return;
		}
		MapSendQueueItem mapSendQueueItem = mapSendQueue.Peek();
		if (mapSendQueueItem.sendTime < DateTime.UtcNow)
		{
			mapSendQueueItem = mapSendQueue.Dequeue();
			int num = 40;
			fixed (byte* data = mapSendQueueItem.data)
			{
				IntPtr intPtr = (IntPtr)data;
				EResult val = SteamNetworkingMessages.SendMessageToUser(ref mapSendQueueItem.NetID, intPtr, (uint)mapSendQueueItem.data.Length, num, 1);
				if ((int)val != 1)
				{
					Debug.Log((object)val);
				}
			}
		}
		if (mapSendQueue.Count == 0 && MapSendDelegate != null)
		{
			MapSendDelegate();
		}
	}

	public unsafe void SendSaveCRC(string mapName, int crcAndGameTime)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Invalid comparison between Unknown and I4
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		byte[] bytes = Encoding.UTF8.GetBytes(mapName);
		byte[] array = new byte[bytes.Length + 4];
		byte[] bytes2 = BitConverter.GetBytes(crcAndGameTime);
		Array.Copy(bytes, 0, array, 4, bytes.Length);
		Array.Copy(bytes2, 0, array, 0, 4);
		int num = 40;
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id.m_SteamID <= 1000 || !(member.id != SteamUser.GetSteamID()))
			{
				continue;
			}
			SteamNetworkingIdentity val = default(SteamNetworkingIdentity);
			((SteamNetworkingIdentity)(ref val)).SetSteamID(member.id);
			fixed (byte* ptr = array)
			{
				IntPtr intPtr = (IntPtr)ptr;
				EResult val2 = SteamNetworkingMessages.SendMessageToUser(ref val, intPtr, (uint)array.Length, num, 21);
				if ((int)val2 != 1)
				{
					Debug.Log((object)val2);
				}
			}
		}
	}

	public unsafe void HostSendLobbyPings()
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null)
		{
			return;
		}
		byte[] array = new byte[0];
		int num = 40;
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (member.id.m_SteamID <= 1000 || !(member.id != SteamUser.GetSteamID()) || !(member.pingSent == DateTime.MinValue))
			{
				continue;
			}
			member.pingSent = DateTime.UtcNow;
			SteamNetworkingIdentity val = default(SteamNetworkingIdentity);
			((SteamNetworkingIdentity)(ref val)).SetSteamID(member.id);
			fixed (byte* ptr = array)
			{
				IntPtr intPtr = (IntPtr)ptr;
				SteamNetworkingMessages.SendMessageToUser(ref val, intPtr, (uint)array.Length, num, 31);
			}
		}
	}

	public bool MapRetrievalInProgress()
	{
		return receiveBuffer != null;
	}

	public unsafe void receiveLobbyMessages()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_04bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_054d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0552: Unknown result type (might be due to invalid IL or missing references)
		//IL_0557: Unknown result type (might be due to invalid IL or missing references)
		//IL_0564: Unknown result type (might be due to invalid IL or missing references)
		//IL_0569: Unknown result type (might be due to invalid IL or missing references)
		//IL_0347: Unknown result type (might be due to invalid IL or missing references)
		//IL_0349: Unknown result type (might be due to invalid IL or missing references)
		//IL_034e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0383: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_038d: Invalid comparison between Unknown and I4
		//IL_041a: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_0424: Invalid comparison between Unknown and I4
		//IL_0454: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_058a: Unknown result type (might be due to invalid IL or missing references)
		//IL_058f: Unknown result type (might be due to invalid IL or missing references)
		//IL_038f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0426: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Unknown result type (might be due to invalid IL or missing references)
		IntPtr[] array = new IntPtr[200];
		int num = SteamNetworkingMessages.ReceiveMessagesOnChannel(1, array, array.Length);
		for (int i = 0; i < num; i++)
		{
			SteamNetworkingMessage_t val = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[i]);
			byte[] array2 = new byte[val.m_cbSize];
			Marshal.Copy(val.m_pData, array2, 0, array2.Length);
			SteamNetworkingMessage_t.Release(array[i]);
			int mode = 0;
			int crc = 0;
			byte[] array3 = decodeMapData(array2, ref mode, ref crc);
			if (mode == 0)
			{
				if (lastCrc != crc)
				{
					lastCrc = crc;
					incomingSize = BitConverter.ToInt32(ReadOnlySpan<byte>.op_Implicit(array3));
					receiveBuffer = new byte[incomingSize];
					MapReceiveProgress = 1;
					incomingOfset = 0;
					if (MapProgressDelegate != null)
					{
						MapProgressDelegate();
					}
				}
			}
			else
			{
				if (lastCrc != crc)
				{
					continue;
				}
				switch (mode)
				{
				case 1:
					lastReceivedFileName = Encoding.UTF8.GetString(array3);
					receivingMode = 2;
					MapReceiveProgress++;
					if (MapProgressDelegate != null)
					{
						MapProgressDelegate();
					}
					break;
				case 2:
				{
					if (receiveBuffer == null)
					{
						break;
					}
					int num2 = array3.Length;
					if (incomingOfset + num2 > incomingSize)
					{
						receivingMode = 0;
						MapReceiveProgress = -1;
						receiveBuffer = null;
						if (MapProgressDelegate != null)
						{
							MapProgressDelegate();
						}
						return;
					}
					Array.Copy(array3, 0, receiveBuffer, incomingOfset, num2);
					incomingOfset += num2;
					if (incomingOfset == incomingSize)
					{
						if (lastCrc != 0 && lastReceivedFileName.Length > 0)
						{
							if (EngineInterface.crc(receiveBuffer) == (uint)lastCrc)
							{
								File.WriteAllBytes(Path.Combine(ConfigSettings.GetUserMapsPath(), lastReceivedFileName + ".map"), receiveBuffer);
								MapReceiveProgress = 0;
								if (MapReceivedDelegate != null)
								{
									MapReceivedDelegate();
								}
								SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "request", "");
								receiveBuffer = null;
							}
							else
							{
								MapReceiveProgress = 0;
								if (MapReceivedDelegate != null)
								{
									MapReceivedDelegate();
								}
								Debug.Log((object)"Download Failed");
							}
						}
						receivingMode = 0;
					}
					else
					{
						MapReceiveProgress = (incomingOfset + num2) * 100 / incomingSize;
						if (MapReceiveProgress > 100)
						{
							MapReceiveProgress = 100;
						}
						if (MapProgressDelegate != null)
						{
							MapProgressDelegate();
						}
					}
					break;
				}
				}
			}
		}
		if (activeLobby == null)
		{
			return;
		}
		if (!activeLobby.isHost)
		{
			int num3 = 40;
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(21, array, array.Length);
			for (int j = 0; j < num; j++)
			{
				SteamNetworkingMessage_t val2 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[j]);
				byte[] array4 = new byte[val2.m_cbSize];
				Marshal.Copy(val2.m_pData, array4, 0, array4.Length);
				int num4 = BitConverter.ToInt32(array4, 0);
				string fileName = Encoding.UTF8.GetString(array4, 4, array4.Length - 4);
				FileHeader headerFromMpSaveFileName = MapFileManager.Instance.GetHeaderFromMpSaveFileName(fileName);
				if (headerFromMpSaveFileName != null && (headerFromMpSaveFileName.xPlaySaveChecksum ^ headerFromMpSaveFileName.xPlaySaveTime) == num4)
				{
					SteamNetworkingIdentity identityPeer = val2.m_identityPeer;
					fixed (byte* ptr = array4)
					{
						IntPtr intPtr = (IntPtr)ptr;
						EResult val3 = SteamNetworkingMessages.SendMessageToUser(ref identityPeer, intPtr, (uint)array4.Length, num3, 21);
						if ((int)val3 != 1)
						{
							Debug.Log((object)val3);
						}
					}
				}
				SteamNetworkingMessage_t.Release(array[j]);
			}
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(31, array, array.Length);
			for (int k = 0; k < num; k++)
			{
				byte[] array5 = new byte[0];
				SteamNetworkingIdentity identityPeer2 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[k]).m_identityPeer;
				SteamNetworkingMessage_t.Release(array[k]);
				fixed (byte* ptr2 = array5)
				{
					IntPtr intPtr2 = (IntPtr)ptr2;
					EResult val4 = SteamNetworkingMessages.SendMessageToUser(ref identityPeer2, intPtr2, (uint)array5.Length, num3, 32);
					if ((int)val4 != 1)
					{
						Debug.Log((object)val4);
					}
				}
			}
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(6, array, array.Length);
			for (int l = 0; l < num; l++)
			{
				SteamNetworkingMessage_t val5 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[l]);
				byte[] array6 = new byte[val5.m_cbSize];
				Marshal.Copy(val5.m_pData, array6, 0, array6.Length);
				SteamNetworkingMessage_t.Release(array[l]);
				DecodeAIVData(array6);
				MainViewModel.Instance.FRONTMultiplayer.UpdateCustomLordNamesFromMP();
			}
			return;
		}
		num = SteamNetworkingMessages.ReceiveMessagesOnChannel(21, array, array.Length);
		for (int m = 0; m < num; m++)
		{
			SteamNetworkingMessage_t val6 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[m]);
			byte[] array7 = new byte[val6.m_cbSize];
			Marshal.Copy(val6.m_pData, array7, 0, array7.Length);
			SteamNetworkingMessage_t.Release(array[m]);
			BitConverter.ToInt32(array7, 0);
			string fileName2 = Encoding.UTF8.GetString(array7, 4, array7.Length - 4);
			FileHeader headerFromMpSaveFileName2 = MapFileManager.Instance.GetHeaderFromMpSaveFileName(fileName2);
			if (headerFromMpSaveFileName2 != null)
			{
				headerFromMpSaveFileName2.retrieveCRCChecks++;
			}
		}
		num = SteamNetworkingMessages.ReceiveMessagesOnChannel(32, array, array.Length);
		for (int n = 0; n < num; n++)
		{
			_ = new byte[0];
			SteamNetworkingIdentity identityPeer3 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[n]).m_identityPeer;
			SteamNetworkingMessage_t.Release(array[n]);
			CSteamID steamID = ((SteamNetworkingIdentity)(ref identityPeer3)).GetSteamID();
			foreach (MPLobbyMember member in activeLobby.members)
			{
				if (member.id == steamID)
				{
					DateTime utcNow = DateTime.UtcNow;
					member.lastPingDuration = (int)(utcNow - member.pingSent).TotalMilliseconds;
					member.pingSent = DateTime.MinValue;
				}
			}
		}
	}

	public void RequestMap(string mapFileName, Action mapReceived, Action mapProgress)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby != null && !activeLobby.isHost)
		{
			MapReceivedDelegate = mapReceived;
			MapProgressDelegate = mapProgress;
			MapReceiveProgress = 1;
			lastCrc = 0;
			lastReceivedFileName = "";
			SteamMatchmaking.SetLobbyMemberData(activeLobby.id, "request", mapFileName);
		}
	}

	public CSteamID getPlayerSteamID(int playerID)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (gameMember.playerID == playerID)
				{
					return new CSteamID(gameMember.steamID);
				}
			}
		}
		return new CSteamID(0uL);
	}

	public void StartGame(EngineInterface.MultiplayerSetupData MPSetupData, FileHeader map, int coopTrailID, int coopMissionID)
	{
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null || map == null)
		{
			return;
		}
		bool refreshTeams = false;
		bool settingsChanged = false;
		EngineInterface.MultiplayerSetupData MPSetupData2 = new EngineInterface.MultiplayerSetupData();
		RefreshLobbyList(ref MPSetupData2, ref refreshTeams, ref settingsChanged, coopTrailID > 0);
		HUD_LoadSaveRequester.ClearSavedName();
		MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MainGame);
		MainViewModel.Instance.Show_MP_LoadingBlack = true;
		newGameNotLoad = true;
		Director.instance.SetEngineFrameRate(MPSetupData.starting_gamespeed);
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		connectionPauseEngineState = false;
		HUD_IngameMenu.RestartMPInfo restartMPInfo = new HUD_IngameMenu.RestartMPInfo();
		if (coopTrailID == 0)
		{
			foreach (MPLobbyMember member in activeLobby.members)
			{
				if (member.SkirmishMember)
				{
					int thisPlayerFromSteamID = activeLobby.getThisPlayerFromSteamID(member.id.m_SteamID);
					string input = "";
					switch (thisPlayerFromSteamID)
					{
					case 2:
						input = activeLobby.AIVDataPlayer2;
						break;
					case 3:
						input = activeLobby.AIVDataPlayer3;
						break;
					case 4:
						input = activeLobby.AIVDataPlayer4;
						break;
					case 5:
						input = activeLobby.AIVDataPlayer5;
						break;
					case 6:
						input = activeLobby.AIVDataPlayer6;
						break;
					case 7:
						input = activeLobby.AIVDataPlayer7;
						break;
					case 8:
						input = activeLobby.AIVDataPlayer8;
						break;
					}
					FRONT_Multiplayer.MPAIVInfo mPAIVInfo = new FRONT_Multiplayer.MPAIVInfo();
					mPAIVInfo.decode(input);
					MPSetupData.preferredAIVs[thisPlayerFromSteamID - 1] = -1 - mPAIVInfo.rotation;
					restartMPInfo.LordNames[thisPlayerFromSteamID - 1] = mPAIVInfo.lordName;
					restartMPInfo.SetImage(mPAIVInfo.imageData, thisPlayerFromSteamID - 1);
				}
			}
		}
		EngineInterface.initMultiplayerGame(skirmishGame: false, restartMPInfo.encode(), coopTrailID, coopMissionID);
		EngineInterface.setMultiplayerStartingData(MPSetupData);
		EngineInterface.GameAction(Enums.GameActionCommand.MPConfig, MPSetupData.starting_gamespeed, 0);
		if (coopTrailID == 0)
		{
			string str = MPSetupData.ToString();
			if (FRONT_Multiplayer.MPLastSetupData == null)
			{
				FRONT_Multiplayer.MPLastSetupData = new EngineInterface.MultiplayerSetupData();
			}
			FRONT_Multiplayer.MPLastSetupData.FromString(str);
		}
		gameMembers = new List<MPGameMember>();
		int num = 1;
		foreach (MPLobbyMember member2 in activeLobby.members)
		{
			MPGameMember mPGameMember = new MPGameMember();
			mPGameMember.lobbyData = member2;
			mPGameMember.playerName = member2.Name;
			mPGameMember.SNI = default(SteamNetworkingIdentity);
			((SteamNetworkingIdentity)(ref mPGameMember.SNI)).SetSteamID(member2.id);
			mPGameMember.steamID = member2.id.m_SteamID;
			num = (mPGameMember.playerID = activeLobby.getThisPlayerFromSteamID(member2.id.m_SteamID));
			mPGameMember.colourID = member2.colourID;
			mPGameMember.lordType = member2.lordType;
			mPGameMember.isSelf = member2.id == SteamUser.GetSteamID();
			mPGameMember.isHost = member2.id == SteamMatchmaking.GetLobbyOwner(activeLobby.id);
			gameMembers.Add(mPGameMember);
			if (mPGameMember.isSelf)
			{
				localPlayerID = num;
			}
			int team = activeLobby.getTeam(member2);
			if (activeLobby.CountTeamMembers(team) <= 1)
			{
				team = 0;
			}
			if (!member2.SkirmishMember)
			{
				EngineInterface.RegisterMPPlayer(num, member2.Name, team, mPGameMember.isSelf, mPGameMember.lordType);
				if (!mPGameMember.isSelf)
				{
					coopPartnerID = member2.GetSteamID();
				}
			}
			else
			{
				mPGameMember.skirmishAI = true;
				EngineInterface.RegisterSkirmishUser(num, member2.GetLordType(), member2.GetLordSubType(), team);
				if (coopTrailID > 0)
				{
					AIVLoader.UploadDefaultAIV(member2.GetLordType(), num);
				}
				else
				{
					string input2 = "";
					switch (num)
					{
					case 2:
						input2 = activeLobby.AIVDataPlayer2;
						break;
					case 3:
						input2 = activeLobby.AIVDataPlayer3;
						break;
					case 4:
						input2 = activeLobby.AIVDataPlayer4;
						break;
					case 5:
						input2 = activeLobby.AIVDataPlayer5;
						break;
					case 6:
						input2 = activeLobby.AIVDataPlayer6;
						break;
					case 7:
						input2 = activeLobby.AIVDataPlayer7;
						break;
					case 8:
						input2 = activeLobby.AIVDataPlayer8;
						break;
					}
					FRONT_Multiplayer.MPAIVInfo mPAIVInfo2 = new FRONT_Multiplayer.MPAIVInfo();
					mPAIVInfo2.decode(input2);
					if (mPAIVInfo2.builtIn || mPAIVInfo2.aivs.Count == 0)
					{
						AIVLoader.UploadDefaultAIV(member2.GetLordType(), num);
					}
					else if (mPAIVInfo2.community)
					{
						AIVLoader.UploadDefaultAIV(member2.GetLordType(), num, evreySkirmishSet: true);
					}
					else if (mPAIVInfo2.historical)
					{
						AIVLoader.UploadDefaultAIV(member2.GetLordType(), num, evreySkirmishSet: false, evreyHistoricalSet: true);
					}
					else
					{
						EngineInterface.ImportAIV(num - 1, 0, mPAIVInfo2.aivs[0].data, 1);
					}
					if (!mPAIVInfo2.builtInLord && mPAIVInfo2.lordConfig != null)
					{
						EngineInterface.setCustomLordConfig(ref mPAIVInfo2.lordConfig.lordData, num);
					}
				}
			}
			num++;
		}
		EngineInterface.LoadMapReturnData retData = EngineInterface.loadMultiplayerMap(map.filePath);
		MainViewModel.Instance.HUDIngameMenu.restartMPInfo = restartMPInfo;
		IsHost = activeLobby.isHost;
		if (activeLobby.isHost)
		{
			int value = EngineInterface.StartMultiplayerGame(fromSave: false);
			MPData mPData = new MPData();
			mPData.packetType = 2;
			mPData.dataLength = 4;
			mPData.data = BitConverter.GetBytes(value);
			SendPacketToAll(mPData);
			AchievementsCommon.Instance.ResetOnMissionStart();
			EditorDirector.instance.postLoading(retData, startGameThread: false);
			EditorDirector.instance.SetLocalPlayer(localPlayerID);
			SpriteMapping.BuildMultiPlayerColourMapping();
			MainViewModel.Instance.UpdateUITroopSprites(UIColourRemap[SpriteMapping.remapColours[localPlayerID]], retData.arabicLord > 0);
			MainViewModel.Instance.InitObjectiveGoodsPanel();
			monitoringForGameStart = true;
			monitoringForGameStartTime = DateTime.UtcNow;
		}
		else if (seedReceived)
		{
			SendEmptyPacketTypeToAll(Enums.MPFlags.InitialAcknowledgePacket);
			EngineInterface.SetMPRandSeed(randSeedReceived);
			AchievementsCommon.Instance.ResetOnMissionStart();
			EditorDirector.instance.postLoading(retData, startGameThread: false);
			MainViewModel.Instance.InitObjectiveGoodsPanel();
			EditorDirector.instance.SetLocalPlayer(localPlayerID);
			SpriteMapping.BuildMultiPlayerColourMapping();
			MainViewModel.Instance.UpdateUITroopSprites(UIColourRemap[SpriteMapping.remapColours[localPlayerID]], retData.arabicLord > 0);
		}
		else
		{
			lastRetData = retData;
			loadingFromSave = false;
		}
		mapLoaded = true;
	}

	public void StartSave(EngineInterface.MultiplayerSetupData MPSetupData, FileHeader map)
	{
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		//IL_018d: Unknown result type (might be due to invalid IL or missing references)
		if (activeLobby == null || map == null)
		{
			return;
		}
		bool refreshTeams = false;
		bool settingsChanged = false;
		EngineInterface.MultiplayerSetupData MPSetupData2 = new EngineInterface.MultiplayerSetupData();
		RefreshLobbyList(ref MPSetupData2, ref refreshTeams, ref settingsChanged, map.coopTrailID > 0);
		MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MainGame);
		MainViewModel.Instance.Show_MP_LoadingBlack = true;
		newGameNotLoad = false;
		Director.instance.SetEngineFrameRate(MPSetupData.starting_gamespeed);
		EngineInterface.sendPath(Application.streamingAssetsPath, ConfigSettings.GetMpAutoSavePath(), ConfigSettings.GetSavesPath());
		EngineInterface.initMultiplayerGame();
		connectionPauseEngineState = false;
		EngineInterface.GameAction(Enums.GameActionCommand.MPConfig, MPSetupData.starting_gamespeed, 0);
		gameMembers = new List<MPGameMember>();
		int num = 1;
		int playerID = -1;
		if (!IsHost)
		{
			for (int i = 1; i < 9; i++)
			{
				loadPlayerRemapping[i] = -1;
			}
		}
		foreach (MPLobbyMember member in activeLobby.members)
		{
			if (!member.SkirmishMember)
			{
				MPGameMember mPGameMember = new MPGameMember();
				mPGameMember.lobbyData = member;
				mPGameMember.playerName = member.Name;
				mPGameMember.SNI = default(SteamNetworkingIdentity);
				((SteamNetworkingIdentity)(ref mPGameMember.SNI)).SetSteamID(member.id);
				mPGameMember.steamID = member.id.m_SteamID;
				mPGameMember.playerID = num;
				mPGameMember.colourID = member.colourID;
				mPGameMember.isSelf = member.id == SteamUser.GetSteamID();
				mPGameMember.isHost = member.id == SteamMatchmaking.GetLobbyOwner(activeLobby.id);
				if (mPGameMember.isHost)
				{
					playerID = num;
				}
				gameMembers.Add(mPGameMember);
				if (mPGameMember.isSelf)
				{
					localPlayerID = num;
				}
				else
				{
					coopPartnerID = mPGameMember.steamID;
				}
				EngineInterface.RegisterMPPlayer(num, member.Name, activeLobby.getTeam(member), mPGameMember.isSelf, -1);
				num++;
			}
		}
		EngineInterface.LoadMapReturnData retData = EngineInterface.loadMultiplayerMap(map.filePath, multiplayerSave: true);
		if (map.hasRestartMPInfo)
		{
			FileHeader fileInfoFromFileName = MapFileManager.Instance.GetFileInfoFromFileName(map.filePath, map.filePath, 0, loadRestartInfo: true);
			if (fileInfoFromFileName.restartMPInfo != null)
			{
				MainViewModel.Instance.HUDIngameMenu.restartMPInfo = fileInfoFromFileName.restartMPInfo;
			}
		}
		int playerID2 = retData.playerID;
		if (!activeLobby.isHost)
		{
			MPData mPData = new MPData();
			mPData.packetType = 9;
			mPData.dataLength = 8;
			byte[] bytes = BitConverter.GetBytes(playerID2);
			byte[] bytes2 = BitConverter.GetBytes(localPlayerID);
			mPData.data = new byte[8];
			for (int j = 0; j < 4; j++)
			{
				mPData.data[j] = bytes[j];
				mPData.data[j + 4] = bytes2[j];
			}
			SendPacketToPlayerID(playerID, mPData);
		}
		else
		{
			loadPlayerRemapping[localPlayerID] = playerID2;
			if (retData.computer_register0 > 0)
			{
				loadPlayerRemapping[1] = 1000 + retData.computer_register0 * 8 + retData.computer_name0;
			}
			if (retData.computer_register1 > 0)
			{
				loadPlayerRemapping[2] = 1000 + retData.computer_register1 * 8 + retData.computer_name1;
			}
			if (retData.computer_register2 > 0)
			{
				loadPlayerRemapping[3] = 1000 + retData.computer_register2 * 8 + retData.computer_name2;
			}
			if (retData.computer_register3 > 0)
			{
				loadPlayerRemapping[4] = 1000 + retData.computer_register3 * 8 + retData.computer_name3;
			}
			if (retData.computer_register4 > 0)
			{
				loadPlayerRemapping[5] = 1000 + retData.computer_register4 * 8 + retData.computer_name4;
			}
			if (retData.computer_register5 > 0)
			{
				loadPlayerRemapping[6] = 1000 + retData.computer_register5 * 8 + retData.computer_name5;
			}
			if (retData.computer_register6 > 0)
			{
				loadPlayerRemapping[7] = 1000 + retData.computer_register6 * 8 + retData.computer_name6;
			}
			if (retData.computer_register7 > 0)
			{
				loadPlayerRemapping[8] = 1000 + retData.computer_register7 * 8 + retData.computer_name7;
			}
		}
		IsHost = activeLobby.isHost;
		if (activeLobby.isHost)
		{
			int value = EngineInterface.StartMultiplayerGame(fromSave: true);
			MPData mPData2 = new MPData();
			mPData2.packetType = 2;
			mPData2.dataLength = 4;
			mPData2.data = BitConverter.GetBytes(value);
			SendPacketToAll(mPData2);
			AchievementsCommon.Instance.UpdateAfterLoadingASave(map.achFood, map.achWood, map.achWeapons);
			EditorDirector.instance.postLoading(retData, startGameThread: false);
			EditorDirector.instance.SetLocalPlayer(localPlayerID);
			SpriteMapping.SetRemapColours(new int[9] { 0, retData.radar_colour_mapping0, retData.radar_colour_mapping1, retData.radar_colour_mapping2, retData.radar_colour_mapping3, retData.radar_colour_mapping4, retData.radar_colour_mapping5, retData.radar_colour_mapping6, retData.radar_colour_mapping7 });
			MainViewModel.Instance.UpdateUITroopSprites(UIColourRemap[SpriteMapping.remapColours[localPlayerID]], retData.arabicLord > 0);
			MainViewModel.Instance.InitObjectiveGoodsPanel();
			monitoringForGameStart = true;
			monitoringForGameStartTime = DateTime.UtcNow;
		}
		else if (seedReceived)
		{
			SendEmptyPacketTypeToAll(Enums.MPFlags.InitialAcknowledgePacket);
			EngineInterface.SetMPRandSeed(randSeedReceived);
			AchievementsCommon.Instance.UpdateAfterLoadingASave(map.achFood, map.achWood, map.achWeapons);
			EditorDirector.instance.postLoading(retData, startGameThread: false);
			EditorDirector.instance.SetLocalPlayer(localPlayerID);
			SpriteMapping.SetRemapColours(new int[9] { 0, retData.radar_colour_mapping0, retData.radar_colour_mapping1, retData.radar_colour_mapping2, retData.radar_colour_mapping3, retData.radar_colour_mapping4, retData.radar_colour_mapping5, retData.radar_colour_mapping6, retData.radar_colour_mapping7 });
			MainViewModel.Instance.UpdateUITroopSprites(UIColourRemap[SpriteMapping.remapColours[localPlayerID]], retData.arabicLord > 0);
			MainViewModel.Instance.InitObjectiveGoodsPanel();
		}
		else
		{
			lastRetData = retData;
			loadingFromSave = true;
			achFood = map.achFood;
			achWood = map.achWood;
			achWeapons = map.achWeapons;
		}
		mapLoaded = true;
	}

	public int getActiveSteamPlayers()
	{
		return -1;
	}

	public int getKickedSteamPlayers()
	{
		return -1;
	}

	public MPGameMember findMember(SteamNetworkingIdentity SNI)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (((SteamNetworkingIdentity)(ref SNI)).GetSteamID() == ((SteamNetworkingIdentity)(ref gameMember.SNI)).GetSteamID())
				{
					return gameMember;
				}
			}
		}
		return null;
	}

	public void HandleGameIncomingConnection(SteamNetworkingMessagesSessionRequest_t callback)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity identityRemote = callback.m_identityRemote;
		if (gameMembers != null)
		{
			foreach (MPGameMember gameMember in gameMembers)
			{
				if (((SteamNetworkingIdentity)(ref identityRemote)).GetSteamID() == gameMember.lobbyData.id)
				{
					SteamNetworkingMessages.AcceptSessionWithUser(ref identityRemote);
					break;
				}
			}
			return;
		}
		if (activeLobby != null && ((SteamNetworkingIdentity)(ref identityRemote)).GetSteamID() == SteamMatchmaking.GetLobbyOwner(activeLobby.id))
		{
			SteamNetworkingMessages.AcceptSessionWithUser(ref identityRemote);
		}
	}

	public unsafe void SendGameData(MPGameMember target, byte[] dataToSend, bool instantMessage = false)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Invalid comparison between Unknown and I4
		if (target.kicked)
		{
			target.packetsSent--;
			return;
		}
		SteamNetworkingIdentity val = default(SteamNetworkingIdentity);
		((SteamNetworkingIdentity)(ref val)).SetSteamID(new CSteamID(target.steamID));
		int num = 40;
		fixed (byte* ptr = dataToSend)
		{
			IntPtr intPtr = (IntPtr)ptr;
			if ((int)SteamNetworkingMessages.SendMessageToUser(ref val, intPtr, (uint)dataToSend.Length, num, 2) != 1)
			{
				target.errorCount++;
				return;
			}
			target.errorCount = 0;
			target.packetsSent++;
		}
	}

	public void clearMPMessages()
	{
		threadedMessages.Clear();
	}

	public void flushGameMessages()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		IntPtr[] array = new IntPtr[200];
		int num = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, array, array.Length);
		for (int i = 0; i < num; i++)
		{
			Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[i]);
			SteamNetworkingMessage_t.Release(array[i]);
		}
	}

	public void ReceiveGameMessages(bool fromThread = false)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		if (gameMembers == null)
		{
			return;
		}
		int num = 0;
		foreach (MPGameMember gameMember in gameMembers)
		{
			if (gameMember.stillWithSteamConnection)
			{
				if (gameMember.kicked)
				{
					SteamNetworkingMessages.CloseSessionWithUser(ref gameMember.SNI);
					gameMember.stillWithSteamConnection = false;
				}
				else if (!gameMember.isSelf && !gameMember.skirmishAI)
				{
					num++;
				}
			}
		}
		if (num == 0)
		{
			return;
		}
		IntPtr[] array = new IntPtr[200];
		int num2 = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, array, array.Length);
		for (int i = 0; i < num2; i++)
		{
			SteamNetworkingMessage_t val = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[i]);
			if (val.m_cbSize > 2000000)
			{
				SteamNetworkingMessage_t.Release(array[i]);
				continue;
			}
			byte[] array2 = new byte[val.m_cbSize];
			Marshal.Copy(val.m_pData, array2, 0, array2.Length);
			MessageData messageData = new MessageData();
			messageData.data = MPData.FromBytes(array2);
			messageData.fromMember = findMember(val.m_identityPeer);
			SteamNetworkingMessage_t.Release(array[i]);
			if (messageData.fromMember != null)
			{
				messageData.fromMember.packetsReceived++;
				if (!processMessage(messageData.data, messageData.fromMember, fromThread))
				{
					threadedMessages.Enqueue(messageData);
				}
			}
		}
	}

	public void processMPMessages()
	{
		while (threadedMessages.Count > 0)
		{
			if (threadedMessages.TryDequeue(out var result))
			{
				processMessage(result.data, result.fromMember, fromThread: false);
			}
		}
	}

	public bool processMessage(MPData data, MPGameMember fromMember, bool fromThread)
	{
		//IL_07da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0681: Unknown result type (might be due to invalid IL or missing references)
		//IL_061c: Unknown result type (might be due to invalid IL or missing references)
		if (fromMember == null)
		{
			if (data.packetType == 2)
			{
				if (fromThread)
				{
					return false;
				}
				int num = BitConverter.ToInt32(ReadOnlySpan<byte>.op_Implicit(data.data));
				randSeedReceived = num;
				seedReceived = true;
			}
			else if (data.packetType == 9 && IsHost)
			{
				if (fromThread)
				{
					return false;
				}
				if (data.dataLength == 8)
				{
					int num2 = BitConverter.ToInt32(data.data, 0);
					int num3 = BitConverter.ToInt32(data.data, 4);
					loadPlayerRemapping[num3] = num2;
				}
			}
			return true;
		}
		if (fromMember.kicked)
		{
			if (IsHost)
			{
				new MPData
				{
					packetType = 7,
					dataLength = 4,
					data = BitConverter.GetBytes(fromMember.playerID)
				};
				byte[] dataToSend = data.ToBytes();
				SendGameData(fromMember, dataToSend);
			}
			return true;
		}
		switch (data.packetType)
		{
		case 2:
		{
			if (fromThread)
			{
				return false;
			}
			int mPRandSeed = BitConverter.ToInt32(ReadOnlySpan<byte>.op_Implicit(data.data));
			if (mapLoaded)
			{
				EngineInterface.SetMPRandSeed(mPRandSeed);
				EditorDirector.instance.postLoading(lastRetData, startGameThread: false);
				EditorDirector.instance.SetLocalPlayer(localPlayerID);
				if (loadingFromSave)
				{
					AchievementsCommon.Instance.UpdateAfterLoadingASave(achFood, achWood, achWeapons);
					SpriteMapping.SetRemapColours(new int[9] { 0, lastRetData.radar_colour_mapping0, lastRetData.radar_colour_mapping1, lastRetData.radar_colour_mapping2, lastRetData.radar_colour_mapping3, lastRetData.radar_colour_mapping4, lastRetData.radar_colour_mapping5, lastRetData.radar_colour_mapping6, lastRetData.radar_colour_mapping7 });
				}
				else
				{
					AchievementsCommon.Instance.ResetOnMissionStart();
					SpriteMapping.BuildMultiPlayerColourMapping();
				}
				MainViewModel.Instance.UpdateUITroopSprites(UIColourRemap[SpriteMapping.remapColours[localPlayerID]], lastRetData.arabicLord > 0);
				MainViewModel.Instance.InitObjectiveGoodsPanel();
				SendEmptyPacketTypeToAll(Enums.MPFlags.InitialAcknowledgePacket);
			}
			else
			{
				randSeedReceived = mPRandSeed;
				seedReceived = true;
			}
			break;
		}
		case 3:
			if (fromThread)
			{
				return false;
			}
			if (IsHost)
			{
				fromMember.acknowledged = true;
			}
			break;
		case 9:
			if (fromThread)
			{
				return false;
			}
			if (IsHost && data.dataLength == 8)
			{
				int num6 = BitConverter.ToInt32(data.data, 0);
				int num7 = BitConverter.ToInt32(data.data, 4);
				loadPlayerRemapping[num7] = num6;
			}
			break;
		case 4:
			if (fromThread)
			{
				return false;
			}
			LeaveLobby();
			if (data.dataLength == 36)
			{
				for (int i = 0; i < 9; i++)
				{
					int num5 = BitConverter.ToInt32(data.data, i * 4);
					loadPlayerRemapping[i] = num5;
				}
				if (!remapMPGameMembers())
				{
					FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);
					return true;
				}
			}
			Director.instance.startSimThread();
			EngineInterface.GameAction(Enums.KeyFunctions.HomeKeep);
			if (ConfigSettings.Settings_ShowPings)
			{
				OnScreenText.Instance.addOSTEntry(Enums.eOnScreenText.OST_PINGS, 1);
			}
			EngineInterface.StartMultiplayerGameSynced();
			Director.instance.DelayHideConnectionScreen();
			MPGameActive = true;
			break;
		case 1:
			if (fromThread && data.dataLength > 0)
			{
				byte b = data.data[0];
				if (b == 39 || b == 54 || b == 89)
				{
					return false;
				}
			}
			EngineInterface.ReceiveChore(fromMember.playerID, data.data, data.dataLength);
			if (data.dataLength > 0)
			{
				switch (data.data[0])
				{
				case 0:
					fromMember.lastTimePacketRecieved = DateTime.UtcNow;
					break;
				case 54:
					resyncing = (resyncingOrSaving = true);
					resyncingStart = DateTime.UtcNow;
					resyncingCurrentSection = 0;
					resyncingCurrentLayer = 0;
					break;
				case 67:
					resyncing = (resyncingOrSaving = false);
					resyncingOrSavingResumeTime = DateTime.MinValue;
					break;
				case 39:
					resyncingOrSaving = true;
					MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(getPlayerName(fromMember.playerID), fromMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 32));
					break;
				case 94:
					resyncingOrSaving = false;
					resyncingOrSavingResumeTime = DateTime.UtcNow.AddSeconds(5.0);
					break;
				case 56:
				case 57:
				case 58:
				case 59:
				case 60:
				case 61:
				case 62:
				case 80:
				case 81:
				case 82:
				case 84:
				case 114:
				case 115:
					resyncingCurrentSection = data.data[0];
					resyncingCurrentLayer = 0;
					break;
				case 63:
				case 64:
				case 65:
					resyncingCurrentSection = data.data[0];
					resyncingCurrentLayer = BitConverter.ToInt32(data.data, 8);
					break;
				case 89:
					EditorDirector.instance.MPGameKilled();
					break;
				}
			}
			break;
		case 5:
			if (fromThread)
			{
				return false;
			}
			if (!fromMember.muted && !MPChatMuted)
			{
				string message = Encoding.UTF8.GetString(data.data);
				MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(getPlayerName(fromMember.playerID), fromMember.playerID, message);
			}
			break;
		case 6:
			if (fromThread)
			{
				return false;
			}
			if (!fromMember.muted && !MPChatMuted && !ConfigSettings.Settings_MuteInsults)
			{
				int num4 = BitConverter.ToInt32(ReadOnlySpan<byte>.op_Implicit(data.data));
				MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(getPlayerName(fromMember.playerID), fromMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_INSULTS, num4), 10);
				if (!ConfigSettings.Settings_MuteInsultSpeech)
				{
					SFXManager.instance.playInsult(num4);
				}
			}
			break;
		case 7:
		{
			if (fromThread)
			{
				return false;
			}
			int playerID = BitConverter.ToInt32(ReadOnlySpan<byte>.op_Implicit(data.data));
			MPGameMember player = getPlayer(playerID);
			if (player == null || player.kicked)
			{
				break;
			}
			if (player.isSelf)
			{
				int otherActivePlayers = countOtherPlayers(player);
				if (player.DoVoteKick(fromMember.playerID, otherActivePlayers))
				{
					exitMP();
					EditorDirector.instance.stopGameSim();
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					MainViewModel.Instance.FrontEndMenu.ShowMPConnectionPopup();
				}
				break;
			}
			player.kickCounter++;
			int otherActivePlayers2 = countOtherPlayers(player);
			if (player.DoVoteKick(fromMember.playerID, otherActivePlayers2))
			{
				if (player.isHost)
				{
					promoteNewHost(player);
				}
				player.kicked = true;
				EngineInterface.KickMPPlayer(playerID, kickImmediate: false);
				MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(player.playerName, player.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 21));
			}
			break;
		}
		case 8:
			if (fromThread)
			{
				return false;
			}
			if (fromMember.isHost)
			{
				promoteNewHost(fromMember);
			}
			fromMember.kicked = true;
			EngineInterface.KickMPPlayer(fromMember.playerID, kickImmediate: false);
			MainViewModel.Instance.HUDMPChatMessages.recieveIngameChat(fromMember.playerName, fromMember.playerID, Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MULTIPLAYER_CONNECTION, 21));
			break;
		case 10:
			CoopContinuationLobbyID = BitConverter.ToUInt64(ReadOnlySpan<byte>.op_Implicit(data.data));
			break;
		}
		return true;
	}

	public bool MonitorNetworkConnectivity()
	{
		return SteamUser.BLoggedOn();
	}

	public void CreateShareCode(ulong lobbyID, bool coopGame = false, int coopTrailID = -1)
	{
		string text = Convert.ToBase64String(BitConverter.GetBytes(lobbyID));
		int num = 0;
		string text2 = text;
		foreach (char c in text2)
		{
			num += c;
		}
		int num2 = num % 26 + 65;
		ShareCodeString = text + (char)num2;
	}

	public ulong DecodeShareCode(string code)
	{
		ulong result = 0uL;
		if (code.Length > 2)
		{
			int num = code[code.Length - 1] - 65;
			code = code.Substring(0, code.Length - 1);
			int num2 = 0;
			string text = code;
			foreach (char c in text)
			{
				num2 += c;
			}
			if (num2 % 26 == num)
			{
				try
				{
					byte[] array = Convert.FromBase64String(code);
					if (array.Length == 8)
					{
						result = BitConverter.ToUInt64(array, 0);
					}
				}
				catch (Exception)
				{
				}
			}
		}
		return result;
	}

	public void SetCoatOfArms(ImageSource coa)
	{
		_localUserCoatOfArms = coa;
	}

	public ulong GetLocalSteamID()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		return SteamUser.GetSteamID().m_SteamID;
	}

	public string GetLocalSteamName()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (!SteamManager.Initialized)
		{
			return "";
		}
		return SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
	}

	public string getSteamUserName(ulong steamID)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (!SteamManager.Initialized)
		{
			return "";
		}
		return SteamFriends.GetFriendPersonaName(new CSteamID(steamID));
	}

	public ImageSource GetLocalAvatar()
	{
		if (!SteamManager.Initialized)
		{
			return null;
		}
		if (ConfigSettings.Settings_UseSteamAvatar)
		{
			if ((BaseComponent)(object)_localUserAvatar != (BaseComponent)null)
			{
				return _localUserAvatar;
			}
		}
		else if ((BaseComponent)(object)_localUserCoatOfArms != (BaseComponent)null)
		{
			return _localUserCoatOfArms;
		}
		return null;
	}

	public void ClearSteamAvatarCache()
	{
		_Cache.Clear();
	}

	public void OnAvatarRequested(AvatarImageLoaded_t param)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		RequestUserAvatar(param.m_steamID, selfCall: true);
	}

	public ImageSource GetUserAvatar(ulong steamID)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return GetUserAvatar(new CSteamID(steamID));
	}

	public ImageSource GetUserAvatar(CSteamID steamID)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		if (!SteamManager.Initialized)
		{
			return null;
		}
		if (steamID == SteamUser.GetSteamID())
		{
			return GetLocalAvatar();
		}
		if (_Cache.ContainsKey(steamID))
		{
			return _Cache[steamID];
		}
		return null;
	}

	public void CreateCoAAvatar(ulong steamID, Avatars.AvatarDesign ad)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		CreateCoAAvatar(new CSteamID(steamID), ad);
	}

	public void CreateCoAAvatar(CSteamID steamID, Avatars.AvatarDesign ad)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		if (!_Cache.ContainsKey(steamID))
		{
			ImageSource avatarTexture = (ImageSource)(object)Avatars.Instance.GetAvatarTexture(ad);
			_Cache.Add(steamID, avatarTexture);
		}
	}

	public void RequestUserAvatar(ulong steamID)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		RequestUserAvatar(new CSteamID(steamID));
	}

	public void RequestUserAvatar(CSteamID steamID, bool selfCall = false)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_0281: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Expected O, but got Unknown
		//IL_0288: Unknown result type (might be due to invalid IL or missing references)
		//IL_0289: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a5: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!SteamManager.Initialized)
			{
				return;
			}
			if (steamID == SteamUser.GetSteamID())
			{
				if ((BaseComponent)(object)_localUserAvatar != (BaseComponent)null)
				{
					return;
				}
			}
			else if (_Cache.ContainsKey(steamID))
			{
				return;
			}
			int largeFriendAvatar = SteamFriends.GetLargeFriendAvatar(steamID);
			switch (largeFriendAvatar)
			{
			case 0:
				_Cache.Add(steamID, null);
				return;
			case -1:
				if (!selfCall && m_AvatarLoadedRequested == null)
				{
					m_AvatarLoadedRequested = Callback<AvatarImageLoaded_t>.Create((DispatchDelegate<AvatarImageLoaded_t>)OnAvatarRequested);
				}
				return;
			}
			uint num = default(uint);
			uint num2 = default(uint);
			if (!SteamUtils.GetImageSize(largeFriendAvatar, ref num, ref num2) || num == 0 || num2 == 0)
			{
				return;
			}
			byte[] array = new byte[num * num2 * 4];
			if (!SteamUtils.GetImageRGBA(largeFriendAvatar, array, (int)(num * num2 * 4)))
			{
				return;
			}
			int num3 = 12;
			Texture2D val = new Texture2D((int)num + num3 * 2, (int)num2 + num3 * 2, (TextureFormat)4, false, false);
			byte[] array2 = new byte[(num + num3 * 2) * (num2 + num3 * 2) * 4];
			for (uint num4 = 0u; num4 < num2; num4++)
			{
				for (uint num5 = 3u; num5 < num * 4; num5 += 4)
				{
					uint num6 = num5 / 4;
					if (num6 > num / 2)
					{
						num6 = num - num6 - 1;
					}
					uint num7 = num4;
					if (num7 > num2 / 2)
					{
						num7 = num2 - num7 - 1;
					}
					if (num6 < 12 || num7 < 12)
					{
						uint num8 = Math.Min(num6, num7);
						array[num5 + num4 * num * 4] = (byte)((int)array[num5 + num4 * num * 4] * num8 / 12);
					}
					if (array[num5 + num4 * num * 4] != byte.MaxValue)
					{
						byte b = array[num5 + num4 * num * 4];
						array[num5 - 1 + num4 * num * 4] = (byte)(array[num5 - 1 + num4 * num * 4] * b / 255);
						array[num5 - 2 + num4 * num * 4] = (byte)(array[num5 - 2 + num4 * num * 4] * b / 255);
						array[num5 - 3 + num4 * num * 4] = (byte)(array[num5 - 3 + num4 * num * 4] * b / 255);
					}
				}
			}
			int num9 = (int)(num + num3 * 2);
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num * 4; j++)
				{
					array2[j + num3 * 4 + (num2 - i - 1 + num3) * num9 * 4] = array[j + i * num * 4];
				}
			}
			val.LoadRawTextureData(array2);
			val.Apply();
			TextureSource val2 = new TextureSource(val);
			if (steamID == SteamUser.GetSteamID())
			{
				_localUserAvatar = (ImageSource)(object)val2;
			}
			else
			{
				_Cache.Add(steamID, (ImageSource)(object)val2);
			}
			Object.DestroyImmediate((Object)(object)val);
		}
		catch (Exception)
		{
		}
	}
}

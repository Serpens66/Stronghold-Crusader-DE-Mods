using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Noesis;
using UnityEngine;

namespace CrusaderDE;

public class HUD_IngameMenu : UserControl
{
	private enum MenuMode
	{
		Normal,
		Multiplayer,
		Editor,
		Tutorial
	}

	public class RestartMapInfo
	{
		public Enums.StartUpUIPanels missionType;

		public FileHeader selectedHeader;

		public Enums.GameDifficulty difficulty = Enums.GameDifficulty.DIFFICULTY_NORMAL;

		public bool removeHostileAnimals;

		public bool advancedFreebuild;

		public int freebuild_GoldLevel;

		public int freebuild_FoodLevel;

		public int freebuild_ResourcesLevel;

		public int freebuild_WeaponsLevel;

		public int freebuild_RandomEvents;

		public int freebuild_Invasions;

		public int freebuild_InvasionDifficulty;

		public int freebuild_Peacetime = 4;

		public int freebuild_Opponents = 7;

		public bool freebuild_Extreme_Troops;

		public bool freebuild_Extreme_Powers;

		public bool freebuild_Defeat_On_Death;

		public byte[] encode()
		{
			List<byte> list = new List<byte>(0);
			list.Add(5);
			list.AddRange(BitConverter.GetBytes((int)missionType));
			list.AddRange(BitConverter.GetBytes(removeHostileAnimals));
			list.AddRange(BitConverter.GetBytes(advancedFreebuild));
			if (advancedFreebuild)
			{
				list.AddRange(BitConverter.GetBytes(freebuild_GoldLevel));
				list.AddRange(BitConverter.GetBytes(freebuild_FoodLevel));
				list.AddRange(BitConverter.GetBytes(freebuild_ResourcesLevel));
				list.AddRange(BitConverter.GetBytes(freebuild_WeaponsLevel));
				list.AddRange(BitConverter.GetBytes(freebuild_RandomEvents));
				list.AddRange(BitConverter.GetBytes(freebuild_Invasions));
				list.AddRange(BitConverter.GetBytes(freebuild_InvasionDifficulty));
				list.AddRange(BitConverter.GetBytes(freebuild_Peacetime));
				list.AddRange(BitConverter.GetBytes(freebuild_Opponents));
				list.AddRange(BitConverter.GetBytes(freebuild_Extreme_Troops));
				list.AddRange(BitConverter.GetBytes(freebuild_Extreme_Powers));
				list.AddRange(BitConverter.GetBytes(freebuild_Defeat_On_Death));
			}
			if (selectedHeader.workshopMap)
			{
				list.Add(2);
			}
			else if (selectedHeader.builtinMap)
			{
				list.Add(1);
			}
			else
			{
				list.Add(0);
			}
			byte[] bytes = Encoding.UTF8.GetBytes(selectedHeader.fileName);
			list.AddRange(BitConverter.GetBytes(bytes.Length));
			list.AddRange(bytes);
			return list.ToArray();
		}

		public static RestartMapInfo decode(byte[] data)
		{
			try
			{
				RestartMapInfo restartMapInfo = new RestartMapInfo();
				int num = 0;
				int num2 = data[num];
				num++;
				restartMapInfo.missionType = (Enums.StartUpUIPanels)BitConverter.ToInt32(data, num);
				num += 4;
				if (num2 >= 3)
				{
					restartMapInfo.removeHostileAnimals = BitConverter.ToBoolean(data, num);
					num++;
				}
				else
				{
					restartMapInfo.removeHostileAnimals = false;
				}
				restartMapInfo.advancedFreebuild = BitConverter.ToBoolean(data, num);
				num++;
				if (restartMapInfo.advancedFreebuild)
				{
					restartMapInfo.freebuild_GoldLevel = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_FoodLevel = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_ResourcesLevel = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_WeaponsLevel = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_RandomEvents = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_Invasions = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_InvasionDifficulty = BitConverter.ToInt32(data, num);
					num += 4;
					restartMapInfo.freebuild_Peacetime = BitConverter.ToInt32(data, num);
					num += 4;
					if (num2 >= 2)
					{
						restartMapInfo.freebuild_Opponents = BitConverter.ToInt32(data, num);
						num += 4;
					}
					else
					{
						restartMapInfo.freebuild_Opponents = 7;
					}
					if (num2 >= 4)
					{
						restartMapInfo.freebuild_Extreme_Troops = BitConverter.ToBoolean(data, num);
						num++;
						restartMapInfo.freebuild_Extreme_Powers = BitConverter.ToBoolean(data, num);
						num++;
					}
					else
					{
						restartMapInfo.freebuild_Extreme_Troops = false;
						restartMapInfo.freebuild_Extreme_Powers = false;
					}
					if (num2 >= 5)
					{
						restartMapInfo.freebuild_Defeat_On_Death = BitConverter.ToBoolean(data, num);
						num++;
					}
					else
					{
						restartMapInfo.freebuild_Defeat_On_Death = false;
					}
				}
				byte num3 = data[num];
				num++;
				bool builtIn = num3 == 1;
				bool workShop = num3 == 2;
				int count = BitConverter.ToInt32(data, num);
				num += 4;
				string fileName = Encoding.UTF8.GetString(data, num, count);
				restartMapInfo.selectedHeader = MapFileManager.Instance.GetHeaderFromFileNameForRestart(fileName, restartMapInfo.missionType == Enums.StartUpUIPanels.FreeBuild, builtIn, workShop);
				if (restartMapInfo.selectedHeader == null)
				{
					return null;
				}
				return restartMapInfo;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	public class RestartSkirmishMapInfo
	{
		public FileHeader selectedHeader;

		public EngineInterface.MultiplayerSetupData MPsetupData;

		public bool extremeTroops;

		public bool extremePowers;

		public bool extremePowersAroundLord;

		public bool allowOutposts = true;

		public bool customisedExtremeTrail = true;

		public bool customTestMission;

		public bool customTrail;

		public int customTrailLevel = -1;

		public string customTrailName = "";

		public int customTrailDifficulty = 1;

		public List<int> lordTypes = new List<int>();

		public List<int> teams = new List<int>();

		public List<int> colours = new List<int>();

		public FRONT_Multiplayer.MPAIVInfo[] aivs;

		public void importMembers(Platform_Multiplayer.MPLobby currentLobby)
		{
			for (int i = 1; i <= 8; i++)
			{
				bool flag = false;
				foreach (Platform_Multiplayer.MPLobbyMember member in currentLobby.members)
				{
					if (i == currentLobby.getThisPlayerFromSteamID(member.id.m_SteamID))
					{
						if (member.SkirmishHumanMember)
						{
							lordTypes.Add(-1);
						}
						else
						{
							lordTypes.Add((int)member.id.m_SteamID);
						}
						teams.Add(currentLobby.getTeam(member));
						colours.Add(member.colourID);
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					lordTypes.Add(-9999);
					teams.Add(0);
					colours.Add(0);
				}
			}
		}

		public void importAIVs(FRONT_Multiplayer.MPAIVInfo[] _aivs)
		{
			if (_aivs == null || _aivs.Length != 8)
			{
				return;
			}
			aivs = new FRONT_Multiplayer.MPAIVInfo[8];
			for (int i = 0; i < 8; i++)
			{
				aivs[i] = new FRONT_Multiplayer.MPAIVInfo();
				aivs[i].lordType = _aivs[i].lordType;
				aivs[i].lordName = _aivs[i].lordName;
				aivs[i].builtIn = _aivs[i].builtIn;
				aivs[i].community = _aivs[i].community;
				aivs[i].historical = _aivs[i].historical;
				aivs[i].rotation = _aivs[i].rotation;
				aivs[i].aivs = new List<CustomisationFileManager.CustomAIV>();
				foreach (CustomisationFileManager.CustomAIV aiv in _aivs[i].aivs)
				{
					aivs[i].aivs.Add(aiv);
				}
				aivs[i].builtInLord = _aivs[i].builtInLord;
				aivs[i].lordConfig = _aivs[i].lordConfig;
				aivs[i].imageData = _aivs[i].imageData;
				aivs[i].image = _aivs[i].image;
			}
		}

		public byte[] encode()
		{
			List<byte> list = new List<byte>(0);
			list.Add(60);
			list.AddRange(BitConverter.GetBytes(lordTypes.Count));
			for (int i = 0; i < lordTypes.Count; i++)
			{
				list.AddRange(BitConverter.GetBytes(lordTypes[i]));
				list.AddRange(BitConverter.GetBytes(teams[i]));
				list.AddRange(BitConverter.GetBytes(colours[i]));
			}
			if (selectedHeader.workshopMap)
			{
				list.Add(2);
			}
			else if (selectedHeader.builtinMap)
			{
				list.Add(1);
			}
			else
			{
				list.Add(0);
			}
			byte[] bytes = Encoding.UTF8.GetBytes(selectedHeader.fileName);
			list.AddRange(BitConverter.GetBytes(bytes.Length));
			list.AddRange(bytes);
			string s = MPsetupData.ToString();
			byte[] bytes2 = Encoding.UTF8.GetBytes(s);
			list.AddRange(BitConverter.GetBytes(bytes2.Length));
			list.AddRange(bytes2);
			list.Add((byte)(extremeTroops ? 1u : 0u));
			list.Add((byte)(extremePowers ? 1u : 0u));
			list.Add((byte)(extremePowersAroundLord ? 1u : 0u));
			list.Add((byte)(allowOutposts ? 1u : 0u));
			list.Add((byte)(customisedExtremeTrail ? 1u : 0u));
			if (aivs != null)
			{
				list.Add((byte)aivs.Length);
				for (int j = 0; j < aivs.Length; j++)
				{
					list.AddRange(BitConverter.GetBytes(aivs[j].lordType));
					list.AddRange(BitConverter.GetBytes(aivs[j].builtIn));
					list.AddRange(BitConverter.GetBytes(aivs[j].community));
					list.AddRange(BitConverter.GetBytes(aivs[j].historical));
					list.AddRange(BitConverter.GetBytes(aivs[j].rotation));
					list.AddRange(BitConverter.GetBytes(aivs[j].aivs.Count));
					for (int k = 0; k < aivs[j].aivs.Count; k++)
					{
						byte[] array = aivs[j].aivs[k].encode();
						list.AddRange(BitConverter.GetBytes(array.Length));
						list.AddRange(array);
					}
					list.AddRange(BitConverter.GetBytes(aivs[j].builtInLord));
					if (!aivs[j].builtInLord)
					{
						byte[] array2 = aivs[j].lordConfig.encode();
						list.AddRange(BitConverter.GetBytes(array2.Length));
						list.AddRange(array2);
					}
					byte[] bytes3 = Encoding.UTF8.GetBytes(aivs[j].lordName);
					list.AddRange(BitConverter.GetBytes(bytes3.Length));
					if (bytes3.Length != 0)
					{
						list.AddRange(bytes3);
					}
					if (aivs[j].imageData != null)
					{
						list.AddRange(BitConverter.GetBytes(aivs[j].imageData.Length));
						list.AddRange(aivs[j].imageData);
					}
					else
					{
						int value = 0;
						list.AddRange(BitConverter.GetBytes(value));
					}
				}
			}
			else
			{
				list.Add(0);
			}
			list.AddRange(BitConverter.GetBytes(customTestMission));
			list.AddRange(BitConverter.GetBytes(customTrail));
			list.AddRange(BitConverter.GetBytes(customTrailLevel));
			byte[] bytes4 = Encoding.UTF8.GetBytes(customTrailName);
			list.AddRange(BitConverter.GetBytes(bytes4.Length));
			if (bytes4.Length != 0)
			{
				list.AddRange(bytes4);
			}
			list.AddRange(BitConverter.GetBytes(customTrailDifficulty));
			return list.ToArray();
		}

		public static RestartSkirmishMapInfo decode(byte[] data, bool customTrailMission = false)
		{
			try
			{
				RestartSkirmishMapInfo restartSkirmishMapInfo = new RestartSkirmishMapInfo();
				int num = 0;
				int num2 = data[num];
				num++;
				int num3 = BitConverter.ToInt32(data, num);
				num += 4;
				for (int i = 0; i < num3; i++)
				{
					restartSkirmishMapInfo.lordTypes.Add(BitConverter.ToInt32(data, num));
					num += 4;
					restartSkirmishMapInfo.teams.Add(BitConverter.ToInt32(data, num));
					num += 4;
					restartSkirmishMapInfo.colours.Add(BitConverter.ToInt32(data, num));
					num += 4;
				}
				byte num4 = data[num];
				num++;
				bool builtIn = num4 == 1;
				bool workShop = num4 == 2;
				int num5 = BitConverter.ToInt32(data, num);
				num += 4;
				string fileName = Encoding.UTF8.GetString(data, num, num5);
				num += num5;
				restartSkirmishMapInfo.selectedHeader = MapFileManager.Instance.GetHeaderFromFileNameForSkirmishRestart(fileName, builtIn, workShop);
				int num6 = BitConverter.ToInt32(data, num);
				num += 4;
				string str = Encoding.UTF8.GetString(data, num, num6);
				num += num6;
				restartSkirmishMapInfo.MPsetupData = new EngineInterface.MultiplayerSetupData();
				restartSkirmishMapInfo.MPsetupData.FromString(str);
				if (num2 >= 52)
				{
					restartSkirmishMapInfo.extremeTroops = data[num] > 0;
					num++;
					restartSkirmishMapInfo.extremePowers = data[num] > 0;
					num++;
					restartSkirmishMapInfo.extremePowersAroundLord = data[num] > 0;
					num++;
					restartSkirmishMapInfo.allowOutposts = data[num] > 0;
					num++;
				}
				if (num2 >= 60)
				{
					restartSkirmishMapInfo.customisedExtremeTrail = data[num] > 0;
					num++;
				}
				else
				{
					restartSkirmishMapInfo.customisedExtremeTrail = false;
				}
				if (num2 >= 53)
				{
					int num7 = data[num];
					num++;
					restartSkirmishMapInfo.aivs = new FRONT_Multiplayer.MPAIVInfo[num7];
					for (int j = 0; j < restartSkirmishMapInfo.aivs.Length; j++)
					{
						restartSkirmishMapInfo.aivs[j] = new FRONT_Multiplayer.MPAIVInfo();
						restartSkirmishMapInfo.aivs[j].lordType = BitConverter.ToInt32(data, num);
						num += 4;
						restartSkirmishMapInfo.aivs[j].builtIn = BitConverter.ToBoolean(data, num);
						num++;
						restartSkirmishMapInfo.aivs[j].community = BitConverter.ToBoolean(data, num);
						num++;
						restartSkirmishMapInfo.aivs[j].historical = BitConverter.ToBoolean(data, num);
						num++;
						if (num2 >= 54)
						{
							restartSkirmishMapInfo.aivs[j].rotation = BitConverter.ToInt32(data, num);
							num += 4;
						}
						else
						{
							restartSkirmishMapInfo.aivs[j].rotation = 0;
						}
						int num8 = BitConverter.ToInt32(data, num);
						num += 4;
						restartSkirmishMapInfo.aivs[j].aivs = new List<CustomisationFileManager.CustomAIV>();
						for (int k = 0; k < num8; k++)
						{
							int num9 = BitConverter.ToInt32(data, num);
							num += 4;
							CustomisationFileManager.CustomAIV item = CustomisationFileManager.CustomAIV.decode(data, num);
							num += num9;
							restartSkirmishMapInfo.aivs[j].aivs.Add(item);
						}
						if (num2 >= 55)
						{
							restartSkirmishMapInfo.aivs[j].builtInLord = BitConverter.ToBoolean(data, num);
							num++;
							if (!restartSkirmishMapInfo.aivs[j].builtInLord)
							{
								int num10 = BitConverter.ToInt32(data, num);
								num += 4;
								CustomisationFileManager.CustomLordConfig lordConfig = CustomisationFileManager.CustomLordConfig.decode(data, num);
								num += num10;
								restartSkirmishMapInfo.aivs[j].lordConfig = lordConfig;
							}
						}
						if (num2 >= 56)
						{
							int num11 = BitConverter.ToInt32(data, num);
							num += 4;
							if (num11 > 0)
							{
								restartSkirmishMapInfo.aivs[j].lordName = Encoding.UTF8.GetString(data, num, num11);
								num += num11;
							}
						}
						else
						{
							restartSkirmishMapInfo.aivs[j].lordName = "";
						}
						if (num2 >= 58)
						{
							int num12 = BitConverter.ToInt32(data, num);
							num += 4;
							if (num12 == 0)
							{
								continue;
							}
							restartSkirmishMapInfo.aivs[j].imageData = new byte[num12];
							Array.Copy(data, num, restartSkirmishMapInfo.aivs[j].imageData, 0, num12);
							num += num12;
							try
							{
								restartSkirmishMapInfo.aivs[j].image = MainViewModel.Instance.LoadImageFile(restartSkirmishMapInfo.aivs[j].imageData);
								if (restartSkirmishMapInfo.aivs[j].image != null && (restartSkirmishMapInfo.aivs[j].image.Width != 144f || restartSkirmishMapInfo.aivs[j].image.Height != 144f))
								{
									restartSkirmishMapInfo.aivs[j].imageData = null;
									restartSkirmishMapInfo.aivs[j].image = null;
								}
							}
							catch (Exception)
							{
							}
						}
						else
						{
							restartSkirmishMapInfo.aivs[j].image = null;
							restartSkirmishMapInfo.aivs[j].imageData = null;
						}
					}
				}
				if (num2 >= 57)
				{
					restartSkirmishMapInfo.customTestMission = BitConverter.ToBoolean(data, num);
					num++;
					restartSkirmishMapInfo.customTrail = BitConverter.ToBoolean(data, num);
					num++;
					restartSkirmishMapInfo.customTrailLevel = BitConverter.ToInt32(data, num);
					num += 4;
					int num13 = BitConverter.ToInt32(data, num);
					num += 4;
					if (num13 > 0)
					{
						restartSkirmishMapInfo.customTrailName = Encoding.UTF8.GetString(data, num, num13);
						num += num13;
					}
					if (num2 >= 58)
					{
						restartSkirmishMapInfo.customTrailDifficulty = BitConverter.ToInt32(data, num);
						num += 4;
					}
					else
					{
						restartSkirmishMapInfo.customTrailDifficulty = 1;
					}
				}
				else
				{
					restartSkirmishMapInfo.customTestMission = false;
					restartSkirmishMapInfo.customTrail = false;
					restartSkirmishMapInfo.customTrailLevel = -1;
					restartSkirmishMapInfo.customTrailName = "";
					restartSkirmishMapInfo.customTrailDifficulty = 1;
				}
				if (restartSkirmishMapInfo.selectedHeader == null && !customTrailMission)
				{
					if (!restartSkirmishMapInfo.customTrail)
					{
						return null;
					}
					restartSkirmishMapInfo.selectedHeader = MapFileManager.Instance.GetHeaderFromCustomTrail(restartSkirmishMapInfo.customTrailName, FRONT_ManageTrail.GetMakerFileName(restartSkirmishMapInfo.customTrailLevel - 1));
					if (restartSkirmishMapInfo.selectedHeader == null)
					{
						return null;
					}
				}
				return restartSkirmishMapInfo;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	public class RestartMPInfo
	{
		public string[] LordNames = new string[8];

		public int[] LordType = new int[8];

		public byte[][] imageData = new byte[8][];

		public TextureSource[] images = new TextureSource[8];

		public RestartMPInfo()
		{
			for (int i = 0; i < 8; i++)
			{
				LordNames[i] = "";
				LordType[i] = 0;
			}
		}

		public void SetImage(byte[] data, int i)
		{
			if (data == null || data.Length == 0)
			{
				return;
			}
			try
			{
				imageData[i] = data;
				images[i] = MainViewModel.Instance.LoadImageFile(imageData[i]);
				if (images[i] != null && (images[i].Width != 144f || images[i].Height != 144f))
				{
					images[i] = null;
					imageData[i] = null;
				}
			}
			catch (Exception)
			{
				imageData[i] = null;
				images[i] = null;
			}
		}

		public byte[] encode()
		{
			List<byte> list = new List<byte>(0);
			list.Add(102);
			for (int i = 0; i < 8; i++)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(LordNames[i]);
				list.AddRange(BitConverter.GetBytes(bytes.Length));
				list.AddRange(bytes);
				list.AddRange(BitConverter.GetBytes(LordType[i]));
				if (imageData[i] != null)
				{
					list.AddRange(BitConverter.GetBytes(imageData[i].Length));
					list.AddRange(imageData[i]);
				}
				else
				{
					int value = 0;
					list.AddRange(BitConverter.GetBytes(value));
				}
			}
			return list.ToArray();
		}

		public static RestartMPInfo decode(byte[] data)
		{
			try
			{
				RestartMPInfo restartMPInfo = new RestartMPInfo();
				int num = 0;
				int num2 = data[num];
				num++;
				for (int i = 0; i < 8; i++)
				{
					int num3 = BitConverter.ToInt32(data, num);
					num += 4;
					restartMPInfo.LordNames[i] = Encoding.UTF8.GetString(data, num, num3);
					num += num3;
					if (num2 >= 101)
					{
						restartMPInfo.LordType[i] = BitConverter.ToInt32(data, num);
						num += 4;
					}
					else
					{
						restartMPInfo.LordType[i] = 0;
					}
					if (num2 < 102)
					{
						continue;
					}
					int num4 = BitConverter.ToInt32(data, num);
					num += 4;
					if (num4 > 0)
					{
						restartMPInfo.imageData[i] = new byte[num4];
						Array.Copy(data, num, restartMPInfo.imageData[i], 0, num4);
						num += num4;
						try
						{
							restartMPInfo.images[i] = MainViewModel.Instance.LoadImageFile(restartMPInfo.imageData[i]);
							if (restartMPInfo.images[i] != null && (restartMPInfo.images[i].Width != 144f || restartMPInfo.images[i].Height != 144f))
							{
								restartMPInfo.images[i] = null;
								restartMPInfo.imageData[i] = null;
							}
						}
						catch (Exception)
						{
							restartMPInfo.images[i] = null;
							restartMPInfo.imageData[i] = null;
						}
					}
					else
					{
						restartMPInfo.imageData[i] = null;
					}
				}
				return restartMPInfo;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	private WGT_Heading RefHeading;

	private Button RefButtonLoad;

	private Button RefButtonWorkshop;

	private Button RefButtonSave;

	private Button RefButtonOptions;

	private Button RefButtonHelp;

	private Button RefButtonRestart;

	private Button RefButtonQuit;

	private Button RefButtonExit;

	private Button RefButtonResume;

	private Noesis.Grid RefLayoutRoot;

	public bool wasPaused;

	private MenuMode menuMode;

	public RestartMapInfo restartMapInfo;

	public RestartSkirmishMapInfo restartSkirmishMapInfo;

	public RestartMPInfo restartMPInfo;

	public HUD_IngameMenu()
	{
		InitializeComponent();
		MainViewModel.Instance.HUDIngameMenu = this;
		RefLayoutRoot = (Noesis.Grid)FindName("LayoutRoot");
		RefHeading = (WGT_Heading)FindName("ScenarioHeader");
		RefButtonLoad = (Button)FindName("ButtonLoad");
		RefButtonWorkshop = (Button)FindName("ButtonWorkshop");
		RefButtonSave = (Button)FindName("ButtonSave");
		RefButtonOptions = (Button)FindName("ButtonOptions");
		RefButtonHelp = (Button)FindName("ButtonHelp");
		RefButtonRestart = (Button)FindName("ButtonRestartMission");
		RefButtonQuit = (Button)FindName("ButtonQuitMission");
		RefButtonExit = (Button)FindName("ButtonExitStronghold");
		RefButtonResume = (Button)FindName("ButtonResume");
	}

	private void InitializeComponent()
	{
		NoesisUnity.LoadComponent(this, "Assets/GUI/XAMLResources/HUD_IngameMenu.xaml");
	}

	protected override bool ConnectEvent(object source, string eventName, string handlerName)
	{
		if (eventName == "MouseEnter" && handlerName == "CommonRedButtonEnter")
		{
			if (source is Button)
			{
				((Button)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			else if (source is RadioButton)
			{
				((RadioButton)source).MouseEnter += MainViewModel.Instance.CommonRedButtonEnter;
			}
			return true;
		}
		return false;
	}

	public void Init()
	{
		if (MainViewModel.Instance.Show_HUD_Help)
		{
			MainViewModel.Instance.HUDHelp.Close();
		}
		if (MainViewModel.Instance.Show_HUD_Options || MainViewModel.Instance.Show_HUD_OptionsMP)
		{
			MainViewModel.Instance.HUDOptions.ButtonClicked(-1);
		}
		MainViewModel.Instance.MPChatVisible = false;
		RefLayoutRoot.Height = 400f;
		if (MainViewModel.Instance.IsMapEditorMode)
		{
			SetAsMapEditor();
		}
		else if (GameData.Instance.multiplayerMap && !Director.instance.SkirmishModeGame)
		{
			SetAsMultiplayer();
		}
		else if (GameData.Instance.game_type == 4)
		{
			SetAsTutorial();
		}
		else
		{
			SetAsNormal();
		}
	}

	private void SetAsNormal()
	{
		menuMode = MenuMode.Normal;
		MainViewModel.Instance.IngameMessageLoadButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 2);
		MainViewModel.Instance.IngameMessageSaveButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 3);
		MainViewModel.Instance.IngameMessageRestartButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 44);
		MainViewModel.Instance.IngameMessageQuitButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 7);
		RefButtonLoad.IsEnabled = true;
		RefButtonSave.IsEnabled = Director.instance.SimRunning;
		RefButtonOptions.IsEnabled = true;
		RefButtonHelp.IsEnabled = true;
		if (GameData.Instance.game_type == 0)
		{
			RefButtonRestart.IsEnabled = true;
		}
		else if (GameData.Instance.game_type == 2)
		{
			RefButtonRestart.IsEnabled = restartMapInfo != null;
		}
		else if (Director.instance.SkirmishModeGame && GameData.Instance.SkirmishGameType != 1)
		{
			RefButtonRestart.IsEnabled = restartSkirmishMapInfo != null;
		}
		else
		{
			RefButtonRestart.IsEnabled = true;
		}
		RefButtonQuit.IsEnabled = true;
		RefButtonExit.IsEnabled = true;
		RefButtonResume.IsEnabled = true;
		RefButtonLoad.Visibility = Visibility.Visible;
		RefButtonWorkshop.Visibility = Visibility.Collapsed;
		RefButtonRestart.Visibility = Visibility.Visible;
		wasPaused = Director.instance.Paused;
		if (!wasPaused)
		{
			Director.instance.SetPausedState(state: true);
		}
	}

	private void SetAsTutorial()
	{
		menuMode = MenuMode.Tutorial;
		MainViewModel.Instance.IngameMessageLoadButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 2);
		MainViewModel.Instance.IngameMessageSaveButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 3);
		MainViewModel.Instance.IngameMessageRestartButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 44);
		MainViewModel.Instance.IngameMessageQuitButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 7);
		RefButtonLoad.IsEnabled = false;
		RefButtonSave.IsEnabled = false;
		RefButtonOptions.IsEnabled = true;
		RefButtonHelp.IsEnabled = true;
		RefButtonRestart.IsEnabled = false;
		RefButtonQuit.IsEnabled = true;
		RefButtonExit.IsEnabled = true;
		RefButtonResume.IsEnabled = true;
		RefButtonLoad.Visibility = Visibility.Visible;
		RefButtonWorkshop.Visibility = Visibility.Collapsed;
		RefButtonRestart.Visibility = Visibility.Visible;
		wasPaused = Director.instance.Paused;
		if (!wasPaused)
		{
			Director.instance.SetPausedState(state: true);
		}
	}

	private void SetAsMapEditor()
	{
		menuMode = MenuMode.Editor;
		MainViewModel.Instance.IngameMessageLoadButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 2);
		MainViewModel.Instance.IngameMessageSaveButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_MAPEDIT, 3);
		MainViewModel.Instance.IngameMessageRestartButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 44);
		if (!EditorDirector.instance.mapChanged)
		{
			MainViewModel.Instance.IngameMessageQuitButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 84);
		}
		else
		{
			MainViewModel.Instance.IngameMessageQuitButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 43);
		}
		RefButtonSave.IsEnabled = Director.instance.SimRunning;
		RefButtonOptions.IsEnabled = true;
		RefButtonHelp.IsEnabled = true;
		RefButtonRestart.IsEnabled = false;
		RefButtonQuit.IsEnabled = true;
		RefButtonExit.IsEnabled = true;
		RefButtonResume.IsEnabled = true;
		RefButtonRestart.Visibility = Visibility.Collapsed;
		RefButtonWorkshop.Visibility = Visibility.Visible;
		if (GameData.Instance.multiplayerMap)
		{
			if (GameData.Instance.lastGameState != null && GameData.Instance.lastGameState.skirmish_map_num_keeps >= 2)
			{
				RefButtonWorkshop.IsEnabled = true;
			}
			else
			{
				RefButtonWorkshop.IsEnabled = false;
			}
		}
		else
		{
			RefButtonWorkshop.IsEnabled = true;
		}
		wasPaused = Director.instance.Paused;
		if (!wasPaused)
		{
			Director.instance.SetPausedState(state: true);
		}
	}

	private void SetAsMultiplayer()
	{
		menuMode = MenuMode.Multiplayer;
		MainViewModel.Instance.IngameMessageLoadButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 2);
		MainViewModel.Instance.IngameMessageSaveButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 3);
		MainViewModel.Instance.IngameMessageRestartButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 44);
		MainViewModel.Instance.IngameMessageQuitButtonText = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 7);
		RefButtonLoad.IsEnabled = false;
		RefButtonSave.IsEnabled = Director.instance.SimRunning && Platform_Multiplayer.Instance.IsHost;
		RefButtonOptions.IsEnabled = true;
		RefButtonHelp.IsEnabled = true;
		RefButtonRestart.IsEnabled = false;
		RefButtonQuit.IsEnabled = true;
		RefButtonExit.IsEnabled = true;
		RefButtonResume.IsEnabled = true;
		RefButtonRestart.Visibility = Visibility.Visible;
		RefButtonLoad.Visibility = Visibility.Visible;
		RefButtonWorkshop.Visibility = Visibility.Collapsed;
	}

	public static void SaveMapEditor(bool fromIngameMenu)
	{
		Action<string, FileHeader> oKAction = delegate(string filename, FileHeader header)
		{
			if (fromIngameMenu)
			{
				MainViewModel.Instance.HUDIngameMenu.Close();
			}
			string text = filename + ".map";
			string path = System.IO.Path.Combine(ConfigSettings.GetUserMapsPath(), text);
			EngineInterface.GameAction(Enums.GameActionCommand.Set_AI_Patrolling, 1, 1);
			EditorDirector.instance.SaveSaveGameOrMap(path, text, lockMap: false, tempLockOnly: false, mapSave: true);
		};
		HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.SaveEditorMap, oKAction, delegate
		{
			if (fromIngameMenu)
			{
				MainViewModel.Instance.Show_HUD_IngameMenu = true;
			}
		});
	}

	private void LoadEditorMap()
	{
		HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadEditorMap, delegate(string filename, FileHeader header)
		{
			Close();
			if (header.isMapEditable())
			{
				EditorDirector.instance.stopGameSim();
				GameData.Instance.SetMissionTextFromHeader(header);
				MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
				EditorDirector.instance.loadMapIntoEditor(header.filePath, header.standAlone_filename);
			}
		}, delegate
		{
			MainViewModel.Instance.Show_HUD_IngameMenu = true;
		});
	}

	public void ButtonIngameMenuFunction(int function)
	{
		switch (function)
		{
		case 1:
		{
			Enums.RequesterTypes reqType;
			Action<string, FileHeader> oKAction;
			switch (menuMode)
			{
			default:
				reqType = Enums.RequesterTypes.SaveSinglePlayerGame;
				oKAction = delegate(string filename, FileHeader header)
				{
					Close();
					string path = filename + ".sav";
					string path2 = System.IO.Path.Combine(ConfigSettings.GetSavesPath(), path);
					EditorDirector.instance.SaveSaveGameOrMap(path2, "");
				};
				break;
			case MenuMode.Multiplayer:
				reqType = Enums.RequesterTypes.SaveMultiplayerGame;
				oKAction = delegate(string filename, FileHeader header)
				{
					Close();
					EngineInterface.TriggerMPSave(filename + ".msv");
				};
				break;
			case MenuMode.Editor:
				SaveMapEditor(fromIngameMenu: true);
				return;
			}
			HUD_LoadSaveRequester.OpenLoadSaveRequester(reqType, oKAction, delegate
			{
				MainViewModel.Instance.Show_HUD_IngameMenu = true;
			});
			Hide();
			break;
		}
		case 2:
			switch (menuMode)
			{
			case MenuMode.Normal:
				HUD_LoadSaveRequester.OpenLoadSaveRequester(Enums.RequesterTypes.LoadSinglePlayerGame, delegate(string filename, FileHeader header)
				{
					Close();
					EditorDirector.instance.stopGameSim();
					Platform_Multiplayer.Instance.gameMembers = null;
					EditorDirector.instance.loadSaveGame(header.filePath, header.standAlone_filename, header);
					MainViewModel.Instance.InitObjectiveGoodsPanelDelayed();
				}, delegate
				{
					MainViewModel.Instance.Show_HUD_IngameMenu = true;
				});
				break;
			case MenuMode.Editor:
			{
				if (!EditorDirector.instance.mapChanged)
				{
					LoadEditorMap();
					break;
				}
				string title2 = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 43);
				SFXManager.instance.playSpeech(1, "General_Quitgame.wav", 1f);
				HUD_ConfirmationPopup.ShowConfirmation(title2, delegate
				{
					LoadEditorMap();
				}, delegate
				{
					MainViewModel.Instance.Show_HUD_IngameMenu = true;
				});
				break;
			}
			}
			Hide();
			break;
		case 99:
			HUD_WorkshopUploader.Open();
			Hide();
			break;
		case 3:
			HUD_Options.OpenOptions(fromIngameMenu: true);
			Hide();
			break;
		case 4:
			if (!ConfigSettings.Settings_UseSteamOverlayForHelp)
			{
				Close();
			}
			HUD_Help.OpenHelp(fromMenu: true, "file://" + Application.dataPath + "/StreamingAssets/Help/help_main.html");
			break;
		case 5:
			Hide();
			HUD_ConfirmationPopup.ShowConfirmation(MainViewModel.Instance.IngameMessageRestartButtonText, delegate
			{
				if (GameData.Instance.game_type == 0)
				{
					EditorDirector.instance.stopGameSim();
					MainViewModel.Instance.StartCampaignMission(GameData.Instance.mission_level);
				}
				else if (GameData.Instance.game_type == 3)
				{
					if (Director.instance.SkirmishModeGame)
					{
						if (GameData.Instance.SkirmishTrailType >= 0)
						{
							EditorDirector.instance.stopGameSim();
							MainViewModel.Instance.FrontEndMenu.StartTrailMission(GameData.Instance.SkirmishTrailLevel, GameData.Instance.SkirmishTrailType);
						}
						else if (restartSkirmishMapInfo != null && (restartSkirmishMapInfo.selectedHeader != null || !restartSkirmishMapInfo.customTrail))
						{
							if (restartSkirmishMapInfo.customTrail)
							{
								MainViewModel.Instance.StartCustomTrailMission(restartSkirmishMapInfo.customTrailName, restartSkirmishMapInfo.customTrailLevel, restartSkirmishMapInfo.customTrailDifficulty);
							}
							else
							{
								MainViewModel.Instance.FRONTMultiplayer.RestartSkirmishGame(restartSkirmishMapInfo);
							}
						}
					}
				}
				else if (restartMapInfo != null)
				{
					EditorDirector.instance.stopGameSim();
					FRONT_StandaloneMission.StartMap(MainViewModel.Instance.HUDIngameMenu.restartMapInfo);
				}
			}, delegate
			{
				MainViewModel.Instance.Show_HUD_IngameMenu = true;
			});
			break;
		case 6:
		{
			Hide();
			string title = ((!MainViewModel.Instance.IsMapEditorMode) ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 7) : (EditorDirector.instance.mapChanged ? Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 43) : Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT2, 84)));
			SFXManager.instance.playSpeech(1, "General_Quitgame.wav", 1f);
			HUD_ConfirmationPopup.ShowConfirmation(title, delegate
			{
				if (GameData.Instance.coopTrailID > 0)
				{
					if (HUD_MPInviteWarning.PendingMPInvite)
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					}
					else
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						if (GameData.Instance.coopTrailID == 1)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop");
						}
						else if (GameData.Instance.coopTrailID == 2)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop2");
						}
						else if (GameData.Instance.coopTrailID == 3)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop3");
						}
						else if (GameData.Instance.coopTrailID == 4)
						{
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Coop4");
						}
					}
				}
				else if (Director.instance.SkirmishModeGame && GameData.Instance.game_type != 0 && (GameData.Instance.SkirmishGameType == 0 || GameData.Instance.SkirmishGameType == 3 || GameData.Instance.SkirmishGameType == 2))
				{
					if (HUD_MPInviteWarning.PendingMPInvite)
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					}
					else
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						if (MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo != null)
						{
							if (!MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrail)
							{
								FRONT_Multiplayer.Open(skirmishSetup: true, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo, coopSetup: false, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTestMission);
								MainViewModel.Instance.Show_FrontMenus_Background_Main = false;
								MainViewModel.Instance.Show_Frontend_MainMenu = false;
								MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo = null;
								MainViewModel.Instance.HUDIngameMenu.restartMPInfo = null;
							}
							else
							{
								MainViewModel.Instance.FrontEndMenu.OpenCustomTrail(MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailName, MainViewModel.Instance.HUDIngameMenu.restartSkirmishMapInfo.customTrailLevel);
							}
						}
					}
				}
				else if (GameData.Instance.game_type == 0)
				{
					if (HUD_MPInviteWarning.PendingMPInvite)
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					}
					else
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
						switch (GameData.Instance.mission_level)
						{
						case 1:
						case 2:
						case 3:
						case 4:
						case 5:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical1");
							break;
						case 6:
						case 7:
						case 8:
						case 9:
						case 10:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical2");
							break;
						case 11:
						case 12:
						case 13:
						case 14:
						case 15:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical3");
							break;
						case 16:
						case 17:
						case 18:
						case 19:
						case 20:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical4");
							break;
						case 21:
						case 22:
						case 23:
						case 24:
						case 25:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical5");
							break;
						case 26:
						case 27:
						case 28:
						case 29:
						case 30:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical6");
							break;
						case 31:
						case 32:
						case 33:
						case 34:
						case 35:
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Historical7");
							break;
						}
					}
				}
				else if (Director.instance.SkirmishModeGame && GameData.Instance.SkirmishGameType == 1)
				{
					if (HUD_MPInviteWarning.PendingMPInvite)
					{
						MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
					}
					else
					{
						switch (GameData.Instance.SkirmishTrailType)
						{
						case 0:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trail");
							break;
						case 1:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trail2");
							break;
						case 2:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Trail3");
							break;
						case 11:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands1");
							break;
						case 12:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands2");
							break;
						case 13:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands3");
							break;
						case 14:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands4");
							break;
						case 15:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands5");
							break;
						case 16:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands6");
							break;
						case 17:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands7");
							break;
						case 18:
							MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
							MainViewModel.Instance.FrontEndMenu.ButtonClicked("Sands8");
							break;
						case 3:
						case 4:
						case 5:
						case 6:
						case 7:
						case 8:
						case 9:
						case 10:
							break;
						}
					}
				}
				else
				{
					MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
				}
			}, delegate
			{
				MainViewModel.Instance.Show_HUD_IngameMenu = true;
			});
			break;
		}
		case 7:
			Hide();
			SFXManager.instance.playSpeech(1, "general_quitgame.wav", 1f);
			HUD_ConfirmationPopup.ShowConfirmation(Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GAME_OPTIONS, 9), delegate
			{
				if (Director.instance.MultiplayerGame)
				{
					Director.instance.ExitAppFromMP();
				}
				else
				{
					FatControler.instance.ExitApp();
				}
			}, delegate
			{
				MainViewModel.Instance.Show_HUD_IngameMenu = true;
			});
			break;
		case 8:
			Close();
			break;
		}
	}

	public void Hide()
	{
		MainViewModel.Instance.Show_HUD_IngameMenu = false;
	}

	public void Close()
	{
		if (MainViewModel.Instance.HUDScenario != null)
		{
			MainViewModel.Instance.HUDScenario.IsEnabled = true;
			MainViewModel.Instance.HUDScenarioPopup.IsEnabled = true;
		}
		MainViewModel.Instance.Show_HUD_IngameMenu = false;
		if (!wasPaused)
		{
			Director.instance.SetPausedState(state: false);
		}
	}
}

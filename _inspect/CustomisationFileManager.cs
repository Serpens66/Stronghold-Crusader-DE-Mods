using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrusaderDE;
using Noesis;
using UnityEngine;

public class CustomisationFileManager
{
	public class CustomLord
	{
		public int lordType = -1;

		public string lordName;

		public string lordDisplayName;

		public string customPath;

		public bool workshopUploadInfoAvailable;

		public bool workshop;

		public byte[] imageData;

		public TextureSource image;

		public string imagePath;

		public List<CustomAIV> aivs = new List<CustomAIV>();

		public List<CustomLordConfig> configs = new List<CustomLordConfig>();
	}

	public class CustomAIV
	{
		public int lordType;

		public string AIVName;

		public bool builtIn;

		public bool workshopUploadInfoAvailable;

		public bool workshop;

		public string path;

		public ulong checksum;

		public short[] data;

		public byte[] encode()
		{
			List<byte> list = new List<byte>(0);
			list.Add(1);
			list.AddRange(BitConverter.GetBytes(lordType));
			list.AddRange(BitConverter.GetBytes(builtIn));
			list.AddRange(BitConverter.GetBytes(checksum));
			byte[] bytes = Encoding.UTF8.GetBytes(AIVName);
			list.AddRange(BitConverter.GetBytes(bytes.Length));
			list.AddRange(bytes);
			list.AddRange(BitConverter.GetBytes(data.Length));
			for (int i = 0; i < data.Length; i++)
			{
				list.AddRange(BitConverter.GetBytes(data[i]));
			}
			return list.ToArray();
		}

		public static CustomAIV decode(byte[] data, int offset)
		{
			CustomAIV customAIV = new CustomAIV();
			_ = data[offset];
			offset++;
			customAIV.lordType = BitConverter.ToInt32(data, offset);
			offset += 4;
			customAIV.builtIn = BitConverter.ToBoolean(data, offset);
			offset++;
			customAIV.checksum = BitConverter.ToUInt64(data, offset);
			offset += 8;
			int num = BitConverter.ToInt32(data, offset);
			offset += 4;
			customAIV.AIVName = Encoding.UTF8.GetString(data, offset, num);
			offset += num;
			int num2 = BitConverter.ToInt32(data, offset);
			offset += 4;
			customAIV.data = new short[num2];
			for (int i = 0; i < num2; i++)
			{
				customAIV.data[i] = BitConverter.ToInt16(data, offset);
				offset += 2;
			}
			return customAIV;
		}
	}

	public class CustomLordConfig
	{
		public string name;

		public string path;

		public int lordType;

		public ulong checksum;

		public bool workshop;

		public bool workshopUploadInfoAvailable;

		public EngineInterface.AILordConfigTransferData lordData;

		public byte[] encode()
		{
			List<byte> list = new List<byte>(0);
			list.Add(1);
			list.AddRange(BitConverter.GetBytes(lordType));
			list.AddRange(BitConverter.GetBytes(checksum));
			byte[] bytes = Encoding.UTF8.GetBytes(name);
			list.AddRange(BitConverter.GetBytes(bytes.Length));
			list.AddRange(bytes);
			byte[] array = EngineInterface.EncodeLordConfig(ref lordData);
			list.AddRange(BitConverter.GetBytes(array.Length));
			list.AddRange(array);
			return list.ToArray();
		}

		public static CustomLordConfig decode(byte[] data, int offset = 0)
		{
			CustomLordConfig customLordConfig = new CustomLordConfig();
			_ = data[offset];
			offset++;
			customLordConfig.lordType = BitConverter.ToInt32(data, offset);
			offset += 4;
			customLordConfig.checksum = BitConverter.ToUInt64(data, offset);
			offset += 8;
			int num = BitConverter.ToInt32(data, offset);
			offset += 4;
			customLordConfig.name = Encoding.UTF8.GetString(data, offset, num);
			offset += num;
			BitConverter.ToInt32(data, offset);
			offset += 4;
			EngineInterface.DecodeLordConfig(ref customLordConfig.lordData, data, offset);
			return customLordConfig;
		}
	}

	public class CustomMediaData
	{
		public string lordName = "";

		public string[] tags = new string[33];
	}

	public class LordRoot
	{
		public NewAIC lord = new NewAIC();
	}

	[Serializable]
	public class NewAIC
	{
		public int opponent_type;

		public int opponent_type_for_speech;

		public int lord_gfx_type;

		public int flag_type;

		public int use_of_religion;

		public int use_of_ale;

		public int vlow_popularity;

		public int low_popularity;

		public int high_popularity;

		public int min_tax;

		public int max_tax;

		public int[] farm_types = new int[8];

		public int people_to_farm_ratio;

		public int extract_wood_ratio;

		public int extract_stone_ratio;

		public int extract_iron_ratio;

		public int extract_pitch_ratio;

		public int max_quarries;

		public int max_mines;

		public int max_woodcutters;

		public int max_pitch_dugouts;

		public int max_farms;

		public int build_rate;

		public int crushed_building_delay;

		public int sell_food_at;

		public int buy_apples_at;

		public int buy_cheese_at;

		public int buy_bread_at;

		public int buy_wheat_at;

		public int buy_hops_at;

		public int buy_food_amount;

		public int buy_weapons;

		public int pester_for_goods_delay;

		public int send_goods_margin;

		public int ration_boost;

		public int trade_wood_at;

		public int trade_stone_at;

		public int trade_resources_at;

		public int trade_flour_at;

		public int trade_weapons_at;

		public int trade_ale_at;

		public int trade_pitch_at;

		public int trade_minimum;

		public int base_gold_reserves;

		public int blacksmiths_make;

		public int fletchers_make;

		public int poleturners_make;

		public int[] sell_all = new int[15];

		public int move_mobile_defenders;

		public int max_mobile_groups;

		public int buy_defense_machines_at;

		public int buy_defense_machines_delay;

		public int dog_release_timing;

		public int dog_points_count;

		public int[] chance_of_defensive = new int[3];

		public int[] chance_of_harrasment = new int[3];

		public int[] chance_of_seiging = new int[3];

		public int economy_protection_number;

		public int economy_protection_type;

		public int bodyguard_number;

		public int bodyguard_type;

		public int moat_diggers;

		public int moat_digger_type;

		public int[] troop_production_rate = new int[3];

		public int defense_patrol_trigger_level;

		public int defense_patrols;

		public int defense_patrol_style;

		public int defense_patrol_delay;

		public int defensive_trigger_level;

		public int[] defensive_troops = new int[8];

		public int harrasment_trigger_level;

		public int harrasment_trigger_variance;

		public int[] harrasment_troops = new int[8];

		public int[] harrasment_machines = new int[8];

		public int max_harrasment_machines;

		public int harrass_delay;

		public int siege_trigger_level;

		public int siege_trigger_variance;

		public int siege_troops_before_will_come_to_rescue;

		public int siege_troops_on_site_percent;

		public int siege_troops_at_home_percent;

		public int siege_soften_up_delay;

		public int siege_victory_delay;

		public int percent_chance_waiting_for_joint_attack;

		public int[] siege_machines = new int[8];

		public int siege_cow_timer;

		public int siege_eng_amount;

		public int siege_moat_troop;

		public int siege_moat_amount;

		public int siege_herring_troop;

		public int siege_herring_amount;

		public int siege_assasin_amount;

		public int siege_ladder_amount;

		public int siege_tunnel_amount;

		public int siege_storm_troop;

		public int siege_storm_amount;

		public int siege_storm_tribes;

		public int siege_cover_troop;

		public int siege_cover_amount;

		public int siege_cover_tribes;

		public int siege_shock_troop;

		public int siege_shock_amount;

		public int siege_reserve_troop;

		public int siege_reserve_amount;

		public int siege_reserve_tribes;

		public int[] siege_wall_troops = new int[24];

		public int siege_wall_amount;

		public int siege_wall_tribes;

		public int who_to_pick_on;

		public int use_improved_sieging;

		public int[] starting_troops_normal = new int[28];

		public int[] starting_troops_deathmatch = new int[28];

		public int[] starting_troops_crusader = new int[28];

		public int lord_power_display_level;

		public int lord_hps_percent;

		public int siege_max_troops = 200;

		public int siege_normal_wave_multiplier = 5;

		public int siege_high_gold_wave_multiplier = 7;
	}

	public static readonly CustomisationFileManager instance;

	public bool filesChanged;

	public Dictionary<int, CustomLord> extendedLords = new Dictionary<int, CustomLord>();

	public Dictionary<string, CustomLord> customLords = new Dictionary<string, CustomLord>();

	public Dictionary<string, CustomMediaData> customMediaData = new Dictionary<string, CustomMediaData>();

	public static bool CustomMediaExists;

	public StringBuilder debugOutput = new StringBuilder();

	public bool CustomisationLoaded;

	public string[] mediaTags = new string[33]
	{
		"taunt1", "taunt2", "taunt3", "taunt4", "angry_siege_lost", "angry_castle_damaged", "defeat", "nerv_pre_siege", "nerv_weak", "victory_good",
		"victory_harass", "kill_player", "kill_npc", "request_goods", "thank_goods", "die_ally", "congrats_on_kill", "boast_of_kill", "ally_need_help", "",
		"", "", "about2siege", "cant_attack", "wont_attack", "cant_help", "wont_help", "not_sending_goods", "sent_goods", "team_winning",
		"team_losing", "will_send_troops", "will_attack_enemy"
	};

	public FileSystemWatcher extendedLordWatcher;

	public FileSystemWatcher customLordWatcher;

	public bool watchersCreated;

	public static CustomisationFileManager Instance => instance;

	static CustomisationFileManager()
	{
		instance = new CustomisationFileManager();
		CustomMediaExists = false;
	}

	public List<CustomLord> GetCustomLords(bool includeWorkshop = true)
	{
		List<CustomLord> list = new List<CustomLord>();
		foreach (KeyValuePair<string, CustomLord> customLord in customLords)
		{
			if (includeWorkshop || !customLord.Value.workshop)
			{
				list.Add(customLord.Value);
			}
		}
		return list;
	}

	public int GetCustomLordsCount()
	{
		return customLords.Count;
	}

	public void BuildFileLists()
	{
		extendedLords.Clear();
		customLords.Clear();
		string userExtendedLordsPath = ConfigSettings.GetUserExtendedLordsPath();
		try
		{
			string[] directories = Directory.GetDirectories(userExtendedLordsPath);
			foreach (string subdirectory in directories)
			{
				BuildExtendedLordDirectory(subdirectory);
			}
		}
		catch (Exception ex)
		{
			debugOutput.AppendLine("Cannot Scan User Extended Lords : " + userExtendedLordsPath);
			debugOutput.AppendLine(ex.Message);
		}
		string userCustomLordsPath = ConfigSettings.GetUserCustomLordsPath();
		try
		{
			string[] directories = Directory.GetDirectories(userCustomLordsPath);
			foreach (string subdirectory2 in directories)
			{
				BuildCustomLordDirectory(subdirectory2);
			}
		}
		catch (Exception ex2)
		{
			debugOutput.AppendLine("Cannot Scan User Extended Lords : " + userExtendedLordsPath);
			debugOutput.AppendLine(ex2.Message);
		}
		if (SteamManager.Initialized)
		{
			try
			{
				List<string> listOfSubscribedItemsPaths = Platform_Workshop.Instance.GetListOfSubscribedItemsPaths();
				if (listOfSubscribedItemsPaths != null)
				{
					foreach (string item in listOfSubscribedItemsPaths)
					{
						string[] directories = Directory.GetDirectories(item);
						foreach (string text in directories)
						{
							DirectoryInfo directoryInfo = new DirectoryInfo(text);
							int lordTypeFromName = getLordTypeFromName(directoryInfo.Name);
							CustomLord value;
							if (lordTypeFromName < 0)
							{
								BuildCustomLordDirectory(text, isWorkshop: true);
							}
							else if (extendedLords.TryGetValue(lordTypeFromName, out value))
							{
								if (value != null)
								{
									string[] files = Directory.GetFiles(text, "*");
									foreach (string text2 in files)
									{
										ProcessExtendedLordFile(text2.ToLower(), text2, value, workshop: true);
									}
								}
							}
							else
							{
								Debug.Log((object)("Unknown Workshop Lord : " + lordTypeFromName));
							}
						}
					}
				}
			}
			catch (Exception ex3)
			{
				debugOutput.AppendLine("Cannot Scan Workshop Lords");
				debugOutput.AppendLine(ex3.Message);
			}
		}
		string userCustomMediaPath = ConfigSettings.GetUserCustomMediaPath();
		if (Directory.Exists(userCustomMediaPath))
		{
			CustomMediaExists = true;
			try
			{
				string[] directories = Directory.GetDirectories(userCustomMediaPath);
				foreach (string text3 in directories)
				{
					string text4 = Path.Combine(text3, "text.txt");
					if (File.Exists(text4))
					{
						AddCustomMediaText(text3, text4);
					}
				}
			}
			catch (Exception ex4)
			{
				debugOutput.AppendLine("Cannot Scan Custom Media : " + userCustomMediaPath);
				debugOutput.AppendLine(ex4.Message);
			}
		}
		filesChanged = false;
		CustomisationLoaded = true;
		CreateWatchers();
	}

	public void BuildExtendedLordDirectory(string subdirectory)
	{
		string name = new DirectoryInfo(subdirectory).Name;
		CustomLord customLord = new CustomLord();
		customLord.lordName = name;
		customLord.customPath = subdirectory;
		customLord.lordType = getLordTypeFromName(name);
		if (customLord.lordType < 0)
		{
			return;
		}
		extendedLords[customLord.lordType] = customLord;
		Translate.Instance.GetLordName(customLord.lordType);
		for (int i = 0; i < 8; i++)
		{
			short[] aIVData = AIVLoader.getAIVData(customLord.lordType, i);
			CustomAIV customAIV = new CustomAIV();
			customAIV.AIVName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 9) + " " + (i + 1);
			customAIV.lordType = customLord.lordType;
			customAIV.data = aIVData;
			customAIV.builtIn = true;
			customAIV.checksum = (ulong)(i + 1);
			customLord.aivs.Add(customAIV);
		}
		if (customLord.lordType < 16)
		{
			for (int j = 0; j < 8; j++)
			{
				short[] aIVData2 = AIVLoader.getAIVData(customLord.lordType, j, evreySkirmishSet: true);
				CustomAIV customAIV2 = new CustomAIV();
				customAIV2.AIVName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 10) + " " + (j + 1);
				customAIV2.lordType = customLord.lordType;
				customAIV2.data = aIVData2;
				customAIV2.builtIn = true;
				customAIV2.checksum = (ulong)(j + 51);
				customLord.aivs.Add(customAIV2);
			}
			for (int k = 0; k < 1; k++)
			{
				short[] aIVData3 = AIVLoader.getAIVData(customLord.lordType, k, evreySkirmishSet: false, evreyHistoricalSet: true);
				CustomAIV customAIV3 = new CustomAIV();
				customAIV3.AIVName = Translate.Instance.lookUpText(Enums.eTextSections.TEXT_CUSTOMISATION, 11);
				customAIV3.lordType = customLord.lordType;
				customAIV3.data = aIVData3;
				customAIV3.builtIn = true;
				customAIV3.checksum = (ulong)(k + 61);
				customLord.aivs.Add(customAIV3);
			}
		}
		string[] files = Directory.GetFiles(subdirectory, "*");
		foreach (string text in files)
		{
			ProcessExtendedLordFile(text.ToLower(), text, customLord);
		}
	}

	public void BuildCustomLordDirectory(string subdirectory, bool isWorkshop = false)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(subdirectory);
		string name = directoryInfo.Name;
		CustomLord customLord = new CustomLord();
		customLord.lordDisplayName = (customLord.lordName = name);
		if (isWorkshop)
		{
			DirectoryInfo parent = directoryInfo.Parent;
			name = (customLord.lordName = parent.Name + "\\" + name);
		}
		customLord.customPath = subdirectory;
		customLord.lordType = -1;
		customLord.workshop = isWorkshop;
		if (!isWorkshop && File.Exists(Path.Combine(subdirectory, directoryInfo.Name + ".data")))
		{
			customLord.workshopUploadInfoAvailable = true;
		}
		string[] files = Directory.GetFiles(subdirectory, "*");
		foreach (string text in files)
		{
			ProcessExtendedLordFile(text.ToLower(), text, customLord);
		}
		if (customLord.aivs.Count > 0 && customLord.configs.Count > 0)
		{
			customLords[customLord.lordName] = customLord;
		}
	}

	public void ProcessExtendedLordFile(string file, string realFileName, CustomLord customLord, bool workshop = false)
	{
		file = file.Replace('/', '\\');
		switch (Path.GetExtension(file))
		{
		case ".aivjson":
		{
			string text2 = "";
			using (FileStream stream2 = new FileStream(file, FileMode.Open, FileAccess.Read))
			{
				using StreamReader streamReader2 = new StreamReader(stream2);
				text2 = streamReader2.ReadToEnd();
			}
			if (text2.Length <= 0)
			{
				break;
			}
			try
			{
				short[] rawData = JsonUtility.FromJson<AIVLoader.SaveData>(text2).GetRawData();
				CustomAIV customAIV = new CustomAIV();
				customAIV.AIVName = Path.GetFileNameWithoutExtension(file);
				customAIV.path = Path.GetDirectoryName(file);
				customAIV.lordType = customLord.lordType;
				customAIV.data = rawData;
				customAIV.checksum = createCRC(customAIV.AIVName, rawData);
				customAIV.workshop = workshop;
				if (File.Exists(file.Replace(".aivjson", ".data")))
				{
					customAIV.workshopUploadInfoAvailable = true;
				}
				customLord.aivs.Add(customAIV);
				break;
			}
			catch (Exception)
			{
				break;
			}
		}
		case ".lordjson":
		{
			string text = "";
			using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read))
			{
				using StreamReader streamReader = new StreamReader(stream);
				text = streamReader.ReadToEnd();
			}
			if (text.Length <= 0)
			{
				break;
			}
			try
			{
				LordRoot lordRoot = JsonUtility.FromJson<LordRoot>(text);
				CustomLordConfig customLordConfig = new CustomLordConfig();
				customLordConfig.name = Path.GetFileNameWithoutExtension(file);
				customLordConfig.path = Path.GetDirectoryName(file);
				customLordConfig.lordType = customLord.lordType;
				customLordConfig.lordData = EngineInterface.CreateAILordConfigData(lordRoot.lord);
				customLordConfig.checksum = createCRC(customLordConfig.name, ref customLordConfig.lordData);
				customLordConfig.workshop = workshop;
				if (File.Exists(file.Replace(".lordjson", ".ldata")))
				{
					customLordConfig.workshopUploadInfoAvailable = true;
				}
				customLord.configs.Add(customLordConfig);
				break;
			}
			catch (Exception)
			{
				break;
			}
		}
		case ".png":
			try
			{
				if (!(Path.GetFileName(file).ToLower() == "avatar.png") || !File.Exists(file))
				{
					break;
				}
				byte[] array = File.ReadAllBytes(file);
				if (array.Length >= 80000)
				{
					break;
				}
				customLord.image = MainViewModel.Instance.LoadImageFile(array);
				if ((BaseComponent)(object)customLord.image != (BaseComponent)null)
				{
					if (((ImageSource)customLord.image).Width == 144f && ((ImageSource)customLord.image).Height == 144f)
					{
						customLord.imageData = array;
						customLord.imagePath = file;
					}
					else
					{
						customLord.image = null;
					}
				}
				break;
			}
			catch (Exception)
			{
				break;
			}
		}
	}

	public void AddCustomMediaText(string subDirectory, string textPath)
	{
		try
		{
			string name = new DirectoryInfo(subDirectory).Name;
			CustomMediaData customMediaData = new CustomMediaData();
			customMediaData.lordName = name;
			string[] array = File.ReadAllLines(textPath);
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split('=', StringSplitOptions.None);
				if (array2.Length <= 1)
				{
					continue;
				}
				string text = array2[0].ToLower();
				text = text.Trim();
				for (int j = 0; j < 33; j++)
				{
					if (!(text == mediaTags[j]))
					{
						continue;
					}
					string text2 = array2[1];
					if (array2.Length > 2)
					{
						for (int k = 2; k < array2.Length; k++)
						{
							text2 += array2[k];
						}
					}
					text2 = text2.Trim();
					text2 = text2.Trim('"');
					if (text2.Length > 0)
					{
						customMediaData.tags[j] = text2;
					}
					break;
				}
			}
			this.customMediaData[name.ToLower()] = customMediaData;
		}
		catch (Exception)
		{
		}
	}

	public string GetCustomLordText(string lordName, int ID)
	{
		lordName = MapFileManager.SplitCustomTrailName(lordName);
		if (customMediaData.TryGetValue(lordName, out var value))
		{
			ID--;
			if (value.tags[ID] != null && value.tags[ID].Length > 0)
			{
				return value.tags[ID];
			}
		}
		return null;
	}

	public TextureSource GetCustomLordImage(string lordName)
	{
		if (customLords.TryGetValue(lordName, out var value))
		{
			return value.image;
		}
		return null;
	}

	public TextureSource GetCustomLordImage(string lordName, ref byte[] imageData)
	{
		imageData = null;
		if (customLords.TryGetValue(lordName, out var value))
		{
			imageData = value.imageData;
			return value.image;
		}
		return null;
	}

	public ulong createCRC(string fileName, short[] data)
	{
		uint num = EngineInterface.crc(Encoding.UTF8.GetBytes(fileName));
		uint num2 = EngineInterface.crc(data);
		return ((ulong)num << 32) | num2;
	}

	public ulong createCRC(string fileName, ref EngineInterface.AILordConfigTransferData td)
	{
		uint num = EngineInterface.crc(Encoding.UTF8.GetBytes(fileName));
		uint num2 = EngineInterface.crc(EngineInterface.EncodeLordConfig(ref td));
		return ((ulong)num << 32) | num2;
	}

	public void ProcessCustomLordFile(string file, string realFileName)
	{
	}

	public int getLordTypeFromName(string lordName)
	{
		switch (lordName.ToLower())
		{
		case "rat":
			return 0;
		case "snake":
			return 1;
		case "pig":
			return 2;
		case "wolf":
			return 3;
		case "saladin":
			return 4;
		case "caliph":
			return 5;
		case "sultan":
			return 6;
		case "richard":
			return 7;
		case "frederick":
			return 8;
		case "phillip":
		case "philip":
			return 9;
		case "wazir":
			return 10;
		case "emir":
			return 11;
		case "nizar":
			return 12;
		case "sheriff":
			return 13;
		case "marshal":
			return 14;
		case "abbot":
			return 15;
		case "jewel":
			return 16;
		case "sentinel":
			return 17;
		case "nomad":
			return 18;
		case "kahinah":
			return 19;
		case "canary":
			return 20;
		case "trader":
			return 21;
		case "sergeant":
			return 22;
		case "lioness":
			return 23;
		case "crocodile":
			return 24;
		case "baldwin":
			return 25;
		case "bullseye":
			return 26;
		default:
			return -1;
		}
	}

	public List<CustomAIV> getLordAIVList(int lordID = -1, string lordName = "")
	{
		if (lordID >= 0)
		{
			if (extendedLords.TryGetValue(lordID, out var value))
			{
				return value.aivs;
			}
			return null;
		}
		if (lordName.Length > 0 && customLords.TryGetValue(lordName, out var value2))
		{
			return value2.aivs;
		}
		return null;
	}

	public List<CustomLordConfig> getLordLordList(int lordID = -1, string lordName = "")
	{
		if (lordID >= 0)
		{
			if (extendedLords.TryGetValue(lordID, out var value))
			{
				return value.configs;
			}
			return null;
		}
		if (lordName.Length > 0 && customLords.TryGetValue(lordName, out var value2))
		{
			return value2.configs;
		}
		return null;
	}

	public bool doesCustomLordExist(string lordname)
	{
		return customLords.ContainsKey(lordname);
	}

	public void CreateWatchers()
	{
		if (!watchersCreated)
		{
			watchersCreated = true;
			string userExtendedLordsPath = ConfigSettings.GetUserExtendedLordsPath();
			extendedLordWatcher = new FileSystemWatcher(userExtendedLordsPath);
			extendedLordWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
			extendedLordWatcher.Changed += OnChanged;
			extendedLordWatcher.Created += OnChanged;
			extendedLordWatcher.Deleted += OnChanged;
			extendedLordWatcher.Renamed += OnRenamed;
			extendedLordWatcher.Filter = "*";
			extendedLordWatcher.IncludeSubdirectories = true;
			extendedLordWatcher.EnableRaisingEvents = true;
			string userCustomLordsPath = ConfigSettings.GetUserCustomLordsPath();
			customLordWatcher = new FileSystemWatcher(userCustomLordsPath);
			customLordWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
			customLordWatcher.Changed += OnChanged;
			customLordWatcher.Created += OnChanged;
			customLordWatcher.Deleted += OnChanged;
			customLordWatcher.Renamed += OnRenamed;
			customLordWatcher.Filter = "*";
			customLordWatcher.IncludeSubdirectories = true;
			customLordWatcher.EnableRaisingEvents = true;
		}
	}

	public void OnChanged(object sender, FileSystemEventArgs e)
	{
		filesChanged = true;
	}

	public void OnRenamed(object sender, RenamedEventArgs e)
	{
		filesChanged = true;
	}
}

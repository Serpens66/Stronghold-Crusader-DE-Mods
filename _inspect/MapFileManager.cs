using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using CrusaderDE;
using UnityEngine;

public class MapFileManager
{
	public class CustomTrailInfo
	{
		public string Name = "";

		public string DisplayName = "";

		public string FullPath = "";

		public bool workshop;

		public bool workshopUploadInfoAvailable;

		public Dictionary<string, FileHeader> headers = new Dictionary<string, FileHeader>();

		public int Count => headers.Count;
	}

	public static readonly MapFileManager instance;

	public Thread fileThread;

	public string UserMapsPath;

	public string UserWorkshopPath;

	public string SavesPath;

	public string TrailMakerPath;

	public string CustomTrailsPath;

	public StringBuilder debugOutput = new StringBuilder();

	public bool fileListLoaded;

	public bool fileListComplete;

	public Dictionary<string, FileHeader> UserInvasion = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> WorkshopInvasion = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> BuiltInInvasion = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> UserFreeBuilds = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> WorkshopFreeBuilds = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> BuiltInFreeBuilds = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> UserMP = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> WorkshopMP = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> BuiltInMP = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> SaveFiles = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> SaveMPFiles = new Dictionary<string, FileHeader>();

	public Dictionary<string, FileHeader> TrailMakerFiles = new Dictionary<string, FileHeader>();

	public Dictionary<string, CustomTrailInfo> CustomTrails = new Dictionary<string, CustomTrailInfo>();

	public Dictionary<string, FileHeader> UserWorkshopUploads = new Dictionary<string, FileHeader>();

	public bool pauseTrailMakerWatcher;

	public FileSystemWatcher userMapsWatcher;

	public FileSystemWatcher userSavesWatcher;

	public FileSystemWatcher userWorkshopUploadsWatcher;

	public FileSystemWatcher trailMakerWatcher;

	public int[] classicSortOrder8 = new int[9] { 0, 1, 7, 6, 5, 4, 3, 2, 8 };

	public int[] classicSortOrder7 = new int[9] { 0, 1, 6, 5, 4, 3, 2, 8, 7 };

	public int[] classicSortOrder6 = new int[9] { 0, 1, 5, 4, 3, 2, 8, 7, 6 };

	public int[] classicSortOrder5 = new int[9] { 0, 1, 4, 3, 2, 8, 7, 6, 5 };

	public int[] classicSortOrder4 = new int[9] { 0, 1, 3, 2, 8, 7, 6, 5, 4 };

	public int[] classicSortOrder3 = new int[9] { 0, 1, 2, 8, 7, 6, 5, 4, 3 };

	public int[] classicSortOrder2 = new int[9] { 0, 1, 8, 7, 6, 5, 4, 3, 2 };

	public Texture2D radarPreviewTexture;

	public Texture2D radarLoadSavePreviewTexture;

	public const int RADARPREVIEW_TEXTURE_SIZE = 200;

	public static MapFileManager Instance => instance;

	static MapFileManager()
	{
		instance = new MapFileManager();
	}

	public void BuildFileList()
	{
		UserMapsPath = ConfigSettings.GetUserMapsPath();
		UserWorkshopPath = ConfigSettings.GetUserWorkshopPath();
		SavesPath = ConfigSettings.GetSavesPath();
		TrailMakerPath = ConfigSettings.GetUserTrailMakerPath();
		CustomTrailsPath = ConfigSettings.GetUserCustomTrailsPath();
		fileThread = new Thread(runMapLoading);
		fileThread.Name = "StrongholdMapLoading";
		fileThread.Start();
	}

	public void runMapLoading()
	{
		BuildFileList("map");
		CreateWatchers();
	}

	public void BuildFileList(string fileType)
	{
		string text = UserMapsPath;
		try
		{
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
		}
		catch (Exception ex)
		{
			debugOutput.AppendLine("Cannot Create User Maps Folder : " + text);
			debugOutput.AppendLine(ex.Message);
		}
		try
		{
			string[] files = Directory.GetFiles(text, "*." + fileType);
			foreach (string text2 in files)
			{
				UpdateUserMap(text2.ToLower(), text2);
			}
		}
		catch (Exception ex2)
		{
			debugOutput.AppendLine("Cannot Scan User Maps : " + text + " : " + fileType);
			debugOutput.AppendLine(ex2.Message);
		}
		try
		{
			text = Path.Combine(Application.streamingAssetsPath, "Maps");
			string[] files = Directory.GetFiles(text, "*." + fileType);
			foreach (string text3 in files)
			{
				UpdateBuiltinMap(text3.ToLower(), text3);
			}
		}
		catch (Exception ex3)
		{
			debugOutput.AppendLine("Cannot Scan Built in Maps : " + text + " : " + fileType);
			debugOutput.AppendLine(ex3.Message);
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
						string[] files = Directory.GetFiles(item, "*." + fileType);
						foreach (string text4 in files)
						{
							UpdateWorkshopMap(text4.ToLower(), text4);
						}
						files = Directory.GetDirectories(item);
						foreach (string text5 in files)
						{
							try
							{
								DirectoryInfo directoryInfo = new DirectoryInfo(text5);
								CustomTrailInfo customTrailInfo = new CustomTrailInfo();
								string text6 = string.Concat(str2: customTrailInfo.DisplayName = directoryInfo.Name, str0: directoryInfo.Parent.Name, str1: "\\");
								customTrailInfo.FullPath = text5;
								customTrailInfo.workshop = true;
								customTrailInfo.Name = text6;
								CustomTrails[text6] = customTrailInfo;
								string[] files2 = Directory.GetFiles(text5, "*.trail");
								foreach (string text7 in files2)
								{
									UpdateCustomTrailFile(text6, text7.ToLower(), text7);
								}
								if (CustomTrails[text6].Count == 0)
								{
									CustomTrails.Remove(text6);
								}
								else if (File.Exists(Path.Combine(text5, text6 + ".data")))
								{
									customTrailInfo.workshopUploadInfoAvailable = true;
								}
							}
							catch (Exception ex4)
							{
								debugOutput.AppendLine("Cannot Scan Custom trail files : " + text5);
								debugOutput.AppendLine(ex4.Message);
							}
						}
					}
				}
			}
			catch (Exception ex5)
			{
				debugOutput.AppendLine("Cannot Scan Workshop Maps");
				debugOutput.AppendLine(ex5.Message);
			}
			try
			{
				text = UserWorkshopPath;
				string[] files = Directory.GetFiles(text, "*.map");
				foreach (string text8 in files)
				{
					UpdateUserWorkshopUploads(text8.ToLower(), text8);
				}
			}
			catch (Exception ex6)
			{
				debugOutput.AppendLine("Cannot Scan User Workshop Maps : " + text + " : .map");
				debugOutput.AppendLine(ex6.Message);
			}
		}
		text = SavesPath;
		try
		{
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
		}
		catch (Exception ex7)
		{
			debugOutput.AppendLine("Cannot Create User Saves Folder : " + text);
			debugOutput.AppendLine(ex7.Message);
		}
		try
		{
			string[] files = Directory.GetFiles(text, "*.sav");
			foreach (string text9 in files)
			{
				UpdateUserSave(text9.ToLower(), text9);
			}
		}
		catch (Exception ex8)
		{
			debugOutput.AppendLine("Cannot Scan User Saves : " + text + " : .sav");
			debugOutput.AppendLine(ex8.Message);
		}
		try
		{
			string[] files = Directory.GetFiles(text, "*.msv");
			foreach (string text10 in files)
			{
				UpdateUserMPSave(text10.ToLower(), text10);
			}
		}
		catch (Exception ex9)
		{
			debugOutput.AppendLine("Cannot Scan User MP saves : " + text + " : .msv");
			debugOutput.AppendLine(ex9.Message);
		}
		text = TrailMakerPath;
		try
		{
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
		}
		catch (Exception ex10)
		{
			debugOutput.AppendLine("Cannot Create Trail Maker Folder : " + text);
			debugOutput.AppendLine(ex10.Message);
		}
		try
		{
			string[] files = Directory.GetFiles(text, "*.trail");
			foreach (string text11 in files)
			{
				UpdateTrailMakerFile(text11.ToLower(), text11);
			}
		}
		catch (Exception ex11)
		{
			debugOutput.AppendLine("Cannot Scan trail Maker missions : " + text);
			debugOutput.AppendLine(ex11.Message);
		}
		text = CustomTrailsPath;
		try
		{
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
		}
		catch (Exception ex12)
		{
			debugOutput.AppendLine("Cannot Create Custom Trails Folder : " + text);
			debugOutput.AppendLine(ex12.Message);
		}
		try
		{
			string[] files = Directory.GetDirectories(text);
			foreach (string text12 in files)
			{
				try
				{
					string name = new DirectoryInfo(text12).Name;
					CustomTrailInfo customTrailInfo2 = new CustomTrailInfo();
					customTrailInfo2.DisplayName = (customTrailInfo2.Name = name);
					customTrailInfo2.FullPath = text12;
					CustomTrails[name] = customTrailInfo2;
					string[] files2 = Directory.GetFiles(text12, "*.trail");
					foreach (string text13 in files2)
					{
						UpdateCustomTrailFile(name, text13.ToLower(), text13);
					}
					if (CustomTrails[name].Count == 0)
					{
						CustomTrails.Remove(name);
					}
					else if (File.Exists(Path.Combine(text12, name + ".data")))
					{
						customTrailInfo2.workshopUploadInfoAvailable = true;
					}
				}
				catch (Exception ex13)
				{
					debugOutput.AppendLine("Cannot Scan Custom trail files : " + text12);
					debugOutput.AppendLine(ex13.Message);
				}
			}
		}
		catch (Exception ex14)
		{
			debugOutput.AppendLine("Cannot Scan Custom trail files : " + text);
			debugOutput.AppendLine(ex14.Message);
		}
		fileListLoaded = true;
	}

	public void UpdateUserMap(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 0);
		if (fileInfoFromFileName == null || fileInfoFromFileName.missionMap)
		{
			return;
		}
		if (fileInfoFromFileName.mapType == 1)
		{
			UserMP[file] = fileInfoFromFileName;
			return;
		}
		switch (fileInfoFromFileName.scnMissionSiegeOrInvasion)
		{
		case 1:
			UserInvasion[file] = fileInfoFromFileName;
			break;
		case 3:
			UserFreeBuilds[file] = fileInfoFromFileName;
			break;
		}
	}

	public void RemoveUserMap(string file)
	{
		file = file.Replace('/', '\\');
		if (UserInvasion.ContainsKey(file))
		{
			UserInvasion.Remove(file);
		}
		if (UserFreeBuilds.ContainsKey(file))
		{
			UserFreeBuilds.Remove(file);
		}
		if (UserMP.ContainsKey(file))
		{
			UserMP.Remove(file);
		}
	}

	public void UpdateWorkshopMap(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 2);
		if (fileInfoFromFileName == null || fileInfoFromFileName.missionMap)
		{
			return;
		}
		if (fileInfoFromFileName.mapType == 1)
		{
			WorkshopMP[file] = fileInfoFromFileName;
			return;
		}
		switch (fileInfoFromFileName.scnMissionSiegeOrInvasion)
		{
		case 1:
			WorkshopInvasion[file] = fileInfoFromFileName;
			break;
		case 3:
			WorkshopFreeBuilds[file] = fileInfoFromFileName;
			break;
		}
	}

	public void RemoveWorkshopMap(string file)
	{
		file = file.Replace('/', '\\');
		if (WorkshopInvasion.ContainsKey(file))
		{
			WorkshopInvasion.Remove(file);
		}
		if (WorkshopFreeBuilds.ContainsKey(file))
		{
			WorkshopFreeBuilds.Remove(file);
		}
		if (WorkshopMP.ContainsKey(file))
		{
			WorkshopMP.Remove(file);
		}
	}

	public void UpdateBuiltinMap(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 1);
		if (fileInfoFromFileName == null || fileInfoFromFileName.missionMap)
		{
			return;
		}
		if (fileInfoFromFileName.mapType == 1)
		{
			BuiltInMP[file] = fileInfoFromFileName;
			return;
		}
		switch (fileInfoFromFileName.scnMissionSiegeOrInvasion)
		{
		case 1:
			BuiltInInvasion[file] = fileInfoFromFileName;
			break;
		case 3:
			BuiltInFreeBuilds[file] = fileInfoFromFileName;
			break;
		}
	}

	public void UpdateUserWorkshopUploads(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 0);
		if (fileInfoFromFileName == null)
		{
			return;
		}
		string path = file.Replace(".map", ".data");
		try
		{
			string[] array = File.ReadAllLines(path);
			if (array != null && array.Length >= 2)
			{
				UserWorkshopUploads[file] = fileInfoFromFileName;
			}
		}
		catch (Exception)
		{
		}
	}

	public void RemoveUserWorkshopUploads(string file)
	{
		file = file.Replace('/', '\\');
		if (UserWorkshopUploads.ContainsKey(file))
		{
			UserWorkshopUploads.Remove(file);
		}
	}

	public void UpdateUserSave(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 0);
		if (fileInfoFromFileName != null)
		{
			SaveFiles[file] = fileInfoFromFileName;
		}
	}

	public void UpdateUserMPSave(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 0);
		if (fileInfoFromFileName != null)
		{
			SaveMPFiles[file] = fileInfoFromFileName;
		}
	}

	public void RemoveUserSave(string file)
	{
		file = file.Replace('/', '\\');
		if (SaveFiles.ContainsKey(file))
		{
			SaveFiles.Remove(file);
		}
	}

	public void RemoveUserMPSave(string file)
	{
		file = file.Replace('/', '\\');
		if (SaveMPFiles.ContainsKey(file))
		{
			SaveMPFiles.Remove(file);
		}
	}

	public void RescanTrailMakerFolder()
	{
		pauseTrailMakerWatcher = true;
		string trailMakerPath = TrailMakerPath;
		TrailMakerFiles.Clear();
		try
		{
			string[] files = Directory.GetFiles(trailMakerPath, "*.trail");
			foreach (string text in files)
			{
				UpdateTrailMakerFile(text.ToLower(), text);
			}
		}
		catch (Exception ex)
		{
			debugOutput.AppendLine("Cannot Scan trail Maker missions : " + trailMakerPath);
			debugOutput.AppendLine(ex.Message);
		}
		pauseTrailMakerWatcher = false;
	}

	public void UpdateTrailMakerFile(string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 3);
		if (fileInfoFromFileName != null)
		{
			TrailMakerFiles[file] = fileInfoFromFileName;
		}
	}

	public void RemoveTrailMakerFile(string file)
	{
		file = file.Replace('/', '\\');
		if (TrailMakerFiles.ContainsKey(file))
		{
			TrailMakerFiles.Remove(file);
		}
	}

	public void RescanCustomTrailsFolder()
	{
		pauseTrailMakerWatcher = true;
		string customTrailsPath = CustomTrailsPath;
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, CustomTrailInfo> customTrail in CustomTrails)
		{
			if (!customTrail.Value.workshop)
			{
				list.Add(customTrail.Key);
			}
		}
		foreach (string item in list)
		{
			CustomTrails.Remove(item);
		}
		try
		{
			string[] directories = Directory.GetDirectories(customTrailsPath);
			foreach (string text in directories)
			{
				try
				{
					string name = new DirectoryInfo(text).Name;
					CustomTrailInfo customTrailInfo = new CustomTrailInfo();
					customTrailInfo.DisplayName = (customTrailInfo.Name = name);
					CustomTrails[name] = customTrailInfo;
					string[] files = Directory.GetFiles(text, "*.trail");
					foreach (string text2 in files)
					{
						UpdateCustomTrailFile(name, text2.ToLower(), text2);
					}
					if (CustomTrails[name].headers.Count == 0)
					{
						CustomTrails.Remove(name);
					}
					else if (File.Exists(Path.Combine(text, name + ".data")))
					{
						customTrailInfo.workshopUploadInfoAvailable = true;
					}
				}
				catch (Exception ex)
				{
					debugOutput.AppendLine("Cannot Scan Custom trail files : " + text);
					debugOutput.AppendLine(ex.Message);
				}
			}
		}
		catch (Exception ex2)
		{
			debugOutput.AppendLine("Cannot Scan Custom trail files : " + customTrailsPath);
			debugOutput.AppendLine(ex2.Message);
		}
		pauseTrailMakerWatcher = false;
	}

	public void UpdateCustomTrailFile(string trailName, string file, string realFileName)
	{
		file = file.Replace('/', '\\');
		FileHeader fileInfoFromFileName = GetFileInfoFromFileName(file, realFileName, 4);
		if (fileInfoFromFileName != null)
		{
			CustomTrails[trailName].headers[file] = fileInfoFromFileName;
		}
	}

	public void RemoveCustomTrailFile(string trailName, string file)
	{
		file = file.Replace('/', '\\');
		if (TrailMakerFiles.ContainsKey(file))
		{
			TrailMakerFiles.Remove(file);
		}
	}

	public void CreateWatchers()
	{
		string userMapsPath = UserMapsPath;
		userMapsWatcher = new FileSystemWatcher(userMapsPath);
		userMapsWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
		userMapsWatcher.Changed += OnChangedMap;
		userMapsWatcher.Created += OnCreatedMap;
		userMapsWatcher.Deleted += OnDeletedMap;
		userMapsWatcher.Renamed += OnRenamedMap;
		userMapsWatcher.Filter = "*";
		userMapsWatcher.IncludeSubdirectories = false;
		userMapsWatcher.EnableRaisingEvents = true;
		userMapsPath = SavesPath;
		userSavesWatcher = new FileSystemWatcher(userMapsPath);
		userSavesWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
		userSavesWatcher.Changed += OnChangedSave;
		userSavesWatcher.Created += OnCreatedSave;
		userSavesWatcher.Deleted += OnDeletedSave;
		userSavesWatcher.Renamed += OnRenamedSave;
		userSavesWatcher.Filter = "*";
		userSavesWatcher.IncludeSubdirectories = false;
		userSavesWatcher.EnableRaisingEvents = true;
		userMapsPath = UserWorkshopPath;
		userWorkshopUploadsWatcher = new FileSystemWatcher(userMapsPath);
		userWorkshopUploadsWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
		userWorkshopUploadsWatcher.Changed += OnChangedWS;
		userWorkshopUploadsWatcher.Created += OnCreatedWS;
		userWorkshopUploadsWatcher.Deleted += OnDeletedWS;
		userWorkshopUploadsWatcher.Renamed += OnRenamedWS;
		userWorkshopUploadsWatcher.Filter = "*";
		userWorkshopUploadsWatcher.IncludeSubdirectories = false;
		userWorkshopUploadsWatcher.EnableRaisingEvents = true;
		userMapsPath = TrailMakerPath;
		trailMakerWatcher = new FileSystemWatcher(userMapsPath);
		trailMakerWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.LastAccess | NotifyFilters.CreationTime | NotifyFilters.Security;
		trailMakerWatcher.Changed += OnChangedTrailMaker;
		trailMakerWatcher.Created += OnCreatedTrailMaker;
		trailMakerWatcher.Deleted += OnDeletedTrailMaker;
		trailMakerWatcher.Renamed += OnRenamedTrailMaker;
		trailMakerWatcher.Filter = "*";
		trailMakerWatcher.IncludeSubdirectories = false;
		trailMakerWatcher.EnableRaisingEvents = true;
	}

	public void OnChangedMap(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType == WatcherChangeTypes.Changed && e.FullPath.ToLower().EndsWith(".map"))
		{
			UpdateUserMap(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnCreatedMap(object sender, FileSystemEventArgs e)
	{
		if (e.FullPath.ToLower().EndsWith(".map"))
		{
			UpdateUserMap(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnDeletedMap(object sender, FileSystemEventArgs e)
	{
		if (e.FullPath.ToLower().EndsWith(".map"))
		{
			RemoveUserMap(e.FullPath.ToLower());
		}
	}

	public void OnRenamedMap(object sender, RenamedEventArgs e)
	{
		if (e.OldFullPath.ToLower().EndsWith(".map"))
		{
			RemoveUserMap(e.OldFullPath.ToLower());
		}
		if (e.FullPath.ToLower().EndsWith(".map"))
		{
			UpdateUserMap(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnChangedSave(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType == WatcherChangeTypes.Changed)
		{
			if (e.FullPath.ToLower().EndsWith(".sav"))
			{
				UpdateUserSave(e.FullPath.ToLower(), e.FullPath);
			}
			else if (e.FullPath.ToLower().EndsWith(".msv"))
			{
				UpdateUserMPSave(e.FullPath.ToLower(), e.FullPath);
			}
		}
	}

	public void OnCreatedSave(object sender, FileSystemEventArgs e)
	{
		if (e.FullPath.ToLower().EndsWith(".sav"))
		{
			UpdateUserSave(e.FullPath.ToLower(), e.FullPath);
		}
		else if (e.FullPath.ToLower().EndsWith(".msv"))
		{
			UpdateUserMPSave(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnDeletedSave(object sender, FileSystemEventArgs e)
	{
		if (e.FullPath.ToLower().EndsWith(".sav"))
		{
			RemoveUserSave(e.FullPath.ToLower());
		}
		else if (e.FullPath.ToLower().EndsWith(".msv"))
		{
			RemoveUserSave(e.FullPath.ToLower());
		}
	}

	public void OnRenamedSave(object sender, RenamedEventArgs e)
	{
		if (e.OldFullPath.ToLower().EndsWith(".sav"))
		{
			RemoveUserSave(e.OldFullPath.ToLower());
		}
		else if (e.OldFullPath.ToLower().EndsWith(".msv"))
		{
			RemoveUserMPSave(e.OldFullPath.ToLower());
		}
		if (e.FullPath.ToLower().EndsWith(".sav"))
		{
			UpdateUserSave(e.FullPath.ToLower(), e.FullPath);
		}
		else if (e.FullPath.ToLower().EndsWith(".msv"))
		{
			UpdateUserSave(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnChangedWS(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType == WatcherChangeTypes.Changed && e.FullPath.ToLower().EndsWith(".map"))
		{
			UpdateUserWorkshopUploads(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnCreatedWS(object sender, FileSystemEventArgs e)
	{
		if (e.FullPath.ToLower().EndsWith(".map"))
		{
			UpdateUserWorkshopUploads(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnDeletedWS(object sender, FileSystemEventArgs e)
	{
		if (e.FullPath.ToLower().EndsWith(".map"))
		{
			RemoveUserWorkshopUploads(e.FullPath.ToLower());
		}
	}

	public void OnRenamedWS(object sender, RenamedEventArgs e)
	{
		if (e.OldFullPath.ToLower().EndsWith(".map"))
		{
			RemoveUserWorkshopUploads(e.OldFullPath.ToLower());
		}
		if (e.FullPath.ToLower().EndsWith(".map"))
		{
			UpdateUserWorkshopUploads(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnChangedTrailMaker(object sender, FileSystemEventArgs e)
	{
		if (!pauseTrailMakerWatcher && e.ChangeType == WatcherChangeTypes.Changed && e.FullPath.ToLower().EndsWith(".trail"))
		{
			UpdateTrailMakerFile(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnCreatedTrailMaker(object sender, FileSystemEventArgs e)
	{
		if (!pauseTrailMakerWatcher && e.FullPath.ToLower().EndsWith(".trail"))
		{
			UpdateTrailMakerFile(e.FullPath.ToLower(), e.FullPath);
		}
	}

	public void OnDeletedTrailMaker(object sender, FileSystemEventArgs e)
	{
		if (!pauseTrailMakerWatcher && e.FullPath.ToLower().EndsWith(".trail"))
		{
			RemoveTrailMakerFile(e.FullPath.ToLower());
		}
	}

	public void OnRenamedTrailMaker(object sender, RenamedEventArgs e)
	{
		if (!pauseTrailMakerWatcher)
		{
			if (e.OldFullPath.ToLower().EndsWith(".trail"))
			{
				RemoveTrailMakerFile(e.OldFullPath.ToLower());
			}
			if (e.FullPath.ToLower().EndsWith(".map"))
			{
				UpdateTrailMakerFile(e.FullPath.ToLower(), e.FullPath);
			}
		}
	}

	public List<FileHeader> SortList(List<FileHeader> list, int sortMode, bool sortAscend)
	{
		switch (sortMode)
		{
		case 0:
			if (sortAscend)
			{
				list.Sort((FileHeader x, FileHeader y) => x.sortFileName.CompareTo(y.sortFileName));
			}
			else
			{
				list.Sort((FileHeader x, FileHeader y) => y.sortFileName.CompareTo(x.sortFileName));
			}
			break;
		case 1:
			if (sortAscend)
			{
				list.Sort((FileHeader x, FileHeader y) => x.written.CompareTo(y.written));
			}
			else
			{
				list.Sort((FileHeader x, FileHeader y) => y.written.CompareTo(x.written));
			}
			break;
		case 2:
			if (sortAscend)
			{
				list.Sort((FileHeader x, FileHeader y) => x.maxPlayers.CompareTo(y.maxPlayers));
			}
			else
			{
				list.Sort((FileHeader x, FileHeader y) => y.maxPlayers.CompareTo(x.maxPlayers));
			}
			break;
		case 3:
			if (sortAscend)
			{
				list.Sort((FileHeader x, FileHeader y) => x.world_size.CompareTo(y.world_size));
			}
			else
			{
				list.Sort((FileHeader x, FileHeader y) => y.world_size.CompareTo(x.world_size));
			}
			break;
		case 4:
			if (sortAscend)
			{
				list.Sort((FileHeader x, FileHeader y) => x.typeString.CompareTo(y.typeString));
			}
			else
			{
				list.Sort((FileHeader x, FileHeader y) => y.typeString.CompareTo(x.typeString));
			}
			break;
		case 5:
			if (sortAscend)
			{
				list.Sort((FileHeader x, FileHeader y) => x.sortFileName.CompareTo(y.sortFileName));
				list.Sort((FileHeader x, FileHeader y) => x.balanced.CompareTo(y.balanced));
			}
			else
			{
				list.Sort((FileHeader x, FileHeader y) => y.sortFileName.CompareTo(x.sortFileName));
				list.Sort((FileHeader x, FileHeader y) => y.balanced.CompareTo(x.balanced));
			}
			break;
		case 10:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder8[y.maxPlayers].CompareTo(classicSortOrder8[x.maxPlayers]));
			break;
		case 11:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder7[y.maxPlayers].CompareTo(classicSortOrder7[x.maxPlayers]));
			break;
		case 12:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder6[y.maxPlayers].CompareTo(classicSortOrder6[x.maxPlayers]));
			break;
		case 13:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder5[y.maxPlayers].CompareTo(classicSortOrder5[x.maxPlayers]));
			break;
		case 14:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder4[y.maxPlayers].CompareTo(classicSortOrder4[x.maxPlayers]));
			break;
		case 15:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder3[y.maxPlayers].CompareTo(classicSortOrder3[x.maxPlayers]));
			break;
		case 16:
			list.Sort((FileHeader x, FileHeader y) => classicSortOrder2[y.maxPlayers].CompareTo(classicSortOrder2[x.maxPlayers]));
			break;
		}
		return list;
	}

	public List<FileHeader> GetUserWorkshopUploads(int sortMode, bool sortAscend)
	{
		List<FileHeader> list = new List<FileHeader>();
		foreach (KeyValuePair<string, FileHeader> userWorkshopUpload in UserWorkshopUploads)
		{
			list.Add(userWorkshopUpload.Value);
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<FileHeader> GetMultiplayerMaps(int sortMode, bool sortAscend, int numberOfPlayersMin, bool includeBuiltIn, bool includeUser, bool includeWorkshop)
	{
		List<FileHeader> list = new List<FileHeader>();
		if (includeUser)
		{
			foreach (KeyValuePair<string, FileHeader> item in UserMP)
			{
				if (item.Value.maxPlayers >= numberOfPlayersMin)
				{
					list.Add(item.Value);
				}
			}
		}
		if (includeBuiltIn)
		{
			foreach (KeyValuePair<string, FileHeader> item2 in BuiltInMP)
			{
				if (item2.Value.maxPlayers >= numberOfPlayersMin)
				{
					list.Add(item2.Value);
				}
			}
		}
		if (includeWorkshop)
		{
			foreach (KeyValuePair<string, FileHeader> item3 in WorkshopMP)
			{
				if (item3.Value.maxPlayers >= numberOfPlayersMin)
				{
					list.Add(item3.Value);
				}
			}
		}
		return SortList(list, sortMode, sortAscend);
	}

	public FileHeader GetRandomMultiplayerMap(int numberOfPlayersMin, int minSize, int maxSize, bool includeBuiltIn, bool includeUser, bool includeWorkshop)
	{
		List<FileHeader> list = new List<FileHeader>();
		if (includeUser)
		{
			foreach (KeyValuePair<string, FileHeader> item in UserMP)
			{
				if (item.Value.maxPlayers >= numberOfPlayersMin && item.Value.world_size >= minSize && item.Value.world_size <= maxSize)
				{
					list.Add(item.Value);
				}
			}
		}
		if (includeWorkshop)
		{
			foreach (KeyValuePair<string, FileHeader> item2 in WorkshopMP)
			{
				if (item2.Value.maxPlayers >= numberOfPlayersMin && item2.Value.world_size >= minSize && item2.Value.world_size <= maxSize)
				{
					list.Add(item2.Value);
				}
			}
		}
		if (includeBuiltIn || list.Count == 0)
		{
			foreach (KeyValuePair<string, FileHeader> item3 in BuiltInMP)
			{
				if (item3.Value.maxPlayers >= numberOfPlayersMin && item3.Value.world_size >= minSize && item3.Value.world_size <= maxSize)
				{
					list.Add(item3.Value);
				}
			}
		}
		if (list.Count > 0)
		{
			int index = new Random().Next(list.Count);
			return list[index];
		}
		return null;
	}

	public List<FileHeader> GetInvasionMaps(int sortMode, bool sortAscend, bool includeBuiltIn, bool includeUser, bool includeWorkshop)
	{
		List<FileHeader> list = new List<FileHeader>();
		if (includeUser)
		{
			foreach (KeyValuePair<string, FileHeader> item in UserInvasion)
			{
				list.Add(item.Value);
			}
		}
		if (includeBuiltIn)
		{
			foreach (KeyValuePair<string, FileHeader> item2 in BuiltInInvasion)
			{
				list.Add(item2.Value);
			}
		}
		if (includeWorkshop)
		{
			foreach (KeyValuePair<string, FileHeader> item3 in WorkshopInvasion)
			{
				list.Add(item3.Value);
			}
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<FileHeader> GetFreebuildMaps(int sortMode, bool sortAscend, bool includeBuiltIn, bool includeUser, bool includeWorkshop)
	{
		List<FileHeader> list = new List<FileHeader>();
		if (includeUser)
		{
			foreach (KeyValuePair<string, FileHeader> userFreeBuild in UserFreeBuilds)
			{
				list.Add(userFreeBuild.Value);
			}
		}
		if (includeBuiltIn)
		{
			foreach (KeyValuePair<string, FileHeader> builtInFreeBuild in BuiltInFreeBuilds)
			{
				list.Add(builtInFreeBuild.Value);
			}
		}
		if (includeWorkshop)
		{
			foreach (KeyValuePair<string, FileHeader> workshopFreeBuild in WorkshopFreeBuilds)
			{
				list.Add(workshopFreeBuild.Value);
			}
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<FileHeader> GetSaves(int sortMode, bool sortAscend, bool excludeQuicksaves = false, bool coopOnly = false)
	{
		List<FileHeader> list = new List<FileHeader>();
		foreach (KeyValuePair<string, FileHeader> saveFile in SaveFiles)
		{
			if ((!excludeQuicksaves || !saveFile.Value.display_filename.ToLowerInvariant().StartsWith("quicksave 20")) && (!coopOnly || saveFile.Value.coopTrailID != 0))
			{
				list.Add(saveFile.Value);
			}
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<FileHeader> GetMPSaves(int sortMode, bool sortAscend, bool coopOnly = false)
	{
		List<FileHeader> list = new List<FileHeader>();
		foreach (KeyValuePair<string, FileHeader> saveMPFile in SaveMPFiles)
		{
			if (!coopOnly || saveMPFile.Value.coopTrailID != 0)
			{
				list.Add(saveMPFile.Value);
			}
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<FileHeader> GetMapEditableMaps(int sortMode, bool sortAscend)
	{
		List<FileHeader> list = new List<FileHeader>();
		foreach (KeyValuePair<string, FileHeader> userFreeBuild in UserFreeBuilds)
		{
			if (userFreeBuild.Value.isMapEditable())
			{
				list.Add(userFreeBuild.Value);
			}
		}
		foreach (KeyValuePair<string, FileHeader> item in UserInvasion)
		{
			if (item.Value.isMapEditable())
			{
				list.Add(item.Value);
			}
		}
		foreach (KeyValuePair<string, FileHeader> item2 in UserMP)
		{
			if (item2.Value.isMapEditable())
			{
				list.Add(item2.Value);
			}
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<FileHeader> GetAllUserMapsForfilenameCheck()
	{
		List<FileHeader> list = new List<FileHeader>();
		foreach (KeyValuePair<string, FileHeader> userFreeBuild in UserFreeBuilds)
		{
			list.Add(userFreeBuild.Value);
		}
		foreach (KeyValuePair<string, FileHeader> item in UserInvasion)
		{
			list.Add(item.Value);
		}
		foreach (KeyValuePair<string, FileHeader> item2 in UserMP)
		{
			list.Add(item2.Value);
		}
		return list;
	}

	public List<FileHeader> GetTrailMakerFiles(int sortMode, bool sortAscend)
	{
		List<FileHeader> list = new List<FileHeader>();
		foreach (KeyValuePair<string, FileHeader> trailMakerFile in TrailMakerFiles)
		{
			list.Add(trailMakerFile.Value);
		}
		return SortList(list, sortMode, sortAscend);
	}

	public List<CustomTrailInfo> GetCustomTrails(bool ignoreWorkshopTrails = false)
	{
		List<CustomTrailInfo> list = new List<CustomTrailInfo>();
		foreach (KeyValuePair<string, CustomTrailInfo> customTrail in CustomTrails)
		{
			if (!ignoreWorkshopTrails || !customTrail.Value.workshop)
			{
				list.Add(customTrail.Value);
			}
		}
		list.Sort((CustomTrailInfo a, CustomTrailInfo b) => a.Name.CompareTo(b.Name));
		return list;
	}

	public int GetCustomTrailsCount()
	{
		return CustomTrails.Count;
	}

	public List<FileHeader> GetCustomTrailMissions(string trailName)
	{
		List<FileHeader> list = new List<FileHeader>();
		if (CustomTrails.TryGetValue(trailName, out var value))
		{
			foreach (KeyValuePair<string, FileHeader> header in value.headers)
			{
				list.Add(header.Value);
			}
		}
		return SortList(list, 0, sortAscend: true);
	}

	public int GetCustomTrailMissionsCount(string trailName)
	{
		if (CustomTrails.TryGetValue(trailName, out var value))
		{
			return value.headers.Count;
		}
		return 0;
	}

	public FileHeader GetHeaderFromFileNameForRestart(string fileName, bool freeBuild, bool builtIn, bool workShop)
	{
		FileHeader fileHeader = null;
		if (workShop)
		{
			fileHeader = ((!freeBuild) ? FindFileFromList(fileName, WorkshopInvasion) : FindFileFromList(fileName, WorkshopFreeBuilds));
			if (fileHeader != null)
			{
				return fileHeader;
			}
		}
		else if (builtIn)
		{
			fileHeader = ((!freeBuild) ? FindFileFromList(fileName, BuiltInInvasion) : FindFileFromList(fileName, BuiltInFreeBuilds));
			if (fileHeader != null)
			{
				return fileHeader;
			}
		}
		else
		{
			fileHeader = ((!freeBuild) ? FindFileFromList(fileName, UserInvasion) : FindFileFromList(fileName, UserFreeBuilds));
			if (fileHeader != null)
			{
				return fileHeader;
			}
		}
		return null;
	}

	public FileHeader GetHeaderFromFileNameForSkirmishRestart(string fileName, bool builtIn, bool workShop)
	{
		FileHeader fileHeader = null;
		if (workShop)
		{
			fileHeader = FindFileFromList(fileName, WorkshopMP);
			if (fileHeader != null)
			{
				return fileHeader;
			}
		}
		else if (builtIn)
		{
			fileHeader = FindFileFromList(fileName, BuiltInMP);
			if (fileHeader != null)
			{
				return fileHeader;
			}
		}
		else
		{
			fileHeader = FindFileFromList(fileName, UserMP);
			if (fileHeader != null)
			{
				return fileHeader;
			}
		}
		return null;
	}

	public FileHeader GetHeaderFromFileNameMP(string fileName)
	{
		FileHeader fileHeader = FindFileFromList(fileName, BuiltInMP);
		if (fileHeader != null)
		{
			return fileHeader;
		}
		fileHeader = FindFileFromList(fileName, WorkshopMP);
		if (fileHeader != null)
		{
			return fileHeader;
		}
		fileHeader = FindFileFromList(fileName, UserMP);
		if (fileHeader != null)
		{
			return fileHeader;
		}
		return null;
	}

	public FileHeader GetHeaderFromFileNameMP(string fileName, int crc)
	{
		FileHeader fileHeader = FindFileFromList(fileName, BuiltInMP);
		if (fileHeader != null && fileHeader.crc == crc)
		{
			return fileHeader;
		}
		fileHeader = FindFileFromList(fileName, WorkshopMP);
		if (fileHeader != null && fileHeader.crc == crc)
		{
			return fileHeader;
		}
		fileHeader = FindFileFromList(fileName, UserMP);
		if (fileHeader != null && fileHeader.crc == crc)
		{
			return fileHeader;
		}
		return null;
	}

	public FileHeader GetHeaderFromMpSaveFileName(string fileName)
	{
		FileHeader fileHeader = FindFileFromList(fileName, SaveMPFiles);
		if (fileHeader != null)
		{
			return fileHeader;
		}
		return null;
	}

	public FileHeader GetHeaderFromTrailMaker(string fileName)
	{
		FileHeader fileHeader = FindFileFromList(fileName, TrailMakerFiles);
		if (fileHeader != null)
		{
			return fileHeader;
		}
		return null;
	}

	public FileHeader GetHeaderFromCustomTrail(string trailName, string fileName)
	{
		FileHeader fileHeader = FindFileFromList(fileName, CustomTrails[trailName].headers);
		if (fileHeader != null)
		{
			return fileHeader;
		}
		return null;
	}

	public FileHeader FindFileFromList(string fileName, Dictionary<string, FileHeader> files)
	{
		if (files == null)
		{
			return null;
		}
		string text = fileName.ToLower();
		foreach (KeyValuePair<string, FileHeader> file in files)
		{
			if (file.Value.fileName.ToLower() == text)
			{
				return file.Value;
			}
		}
		return null;
	}

	public FileHeader GetFileInfoFromFileName(string filePath, string realFilePath, int folderType, bool loadRestartInfo = false)
	{
		int num = 0;
		realFilePath = realFilePath.Replace('/', '\\');
		try
		{
			bool flag = true;
			bool flag2 = false;
			if (filePath.ToLower().EndsWith(".map"))
			{
				flag = false;
			}
			if (filePath.ToLower().EndsWith(".msv"))
			{
				flag2 = true;
			}
			FileHeader fileHeader = new FileHeader();
			switch (folderType)
			{
			case 0:
				fileHeader.userMap = true;
				break;
			case 1:
				fileHeader.builtinMap = true;
				break;
			case 2:
				fileHeader.workshopMap = true;
				break;
			case 3:
				fileHeader.customTrailMap = true;
				break;
			case 4:
				fileHeader.customTrailMap = true;
				break;
			}
			DateTime creationTime = File.GetCreationTime(realFilePath);
			fileHeader.created = creationTime;
			DateTime lastWriteTime = File.GetLastWriteTime(realFilePath);
			fileHeader.written = lastWriteTime;
			fileHeader.showAlternateMissionTextForBriefing = false;
			fileHeader.achFood = 0;
			fileHeader.achWood = 0;
			fileHeader.achWeapons = 0;
			using FileStream fileStream = new FileStream(realFilePath, FileMode.Open, FileAccess.Read);
			if (fileStream.Length < 35000 || fileStream.Length > 9000000)
			{
				return null;
			}
			byte[] array = new byte[fileStream.Length];
			int num2 = (int)fileStream.Length;
			if (!flag2 && !loadRestartInfo)
			{
				num2 = Math.Min(num2, 250000);
			}
			fileHeader.length = num2;
			int num3 = 0;
			while (num2 > 0)
			{
				int num4 = fileStream.Read(array, num3, num2);
				if (num4 == 0)
				{
					break;
				}
				num3 += num4;
				num2 -= num4;
			}
			if (flag2)
			{
				fileHeader.crc = EngineInterface.crc(array);
			}
			fileHeader.headerID = BitConverter.ToInt32(array, num);
			num += 4;
			if (fileHeader.headerID >= 0)
			{
				return null;
			}
			fileHeader.radarMapCompressedSize = BitConverter.ToInt32(array, num);
			if (fileHeader.radarMapCompressedSize > 0)
			{
				num += 4;
				int num5 = BitConverter.ToInt32(array, num + 4);
				bool flag3 = false;
				if (num5 + 12 != fileHeader.radarMapCompressedSize)
				{
					flag3 = true;
				}
				num += fileHeader.radarMapCompressedSize;
				int num6 = num;
				int num7 = BitConverter.ToInt32(array, num);
				num += 4;
				if (num7 > 0)
				{
					fileHeader.missionTextType = BitConverter.ToInt32(array, num);
					num += 4;
					fileHeader.missionTextNumber = BitConverter.ToInt32(array, num);
					num += 4;
					fileHeader.utf8MissionText = "";
					if (fileHeader.missionTextType == 0 && fileHeader.missionTextNumber == 1234567 && flag3)
					{
						int num8 = BitConverter.ToInt32(array, num6 - 4);
						fileHeader.utf8MissionText = Encoding.UTF8.GetString(array, num6 - 4 - num8, num8);
					}
					byte[] array2 = new byte[num7 - 8];
					Buffer.BlockCopy(array, num, array2, 0, num7 - 8);
					byte[] array3 = EngineInterface.unpack(array2);
					if (array3 != null && array3.Length != 0)
					{
						byte[] bytes = removeTrailingZerosAnsi(array3);
						try
						{
							fileHeader.ansiMissionText = Encoding.ASCII.GetString(bytes);
						}
						catch (Exception)
						{
							fileHeader.ansiMissionText = "";
						}
						byte[] bytes2 = removeTrailingZerosUnicode(array3);
						try
						{
							fileHeader.unicodeMissionText = Encoding.Unicode.GetString(bytes2);
						}
						catch (Exception)
						{
							fileHeader.unicodeMissionText = "";
						}
						if (fileHeader.utf8MissionText.Length == 0)
						{
							if (fileHeader.ansiMissionText.Length > 0 && fileHeader.unicodeMissionText.Length > 0)
							{
								fileHeader.showAlternateMissionTextForBriefing = true;
							}
							if (fileHeader.ansiMissionText.Length > 0)
							{
								fileHeader.utf8MissionText = fileHeader.ansiMissionText;
							}
							else if (fileHeader.unicodeMissionText.Length > 0)
							{
								fileHeader.utf8MissionText = fileHeader.unicodeMissionText;
							}
						}
					}
					num += num7 - 8;
					num7 = BitConverter.ToInt32(array, num);
					num += 4;
					if (num7 > 0)
					{
						fileHeader.xPlaySaveTime = BitConverter.ToInt32(array, num);
						num += 4;
						fileHeader.xPlaySaveChecksum = BitConverter.ToInt32(array, num);
						num += 4;
						num7 = BitConverter.ToInt32(array, num);
						if (num7 > 0)
						{
							num += 4;
							fileHeader.mapType = BitConverter.ToInt32(array, num);
							if (fileHeader.mapType < 0)
							{
								int num9 = -fileHeader.mapType;
								fileHeader.trailID = num9 % 100;
								fileHeader.mapType = 1;
								fileHeader.trail = num9 / 100;
							}
							num += 4;
							fileHeader.mapKeeps[0] = BitConverter.ToInt32(array, num);
							num += 4;
							fileHeader.mapKeeps[1] = BitConverter.ToInt32(array, num);
							num += 4;
							fileHeader.mapKeeps[2] = BitConverter.ToInt32(array, num);
							num += 4;
							fileHeader.mapKeeps[3] = BitConverter.ToInt32(array, num);
							num += 4;
							fileHeader.mapKeeps[4] = BitConverter.ToInt32(array, num);
							num += 4;
							fileHeader.maxPlayers = BitConverter.ToInt32(array, num);
							num += 4;
							num7 = BitConverter.ToInt32(array, num);
							num += 4;
							if (num7 > 0)
							{
								fileHeader.scnMissionType = BitConverter.ToInt32(array, num);
								num += 4;
								if (num7 > 4)
								{
									fileHeader.scnMissionSiegeOrInvasion = BitConverter.ToInt32(array, num);
									num += 4;
									if (num7 > 8)
									{
										fileHeader.missionLockType = BitConverter.ToInt32(array, num);
										num += 4;
										if (fileHeader.headerID == -1)
										{
											byte[] array4 = new byte[num7 - 16];
											Buffer.BlockCopy(array, num, array4, 0, num7 - 16);
											fileHeader.standAlone_filename = Encoding.ASCII.GetString(array4);
											num += num7 - 16;
											fileHeader.inv_or_eco = BitConverter.ToInt32(array, num);
											num += 4;
										}
										else if (num7 > 12)
										{
											int num10 = BitConverter.ToInt32(array, num);
											num += 4;
											if (num10 > 0)
											{
												byte[] array5 = new byte[num10];
												Buffer.BlockCopy(array, num, array5, 0, num10);
												fileHeader.standAlone_filename = Encoding.UTF8.GetString(array5);
												num += num10;
											}
											fileHeader.inv_or_eco = BitConverter.ToInt32(array, num);
											num += 4;
											fileHeader.achFood = BitConverter.ToInt16(array, num);
											num += 2;
											fileHeader.achWeapons = BitConverter.ToInt16(array, num);
											num += 2;
											fileHeader.achWood = BitConverter.ToInt16(array, num);
											num += 2;
										}
									}
								}
								num7 = BitConverter.ToInt32(array, num);
								num += 4;
								if (num7 > 0)
								{
									fileHeader.isKingOfTheHill = BitConverter.ToInt32(array, num);
									if (fileHeader.isKingOfTheHill > 0)
									{
										fileHeader.maxPlayers--;
									}
									num += 4;
									if (num7 > 4)
									{
										fileHeader.skirmishMap = BitConverter.ToInt32(array, num) == 99;
										num += 4;
										if (num7 > 8)
										{
											fileHeader.xPlayAutoSave = BitConverter.ToInt32(array, num);
											num += 4;
											if (num7 <= 12)
											{
												return null;
											}
											fileHeader.balanced = BitConverter.ToInt32(array, num) == 0;
											num += 4;
											if (num7 > 16)
											{
												for (int i = 0; i < 8; i++)
												{
													fileHeader.keep_locations[i, 0] = BitConverter.ToInt32(array, num);
													num += 4;
													fileHeader.keep_locations[i, 1] = BitConverter.ToInt32(array, num);
													num += 4;
												}
												if (num7 > 80)
												{
													fileHeader.classicSave = false;
													fileHeader.world_size = BitConverter.ToInt32(array, num);
													num += 4;
													if (num7 > 84)
													{
														fileHeader.chimps_limit = BitConverter.ToInt32(array, num);
														num += 4;
														fileHeader.flies_limit = BitConverter.ToInt32(array, num);
														num += 4;
														fileHeader.extreme_powers_available = BitConverter.ToInt32(array, num) != 0;
														num += 4;
														fileHeader.hasOutposts = BitConverter.ToInt32(array, num) != 0;
														num += 4;
														if (num7 > 100)
														{
															fileHeader.hostileAnimals = BitConverter.ToInt32(array, num);
															num += 4;
															if (num7 > 104)
															{
																fileHeader.coopTrailID = BitConverter.ToInt32(array, num);
																num += 4;
																fileHeader.coopMissionID = BitConverter.ToInt32(array, num);
																num += 4;
															}
														}
													}
												}
											}
										}
									}
								}
								num7 = BitConverter.ToInt32(array, num);
								num += 4;
								if (num7 > 0)
								{
									byte[] array6 = new byte[num7];
									Buffer.BlockCopy(array, num, array6, 0, num7);
									num += num7;
									if (array6[0] < 50)
									{
										fileHeader.restartInfo = HUD_IngameMenu.RestartMapInfo.decode(array6);
									}
									else if (array6[0] >= 100)
									{
										fileHeader.hasRestartMPInfo = true;
										if (loadRestartInfo)
										{
											fileHeader.restartMPInfo = HUD_IngameMenu.RestartMPInfo.decode(array6);
										}
									}
									else
									{
										fileHeader.hasRestartSkirmishInfo = true;
										if (loadRestartInfo)
										{
											fileHeader.restartSkirmishInfo = HUD_IngameMenu.RestartSkirmishMapInfo.decode(array6, folderType == 4);
										}
									}
								}
							}
						}
					}
					string text = (fileHeader.fileName = Path.GetFileNameWithoutExtension(realFilePath));
					fileHeader.filePath = realFilePath;
					if (fileHeader.builtinMap)
					{
						fileHeader.display_filename = Translate.Instance.translateMapNames(text, ref fileHeader.mission_description);
					}
					else
					{
						fileHeader.display_filename = text;
					}
					if (fileHeader.standAlone_filename == "" || !flag)
					{
						fileHeader.standAlone_filename = text;
					}
					fileHeader.sortFileName = fileHeader.display_filename.ToLowerInvariant();
					fileHeader.missionMap = false;
					fileHeader.missionMap = fileHeader.missionLockType == 2;
					if (!fileHeader.missionMap && (text.ToLowerInvariant().StartsWith("crusaders_mission") || text.ToLowerInvariant().StartsWith("crusader_tutorial")))
					{
						fileHeader.missionMap = true;
					}
					if (flag)
					{
						fileHeader.mission_level = EngineInterface.GetCampaignLevel(realFilePath);
					}
					else
					{
						fileHeader.mission_level = -1;
					}
					fileHeader.setGameTypeString();
					return fileHeader;
				}
				return null;
			}
			return null;
		}
		catch (Exception)
		{
		}
		return null;
	}

	public byte[] removeTrailingZerosAnsi(byte[] data)
	{
		int num = data.Length;
		for (int i = 0; i < data.Length; i++)
		{
			if (data[i] == 0)
			{
				num = i;
				break;
			}
		}
		byte[] array = new byte[num];
		for (int j = 0; j < num; j++)
		{
			array[j] = data[j];
		}
		return array;
	}

	public byte[] removeTrailingZerosUnicode(byte[] data)
	{
		int num = data.Length;
		for (int i = 0; i < data.Length; i += 2)
		{
			if (data[i] == 0 && i + 1 < data.Length && data[i + 1] == 0)
			{
				num = i;
				break;
			}
		}
		byte[] array = new byte[num];
		for (int j = 0; j < num; j++)
		{
			array[j] = data[j];
		}
		return array;
	}

	public byte[] GetRadarFromFile(string path)
	{
		int num = 0;
		try
		{
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
			byte[] array = new byte[fileStream.Length];
			int num2 = (int)fileStream.Length;
			int num3 = 0;
			while (num2 > 0)
			{
				int num4 = fileStream.Read(array, num3, num2);
				if (num4 == 0)
				{
					break;
				}
				num3 += num4;
				num2 -= num4;
			}
			int num5 = BitConverter.ToInt32(array, num);
			num += 4;
			if (num5 >= 0)
			{
				return null;
			}
			int num6 = BitConverter.ToInt32(array, num);
			num += 4;
			if (num6 <= 0)
			{
				return null;
			}
			byte[] array2 = new byte[num6];
			Array.Copy(array, num, array2, 0, num6);
			return EngineInterface.unpackSavedRadar(array2);
		}
		catch (Exception)
		{
		}
		return null;
	}

	public Texture2D GetRadarPreview(byte[] radarMapPreview)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		if ((Object)(object)radarPreviewTexture == (Object)null)
		{
			radarPreviewTexture = new Texture2D(200, 200, (TextureFormat)14, false);
			((Texture)radarPreviewTexture).filterMode = (FilterMode)0;
		}
		radarPreviewTexture.SetPixelData<byte>(radarMapPreview, 0, 0);
		radarPreviewTexture.Apply();
		return radarPreviewTexture;
	}

	public Texture2D GetLoadSaveRadarPreview(byte[] radarMapPreview)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		if ((Object)(object)radarLoadSavePreviewTexture == (Object)null)
		{
			radarLoadSavePreviewTexture = new Texture2D(200, 200, (TextureFormat)14, false);
			((Texture)radarLoadSavePreviewTexture).filterMode = (FilterMode)0;
		}
		radarLoadSavePreviewTexture.SetPixelData<byte>(radarMapPreview, 0, 0);
		radarLoadSavePreviewTexture.Apply();
		return radarLoadSavePreviewTexture;
	}

	public void clearMPCRCChecks()
	{
		foreach (FileHeader mPSafe in GetMPSaves(0, sortAscend: true))
		{
			mPSafe.retrieveCRCChecks = 0;
		}
	}

	public static string SplitCustomTrailName(string CustomTrailName)
	{
		string text = CustomTrailName;
		if (text.Contains('\\'))
		{
			string[] array = text.Split('\\', StringSplitOptions.None);
			if (array.Length > 1)
			{
				text = array[1];
			}
		}
		return text;
	}
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using R3;
using SHCDESE.API.Components.ModManager;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Steamworks;
using SHCDESE.IO;
using SHCDESE.Interop;
using SHCDESE.Logging;

namespace SHCDESE.API.Components.Archive;

/// <summary>
/// Map Mod Manager. Takes care of loading BepInEx mods hidden in Map Archives.
/// </summary>
internal sealed class MapModManager
{
	/// <summary>
	/// Arguments shared by both updater backends, resolved once before dispatch.
	/// </summary>
	private readonly struct UpdaterContext(string scriptDirectory, string stagingDirectory, string targetDirectory, string deleteManifest, string gameExe, int gamePid)
	{
		public string ScriptDirectory { get; } = scriptDirectory;

		public string StagingDirectory { get; } = stagingDirectory;

		public string TargetDirectory { get; } = targetDirectory;

		public string DeleteManifest { get; } = deleteManifest;

		public string GameExe { get; } = gameExe;

		public int GamePid { get; } = gamePid;
	}

	private static readonly Lazy<MapModManager> _lazy = new Lazy<MapModManager>(() => new MapModManager());

	/// <summary>
	/// Consecutive failed deployments before we stop trying. Without this, a
	/// deployment that launches successfully but never applies its files would
	/// restart the game on every boot.
	/// </summary>
	private const int MaxDeployAttempts = 3;

	private readonly string _seDirectory;

	private readonly string _stagingDirectory;

	private readonly string _deleteManifestPath;

	private readonly string _deployAttemptsPath;

	private int _initialized;

	public static MapModManager Instance => _lazy.Value;

	private MapModManager()
	{
		_seDirectory = Path.Combine(DirectoryHelpers.GameDirectory, "_SE");
		_stagingDirectory = Path.Combine(_seDirectory, ".staging");
		_deleteManifestPath = Path.Combine(_stagingDirectory, "delete_list.txt");
		_deployAttemptsPath = Path.Combine(_seDirectory, ".deploy_attempts");
	}

	internal void Setup()
	{
		if (Directory.Exists(_stagingDirectory))
		{
			try
			{
				Directory.Delete(_stagingDirectory, recursive: true);
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex, "Exception during staging cleanup (this might be okay)", "Setup", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			}
		}
		InitializeSubscribers();
	}

	internal static void InitializeSubscribers()
	{
		if (Interlocked.Exchange(ref Instance._initialized, 1) == 0)
		{
			LogHelper.Information("Setting up subscribers", "InitializeSubscribers", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			SteamworksR3EventHooks.OnSteamworksInitialized.Observable.Subscribe(OnSteamworksInitialized);
		}
	}

	private static void OnSteamworksInitialized(SteamworksInitializedEventArgs e)
	{
		if (e.Phase == EventHookPhase.Post && SteamManager.s_instance.m_bInitialized)
		{
			Instance.TryUpdateModsFromRemote();
		}
	}

	/// <summary>
	/// True when this Windows process is running inside Wine/Proton
	/// </summary>
	private static bool IsWine()
	{
		try
		{
			IntPtr moduleHandle = MinWinAPI.GetModuleHandle("ntdll.dll");
			return moduleHandle != IntPtr.Zero && MinWinAPI.GetProcAddress(moduleHandle, "wine_get_version") != IntPtr.Zero;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Exception during IsWine check", "IsWine", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
	}

	/// <summary>
	/// Converts a Wine Z:-drive path to its host path. Returns false for any other drive.
	/// </summary>
	private static bool TryToHostPath(string winPath, out string unixPath)
	{
		unixPath = string.Empty;
		if (string.IsNullOrEmpty(winPath) || winPath.Length < 2 || winPath[1] != ':')
		{
			return false;
		}
		if (char.ToUpperInvariant(winPath[0]) != 'Z')
		{
			return false;
		}
		unixPath = winPath.Substring(2).Replace('\\', '/');
		if (!unixPath.StartsWith("/"))
		{
			unixPath = "/" + unixPath;
		}
		return true;
	}

	/// <summary>
	/// Tries to extract all map mods to the game directory.
	/// Local Maps saved in: C:\Users\Rawra\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Maps
	/// Steam Maps saved in: h:\steamlibrary\steamapps\workshop\content\3024040\3588254906\ruins of atlantis [arena][8 player][ki battle][balanced].map
	/// Target: H:\SteamLibrary\steamapps\common\Stronghold Crusader Definitive Edition
	/// </summary>
	private void TryUpdateModsFromRemote()
	{
		LogHelper.Information("Trying to update mods from steam-remote...", "TryUpdateModsFromRemote", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		List<string> listOfSubscribedItemsPaths = Platform_Workshop.Instance.GetListOfSubscribedItemsPaths();
		LogHelper.Information($"Retrieved {listOfSubscribedItemsPaths.Count} items", "TryUpdateModsFromRemote", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		bool flag = false;
		if (DetectAndStageUninstalls(listOfSubscribedItemsPaths))
		{
			flag = true;
		}
		foreach (string item in listOfSubscribedItemsPaths)
		{
			string firstMapFileInDirectory = GetFirstMapFileInDirectory(item);
			if (string.IsNullOrEmpty(firstMapFileInDirectory) || !MapArchive.TryLoad(firstMapFileInDirectory, out MapArchive mapArchive))
			{
				continue;
			}
			using (mapArchive)
			{
				ModInfo? info = mapArchive.Info;
				if (info != null && info.Manifest == ModManifest.BepInEx)
				{
					goto IL_00b4;
				}
				ModInfo? info2 = mapArchive.Info;
				if (info2 != null && info2.Manifest == ModManifest.Asset)
				{
					goto IL_00b4;
				}
				goto end_IL_0084;
				IL_00b4:
				if (TryStageModUpdate(firstMapFileInDirectory, mapArchive))
				{
					flag = true;
				}
				end_IL_0084:;
			}
		}
		if (!flag)
		{
			ResetDeployAttempts();
			return;
		}
		int num = ReadDeployAttempts();
		if (num >= 3)
		{
			LogHelper.Error($"Mod deployment has failed {num} times in a row. Not retrying. " + "Delete [" + _deployAttemptsPath + "] to try again.", "TryUpdateModsFromRemote", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return;
		}
		if (MinWinAPI.MessageBoxA(IntPtr.Zero, "Mod installs, updates, or removals are staged. Apply them and restart now?\n\nYes: apply changes and restart\nNo: discard staged changes and continue playing", "Script Extender", 308u) != 6)
		{
			LogHelper.Information("User declined the staged mod changes; continuing without deploying them.", "TryUpdateModsFromRemote", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			try
			{
				Directory.Delete(_stagingDirectory, recursive: true);
				return;
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex, "Could not discard staged mod changes", "TryUpdateModsFromRemote", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
				return;
			}
		}
		WriteDeployAttempts(num + 1);
		LaunchUpdaterAndExit();
	}

	/// <summary>
	/// Number of consecutive deployments that were started but never confirmed
	/// complete. Reset once a run finds nothing left to deploy.
	/// </summary>
	private int ReadDeployAttempts()
	{
		try
		{
			int result;
			return (File.Exists(_deployAttemptsPath) && int.TryParse(File.ReadAllText(_deployAttemptsPath).Trim(), out result)) ? result : 0;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Could not read deploy attempt counter", "ReadDeployAttempts", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return 0;
		}
	}

	private void WriteDeployAttempts(int value)
	{
		try
		{
			Directory.CreateDirectory(_seDirectory);
			File.WriteAllText(_deployAttemptsPath, value.ToString());
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Could not write deploy attempt counter", "WriteDeployAttempts", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		}
	}

	private void ResetDeployAttempts()
	{
		try
		{
			if (File.Exists(_deployAttemptsPath))
			{
				File.Delete(_deployAttemptsPath);
				LogHelper.Information("Previous mod deployment completed successfully.", "ResetDeployAttempts", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Could not clear deploy attempt counter", "ResetDeployAttempts", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		}
	}

	/// <summary>
	/// Compares locally installed mods (in _SE/*.json) against the active subscription list.
	/// If a mod is found locally but not in the subscription list, it is staged for deletion.
	/// </summary>
	private bool DetectAndStageUninstalls(List<string> subscribedItemPaths)
	{
		LogHelper.Information("Scanning for uninstalls in: [" + _seDirectory + "]", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		if (!Directory.Exists(_seDirectory))
		{
			LogHelper.Warning("_SE Directory not found. Skipping uninstall check.", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string subscribedItemPath in subscribedItemPaths)
		{
			string firstMapFileInDirectory = GetFirstMapFileInDirectory(subscribedItemPath);
			if (!string.IsNullOrEmpty(firstMapFileInDirectory))
			{
				hashSet.Add(Path.GetFileNameWithoutExtension(firstMapFileInDirectory));
			}
		}
		string[] files = Directory.GetFiles(_seDirectory, "*.json");
		LogHelper.Information($"Found {files.Length} local manifests.", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		bool flag = false;
		List<string> list = new List<string>();
		string[] array = files;
		foreach (string text in array)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
			if (hashSet.Contains(fileNameWithoutExtension))
			{
				continue;
			}
			LogHelper.Information("Detected unsubscribe for mod: [" + fileNameWithoutExtension + "]. Staging for removal.", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			list.Add(text);
			try
			{
				ModInfo modInfo = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(text));
				if (modInfo != null && !string.IsNullOrWhiteSpace(modInfo.GUID))
				{
					if (modInfo.GUID.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || modInfo.GUID.Contains("..") || modInfo.GUID.Contains('/') || modInfo.GUID.Contains('\\'))
					{
						LogHelper.Warning("Refusing unsafe GUID [" + modInfo.GUID + "] from [" + fileNameWithoutExtension + "]; skipping folder deletion.", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
					}
					else
					{
						string text2 = Path.Combine(DirectoryHelpers.GameDirectory, "BepInEx", "plugins", modInfo.GUID);
						if (Directory.Exists(text2))
						{
							list.Add(text2);
							LogHelper.Information("Scheduled directory deletion: [" + text2 + "]", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
						}
						else
						{
							LogHelper.Warning("Could not find plugin folder for GUID [" + modInfo.GUID + "]. It may already be deleted.", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex, "Failed to parse manifest for [" + fileNameWithoutExtension + "] during uninstall", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			}
			flag = true;
		}
		if (flag && list.Count > 0)
		{
			try
			{
				if (!Directory.Exists(_stagingDirectory))
				{
					Directory.CreateDirectory(_stagingDirectory);
				}
				File.WriteAllLines(_deleteManifestPath, list);
				return true;
			}
			catch (Exception ex2)
			{
				LogHelper.Error(ex2, "Failed to write delete manifest.", "DetectAndStageUninstalls", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			}
		}
		return flag;
	}

	private string GetFirstMapFileInDirectory(string path)
	{
		if (!Directory.Exists(path))
		{
			return string.Empty;
		}
		return Directory.EnumerateFiles(path, "*.map").FirstOrDefault() ?? string.Empty;
	}

	/// <summary>
	/// Extracts the mod to a staging folder. Does NOT touch the live BepInEx
	/// folder or the installed version file directly.
	/// </summary>
	private bool TryStageModUpdate(string path, MapArchive archive)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
		LogHelper.Information("Querying workshop mod (MAS): [" + fileNameWithoutExtension + "], full: [" + path + "]", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		if (!ShouldUpdate(fileNameWithoutExtension, archive))
		{
			LogHelper.Information("Skipping: [" + fileNameWithoutExtension + "]", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
		try
		{
			if (!Directory.Exists(_stagingDirectory))
			{
				Directory.CreateDirectory(_stagingDirectory);
			}
			string text = Path.Combine(_stagingDirectory, "BepInEx");
			LogHelper.Information("Staging files for [" + fileNameWithoutExtension + "]...", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			if (!archive.TryExtractFolder("BepInEx", text))
			{
				throw new Exception("Archive extraction to staging failed.");
			}
			if (archive.Info != null)
			{
				string text2 = archive.TryReadTextFile("info.json");
				if (!string.IsNullOrEmpty(text2))
				{
					string gUID = archive.Info.GUID;
					if (string.IsNullOrEmpty(archive.Info.GUID))
					{
						LogHelper.Error("info.json does not contain GUID!", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
						return false;
					}
					string text3 = Path.Combine(text, "plugins", gUID);
					Directory.CreateDirectory(text3);
					string text4 = Path.Combine(text3, "info.json");
					File.WriteAllText(text4, text2);
					LogHelper.Information("Staged info.json to [" + text4 + "]", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
				}
				else
				{
					LogHelper.Warning("Could not read info.json from archive for [" + fileNameWithoutExtension + "]", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
				}
			}
			string text5 = Path.Combine(_stagingDirectory, "_SE");
			Directory.CreateDirectory(text5);
			string text6 = Path.Combine(text5, fileNameWithoutExtension + ".json");
			File.WriteAllText(text6, JsonSerializer.Serialize(archive.Info, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
			LogHelper.Information("Staged mod info at [" + text6 + "]", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			LogHelper.Information("Successfully staged mod [" + fileNameWithoutExtension + "]", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return true;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Failed to stage mod [" + fileNameWithoutExtension + "].", "TryStageModUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
	}

	private bool ShouldUpdate(string fileName, MapArchive archive)
	{
		string text = Path.Combine(_seDirectory, fileName) + ".json";
		LogHelper.Information("Querying local mod info: [" + text + "]", "ShouldUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		if (!File.Exists(text))
		{
			LogHelper.Information("File does not exist (yet): [" + text + "]", "ShouldUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return true;
		}
		ModInfo modInfo = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(text));
		if (modInfo == null)
		{
			LogHelper.Warning("Failed to parse local mod info: [" + text + "]. Forcing update.", "ShouldUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return true;
		}
		string text2 = modInfo.Version ?? "0.0.0";
		string text3 = archive.Info?.Version ?? "0.0.0";
		LogHelper.Information("Local mod info version: [" + text2 + "], Remote version: [" + text3 + "]", "ShouldUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		try
		{
			SimpleSemVer simpleSemVer = new SimpleSemVer(text2);
			SimpleSemVer other = new SimpleSemVer(text3);
			if (simpleSemVer.AtleastMajorMinorPatch(other))
			{
				return false;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Version parsing failed. Defaulting to Update.", "ShouldUpdate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return true;
		}
		return true;
	}

	/// <summary>
	/// Quotes an argument for the Windows command-line parser. Wine applies the same rules before handing argv to a native binary, so this is used by both backends.
	/// </summary>
	private static string Quote(string value)
	{
		string text = value.TrimEnd('\\');
		return "\"" + text.Replace("\"", "\\\"") + "\"";
	}

	/// <summary>
	/// Locates the host <c>sh</c> binary through the Wine Z: mapping.
	/// </summary>
	private static string? ResolveHostShell()
	{
		string[] array = new string[2] { "Z:\\bin\\sh", "Z:\\usr\\bin\\sh" };
		foreach (string text in array)
		{
			if (File.Exists(text))
			{
				return text;
			}
		}
		return null;
	}

	/// <summary>
	/// Stages the post-exit updater and terminates the game so it can run.
	/// </summary>
	private void LaunchUpdaterAndExit()
	{
		try
		{
			string scriptDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "data");
			Process currentProcess = Process.GetCurrentProcess();
			string gameExe = currentProcess.MainModule?.FileName ?? "Stronghold Crusader Definitive Edition.exe";
			string stagingDirectory = _stagingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string targetDirectory = DirectoryHelpers.GameDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string deleteManifest = (File.Exists(_deleteManifestPath) ? _deleteManifestPath : string.Empty);
			UpdaterContext ctx = new UpdaterContext(scriptDirectory, stagingDirectory, targetDirectory, deleteManifest, gameExe, currentProcess.Id);
			if (!(IsWine() ? LaunchUpdaterAndExit_Unix(in ctx) : LaunchUpdaterAndExit_Win32(in ctx)))
			{
				LogHelper.Error("Updater could not be launched. Updates will be retried on next start.", "LaunchUpdaterAndExit", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
				return;
			}
			LogHelper.Information("Updater launched. Terminating game so files can be replaced.", "LaunchUpdaterAndExit", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			currentProcess.Kill();
		}
		catch (Exception ex)
		{
			LogHelper.Fatal(ex, "Failed to launch updater script. Updates will not be applied.", "LaunchUpdaterAndExit", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		}
	}

	/// <summary>
	/// Windows backend. Runs <c>mod-updater.ps1</c> under pwsh, falling back to Windows PowerShell.
	/// </summary>
	private bool LaunchUpdaterAndExit_Win32(in UpdaterContext ctx)
	{
		string text = Path.Combine(ctx.ScriptDirectory, "mod-updater.ps1");
		if (!File.Exists(text))
		{
			LogHelper.Error("Updater script not found at: [" + text + "]", "LaunchUpdaterAndExit_Win32", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
		string arguments = "-ExecutionPolicy Bypass -File " + Quote(text) + " " + $"-GamePid {ctx.GamePid} " + "-StagingDir " + Quote(ctx.StagingDirectory) + " -TargetDir " + Quote(ctx.TargetDirectory) + " -GameExe " + Quote(ctx.GameExe) + " -DeleteManifest " + Quote(ctx.DeleteManifest);
		string[] array = new string[2] { "pwsh", "powershell" };
		foreach (string text2 in array)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = text2,
				Arguments = arguments,
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Normal
			};
			try
			{
				Process.Start(startInfo);
				LogHelper.Information("Updater started via [" + text2 + "].", "LaunchUpdaterAndExit_Win32", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
				return true;
			}
			catch (Win32Exception)
			{
				LogHelper.Warning("[" + text2 + "] is not available; trying next interpreter.", "LaunchUpdaterAndExit_Win32", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			}
		}
		LogHelper.Error("Neither pwsh nor powershell could be started.", "LaunchUpdaterAndExit_Win32", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
		return false;
	}

	/// <summary>
	/// Wine/Proton backend. Escapes the prefix and runs <c>mod-updater.sh</c>
	/// through the host shell, translating all paths to host form.
	/// </summary>
	private bool LaunchUpdaterAndExit_Unix(in UpdaterContext ctx)
	{
		string text = Path.Combine(ctx.ScriptDirectory, "mod-updater.sh");
		if (!File.Exists(text))
		{
			LogHelper.Error("Updater script not found at: [" + text + "]", "LaunchUpdaterAndExit_Unix", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
		string text2 = ResolveHostShell();
		if (text2 == null)
		{
			LogHelper.Error("Running under Wine but no host sh binary is reachable via the Z: drive.", "LaunchUpdaterAndExit_Unix", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
		if (!TryToHostPath(text, out string unixPath) || !TryToHostPath(ctx.StagingDirectory, out string unixPath2) || !TryToHostPath(ctx.TargetDirectory, out string unixPath3))
		{
			LogHelper.Error("Game paths are not on the Wine Z: drive; cannot translate to host paths. Updates will not be applied.", "LaunchUpdaterAndExit_Unix", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
		string unixPath4 = string.Empty;
		if (!string.IsNullOrEmpty(ctx.DeleteManifest) && !TryToHostPath(ctx.DeleteManifest, out unixPath4))
		{
			LogHelper.Warning("Delete manifest path could not be translated; uninstalls will be skipped.", "LaunchUpdaterAndExit_Unix", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			unixPath4 = string.Empty;
		}
		string fileName = Path.GetFileName(ctx.GameExe);
		string arguments = Quote(unixPath) + " --proc " + Quote(fileName) + " --staging " + Quote(unixPath2) + " --target " + Quote(unixPath3) + " --delete-manifest " + Quote(unixPath4);
		ProcessStartInfo startInfo = new ProcessStartInfo
		{
			FileName = text2,
			Arguments = arguments,
			UseShellExecute = false
		};
		try
		{
			Process.Start(startInfo);
			LogHelper.Information("Updater started via host shell [" + text2 + "].", "LaunchUpdaterAndExit_Unix", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return true;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Failed to start host shell [" + text2 + "].", "LaunchUpdaterAndExit_Unix", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\Archive\\MapModManager.cs");
			return false;
		}
	}
}

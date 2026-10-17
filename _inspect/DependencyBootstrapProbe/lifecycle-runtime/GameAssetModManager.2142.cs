using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using SHCDESE.API.Components.Assets;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.IO;
using SHCDESE.Logging;
using SHCDESE.Lua;
using SHCDESE.Security;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Represents the core mod manager for Asset Mods.
/// </summary>
public sealed class GameAssetModManager
{
	/// <summary>
	/// A mod folder plus its parsed <c>info.json</c> metadata and comparable version.
	/// </summary>
	private sealed class AssetModCandidate(IModResourceSource source, ModInfo info, Version version)
	{
		/// <summary>Absolute path to the mod folder.</summary>
		public string Directory { get; } = source.ContainerPath;

		public IModResourceSource Source { get; } = source;

		public ModInfo Info { get; } = info;

		public Version Version { get; } = version;

		public string DisplayVersion
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Info.Version))
				{
					return Info.Version;
				}
				return "<none>";
			}
		}

		public string DisplayName
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Info.Name))
				{
					return Info.Name;
				}
				return Info.GUID;
			}
		}
	}

	private sealed class InstalledModVersion(string displayName, string? rawVersion)
	{
		public string DisplayName { get; } = displayName;

		public string? RawVersion { get; } = rawVersion;
	}

	private static readonly Lazy<GameAssetModManager> _lazy = new Lazy<GameAssetModManager>(() => new GameAssetModManager());

	/// <summary>
	/// Relative path inside a mod folder that is treated as the Lua entry point.
	/// </summary>
	private const string LUA_INIT_RELATIVE_PATH = "Scripts/init.lua";

	/// <summary>
	/// Version used for mods whose <see cref="P:SHCDESE.API.Components.ModManager.ModInfo.Version" /> is missing or unparsable.
	/// Any mod that does declare a usable version therefore wins over one that does not.
	/// </summary>
	private static readonly Version UNKNOWN_VERSION = new Version(0, 0, 0, 0);

	/// <summary>
	/// Characters that terminate the numeric part of a SemVer-style version string,
	/// e.g. the <c>-</c> in <c>1.4.0-beta.2</c> or the <c>+</c> in <c>1.4.0+build9</c>.
	/// </summary>
	private static readonly char[] VERSION_SUFFIX_SEPARATORS = new char[3] { '-', '+', ' ' };

	private readonly Dictionary<ModInfo, string> _registeredAssetDirectories = new Dictionary<ModInfo, string>();

	/// <summary>
	/// Index of everything that has actually been registered, keyed by mod GUID. 
	/// This is what makes registration idempotent per GUID: a second folder claiming an already-registered
	/// GUID is rejected instead of registering its assets, localizations, menu entry, XAML patches and mod-hash entry a second time.
	/// </summary>
	private readonly Dictionary<string, AssetModCandidate> _registeredByGuid = new Dictionary<string, AssetModCandidate>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Maps a mod's GUID to its absolute directory path for all mods that ship a
	/// <c>Scripts/init.lua</c>. Populated in <see cref="M:SHCDESE.API.Components.ModManager.GameAssetModManager.RegisterAssetMod(System.String)" /> and consumed
	/// by <see cref="T:SHCDESE.Lua.LuaManager" /> during map init.
	/// </summary>
	private readonly Dictionary<string, string> _pendingLuaMods = new Dictionary<string, string>();

	/// <summary>
	/// Candidates discovered by the early mod compatibility check. 
	/// Keeping thes winning candidates here lets normal registration reuse the same discovery result.
	/// </summary>
	private List<AssetModCandidate>? _preparedCandidates;

	public static GameAssetModManager Instance => _lazy.Value;

	private GameAssetModManager()
	{
	}

	public IEnumerable<KeyValuePair<ModInfo, string>> GetRegisteredAssetDirectories()
	{
		return _registeredAssetDirectories;
	}

	/// <summary>
	/// Returns all mods that have a Lua entry point, keyed by GUID → absolute directory.
	/// The <see cref="T:SHCDESE.Lua.LuaManager" /> calls this during map initialisation.
	/// </summary>
	internal IReadOnlyDictionary<string, string> GetPendingLuaMods()
	{
		return _pendingLuaMods;
	}

	/// <summary>
	/// Returns <see langword="true" /> when a mod with the given GUID has already been registered. GUIDs are compared case-insensitively.
	/// </summary>
	public bool IsRegistered(string guid)
	{
		if (!string.IsNullOrWhiteSpace(guid))
		{
			return _registeredByGuid.ContainsKey(guid);
		}
		return false;
	}

	/// <summary>
	/// Looks up the absolute directory of an already-registered mod by its GUID.
	/// </summary>
	/// <param name="guid">The GUID declared in the mod's <c>info.json</c>.</param>
	/// <param name="directory">The absolute path the mod was registered from.</param>
	/// <returns><see langword="true" /> if the GUID is registered.</returns>
	public bool TryGetRegisteredDirectory(string guid, out string directory)
	{
		if (!string.IsNullOrWhiteSpace(guid) && _registeredByGuid.TryGetValue(guid, out AssetModCandidate value))
		{
			directory = value.Directory;
			return true;
		}
		directory = string.Empty;
		return false;
	}

	/// <summary>
	/// Discovers the mods that would be registered and checks all declared mod dependencies.
	/// The Script Extender participates as a normal installed dependency under <see cref="F:SHCDESE.BepInEx.Bootstrap.Plugin.PLUGIN_GUID" />.
	/// The discovered candidates are retained for <see cref="M:SHCDESE.API.Components.ModManager.GameAssetModManager.RegisterAll" />.
	/// </summary>
	/// <param name="currentScriptExtenderVersion">The running Script Extender version.</param>
	/// <returns>One issue for each missing, malformed, or version-incompatible dependency.</returns>
	internal IReadOnlyList<ModCompatibilityIssue> PrepareAllAndCheckCompatibility(string currentScriptExtenderVersion)
	{
		if (!TryParseModVersion(currentScriptExtenderVersion, out Version _))
		{
			throw new InvalidOperationException("The running Script Extender version [" + currentScriptExtenderVersion + "] is invalid.");
		}
		DiscardPreparedCandidates();
		_preparedCandidates = DiscoverWinningCandidates();
		Dictionary<string, InstalledModVersion> dictionary = new Dictionary<string, InstalledModVersion>(StringComparer.OrdinalIgnoreCase) { ["000shcdese"] = new InstalledModVersion("SHCDE-SE", currentScriptExtenderVersion) };
		foreach (AssetModCandidate preparedCandidate in _preparedCandidates)
		{
			if (!dictionary.ContainsKey(preparedCandidate.Info.GUID))
			{
				dictionary[preparedCandidate.Info.GUID] = new InstalledModVersion(preparedCandidate.DisplayName, preparedCandidate.Info.Version);
			}
		}
		List<ModCompatibilityIssue> list = new List<ModCompatibilityIssue>();
		foreach (AssetModCandidate preparedCandidate2 in _preparedCandidates)
		{
			foreach (ModDependency item in EnumerateDependencies(preparedCandidate2.Info))
			{
				if (TryGetDependencyIssue(item, dictionary, out string reason))
				{
					ModCompatibilityIssue modCompatibilityIssue = new ModCompatibilityIssue(preparedCandidate2.Info, item, reason);
					list.Add(modCompatibilityIssue);
					LogHelper.Warning("Mod [" + modCompatibilityIssue.DisplayName + "] has an unmet dependency: " + modCompatibilityIssue.Reason, "PrepareAllAndCheckCompatibility", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
				}
			}
		}
		LogHelper.Information($"Mod compatibility check completed ({_preparedCandidates.Count} active mod(s), {list.Count} issue(s)).", "PrepareAllAndCheckCompatibility", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
		return list;
	}

	/// <summary>
	/// Releases candidates retained by <see cref="M:SHCDESE.API.Components.ModManager.GameAssetModManager.PrepareAllAndCheckCompatibility(System.String)" /> when startup is cancelled.
	/// </summary>
	internal void DiscardPreparedCandidates()
	{
		if (_preparedCandidates == null)
		{
			return;
		}
		foreach (AssetModCandidate preparedCandidate in _preparedCandidates)
		{
			preparedCandidate.Source.Dispose();
		}
		_preparedCandidates = null;
	}

	/// <summary>
	/// Finds and registers all directories and top-level .semod packages within BepInEx/plugins/.
	/// </summary>
	/// <remarks>
	/// Discovery runs before registration. Every immediate subdirectory is first read and parsed without registering anything, then candidates that declare the same <c>info.json</c> GUID
	/// are collapsed down to the one with the highest <see cref="P:SHCDESE.API.Components.ModManager.ModInfo.Version" />, and only the surviving candidates are registered. 
	/// When the startup compatibility check already ran, this method reuses its discovered winners.
	/// Losing copies are logged and otherwise ignored, so a mod that is installed twice no longer loads as well.
	///
	/// Winners are registered by container name using ordinal comparison, matching BepInEx-style numeric/uppercase/lowercase folder ordering for both loose and packed mods.
	/// </remarks>
	public void RegisterAll()
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		List<AssetModCandidate>? obj = _preparedCandidates ?? DiscoverWinningCandidates();
		_preparedCandidates = null;
		foreach (AssetModCandidate item in obj)
		{
			LogHelper.Information("Loading Asset Mod: [" + item.Directory + "]", "RegisterAll", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			RegisterAssetModCore(item);
		}
		stopwatch.Stop();
		LogHelper.Information($"Asset mod registration and indexing completed in {stopwatch.ElapsedMilliseconds} ms ({_registeredByGuid.Count} mod(s)).", "RegisterAll", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
	}

	/// <summary>
	/// Registers a directory or .semod package as an Asset Mod, wiring up asset overrides, localisations, and when a <c>Scripts/init.lua</c> is present, a pending Lua registration that will be
	/// picked up by <see cref="T:SHCDESE.Lua.LuaManager" /> on the next map load.
	/// </summary>
	/// <remarks>
	/// If a mod with the same <c>info.json</c> GUID is already registered, the call is ignored and a warning is logged..
	/// </remarks>
	/// <param name="directory">Absolute path to the mod folder.</param>
	public void RegisterAssetMod(string directory)
	{
		AssetModCandidate assetModCandidate = TryReadCandidate(directory);
		if (assetModCandidate != null)
		{
			RegisterAssetModCore(assetModCandidate);
		}
	}

	internal void Unload()
	{
		DiscardPreparedCandidates();
		_registeredAssetDirectories.Clear();
		_registeredByGuid.Clear();
		_pendingLuaMods.Clear();
	}

	/// <summary>
	/// Reads every candidate and collapses duplicate GUIDs to the highest mod version.
	/// </summary>
	private static List<AssetModCandidate> DiscoverWinningCandidates()
	{
		string bepInExPluginsDirectory = DirectoryHelpers.BepInExPluginsDirectory;
		if (!Directory.Exists(bepInExPluginsDirectory))
		{
			LogHelper.Warning("BepInEx plugins directory does not exist at [" + bepInExPluginsDirectory + "] - no asset mods will be registered.", "DiscoverWinningCandidates", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			return new List<AssetModCandidate>();
		}
		List<AssetModCandidate> list = new List<AssetModCandidate>();
		try
		{
			IEnumerable<string> second = from path in Directory.EnumerateFiles(bepInExPluginsDirectory, "*", SearchOption.TopDirectoryOnly)
				where string.Equals(Path.GetExtension(path), ".semod", StringComparison.OrdinalIgnoreCase)
				select path;
			foreach (string item in Directory.EnumerateDirectories(bepInExPluginsDirectory).Concat(second).OrderBy((string path) => Path.GetFileName(path), StringComparer.Ordinal))
			{
				AssetModCandidate assetModCandidate = TryReadCandidate(item);
				if (assetModCandidate != null)
				{
					list.Add(assetModCandidate);
				}
			}
			Dictionary<string, AssetModCandidate> dictionary = new Dictionary<string, AssetModCandidate>(StringComparer.OrdinalIgnoreCase);
			foreach (AssetModCandidate item2 in list)
			{
				if (!dictionary.TryGetValue(item2.Info.GUID, out var value))
				{
					dictionary[item2.Info.GUID] = item2;
				}
				else if (item2.Version > value.Version)
				{
					dictionary[item2.Info.GUID] = item2;
					LogShadowedCandidate(value, item2);
				}
				else
				{
					LogShadowedCandidate(item2, value);
				}
			}
			List<AssetModCandidate> list2 = new List<AssetModCandidate>();
			foreach (AssetModCandidate item3 in list)
			{
				if (dictionary.TryGetValue(item3.Info.GUID, out var value2) && value2 == item3)
				{
					list2.Add(item3);
				}
				else
				{
					item3.Source.Dispose();
				}
			}
			return list2;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Error during discovery", "DiscoverWinningCandidates", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			foreach (AssetModCandidate item4 in list)
			{
				item4.Source.Dispose();
			}
			throw;
		}
	}

	/// <summary>
	/// Returns dependencies declared through the generic collection, including malformed null
	/// entries so they can be reported instead of silently ignored.
	/// </summary>
	private static IEnumerable<ModDependency> EnumerateDependencies(ModInfo info)
	{
		foreach (ModDependency item in info.Dependencies ?? new List<ModDependency>())
		{
			yield return item ?? new ModDependency();
		}
	}

	/// <summary>
	/// Checks one dependency against the installed mod index. Invalid declarations are treated as
	/// issues so malformed metadata cannot silently bypass the check.
	/// </summary>
	private static bool TryGetDependencyIssue(ModDependency dependency, IReadOnlyDictionary<string, InstalledModVersion> installedMods, out string reason)
	{
		reason = string.Empty;
		if (string.IsNullOrWhiteSpace(dependency.GUID))
		{
			reason = "a dependency declares no GUID";
			return true;
		}
		string text = dependency.GUID.Trim();
		if (!installedMods.TryGetValue(text, out InstalledModVersion value))
		{
			reason = "required mod [" + text + "] is not installed";
			return true;
		}
		Version version = null;
		Version version2 = null;
		if (!string.IsNullOrWhiteSpace(dependency.MinimumVersion))
		{
			if (!TryParseModVersion(dependency.MinimumVersion, out Version version3))
			{
				reason = "dependency [" + text + "] has an invalid MinimumVersion [" + dependency.MinimumVersion + "]";
				return true;
			}
			version = version3;
		}
		if (!string.IsNullOrWhiteSpace(dependency.MaximumVersion))
		{
			if (!TryParseModVersion(dependency.MaximumVersion, out Version version4))
			{
				reason = "dependency [" + text + "] has an invalid MaximumVersion [" + dependency.MaximumVersion + "]";
				return true;
			}
			version2 = version4;
		}
		if (version != null && version2 != null && version > version2)
		{
			reason = "dependency [" + text + "] declares minimum [" + dependency.MinimumVersion + "] greater than maximum [" + dependency.MaximumVersion + "]";
			return true;
		}
		if (version == null && version2 == null)
		{
			return false;
		}
		if (!TryParseModVersion(value.RawVersion, out Version version5))
		{
			reason = "installed mod [" + value.DisplayName + "] (" + text + ") has an invalid version [" + (value.RawVersion ?? "<null>") + "]";
			return true;
		}
		if (version != null && version5 < version)
		{
			reason = "installed mod [" + value.DisplayName + "] (" + text + ") is version [" + value.RawVersion + "], but [" + dependency.MinimumVersion + "] or newer is required";
			return true;
		}
		if (version2 != null && version5 > version2)
		{
			reason = "installed mod [" + value.DisplayName + "] (" + text + ") is version [" + value.RawVersion + "], but [" + dependency.MaximumVersion + "] or older is required";
			return true;
		}
		return false;
	}

	/// <summary>
	/// Reads and validates a mod folder's <c>info.json</c> without touching any registry.
	/// </summary>
	/// <returns>The parsed candidate, or <see langword="null" /> when the folder is unusable.</returns>
	private static AssetModCandidate? TryReadCandidate(string directory)
	{
		IModResourceSource modResourceSource = null;
		try
		{
			if (Directory.Exists(directory))
			{
				modResourceSource = new DirectoryResourceSource(directory);
			}
			else
			{
				if (!File.Exists(directory) || !string.Equals(Path.GetExtension(directory), ".semod", StringComparison.OrdinalIgnoreCase))
				{
					return null;
				}
				modResourceSource = new SEmodResourceSource(directory);
			}
			return TryReadCandidate(modResourceSource);
		}
		catch (Exception ex)
		{
			modResourceSource?.Dispose();
			LogHelper.Error(ex, "Failed to open asset mod source [" + directory + "]", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			return null;
		}
	}

	private static AssetModCandidate? TryReadCandidate(IModResourceSource source)
	{
		if (!source.Contains("info.json"))
		{
			LogHelper.Error("Mod has no info.json. Expected one in: [" + source.DisplayName + "]", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		ModInfo modInfo;
		try
		{
			using Stream stream = source.OpenRead("info.json");
			using StreamReader streamReader = new StreamReader(stream);
			modInfo = JsonSerializer.Deserialize<ModInfo>(streamReader.ReadToEnd());
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Failed to parse mod json in [" + source.DisplayName + "]", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		if (modInfo == null)
		{
			LogHelper.Error("Failed to parse mod json in [" + source.DisplayName + "]", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		if (string.IsNullOrWhiteSpace(modInfo.GUID))
		{
			LogHelper.Error("Mod at [" + source.DisplayName + "] declares no GUID in its info.json and cannot be registered.", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		KillSwitchRegistry value = KillSwitchRegistry.Current.Value;
		if (value.BlocksGuid(modInfo.GUID, out string reason))
		{
			LogHelper.Error("Kill switch blocked asset mod [" + modInfo.GUID + "] from [" + source.DisplayName + "]: " + reason, "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		if (value.HasHashes)
		{
			try
			{
				foreach (string item in source.EnumerateFiles())
				{
					if (item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
					{
						bool flag;
						using (Stream input = source.OpenRead(item))
						{
							flag = value.BlocksAssembly(input, out reason);
						}
						if (flag)
						{
							LogHelper.Error("Kill switch blocked asset mod [" + modInfo.GUID + "] from [" + source.DisplayName + "]: assembly [" + item + "], " + reason, "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
							source.Dispose();
							return null;
						}
					}
				}
			}
			catch (Exception ex2)
			{
				LogHelper.Error(ex2, "Kill switch could not inspect assembly files in [" + source.DisplayName + "]", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
				source.Dispose();
				return null;
			}
		}
		if (source is SEmodResourceSource && modInfo.Manifest != ModManifest.Asset)
		{
			LogHelper.Error($"Packed mod [{source.DisplayName}] declares Manifest [{modInfo.Manifest}]. .semod packages support asset/Lua mods only.", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		if (!Enum.IsDefined(typeof(ModAssetMode), modInfo.AssetMode))
		{
			LogHelper.Error($"Mod [{modInfo.GUID}] at [{source.DisplayName}] declares an unknown AssetMode [{modInfo.AssetMode}].", "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			source.Dispose();
			return null;
		}
		if (!TryParseModVersion(modInfo.Version, out Version version))
		{
			LogHelper.Warning(string.Format("Mod [{0}] at [{1}] declares an unusable Version [{2}]. It will be treated as [{3}] when resolving duplicates, so any copy with a valid version wins.", modInfo.GUID, source.DisplayName, modInfo.Version ?? "<null>", UNKNOWN_VERSION), "TryReadCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
		}
		return new AssetModCandidate(source, modInfo, version);
	}

	/// <summary>
	/// Performs the actual registration for a candidate that has already been parsed and validated, rejecting it if its GUID is already registered.
	/// </summary>
	private void RegisterAssetModCore(AssetModCandidate candidate)
	{
		ModInfo info = candidate.Info;
		string directory = candidate.Directory;
		if (_registeredByGuid.TryGetValue(info.GUID, out AssetModCandidate value))
		{
			if (candidate.Version > value.Version)
			{
				LogHelper.Warning("Asset mod [" + info.GUID + "] is already registered from [" + value.Directory + "] at version [" + value.DisplayVersion + "]. The newer copy at [" + directory + "] (version [" + candidate.DisplayVersion + "]) was discovered too late to replace it and will be ignored. Delete the older copy if you want the newer one to load.", "RegisterAssetModCore", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			}
			else
			{
				LogHelper.Warning("Asset mod [" + info.GUID + "] is already registered from [" + value.Directory + "] at version [" + value.DisplayVersion + "]. Ignoring the duplicate at [" + directory + "] (version [" + candidate.DisplayVersion + "]).", "RegisterAssetModCore", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			}
			candidate.Source.Dispose();
		}
		else if (GameAssetManagerAPI.Instance.RegisterModProvider(info.GUID, candidate.Source, info.AssetMode))
		{
			_registeredByGuid[info.GUID] = candidate;
			_registeredAssetDirectories.Add(info, directory);
			string text = "Override/Assets/GUI/Sprites/" + info.GUID + ".png";
			if (candidate.Source.Contains(text))
			{
				string iconPath = ((info.AssetMode == ModAssetMode.Local) ? ("/" + GameAssetManagerAPI.CreateModAssetReference(info.GUID, text)) : ("/Assets/GUI/Sprites/" + info.GUID));
				Plugin.ViewModel.AddMod(iconPath, info);
			}
			else
			{
				LogHelper.Warning("Mod has no sprite logo at [" + text + "] in [" + directory + "] - This may be ignored.", "RegisterAssetModCore", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			}
			int num = GameTranslateAPI.Instance.LoadModLocalizations(info.GUID, directory);
			if (num > 0)
			{
				LogHelper.Information($"Loaded {num} localization file(s) for mod [{info.Name}]", "RegisterAssetModCore", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			}
			string text2 = "Scripts/init.lua";
			if (candidate.Source.Contains(text2))
			{
				_pendingLuaMods[info.GUID] = directory;
				LuaManager.Instance.RegisterAssetMod(info.GUID, text2, directory);
				LogHelper.Information("Registered Lua script for mod [" + info.Name + "] at [" + text2 + "]", "RegisterAssetModCore", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			}
			else
			{
				LogHelper.Information("Mod [" + info.Name + "] has no Lua scripts (Scripts/init.lua not found) - This is fine.", "RegisterAssetModCore", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
			}
		}
	}

	/// <summary>
	/// Logs that <paramref name="skipped" /> lost the duplicate-GUID contest against <paramref name="winner" />.
	/// </summary>
	private static void LogShadowedCandidate(AssetModCandidate skipped, AssetModCandidate winner)
	{
		if (skipped.Version == winner.Version)
		{
			LogHelper.Warning("Skipping asset mod [" + skipped.Info.Name + "] at [" + skipped.Directory + "] because GUID [" + skipped.Info.GUID + "] is provided at the same version [" + skipped.DisplayVersion + "] by [" + winner.Directory + "]. Two identical copies are installed; remove one of them.", "LogShadowedCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
		}
		else
		{
			LogHelper.Warning("Skipping asset mod [" + skipped.Info.Name + " " + skipped.DisplayVersion + "] at [" + skipped.Directory + "] because a newer version exists ([" + winner.Info.Name + " " + winner.DisplayVersion + "] at [" + winner.Directory + "]).", "LogShadowedCandidate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\API\\Components\\ModManager\\GameAssetModManager.cs");
		}
	}

	/// <summary>
	/// Converts the free-form <see cref="P:SHCDESE.API.Components.ModManager.ModInfo.Version" /> string into a comparable <see cref="T:System.Version" />.
	/// </summary>
	/// <remarks>
	/// Accepts an optional <c>v</c> prefix, a bare major (<c>"3"</c> becomes <c>3.0</c>), and SemVer pre-release or build metadata, which is discarded: <c>"1.4.0-beta.2"</c> compares
	/// equal to <c>"1.4.0"</c>. Anything else yields <see cref="F:SHCDESE.API.Components.ModManager.GameAssetModManager.UNKNOWN_VERSION" />.
	/// </remarks>
	/// <returns><see langword="true" /> when the string produced a real version.</returns>
	internal static bool TryParseModVersion(string? rawVersion, out Version version)
	{
		version = UNKNOWN_VERSION;
		if (string.IsNullOrWhiteSpace(rawVersion))
		{
			return false;
		}
		string text = rawVersion.Trim();
		if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
		{
			text = text.Substring(1);
		}
		int num = text.IndexOfAny(VERSION_SUFFIX_SEPARATORS);
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		if (text.IndexOf('.') < 0)
		{
			text += ".0";
		}
		if (!Version.TryParse(text, out var result) || result == null)
		{
			return false;
		}
		version = new Version(result.Major, result.Minor, Math.Max(result.Build, 0), Math.Max(result.Revision, 0));
		return true;
	}
}

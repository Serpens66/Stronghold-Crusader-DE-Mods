using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

namespace BepInEx.Bootstrap;

/// <summary>
/// The manager and loader for all plugins, and the entry point for BepInEx plugin system.
/// </summary>
public static class Chainloader
{
	private static readonly List<BaseUnityPlugin> _plugins = new List<BaseUnityPlugin>();

	private static bool? isEditor;

	private static bool _loaded = false;

	private static bool _initialized = false;

	private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

	private static readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

	internal static readonly ConfigEntry<bool> ConfigHideBepInExGOs = ConfigFile.CoreConfig.Bind("Chainloader", "HideManagerGameObject", defaultValue: false, new StringBuilder().AppendLine("If enabled, hides BepInEx Manager GameObject from Unity.").AppendLine("This can fix loading issues in some games that attempt to prevent BepInEx from being loaded.").AppendLine("Use this only if you know what this option means, as it can affect functionality of some older plugins.")
		.ToString());

	private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind("Logging", "UnityLogListening", defaultValue: true, "Enables showing unity log messages in the BepInEx logging system.");

	private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind("Logging.Disk", "WriteUnityLog", defaultValue: false, "Include unity log messages in log file output.");

	private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind("Logging.Disk", "AppendLog", defaultValue: false, "Appends to the log file instead of overwriting, on game startup.");

	private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind("Logging.Disk", "Enabled", defaultValue: true, "Enables writing log messages to disk.");

	private static readonly ConfigEntry<LogLevel> ConfigDiskConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind("Logging.Disk", "LogLevels", LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info, "Which log leves are saved to the disk log output.");

	/// <summary>
	/// The loaded and initialized list of plugins.
	/// </summary>
	public static Dictionary<string, PluginInfo> PluginInfos { get; } = new Dictionary<string, PluginInfo>();

	private static string UnityVersion
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			return Application.unityVersion;
		}
	}

	private static bool IsHeadless
	{
		get
		{
			MethodInfo methodInfo = AccessTools.PropertyGetter(typeof(Application), "isBatchMode");
			if (methodInfo != null)
			{
				return (bool)methodInfo.Invoke(null, null);
			}
			MethodInfo methodInfo2 = AccessTools.PropertyGetter(typeof(SystemInfo), "graphicsDeviceID");
			if (methodInfo2 != null)
			{
				return (int)methodInfo2.Invoke(null, null) == 0;
			}
			return false;
		}
	}

	internal static bool IsEditor
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			bool? flag = isEditor;
			if (!flag.HasValue)
			{
				bool? flag2 = (isEditor = Application.isEditor);
				return flag2 == true;
			}
			return flag == true;
		}
	}

	/// <summary>
	/// List of all <see cref="T:BepInEx.BepInPlugin" /> loaded via the chainloader.
	/// </summary>
	[Obsolete("Use PluginInfos instead")]
	public static List<BaseUnityPlugin> Plugins
	{
		get
		{
			lock (_plugins)
			{
				_plugins.RemoveAll((BaseUnityPlugin x) => (Object)(object)x == (Object)null);
				return _plugins.ToList();
			}
		}
	}

	/// <summary>
	/// Collection of error chainloader messages that occured during plugin loading.
	/// Contains information about what certain plugins were not loaded.
	/// </summary>
	public static List<string> DependencyErrors { get; } = new List<string>();

	/// <summary>
	/// The GameObject that all plugins are attached to as components.
	/// </summary>
	public static GameObject ManagerObject { get; private set; }

	private static Regex allowedGuidRegex { get; } = new Regex("^[a-zA-Z0-9\\._\\-]+$");

	/// <summary>
	/// Initializes BepInEx to be able to start the chainloader.
	/// </summary>
	public static void Initialize(string gameExePath, bool startConsole = true, ICollection<LogEventArgs> preloaderLogEvents = null)
	{
		if (!_initialized)
		{
			ThreadingHelper.Initialize();
			if (gameExePath != null)
			{
				Paths.SetExecutablePath(gameExePath);
			}
			if (ConsoleManager.ConsoleEnabled && startConsole)
			{
				ConsoleManager.CreateConsole();
				Logger.Listeners.Add(new ConsoleLogListener());
			}
			Logger.InitializeInternalLoggers();
			if (ConfigDiskLogging.Value)
			{
				Logger.Listeners.Add(new DiskLogListener("LogOutput.log", ConfigDiskConsoleDisplayedLevel.Value, ConfigDiskAppend.Value, ConfigDiskWriteUnityLog.Value));
			}
			if (!TraceLogSource.IsListening)
			{
				Logger.Sources.Add(TraceLogSource.CreateSource());
			}
			ReplayPreloaderLogs(preloaderLogEvents);
			if (ConfigUnityLogging.Value)
			{
				Logger.Sources.Add(new UnityLogSource());
			}
			if (!IsHeadless)
			{
				Logger.Listeners.Add(new UnityLogListener());
			}
			else if (Logger.Listeners.FirstOrDefault((ILogListener l) => l is ConsoleLogListener) is ConsoleLogListener consoleLogListener)
			{
				consoleLogListener.WriteUnityLogs = false;
			}
			if (PlatformHelper.Is(Platform.Unix))
			{
				Logger.LogInfo("Detected Unity version: v" + UnityVersion);
			}
			Logger.LogMessage("Chainloader ready");
			_initialized = true;
		}
	}

	private static void ReplayPreloaderLogs(ICollection<LogEventArgs> preloaderLogEvents)
	{
		if (preloaderLogEvents == null)
		{
			return;
		}
		UnityLogListener item = new UnityLogListener();
		Logger.Listeners.Add(item);
		ILogListener logListener = Logger.Listeners.FirstOrDefault((ILogListener logger) => logger is ConsoleLogListener);
		if (logListener != null)
		{
			Logger.Listeners.Remove(logListener);
		}
		ManualLogSource manualLogSource = Logger.CreateLogSource("Preloader");
		foreach (LogEventArgs preloaderLogEvent in preloaderLogEvents)
		{
			Logger.InternalLogEvent(manualLogSource, preloaderLogEvent);
		}
		Logger.Sources.Remove(manualLogSource);
		Logger.Listeners.Remove(item);
		if (logListener != null)
		{
			Logger.Listeners.Add(logListener);
		}
	}

	/// <summary>
	/// Analyzes the given type definition and attempts to convert it to a valid <see cref="T:BepInEx.PluginInfo" />
	/// </summary>
	/// <param name="type">Type definition to analyze.</param>
	/// <returns>If the type represent a valid plugin, returns a <see cref="T:BepInEx.PluginInfo" /> instance. Otherwise, return null.</returns>
	public static PluginInfo ToPluginInfo(TypeDefinition type)
	{
		if (type.IsInterface || type.IsAbstract)
		{
			return null;
		}
		try
		{
			if (!type.IsSubtypeOf(typeof(BaseUnityPlugin)))
			{
				return null;
			}
		}
		catch (AssemblyResolutionException)
		{
			return null;
		}
		BepInPlugin bepInPlugin = BepInPlugin.FromCecilType(type);
		if (bepInPlugin == null)
		{
			Logger.LogWarning("Skipping over type [" + type.FullName + "] as no metadata attribute is specified");
			return null;
		}
		if (string.IsNullOrEmpty(bepInPlugin.GUID) || !allowedGuidRegex.IsMatch(bepInPlugin.GUID))
		{
			Logger.LogWarning("Skipping type [" + type.FullName + "] because its GUID [" + bepInPlugin.GUID + "] is of an illegal format.");
			return null;
		}
		if (bepInPlugin.Version == null)
		{
			Logger.LogWarning("Skipping type [" + type.FullName + "] because its version is invalid.");
			return null;
		}
		if (bepInPlugin.Name == null)
		{
			Logger.LogWarning("Skipping type [" + type.FullName + "] because its name is null.");
			return null;
		}
		List<BepInProcess> processes = BepInProcess.FromCecilType(type);
		IEnumerable<BepInDependency> dependencies = BepInDependency.FromCecilType(type);
		IEnumerable<BepInIncompatibility> incompatibilities = BepInIncompatibility.FromCecilType(type);
		Version targettedBepInExVersion = type.Module.AssemblyReferences.FirstOrDefault((AssemblyNameReference reference) => reference.Name == "BepInEx")?.Version ?? new Version();
		return new PluginInfo
		{
			Metadata = bepInPlugin,
			Processes = processes,
			Dependencies = dependencies,
			Incompatibilities = incompatibilities,
			TypeName = type.FullName,
			TargettedBepInExVersion = targettedBepInExVersion
		};
	}

	private static bool HasBepinPlugins(AssemblyDefinition ass)
	{
		if (ass.MainModule.AssemblyReferences.All((AssemblyNameReference r) => r.Name != CurrentAssemblyName))
		{
			return false;
		}
		if (ass.MainModule.GetTypeReferences().All((TypeReference r) => r.FullName != typeof(BepInPlugin).FullName))
		{
			return false;
		}
		return true;
	}

	private static bool PluginTargetsWrongBepin(PluginInfo pluginInfo)
	{
		Version targettedBepInExVersion = pluginInfo.TargettedBepInExVersion;
		if (targettedBepInExVersion.Major != CurrentAssemblyVersion.Major)
		{
			return true;
		}
		if (targettedBepInExVersion.Minor > CurrentAssemblyVersion.Minor)
		{
			return true;
		}
		if (targettedBepInExVersion.Minor < CurrentAssemblyVersion.Minor)
		{
			return false;
		}
		return targettedBepInExVersion.Build > CurrentAssemblyVersion.Build;
	}

	/// <summary>
	/// The entrypoint for the BepInEx plugin system.
	/// </summary>
	public static void Start()
	{
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected O, but got Unknown
		if (_loaded)
		{
			return;
		}
		if (!_initialized)
		{
			throw new InvalidOperationException("BepInEx has not been initialized. Please call Chainloader.Initialize prior to starting the chainloader instance.");
		}
		if (!Directory.Exists(Paths.PluginPath))
		{
			Directory.CreateDirectory(Paths.PluginPath);
		}
		if (!Directory.Exists(Paths.PatcherPluginPath))
		{
			Directory.CreateDirectory(Paths.PatcherPluginPath);
		}
		try
		{
			PropertyInfo property = typeof(Application).GetProperty("productName", BindingFlags.Static | BindingFlags.Public);
			if (ConsoleManager.ConsoleActive)
			{
				ConsoleManager.SetConsoleTitle($"{CurrentAssemblyName} {CurrentAssemblyVersion} - {property?.GetValue(null, null) ?? Paths.ProcessName}");
			}
			Logger.LogMessage("Chainloader started");
			ManagerObject = new GameObject("BepInEx_Manager");
			if (ConfigHideBepInExGOs.Value)
			{
				((Object)ManagerObject).hideFlags = (HideFlags)61;
			}
			Object.DontDestroyOnLoad((Object)(object)ManagerObject);
			Dictionary<string, List<PluginInfo>> dictionary = TypeLoader.FindPluginTypes(Paths.PluginPath, ToPluginInfo, HasBepinPlugins, "chainloader");
			foreach (KeyValuePair<string, List<PluginInfo>> item in dictionary)
			{
				foreach (PluginInfo item2 in item.Value)
				{
					item2.Location = item.Key;
				}
			}
			List<PluginInfo> list = dictionary.SelectMany((KeyValuePair<string, List<PluginInfo>> p) => p.Value).ToList();
			Dictionary<string, Assembly> dictionary2 = new Dictionary<string, Assembly>();
			Logger.LogInfo(string.Format("{0} plugin{1} to load", list.Count, (list.Count == 1) ? "" : "s"));
			SortedDictionary<string, IEnumerable<string>> dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
			Dictionary<string, PluginInfo> pluginsByGUID = new Dictionary<string, PluginInfo>();
			foreach (IGrouping<string, PluginInfo> item3 in from info in list
				group info by info.Metadata.GUID)
			{
				PluginInfo pluginInfo = null;
				foreach (PluginInfo item4 in item3.OrderByDescending((PluginInfo x) => x.Metadata.Version))
				{
					if (pluginInfo != null)
					{
						Logger.LogWarning($"Skipping [{item4}] because a newer version exists ({pluginInfo})");
						continue;
					}
					List<BepInProcess> list2 = item4.Processes.ToList();
					if (list2.Count != 0 && list2.All((BepInProcess x) => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase)))
					{
						Logger.LogWarning(string.Format("Skipping [{0}] because of process filters ({1})", item4, string.Join(", ", item4.Processes.Select((BepInProcess p) => p.ProcessName).ToArray())));
						continue;
					}
					pluginInfo = item4;
					dependencyDict[item4.Metadata.GUID] = item4.Dependencies.Select((BepInDependency d) => d.DependencyGUID);
					pluginsByGUID[item4.Metadata.GUID] = item4;
				}
			}
			foreach (PluginInfo item5 in pluginsByGUID.Values.ToList())
			{
				if (item5.Incompatibilities.Any((BepInIncompatibility incompatibility) => pluginsByGUID.ContainsKey(incompatibility.IncompatibilityGUID)))
				{
					pluginsByGUID.Remove(item5.Metadata.GUID);
					dependencyDict.Remove(item5.Metadata.GUID);
					string[] value = (from x in item5.Incompatibilities
						select x.IncompatibilityGUID into x
						where pluginsByGUID.ContainsKey(x)
						select x).ToArray();
					string text = string.Format("Could not load [{0}] because it is incompatible with: {1}", item5, string.Join(", ", value));
					DependencyErrors.Add(text);
					Logger.LogError(text);
				}
				else if (PluginTargetsWrongBepin(item5))
				{
					string text2 = $"Plugin [{item5}] targets a wrong version of BepInEx ({item5.TargettedBepInExVersion}) and might not work until you update";
					DependencyErrors.Add(text2);
					Logger.LogWarning(text2);
				}
			}
			string[] emptyDependencies = new string[0];
			List<string> list3 = Utility.TopologicalSort(dependencyDict.Keys, (string x) => (!dependencyDict.TryGetValue(x, out var value5)) ? emptyDependencies : value5).ToList();
			HashSet<string> hashSet = new HashSet<string>();
			Dictionary<string, Version> dictionary3 = new Dictionary<string, Version>();
			foreach (string item6 in list3)
			{
				if (!pluginsByGUID.TryGetValue(item6, out var value2))
				{
					continue;
				}
				bool flag = false;
				List<BepInDependency> list4 = new List<BepInDependency>();
				foreach (BepInDependency dependency in value2.Dependencies)
				{
					if (!dictionary3.TryGetValue(dependency.DependencyGUID, out var value3) || value3 < dependency.MinimumVersion)
					{
						if (IsHardDependency(dependency))
						{
							list4.Add(dependency);
						}
					}
					else if (hashSet.Contains(dependency.DependencyGUID) && IsHardDependency(dependency))
					{
						flag = true;
						break;
					}
				}
				dictionary3.Add(item6, value2.Metadata.Version);
				if (flag)
				{
					string text3 = $"Skipping [{value2}] because it has a dependency that was not loaded. See previous errors for details.";
					DependencyErrors.Add(text3);
					Logger.LogWarning(text3);
					continue;
				}
				if (list4.Count != 0)
				{
					string text4 = string.Format("Could not load [{0}] because it has missing dependencies: {1}", value2, string.Join(", ", list4.Select((BepInDependency s) => (!IsEmptyVersion(s.MinimumVersion)) ? $"{s.DependencyGUID} (v{s.MinimumVersion} or newer)" : s.DependencyGUID).ToArray()));
					DependencyErrors.Add(text4);
					Logger.LogError(text4);
					hashSet.Add(item6);
					continue;
				}
				try
				{
					Logger.LogInfo($"Loading [{value2}]");
					if (!dictionary2.TryGetValue(value2.Location, out var value4))
					{
						value4 = (dictionary2[value2.Location] = Assembly.LoadFile(value2.Location));
					}
					PluginInfos[item6] = value2;
					value2.Instance = (BaseUnityPlugin)(object)ManagerObject.AddComponent(value4.GetType(value2.TypeName));
					_plugins.Add(value2.Instance);
				}
				catch (Exception ex)
				{
					hashSet.Add(item6);
					PluginInfos.Remove(item6);
					Logger.LogError($"Error loading [{value2}] : {ex.Message}");
					if (ex is ReflectionTypeLoadException ex2)
					{
						Logger.LogDebug(TypeLoader.TypeLoadExceptionToString(ex2));
					}
					else
					{
						Logger.LogDebug(ex);
					}
				}
			}
		}
		catch (Exception ex3)
		{
			try
			{
				ConsoleManager.CreateConsole();
			}
			catch
			{
			}
			Logger.LogFatal("Error occurred starting the game");
			Logger.LogFatal(ex3.ToString());
		}
		Logger.LogMessage("Chainloader startup complete");
		_loaded = true;
		static bool IsEmptyVersion(Version v)
		{
			if (v.Major == 0 && v.Minor == 0 && v.Build <= 0)
			{
				return v.Revision <= 0;
			}
			return false;
		}
		static bool IsHardDependency(BepInDependency dep)
		{
			return (dep.Flags & BepInDependency.DependencyFlags.HardDependency) != 0;
		}
	}
}

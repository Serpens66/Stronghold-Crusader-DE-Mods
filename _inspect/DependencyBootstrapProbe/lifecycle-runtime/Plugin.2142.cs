using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using Microsoft.Extensions.Logging;
using Noesis;
using PolyHook2.API.Logger;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory.Scanners;
using RedBird.Core.Utilities;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.AntiTamper;
using SHCDESE.BepInEx.Logging;
using SHCDESE.DebugMenu;
using SHCDESE.Detours;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.IO;
using SHCDESE.Interop;
using SHCDESE.Logging;
using SHCDESE.Lua;
using SHCDESE.ManagedHooks.Callbacks;
using SHCDESE.NativeHooks;
using SHCDESE.UI;
using SHCDESE.ViewModels;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Steamworks;
using UUIMGUI.Core;
using UnityEngine;

namespace SHCDESE.BepInEx.Bootstrap;

/// <summary>
/// The main entry point for the SHCDE Script Extender BepInEx plugin.
/// </summary>
/// <remarks>
/// This class is responsible for the entire bootstrap process, including setting up logging,
/// applying critical native hooks to intercept the game's library loading, and orchestrating the
/// initialization of all script extender managers and APIs once the game is ready.
/// </remarks>
[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInPlugin("000shcdese", "SHCDE-SE", "2.14.2.0")]
public class Plugin : BaseUnityPlugin
{
	/// <summary>The unique identifier for this BepInEx plugin.</summary>
	public const string PLUGIN_GUID = "000shcdese";

	/// <summary>The public display name of this plugin.</summary>
	public const string PLUGIN_NAME = "SHCDE-SE";

	/// <summary>The current version of this plugin.</summary>
	public const string PLUGIN_VERSION = "2.14.2.0";

	/// <summary>
	/// A static reference to the singleton instance of this plugin.
	/// </summary>
	public static Plugin Instance;

	public LoggingLevelSwitch LogLevelSwitch;

	public ILoggerFactory LoggerFactory;

	public static MainMenuViewModel ViewModel;

	public static LoadedModsViewModel LoadedModsViewModel = new LoadedModsViewModel();

	public static LobbyModSettingsHubViewModel ModSettingsHubViewModel = new LobbyModSettingsHubViewModel();

	private string _currentCultureName = string.Empty;

	/// <summary>
	/// This setting controls whether custom AI lords can use their custom lua scripts.
	/// </summary>
	public ConfigEntry<bool> AllowScriptedLords;

	/// <summary>
	/// This setting controls what other lobbies a script extender modified game can see.
	/// </summary>
	public ConfigEntry<bool> LobbyIsolation;

	/// <summary>
	/// Configuration entry to enable or disable some features that are only useful if debugging or otherwise analysing the game state, these
	/// may be impactful on performance and are not recommended for normal gameplay.
	/// </summary>
	public ConfigEntry<bool> DebugMode;

	/// <summary>
	/// Configuration entry to expose the CoreCLR (.NET runtime) to the Lua environment.
	/// </summary>
	/// <remarks>
	/// WARNING: This is a dangerous setting intended for advanced developers only. Enabling this gives Lua scripts
	/// the ability to interact with the entire .NET runtime, which can easily lead to game instability or crashes if misused.
	/// </remarks>
	public ConfigEntry<bool> ExposeCoreCLRToLua;

	/// <summary>
	/// Allows fixed buffer access to the Lua environment.
	/// </summary>
	/// <remarks>
	/// This is dangerous and not supported.
	/// </remarks>
	public ConfigEntry<bool> ExposeFixedBuffersToLua;

	/// <summary>
	/// Configuration entry to set the minimum game speed multiplier.
	/// </summary>
	public ConfigEntry<ushort> MinGameSpeed;

	/// <summary>
	/// Configuration entry to set the maximum game speed multiplier.
	/// </summary>
	public ConfigEntry<ushort> MaxGameSpeed;

	/// <summary>
	/// Configuration entry to set the increment value for game speed.
	/// </summary>
	public ConfigEntry<ushort> GameSpeedChangeIncrement;

	/// <summary>
	/// A flag used to determine if this is the first time the user is running the script extender, used to show an informational message.
	/// </summary>
	public ConfigEntry<bool> FirstStart;

	/// <summary>
	/// The log level use for the internal library.
	/// </summary>
	public ConfigEntry<LogEventLevel> LogLevel;

	/// <summary>
	/// Show the mod logo in the main menu
	/// </summary>
	public ConfigEntry<bool> ShowLogo;

	/// <summary>
	/// Ordered, semicolon-separated list of .webm or .mp4 files played inside the main-menu diamond.
	/// </summary>
	public ConfigEntry<string> LogoVideos;

	/// <summary>Enable the decorative main-menu logo video player.</summary>
	public ConfigEntry<bool> LogoVideoEnabled;

	/// <summary>Mute decorative main-menu logo videos.</summary>
	public ConfigEntry<bool> LogoVideoMuted;

	/// <summary>Enable the animated static/vignette overlay on logo videos.</summary>
	public ConfigEntry<bool> LogoStaticEffect;

	/// <summary>Enable the animated scanline overlay on logo videos.</summary>
	public ConfigEntry<bool> LogoScanlineEffect;

	/// <summary>Opacity from 0.0 (transparent) to 1.0 (opaque) for the processed logo video.</summary>
	public ConfigEntry<double> LogoVideoOpacity;

	/// <summary>Chance from 0.0 to 1.0 of showing the rare logo caption per clip.</summary>
	public ConfigEntry<double> PokemonCaptionChance;

	/// <summary>
	/// The language provider to use for existing localization.
	/// Set to "detect" to auto-detect it.
	/// </summary>
	public ConfigEntry<string> LanguageProvider;

	/// <summary>
	/// Dumps the embedded encoded AIVs into binary blobs in the _EXPORT folder in the main game directory.
	/// </summary>
	public ConfigEntry<bool> DumpEmbeddedAIVs;

	/// <summary>
	/// Dumps the embedded AICs into binary blobs in the _EXPORT folder in the main game directory.
	/// </summary>
	public ConfigEntry<bool> DumpEmbeddedAICs;

	/// <summary>
	/// Generate a lua reference md file from existing DocFX summaries.
	/// </summary>
	public ConfigEntry<bool> GenerateLuaDocumentation;

	/// <summary>
	/// Should the SE make use of async logging? (might make debugging harder, otherwise its a perf. gain)
	/// </summary>
	public ConfigEntry<bool> AsyncLogging;

	/// <summary>
	/// Prefix log messages with the local date and time at which the event was created.
	/// </summary>
	public ConfigEntry<bool> LogTimestamps;

	/// <summary>
	/// Prefix log messages with the managed ID of the thread that created the event.
	/// </summary>
	public ConfigEntry<bool> LogThreadIds;

	/// <summary>
	/// Enables the native vectored-exception reporter and minidump writer.
	/// </summary>
	public ConfigEntry<bool> EnableNativeCrashHandler;

	/// <summary>
	/// Maximum number of native crash-handler files retained between launches.
	/// </summary>
	public ConfigEntry<int> MaxNativeCrashDumpFiles;

	/// <summary>
	/// Should the SE allow the chat in non-multiplayer matches?
	/// </summary>
	public ConfigEntry<bool> AllowChatInNonMultiplayer;

	/// <summary>
	/// Allows multiple instances of the game by bypassing the game's SteamAPI.RestartAppIfNecessary call which enforces
	/// that the game was launched through steam: something that you can only do once.
	/// Do note that this will likely result in steam not being able to initialize properly alongside steamworks, how you 
	/// deal with that will be up to you.
	/// </summary>
	public ConfigEntry<bool> AllowMultipleInstances;

	/// <summary>
	/// Enables some anti-tamper functionalities.
	/// </summary>
	public ConfigEntry<bool> EnableAntiTamper;

	/// <summary>
	/// Initializes steam early.
	/// </summary>
	public ConfigEntry<bool> EnableEarlySteamInitialization;

	/// <summary>
	/// Skips the Script Extender's SteamUGC.DownloadItem update requests.
	/// </summary>
	public ConfigEntry<bool> DisableSteamUGCUpdates;

	/// <summary>
	/// Preloader-enforced signature requirement for ordinary BepInEx plugin folders.
	/// </summary>
	public ConfigEntry<bool> RequireSignedBepInExPlugins;

	/// <summary>
	/// Ignores any found dependency incompatibilities, mainly used for testing.
	/// </summary>
	public ConfigEntry<bool> IgnoreDependencyIncompatibilities;

	/// <summary>
	/// Skips the sh4 advert screen.
	/// </summary>
	public ConfigEntry<bool> SkipStronghold4Advert;

	/// <summary>
	/// Allows special troop actions within the map editor.
	/// </summary>
	public ConfigEntry<bool> AllowTroopActionsInMapEditor;

	private Plugin()
	{
		Instance = this;
		SHCDESE.AntiTamper.AntiTamper.Instance.CreateSnapshot();
	}

	/// <summary>
	/// The primary entry point called by BepInEx when the plugin is loaded.
	/// </summary>
	private void Awake()
	{
		try
		{
			_ = UnityMainThreadDispatcher.Instance;
			PluginConfigAwake();
			LogLevelSwitch = new LoggingLevelSwitch
			{
				MinimumLevel = LogLevel.Value
			};
			LoggerConfiguration loggerConfiguration = new LoggerConfiguration().MinimumLevel.ControlledBy(LogLevelSwitch).Enrich.FromLogContext();
			if (LogThreadIds.Value)
			{
				loggerConfiguration.Enrich.With(new ThreadIdEnricher());
			}
			if (AsyncLogging.Value)
			{
				loggerConfiguration.WriteTo.Async(delegate(LoggerSinkConfiguration a)
				{
					a.Sink(new BepInExSink(((BaseUnityPlugin)this).Logger, LogTimestamps.Value, LogThreadIds.Value));
				});
			}
			else
			{
				loggerConfiguration.WriteTo.Sink(new BepInExSink(((BaseUnityPlugin)this).Logger, LogTimestamps.Value, LogThreadIds.Value));
			}
			Serilog.Log.Logger = loggerConfiguration.CreateLogger();
			LoggerFactory = new SerilogLoggerFactory(Serilog.Log.Logger);
			ModLoggerFactory.Initialize(LogLevelSwitch);
			LogHelper.Information("Plugin SHCDE-SE is loading!", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			if (!IgnoreDependencyIncompatibilities.Value && !ConfirmModCompatibility())
			{
				return;
			}
			if (EnableEarlySteamInitialization.Value)
			{
				SteamAPI.Init();
			}
			InitLanguageProviderEarly();
			if (!HookBackends.TrySetDefault(NativeDetourBackend.Instance))
			{
				LogHelper.Fatal("Failed to set the default detouring backend.", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			}
			if (DumpEmbeddedAIVs.Value)
			{
				GameAIManagerAPI.DumpEmbeddedAIVs();
			}
			_ = GenerateLuaDocumentation.Value;
			string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			LibraryLoaderManager.Instance.AddLibrarySearchPath(directoryName);
			LogHelper.Information("Added to library search path: " + directoryName, "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			try
			{
				int num = NativeCrashHandler.TrimCrashDumps(MaxNativeCrashDumpFiles.Value);
				if (num > 0)
				{
					LogHelper.Information($"Deleted {num} old native crash-handler file(s)", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex, "Failed to trim old native crash-handler files", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			}
			if (EnableNativeCrashHandler.Value)
			{
				try
				{
					string text = NativeCrashHandler.Install();
					LogHelper.Information("Native crash handler installed; dumps will be written to " + text, "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
				}
				catch (Exception ex2)
				{
					LogHelper.Error(ex2, "Failed to install the native crash handler", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
				}
			}
			CrusaderLibrary.Instance.LibraryLoaded += CrusaderLibrary_LibraryLoaded;
			PolyHook2.API.Logger.Log.Initialize();
			PolyHook2.API.Logger.Log.LogReceived += Log_LogReceived;
			NoesisUnity.InitCore();
			DetourManager.Instance.ApplyManagedEarly();
			ViewModel = new MainMenuViewModel(LogoVideoEnabled.Value, LogoVideos.Value, LogoVideoMuted.Value, LogoStaticEffect.Value, LogoScanlineEffect.Value, LogoVideoOpacity.Value, PokemonCaptionChance.Value);
			GameXAMLManagerAPI.Instance.RegisterBinding("SEMainMenuContainer", ViewModel);
			ViewModel.IsSEMenuVisible = (Visibility)(ShowLogo.Value ? 2 : 0);
			ViewModel.CheckForUpdateAsync("2.14.2.0");
			GameXAMLManagerAPI.Instance.RegisterBinding("SE_LoadedModsModal", LoadedModsViewModel);
			GameXAMLManagerAPI.Instance.RegisterBinding("SE_ModOptionsToggle", ModSettingsHubViewModel);
			GameXAMLManagerAPI.Instance.RegisterBinding("SE_ModOptionsModal", ModSettingsHubViewModel);
			if (EnableAntiTamper.Value)
			{
				SHCDESE.AntiTamper.AntiTamper.Instance.Compare();
			}
			GameAssetModManager.Instance.RegisterAll();
			GameAssetManagerAPI.Instance.PreloadMusic();
			if (LogoVideoEnabled.Value)
			{
				AnalogLogoMediaPlayer.PreloadShader();
			}
			GameXAMLManagerAPI.Instance.Setup();
			DetourManager.Instance.ApplyManaged();
			NoesisCallbacks.Install();
			Application.quitting += Application_quitting;
			LoadNativeLibrary();
			ShowFirstTimeMessage();
			LogHelper.Information("Plugin SHCDE-SE is loaded!", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		}
		catch (Exception ex3)
		{
			LogHelper.Error(ex3, "Error during plugin startup", "Awake", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		}
	}

	/// <summary>
	/// Warns when an active mod has a missing or version-incompatible dependency.
	/// </summary>
	/// <returns><see langword="true" /> when startup should continue.</returns>
	private static bool ConfirmModCompatibility()
	{
		GameAssetModManager instance = GameAssetModManager.Instance;
		IReadOnlyList<ModCompatibilityIssue> readOnlyList = instance.PrepareAllAndCheckCompatibility("2.14.2.0");
		if (readOnlyList.Count == 0)
		{
			return true;
		}
		string text = string.Join("\n", from name in readOnlyList.Select((ModCompatibilityIssue issue) => issue.DisplayName).Distinct<string>(StringComparer.OrdinalIgnoreCase).OrderBy((string name) => name, StringComparer.OrdinalIgnoreCase)
			select "- " + name);
		string lpText = "One or more active mods have missing or incompatible dependencies:\n\n" + text + "\n\nUpdate the listed mods, or install/update their required dependencies. Continuing may cause errors or crashes.\nFor a more detailed listing of the exact mods involved, please see the log file!\n\nYes: continue starting the game\nNo: stop the game";
		if (MinWinAPI.MessageBoxA(IntPtr.Zero, lpText, "Script Extender", 308u) == 6)
		{
			LogHelper.Warning($"The user chose to continue with {readOnlyList.Count} mod compatibility issue(s).", "ConfirmModCompatibility", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			return true;
		}
		LogHelper.Warning("The user chose to stop the game because of unmet mod dependencies.", "ConfirmModCompatibility", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		instance.DiscardPreparedCandidates();
		Environment.Exit(0);
		return false;
	}

	/// <summary>
	/// When unity is closing, we need to restore the original game VEH for "clean" shutdown.
	/// Thanks FireFly.
	/// </summary>
	private static void Application_quitting()
	{
		LogHelper.Information("Restoring original VEH", "Application_quitting", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		BulkVEHDetours.RestoreOriginalHandler();
		NativeCrashHandler.Uninstall();
	}

	private unsafe void LoadNativeLibrary()
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
		IntPtr intPtr = MinWinAPI.LoadLibraryA(fileNameWithoutExtension + "_Data/Plugins/x86_64/CrusaderDE.dll");
		if (intPtr == IntPtr.Zero)
		{
			LogHelper.Error("CrusaderDE.dll not found at " + fileNameWithoutExtension + "_Data/Plugins/x86_64/CrusaderDE.dll", "LoadNativeLibrary", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			return;
		}
		LogHelper.Information("CrusaderDE: " + intPtr.ToString("X16"), "LoadNativeLibrary", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		if (!MinWinAPI.GetModuleInformation(Process.GetCurrentProcess().Handle, intPtr, out var lpmodinfo, (uint)Marshal.SizeOf(typeof(MinWinAPI.MODULEINFO))))
		{
			LogHelper.Error("Failed to retrieve module information for CrusaderDE.dll", "LoadNativeLibrary", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			return;
		}
		Serilog.Log.Information("Base address: 0x" + lpmodinfo.lpBaseOfDll.ToString("X16"));
		Serilog.Log.Information("Module size: 0x" + lpmodinfo.SizeOfImage.ToString("X16") + " bytes");
		ReadOnlySpan<byte> memory = new ReadOnlySpan<byte>((void*)lpmodinfo.lpBaseOfDll, (int)lpmodinfo.SizeOfImage);
		CrusaderLibrary.Instance.SignalLibraryLoaded(intPtr, memory);
		CrusaderLibrary.Instance.RaiseLibraryLoaded();
	}

	/// <summary>
	/// Detect the soon to be used language code.
	/// </summary>
	internal void InitLanguageProviderEarly()
	{
		_currentCultureName = (string.Equals(LanguageProvider.Value, "detect", StringComparison.InvariantCultureIgnoreCase) ? SteamAppsExtensions.GetCurrentGameCultureName() : LanguageProvider.Value);
		GameAssetManagerAPI.Instance.CurrentLanguage = _currentCultureName;
		LogHelper.Information("Set language provider to [" + _currentCultureName + "]", "InitLanguageProviderEarly", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
	}

	/// <summary>
	/// Initialize the language provider. Allows auto-detect or forceful set.
	/// </summary>
	internal void InitLanguageProviderLate()
	{
		CultureInfo culture = new CultureInfo(_currentCultureName);
		LogHelper.Information("Set language provider to " + _currentCultureName, "InitLanguageProviderLate", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		LocalizationManager.Instance.SetCulture(culture);
		GameAssetManagerAPI.Instance.CurrentLanguage = _currentCultureName;
	}

	/// <summary>
	/// Callback for receiving log messages from the underlying PolyHook2 native library.
	/// </summary>
	private void Log_LogReceived(object sender, LogEventArgs e)
	{
		Serilog.Log.Debug($"PLH::{e.Level}: {e.Message}");
	}

	/// <summary>
	/// Displays a one-time message box to the user on the first run of the script extender.
	/// </summary>
	internal static void ShowFirstTimeMessage()
	{
		if (Instance.FirstStart.Value)
		{
			MinWinAPI.MessageBoxA(IntPtr.Zero, "\nYou're playing Stronghold Crusader Definitive Edition with SHCDE Script Extender (SHCDE:SE)!\n\nWebsite: gitlab.com/rawra-stronghold-crusader/shcde-script-extender\nMake sure to report any bugs that may arise.\n\nCaution: \n- Do not play maps from untrusted sources.\n- Only use the script extender if you downloaded it from the official sources (GitLab, NexusMods, ModDB).\n\nThis message will only appear once.\nEnjoy!", "Script Extender", 64u);
			Instance.FirstStart.Value = false;
		}
	}

	/// <summary>
	/// The primary event handler that is called once the game's main library (`CrusaderDE.dll`) has been loaded into memory.
	/// </summary>
	/// <param name="context">The loaded library context.</param>
	/// <remarks>
	/// This method is the entry point for all game-specific initialization. 
	/// It is responsible for finding game globals, applying all game-function detours, and setting up all high-level APIs like the Lua manager.
	/// </remarks>
	private void CrusaderLibrary_LibraryLoaded(CrusaderLibraryLoadContext context)
	{
		try
		{
			LogHelper.Information("Initializing native-side...", "CrusaderLibrary_LibraryLoaded", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
			AobCacheOptions aobCacheOptions = new AobCacheOptions(Path.Combine(DirectoryHelpers.ModDataDirectory, "aobcache.json"));
			Microsoft.Extensions.Logging.ILogger logger = Instance.LoggerFactory.CreateLogger("Scanner");
			DataScanner scanner = DataScanner.Create(context.Region, logger, aobCacheOptions);
			GameGlobalsManager.Instance.FindGameGlobals(context.Memory, context.Region, scanner);
			DetourManager.Instance.ApplyNative(context.Memory, context.Region, scanner, aobCacheOptions);
			GameMapArchiveManagerAPI.InitializeSubscribers();
			LuaManager.InitializeSubscribers();
			GameNetworkAPI.InitializeSubscribers();
			GamePlayerManagerAPI.InitializeSubscribers();
			GameUnitManagerAPI.InitializeSubscribers();
			GameBuildingManagerAPI.InitializeSubscribers();
			GameMetadataManagerAPI.InitializeSubscribers();
			GameSoundManagerAPI.InitializeSubscribers();
			GameTranslateAPI.InitializeSubscribers();
			GameTriggerManager.InitializeSubscribers();
			GameTimeManagerAPI.InitializeSubscribers();
			GameAfterImageManager.InitializeSubscribers();
			GameBundleManagerAPI.InitializeSubscribers();
			DebugMenuManager.Instance.Initialize();
			MapModManager.Instance.Setup();
			InitLanguageProviderLate();
			if (DumpEmbeddedAICs.Value)
			{
				GameAIManagerAPI.Instance.DumpEmbeddedAICs();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex, "Error during LibraryLoad event", "CrusaderLibrary_LibraryLoaded", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		}
		finally
		{
			LogHelper.Information("Native-side initialization done", "CrusaderLibrary_LibraryLoaded", "I:\\GitLab-Runner\\builds\\dRbf5w-yt\\0\\rawra-stronghold-crusader\\shcde-script-extender\\src\\SHCDESE.BepInEx\\Bootstrap\\Plugin.cs");
		}
	}

	/// <summary>
	/// Binds all the plugin's configuration entries to the BepInEx configuration system.
	/// </summary>
	private void PluginConfigAwake()
	{
		DebugMode = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "DebugMode", false, "Toggle debug mode");
		ExposeCoreCLRToLua = ((BaseUnityPlugin)this).Config.Bind<bool>("LUA", "ExposeCoreCLR", false, "Expose CoreCLR to LUA (Dangerous)");
		ExposeFixedBuffersToLua = ((BaseUnityPlugin)this).Config.Bind<bool>("LUA", "ExposeFixedBuffers", false, "Expose fixed buffers to LUA (Dangerous)");
		AllowScriptedLords = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "AllowScriptedLords", true, "This setting controls whether custom AI lords can use their custom lua scripts");
		MinGameSpeed = ((BaseUnityPlugin)this).Config.Bind<ushort>("General", "MinGameSpeed", (ushort)5, "Minimum game speed (do not go below 1)");
		MaxGameSpeed = ((BaseUnityPlugin)this).Config.Bind<ushort>("General", "MaxGameSpeed", (ushort)1500, "Maximum game speed (do not go above 1500)");
		GameSpeedChangeIncrement = ((BaseUnityPlugin)this).Config.Bind<ushort>("General", "GameSpeedChangeIncrement", (ushort)5, "Game speed change increment");
		LobbyIsolation = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "LobbyIsolation", true, "Toggle lobby isolation");
		AllowTroopActionsInMapEditor = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "AllowTroopActionsInMapEditor", false, "Allows special troop actions within the map editor");
		AsyncLogging = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "AsyncLogging", true, "Should the script extender use async logging?");
		LogTimestamps = ((BaseUnityPlugin)this).Config.Bind<bool>("Bootstrap", "LogTimestamps", true, "Prefix log messages with a local timestamp");
		LogThreadIds = ((BaseUnityPlugin)this).Config.Bind<bool>("Bootstrap", "LogThreadIds", true, "Prefix log messages with the originating managed thread ID");
		EnableNativeCrashHandler = ((BaseUnityPlugin)this).Config.Bind<bool>("Bootstrap", "EnableNativeCrashHandler", true, "Write native crash diagnostics and minidumps to BepInEx/crashdumps");
		MaxNativeCrashDumpFiles = ((BaseUnityPlugin)this).Config.Bind<int>("Bootstrap", "MaxNativeCrashDumpFiles", 5, "Maximum number of files retained in BepInEx/crashdumps. The oldest files are deleted during startup; set to 0 to clear all existing files.");
		AllowChatInNonMultiplayer = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "AllowChatInNonMultiplayer", false, "Should the script extender allow the chat in non-multiplayer matches");
		IgnoreDependencyIncompatibilities = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "IgnoreDependencyIncompatibilities", false, "Should the script extender ignore any mod dependency incompatibilities");
		LogLevel = ((BaseUnityPlugin)this).Config.Bind<LogEventLevel>("Bootstrap", "LogLevel", LogEventLevel.Information, "Log level");
		FirstStart = ((BaseUnityPlugin)this).Config.Bind<bool>("Bootstrap", "FirstStart", true, "Set to false after first start");
		ShowLogo = ((BaseUnityPlugin)this).Config.Bind<bool>("Visual", "ShowLogo", true, "Show the mod logo in the main menu");
		LogoVideoEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("Visual", "LogoVideoEnabled", false, "Enable the decorative video player inside the main-menu logo");
		LogoVideos = ((BaseUnityPlugin)this).Config.Bind<string>("Visual", "LogoVideos", string.Empty, "Ordered, semicolon-separated .webm/.mp4 paths for the diamond logo video (for example: Assets/GUI/Video/SELogo/one.webm;Assets/GUI/Video/SELogo/two.mp4)");
		LogoVideoMuted = ((BaseUnityPlugin)this).Config.Bind<bool>("Visual", "LogoVideoMuted", true, "Mute audio from decorative logo videos");
		LogoStaticEffect = ((BaseUnityPlugin)this).Config.Bind<bool>("Visual", "LogoStaticEffect", true, "Overlay animated analog static and a vignette on the logo video only");
		LogoScanlineEffect = ((BaseUnityPlugin)this).Config.Bind<bool>("Visual", "LogoScanlineEffect", true, "Overlay animated scanlines on the logo video only");
		LogoVideoOpacity = ((BaseUnityPlugin)this).Config.Bind<double>("Visual", "LogoVideoOpacity", 0.85, "Processed logo-video opacity from 0.0 (transparent) to 1.0 (opaque)");
		PokemonCaptionChance = ((BaseUnityPlugin)this).Config.Bind<double>("Visual", "PokemonCaptionChance", 0.03, "Chance from 0.0 to 1.0 of showing 'Not a pokemon!' below Zhuqiaomon for each logo-video loop");
		SkipStronghold4Advert = ((BaseUnityPlugin)this).Config.Bind<bool>("Visual", "SkipStronghold4Advert", true, "Skips the stronghold 4 advert screen");
		LanguageProvider = ((BaseUnityPlugin)this).Config.Bind<string>("Visual", "LanguageProvider", "detect", "Language provider to use. Set to detect for auto-detection.");
		DumpEmbeddedAIVs = ((BaseUnityPlugin)this).Config.Bind<bool>("Export", "DumpEmbeddedAIVs", false, "Dumps the embedded encoded AIVs into binary blobs in the _EXPORT folder in the main game directory");
		DumpEmbeddedAICs = ((BaseUnityPlugin)this).Config.Bind<bool>("Export", "DumpEmbeddedAICs", false, "Dumps the embedded encoded AICs into binary blobs in the _EXPORT folder in the main game directory");
		GenerateLuaDocumentation = ((BaseUnityPlugin)this).Config.Bind<bool>("Export", "GenerateLuaDocumentation", false, "Generate a lua reference md file from existing DocFX summaries");
		AllowMultipleInstances = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "AllowMultipleInstances", false, "Allows multiple instances of the game by bypassing the game's SteamAPI.RestartAppIfNecessary call which enforces that the game was launched through steam: something that you can only do once.");
		EnableEarlySteamInitialization = ((BaseUnityPlugin)this).Config.Bind<bool>("Steam", "EarlyInitialization", true, "Initializes steam early");
		DisableSteamUGCUpdates = ((BaseUnityPlugin)this).Config.Bind<bool>("Steam", "DisableSteamUGCUpdates", false, "Do not request automatic Steam Workshop item downloads from the Script Extender. Steam's own update settings still apply.");
		RequireSignedBepInExPlugins = ((BaseUnityPlugin)this).Config.Bind<bool>("Security", "RequireSignedBepInExPlugins", false, "Require an approved Ed25519 signature for every BepInEx plugin folder. Restart the game after changing this setting.");
		EnableAntiTamper = ((BaseUnityPlugin)this).Config.Bind<bool>("Security", "EnableAntiTamper", true, "Enables some Anti-Tamper functionalities to protect users. This only a very surface-level protection which can be bypassed easily. You still need to trust the author(s) of the mods you download. Do not rely on this. But do not disable this unless you know what youre doing.");
	}
}

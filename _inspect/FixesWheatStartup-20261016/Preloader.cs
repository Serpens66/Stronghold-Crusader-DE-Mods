using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using BepInEx.Preloader.RuntimeFixes;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace BepInEx.Preloader;

/// <summary>
///     The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
/// </summary>
internal static class Preloader
{
	private enum MonoModBackend
	{
		[Description("Auto")]
		auto,
		[Description("DynamicMethod")]
		dynamicmethod,
		[Description("MethodBuilder")]
		methodbuilder,
		[Description("Cecil")]
		cecil
	}

	private static readonly ConfigEntry<string> ConfigEntrypointAssembly = ConfigFile.CoreConfig.Bind("Preloader.Entrypoint", "Assembly", IsPostUnity2017 ? "UnityEngine.CoreModule.dll" : "UnityEngine.dll", "The local filename of the assembly to target.");

	private static readonly ConfigEntry<string> ConfigEntrypointType = ConfigFile.CoreConfig.Bind("Preloader.Entrypoint", "Type", "Application", "The name of the type in the entrypoint assembly to search for the entrypoint method.");

	private static readonly ConfigEntry<string> ConfigEntrypointMethod = ConfigFile.CoreConfig.Bind("Preloader.Entrypoint", "Method", ".cctor", "The name of the method in the specified entrypoint assembly and type to hook and load Chainloader from.");

	internal static readonly ConfigEntry<bool> ConfigApplyRuntimePatches = ConfigFile.CoreConfig.Bind("Preloader", "ApplyRuntimePatches", defaultValue: true, "Enables or disables runtime patches.\nThis should always be true, unless you cannot start the game due to a Harmony related issue (such as running .NET Standard runtime) or you know what you're doing.");

	private static readonly ConfigEntry<MonoModBackend> HarmonyBackend = ConfigFile.CoreConfig.Bind("Preloader", "HarmonyBackend", MonoModBackend.auto, "Specifies which MonoMod backend to use for Harmony patches. Auto uses the best available backend.\nThis setting should only be used for development purposes (e.g. debugging in dnSpy). Other code might override this setting.");

	/// <summary>
	///     The log writer that is specific to the preloader.
	/// </summary>
	private static PreloaderConsoleListener PreloaderLog { get; set; }

	public static bool IsPostUnity2017 { get; } = File.Exists(Path.Combine(Paths.ManagedPath, "UnityEngine.CoreModule.dll"));

	public static void Run()
	{
		try
		{
			InitializeHarmony();
			HarmonyInteropFix.Apply();
			ConsoleManager.Initialize(alreadyActive: false);
			AllocateConsole();
			Utility.TryDo(delegate
			{
				if (ConfigApplyRuntimePatches.Value)
				{
					UnityPatches.Apply();
				}
			}, out var exception);
			Logger.InitializeInternalLoggers();
			Logger.Sources.Add(TraceLogSource.CreateSource());
			PreloaderLog = new PreloaderConsoleListener();
			Logger.Listeners.Add(PreloaderLog);
			Version version = typeof(Paths).Assembly.GetName().Version;
			string text = $"BepInEx {version} - {Paths.ProcessName}";
			if (ConsoleManager.ConsoleActive)
			{
				ConsoleManager.SetConsoleTitle(text);
			}
			Logger.LogMessage($"{text} ({File.GetLastWriteTime(Paths.ExecutablePath)})");
			object[] customAttributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), inherit: false);
			if (customAttributes.Length != 0)
			{
				Logger.LogMessage(((BuildInfoAttribute)customAttributes[0]).Info);
			}
			Logger.LogInfo("Running under Unity v" + GetUnityVersion());
			Logger.LogInfo($"CLR runtime version: {Environment.Version}");
			Logger.LogInfo($"Supports SRE: {Utility.CLRSupportsDynamicAssemblies}");
			Logger.LogInfo($"System platform: {PlatformHelper.Current}");
			if (exception != null)
			{
				Logger.LogWarning("Failed to apply runtime patches for Mono. See more info in the output log. Error message: " + exception.Message);
			}
			Logger.LogMessage("Preloader started");
			AssemblyPatcher.AddPatcher(new PatcherPlugin
			{
				TargetDLLs = () => new string[1] { ConfigEntrypointAssembly.Value },
				Patcher = PatchEntrypoint,
				TypeName = "BepInEx.Chainloader"
			});
			Logger.Log(LogLevel.Info, $"Loaded 1 patcher method from [BepInEx.Preloader {version}]");
			AssemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);
			Logger.LogInfo(string.Format("{0} patcher plugin{1} loaded", AssemblyPatcher.PatcherPlugins.Count, (AssemblyPatcher.PatcherPlugins.Count == 1) ? "" : "s"));
			AssemblyPatcher.PatchAndLoad(Paths.DllSearchPaths);
			AssemblyPatcher.DisposePatchers();
			Logger.LogMessage("Preloader finished");
			Logger.Listeners.Remove(PreloaderLog);
			PreloaderLog.Dispose();
		}
		catch (Exception ex)
		{
			try
			{
				Logger.LogFatal("Could not run preloader!");
				Logger.LogFatal(ex);
				if (!ConsoleManager.ConsoleActive)
				{
					AllocateConsole();
					Console.Write(PreloaderLog);
				}
			}
			catch
			{
			}
			string text2 = string.Empty;
			try
			{
				text2 = string.Join("\r\n", PreloaderConsoleListener.LogEvents.Select((LogEventArgs x) => x.ToString()).ToArray());
				text2 += "\r\n";
				PreloaderLog?.Dispose();
				PreloaderLog = null;
			}
			catch
			{
			}
			File.WriteAllText(Path.Combine(Paths.GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"), text2 + ex);
		}
	}

	/// <summary>
	///     Inserts BepInEx's own chainloader entrypoint into UnityEngine.
	/// </summary>
	/// <param name="assembly">The assembly that will be attempted to be patched.</param>
	public static void PatchEntrypoint(ref AssemblyDefinition assembly)
	{
		if (assembly.MainModule.AssemblyReferences.Any((AssemblyNameReference x) => x.Name.Contains("BepInEx")))
		{
			throw new Exception("BepInEx has been detected to be patched! Please unpatch before using a patchless variant!");
		}
		string entrypointType = ConfigEntrypointType.Value;
		string entrypointMethod = ConfigEntrypointMethod.Value;
		bool flag = entrypointMethod.IsNullOrWhiteSpace() || entrypointMethod == ".cctor";
		TypeDefinition typeDefinition = assembly.MainModule.Types.FirstOrDefault((TypeDefinition x) => x.Name == entrypointType);
		if (typeDefinition == null)
		{
			throw new Exception("The entrypoint type is invalid! Please check your config/BepInEx.cfg file");
		}
		using AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(Paths.BepInExAssemblyPath);
		MethodDefinition method = assemblyDefinition.MainModule.Types.First((TypeDefinition x) => x.Name == "Chainloader").Methods.First((MethodDefinition x) => x.Name == "Initialize");
		MethodDefinition method2 = assemblyDefinition.MainModule.Types.First((TypeDefinition x) => x.Name == "Chainloader").Methods.First((MethodDefinition x) => x.Name == "Start");
		MethodReference method3 = assembly.MainModule.ImportReference(method);
		MethodReference method4 = assembly.MainModule.ImportReference(method2);
		List<MethodDefinition> list = new List<MethodDefinition>();
		if (flag)
		{
			MethodDefinition methodDefinition = typeDefinition.Methods.FirstOrDefault((MethodDefinition m) => m.IsConstructor && m.IsStatic);
			if (methodDefinition == null)
			{
				methodDefinition = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, assembly.MainModule.ImportReference(typeof(void)));
				typeDefinition.Methods.Add(methodDefinition);
				ILProcessor iLProcessor = methodDefinition.Body.GetILProcessor();
				iLProcessor.Append(iLProcessor.Create(OpCodes.Ret));
			}
			list.Add(methodDefinition);
		}
		else
		{
			list.AddRange(typeDefinition.Methods.Where((MethodDefinition x) => x.Name == entrypointMethod));
		}
		if (!list.Any())
		{
			throw new Exception("The entrypoint method is invalid! Please check your config.ini");
		}
		foreach (MethodDefinition item in list)
		{
			ILProcessor iLProcessor2 = item.Body.GetILProcessor();
			Instruction target = iLProcessor2.Body.Instructions.First();
			iLProcessor2.InsertBefore(target, iLProcessor2.Create(OpCodes.Ldnull));
			iLProcessor2.InsertBefore(target, iLProcessor2.Create(OpCodes.Ldc_I4_0));
			iLProcessor2.InsertBefore(target, iLProcessor2.Create(OpCodes.Call, assembly.MainModule.ImportReference(AccessTools.PropertyGetter(typeof(PreloaderConsoleListener), "LogEvents"))));
			iLProcessor2.InsertBefore(target, iLProcessor2.Create(OpCodes.Call, method3));
			iLProcessor2.InsertBefore(target, iLProcessor2.Create(OpCodes.Call, method4));
		}
	}

	/// <summary>
	///     Allocates a console window for use by BepInEx safely.
	/// </summary>
	public static void AllocateConsole()
	{
		if (!ConsoleManager.ConsoleEnabled)
		{
			return;
		}
		try
		{
			ConsoleManager.CreateConsole();
			Logger.Listeners.Add(new ConsoleLogListener());
		}
		catch (Exception data)
		{
			Logger.LogError("Failed to allocate console!");
			Logger.LogError(data);
		}
	}

	public static string GetUnityVersion()
	{
		if (PlatformHelper.Is(Platform.Windows))
		{
			return FileVersionInfo.GetVersionInfo(Paths.ExecutablePath).FileVersion;
		}
		return "Unknown (" + (IsPostUnity2017 ? "post" : "pre") + "-2017)";
	}

	private static void InitializeHarmony()
	{
		switch (HarmonyBackend.Value)
		{
		case MonoModBackend.dynamicmethod:
		case MonoModBackend.methodbuilder:
		case MonoModBackend.cecil:
			Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", HarmonyBackend.Value.ToString());
			break;
		default:
			throw new ArgumentOutOfRangeException("HarmonyBackend", HarmonyBackend.Value, "Unknown backend");
		case MonoModBackend.auto:
			break;
		}
	}
}

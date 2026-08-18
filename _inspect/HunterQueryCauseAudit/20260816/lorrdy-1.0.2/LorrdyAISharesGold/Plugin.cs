using System;
using BepInEx;
using LorrdyAISharesGold.UI;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;

namespace LorrdyAISharesGold;

[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInPlugin("LorrdyAISharesGold", "Lorrdy AI Shares Gold", "1.0.2")]
public class Plugin : BaseUnityPlugin
{
	public static LobbySettingsViewModel LobbySettingsViewModel { get; private set; }

	private void Awake()
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		((BaseUnityPlugin)this).Logger.LogInfo((object)"Plugin is initializing...");
		LobbySettingsViewModel = new LobbySettingsViewModel();
		GameXAMLManagerAPI.Instance.RegisterLobbyModSettings((BaseUnityPlugin)(object)this, "Lorrdy AI Shares Gold", (object)LobbySettingsViewModel, "XAMLResources/LorrdyAISharesGoldSettings.xaml");
		CrusaderLibrary.Instance.LibraryLoaded += new OnLibraryLoadedDelegate(OnLibraryLoaded);
	}

	private void OnLibraryLoaded(IntPtr moduleHandle, System.ReadOnlySpan<byte> memory)
	{
		((BaseUnityPlugin)this).Logger.LogInfo((object)"Game Library Loaded! APIs are now safe to use.");
		InitializeGameLogic();
	}

	private void InitializeGameLogic()
	{
		try
		{
			ObservableSubscribeExtensions.Subscribe<MapStartEventArgs>(MapLoaderR3EventHooks.OnStartMap.Observable, (Action<MapStartEventArgs>)ShareGold.OnStartMap);
		}
		catch (Exception ex)
		{
			((BaseUnityPlugin)this).Logger.LogError((object)("Error initializing game logic: " + ex.Message));
		}
	}
}

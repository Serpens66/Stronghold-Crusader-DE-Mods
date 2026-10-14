using System;
using System.Collections.Generic;
using BepInEx;
using APIShared;
using APIShared.ModSettings;
using ExtendedData;
using SHCDESE.API.Components.Network;
public sealed class ExampleSettings : PresetLobbyModSettingsViewModel
{
    private bool enableMod = true;
    private int strength = 100;

    [SyncHostOnly]
    public bool EnableMod
    {
        get => enableMod;
        set
        {
            if (!CanMutateSetting() || enableMod == value) return;
            enableMod = value;
            OnPropertyChanged(nameof(EnableMod));
        }
    }

    [SyncHostOnly]
    public int Strength
    {
        get => strength;
        set
        {
            if (!CanMutateSetting() || strength == value) return;
            strength = value;
            OnPropertyChanged(nameof(Strength));
        }
    }
}
public sealed class DocumentRegistrationFixture : BaseUnityPlugin
{
    private static readonly ExampleSettings Settings = new ExampleSettings();
    public void CheckRegistration()
    {LobbyModSettingsPresetRegistration.Register(
    this, Logger, Info.Metadata.Name, Settings,
    "ScriptExtenderUI/ExampleSettings.xaml");
    }
    public static void CheckMapRead()
    {ExtendedDataModDataReadResult result =
    ExtendedDataModDataApi.ReadCurrentMapNamespace("author.example-mod");

if (result.Success)
{
    IReadOnlyDictionary<string, object> data = result.Data;
    string namespaceJson = result.Json;
}

    }
    public static void CheckDocumentedBindings() { object[] values = { Settings.System_OpenPresetLoadCommand, Settings.System_OpenPresetSaveCommand, Settings.System_PresetLoadEntries, Settings.System_PresetLoadText, Settings.System_PresetSaveSettings, Settings.System_PresetSaveTargets, Settings.System_PresetSaveText, Settings.System_PresetStatusText, Settings.System_PresetStatusVisibility, Settings.System_SelectedPresetLoadEntry }; }
public static void CheckSettingsTypes()
    {
        Type[] contracts = { typeof(IModSettingsPresetEndpoint), typeof(IModSettingsWorkingCopyEndpoint),
            typeof(ModSettingsWorkingSource), typeof(IDynamicPresetSettingsProvider),
            typeof(IModSettingsApplicationBackend), typeof(ModSettingsSearch), typeof(ModSettingsMode) };
    }
}
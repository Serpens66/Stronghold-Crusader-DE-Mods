// Engine/UI boundary doubles only; production preset logic is source-linked.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
namespace BepInEx
{
    public class BaseUnityPlugin { public PluginInfo Info { get; } = new PluginInfo(); }
    public sealed class PluginInfo
    {
        public string Location { get; set; } = typeof(ExtraFeatures.Tests.PresetEntry).Assembly.Location;
        public PluginMetadata Metadata { get; } = new PluginMetadata();
    }
    public sealed class PluginMetadata
    {
        public string GUID { get; set; } = "ExtraFeatures_Serp";
        public Version Version { get; set; } = new Version(1, 0, 0);
    }
}

namespace BepInEx.Logging { public sealed class ManualLogSource { } }

namespace Noesis
{
    public enum Visibility { Visible, Hidden, Collapsed }
    public sealed class ComboBoxItem { public object Content { get; set; } public Visibility Visibility { get; set; } }
}

namespace SHCDESE.EventAPI
{
    public enum EventHookPhase
    {
        Pre,
        Post
    }
}

namespace SHCDESE.ViewModels
{
    public class LobbyModSettingsBaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal bool IsSuppressingSync { get; private set; }
        internal bool IsApplyingAuthorisedUpdate { get; private set; }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void System_TriggerUpdate(string name) => OnPropertyChanged(name);
        internal void BeginAuthorisedUpdate() => IsApplyingAuthorisedUpdate = true;
        internal void EndAuthorisedUpdate() => IsApplyingAuthorisedUpdate = false;

        protected bool CanEdit(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || IsApplyingAuthorisedUpdate)
                return true;

            PropertyInfo property = GetType().GetProperty(propertyName);
            if (property?.GetCustomAttribute<SyncHostOnlyAttribute>() == null ||
                !GameNetworkAPI.IsNetworkedEnvironment() ||
                GameNetworkAPI.IsLocalHost())
            {
                return true;
            }

            NotifyRevert(propertyName);
            return false;
        }

        private void NotifyRevert(string propertyName)
        {
            IsSuppressingSync = true;
            try
            {
                OnPropertyChanged(propertyName);
            }
            finally
            {
                IsSuppressingSync = false;
            }
        }
    }
}

namespace SHCDESE.API.Components.Network
{
    [AttributeUsage(AttributeTargets.Property)] public sealed class SyncHostOnlyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public sealed class SyncPerPlayerAttribute : Attribute { }
}

namespace SHCDESE.API.Components.ModManager
{
    [AttributeUsage(AttributeTargets.Property)] public sealed class PersistLocalAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public sealed class DoNotPersistAttribute : Attribute { }

    public enum LobbyModSettingsChangeOrigin { Local, IncomingNetwork, InitialLoad, UiRevert }

    public sealed class LobbyModSettingsStorage
    {
        public const string STORAGE_FOLDER_NAME = "LobbyModSettings";
        public const string FILE_EXTENSION = ".msgpack";

        private readonly string filePath;

        public LobbyModSettingsStorage(string pluginAssemblyLocation, string modName)
        {
            string safeName = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
            filePath = Path.Combine(Path.GetDirectoryName(pluginAssemblyLocation),
                STORAGE_FOLDER_NAME, safeName + FILE_EXTENSION);
        }

        public void Load(object viewModel)
        {
            if (!File.Exists(filePath))
                return;
            Dictionary<string, byte[]> payload =
                MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(filePath));
            foreach (PropertyInfo property in viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool persisted = (property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncHostOnlyAttribute>() != null ||
                    property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncPerPlayerAttribute>() != null ||
                    property.GetCustomAttribute<PersistLocalAttribute>() != null) &&
                    property.GetCustomAttribute<DoNotPersistAttribute>() == null;
                if (persisted && property.CanWrite && payload.TryGetValue(property.Name, out byte[] bytes))
                    property.SetValue(viewModel, MessagePackSerializer.Deserialize(property.PropertyType, bytes));
            }
        }
    }

    public sealed class ModInfo
    {
        public string GUID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class GameAssetModManager
    {
        public static GameAssetModManager Instance { get; } = new GameAssetModManager();

        public static List<KeyValuePair<ModInfo, string>> RegisteredAssetDirectories { get; } =
            new List<KeyValuePair<ModInfo, string>>();

        public IEnumerable<KeyValuePair<ModInfo, string>> GetRegisteredAssetDirectories() =>
            RegisteredAssetDirectories;
    }
}

namespace SHCDESE.API
{
    public static class GameNetworkAPI
    {
        public static bool LocalHost = true;
        public static bool Networked = true;
        public static bool ThrowOnRoleQuery;
        public static bool MultiplayerGame
        {
            get => Platform_Multiplayer.MPGameActive;
            set => Platform_Multiplayer.MPGameActive = value;
        }
        public static bool IsNetworkedEnvironment() => Networked;
        public static bool IsMultiplayerGame() => Platform_Multiplayer.MPGameActive;
        public static byte[] Serialize<T>(T packet) => MessagePackSerializer.Serialize(packet);
        public static bool IsLocalHost()
        {
            if (ThrowOnRoleQuery)
                throw new InvalidOperationException("simulated unavailable network singleton");
            return LocalHost;
        }
    }
    public sealed class GameXAMLManagerAPI
    {
        private Dictionary<string, byte[]> routingCache =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public static GameXAMLManagerAPI Instance { get; } = new GameXAMLManagerAPI();
        public SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin CurrentLobbyModSettingsChangeOrigin { get; private set; }
        // External Extender boundary: nested origins restore their predecessor and
        // callback failures are swallowed there. APIShared's real wrapper rethrows.
        internal void RunWithChangeOrigin(SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin origin, Action action)
        {
            var previous = CurrentLobbyModSettingsChangeOrigin;
            CurrentLobbyModSettingsChangeOrigin = origin;
            try { action(); }
            catch { }
            finally { CurrentLobbyModSettingsChangeOrigin = previous; }
        }
        public List<Registration> RegisteredModSettings { get; } = new List<Registration>();
        public int BroadcastCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool LastUseBuiltInPersistence { get; private set; }
        public bool FailNextRegistration { get; set; }
        public bool ThrowNextRegistration { get; set; }

        public void RegisterLobbyModSettings(global::BepInEx.BaseUnityPlugin plugin, string name, object vm, string xaml)
            => RegisterLobbyModSettings(plugin, name, vm, xaml, true);

        public void RegisterLobbyModSettings(global::BepInEx.BaseUnityPlugin plugin, string name, object vm, string xaml,
            bool useBuiltInPersistence)
        {
            if (ThrowNextRegistration)
            {
                ThrowNextRegistration = false;
                throw new InvalidOperationException("Synthetic registration failure.");
            }
            if (FailNextRegistration)
            {
                FailNextRegistration = false;
                return;
            }
            LastUseBuiltInPersistence = useBuiltInPersistence;
            RegisteredModSettings.Add(new Registration
            {
                ViewModel = vm,
                View = new object()
            });
            if (!(vm is INotifyPropertyChanged notify))
                return;

            notify.PropertyChanged += (sender, args) =>
            {
                if (CurrentLobbyModSettingsChangeOrigin == SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin.IncomingNetwork ||
                    CurrentLobbyModSettingsChangeOrigin == SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin.InitialLoad ||
                    sender is SHCDESE.ViewModels.LobbyModSettingsBaseViewModel revertingVm && revertingVm.IsSuppressingSync ||
                    sender == null ||
                    string.IsNullOrEmpty(args.PropertyName))
                {
                    return;
                }

                PropertyInfo property = sender.GetType().GetProperty(args.PropertyName);
                if (property == null)
                    return;

                bool hostOnly = property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncHostOnlyAttribute>() != null;
                bool perPlayer = property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncPerPlayerAttribute>() != null;
                bool synced = hostOnly || perPlayer;
                bool persisted =
                    (synced || property.GetCustomAttribute<SHCDESE.API.Components.ModManager.PersistLocalAttribute>() != null) &&
                    property.GetCustomAttribute<SHCDESE.API.Components.ModManager.DoNotPersistAttribute>() == null;
                if (!synced && !persisted)
                    return;
                if (hostOnly && GameNetworkAPI.IsNetworkedEnvironment() && !GameNetworkAPI.IsLocalHost())
                    return;

                if (synced && GameNetworkAPI.IsNetworkedEnvironment())
                    BroadcastCount++;
                if (persisted && useBuiltInPersistence)
                    SaveSnapshot(sender);
            };
        }

        public void ResetRoutingProbe()
        {
            RegisteredModSettings.Clear();
            FailNextRegistration = false;
            ThrowNextRegistration = false;
            LastUseBuiltInPersistence = false;
            routingCache = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            ResetRoutingCounts();
        }

        public void ResetRoutingCounts()
        {
            BroadcastCount = 0;
            SaveCount = 0;
        }

        public void PrimeStoredInt(string propertyName, int value) =>
            routingCache[propertyName] = MessagePackSerializer.Serialize(value);

        public bool HasStoredValue(string propertyName) => routingCache.ContainsKey(propertyName);

        public int ReadStoredInt(string propertyName) =>
            MessagePackSerializer.Deserialize<int>(routingCache[propertyName]);

        private void SaveSnapshot(object viewModel)
        {
            var payload = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (PropertyInfo property in viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool hostOnly = property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncHostOnlyAttribute>() != null;
                bool synced = hostOnly ||
                    property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncPerPlayerAttribute>() != null;
                bool persisted =
                    (synced || property.GetCustomAttribute<SHCDESE.API.Components.ModManager.PersistLocalAttribute>() != null) &&
                    property.GetCustomAttribute<SHCDESE.API.Components.ModManager.DoNotPersistAttribute>() == null;
                if (!property.CanRead || !persisted)
                    continue;

                if (hostOnly && !GameNetworkAPI.IsLocalHost())
                {
                    if (routingCache.TryGetValue(property.Name, out byte[] cached))
                        payload[property.Name] = (byte[])cached.Clone();
                    continue;
                }

                object value = property.GetValue(viewModel);
                if (value != null)
                    payload[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, value);
            }

            routingCache = payload;
            SaveCount++;
        }

        public void ApplyNetworkSync(SHCDESE.ViewModels.LobbyModSettingsBaseViewModel viewModel, Action action)
        {
            SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin previous = CurrentLobbyModSettingsChangeOrigin;
            CurrentLobbyModSettingsChangeOrigin = SHCDESE.API.Components.ModManager.LobbyModSettingsChangeOrigin.IncomingNetwork;
            viewModel.BeginAuthorisedUpdate();
            try
            {
                action();
            }
            finally
            {
                viewModel.EndAuthorisedUpdate();
                CurrentLobbyModSettingsChangeOrigin = previous;
            }
        }
        public sealed class Registration
        {
            public object ViewModel { get; set; }
            public object View { get; set; }
        }
    }
}

#pragma warning disable 0649
internal sealed class Director
{
    public static Director instance;
    public bool MultiplayerGame;
    public bool SkirmishModeGame;
}

internal sealed class GameData
{
    public static GameData Instance;
    public int game_type = -1;
    public int SkirmishGameType = -1;
    public int SkirmishTrailType = -1;
    public int coopTrailID = -1;
    public Enums.GameModes mapType = Enums.GameModes.BUILD;
    public bool IsSandsOfTime() =>
        game_type == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
        SkirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL &&
        SkirmishTrailType >= (int)APIShared.GameModes.GameTrailType.SandsOne &&
        SkirmishTrailType <= (int)APIShared.GameModes.GameTrailType.SandsEight;
}

public static class Enums
{
    public enum Goods { }
    public enum eTextSections { TEXT_GOODS }

    internal enum eGameTypeModes
    {
        GAMETYPE_CAMPAIGN = 0,
        GAMETYPE_BUILDER = 1,
        GAMETYPE_MAP = 2,
        GAMETYPE_MULTIPLAYER = 3,
        GAMETYPE_TUTORIAL = 4,
    }

    internal enum eSkirmishGameMode
    {
        SKIRMISH_GAME_NOT_SKIRMISH = -1,
        SKIRMISH_GAME_CUSTOM = 0,
        SKIRMISH_GAME_TRAIL = 1,
        SKIRMISH_GAME_CUSTOM_TRAIL = 2,
        SKIRMISH_GAME_TEST_MISSION = 3,
    }

    internal enum GameModes
    {
        BUILD = 0,
        ECO = 1,
        SIEGE = 2,
        INVASION = 3,
        MAP_EDITOR = 10,
    }
}

public sealed class Platform_Multiplayer
{
    public static Platform_Multiplayer instance = new Platform_Multiplayer();
    public static Platform_Multiplayer Instance => instance;
    public static bool MPGameActive;

    public MPLobby activeLobby;
    public List<MPGameMember> gameMembers;
    public bool IsHost;

    public struct TestSteamId
    {
        public ulong m_SteamID;
    }

    public sealed class MPLobby
    {
        public TestSteamId id;
        public List<MPLobbyMember> members;
    }

    public sealed class MPLobbyMember
    {
        public bool SkirmishMember;
    }

    public sealed class MPGameMember
    {
        public bool skirmishAI;
        public ulong steamID;
    }
}
#pragma warning restore 0649

namespace SHCDESE.API
{
    public sealed class GamePlayerManagerAPI
    {
        public static GamePlayerManagerAPI Instance { get; } = new GamePlayerManagerAPI();
        public bool MapEditor { get; set; }
        public bool IsInMapEditor() => MapEditor;
    }
}

namespace SHCDESE.EventAPI.MapLoader
{
    internal sealed class MapStartEventArgs
    {
        public EventHookPhase Phase { get; set; }
        public byte bMultiplayerSave { get; set; }
        public int CampaignMapId { get; set; }
    }

    internal sealed class MapLoadEventArgs
    {
        public uint CampaignMapID { get; set; }
        public byte bMultiplayerSave { get; set; }
        public int TrailType { get; set; }
    }

    internal sealed class LoadSaveGameEventArgs
    {
        public LoadSaveGameEventArgs(bool loadingEditorMap) => LoadingEditorMap = loadingEditorMap;
        public EventHookPhase Phase { get; set; }
        public long ReturnValue { get; set; }
        public string FileName { get; set; }
        public bool LoadingEditorMap { get; }
    }
}

namespace SHCDESE.BepInEx.Bootstrap
{
    public static class Plugin { public static SHCDESE.ViewModels.LobbyModSettingsBaseViewModel ModSettingsHubViewModel { get; } = new SHCDESE.ViewModels.LobbyModSettingsBaseViewModel(); }
}

namespace CrusaderDE
{
    public sealed class MainViewModel
    {
        public System.Collections.Generic.List<Noesis.ImageSource> GameSprites { get; } = new System.Collections.Generic.List<Noesis.ImageSource>();
        public Noesis.ImageSource LoadImageFile(byte[] bytes) => null;
        public Noesis.ImageSource getSmallGoodsIcon(int good) => null;
        public int goodsSpriteEnumFromGoodsEnum(Enums.Goods good) => -1;

        private static readonly MainViewModel Value = new MainViewModel();
        public static bool viewModelLoaded;
        public static int InstanceReadCount { get; private set; }
        public bool IsMapEditorMode { get; set; }

        public static MainViewModel Instance
        {
            get
            {
                InstanceReadCount++;
                if (!viewModelLoaded)
                    throw new InvalidOperationException("MainViewModel is not loaded.");
                return Value;
            }
        }

        public static void Reset()
        {
            viewModelLoaded = false;
            InstanceReadCount = 0;
            Value.IsMapEditorMode = false;
        }
    }

    public sealed class Translate
    {
        public string lookUpText(Enums.eTextSections section, int id) => id.ToString();

        public static Translate Instance { get; } = new Translate();
        public Dictionary<string, string> GameTexts { get; } = new Dictionary<string, string>();
    }
}

namespace APIShared.Internal
{
    // API logging boundary double; the APIShared tests exercise its real logger.
    internal static class DebugLogHelper
    {
        public static void LogDebug(BepInEx.Logging.ManualLogSource log, string text) { }
        public static void LogDebug(BepInEx.Logging.ManualLogSource log, Func<string> text) { }
        public static void LogInfo(BepInEx.Logging.ManualLogSource log, string text) { }
        public static void LogWarning(BepInEx.Logging.ManualLogSource log, string text) { }
        public static void LogError(BepInEx.Logging.ManualLogSource log, string text) { }
    }
}


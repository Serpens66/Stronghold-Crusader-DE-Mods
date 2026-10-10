using System;
using System.Globalization;
using System.IO;
using System.Linq;
using CrusaderDETweaker.Config.Sync;
using SHCDESE.API.Components.ModManager;

namespace CrusaderDETweaker.Configuration
{
    // Uses the public Extender persistence seam. Personal and temporary settings retain
    // the same MessagePack envelope; network-received values are never saved here.
    internal sealed class ConfigurationLobbyStorage : ILobbyModSettingsStorage
    {
        private readonly string personalPath;
        internal bool Loaded { get; private set; }
        internal Exception LoadError { get; private set; }

        internal ConfigurationLobbyStorage(string pluginLocation)
        {
            string name = string.Concat(PluginInfo.PLUGIN_NAME.Split(Path.GetInvalidFileNameChars()));
            personalPath = Path.Combine(Path.GetDirectoryName(pluginLocation),
                LobbyModSettingsStorage.STORAGE_FOLDER_NAME, name + LobbyModSettingsStorage.FILE_EXTENSION);
            var document = new LobbyConfiguration(
                LobbyMaxCounts.UnitTypes.Select(x => x.ToString()).Concat(LobbyMaxCounts.BuildingTypes.Select(x => x.ToString())),
                ParseStrict, LobbyMaxCounts.Encode, IsCanonicalValue);
            ConfigurationApi.ConfigureLobby(document, personalPath,
                () => ConfigSyncManager.Lobby?.MaxCounts ?? "", ApplySelected);
        }

        private static bool IsCanonicalValue(string text)
        {
            if (!LobbyMaxCounts.TryParseValue(text, out int? value)) return false;
            return text == (value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "");
        }

        private static System.Collections.Generic.Dictionary<string, int> ParseStrict(string text)
        {
            var errors = new System.Collections.Generic.List<string>();
            var values = LobbyMaxCounts.Parse(text, errors);
            if (errors.Count != 0) throw new InvalidDataException(string.Join("; ", errors));
            return values;
        }

        internal static byte[] ReadFile(string path)
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidDataException("Lobby settings exceed 8 MiB.");
            return File.ReadAllBytes(path);
        }

        public void Load(object viewModel)
        {
            try
            {
                var settings = (ConfigSyncLobbySettings)viewModel;
                byte[] own = ReadFile(personalPath);
                string selected = LobbyConfiguration.ReadEncoded(ReadFile(ConfigurationApi.SelectedLobbyPath));
                ParseStrict(selected);
                settings.SyncEnabled = LobbyConfiguration.ReadSyncEnabled(own);
                settings.MaxCounts = selected;
                Loaded = true;
                LoadError = null;
            }
            catch (Exception error)
            {
                LoadError = error;
                throw;
            }
        }

        public void Save(object viewModel, string propertyName)
        {
            var settings = (ConfigSyncLobbySettings)viewModel;
            if (propertyName == nameof(settings.MaxCounts))
            {
                string path = ConfigurationApi.SelectedLobbyPath;
                byte[] updated = LobbyConfiguration.WithValue(ReadFile(path), propertyName, settings.MaxCounts);
                ConfigurationFileTransaction.AtomicWrite(path, updated);
            }
            else if (propertyName == nameof(settings.SyncEnabled))
            {
                byte[] updated = LobbyConfiguration.WithValue(ReadFile(personalPath), propertyName, settings.SyncEnabled);
                ConfigurationFileTransaction.AtomicWrite(personalPath, updated);
            }
        }

        private void ApplySelected(string values)
        {
            if (!Loaded || ConfigSyncManager.Lobby == null)
                throw new InvalidOperationException("Lobby settings were not loaded successfully.", LoadError);
            ConfigSyncManager.Lobby.RestoreOwnMaxCounts(values);
            ConfigSyncManager.OnMaxCountsSet(values, values);
        }
    }
}

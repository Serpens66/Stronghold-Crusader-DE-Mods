using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using CrusaderDE;
using MessagePack;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;

namespace Shared;

/// <summary>
/// Adds two local presets to a Script Extender lobby-settings ViewModel while
/// keeping the outer MessagePack dictionary readable by the Script Extender.
/// </summary>
public abstract class PresetLobbyModSettingsViewModel : LobbyModSettingsBaseViewModel, IModSettingsWorkingCopyEndpoint, IModSettingsPresetEndpoint, IModSettingsMissionSourceEndpoint
{
	private enum SettingsMenuContext
	{
		Other,
		Campaign,
		DirectTrail,
		CustomizeSetup
	}

	private sealed class PresetController
	{
		internal const string SchemaVersionKey = "__SerpPresetSchemaVersion";

		internal const string ActivePresetKey = "__SerpActivePreset";

		internal const string Preset1Key = "__SerpPreset1";

		internal const string Preset2Key = "__SerpPreset2";

		internal const string PublishedPresetKey = "__SerpPublishedPreset";

		internal const string CurrentSettingsKey = "__SerpCurrentSettings";

		internal const string BasedOnPresetKey = "__SerpBasedOnPreset";

		internal const string PresetDirtyKey = "__SerpPresetDirty";

		internal const string LegacyPresetImportCompletedKey = "__SerpLegacyPresetImportCompleted";

		private const int SchemaVersion = 3;

		private readonly PresetLobbyModSettingsViewModel owner;

		private readonly ManualLogSource log;

		private readonly string modName;

		private readonly bool routineLoggingEnabled;

		private readonly string filePath;

		private readonly string pluginDirectory;

		private readonly string personalPresetDirectory;

		private readonly string targetGuid;

		private readonly Version targetVersion;

		private readonly IDynamicPresetSettingsProvider dynamicProvider;

		private readonly PresetPropertyAccessor[] persistedProperties;

		private readonly PresetPropertyAccessor[] hostProperties;

		private readonly PresetPropertyAccessor[] clientProperties;

		private readonly PresetPropertyAccessor hostSettingsActivationProperty;

		private readonly PresetPropertyAccessor clientSettingsActivationProperty;

		private readonly Dictionary<string, PresetPropertyAccessor> persistedPropertiesByName;

		private readonly List<PublishedModSettingsPreset> publishedPresets = new List<PublishedModSettingsPreset>();

		private Dictionary<string, byte[]> defaults;

		private Dictionary<string, byte[]> preset1;

		private Dictionary<string, byte[]> missionPreset;

		private bool active;

		private bool applying;

		private PublishedModSettingsPreset activePublishedPreset;

		private PublishedModSettingsPreset suspendedPublishedPreset;

		private string basedOnStableId = string.Empty;

		private string basedOnName = string.Empty;

		private string basedOnSource = string.Empty;

		private bool presetDirty;

		private bool legacyPresetImportCompleted;

		private bool legacyMigrationPending;

		private string suspendedBasedOnStableId = string.Empty;

		private string suspendedBasedOnName = string.Empty;

		private string suspendedBasedOnSource = string.Empty;

		private bool suspendedPresetDirty;

		private string missionPresetLabel = string.Empty;

		private PublishedModSettingsPreset missionBasedOnPreset;

		private bool missionPresetDirty;

		public IReadOnlyList<PublishedModSettingsPreset> PublishedPresets => publishedPresets;

		public string TargetGuid => targetGuid;

		public bool HasPersistentSettings => persistedProperties.Length != 0;

		public int MissionPresetIndex => 1;

		public bool HasHostSettings => hostProperties.Length != 0;

		public bool HasClientSettings => clientProperties.Length != 0;

		public bool HasHostSettingsActivation => hostSettingsActivationProperty != null;

		public bool HasClientSettingsActivation => clientSettingsActivationProperty != null;

		public bool HostSettingsEnabled => ReadSettingsActivation(hostSettingsActivationProperty);

		public bool ClientSettingsEnabled => ReadSettingsActivation(clientSettingsActivationProperty);

		public bool IsApplyingSnapshot => applying;

		public PresetController(PresetLobbyModSettingsViewModel owner, ManualLogSource log, string pluginAssemblyLocation, string modName, string targetGuid, Version targetVersion, bool logRoutineActivity)
		{
			this.owner = owner ?? throw new ArgumentNullException("owner");
			this.log = log;
			this.modName = modName ?? throw new ArgumentNullException("modName");
			routineLoggingEnabled = logRoutineActivity;
			pluginDirectory = Path.GetDirectoryName(pluginAssemblyLocation) ?? throw new ArgumentException("Cannot determine the plugin directory for [" + pluginAssemblyLocation + "].", "pluginAssemblyLocation");
			this.targetGuid = ModSettingsPresetCatalog.ValidateTargetGuid(targetGuid ?? throw new ArgumentNullException("targetGuid"));
			this.targetVersion = targetVersion;
			string text = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
			filePath = Path.Combine(pluginDirectory, "LobbyModSettings", text + ".msgpack");
			personalPresetDirectory = Path.Combine(pluginDirectory, "LobbyModSettings", "Presets", "Override", this.targetGuid);
			dynamicProvider = owner.DynamicSettingsProvider;
			persistedProperties = ((dynamicProvider != null) ? (from item in dynamicProvider.GetSettings()
				select new PresetPropertyAccessor(item, dynamicProvider)).ToArray() : (from item in ((object)owner).GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
				select new PresetPropertyAccessor(item)).Where(IsPersistedProperty).ToArray());
			if (persistedProperties.Length > 16384)
			{
				throw new InvalidDataException("Too many preset settings.");
			}
			if (persistedProperties.Any((PresetPropertyAccessor x) => x.RequiresRestart) && owner.SettingsApplicationBackend == null)
			{
				throw new InvalidOperationException("RequiresRestart settings need a configuration application backend.");
			}
			persistedPropertiesByName = persistedProperties.ToDictionary((PresetPropertyAccessor property) => property.Name, StringComparer.Ordinal);
			hostProperties = persistedProperties.Where(IsHostProperty).ToArray();
			clientProperties = persistedProperties.Where(IsClientProperty).ToArray();
			hostSettingsActivationProperty = FindSettingsActivationProperty(hostProperties, "EnableMod");
			clientSettingsActivationProperty = FindSettingsActivationProperty(clientProperties, "EnableClientFeatures", "EnableMod");
			if (persistedProperties.Length != 0)
			{
				RefreshCatalog();
			}
		}

		internal void RestorePreparationLabel(string label)
		{
			missionBasedOnPreset = null;
			missionPresetLabel = label;
		}

		public string GetStatusText(string basedOnText, string modifiedText, string personalSourceText, string bundledSourceText, string externalSourceText)
		{
			bool isMissionPresetSelected = owner.IsMissionPresetSelected;
			if (isMissionPresetSelected && missionBasedOnPreset == null)
			{
				return missionPresetLabel;
			}
			string text = (isMissionPresetSelected ? missionBasedOnPreset.Name : basedOnName);
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}
			string text2 = (basedOnText ?? "Based on") + ": " + text;
			PublishedModSettingsPreset publishedModSettingsPreset = (isMissionPresetSelected ? missionBasedOnPreset : activePublishedPreset);
			string text3 = ((publishedModSettingsPreset == null) ? basedOnSource : DescribeSource(publishedModSettingsPreset, personalSourceText, bundledSourceText, externalSourceText));
			if (!string.IsNullOrWhiteSpace(text3))
			{
				text2 = text2 + " · " + text3;
			}
			if (isMissionPresetSelected ? missionPresetDirty : presetDirty)
			{
				text2 = text2 + " (" + (modifiedText ?? "modified") + ")";
			}
			return text2;
		}

		public void RefreshCatalog()
		{
			if (persistedProperties.Length == 0)
			{
				publishedPresets.Clear();
				return;
			}
			Directory.CreateDirectory(personalPresetDirectory);
			List<PublishedModSettingsPreset> list = new List<PublishedModSettingsPreset>();
			foreach (PublishedModSettingsPreset item in ModSettingsPresetCatalog.Discover(targetGuid, targetVersion, pluginDirectory, personalPresetDirectory, log))
			{
				try
				{
					ValidatePublishedPreset(item);
					list.Add(item);
				}
				catch (Exception ex)
				{
					DebugLogHelper.LogError(log, "Published preset [" + item.SourcePath + "] is incompatible with [" + modName + "]: " + ex.Message);
				}
			}
			publishedPresets.Clear();
			publishedPresets.AddRange(list);
			activePublishedPreset = publishedPresets.FirstOrDefault((PublishedModSettingsPreset item) => string.Equals(item.StableId, basedOnStableId, StringComparison.OrdinalIgnoreCase));
			if (activePublishedPreset != null)
			{
				basedOnName = activePublishedPreset.Name;
				basedOnSource = DescribeSource(activePublishedPreset);
			}
			bool flag = active && basedOnStableId.Length != 0 && activePublishedPreset == null;
			if (flag)
			{
				LogRoutine("[" + modName + "] Preset source [" + SanitizeLogValue(basedOnStableId) + "] is unavailable; retained the materialized working settings.");
				basedOnStableId = string.Empty;
				basedOnName = string.Empty;
				basedOnSource = string.Empty;
				presetDirty = false;
			}
			if (flag && active)
			{
				WriteCombinedPayload();
			}
		}

		public string CreateUniquePersonalPresetId(string name)
		{
			string text = CreatePersonalPresetId(name);
			string text2 = text;
			int num = 2;
			HashSet<string> hashSet = new HashSet<string>(from item in publishedPresets
				where item.SourceKind == ModSettingsPresetSourceKind.Personal
				select item.Id, StringComparer.OrdinalIgnoreCase);
			while (hashSet.Contains(text2) || File.Exists(Path.Combine(personalPresetDirectory, "preset_" + text2 + ".json")))
			{
				text2 = text + "-" + num++;
			}
			return text2;
		}

		private static string CreatePersonalPresetId(string name)
		{
			string text = (name ?? string.Empty).Trim();
			if (text.Length == 0)
			{
				throw new InvalidDataException("A preset name is required.");
			}
			StringBuilder stringBuilder = new StringBuilder(text.Length);
			bool flag = false;
			string text2 = text.ToLowerInvariant();
			foreach (char c in text2)
			{
				if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
				{
					stringBuilder.Append(c);
					flag = false;
				}
				else if (!flag)
				{
					stringBuilder.Append('-');
					flag = true;
				}
			}
			string text3 = stringBuilder.ToString().Trim('-');
			if (text3.Length == 0)
			{
				throw new InvalidDataException("The preset name does not contain a usable id.");
			}
			return (text3.Length <= 128) ? text3 : text3.Substring(0, 128).TrimEnd('-');
		}

		private bool MigrateStagedExports()
		{
			string path = Path.Combine(pluginDirectory, "LobbyModSettings", "PresetExports", "Override", targetGuid);
			if (!Directory.Exists(path))
			{
				return true;
			}
			Directory.CreateDirectory(personalPresetDirectory);
			bool result = true;
			string[] files = Directory.GetFiles(path, "preset_*.json", SearchOption.TopDirectoryOnly);
			foreach (string text in files)
			{
				string text2 = Path.Combine(personalPresetDirectory, Path.GetFileName(text));
				if (!File.Exists(text2))
				{
					try
					{
						PublishPresetJson(text2, File.ReadAllText(text), overwrite: false);
						LogRoutine("[" + modName + "] Imported staged personal preset [" + text + "] to [" + text2 + "].");
					}
					catch (Exception ex)
					{
						result = false;
						DebugLogHelper.LogError(log, "[" + modName + "] Could not import staged personal preset [" + text + "]: " + ex.Message);
					}
				}
			}
			return result;
		}

		public void SetHostSettingsEnabled(bool value)
		{
			WriteSettingsActivation(hostSettingsActivationProperty, value);
		}

		public void SetClientSettingsEnabled(bool value)
		{
			WriteSettingsActivation(clientSettingsActivationProperty, value);
		}

		public bool IsHostSettingsActivationProperty(string propertyName)
		{
			return IsSettingsActivationProperty(hostSettingsActivationProperty, propertyName);
		}

		public bool IsClientSettingsActivationProperty(string propertyName)
		{
			return IsSettingsActivationProperty(clientSettingsActivationProperty, propertyName);
		}

		public bool IsHostPropertyName(string propertyName)
		{
			PresetPropertyAccessor value;
			return !string.IsNullOrEmpty(propertyName) && persistedPropertiesByName.TryGetValue(propertyName, out value) && IsHostProperty(value);
		}

		public void CaptureDefaults()
		{
			defaults = ((dynamicProvider == null) ? CaptureCurrentSettings() : persistedProperties.ToDictionary((PresetPropertyAccessor item) => item.Name, (PresetPropertyAccessor item) => MessagePackSerializer.Serialize(item.PropertyType, item.DefaultValue, (MessagePackSerializerOptions)null, default(CancellationToken)), StringComparer.Ordinal));
		}

		public void SetDefaultValue<T>(string propertyName, T value)
		{
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			if (string.IsNullOrWhiteSpace(propertyName) || !persistedPropertiesByName.TryGetValue(propertyName, out var value2))
			{
				throw new InvalidDataException("Unknown persistent setting [" + propertyName + "].");
			}
			if (value2.PropertyType != typeof(T))
			{
				throw new InvalidDataException("Default setting [" + propertyName + "] expects [" + value2.PropertyType.FullName + "], not [" + typeof(T).FullName + "].");
			}
			if (value == null && value2.PropertyType.IsValueType && Nullable.GetUnderlyingType(value2.PropertyType) == null)
			{
				throw new InvalidDataException("Default setting [" + propertyName + "] cannot be null.");
			}
			byte[] array = MessagePackSerializer.Serialize(value2.PropertyType, (object)value, (MessagePackSerializerOptions)null, default(CancellationToken));
			MessagePackSerializer.Deserialize(value2.PropertyType, ReadOnlyMemory<byte>.op_Implicit(array), (MessagePackSerializerOptions)null, default(CancellationToken));
			defaults[value2.Name] = array;
		}

		public void Activate()
		{
			//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_024b: Unknown result type (might be due to invalid IL or missing references)
			if (active)
			{
				return;
			}
			if (persistedProperties.Length == 0)
			{
				active = true;
				LogRoutine("[" + modName + "] Preset storage skipped because the ViewModel has no persistent settings.");
				return;
			}
			Dictionary<string, byte[]> payload = null;
			bool flag = File.Exists(filePath);
			if (flag && !TryReadPayload(out payload))
			{
				BackupCorruptFile();
				payload = null;
			}
			bool flag2 = false;
			PublishedModSettingsPreset publishedModSettingsPreset = null;
			Dictionary<string, byte[]> dictionary = null;
			Dictionary<string, byte[]> dictionary2 = null;
			int legacySelected = 0;
			int num = 0;
			string legacyStableId = string.Empty;
			if (payload != null && payload.ContainsKey("__SerpPresetSchemaVersion"))
			{
				try
				{
					int num2 = MessagePackSerializer.Deserialize<int>(ReadOnlyMemory<byte>.op_Implicit(payload["__SerpPresetSchemaVersion"]), (MessagePackSerializerOptions)null, default(CancellationToken));
					num = num2;
					if (num2 < 1 || num2 > 3)
					{
						throw new InvalidDataException($"Unsupported preset schema version [{num2}].");
					}
					if (num2 == 3)
					{
						if (payload.TryGetValue("__SerpLegacyPresetImportCompleted", out var value))
						{
							legacyPresetImportCompleted = MessagePackSerializer.Deserialize<bool>(ReadOnlyMemory<byte>.op_Implicit(value), (MessagePackSerializerOptions)null, default(CancellationToken));
						}
						preset1 = ReadSnapshot(payload, "__SerpCurrentSettings") ?? CaptureCurrentSettings();
						if (payload.TryGetValue("__SerpBasedOnPreset", out var value2))
						{
							basedOnStableId = MessagePackSerializer.Deserialize<string>(ReadOnlyMemory<byte>.op_Implicit(value2), (MessagePackSerializerOptions)null, default(CancellationToken)) ?? string.Empty;
						}
						if (payload.TryGetValue("__SerpPresetDirty", out var value3))
						{
							presetDirty = MessagePackSerializer.Deserialize<bool>(ReadOnlyMemory<byte>.op_Implicit(value3), (MessagePackSerializerOptions)null, default(CancellationToken));
						}
					}
					else
					{
						legacySelected = (payload.TryGetValue("__SerpActivePreset", out var value4) ? NormalizePreset(MessagePackSerializer.Deserialize<int>(ReadOnlyMemory<byte>.op_Implicit(value4), (MessagePackSerializerOptions)null, default(CancellationToken))) : 0);
						dictionary = ReadSnapshot(payload, "__SerpPreset1") ?? CaptureCurrentSettings();
						dictionary2 = ReadSnapshot(payload, "__SerpPreset2");
						if (num2 >= 2 && payload.TryGetValue("__SerpPublishedPreset", out var value5))
						{
							legacyStableId = MessagePackSerializer.Deserialize<string>(ReadOnlyMemory<byte>.op_Implicit(value5), (MessagePackSerializerOptions)null, default(CancellationToken)) ?? string.Empty;
						}
					}
				}
				catch (Exception arg)
				{
					DebugLogHelper.LogError(log, $"[{modName}] Preset metadata is invalid: {arg}");
					BackupCorruptFile();
					payload = null;
				}
			}
			if (payload != null && num > 0 && num < 3)
			{
				preset1 = Clone((legacySelected == 1 && dictionary2 != null) ? dictionary2 : dictionary);
				publishedModSettingsPreset = FindLegacyPublishedPreset(legacyStableId);
				try
				{
					SaveLegacySnapshotAsPersonalPreset("legacy-preset-1", "Preset 1 (migrated)", dictionary);
					if (dictionary2 != null)
					{
						SaveLegacySnapshotAsPersonalPreset("legacy-preset-2", "Preset 2 (migrated)", dictionary2);
					}
					RefreshCatalog();
					if (publishedModSettingsPreset == null)
					{
						PublishedModSettingsPreset preset = publishedPresets.FirstOrDefault((PublishedModSettingsPreset item) => item.SourceKind == ModSettingsPresetSourceKind.Personal && string.Equals(item.Id, (legacySelected == 1) ? "legacy-preset-2" : "legacy-preset-1", StringComparison.OrdinalIgnoreCase));
						SetBasedOn(preset, modified: false);
					}
					flag2 = true;
					LogRoutine("[" + modName + "] Migrated legacy Preset 1/2 storage to personal JSON presets.");
				}
				catch (Exception arg2)
				{
					legacyMigrationPending = true;
					DebugLogHelper.LogError(log, $"[{modName}] Could not publish legacy presets; the valid MessagePack data was retained for retry: {arg2}");
				}
			}
			if (payload == null || !payload.ContainsKey("__SerpPresetSchemaVersion"))
			{
				preset1 = CaptureCurrentSettings();
				if (flag)
				{
					try
					{
						SaveLegacySnapshotAsPersonalPreset("legacy-preset-1", "Preset 1 (migrated)", preset1);
						RefreshCatalog();
						SetBasedOn(publishedPresets.FirstOrDefault((PublishedModSettingsPreset item) => item.SourceKind == ModSettingsPresetSourceKind.Personal && string.Equals(item.Id, "legacy-preset-1", StringComparison.OrdinalIgnoreCase)), modified: false);
						flag2 = true;
					}
					catch (Exception arg3)
					{
						legacyMigrationPending = true;
						DebugLogHelper.LogError(log, $"[{modName}] Could not publish the legacy lobby-settings preset; the original file was retained for retry: {arg3}");
					}
				}
				LogRoutine((flag && !legacyMigrationPending) ? ("[" + modName + "] Migrated legacy lobby settings to a personal preset.") : (flag ? ("[" + modName + "] Deferred legacy lobby-settings migration after a publication failure.") : ("[" + modName + "] Initialized editable settings from code defaults.")));
			}
			if (!legacyMigrationPending && !legacyPresetImportCompleted)
			{
				legacyPresetImportCompleted = MigrateStagedExports();
				RefreshCatalog();
				flag2 = true;
			}
			string text = basedOnStableId;
			RestoreBasedOnMetadata();
			if (text.Length != 0 && basedOnStableId.Length == 0)
			{
				flag2 = true;
			}
			LogRoutine($"[{modName}] Loaded editable lobby-settings working state; basedOn={SanitizeLogValue(basedOnStableId)}, modified={presetDirty}.");
			active = true;
			if (dynamicProvider != null)
			{
				preset1 = CaptureCurrentSettings();
				SetBasedOn(null, modified: false);
			}
			ApplySnapshot(preset1, 0, writeLocalStorage: false);
			if (publishedModSettingsPreset != null)
			{
				ApplyPublishedPreset(publishedModSettingsPreset, writeLocalStorage: false);
			}
			if (flag2 && !legacyMigrationPending)
			{
				WriteCombinedPayload();
			}
		}

		public void LoadPreset(PublishedModSettingsPreset preset)
		{
			if (preset == null || !publishedPresets.Contains(preset))
			{
				throw new InvalidDataException("The selected preset is unavailable.");
			}
			if (owner.IsMissionPresetSelected && !owner.missionPresetEditable)
			{
				throw new InvalidOperationException("The active mission preset is read-only.");
			}
			ApplyPublishedPreset(preset, !owner.IsMissionPresetSelected);
		}

		public void ApplyDefaultsAsWorkingCopy()
		{
			if (owner.IsMissionPresetSelected)
			{
				throw new InvalidOperationException("Mission defaults must be materialized by the registered mission source provider.");
			}
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			PresetPropertyAccessor[] array = persistedProperties;
			foreach (PresetPropertyAccessor presetPropertyAccessor in array)
			{
				if ((!IsHostProperty(presetPropertyAccessor) || owner.isLocalHost) && defaults.TryGetValue(presetPropertyAccessor.Name, out var value))
				{
					dictionary[presetPropertyAccessor.Name] = ((value == null) ? null : ((byte[])value.Clone()));
				}
			}
			ApplySnapshot(dictionary, 0, writeLocalStorage: false);
			foreach (KeyValuePair<string, byte[]> item in dictionary)
			{
				preset1[item.Key] = item.Value;
			}
			SetBasedOn(null, modified: false);
			WriteCombinedPayload();
			LogRoutine("[" + modName + "] Loaded Mod defaults as editable working settings.");
		}

		public void ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException("snapshot");
			}
			if (owner.IsMissionPresetSelected)
			{
				ApplyMissionWorkingSnapshot(snapshot, missionPresetLabel);
				return;
			}
			Dictionary<string, byte[]> stored = Clone(snapshot);
			ApplySnapshot(stored, 0, writeLocalStorage: false);
			preset1 = stored;
			WriteCombinedPayload();
		}

		private void RestoreBasedOnMetadata()
		{
			activePublishedPreset = publishedPresets.FirstOrDefault((PublishedModSettingsPreset item) => string.Equals(item.StableId, basedOnStableId, StringComparison.OrdinalIgnoreCase));
			if (activePublishedPreset == null)
			{
				if (basedOnStableId.Length != 0)
				{
					LogRoutine("[" + modName + "] Preset source [" + SanitizeLogValue(basedOnStableId) + "] is unavailable; retained the materialized working settings.");
				}
				basedOnStableId = string.Empty;
				basedOnName = string.Empty;
				basedOnSource = string.Empty;
			}
			else
			{
				basedOnName = activePublishedPreset.Name;
				basedOnSource = DescribeSource(activePublishedPreset);
			}
		}

		private PublishedModSettingsPreset FindLegacyPublishedPreset(string legacyStableId)
		{
			if (string.IsNullOrWhiteSpace(legacyStableId))
			{
				return null;
			}
			return publishedPresets.FirstOrDefault((PublishedModSettingsPreset item) => string.Equals(item.ProviderGuid + "\n" + item.TargetGuid + "\n" + item.Id, legacyStableId, StringComparison.OrdinalIgnoreCase));
		}

		private void SetBasedOn(PublishedModSettingsPreset preset, bool modified)
		{
			activePublishedPreset = preset;
			basedOnStableId = preset?.StableId ?? string.Empty;
			basedOnName = preset?.Name ?? string.Empty;
			basedOnSource = ((preset == null) ? string.Empty : DescribeSource(preset));
			presetDirty = preset != null && modified;
		}

		private static string DescribeSource(PublishedModSettingsPreset preset)
		{
			return DescribeSource(preset, "Personal presets", "Bundled with this mod", "External presets");
		}

		private static string DescribeSource(PublishedModSettingsPreset preset, string personalSourceText, string bundledSourceText, string externalSourceText)
		{
			return preset.SourceKind switch
			{
				ModSettingsPresetSourceKind.Personal => personalSourceText, 
				ModSettingsPresetSourceKind.Bundled => bundledSourceText, 
				ModSettingsPresetSourceKind.External => externalSourceText + ": " + preset.ProviderName, 
				_ => preset.ProviderName, 
			};
		}

		private void SaveLegacySnapshotAsPersonalPreset(string id, string name, Dictionary<string, byte[]> snapshot)
		{
			//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			Directory.CreateDirectory(personalPresetDirectory);
			string text = Path.Combine(personalPresetDirectory, "preset_" + id + ".json");
			Dictionary<string, PublishedPresetSetting> dictionary = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
			PresetPropertyAccessor[] array = persistedProperties;
			foreach (PresetPropertyAccessor presetPropertyAccessor in array)
			{
				if (snapshot != null && snapshot.TryGetValue(presetPropertyAccessor.Name, out var value) && value != null)
				{
					object value2 = MessagePackSerializer.Deserialize(presetPropertyAccessor.PropertyType, ReadOnlyMemory<byte>.op_Implicit(value), (MessagePackSerializerOptions)null, default(CancellationToken));
					dictionary[presetPropertyAccessor.Name] = new PublishedPresetSetting
					{
						Mode = PublishedPresetValueMode.Fixed,
						Value = ModSettingsPresetJson.ToJsonValue(presetPropertyAccessor.PropertyType, value2)
					};
				}
			}
			string text2 = ModSettingsPresetJson.Serialize(targetGuid, id, name, "Migrated from the previous local Preset 1/2 storage.", string.Empty, string.Empty, dictionary);
			if (File.Exists(text))
			{
				string a = File.ReadAllText(text);
				if (!string.Equals(a, text2, StringComparison.Ordinal))
				{
					throw new InvalidDataException("The migration target [" + text + "] already exists with different contents.");
				}
			}
			else
			{
				PublishPresetJson(text, text2, overwrite: false);
			}
		}

		public IReadOnlyList<PresetSettingDescriptor> GetSettingDescriptors()
		{
			return (from property in persistedProperties
				select new PresetSettingDescriptor
				{
					PropertyName = property.Name,
					PropertyType = property.PropertyType,
					Group = property.Group,
					DisplayName = property.DisplayName,
					RequiresRestart = property.RequiresRestart,
					Scope = ((!IsHostProperty(property)) ? ((property.GetCustomAttribute<SyncPerPlayerAttribute>() != null) ? PresetSettingScope.Player : PresetSettingScope.Local) : PresetSettingScope.Host)
				} into item
				orderby item.Scope
				select item).ThenBy((PresetSettingDescriptor item) => item.PropertyName, StringComparer.Ordinal).ToArray();
		}

		public string SavePersonalPreset(string id, string name, string description, IEnumerable<PresetSaveSelection> selections, bool overwrite)
		{
			//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
			PresetSaveSelection[] array = (selections ?? Enumerable.Empty<PresetSaveSelection>()).ToArray();
			if (array.Length == 0)
			{
				throw new InvalidDataException("At least one setting must be selected for saving.");
			}
			IGrouping<string, PresetSaveSelection> grouping = array.GroupBy((PresetSaveSelection item) => item?.PropertyName ?? string.Empty, StringComparer.Ordinal).FirstOrDefault((IGrouping<string, PresetSaveSelection> group) => group.Count() > 1);
			if (grouping != null)
			{
				throw new InvalidDataException("Setting [" + grouping.Key + "] was selected more than once.");
			}
			Dictionary<string, PublishedPresetSetting> dictionary = new Dictionary<string, PublishedPresetSetting>(StringComparer.Ordinal);
			PresetSaveSelection[] array2 = array;
			foreach (PresetSaveSelection presetSaveSelection in array2)
			{
				if (presetSaveSelection == null || !persistedPropertiesByName.TryGetValue(presetSaveSelection.PropertyName, out var value))
				{
					throw new InvalidDataException("Unknown persistent setting [" + presetSaveSelection?.PropertyName + "].");
				}
				object value2 = null;
				if (presetSaveSelection.Mode == PublishedPresetValueMode.Fixed)
				{
					object value3 = value.GetValue(owner);
					if (IsHostProperty(value) && !owner.isLocalHost)
					{
						Dictionary<string, byte[]> dictionary2 = preset1 ?? defaults;
						if (!dictionary2.TryGetValue(value.Name, out var value4) || value4 == null)
						{
							throw new InvalidDataException("Locally owned value for [" + value.Name + "] is unavailable.");
						}
						value3 = MessagePackSerializer.Deserialize(value.PropertyType, ReadOnlyMemory<byte>.op_Implicit(value4), (MessagePackSerializerOptions)null, default(CancellationToken));
					}
					value2 = ModSettingsPresetJson.ToJsonValue(value.PropertyType, value3);
				}
				dictionary.Add(value.Name, new PublishedPresetSetting
				{
					Mode = presetSaveSelection.Mode,
					Value = value2
				});
			}
			string json = ModSettingsPresetJson.Serialize(targetGuid, id, name, description, string.Empty, string.Empty, dictionary);
			string text = string.Concat(id.Split(Path.GetInvalidFileNameChars()));
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidDataException("Preset id has no safe filename characters.");
			}
			string text2 = Path.Combine(personalPresetDirectory, "preset_" + text + ".json");
			PublishPresetJson(text2, json, overwrite);
			RefreshCatalog();
			LogRoutine("[" + modName + "] " + (overwrite ? "Overwrote" : "Saved") + " personal preset [" + id + "] at [" + text2 + "].");
			return text2;
		}

		public string ImportPresetJson(string json)
		{
			PublishedModSettingsPreset publishedModSettingsPreset = ModSettingsPresetJson.Parse(json, targetGuid, modName, targetGuid, "");
			ValidatePublishedPreset(publishedModSettingsPreset);
			string text = string.Concat(publishedModSettingsPreset.Id.Split(Path.GetInvalidFileNameChars()));
			if (string.IsNullOrWhiteSpace(text))
			{
				throw new InvalidDataException("Invalid preset id.");
			}
			string text2 = Path.Combine(personalPresetDirectory, "preset_" + text + ".json");
			PublishPresetJson(text2, ModSettingsPresetJson.Serialize(targetGuid, publishedModSettingsPreset.Id, publishedModSettingsPreset.Name, publishedModSettingsPreset.Description, publishedModSettingsPreset.MinimumTargetVersion, publishedModSettingsPreset.MaximumTargetVersion, publishedModSettingsPreset.Settings), overwrite: false);
			RefreshCatalog();
			return text2;
		}

		public void LogPresetOperationCancelled(string operation, string id)
		{
			LogRoutine("[" + modName + "] Personal preset operation [" + operation + "] for [" + (id ?? string.Empty) + "] was cancelled.");
		}

		public void LogPresetOperationFailure(string operation, string id, Exception exception)
		{
			DebugLogHelper.LogError(log, $"[{modName}] Personal preset operation [{operation}] for [{id ?? string.Empty}] failed: {exception}");
		}

		public void DeletePersonalPreset(PublishedModSettingsPreset preset)
		{
			if (preset == null || preset.SourceKind != ModSettingsPresetSourceKind.Personal || !publishedPresets.Contains(preset))
			{
				throw new InvalidDataException("Only an available personal preset can be deleted.");
			}
			string fullPath = Path.GetFullPath(preset.SourcePath ?? string.Empty);
			ModSettingsPresetCatalog.ValidatePersonalWritePath(pluginDirectory, personalPresetDirectory, fullPath);
			if (!File.Exists(fullPath))
			{
				throw new FileNotFoundException("The selected personal preset no longer exists.", fullPath);
			}
			string stableId = preset.StableId;
			File.Delete(fullPath);
			bool flag = false;
			if (string.Equals(basedOnStableId, stableId, StringComparison.OrdinalIgnoreCase))
			{
				activePublishedPreset = null;
				basedOnStableId = string.Empty;
				basedOnName = string.Empty;
				basedOnSource = string.Empty;
				presetDirty = false;
				flag = true;
			}
			if (string.Equals(suspendedBasedOnStableId, stableId, StringComparison.OrdinalIgnoreCase))
			{
				suspendedPublishedPreset = null;
				suspendedBasedOnStableId = string.Empty;
				suspendedBasedOnName = string.Empty;
				suspendedBasedOnSource = string.Empty;
				suspendedPresetDirty = false;
				flag = true;
			}
			RefreshCatalog();
			if (flag && active)
			{
				WriteCombinedPayload();
			}
			LogRoutine("[" + modName + "] Deleted personal preset [" + SanitizeLogValue(stableId) + "].");
		}

		private void PublishPresetJson(string path, string json, bool overwrite)
		{
			string directoryName = Path.GetDirectoryName(path);
			ModSettingsPresetCatalog.ValidatePersonalWritePath(pluginDirectory, personalPresetDirectory, path);
			Directory.CreateDirectory(directoryName);
			ModSettingsPresetCatalog.ValidatePersonalWritePath(pluginDirectory, personalPresetDirectory, path);
			string text = path + ".tmp-" + Guid.NewGuid().ToString("N");
			try
			{
				File.WriteAllText(text, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				if (!overwrite)
				{
					try
					{
						File.Move(text, path);
						return;
					}
					catch (IOException)
					{
						if (File.Exists(path))
						{
							throw new PresetSaveFileExistsException(path);
						}
						throw;
					}
				}
				PresetAtomicPublishResult presetAtomicPublishResult = PresetAtomicFilePublisher.Publish(text, path);
				if (!presetAtomicPublishResult.Succeeded)
				{
					throw presetAtomicPublishResult.Error;
				}
			}
			finally
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
		}

		public Dictionary<string, byte[]> CreateDisabledSnapshot()
		{
			Dictionary<string, byte[]> dictionary = CopyProperties(defaults, hostProperties);
			if (persistedPropertiesByName.TryGetValue("EnableMod", out var value) && value.PropertyType == typeof(bool))
			{
				dictionary[value.Name] = MessagePackSerializer.Serialize<bool>(false, (MessagePackSerializerOptions)null, default(CancellationToken));
			}
			return dictionary;
		}

		public Dictionary<string, byte[]> CreateDefaultSnapshot()
		{
			return Clone(defaults);
		}

		public Dictionary<string, byte[]> CreateCurrentMissionSnapshot()
		{
			return Clone(owner.IsMissionPresetSelected ? missionPreset : (preset1 ?? defaults));
		}

		public Dictionary<string, byte[]> CreatePlayerMissionSnapshot()
		{
			return Clone(owner.IsMissionPresetSelected ? (preset1 ?? defaults) : CaptureCurrentSettings());
		}

		public void ApplyMissionWorkingSnapshot(Dictionary<string, byte[]> snapshot, string label)
		{
			if (!owner.IsMissionPresetSelected || !owner.missionPresetEditable)
			{
				throw new InvalidOperationException("Mission settings are not currently editable.");
			}
			if (snapshot == null)
			{
				throw new ArgumentNullException("snapshot");
			}
			Dictionary<string, byte[]> dictionary = Clone(missionPreset ?? defaults);
			foreach (KeyValuePair<string, byte[]> item in snapshot)
			{
				if (persistedPropertiesByName.TryGetValue(item.Key, out var value) && (!IsHostProperty(value) || owner.isLocalHost))
				{
					dictionary[item.Key] = ((item.Value == null) ? null : ((byte[])item.Value.Clone()));
				}
			}
			missionPreset = dictionary;
			missionPresetLabel = label ?? string.Empty;
			missionBasedOnPreset = null;
			missionPresetDirty = false;
			ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
			LogRoutine("[" + modName + "] Loaded mission source [" + SanitizeLogValue(missionPresetLabel) + "] into the editable working copy.");
		}

		public void EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
		{
			suspendedPublishedPreset = activePublishedPreset;
			suspendedBasedOnStableId = basedOnStableId;
			suspendedBasedOnName = basedOnName;
			suspendedBasedOnSource = basedOnSource;
			suspendedPresetDirty = presetDirty;
			activePublishedPreset = null;
			missionPresetLabel = label ?? string.Empty;
			missionBasedOnPreset = null;
			missionPresetDirty = false;
			missionPreset = Clone(preset1 ?? defaults);
			Dictionary<string, byte[]> dictionary = snapshot ?? CreateDisabledSnapshot();
			foreach (KeyValuePair<string, byte[]> item in dictionary)
			{
				missionPreset[item.Key] = ((item.Value == null) ? null : ((byte[])item.Value.Clone()));
			}
			ApplySnapshot(missionPreset, MissionPresetIndex, writeLocalStorage: false);
			LogRoutine("[" + modName + "] Entered " + (editable ? "editable" : "read-only") + " mission preset.");
		}

		public void ExitMissionPreset()
		{
			missionPreset = null;
			missionBasedOnPreset = null;
			missionPresetDirty = false;
			basedOnStableId = suspendedBasedOnStableId;
			basedOnName = suspendedBasedOnName;
			basedOnSource = suspendedBasedOnSource;
			presetDirty = suspendedPresetDirty;
			activePublishedPreset = ((suspendedPublishedPreset != null && publishedPresets.Contains(suspendedPublishedPreset)) ? suspendedPublishedPreset : null);
			ApplySnapshot(preset1, 0, writeLocalStorage: true);
			suspendedPublishedPreset = null;
			missionPresetLabel = string.Empty;
			LogRoutine("[" + modName + "] Left mission preset and restored the previous normal preset.");
		}

		public void AfterPropertyChanged(string propertyName)
		{
			if (!active || applying || string.IsNullOrEmpty(propertyName))
			{
				return;
			}
			persistedPropertiesByName.TryGetValue(propertyName, out var value);
			if (IsNetworkSyncInProgress())
			{
				if (value != null && owner.IsMissionPresetSelected && IsHostProperty(value))
				{
					StoreProperty(missionPreset, value);
				}
			}
			else
			{
				if (value == null)
				{
					return;
				}
				if (owner.IsMissionPresetSelected)
				{
					if (owner.missionPresetEditable && (owner.isLocalHost || IsClientProperty(value)))
					{
						StoreProperty(missionPreset, value);
						if (missionBasedOnPreset != null)
						{
							missionPresetDirty = true;
						}
					}
				}
				else if ((!IsHostProperty(value) || owner.isLocalHost) && (owner.isLocalHost || IsClientProperty(value)))
				{
					StoreProperty(preset1, value);
					if (basedOnStableId.Length != 0)
					{
						presetDirty = true;
					}
					WriteCombinedPayload();
				}
			}
		}

		private void ApplyPublishedPreset(PublishedModSettingsPreset preset, bool writeLocalStorage)
		{
			if (preset == null)
			{
				throw new ArgumentNullException("preset");
			}
			Dictionary<string, byte[]> dictionary = (owner.IsMissionPresetSelected ? Clone(preset1 ?? defaults) : CaptureCurrentSettings());
			Dictionary<PresetPropertyAccessor, byte[]> dictionary2 = new Dictionary<PresetPropertyAccessor, byte[]>();
			foreach (KeyValuePair<string, PublishedPresetSetting> setting in preset.Settings)
			{
				PresetPropertyAccessor presetPropertyAccessor = persistedPropertiesByName[setting.Key];
				if (IsHostProperty(presetPropertyAccessor) && !owner.isLocalHost)
				{
					continue;
				}
				byte[] value;
				switch (setting.Value.Mode)
				{
				case PublishedPresetValueMode.ModDefault:
					if (!defaults.TryGetValue(presetPropertyAccessor.Name, out value))
					{
						throw new InvalidDataException("Code default for [" + presetPropertyAccessor.Name + "] is unavailable.");
					}
					break;
				case PublishedPresetValueMode.Player:
					if (!dictionary.TryGetValue(presetPropertyAccessor.Name, out value) && !defaults.TryGetValue(presetPropertyAccessor.Name, out value))
					{
						throw new InvalidDataException("Player value for [" + presetPropertyAccessor.Name + "] is unavailable.");
					}
					break;
				case PublishedPresetValueMode.Fixed:
				{
					object obj = ModSettingsPresetJson.ConvertValue(setting.Value.Value, presetPropertyAccessor.PropertyType);
					value = MessagePackSerializer.Serialize(presetPropertyAccessor.PropertyType, obj, (MessagePackSerializerOptions)null, default(CancellationToken));
					break;
				}
				default:
					throw new InvalidDataException("Unsupported published preset mode for [" + presetPropertyAccessor.Name + "].");
				}
				dictionary2[presetPropertyAccessor] = (byte[])value.Clone();
			}
			applying = true;
			try
			{
				if (dynamicProvider != null || owner.SettingsApplicationBackend != null)
				{
					ApplyDynamicValues(dictionary2);
				}
				else
				{
					foreach (KeyValuePair<PresetPropertyAccessor, byte[]> item in dictionary2)
					{
						if (!TryApplyProperty(item.Key, item.Value))
						{
							throw new InvalidDataException("Published value for [" + item.Key.Name + "] could not be applied.");
						}
					}
				}
				if (owner.IsMissionPresetSelected)
				{
					foreach (PresetPropertyAccessor key in dictionary2.Keys)
					{
						StoreProperty(missionPreset, key);
					}
					missionBasedOnPreset = preset;
					missionPresetDirty = false;
					owner.SetSelectedPresetCore(MissionPresetIndex);
				}
				else
				{
					foreach (PresetPropertyAccessor key2 in dictionary2.Keys)
					{
						StoreProperty(preset1, key2);
					}
					SetBasedOn(preset, modified: false);
					owner.SetSelectedPresetCore(0);
				}
			}
			finally
			{
				applying = false;
			}
			owner.OnSettingsSnapshotApplied();
			if (writeLocalStorage)
			{
				WriteCombinedPayload();
			}
			LogRoutine("[" + modName + "] Loaded editable preset [" + preset.Name + "] from [" + preset.ProviderName + "].");
		}

		private static string SanitizeLogValue(string value)
		{
			return (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
		}

		private void ValidatePublishedPreset(PublishedModSettingsPreset preset)
		{
			foreach (KeyValuePair<string, PublishedPresetSetting> setting in preset.Settings)
			{
				if (!persistedPropertiesByName.TryGetValue(setting.Key, out var value))
				{
					throw new InvalidDataException("Unknown persistent property [" + setting.Key + "].");
				}
				if (setting.Value == null)
				{
					throw new InvalidDataException("Setting [" + setting.Key + "] is null.");
				}
				if (setting.Value.Mode == PublishedPresetValueMode.Fixed)
				{
					object obj = ModSettingsPresetJson.ConvertValue(setting.Value.Value, value.PropertyType);
					MessagePackSerializer.Serialize(value.PropertyType, obj, (MessagePackSerializerOptions)null, default(CancellationToken));
				}
			}
		}

		private void ApplySnapshot(Dictionary<string, byte[]> stored, int selected, bool writeLocalStorage)
		{
			applying = true;
			try
			{
				if (dynamicProvider != null || owner.SettingsApplicationBackend != null)
				{
					Dictionary<PresetPropertyAccessor, byte[]> dictionary = new Dictionary<PresetPropertyAccessor, byte[]>();
					PresetPropertyAccessor[] array = persistedProperties;
					foreach (PresetPropertyAccessor presetPropertyAccessor in array)
					{
						if (selected == MissionPresetIndex || owner.isLocalHost || IsClientProperty(presetPropertyAccessor))
						{
							byte[] value = null;
							stored?.TryGetValue(presetPropertyAccessor.Name, out value);
							if (value == null)
							{
								defaults.TryGetValue(presetPropertyAccessor.Name, out value);
							}
							if (value == null)
							{
								throw new InvalidDataException("Missing dynamic setting: " + presetPropertyAccessor.Name);
							}
							dictionary.Add(presetPropertyAccessor, value);
						}
					}
					ApplyDynamicValues(dictionary);
				}
				else
				{
					PresetPropertyAccessor[] array2 = persistedProperties;
					foreach (PresetPropertyAccessor presetPropertyAccessor2 in array2)
					{
						if (selected == MissionPresetIndex || owner.isLocalHost || IsClientProperty(presetPropertyAccessor2))
						{
							byte[] value2 = null;
							stored?.TryGetValue(presetPropertyAccessor2.Name, out value2);
							if (value2 == null)
							{
								defaults.TryGetValue(presetPropertyAccessor2.Name, out value2);
							}
							if (value2 != null && presetPropertyAccessor2.CanWrite && !TryApplyProperty(presetPropertyAccessor2, value2) && defaults.TryGetValue(presetPropertyAccessor2.Name, out var value3) && value2 != value3)
							{
								TryApplyProperty(presetPropertyAccessor2, value3);
							}
						}
					}
				}
				owner.SetSelectedPresetCore(selected);
			}
			finally
			{
				applying = false;
			}
			owner.OnSettingsSnapshotApplied();
			if (writeLocalStorage)
			{
				WriteCombinedPayload();
			}
		}

		private void ApplyDynamicValues(Dictionary<PresetPropertyAccessor, byte[]> prepared)
		{
			//IL_005a: Unknown result type (might be due to invalid IL or missing references)
			Dictionary<string, object> dictionary = owner.SettingsApplicationBackend?.ReadDesiredValues() ?? dynamicProvider.ReadValues();
			foreach (KeyValuePair<PresetPropertyAccessor, byte[]> item in prepared)
			{
				dictionary[item.Key.Name] = MessagePackSerializer.Deserialize(item.Key.PropertyType, ReadOnlyMemory<byte>.op_Implicit(item.Value), (MessagePackSerializerOptions)null, default(CancellationToken));
			}
			if (owner.SettingsApplicationBackend != null)
			{
				owner.SettingsApplicationBackend.ReplaceDesiredValues(dictionary);
				return;
			}
			dynamicProvider.ValidateValues(dictionary);
			dynamicProvider.ReplaceValues(dictionary);
		}

		private bool TryApplyProperty(PresetPropertyAccessor property, byte[] bytes)
		{
			//IL_0009: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				object obj = MessagePackSerializer.Deserialize(property.PropertyType, ReadOnlyMemory<byte>.op_Implicit(bytes), (MessagePackSerializerOptions)null, default(CancellationToken));
				if (obj == null)
				{
					return false;
				}
				property.SetValue(owner, obj);
				return true;
			}
			catch (Exception ex)
			{
				DebugLogHelper.LogWarning(log, "[" + modName + "] Could not restore [" + property.Name + "] from the current settings snapshot: " + ex.Message);
				return false;
			}
		}

		private Dictionary<string, byte[]> CaptureCurrentSettings()
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			PresetPropertyAccessor[] array = persistedProperties;
			foreach (PresetPropertyAccessor property in array)
			{
				StoreProperty(dictionary, property);
			}
			return dictionary;
		}

		private void StoreProperty(Dictionary<string, byte[]> snapshot, PresetPropertyAccessor property)
		{
			if (!property.CanRead)
			{
				return;
			}
			try
			{
				object obj = ((dynamicProvider != null) ? dynamicProvider.ReadValue(property.Name) : ((owner.SettingsApplicationBackend != null) ? owner.SettingsApplicationBackend.ReadDesiredValues()[property.Name] : property.GetValue(owner)));
				if (obj == null)
				{
					snapshot.Remove(property.Name);
				}
				else
				{
					snapshot[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, obj, (MessagePackSerializerOptions)null, default(CancellationToken));
				}
			}
			catch (Exception ex)
			{
				DebugLogHelper.LogWarning(log, "[" + modName + "] Could not capture [" + property.Name + "] for the current settings snapshot: " + ex.Message);
			}
		}

		private void WriteCombinedPayload()
		{
			if (persistedProperties.Length == 0 || legacyMigrationPending)
			{
				return;
			}
			Dictionary<string, byte[]> dictionary = ComposeSafeTopLevelSnapshot();
			dictionary["__SerpPresetSchemaVersion"] = MessagePackSerializer.Serialize<int>(3, (MessagePackSerializerOptions)null, default(CancellationToken));
			dictionary["__SerpCurrentSettings"] = MessagePackSerializer.Serialize<Dictionary<string, byte[]>>(preset1 ?? Clone(defaults), (MessagePackSerializerOptions)null, default(CancellationToken));
			if (!string.IsNullOrEmpty(basedOnStableId))
			{
				dictionary["__SerpBasedOnPreset"] = MessagePackSerializer.Serialize<string>(basedOnStableId, (MessagePackSerializerOptions)null, default(CancellationToken));
			}
			dictionary["__SerpPresetDirty"] = MessagePackSerializer.Serialize<bool>(presetDirty, (MessagePackSerializerOptions)null, default(CancellationToken));
			dictionary["__SerpLegacyPresetImportCompleted"] = MessagePackSerializer.Serialize<bool>(legacyPresetImportCompleted, (MessagePackSerializerOptions)null, default(CancellationToken));
			string directoryName = Path.GetDirectoryName(filePath);
			string text = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
			int num = 0;
			try
			{
				Directory.CreateDirectory(directoryName);
				File.WriteAllBytes(text, MessagePackSerializer.Serialize<Dictionary<string, byte[]>>(dictionary, (MessagePackSerializerOptions)null, default(CancellationToken)));
				PresetAtomicPublishResult presetAtomicPublishResult = PresetAtomicFilePublisher.Publish(text, filePath);
				num = presetAtomicPublishResult.Attempts;
				if (!presetAtomicPublishResult.Succeeded)
				{
					DebugLogHelper.LogError(log, "[" + modName + "] Could not atomically publish lobby-settings presets to [" + filePath + "] " + $"after {presetAtomicPublishResult.Attempts} attempts; hresult=0x{presetAtomicPublishResult.Error.HResult:X8}: {presetAtomicPublishResult.Error}");
				}
			}
			catch (Exception ex)
			{
				DebugLogHelper.LogError(log, "[" + modName + "] Could not save lobby-settings presets to [" + filePath + "]; " + $"publishAttempts={num}, hresult=0x{ex.HResult:X8}: {ex}");
			}
			finally
			{
				try
				{
					if (File.Exists(text))
					{
						File.Delete(text);
					}
				}
				catch (Exception ex2)
				{
					DebugLogHelper.LogWarning(log, "[" + modName + "] Could not remove temporary preset file [" + text + "]: " + ex2.Message);
				}
			}
		}

		private Dictionary<string, byte[]> ComposeSafeTopLevelSnapshot()
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			Dictionary<string, byte[]> dictionary2 = preset1 ?? defaults;
			PresetPropertyAccessor[] array = persistedProperties;
			foreach (PresetPropertyAccessor presetPropertyAccessor in array)
			{
				byte[] value;
				if (!owner.IsMissionPresetSelected && (IsClientProperty(presetPropertyAccessor) || owner.isLocalHost))
				{
					StoreProperty(dictionary, presetPropertyAccessor);
				}
				else if (dictionary2.TryGetValue(presetPropertyAccessor.Name, out value))
				{
					dictionary[presetPropertyAccessor.Name] = ((value == null) ? null : ((byte[])value.Clone()));
				}
				else if (defaults.TryGetValue(presetPropertyAccessor.Name, out value))
				{
					dictionary[presetPropertyAccessor.Name] = ((value == null) ? null : ((byte[])value.Clone()));
				}
			}
			return dictionary;
		}

		private bool TryReadPayload(out Dictionary<string, byte[]> payload)
		{
			//IL_0028: Unknown result type (might be due to invalid IL or missing references)
			payload = null;
			if (!File.Exists(filePath))
			{
				return false;
			}
			try
			{
				payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(ReadOnlyMemory<byte>.op_Implicit(File.ReadAllBytes(filePath)), (MessagePackSerializerOptions)null, default(CancellationToken));
				return payload != null;
			}
			catch (Exception arg)
			{
				DebugLogHelper.LogError(log, $"[{modName}] Could not read lobby-settings presets from [{filePath}]: {arg}");
				return false;
			}
		}

		private void BackupCorruptFile()
		{
			if (!File.Exists(filePath))
			{
				return;
			}
			string text = filePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
			try
			{
				File.Copy(filePath, text, overwrite: false);
				DebugLogHelper.LogWarning(log, "[" + modName + "] Preserved invalid preset data at [" + text + "].");
			}
			catch (Exception arg)
			{
				DebugLogHelper.LogError(log, $"[{modName}] Could not preserve invalid preset data: {arg}");
			}
		}

		private Dictionary<string, byte[]> ReadSnapshot(Dictionary<string, byte[]> payload, string key)
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			if (!payload.TryGetValue(key, out var value))
			{
				return null;
			}
			return MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(ReadOnlyMemory<byte>.op_Implicit(value), (MessagePackSerializerOptions)null, default(CancellationToken));
		}

		private static int NormalizePreset(int selected)
		{
			return (selected == 1) ? 1 : 0;
		}

		private static bool IsPersistedProperty(PresetPropertyAccessor property)
		{
			return property.GetCustomAttribute<DoNotPersistAttribute>() == null && (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null || property.GetCustomAttribute<SyncHostOnlyAttribute>() != null || property.GetCustomAttribute<PresetLocalAttribute>() != null);
		}

		private static bool IsHostProperty(PresetPropertyAccessor property)
		{
			return property.GetCustomAttribute<SyncHostOnlyAttribute>() != null;
		}

		private static bool IsClientProperty(PresetPropertyAccessor property)
		{
			return property.GetCustomAttribute<SyncHostOnlyAttribute>() == null && (property.GetCustomAttribute<SyncPerPlayerAttribute>() != null || property.GetCustomAttribute<PresetLocalAttribute>() != null);
		}

		private static PresetPropertyAccessor FindSettingsActivationProperty(IEnumerable<PresetPropertyAccessor> properties, params string[] preferredNames)
		{
			foreach (string name in preferredNames)
			{
				PresetPropertyAccessor presetPropertyAccessor = properties.FirstOrDefault((PresetPropertyAccessor item) => item.Name == name && item.PropertyType == typeof(bool) && item.CanRead && item.CanWrite);
				if (presetPropertyAccessor != null)
				{
					return presetPropertyAccessor;
				}
			}
			return null;
		}

		private bool ReadSettingsActivation(PresetPropertyAccessor property)
		{
			return property != null && (bool)property.GetValue(owner);
		}

		private void WriteSettingsActivation(PresetPropertyAccessor property, bool value)
		{
			if (property != null && ReadSettingsActivation(property) != value)
			{
				property.SetValue(owner, value);
			}
		}

		private static bool IsSettingsActivationProperty(PresetPropertyAccessor property, string propertyName)
		{
			return property != null && string.Equals(property.Name, propertyName, StringComparison.Ordinal);
		}

		public static bool IsNetworkSyncInProgress()
		{
			//IL_000d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Invalid comparison between Unknown and I4
			return GameXAMLManagerAPI.Instance != null && (int)GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin == 1;
		}

		private static Dictionary<string, byte[]> CopyProperties(Dictionary<string, byte[]> source, IEnumerable<PresetPropertyAccessor> properties)
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			if (source == null)
			{
				return dictionary;
			}
			foreach (PresetPropertyAccessor property in properties)
			{
				if (source.TryGetValue(property.Name, out var value))
				{
					dictionary[property.Name] = ((value == null) ? null : ((byte[])value.Clone()));
				}
			}
			return dictionary;
		}

		private void LogRoutine(string message)
		{
			if (routineLoggingEnabled)
			{
				DebugLogHelper.LogInfo(log, message);
			}
		}

		private static Dictionary<string, byte[]> Clone(Dictionary<string, byte[]> source)
		{
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			if (source == null)
			{
				return dictionary;
			}
			foreach (KeyValuePair<string, byte[]> item in source)
			{
				dictionary[item.Key] = ((item.Value == null) ? null : ((byte[])item.Value.Clone()));
			}
			return dictionary;
		}
	}

	private static readonly MethodInfo NotifyRevertMethod = typeof(LobbyModSettingsBaseViewModel).GetMethod("NotifyRevert", BindingFlags.Instance | BindingFlags.NonPublic);

	private PresetController presetController;

	private int selectedPreset;

	private bool missionPresetContext;

	private bool missionPresetEditable;

	private bool missionPresetHasExplicitSettings;

	private bool showDirectLaunchNotice;

	private bool isCastlePlannerSettings;

	private SettingsMenuContext settingsMenuContext;

	private bool isRealMultiplayer;

	private bool isLocalHost = true;

	private PerPlayerLobbySettingsCoordinator perPlayerSettingsCoordinator;

	private string modSettingsSearchText = string.Empty;

	private string modSettingsSearchExactKey = string.Empty;

	private bool modSettingsSearchIncludeToolTips;

	private bool modSettingsSearchExpanded;

	private int modSettingsSearchFocusRequest;

	private bool presetSavePanelOpen;

	private bool presetLoadPanelOpen;

	private string presetSaveName = string.Empty;

	private string presetSaveDescription = string.Empty;

	private ModSettingsPresetListEntry selectedPresetLoadEntry;

	private ModSettingsPresetSaveTarget selectedPresetSaveTarget;

	private readonly ObservableCollection<ModSettingsPresetListEntry> presetLoadEntries = new ObservableCollection<ModSettingsPresetListEntry>();

	private readonly ObservableCollection<ModSettingsPresetSaveTarget> presetSaveTargets = new ObservableCollection<ModSettingsPresetSaveTarget>();

	private bool applyingPresetSaveBulkMode;

	private readonly ObservableCollection<PresetSaveSettingViewModel> presetSaveSettings = new ObservableCollection<PresetSaveSettingViewModel>();

	private readonly ObservableCollection<ModSettingsWorkingSource> settingsSources = new ObservableCollection<ModSettingsWorkingSource>();

	private ModSettingsWorkingSource selectedSettingsSource;

	private string preferredSettingsSourceToken = string.Empty;

	private PublishedModSettingsPreset pendingDeletePreset;

	private string pendingOverwriteId;

	private PresetSaveSelection[] pendingOverwriteSelections;

	private string presetInlineConfirmationTitle = string.Empty;

	private string presetInlineConfirmationMessage = string.Empty;

	private string presetOperationStatus = string.Empty;

	private bool presetOperationFailed;

	public bool HasHostSettings => presetController?.HasHostSettings ?? false;

	public bool HasClientSettings => presetController?.HasClientSettings ?? false;

	public bool HasHostSettingsActivation => presetController?.HasHostSettingsActivation ?? false;

	public bool HasClientSettingsActivation => presetController?.HasClientSettingsActivation ?? false;

	public bool HostSettingsEnabled
	{
		get
		{
			return presetController?.HostSettingsEnabled ?? false;
		}
		set
		{
			presetController?.SetHostSettingsEnabled(value);
		}
	}

	public bool ClientSettingsEnabled
	{
		get
		{
			return presetController?.ClientSettingsEnabled ?? false;
		}
		set
		{
			presetController?.SetClientSettingsEnabled(value);
		}
	}

	public bool IsLocalSettingsHost => isLocalHost;

	public bool IsRealMultiplayerContext => isRealMultiplayer;

	public bool MissionPresetEditable => missionPresetEditable;

	public bool IsMissionPresetSelected => missionPresetContext && selectedPreset == (presetController?.MissionPresetIndex ?? 2);

	public Visibility System_DirectLaunchNoticeVisibility => (Visibility)((showDirectLaunchNotice && (settingsMenuContext == SettingsMenuContext.Campaign || (settingsMenuContext == SettingsMenuContext.DirectTrail && (!missionPresetContext || !missionPresetHasExplicitSettings)))) ? 2 : 0);

	public string System_DirectLaunchNoticeText => isCastlePlannerSettings ? ResolveSettingsUiTextSafe("Common.DirectLaunchCastlePlannerNotice", "Castle spawning and gameplay changes are inactive for this direct start. Blueprints remain available. Use Customize to play with these changes; edits here are saved for later games.") : ResolveSettingsUiTextSafe("Common.DirectLaunchNotice", "This mod's gameplay changes are inactive for this direct start. Use Customize to play with them; edits here are saved for later games.");

	public Visibility System_TrailSourceNoticeVisibility => (Visibility)((settingsMenuContext == SettingsMenuContext.DirectTrail && missionPresetContext && !missionPresetEditable && missionPresetHasExplicitSettings) ? 2 : 0);

	public string System_TrailSourceNoticeText => ResolveSettingsUiTextSafe("Common.TrailSourceReadOnlyNotice", "These settings come from the selected Trail and are read-only here. Use Customize to change them.");

	public bool CanEditHostSettings => isLocalHost && (!IsMissionPresetSelected || missionPresetEditable);

	public bool CanEditClientSettings => !IsMissionPresetSelected || missionPresetEditable;

	public bool CanToggleHostSettings => HasHostSettings && HasHostSettingsActivation && CanEditHostSettings;

	public bool CanToggleClientSettings => HasClientSettings && HasClientSettingsActivation && CanEditClientSettings;

	public bool CanChangePreset => (!IsMissionPresetSelected || missionPresetEditable) && (isLocalHost || HasClientSettings);

	public bool CanResetSettings => CanEditHostSettings || (HasClientSettings && CanEditClientSettings);

	public Visibility PresetVisibility => (Visibility)((missionPresetContext || isLocalHost || HasClientSettings) ? 2 : 0);

	public Visibility ClientSettingsActivationVisibility => (Visibility)(HasClientSettingsActivation ? 2 : 0);

	public Visibility HostReadOnlyNoticeVisibility => (Visibility)((HasHostSettings && isRealMultiplayer && !isLocalHost) ? 2 : 0);

	public string HostOptionsText => ResolveSettingsUiTextSafe("Common.HostOptions", "HOST OPTIONS");

	public string ClientOptionsText => ResolveSettingsUiTextSafe("Common.ClientOptions", "LOCAL CLIENT OPTIONS");

	public string PresetText => ResolveSettingsUiTextSafe("Common.Preset", "Preset");

	public string ModEnabledText => ResolveSettingsUiTextSafe("Common.EnableMod", "Enable Mod");

	public string HostActivationLabelText => ResolveSettingsUiTextSafe("Common.HostActivationLabel", "(Host-)");

	public string ClientActivationLabelText => ResolveSettingsUiTextSafe("Common.ClientActivationLabel", "(Client settings)");

	public Visibility ActionsScopeNoticeVisibility => (Visibility)((isRealMultiplayer && HasClientSettings) ? 2 : 0);

	public string ActionsScopeNoticeText => (HasHostSettings && isLocalHost) ? ResolveSettingsUiTextSafe("Common.ActionsScopeHost", "Loading a preset or resetting settings affects host settings and your local client settings.") : ResolveSettingsUiTextSafe("Common.ActionsScopeClient", "Loading a preset or resetting settings affects only your local client settings.");

	public string HostReadOnlyNoticeText => ResolveSettingsUiTextSafe("Common.HostReadOnly", "Values from host - read-only");

	public string EnableModHelpText => ResolveSettingsUiTextSafe("Common.EnableModHelp", "Enables or disables this mod for the match.");

	public string HostSettingsActivationHelpText => ResolveSettingsUiTextSafe("Common.HostSettingsActivationHelp", "Enables or disables all host-controlled settings of this mod.");

	public string ClientSettingsActivationHelpText => ResolveSettingsUiTextSafe("Common.ClientSettingsActivationHelp", "Enables or disables all local and personal client settings of this mod.");

	public string PresetHelpText => ResolveSettingsUiTextSafe("Common.PresetHelp", "Loads saved settings as an editable working copy.");

	public string System_PresetLoadText => ResolveSettingsUiTextSafe("Common.PresetLoad", "Load preset");

	public string System_PresetSaveText => ResolveSettingsUiTextSafe("Common.PresetSave", "Save preset");

	public string System_SettingsSourceText => ResolveSettingsUiTextSafe("Common.SettingsSource", "Reset settings to");

	public string System_SettingsSourceLoadText => ResolveSettingsUiTextSafe("Common.SettingsSourceLoad", "Reset");

	public string System_SettingsSourceHelpText => ResolveSettingsUiTextSafe("Common.SettingsSourceHelp", "Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.");

	public ObservableCollection<ModSettingsWorkingSource> System_SettingsSources => settingsSources;

	public ModSettingsWorkingSource System_SelectedSettingsSource
	{
		get
		{
			return selectedSettingsSource;
		}
		set
		{
			if (selectedSettingsSource != value)
			{
				selectedSettingsSource = value;
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedSettingsSource");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanLoadSettingsSource");
			}
		}
	}

	public bool System_CanLoadSettingsSource => selectedSettingsSource != null && (!IsMissionPresetSelected || missionPresetEditable);

	public Visibility System_SettingsSourceVisibility
	{
		get
		{
			PresetController obj = presetController;
			return (Visibility)((obj != null && obj.HasPersistentSettings) ? 2 : 0);
		}
	}

	public string System_PresetStatusText => (!string.IsNullOrEmpty(System_ApplicationNotice)) ? System_ApplicationNotice : (presetController?.GetStatusText(ResolveSettingsUiTextSafe("Common.PresetBasedOn", "Based on"), ResolveSettingsUiTextSafe("Common.PresetModified", "modified"), ResolveSettingsUiTextSafe("Common.PresetSourcePersonal", "Personal presets"), ResolveSettingsUiTextSafe("Common.PresetSourceBundled", "Bundled with this mod"), ResolveSettingsUiTextSafe("Common.PresetSourceExternal", "External presets")) ?? string.Empty);

	public Visibility System_PresetStatusVisibility => (Visibility)((!string.IsNullOrWhiteSpace(System_PresetStatusText)) ? 2 : 0);

	public Visibility System_PresetLoadPanelVisibility => (Visibility)(presetLoadPanelOpen ? 2 : 0);

	public ObservableCollection<ModSettingsPresetListEntry> System_PresetLoadEntries => presetLoadEntries;

	public ModSettingsPresetListEntry System_SelectedPresetLoadEntry
	{
		get
		{
			return selectedPresetLoadEntry;
		}
		set
		{
			if (selectedPresetLoadEntry != value)
			{
				selectedPresetLoadEntry = value;
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedPresetLoadEntry");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanDeleteSelectedPreset");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetDeleteVisibility");
			}
		}
	}

	public string System_PresetLoadConfirmText => ResolveSettingsUiTextSafe("Common.PresetLoadConfirm", "Load");

	public string System_PresetLoadCancelText => ResolveSettingsUiTextSafe("Common.PresetLoadCancel", "Cancel");

	public string System_PresetDeleteText => ResolveSettingsUiTextSafe("Common.PresetDelete", "Delete");

	public bool System_CanDeleteSelectedPreset => selectedPresetLoadEntry?.CanDelete ?? false;

	public Visibility System_PresetDeleteVisibility => (Visibility)(System_CanDeleteSelectedPreset ? 2 : 0);

	public ObservableCollection<ModSettingsPresetSaveTarget> System_PresetSaveTargets => presetSaveTargets;

	public ModSettingsPresetSaveTarget System_SelectedPresetSaveTarget
	{
		get
		{
			return selectedPresetSaveTarget;
		}
		set
		{
			if (selectedPresetSaveTarget != value)
			{
				selectedPresetSaveTarget = value;
				if (value?.Preset != null)
				{
					presetSaveName = value.Preset.Name;
					presetSaveDescription = value.Preset.Description;
					ApplyPresetToSaveRows(value.Preset);
				}
				else if (value != null)
				{
					ResetPresetSaveForm();
				}
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedPresetSaveTarget");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveName");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveDescription");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanConfirmPresetSave");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveConfirmHelpText");
			}
		}
	}

	public string System_PresetSaveTargetText => ResolveSettingsUiTextSafe("Common.PresetSaveTarget", "Save as");

	public Visibility System_PresetSavePanelVisibility => (Visibility)(presetSavePanelOpen ? 2 : 0);

	public string System_PresetSaveName
	{
		get
		{
			return presetSaveName;
		}
		set
		{
			string b = value ?? string.Empty;
			if (!string.Equals(presetSaveName, b, StringComparison.Ordinal))
			{
				presetSaveName = b;
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveName");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanConfirmPresetSave");
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveConfirmHelpText");
			}
		}
	}

	public string System_PresetSaveDescription
	{
		get
		{
			return presetSaveDescription;
		}
		set
		{
			string b = value ?? string.Empty;
			if (!string.Equals(presetSaveDescription, b, StringComparison.Ordinal))
			{
				presetSaveDescription = b;
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveDescription");
			}
		}
	}

	public ObservableCollection<PresetSaveSettingViewModel> System_PresetSaveSettings => presetSaveSettings;

	public string System_PresetSaveNameText => ResolveSettingsUiTextSafe("Common.PresetSaveName", "Preset name");

	public string System_PresetSaveDescriptionText => ResolveSettingsUiTextSafe("Common.PresetSaveDescription", "Description (optional)");

	public string System_PresetSaveBulkModeText => ResolveSettingsUiTextSafe("Common.PresetSaveBulkMode", "Set all modes");

	public string System_PresetSaveBulkModeHelpText => ResolveSettingsUiTextSafe("Common.PresetSaveBulkModeHelp", "Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.");

	public string System_PresetLoadSelectionHelpText => ResolveSettingsUiTextSafe("Common.PresetLoadSelectionHelp", "Selecting a preset changes nothing until you choose Load.");

	public ComboBoxItem[] System_PresetSaveBulkModeOptions => (ComboBoxItem[])(object)new ComboBoxItem[5]
	{
		new ComboBoxItem
		{
			Content = ResolveSettingsUiTextSafe("Common.PresetModeDefault", "Default")
		},
		new ComboBoxItem
		{
			Content = ResolveSettingsUiTextSafe("Common.PresetModePlayer", "Player")
		},
		new ComboBoxItem
		{
			Content = ResolveSettingsUiTextSafe("Common.PresetModeFixed", "Fixed")
		},
		new ComboBoxItem
		{
			Content = ResolveSettingsUiTextSafe("Common.PresetModeHostFixed", "Host Fixed")
		},
		new ComboBoxItem
		{
			Content = ResolveSettingsUiTextSafe("Common.PresetModeMixed", "Mixed"),
			IsEnabled = false
		}
	};

	public int System_PresetSaveBulkModeIndex
	{
		get
		{
			if (presetSaveSettings.Count == 0)
			{
				return 3;
			}
			int[] array = presetSaveSettings.Select((PresetSaveSettingViewModel item) => item.SelectedModeIndex).Distinct().ToArray();
			if (array.Length == 1)
			{
				return array[0];
			}
			if (presetSaveSettings.All((PresetSaveSettingViewModel item) => item.SelectedModeIndex == ((item.Scope != PresetSettingScope.Host) ? 1 : 2)))
			{
				return 3;
			}
			return 4;
		}
		set
		{
			if (value < 0 || value > 3)
			{
				return;
			}
			applyingPresetSaveBulkMode = true;
			try
			{
				foreach (PresetSaveSettingViewModel presetSaveSetting in presetSaveSettings)
				{
					presetSaveSetting.SelectedModeIndex = ((value == 3) ? ((presetSaveSetting.Scope != PresetSettingScope.Host) ? 1 : 2) : value);
				}
			}
			finally
			{
				applyingPresetSaveBulkMode = false;
			}
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveBulkModeIndex");
		}
	}

	public string System_PresetSaveConfirmText => ResolveSettingsUiTextSafe("Common.PresetSaveConfirm", "Save");

	public bool System_CanConfirmPresetSave => !string.IsNullOrWhiteSpace(presetSaveName);

	public string System_PresetSaveConfirmHelpText => System_CanConfirmPresetSave ? System_PresetSaveConfirmText : ResolveSettingsUiTextSafe("Common.PresetSaveNameRequired", "Enter a preset name before saving.");

	public string System_PresetSaveCancelText => ResolveSettingsUiTextSafe("Common.PresetSaveCancel", "Cancel");

	public RelayCommand System_OpenPresetLoadCommand { get; }

	public RelayCommand System_ConfirmPresetLoadCommand { get; }

	public RelayCommand System_DeletePresetCommand { get; }

	public RelayCommand System_CancelPresetLoadCommand { get; }

	public RelayCommand System_OpenPresetSaveCommand { get; }

	public RelayCommand System_ConfirmPresetSaveCommand { get; }

	public RelayCommand System_CancelPresetSaveCommand { get; }

	public RelayCommand System_LoadSettingsSourceCommand { get; }

	public RelayCommand System_ConfirmPresetInlineActionCommand { get; }

	public RelayCommand System_CancelPresetInlineActionCommand { get; }

	public RelayCommand System_DismissPresetStatusCommand { get; }

	public string System_PresetInlineConfirmationTitle => presetInlineConfirmationTitle;

	public string System_PresetInlineConfirmationMessage => presetInlineConfirmationMessage;

	public Visibility System_PresetInlineConfirmationVisibility => (Visibility)((pendingDeletePreset != null || pendingOverwriteId != null) ? 2 : 0);

	public string System_PresetInlineConfirmText => ResolveSettingsUiTextSafe("Common.PresetConfirm", "Confirm");

	public string System_PresetInlineCancelText => ResolveSettingsUiTextSafe("Common.PresetSaveCancel", "Cancel");

	public string System_PresetOperationStatusText => presetOperationStatus;

	public Visibility System_PresetOperationStatusVisibility => (Visibility)((presetOperationFailed && !string.IsNullOrWhiteSpace(presetOperationStatus)) ? 2 : 0);

	public Visibility System_PresetOperationErrorVisibility => (Visibility)((presetOperationFailed && !string.IsNullOrWhiteSpace(presetOperationStatus)) ? 2 : 0);

	public Visibility System_PresetOperationSuccessVisibility => (Visibility)0;

	public string System_PresetStatusDismissText => ResolveSettingsUiTextSafe("Common.PresetStatusDismiss", "Close");

	public string System_ModSettingsSearchText
	{
		get
		{
			return modSettingsSearchText;
		}
		set
		{
			string b = value ?? string.Empty;
			if (!string.Equals(modSettingsSearchText, b, StringComparison.Ordinal) || modSettingsSearchExactKey.Length != 0)
			{
				modSettingsSearchText = b;
				modSettingsSearchExactKey = string.Empty;
				RaiseModSettingsSearchProperties();
			}
		}
	}

	public bool System_ModSettingsSearchIncludeToolTips
	{
		get
		{
			return modSettingsSearchIncludeToolTips;
		}
		set
		{
			if (modSettingsSearchIncludeToolTips != value)
			{
				modSettingsSearchIncludeToolTips = value;
				RaiseModSettingsSearchProperties();
			}
		}
	}

	public string System_ModSettingsSearchExactKey => modSettingsSearchExactKey;

	public int System_ModSettingsSearchFocusRequest => modSettingsSearchFocusRequest;

	public bool System_ModSettingsSearchHasActiveFilter => modSettingsSearchExactKey.Length > 0 || !string.IsNullOrWhiteSpace(modSettingsSearchText);

	public Visibility System_ModSettingsSearchPanelVisibility => (Visibility)(modSettingsSearchExpanded ? 2 : 0);

	public Visibility System_ModSettingsSearchInactiveVisibility => (Visibility)((!System_ModSettingsSearchHasActiveFilter) ? 2 : 0);

	public Visibility System_ModSettingsSearchNoResultsVisibility => (Visibility)((System_ModSettingsSearchHasActiveFilter && !ModSettingsSearch.HasMatches(this, modSettingsSearchText, modSettingsSearchIncludeToolTips, modSettingsSearchExactKey)) ? 2 : 0);

	public string System_ModSettingsSearchLabelText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchLabel", "Search");

	public string System_ModSettingsSearchHelpText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchHelp", "Search setting titles. Optionally include tooltips.");

	public string System_ModSettingsSearchToggleHelpText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchToggleHelp", "Show or hide the settings search.");

	public string System_ModSettingsSearchIncludeToolTipsText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchIncludeToolTips", "Search tooltips");

	public string System_ModSettingsSearchIncludeToolTipsHelpText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchIncludeToolTipsHelp", "Also search the explanatory tooltips of settings.");

	public string System_ModSettingsSearchClearHelpText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchClearHelp", "Clear the settings filter.");

	public string System_ModSettingsSearchNoResultsText => ResolveSettingsUiTextSafe("Common.ModSettingsSearchNoResults", "No matching settings found.");

	public RelayCommand System_ToggleModSettingsSearchCommand { get; }

	public RelayCommand System_ClearModSettingsSearchCommand { get; }

	public bool AreSettingsEditable => CanEditHostSettings;

	public bool IsMissionPresetActive => missionPresetContext;

	protected bool IsApplyingSettingsSnapshot => presetController?.IsApplyingSnapshot ?? false;

	/// <summary>Optional dynamically described local working configuration.</summary>
	protected virtual IDynamicPresetSettingsProvider DynamicSettingsProvider => null;

	protected virtual IModSettingsApplicationBackend SettingsApplicationBackend => DynamicSettingsProvider as IModSettingsApplicationBackend;

	public bool System_HasDynamicSettings => DynamicSettingsProvider != null;

	public bool System_HasApplicationBackend => SettingsApplicationBackend != null;

	public string System_ApplicationNotice { get; private set; } = "";

	public bool System_HasPendingConfiguration
	{
		get
		{
			try
			{
				return SettingsApplicationBackend != null && (SettingsApplicationBackend.ReadPendingValues() != null || ModSettingsApplication.HasRestartPreparation);
			}
			catch
			{
				return SettingsApplicationBackend != null;
			}
		}
	}

	public bool IsPerPlayerLobbySettingsReady => perPlayerSettingsCoordinator?.IsReady ?? true;

	public string PerPlayerLobbySettingsReadinessError => perPlayerSettingsCoordinator?.ReadinessError ?? string.Empty;

	protected PresetLobbyModSettingsViewModel()
	{
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Expected O, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Expected O, but got Unknown
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Expected O, but got Unknown
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Expected O, but got Unknown
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Expected O, but got Unknown
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Expected O, but got Unknown
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Expected O, but got Unknown
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Expected O, but got Unknown
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Expected O, but got Unknown
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Expected O, but got Unknown
		System_ToggleModSettingsSearchCommand = new RelayCommand((Action)ToggleModSettingsSearch, (Func<bool>)null);
		System_ClearModSettingsSearchCommand = new RelayCommand((Action)ClearModSettingsSearch, (Func<bool>)null);
		System_OpenPresetLoadCommand = new RelayCommand((Action)OpenPresetLoad, (Func<bool>)null);
		System_ConfirmPresetLoadCommand = new RelayCommand((Action)ConfirmPresetLoad, (Func<bool>)null);
		System_DeletePresetCommand = new RelayCommand((Action)DeleteSelectedPreset, (Func<bool>)null);
		System_CancelPresetLoadCommand = new RelayCommand((Action)CancelPresetLoad, (Func<bool>)null);
		System_OpenPresetSaveCommand = new RelayCommand((Action)OpenPresetSave, (Func<bool>)null);
		System_ConfirmPresetSaveCommand = new RelayCommand((Action)ConfirmPresetSave, (Func<bool>)null);
		System_CancelPresetSaveCommand = new RelayCommand((Action)CancelPresetSave, (Func<bool>)null);
		System_LoadSettingsSourceCommand = new RelayCommand((Action)LoadSelectedSettingsSource, (Func<bool>)null);
		System_ConfirmPresetInlineActionCommand = new RelayCommand((Action)ConfirmPresetInlineAction, (Func<bool>)null);
		System_CancelPresetInlineActionCommand = new RelayCommand((Action)CancelPresetInlineAction, (Func<bool>)null);
		System_DismissPresetStatusCommand = new RelayCommand((Action)DismissPresetStatus, (Func<bool>)null);
		ModSettingsWorkingSourceRegistry.SourcesChanged += RebuildSettingsSources;
	}

	private void OpenPresetLoad()
	{
		if (presetLoadPanelOpen)
		{
			presetLoadPanelOpen = false;
			RaisePresetDialogProperties();
			return;
		}
		presetSavePanelOpen = false;
		presetController?.RefreshCatalog();
		RebuildPresetDialogCatalogs();
		selectedPresetLoadEntry = presetLoadEntries.FirstOrDefault();
		presetLoadPanelOpen = true;
		RaisePresetDialogProperties();
	}

	private void ConfirmPresetLoad()
	{
		if (selectedPresetLoadEntry?.Preset == null)
		{
			return;
		}
		try
		{
			ApplyConfirmedPresetSelection(selectedPresetLoadEntry.Preset);
			presetLoadPanelOpen = false;
			RaisePresetDialogProperties();
			RaiseAccessProperties();
			DismissPresetStatus();
		}
		catch (Exception ex)
		{
			SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetLoadFailedTitle", "Preset load failed") + ": " + ex.Message, failed: true);
		}
	}

	private void CancelPresetLoad()
	{
		presetLoadPanelOpen = false;
		RaisePresetDialogProperties();
	}

	private void DeleteSelectedPreset()
	{
		PublishedModSettingsPreset publishedModSettingsPreset = selectedPresetLoadEntry?.Preset;
		if (publishedModSettingsPreset != null && System_CanDeleteSelectedPreset)
		{
			string text = ResolveSettingsUiTextSafe("Common.PresetDeleteConfirm", "The personal preset will be permanently deleted. Continue?") + Environment.NewLine + Environment.NewLine + publishedModSettingsPreset.Name;
			pendingDeletePreset = publishedModSettingsPreset;
			pendingOverwriteId = null;
			pendingOverwriteSelections = null;
			presetInlineConfirmationTitle = ResolveSettingsUiTextSafe("Common.PresetDeleteTitle", "Delete personal preset");
			presetInlineConfirmationMessage = text;
			RaisePresetInlineProperties();
		}
	}

	private void RebuildSettingsSources()
	{
		if (presetController == null)
		{
			return;
		}
		string previous = selectedSettingsSource?.Id;
		settingsSources.Clear();
		settingsSources.Add(new ModSettingsWorkingSource
		{
			Id = "mod-default",
			Kind = ModSettingsWorkingSourceKind.ModDefault,
			DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceDefaults", "Mod defaults")
		});
		foreach (ModSettingsWorkingSource providerSource in ModSettingsWorkingSourceRegistry.GetProviderSources(presetController.TargetGuid))
		{
			if (providerSource != null && !string.IsNullOrWhiteSpace(providerSource.Id) && !string.Equals(providerSource.Id, "mod-default", StringComparison.Ordinal))
			{
				if (providerSource.Kind == ModSettingsWorkingSourceKind.Trail)
				{
					providerSource.DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceTrail", "Trail settings");
				}
				else if (providerSource.Kind == ModSettingsWorkingSourceKind.Map)
				{
					providerSource.DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceMap", "Map settings");
				}
				settingsSources.Add(providerSource);
			}
		}
		ModSettingsWorkingSource modSettingsWorkingSource = settingsSources.FirstOrDefault((ModSettingsWorkingSource item) => item.IsPreferred) ?? settingsSources.FirstOrDefault((ModSettingsWorkingSource item) => string.Equals(item.Id, "mod-default", StringComparison.Ordinal));
		string preferred = modSettingsWorkingSource?.Id ?? "mod-default";
		string b = preferred + "\n" + (modSettingsWorkingSource?.PreferenceContextId ?? string.Empty);
		bool flag = !string.Equals(preferredSettingsSourceToken, b, StringComparison.Ordinal);
		selectedSettingsSource = (flag ? settingsSources.FirstOrDefault((ModSettingsWorkingSource item) => string.Equals(item.Id, preferred, StringComparison.Ordinal)) : settingsSources.FirstOrDefault((ModSettingsWorkingSource item) => string.Equals(item.Id, previous, StringComparison.Ordinal))) ?? settingsSources.FirstOrDefault((ModSettingsWorkingSource item) => string.Equals(item.Id, preferred, StringComparison.Ordinal)) ?? settingsSources.FirstOrDefault();
		preferredSettingsSourceToken = b;
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SettingsSources");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedSettingsSource");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanLoadSettingsSource");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SettingsSourceVisibility");
	}

	private void LoadSelectedSettingsSource()
	{
		ModSettingsWorkingSource modSettingsWorkingSource = selectedSettingsSource;
		if (modSettingsWorkingSource == null || !System_CanLoadSettingsSource)
		{
			return;
		}
		try
		{
			System_RefreshOwnConfiguration();
			if (string.Equals(modSettingsWorkingSource.Id, "mod-default", StringComparison.Ordinal))
			{
				if (IsMissionPresetSelected)
				{
					ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, modSettingsWorkingSource.Id);
				}
				else
				{
					presetController.ApplyDefaultsAsWorkingCopy();
				}
			}
			else
			{
				ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, modSettingsWorkingSource.Id);
			}
			System_CommitConfiguration();
			DismissPresetStatus();
			RaiseAccessProperties();
		}
		catch (Exception ex)
		{
			SetPresetStatus(ResolveSettingsUiTextSafe("Common.SettingsSourceLoadFailed", "Could not reset settings") + ": " + ex.Message, failed: true);
		}
	}

	private void CompletePresetDelete(PublishedModSettingsPreset preset)
	{
		try
		{
			presetController?.DeletePersonalPreset(preset);
			RebuildPresetDialogCatalogs();
			selectedPresetLoadEntry = presetLoadEntries.FirstOrDefault();
			RaisePresetDialogProperties();
			RaiseAccessProperties();
			DismissPresetStatus();
		}
		catch (Exception ex)
		{
			presetController?.LogPresetOperationFailure("delete", preset?.Id, ex);
			SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetDeleteFailedTitle", "Preset deletion failed") + ": " + ex.Message, failed: true);
		}
	}

	private void OpenPresetSave()
	{
		if (presetSavePanelOpen)
		{
			presetSavePanelOpen = false;
			RaisePresetSaveProperties();
			return;
		}
		presetLoadPanelOpen = false;
		presetController?.RefreshCatalog();
		RebuildPresetDialogCatalogs();
		selectedPresetSaveTarget = presetSaveTargets.FirstOrDefault();
		ExecutePresetAction();
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedPresetSaveTarget");
	}

	private void RebuildPresetDialogCatalogs()
	{
		presetLoadEntries.Clear();
		presetSaveTargets.Clear();
		presetSaveTargets.Add(new ModSettingsPresetSaveTarget
		{
			DisplayText = ResolveSettingsUiTextSafe("Common.PresetSaveNew", "New personal preset")
		});
		foreach (PublishedModSettingsPreset item in presetController?.PublishedPresets ?? Array.Empty<PublishedModSettingsPreset>())
		{
			string sourceLabel = item.SourceKind switch
			{
				ModSettingsPresetSourceKind.Personal => ResolveSettingsUiTextSafe("Common.PresetSourcePersonal", "Personal presets"), 
				ModSettingsPresetSourceKind.Bundled => ResolveSettingsUiTextSafe("Common.PresetSourceBundled", "Bundled with this mod"), 
				_ => ResolveSettingsUiTextSafe("Common.PresetSourceExternal", "External presets") + ": " + item.ProviderName, 
			};
			presetLoadEntries.Add(new ModSettingsPresetListEntry
			{
				Preset = item,
				SourceLabel = sourceLabel
			});
			if (item.CanOverwrite)
			{
				presetSaveTargets.Add(new ModSettingsPresetSaveTarget
				{
					Preset = item,
					DisplayText = item.Name
				});
			}
		}
	}

	private void ApplyPresetToSaveRows(PublishedModSettingsPreset preset)
	{
		if (preset == null)
		{
			return;
		}
		foreach (PresetSaveSettingViewModel presetSaveSetting in presetSaveSettings)
		{
			if (preset.Settings.TryGetValue(presetSaveSetting.PropertyName, out var value))
			{
				presetSaveSetting.SelectedModeIndex = (int)value.Mode;
			}
			else
			{
				presetSaveSetting.SelectedModeIndex = 1;
			}
		}
	}

	private void RaisePresetDialogProperties()
	{
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetLoadPanelVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSavePanelVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetLoadEntries");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedPresetLoadEntry");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanDeleteSelectedPreset");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetDeleteVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveTargets");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_SelectedPresetSaveTarget");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetStatusText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetStatusVisibility");
	}

	private void ExecutePresetAction()
	{
		presetSaveSettings.Clear();
		string[] modeOptions = new string[3]
		{
			ResolveSettingsUiTextSafe("Common.PresetModeDefault", "Default"),
			ResolveSettingsUiTextSafe("Common.PresetModePlayer", "Player"),
			ResolveSettingsUiTextSafe("Common.PresetModeFixed", "Fixed")
		};
		foreach (PresetSettingDescriptor item in System_GetPresetSettingDescriptors())
		{
			PresetSaveSettingViewModel presetSaveSettingViewModel = new PresetSaveSettingViewModel(item, ResolvePresetSettingScopeText(item.Scope), modeOptions);
			presetSaveSettingViewModel.RestartHelp = ResolveSettingsUiTextSafe("Common.RestartRequiredOption", "Restart required");
			presetSaveSettingViewModel.PropertyChanged += OnPresetSaveSettingPropertyChanged;
			presetSaveSettings.Add(presetSaveSettingViewModel);
		}
		ResetPresetSaveForm();
		presetSavePanelOpen = true;
		RaisePresetSaveProperties();
	}

	private void ResetPresetSaveForm()
	{
		foreach (PresetSaveSettingViewModel presetSaveSetting in presetSaveSettings)
		{
			presetSaveSetting.SelectedModeIndex = ((presetSaveSetting.Scope != PresetSettingScope.Host) ? 1 : 2);
		}
		presetSaveName = string.Empty;
		presetSaveDescription = string.Empty;
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveBulkModeIndex");
	}

	private void OnPresetSaveSettingPropertyChanged(object sender, PropertyChangedEventArgs args)
	{
		if (!applyingPresetSaveBulkMode && string.Equals(args?.PropertyName, "SelectedModeIndex", StringComparison.Ordinal))
		{
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveBulkModeIndex");
		}
	}

	private string ResolvePresetSettingScopeText(PresetSettingScope scope)
	{
		return scope switch
		{
			PresetSettingScope.Host => ResolveSettingsUiTextSafe("Common.PresetScopeHost", "Host"), 
			PresetSettingScope.Player => ResolveSettingsUiTextSafe("Common.PresetScopePlayer", "Player"), 
			PresetSettingScope.Local => ResolveSettingsUiTextSafe("Common.PresetScopeLocal", "Local"), 
			_ => scope.ToString(), 
		};
	}

	private void ConfirmPresetSave()
	{
		try
		{
			PresetSaveSelection[] selections = CreatePresetSaveSelections();
			PublishedModSettingsPreset publishedModSettingsPreset = selectedPresetSaveTarget?.Preset;
			if (publishedModSettingsPreset != null)
			{
				pendingDeletePreset = null;
				pendingOverwriteId = publishedModSettingsPreset.Id;
				pendingOverwriteSelections = selections;
				presetInlineConfirmationTitle = ResolveSettingsUiTextSafe("Common.PresetSaveOverwriteTitle", "Overwrite personal preset");
				presetInlineConfirmationMessage = ResolveSettingsUiTextSafe("Common.PresetSaveOverwrite", "The selected personal preset will be completely replaced. Continue?");
				RaisePresetInlineProperties();
			}
			else
			{
				string id = presetController?.CreateUniquePersonalPresetId(presetSaveName) ?? CreatePublishedPresetId(presetSaveName);
				System_SavePersonalPreset(id, presetSaveName, presetSaveDescription, selections, overwrite: false);
				ShowPresetSaveCompleted();
			}
		}
		catch (Exception exception)
		{
			presetController?.LogPresetOperationFailure("save", selectedPresetSaveTarget?.Preset?.Id, exception);
			ShowPresetSaveError(exception);
		}
	}

	private PresetSaveSelection[] CreatePresetSaveSelections()
	{
		return presetSaveSettings.Select((PresetSaveSettingViewModel item) => item.ToSelection()).ToArray();
	}

	private void CompletePresetSave(string id, PresetSaveSelection[] selections, bool overwrite)
	{
		try
		{
			System_SavePersonalPreset(id, presetSaveName, presetSaveDescription, selections, overwrite);
			ShowPresetSaveCompleted();
		}
		catch (Exception exception)
		{
			presetController?.LogPresetOperationFailure(overwrite ? "overwrite" : "save", id, exception);
			ShowPresetSaveError(exception);
		}
	}

	private void ShowPresetSaveCompleted()
	{
		presetSavePanelOpen = false;
		presetController?.RefreshCatalog();
		RebuildPresetDialogCatalogs();
		RaisePresetSaveProperties();
		DismissPresetStatus();
	}

	private void ShowPresetSaveError(Exception exception)
	{
		SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetSaveFailedTitle", "Preset save failed") + ": " + exception.Message, failed: true);
	}

	private void ConfirmPresetInlineAction()
	{
		PublishedModSettingsPreset publishedModSettingsPreset = pendingDeletePreset;
		string text = pendingOverwriteId;
		PresetSaveSelection[] array = pendingOverwriteSelections;
		ClearPresetInlineConfirmation();
		if (publishedModSettingsPreset != null)
		{
			CompletePresetDelete(publishedModSettingsPreset);
		}
		else if (text != null)
		{
			CompletePresetSave(text, array ?? Array.Empty<PresetSaveSelection>(), overwrite: true);
		}
	}

	private void CancelPresetInlineAction()
	{
		if (pendingDeletePreset != null)
		{
			presetController?.LogPresetOperationCancelled("delete", pendingDeletePreset.Id);
		}
		else if (pendingOverwriteId != null)
		{
			presetController?.LogPresetOperationCancelled("overwrite", pendingOverwriteId);
		}
		ClearPresetInlineConfirmation();
	}

	private void ClearPresetInlineConfirmation()
	{
		pendingDeletePreset = null;
		pendingOverwriteId = null;
		pendingOverwriteSelections = null;
		presetInlineConfirmationTitle = string.Empty;
		presetInlineConfirmationMessage = string.Empty;
		RaisePresetInlineProperties();
	}

	private void SetPresetStatus(string message, bool failed)
	{
		presetOperationStatus = message ?? string.Empty;
		presetOperationFailed = failed;
		RaisePresetInlineProperties();
	}

	private void DismissPresetStatus()
	{
		SetPresetStatus(string.Empty, failed: false);
	}

	private void RaisePresetInlineProperties()
	{
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetInlineConfirmationTitle");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetInlineConfirmationMessage");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetInlineConfirmationVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetOperationStatusText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetOperationStatusVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetOperationErrorVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetOperationSuccessVisibility");
	}

	private void CancelPresetSave()
	{
		presetSavePanelOpen = false;
		RaisePresetSaveProperties();
	}

	private void RaisePresetSaveProperties()
	{
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSavePanelVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveName");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveDescription");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveBulkModeIndex");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanConfirmPresetSave");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetSaveConfirmHelpText");
		RaisePresetDialogProperties();
	}

	private static string CreatePublishedPresetId(string name)
	{
		string text = (name ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			throw new InvalidDataException("A preset name is required.");
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		bool flag = false;
		string text2 = text.ToLowerInvariant();
		foreach (char c in text2)
		{
			if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
			{
				stringBuilder.Append(c);
				flag = false;
			}
			else if (!flag)
			{
				stringBuilder.Append('-');
				flag = true;
			}
		}
		string text3 = stringBuilder.ToString().Trim('-');
		if (text3.Length == 0)
		{
			throw new InvalidDataException("The preset name does not contain a usable id.");
		}
		return (text3.Length <= 128) ? text3 : text3.Substring(0, 128).TrimEnd('-');
	}

	/// <summary>Safe reflection bridge used by the optional global search host.</summary>
	public bool System_ApplyModSettingsSearchTarget(string key, string title)
	{
		string text = ModSettingsSearchMatcher.Normalize(key);
		if (text.Length == 0)
		{
			return false;
		}
		modSettingsSearchText = title ?? string.Empty;
		modSettingsSearchExactKey = text;
		modSettingsSearchExpanded = true;
		RaiseModSettingsSearchProperties();
		return true;
	}

	private void ToggleModSettingsSearch()
	{
		modSettingsSearchExpanded = !modSettingsSearchExpanded;
		RaiseModSettingsSearchProperties();
		if (modSettingsSearchExpanded)
		{
			modSettingsSearchFocusRequest++;
			if (modSettingsSearchFocusRequest <= 0)
			{
				modSettingsSearchFocusRequest = 1;
			}
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchFocusRequest");
		}
	}

	private void ClearModSettingsSearch()
	{
		modSettingsSearchText = string.Empty;
		modSettingsSearchExactKey = string.Empty;
		RaiseModSettingsSearchProperties();
	}

	private void RaiseModSettingsSearchProperties()
	{
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchIncludeToolTips");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchExactKey");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchHasActiveFilter");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchPanelVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchInactiveVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_ModSettingsSearchNoResultsVisibility");
	}

	protected virtual string ResolveSettingsUiText(string key, string fallback)
	{
		return fallback;
	}

	/// <summary>Applies an explicitly confirmed selection. Overrides may add deferred application.</summary>
	protected virtual void ApplyConfirmedPresetSelection(PublishedModSettingsPreset preset)
	{
		if (presetController == null)
		{
			throw new InvalidOperationException("Preset controller is not initialized.");
		}
		if (SettingsApplicationBackend != null)
		{
			SettingsApplicationBackend.ReplaceDesiredValues(SettingsApplicationBackend.ReadOwnValues());
		}
		presetController.LoadPreset(preset);
		System_CommitConfiguration();
	}

	private string ResolveSettingsUiTextSafe(string key, string fallback)
	{
		string text = ResolveSettingsUiText(key, fallback);
		return (string.IsNullOrWhiteSpace(text) || string.Equals(text, key, StringComparison.Ordinal)) ? ResolveApiSharedFallback(key, fallback) : text;
	}

	private static string ResolveApiSharedFallback(string key, string english)
	{
		string text = string.Empty;
		try
		{
			text = GameAssetManagerAPI.Instance.CurrentLanguage ?? string.Empty;
		}
		catch
		{
		}
		if (!text.StartsWith("de", StringComparison.OrdinalIgnoreCase))
		{
			return english;
		}
		return key switch
		{
			"Common.PresetLoad" => "Preset laden", 
			"Common.PresetSave" => "Preset speichern", 
			"Common.PresetBasedOn" => "Basiert auf", 
			"Common.PresetModified" => "geändert", 
			"Common.PresetLoadConfirm" => "Laden", 
			"Common.PresetLoadCancel" => "Abbrechen", 
			"Common.PresetDelete" => "Löschen", 
			"Common.PresetDeleteTitle" => "Eigenes Preset löschen", 
			"Common.PresetDeleteConfirm" => "Das eigene Preset wird endgültig gelöscht. Fortfahren?", 
			"Common.PresetDeleteFailedTitle" => "Preset konnte nicht gelöscht werden", 
			"Common.PresetSaveTarget" => "Speichern als", 
			"Common.PresetSaveNew" => "Neues persönliches Preset", 
			"Common.PresetSaveName" => "Presetname", 
			"Common.PresetSaveDescription" => "Beschreibung (optional)", 
			"Common.PresetSaveBulkMode" => "Alle Modi setzen", 
			"Common.PresetSaveBulkModeHelp" => "Standard: Mod-Standard verwenden. Spieler: aktuellen Spielerwert behalten. Fest: gespeicherten Wert anwenden. Host fest: Hostwerte festlegen, Spieler-/lokale Werte behalten.", 
			"Common.PresetLoadSelectionHelp" => "Die Auswahl ändert noch nichts. Erst Laden wendet das Preset an.", 
			"Common.PresetSaveCancel" => "Abbrechen", 
			"Common.PresetSourcePersonal" => "Eigene Presets", 
			"Common.PresetSourceBundled" => "Mit diesem Mod geliefert", 
			"Common.PresetSourceExternal" => "Externe Presets", 
			"Common.PresetSaveConfirm" => "Speichern", 
			"Common.PresetSaveNameRequired" => "Vor dem Speichern einen Presetnamen eingeben.", 
			"Common.PresetModeHostFixed" => "Host fest", 
			"Common.PresetSaveOverwriteTitle" => "Eigenes Preset überschreiben", 
			"Common.PresetSaveOverwrite" => "Das gewählte eigene Preset wird vollständig ersetzt. Fortfahren?", 
			"Common.PresetSaveFailedTitle" => "Preset konnte nicht gespeichert werden", 
			"Common.PresetLoadFailedTitle" => "Preset konnte nicht geladen werden", 
			"Common.SettingsSource" => "Einstellungen zurücksetzen auf", 
			"Common.SettingsSourceLoad" => "Zurücksetzen", 
			"Common.SettingsSourceHelp" => "Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.", 
			"Common.SettingsSourceDefaults" => "Mod-Standards", 
			"Common.SettingsSourceTrail" => "Trail-Einstellungen", 
			"Common.SettingsSourceMap" => "Map-Einstellungen", 
			"Common.SettingsSourceLoadFailed" => "Einstellungen konnten nicht zurückgesetzt werden", 
			"Common.DirectLaunchNotice" => "Die Spieländerungen dieser Mod sind für diesen direkten Start inaktiv. Über „Customize“ starten, um damit zu spielen. Änderungen hier werden für spätere Partien gespeichert.", 
			"Common.DirectLaunchCastlePlannerNotice" => "Burgplatzierung und Spieländerungen sind für diesen direkten Start inaktiv; Blaupausen bleiben verfügbar. Über „Customize“ starten, um die Spieländerungen zu nutzen. Änderungen hier werden gespeichert.", 
			"Common.TrailSourceReadOnlyNotice" => "Diese Werte stammen aus dem gewählten Trail und sind hier schreibgeschützt. Zum Ändern „Customize“ wählen.", 
			"Common.PresetConfirm" => "Bestätigen", 
			"Common.PresetStatusDismiss" => "Schließen", 
			_ => english, 
		};
	}

	/// <summary>
	/// Replaces one captured code-default value without changing the current working settings.
	/// This is intended for defaults which can only be materialized after dynamic discovery.
	/// </summary>
	protected void SetModDefaultValue<T>(string propertyName, T value)
	{
		if (presetController == null)
		{
			throw new InvalidOperationException("Preset storage must be prepared before dynamic defaults are updated.");
		}
		presetController.SetDefaultValue(propertyName, value);
	}

	public object System_ReadDescribedValue(string key)
	{
		return DynamicSettingsProvider.ReadValue(key);
	}

	public void System_DiscardPendingConfiguration()
	{
		if (ModSettingsApplication.HasRestartPreparation)
		{
			ModSettingsApplication.DiscardPreparation();
		}
		else
		{
			DiscardApplicationPackage();
		}
	}

	internal void DiscardApplicationPackage()
	{
		IModSettingsApplicationBackend settingsApplicationBackend = SettingsApplicationBackend;
		if (settingsApplicationBackend == null)
		{
			return;
		}
		settingsApplicationBackend.DiscardPendingConfiguration();
		bool flag = !string.IsNullOrEmpty(settingsApplicationBackend.ActiveContextId);
		settingsApplicationBackend.ReplaceDesiredValues(flag ? settingsApplicationBackend.ReadOwnValues() : settingsApplicationBackend.ReadActiveValues());
		if (flag)
		{
			if (System_GetPresetSettingDescriptors().Any((PresetSettingDescriptor x) => x.RequiresRestart))
			{
				settingsApplicationBackend.ReturnToOwnConfiguration();
			}
			else
			{
				settingsApplicationBackend.ApplyValues(settingsApplicationBackend.ReadDesiredValues(), "");
				Dictionary<string, object> applied = settingsApplicationBackend.ReadActiveValues();
				if (settingsApplicationBackend.ReadDesiredValues().Any((KeyValuePair<string, object> x) => !applied.TryGetValue(x.Key, out var value) || !object.Equals(value, x.Value)))
				{
					throw new InvalidOperationException("Configuration backend did not restore personal values.");
				}
			}
		}
		Dictionary<string, object> activeAfterDiscard = settingsApplicationBackend.ReadActiveValues();
		Dictionary<string, object> desiredAfterDiscard = settingsApplicationBackend.ReadDesiredValues();
		ReportConfigurationResult(flag && System_GetPresetSettingDescriptors().Any((PresetSettingDescriptor x) => x.RequiresRestart && (!activeAfterDiscard.TryGetValue(x.PropertyName, out var value) || !object.Equals(value, desiredAfterDiscard[x.PropertyName]))));
	}

	public void System_CommitConfiguration()
	{
		bool restart = ModSettingsApplication.Commit(presetController.TargetGuid);
		ReportConfigurationResult(restart);
	}

	internal void ReportConfigurationResult(bool restart)
	{
		SetConfigurationNotice(restart ? ResolveSettingsUiTextSafe("Common.RestartRequired", "Settings prepared. Restart the game to apply them.") : "");
	}

	protected void SetConfigurationNotice(string message)
	{
		System_ApplicationNotice = message ?? "";
		RefreshConfigurationBindings();
	}

	internal void RefreshConfigurationBindings()
	{
		OnPropertyChanged("System_ApplicationNotice");
		OnPropertyChanged("System_HasPendingConfiguration");
		OnPropertyChanged("System_PresetStatusText");
		OnPropertyChanged("System_PresetStatusVisibility");
	}

	internal Dictionary<string, byte[]> CaptureApplicationSnapshot()
	{
		if (SettingsApplicationBackend == null)
		{
			return System_CreateCurrentWorkingSnapshot();
		}
		return SettingsApplicationBackend.ReadDesiredValues().ToDictionary((KeyValuePair<string, object> x) => x.Key, (KeyValuePair<string, object> x) => MessagePackSerializer.Serialize(x.Value.GetType(), x.Value, (MessagePackSerializerOptions)null, default(CancellationToken)), StringComparer.Ordinal);
	}

	internal Dictionary<string, string> CaptureRestartSources()
	{
		return new Dictionary<string, string>
		{
			["resetSource"] = selectedSettingsSource?.Id ?? "",
			["label"] = presetController?.GetStatusText("Based on", "modified", "Personal presets", "Bundled with this mod", "External presets") ?? ""
		};
	}

	internal void RestoreRestartSources(Dictionary<string, string> sources)
	{
		if (sources.TryGetValue("resetSource", out var selected))
		{
			System_SelectedSettingsSource = settingsSources.FirstOrDefault((ModSettingsWorkingSource x) => x.Id == selected) ?? selectedSettingsSource;
		}
		if (sources.TryGetValue("label", out var value) && !string.IsNullOrWhiteSpace(value))
		{
			presetController?.RestorePreparationLabel(value);
		}
		RaiseAccessProperties();
	}

	public string System_OwnConfigurationFingerprint()
	{
		if (SettingsApplicationBackend == null)
		{
			return "";
		}
		Dictionary<string, object> source = SettingsApplicationBackend.ReadOwnValues();
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		foreach (KeyValuePair<string, object> item in source.OrderBy((KeyValuePair<string, object> x) => x.Key, StringComparer.Ordinal))
		{
			binaryWriter.Write(item.Key);
			byte[] array = MessagePackSerializer.Serialize(item.Value.GetType(), item.Value, (MessagePackSerializerOptions)null, default(CancellationToken));
			binaryWriter.Write(array.Length);
			binaryWriter.Write(array);
		}
		binaryWriter.Flush();
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(memoryStream.ToArray())).Replace("-", "");
	}

	public void System_RefreshOwnConfiguration()
	{
		if (SettingsApplicationBackend != null)
		{
			SettingsApplicationBackend.ReplaceDesiredValues(SettingsApplicationBackend.ReadOwnValues());
		}
	}

	public bool System_ConfigurationNeedsRestart()
	{
		if (SettingsApplicationBackend == null)
		{
			return false;
		}
		if (SettingsApplicationBackend is INetworkModSettingsApplicationBackend { IsNetworkConfigurationClient: not false })
		{
			return false;
		}
		Dictionary<string, object> active = SettingsApplicationBackend.ReadActiveValues();
		Dictionary<string, object> desired = SettingsApplicationBackend.ReadDesiredValues();
		return System_GetPresetSettingDescriptors().Any((PresetSettingDescriptor x) => x.RequiresRestart && (!active.TryGetValue(x.PropertyName, out var value) || !object.Equals(value, desired[x.PropertyName])));
	}

	public bool System_ApplyConfiguration(string contextId)
	{
		return System_ApplyConfiguration(contextId, personalChoiceConfirmed: false);
	}

	internal bool System_ApplyConfiguration(string contextId, bool personalChoiceConfirmed)
	{
		IModSettingsApplicationBackend settingsApplicationBackend = SettingsApplicationBackend;
		if (settingsApplicationBackend == null)
		{
			return false;
		}
		if (settingsApplicationBackend is INetworkModSettingsApplicationBackend { IsNetworkConfigurationClient: not false } networkModSettingsApplicationBackend)
		{
			return !networkModSettingsApplicationBackend.PrepareNetworkConfiguration();
		}
		Dictionary<string, object> desired = settingsApplicationBackend.ReadDesiredValues();
		Dictionary<string, object> active = settingsApplicationBackend.ReadActiveValues();
		PresetSettingDescriptor[] array = (from x in System_GetPresetSettingDescriptors()
			where !active.TryGetValue(x.PropertyName, out var value) || !object.Equals(value, desired[x.PropertyName])
			select x).ToArray();
		if (array.Length == 0)
		{
			Dictionary<string, object> own = settingsApplicationBackend.ReadOwnValues();
			bool flag = string.IsNullOrEmpty(contextId) && desired.Any((KeyValuePair<string, object> x) => !own.TryGetValue(x.Key, out var value) || !object.Equals(value, x.Value));
			if (flag && personalChoiceConfirmed)
			{
				if (System_GetPresetSettingDescriptors().Any((PresetSettingDescriptor x) => x.RequiresRestart))
				{
					settingsApplicationBackend.StageValues(desired, "");
				}
				else
				{
					settingsApplicationBackend.ApplyValues(desired, "");
				}
			}
			else
			{
				Dictionary<string, object> pending = settingsApplicationBackend.ReadPendingValues();
				bool flag2 = flag && pending != null && desired.All((KeyValuePair<string, object> x) => pending.TryGetValue(x.Key, out var value) && object.Equals(value, x.Value));
				if (pending != null && !flag2)
				{
					settingsApplicationBackend.DiscardPendingConfiguration();
				}
			}
			return false;
		}
		if (array.Any((PresetSettingDescriptor x) => x.RequiresRestart))
		{
			settingsApplicationBackend.StageValues(desired, contextId);
			return true;
		}
		settingsApplicationBackend.ApplyValues(desired, contextId);
		Dictionary<string, object> applied = settingsApplicationBackend.ReadActiveValues();
		if (desired.Any((KeyValuePair<string, object> x) => !applied.TryGetValue(x.Key, out var value) || !object.Equals(value, x.Value)))
		{
			throw new InvalidOperationException("Configuration backend did not apply the requested values.");
		}
		if (settingsApplicationBackend.ReadPendingValues() != null)
		{
			settingsApplicationBackend.DiscardPendingConfiguration();
		}
		return false;
	}

	public void System_ReturnToOwnConfiguration()
	{
		IModSettingsApplicationBackend settingsApplicationBackend = SettingsApplicationBackend;
		if (settingsApplicationBackend == null || settingsApplicationBackend.ReadPendingValues() != null)
		{
			return;
		}
		settingsApplicationBackend.ReplaceDesiredValues(settingsApplicationBackend.ReadOwnValues());
		if (string.IsNullOrEmpty(settingsApplicationBackend.ActiveContextId))
		{
			return;
		}
		if (System_GetPresetSettingDescriptors().Any((PresetSettingDescriptor x) => x.RequiresRestart))
		{
			settingsApplicationBackend.ReturnToOwnConfiguration();
			return;
		}
		settingsApplicationBackend.ApplyValues(settingsApplicationBackend.ReadDesiredValues(), "");
		Dictionary<string, object> applied = settingsApplicationBackend.ReadActiveValues();
		if (!settingsApplicationBackend.ReadDesiredValues().Any((KeyValuePair<string, object> x) => !applied.TryGetValue(x.Key, out var value) || !object.Equals(value, x.Value)))
		{
			return;
		}
		throw new InvalidOperationException("Configuration backend did not restore personal values.");
	}

	protected virtual void OnSettingsSnapshotApplied()
	{
	}

	/// <summary>
	/// Declares the few domain-specific parts of personal settings. Transport,
	/// player-slot ownership, lobby convergence and readiness stay in APIShared.
	/// </summary>
	protected virtual void ConfigurePerPlayerLobbySettings(PerPlayerLobbySettingsBuilder settings)
	{
	}

	public void System_RequestPerPlayerSettingsPublish()
	{
		perPlayerSettingsCoordinator?.RequestPublish();
	}

	public IReadOnlyList<ModSettingsSearchEntry> System_GetModSettingsSearchEntries(FrameworkElement view)
	{
		return ModSettingsSearch.Export(this, view);
	}

	public bool System_ArePerPlayerSettingsReady(IEnumerable<int> playerIds, out string error)
	{
		if (perPlayerSettingsCoordinator == null)
		{
			error = string.Empty;
			return true;
		}
		return perPlayerSettingsCoordinator.ArePlayersReady(playerIds, out error);
	}

	/// <summary>
	/// Authorizes a settings mutation before any backing state is changed.
	/// Preset and Trail snapshots are trusted internal applications; all other
	/// writes use the Script Extender's ownership gate.
	/// </summary>
	protected bool CanMutateSetting([CallerMemberName] string propertyName = null)
	{
		PresetController obj = presetController;
		if (obj != null && obj.IsApplyingSnapshot)
		{
			return true;
		}
		if (PresetController.IsNetworkSyncInProgress())
		{
			return ((LobbyModSettingsBaseViewModel)this).CanEdit(propertyName);
		}
		System_RefreshSettingsAccess();
		if (IsMissionPresetSelected && !missionPresetEditable)
		{
			NotifyRejectedProperty(propertyName);
			return false;
		}
		return ((LobbyModSettingsBaseViewModel)this).CanEdit(propertyName);
	}

	/// <summary>
	/// Also refreshes editable proxy properties after a rejected write. The
	/// Extender's private revert path keeps these notifications out of sync
	/// and storage just like the primary property notification.
	/// </summary>
	protected bool CanMutateSettingWithDependents(string propertyName, params string[] dependentPropertyNames)
	{
		if (CanMutateSetting(propertyName))
		{
			return true;
		}
		if (dependentPropertyNames == null)
		{
			return false;
		}
		foreach (string text in dependentPropertyNames)
		{
			if (!string.IsNullOrEmpty(text) && !string.Equals(propertyName, text, StringComparison.Ordinal))
			{
				NotifyRejectedProperty(text);
			}
		}
		return false;
	}

	private void NotifyRejectedProperty(string propertyName)
	{
		if (NotifyRevertMethod != null && !string.IsNullOrEmpty(propertyName))
		{
			NotifyRevertMethod.Invoke(this, new object[1] { propertyName });
		}
	}

	internal void PreparePresets(ManualLogSource log, string pluginAssemblyLocation, string modName, string targetGuid, Version targetVersion, bool logRoutineActivity = true)
	{
		if (presetController != null)
		{
			throw new InvalidOperationException("Preset storage for [" + modName + "] was already prepared.");
		}
		presetController = new PresetController(this, log, pluginAssemblyLocation, modName, targetGuid, targetVersion, logRoutineActivity);
		presetController.CaptureDefaults();
		ModSettingsApplication.Register(targetGuid, this, pluginAssemblyLocation);
		RebuildSettingsSources();
		((LobbyModSettingsBaseViewModel)this).PropertyChanged += delegate
		{
			System_RefreshSettingsAccess();
		};
		System_RefreshSettingsAccess();
	}

	internal void ActivatePresets()
	{
		if (presetController == null)
		{
			throw new InvalidOperationException("Preset storage must be prepared before it is activated.");
		}
		presetController.Activate();
	}

	internal void PreparePerPlayerLobbySettings(ManualLogSource log, string modName, string ownerGuid, bool logRoutineActivity = true)
	{
		if (perPlayerSettingsCoordinator != null)
		{
			throw new InvalidOperationException("Per-player lobby settings for [" + modName + "] were already prepared.");
		}
		PerPlayerLobbySettingsBuilder perPlayerLobbySettingsBuilder = new PerPlayerLobbySettingsBuilder(this);
		ConfigurePerPlayerLobbySettings(perPlayerLobbySettingsBuilder);
		perPlayerSettingsCoordinator = new PerPlayerLobbySettingsCoordinator(this, log, modName, ownerGuid, perPlayerLobbySettingsBuilder.Build(), logRoutineActivity);
	}

	internal void ActivatePerPlayerLobbySettings()
	{
		if (perPlayerSettingsCoordinator == null)
		{
			throw new InvalidOperationException("Per-player lobby settings must be prepared before activation.");
		}
		perPlayerSettingsCoordinator.Activate();
	}

	internal void DeactivatePerPlayerLobbySettings()
	{
		perPlayerSettingsCoordinator?.Deactivate();
		perPlayerSettingsCoordinator = null;
	}

	public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot()
	{
		return presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
	}

	public Dictionary<string, byte[]> System_CreateModDefaultSnapshot()
	{
		return presetController?.CreateDefaultSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
	}

	public Dictionary<string, byte[]> System_CreateCurrentMissionPresetSnapshot()
	{
		return System_CreateCurrentWorkingSnapshot();
	}

	public Dictionary<string, byte[]> System_CreatePlayerMissionPresetSnapshot()
	{
		if (SettingsApplicationBackend != null)
		{
			return SettingsApplicationBackend.ReadOwnValues().ToDictionary((KeyValuePair<string, object> x) => x.Key, (KeyValuePair<string, object> x) => MessagePackSerializer.Serialize(x.Value.GetType(), x.Value, (MessagePackSerializerOptions)null, default(CancellationToken)), StringComparer.Ordinal);
		}
		return presetController?.CreatePlayerMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
	}

	public void System_ApplyMissionPresetSnapshot(Dictionary<string, byte[]> snapshot, string label)
	{
		presetController?.ApplyMissionWorkingSnapshot(snapshot, label);
		RaiseAccessProperties();
	}

	public Dictionary<string, byte[]> System_CreateCurrentWorkingSnapshot()
	{
		return (SettingsApplicationBackend != null) ? CaptureApplicationSnapshot() : (presetController?.CreateCurrentMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal));
	}

	public void System_ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot)
	{
		presetController?.ApplyWorkingSnapshot(snapshot);
		RaiseAccessProperties();
	}

	public void System_LoadModDefaults()
	{
		if (IsMissionPresetSelected)
		{
			ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, "mod-default");
		}
		else
		{
			presetController?.ApplyDefaultsAsWorkingCopy();
		}
		System_CommitConfiguration();
		RaiseAccessProperties();
	}

	/// <summary>Returns the persistent settings available to shared preset authoring UI.</summary>
	public IReadOnlyList<PresetSettingDescriptor> System_GetPresetSettingDescriptors()
	{
		return presetController?.GetSettingDescriptors() ?? Array.Empty<PresetSettingDescriptor>();
	}

	/// <summary>Imports a validated preset through the shared catalog, without overwriting an existing preset.</summary>
	public string System_ImportPresetJson(string json)
	{
		if (!CanChangePreset)
		{
			throw new InvalidOperationException("Preset editing is locked.");
		}
		string result = presetController.ImportPresetJson(json);
		RebuildPresetDialogCatalogs();
		RaisePresetDialogProperties();
		return result;
	}

	/// <summary>Exports the selected catalog preset with its original per-option modes.</summary>
	public string System_ExportSelectedPresetJson()
	{
		PublishedModSettingsPreset publishedModSettingsPreset = selectedPresetLoadEntry?.Preset;
		if (publishedModSettingsPreset == null)
		{
			throw new InvalidOperationException("Select a saved preset in the load list first.");
		}
		return ModSettingsPresetJson.Serialize(presetController.TargetGuid, publishedModSettingsPreset.Id, publishedModSettingsPreset.Name, publishedModSettingsPreset.Description, publishedModSettingsPreset.MinimumTargetVersion, publishedModSettingsPreset.MaximumTargetVersion, publishedModSettingsPreset.Settings);
	}

	/// <summary>Saves selected settings as a persistent, personal preset JSON file.</summary>
	public string System_SavePersonalPreset(string id, string name, string description, IEnumerable<PresetSaveSelection> selections, bool overwrite)
	{
		return presetController?.SavePersonalPreset(id, name, description, selections, overwrite) ?? string.Empty;
	}

	public void System_SetExplicitMissionSettings(bool hasExplicitSettings)
	{
		missionPresetHasExplicitSettings = hasExplicitSettings;
		RaiseAccessProperties();
	}

	public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
	{
		if (presetController != null)
		{
			missionPresetContext = true;
			missionPresetEditable = editable;
			presetController.EnterMissionPreset(snapshot, label, editable);
			RaiseAccessProperties();
		}
	}

	public void System_ExitMissionPreset()
	{
		if (missionPresetContext && presetController != null)
		{
			missionPresetContext = false;
			missionPresetEditable = false;
			missionPresetHasExplicitSettings = false;
			presetController.ExitMissionPreset();
			RaiseAccessProperties();
		}
	}

	public void System_RefreshSettingsAccess()
	{
		bool flag;
		bool flag2;
		SettingsMenuContext settingsMenuContext;
		try
		{
			flag = GameModeHelper.IsRealMultiplayer();
			flag2 = GameNetworkAPI.IsLocalHost();
			settingsMenuContext = CaptureSettingsMenuContext();
		}
		catch
		{
			return;
		}
		if (isLocalHost != flag2 || isRealMultiplayer != flag || this.settingsMenuContext != settingsMenuContext)
		{
			isLocalHost = flag2;
			isRealMultiplayer = flag;
			this.settingsMenuContext = settingsMenuContext;
			RaiseAccessProperties();
		}
	}

	private static SettingsMenuContext CaptureSettingsMenuContext()
	{
		if (!MainViewModel.viewModelLoaded)
		{
			return SettingsMenuContext.Other;
		}
		try
		{
			MainViewModel instance = MainViewModel.Instance;
			if (instance == null)
			{
				return SettingsMenuContext.Other;
			}
			bool campaign = instance.Show_Historical1CampaignMenu || instance.Show_Historical2CampaignMenu || instance.Show_Historical3CampaignMenu || instance.Show_Historical4CampaignMenu || instance.Show_Historical5CampaignMenu || instance.Show_Historical6CampaignMenu || instance.Show_Historical7CampaignMenu;
			bool trail = instance.Show_TrailCampaignMenu || instance.Show_Trail2CampaignMenu || instance.Show_Trail3CampaignMenu || instance.Show_SandsTrail1Menu || instance.Show_SandsTrail2Menu || instance.Show_SandsTrail3Menu || instance.Show_SandsTrail4Menu || instance.Show_SandsTrail5Menu || instance.Show_SandsTrail6Menu || instance.Show_SandsTrail7Menu || instance.Show_SandsTrail8Menu;
			bool coopTrail = instance.Show_CoopTrail1 || instance.Show_CoopTrail2 || instance.Show_CoopTrail3 || instance.Show_CoopTrail4;
			return ResolveSettingsMenuContext(instance.Show_MultiplayerSetup, campaign, trail, coopTrail);
		}
		catch
		{
		}
		return SettingsMenuContext.Other;
	}

	private static SettingsMenuContext ResolveSettingsMenuContext(bool customizeSetup, bool campaign, bool trail, bool coopTrail)
	{
		if (coopTrail && !campaign)
		{
			return SettingsMenuContext.DirectTrail;
		}
		if (customizeSetup)
		{
			return SettingsMenuContext.CustomizeSetup;
		}
		if (campaign && !trail)
		{
			return SettingsMenuContext.Campaign;
		}
		if (trail && !campaign)
		{
			return SettingsMenuContext.DirectTrail;
		}
		return SettingsMenuContext.Other;
	}

	public void System_ConfigureDirectLaunchNotice(string modGuid)
	{
		if (!string.IsNullOrWhiteSpace(modGuid))
		{
			try
			{
				GameplayModModePolicy.GetProfile(modGuid, modGuid);
				showDirectLaunchNotice = true;
				isCastlePlannerSettings = string.Equals(modGuid, "CastlePlanner_Serp", StringComparison.Ordinal);
			}
			catch (ArgumentOutOfRangeException)
			{
				showDirectLaunchNotice = false;
				isCastlePlannerSettings = false;
			}
			RaiseAccessProperties();
		}
	}

	protected void OnPropertyChanged(string name)
	{
		try
		{
			((LobbyModSettingsBaseViewModel)this).OnPropertyChanged(name);
			PresetController obj = presetController;
			if (obj != null && obj.IsHostSettingsActivationProperty(name))
			{
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HostSettingsEnabled");
			}
			PresetController obj2 = presetController;
			if (obj2 != null && obj2.IsClientSettingsActivationProperty(name))
			{
				((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ClientSettingsEnabled");
			}
		}
		finally
		{
			presetController?.AfterPropertyChanged(name);
			System_RefreshSettingsAccess();
		}
	}

	private void SetSelectedPresetCore(int value)
	{
		if (selectedPreset != value)
		{
			selectedPreset = value;
			RaiseAccessProperties();
		}
	}

	private void RaiseAccessProperties()
	{
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("IsLocalSettingsHost");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("IsRealMultiplayerContext");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HasHostSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HasClientSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HasHostSettingsActivation");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HasClientSettingsActivation");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HostSettingsEnabled");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ClientSettingsEnabled");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("MissionPresetEditable");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("IsMissionPresetSelected");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("CanEditHostSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("CanEditClientSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("CanToggleHostSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("CanToggleClientSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("CanChangePreset");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("CanResetSettings");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("PresetVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ClientSettingsActivationVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("HostReadOnlyNoticeVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ActionsScopeNoticeVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("ActionsScopeNoticeText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("AreSettingsEditable");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("IsMissionPresetActive");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_DirectLaunchNoticeVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_DirectLaunchNoticeText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_TrailSourceNoticeVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_TrailSourceNoticeText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetStatusText");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_PresetStatusVisibility");
		((LobbyModSettingsBaseViewModel)this).OnPropertyChanged("System_CanLoadSettingsSource");
	}

	private static string GetVanillaText(ManualLogSource log, string key, string fallback)
	{
		try
		{
			if (Translate.Instance.GameTexts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
			{
				return value;
			}
		}
		catch (Exception ex)
		{
			DebugLogHelper.LogWarning(log, "Could not read Vanilla preset text [" + key + "]: " + ex.Message);
		}
		DebugLogHelper.LogWarning(log, "Vanilla preset text [" + key + "] is unavailable; using [" + fallback + "].");
		return fallback;
	}
}

// Feature: Remember the last AIV/AIC selection for each AI lord.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class SkirmishAiSelectionMemoryHook : IDisposable
    {
        internal const int MaxStoredAivEntriesPerLord = 999;

        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string param);
        private delegate void AiSettingsButtonClickedDelegate(FRONT_Multiplayer_AISettings self, string param);
        private delegate void AiSettingsAddSelectedDelegate(FRONT_Multiplayer_AISettings self);

        private const long MaxStoreFileBytes = 256L * 1024L * 1024L;
        private const string BuiltInPrefix = "builtin:";
        private const string CustomPrefix = "custom:";
        private const string StoreFileName = "AiAivSelectionMemory.json";

        private static readonly FieldInfo AiSettingsAivInfoField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "AIVInfo");
        private static readonly MethodInfo MultiplayerUpdateHostInfoMethod =
            FindMethod(typeof(FRONT_Multiplayer), "UpdateHostInfo", typeof(bool));

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<string, string> storedSelections =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string storePath;

        private readonly Hook multiplayerButtonClickedHook;
        private readonly Hook skirmishAiAddClickHook;
        private readonly Hook aiSettingsButtonClickedHook;
        private readonly Hook aiSettingsAddSelectedHook;
        private readonly MultiplayerButtonClickedDelegate multiplayerButtonClickedTrampoline;
        private readonly MultiplayerButtonClickedDelegate skirmishAiAddClickTrampoline;
        private readonly AiSettingsButtonClickedDelegate aiSettingsButtonClickedTrampoline;
        private readonly AiSettingsAddSelectedDelegate aiSettingsAddSelectedTrampoline;
        private bool disposed;

        public SkirmishAiSelectionMemoryHook(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            storePath = Path.Combine(GetPluginDirectory(), "LobbyModSettings", StoreFileName);
            LoadStore();

            MethodInfo multiplayerButtonClickedMethod =
                FindMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string));
            MethodInfo skirmishAiAddClickMethod =
                FindMethod(typeof(FRONT_Multiplayer), "SkirmishAIAddClick", typeof(string));
            MethodInfo aiSettingsButtonClickedMethod =
                FindMethod(typeof(FRONT_Multiplayer_AISettings), "ButtonClicked", typeof(string));
            MethodInfo aiSettingsAddSelectedMethod =
                FindMethod(typeof(FRONT_Multiplayer_AISettings), "AddSelected");

            Hook multiplayerButtonClicked = null;
            Hook skirmishAiAddClick = null;
            Hook aiSettingsButtonClicked = null;
            Hook aiSettingsAddSelected = null;
            try
            {
                multiplayerButtonClicked =
                    new Hook(multiplayerButtonClickedMethod, (MultiplayerButtonClickedDelegate)MultiplayerButtonClickedHook);
                MultiplayerButtonClickedDelegate multiplayerButtonClickedOriginal =
                    multiplayerButtonClicked.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

                skirmishAiAddClick =
                    new Hook(skirmishAiAddClickMethod, (MultiplayerButtonClickedDelegate)SkirmishAiAddClickHook);
                MultiplayerButtonClickedDelegate skirmishAiAddClickOriginal =
                    skirmishAiAddClick.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

                aiSettingsButtonClicked =
                    new Hook(aiSettingsButtonClickedMethod, (AiSettingsButtonClickedDelegate)AiSettingsButtonClickedHook);
                AiSettingsButtonClickedDelegate aiSettingsButtonClickedOriginal =
                    aiSettingsButtonClicked.GenerateTrampoline<AiSettingsButtonClickedDelegate>();

                aiSettingsAddSelected =
                    new Hook(aiSettingsAddSelectedMethod, (AiSettingsAddSelectedDelegate)AiSettingsAddSelectedHook);
                AiSettingsAddSelectedDelegate aiSettingsAddSelectedOriginal =
                    aiSettingsAddSelected.GenerateTrampoline<AiSettingsAddSelectedDelegate>();

                multiplayerButtonClickedHook = multiplayerButtonClicked;
                multiplayerButtonClickedTrampoline = multiplayerButtonClickedOriginal;
                skirmishAiAddClickHook = skirmishAiAddClick;
                skirmishAiAddClickTrampoline = skirmishAiAddClickOriginal;
                aiSettingsButtonClickedHook = aiSettingsButtonClicked;
                aiSettingsButtonClickedTrampoline = aiSettingsButtonClickedOriginal;
                aiSettingsAddSelectedHook = aiSettingsAddSelected;
                aiSettingsAddSelectedTrampoline = aiSettingsAddSelectedOriginal;
            }
            catch
            {
                aiSettingsAddSelected?.Dispose();
                aiSettingsButtonClicked?.Dispose();
                skirmishAiAddClick?.Dispose();
                multiplayerButtonClicked?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"Bugfixes and QoL AI selection memory hooks installed. memoryEnabled={IsMemoryActive()}, " +
                    $"storePath={storePath}");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            multiplayerButtonClickedHook?.Undo();
            skirmishAiAddClickHook?.Undo();
            aiSettingsButtonClickedHook?.Undo();
            aiSettingsAddSelectedHook?.Undo();
            multiplayerButtonClickedHook?.Dispose();
            skirmishAiAddClickHook?.Dispose();
            aiSettingsButtonClickedHook?.Dispose();
            aiSettingsAddSelectedHook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL AI selection memory hooks disposed.");
        }

        private void MultiplayerButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            bool memoryActiveBefore = IsMemoryActive();
            bool mayAddAi = string.Equals(param, "AddCustomLord", StringComparison.Ordinal);
            Dictionary<int, string> before =
                memoryActiveBefore && mayAddAi ? CaptureAiSlotKeys(self) : null;

            if (memoryActiveBefore && string.Equals(param, "CancelAISettings", StringComparison.Ordinal))
                SaveActiveAiSettings();

            if (IsMemoryActive() && string.Equals(param, "Play", StringComparison.Ordinal))
                SaveAllAiSettings(self);

            multiplayerButtonClickedTrampoline(self, param);

            bool memoryActiveAfter = IsMemoryActive();
            if (!memoryActiveAfter || !mayAddAi)
                return;

            try
            {
                if (ApplyStoredSelectionsToNewAiSlots(self, before, "after ButtonClicked " + param))
                    UpdateHostInfo(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI selection apply after ButtonClicked({param}) failed: {ex}");
            }
        }

        private void SkirmishAiAddClickHook(FRONT_Multiplayer self, string param)
        {
            bool memoryActiveBefore = IsMemoryActive();
            Dictionary<int, string> before = memoryActiveBefore ? CaptureAiSlotKeys(self) : null;

            skirmishAiAddClickTrampoline(self, param);

            bool memoryActiveAfter = IsMemoryActive();
            if (!memoryActiveAfter)
                return;

            try
            {
                if (ApplyStoredSelectionsToNewAiSlots(self, before, "after SkirmishAIAddClick " + param))
                    UpdateHostInfo(self);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI selection apply after SkirmishAIAddClick({param}) failed: {ex}");
            }
        }

        private void AiSettingsButtonClickedHook(FRONT_Multiplayer_AISettings self, string param)
        {
            aiSettingsButtonClickedTrampoline(self, param);

            if (!IsMemoryActive() || !IsAiSettingsMutation(param))
                return;

            try
            {
                SaveAiSettings(GetAivInfo(self));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI selection save failed after ButtonClicked({param}): {ex}");
            }
        }

        private void AiSettingsAddSelectedHook(FRONT_Multiplayer_AISettings self)
        {
            aiSettingsAddSelectedTrampoline(self);
            SaveAiSettings(GetAivInfo(self));
        }

        private bool IsMemoryActive()
        {
            return settings.EnableMod && settings.RememberAiAivSettings;
        }

        private void SaveActiveAiSettings()
        {
            try
            {
                FRONT_Multiplayer_AISettings instance = FRONT_Multiplayer_AISettings.Instance;
                SaveAiSettings(GetAivInfo(instance));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL AI selection save failed while closing settings: {ex}");
            }
        }

        private void SaveAllAiSettings(FRONT_Multiplayer parent)
        {
            if (parent?.currentLobby?.members == null || parent.AIVs == null)
                return;

            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (!TryGetAiSlotInfo(parent, member, out int playerId, out _))
                    continue;
                if (playerId < 1 || playerId > parent.AIVs.Length)
                    continue;

                SaveAiSettings(parent.AIVs[playerId - 1]);
            }
        }

        private void SaveAiSettings(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (!IsMemoryActive() || info == null)
                return;

            string key = BuildLordKey(info);
            if (string.IsNullOrEmpty(key))
                return;

            string encoded = AiAivSelectionCodec.Encode(info);
            bool hadExisting = storedSelections.TryGetValue(key, out string existing);
            if (hadExisting && string.Equals(existing, encoded, StringComparison.Ordinal))
                return;

            storedSelections[key] = encoded;
            SaveStore();
            Shared.DebugLogHelper.LogDebug(
                log,
                () =>
                    $"Bugfixes and QoL saved AI AIV/AIC selection: key={key}, hadExisting={hadExisting}, " +
                    $"{BuildInfoSummary(info)}, encodedLength={encoded.Length}.");
        }

        private bool ApplyStoredSelectionsToNewAiSlots(
            FRONT_Multiplayer parent,
            Dictionary<int, string> before,
            string reason)
        {
            if (storedSelections.Count == 0 ||
                parent?.AIVs == null ||
                parent.currentLobby?.members == null ||
                before == null)
            {
                return false;
            }

            bool applied = false;
            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (!TryGetAiSlotInfo(parent, member, out int playerId, out string key))
                    continue;
                if (playerId < 1 || playerId > parent.AIVs.Length)
                    continue;
                if (before.TryGetValue(playerId, out string previousKey) &&
                    string.Equals(previousKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!storedSelections.TryGetValue(key, out string encoded))
                    continue;
                if (!AiAivSelectionCodec.TryDecode(
                        encoded,
                        out FRONT_Multiplayer.MPAIVInfo decoded,
                        out string decodeError))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL ignored invalid stored AI selection for {key}: {decodeError}");
                    continue;
                }
                if (!string.Equals(BuildLordKey(decoded), key, StringComparison.OrdinalIgnoreCase))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL ignored stored AI selection for {key}: decoded lord is {BuildLordKey(decoded)}.");
                    continue;
                }

                AiAivSelectionCodec.CopyInto(decoded, parent.AIVs[playerId - 1]);
                applied = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"Bugfixes and QoL restored shared AI AIV/AIC selection: reason={reason}, key={key}, " +
                        $"player={playerId}, {BuildInfoSummary(decoded)}.");
            }

            return applied;
        }

        private Dictionary<int, string> CaptureAiSlotKeys(FRONT_Multiplayer parent)
        {
            Dictionary<int, string> result = new Dictionary<int, string>();
            if (parent?.currentLobby?.members == null)
                return result;

            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (TryGetAiSlotInfo(parent, member, out int playerId, out string key))
                    result[playerId] = key;
            }

            return result;
        }

        private static bool IsAiSettingsMutation(string param)
        {
            if (string.IsNullOrEmpty(param))
                return false;
            if (param.StartsWith("AIVPlacementLobby_", StringComparison.Ordinal))
                return true;
            if (param.StartsWith("Kick_", StringComparison.Ordinal))
                return true;

            switch (param)
            {
                case "Default":
                case "Community":
                case "Historical":
                case "NoRot":
                case "North":
                case "East":
                case "South":
                case "West":
                case "User":
                case "Clear_Selected":
                case "LordDefault":
                case "LordUser":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetAiSlotInfo(
            FRONT_Multiplayer parent,
            Platform_Multiplayer.MPLobbyMember member,
            out int playerId,
            out string lordKey)
        {
            playerId = 0;
            lordKey = string.Empty;
            if (parent?.currentLobby == null ||
                member == null ||
                !member.SkirmishMember ||
                member.SkirmishHumanMember)
            {
                return false;
            }

            playerId = parent.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
            if (playerId < 1 || playerId > 8)
                return false;

            lordKey = !string.IsNullOrEmpty(member.customLordName)
                ? CustomPrefix + member.customLordName
                : BuiltInPrefix + member.GetLordType();
            return true;
        }

        private static FRONT_Multiplayer.MPAIVInfo GetAivInfo(FRONT_Multiplayer_AISettings instance)
        {
            return instance == null
                ? null
                : AiSettingsAivInfoField.GetValue(instance) as FRONT_Multiplayer.MPAIVInfo;
        }

        private static void UpdateHostInfo(FRONT_Multiplayer parent)
        {
            if (parent != null)
                MultiplayerUpdateHostInfoMethod.Invoke(parent, new object[] { false });
        }

        private static string BuildLordKey(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return string.Empty;

            return !string.IsNullOrEmpty(info.lordName)
                ? CustomPrefix + info.lordName
                : BuiltInPrefix + info.lordType;
        }

        private static string BuildInfoSummary(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return "info=null";

            string mode =
                info.builtIn ? "default" :
                info.community ? "community" :
                info.historical ? "historical" :
                "custom";
            string lordMode = info.builtInLord ? "defaultLord" : "customLordConfig";
            string firstAivName =
                info.aivs == null || info.aivs.Count == 0 ? string.Empty : info.aivs[0].AIVName;

            return
                $"lordType={info.lordType}, lordName={info.lordName ?? string.Empty}, mode={mode}, " +
                $"rotation={info.rotation}, aivCount={info.aivs?.Count ?? 0}, firstAiv={firstAivName}, " +
                $"lordMode={lordMode}, lordConfig={info.lordConfig?.name ?? string.Empty}";
        }

        private void LoadStore()
        {
            storedSelections.Clear();
            if (!File.Exists(storePath))
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    () => $"Bugfixes and QoL shared AI AIV/AIC selection file not found: {storePath}");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(storePath);
                if (fileInfo.Length > MaxStoreFileBytes)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL ignored oversized AI selection store: path={storePath}, " +
                        $"size={fileInfo.Length}, maximum={MaxStoreFileBytes}.");
                    return;
                }

                Dictionary<string, string> parsed = ParseJsonObject(File.ReadAllText(storePath));
                foreach (KeyValuePair<string, string> entry in parsed)
                {
                    if (!entry.Value.StartsWith("v2:", StringComparison.Ordinal))
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Bugfixes and QoL ignored unsupported AI selection entry: key={entry.Key}.");
                        continue;
                    }

                    storedSelections[entry.Key] = entry.Value;
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    () =>
                        $"Bugfixes and QoL loaded {storedSelections.Count} shared AI AIV/AIC selections from {storePath}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL could not load AI selection store from {storePath}: {ex.Message}");
            }
        }

        private void SaveStore()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(storePath));
                File.WriteAllText(
                    storePath,
                    SerializeJsonObject(storedSelections),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not save AI selection store to {storePath}: {ex}");
            }
        }

        private static string GetPluginDirectory()
        {
            try
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                    return Path.GetDirectoryName(assemblyLocation);
            }
            catch
            {
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static MethodInfo FindMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);

            return method;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(type.FullName, fieldName);

            return field;
        }

        private static string SerializeJsonObject(Dictionary<string, string> values)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            bool first = true;
            foreach (KeyValuePair<string, string> entry in values)
            {
                if (!first)
                    builder.AppendLine(",");

                first = false;
                builder.Append("  \"");
                AppendEscapedJsonString(builder, entry.Key);
                builder.Append("\": \"");
                AppendEscapedJsonString(builder, entry.Value);
                builder.Append('"');
            }

            if (!first)
                builder.AppendLine();

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseJsonObject(string json)
        {
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            SkipWhitespace(json, ref index);
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);
            if (TryConsume(json, ref index, '}'))
                return result;

            while (index < json.Length)
            {
                string key = ReadJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                Expect(json, ref index, ':');
                SkipWhitespace(json, ref index);
                result[key] = ReadJsonString(json, ref index);
                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                    return result;

                Expect(json, ref index, ',');
                SkipWhitespace(json, ref index);
            }

            throw new FormatException("Unterminated JSON object.");
        }

        private static void AppendEscapedJsonString(StringBuilder builder, string value)
        {
            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 32)
                            builder.Append("\\u" + ((int)c).ToString("x4"));
                        else
                            builder.Append(c);
                        break;
                }
            }
        }

        private static string ReadJsonString(string text, ref int index)
        {
            Expect(text, ref index, '"');
            StringBuilder builder = new StringBuilder();
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"')
                    return builder.ToString();
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }
                if (index >= text.Length)
                    throw new FormatException("Unterminated JSON escape.");

                char escaped = text[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length)
                            throw new FormatException("Invalid JSON unicode escape.");
                        builder.Append((char)Convert.ToInt32(text.Substring(index, 4), 16));
                        index += 4;
                        break;
                    default:
                        throw new FormatException($"Invalid JSON escape '\\{escaped}'.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static bool TryConsume(string text, ref int index, char expected)
        {
            if (index >= text.Length || text[index] != expected)
                return false;

            index++;
            return true;
        }

        private static void Expect(string text, ref int index, char expected)
        {
            if (index >= text.Length || text[index] != expected)
                throw new FormatException($"Expected '{expected}' at position {index}.");

            index++;
        }

    }
}

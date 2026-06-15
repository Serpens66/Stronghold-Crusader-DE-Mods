using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SomeSettings
{
    internal sealed class SkirmishAiSelectionMemoryHook : IDisposable
    {
        private delegate void MultiplayerButtonClickedDelegate(FRONT_Multiplayer self, string param);
        private delegate void AiSettingsButtonClickedDelegate(FRONT_Multiplayer_AISettings self, string param);
        private delegate void AiSettingsAddSelectedDelegate(FRONT_Multiplayer_AISettings self);

        private const string BuiltInPrefix = "builtin:";
        private const string CustomPrefix = "custom:";
        private const string StoreFileName = "AiAivSelectionMemory.json";
        private static readonly FieldInfo AiSettingsAivInfoField = FindField(typeof(FRONT_Multiplayer_AISettings), "AIVInfo");
        private static readonly MethodInfo MultiplayerUpdateHostInfoMethod = FindMethod(typeof(FRONT_Multiplayer), "UpdateHostInfo", typeof(bool));

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Dictionary<string, string> storedSelections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        public SkirmishAiSelectionMemoryHook(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            storePath = Path.Combine(GetPluginDirectory(), StoreFileName);
            LoadStore();

            multiplayerButtonClickedHook = new Hook(FindMethod(typeof(FRONT_Multiplayer), "ButtonClicked", typeof(string)), (MultiplayerButtonClickedDelegate)MultiplayerButtonClickedHook);
            multiplayerButtonClickedTrampoline = multiplayerButtonClickedHook.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

            skirmishAiAddClickHook = new Hook(FindMethod(typeof(FRONT_Multiplayer), "SkirmishAIAddClick", typeof(string)), (MultiplayerButtonClickedDelegate)SkirmishAiAddClickHook);
            skirmishAiAddClickTrampoline = skirmishAiAddClickHook.GenerateTrampoline<MultiplayerButtonClickedDelegate>();

            aiSettingsButtonClickedHook = new Hook(FindMethod(typeof(FRONT_Multiplayer_AISettings), "ButtonClicked", typeof(string)), (AiSettingsButtonClickedDelegate)AiSettingsButtonClickedHook);
            aiSettingsButtonClickedTrampoline = aiSettingsButtonClickedHook.GenerateTrampoline<AiSettingsButtonClickedDelegate>();

            aiSettingsAddSelectedHook = new Hook(FindMethod(typeof(FRONT_Multiplayer_AISettings), "AddSelected"), (AiSettingsAddSelectedDelegate)AiSettingsAddSelectedHook);
            aiSettingsAddSelectedTrampoline = aiSettingsAddSelectedHook.GenerateTrampoline<AiSettingsAddSelectedDelegate>();

            log.LogDebug($"SomeSettings AI selection memory hooks installed. enabled={settings.EnableMod && settings.RememberAiAivSettings}, storePath={storePath}");
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
            log.LogDebug("SomeSettings AI selection memory hooks disposed.");
        }

        private void MultiplayerButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            bool featureActiveBefore = IsFeatureActive();
            bool relevant = IsRelevantMultiplayerButton(param);
            bool mayAddAi = string.Equals(param, "AddCustomLord", StringComparison.Ordinal);
            Dictionary<int, string> before = featureActiveBefore && (relevant || mayAddAi) ? CaptureAiSlotKeys(self) : null;
            if (relevant)
                log.LogInfo($"SomeSettings AI selection memory ButtonClicked before original: param={param}, featureActive={featureActiveBefore}, beforeSlots={FormatSlotKeys(before)}, storedCount={storedSelections.Count}");

            if (featureActiveBefore && string.Equals(param, "CancelAISettings", StringComparison.Ordinal))
                SaveActiveAiSettings();

            multiplayerButtonClickedTrampoline(self, param);

            bool featureActiveAfter = IsFeatureActive();
            Dictionary<int, string> after = featureActiveAfter && (relevant || mayAddAi) ? CaptureAiSlotKeys(self) : null;
            if (relevant)
                log.LogInfo($"SomeSettings AI selection memory ButtonClicked after original: param={param}, featureActive={featureActiveAfter}, afterSlots={FormatSlotKeys(after)}, storedCount={storedSelections.Count}");

            if (!featureActiveAfter || !mayAddAi)
                return;

            try
            {
                if (ApplyStoredSelectionsToNewAiSlots(self, before, "after ButtonClicked " + param))
                    UpdateHostInfo(self);
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings AI selection memory apply after ButtonClicked({param}) failed: {ex}");
            }
        }

        private void SkirmishAiAddClickHook(FRONT_Multiplayer self, string param)
        {
            bool featureActiveBefore = IsFeatureActive();
            Dictionary<int, string> before = featureActiveBefore ? CaptureAiSlotKeys(self) : null;
            log.LogInfo($"SomeSettings AI selection memory SkirmishAIAddClick before original: param={param}, featureActive={featureActiveBefore}, beforeSlots={FormatSlotKeys(before)}, storedCount={storedSelections.Count}");

            skirmishAiAddClickTrampoline(self, param);

            bool featureActiveAfter = IsFeatureActive();
            Dictionary<int, string> after = featureActiveAfter ? CaptureAiSlotKeys(self) : null;
            log.LogInfo($"SomeSettings AI selection memory SkirmishAIAddClick after original: param={param}, featureActive={featureActiveAfter}, afterSlots={FormatSlotKeys(after)}, storedCount={storedSelections.Count}");

            if (!featureActiveAfter)
                return;

            try
            {
                if (ApplyStoredSelectionsToNewAiSlots(self, before, "after SkirmishAIAddClick " + param))
                    UpdateHostInfo(self);
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings AI selection memory apply after SkirmishAIAddClick({param}) failed: {ex}");
            }
        }

        private void AiSettingsButtonClickedHook(FRONT_Multiplayer_AISettings self, string param)
        {
            aiSettingsButtonClickedTrampoline(self, param);

            if (!IsFeatureActive())
                return;

            try
            {
                SaveAiSettings(GetAivInfo(self));
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings AI selection memory save failed after ButtonClicked({param}): {ex}");
            }
        }

        private void AiSettingsAddSelectedHook(FRONT_Multiplayer_AISettings self)
        {
            aiSettingsAddSelectedTrampoline(self);

            if (!IsFeatureActive())
                return;

            try
            {
                SaveAiSettings(GetAivInfo(self));
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings AI selection memory save failed after AddSelected: {ex}");
            }
        }

        private bool IsFeatureActive()
        {
            return settings.EnableMod && settings.RememberAiAivSettings;
        }

        private static bool IsRelevantMultiplayerButton(string param)
        {
            if (string.IsNullOrEmpty(param))
                return false;

            if (param.StartsWith("AISettings_", StringComparison.Ordinal))
                return true;

            switch (param)
            {
                case "ShowAI":
                case "Back":
                case "CancelAISettings":
                case "AddCustomLord":
                case "CloseCustomLord":
                    return true;
                default:
                    return false;
            }
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
                log.LogError($"SomeSettings AI selection memory save failed while closing settings: {ex}");
            }
        }

        private void SaveAiSettings(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return;

            string key = BuildLordKey(info);
            if (string.IsNullOrEmpty(key))
                return;

            string encoded = info.encode();
            if (string.IsNullOrEmpty(encoded))
                return;

            bool hadExisting = storedSelections.TryGetValue(key, out string existing);
            if (hadExisting && existing == encoded)
                return;

            log.LogDebug($"SomeSettings detected changed AI AIV/lord selection: key={key}, hadExisting={hadExisting}, {BuildInfoSummary(info)}");
            storedSelections[key] = encoded;
            SaveStore();
            log.LogDebug($"SomeSettings saved AI AIV/lord selection: key={key}, encodedLength={encoded.Length}");
        }

        private bool ApplyStoredSelectionsToNewAiSlots(FRONT_Multiplayer parent, Dictionary<int, string> before, string reason)
        {
            if (storedSelections.Count == 0 || parent == null || parent.AIVs == null || parent.currentLobby == null || before == null)
            {
                log.LogInfo($"SomeSettings AI selection memory apply skipped before scan: reason={reason}, storedCount={storedSelections.Count}, hasParent={parent != null}, hasAIVs={parent?.AIVs != null}, hasLobby={parent?.currentLobby != null}, hasBefore={before != null}");
                return false;
            }

            log.LogInfo($"SomeSettings AI selection memory apply scan started: reason={reason}, beforeSlots={FormatSlotKeys(before)}, storedCount={storedSelections.Count}, lobbyMemberCount={parent.currentLobby.members.Count}");
            bool applied = false;
            foreach (Platform_Multiplayer.MPLobbyMember member in parent.currentLobby.members)
            {
                if (!TryGetAiSlotInfo(parent, member, out int playerId, out string key))
                {
                    log.LogInfo($"SomeSettings AI selection memory apply skipped lobby member: reason={reason}, skirmish={member?.SkirmishMember}, human={member?.SkirmishHumanMember}, steamId={member?.GetSteamID()}");
                    continue;
                }

                if (playerId < 1 || playerId > parent.AIVs.Length)
                {
                    log.LogInfo($"SomeSettings AI selection memory apply skipped invalid player id: reason={reason}, key={key}, lobbyPlayer={playerId}, aivLength={parent.AIVs.Length}");
                    continue;
                }

                if (before.TryGetValue(playerId, out string previousKey) && string.Equals(previousKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogInfo($"SomeSettings AI selection memory apply skipped existing unchanged slot: reason={reason}, key={key}, lobbyPlayer={playerId}, previousKey={previousKey}");
                    continue;
                }

                if (!storedSelections.TryGetValue(key, out string encoded))
                {
                    log.LogInfo($"SomeSettings AI selection memory apply skipped missing stored selection: reason={reason}, key={key}, lobbyPlayer={playerId}, previousKey={(before.TryGetValue(playerId, out previousKey) ? previousKey : "<new-slot>")}");
                    continue;
                }

                string currentEncoded = parent.AIVs[playerId - 1].encode();
                if (currentEncoded == encoded)
                {
                    log.LogInfo($"SomeSettings AI selection memory apply skipped already matching encoded selection: reason={reason}, key={key}, lobbyPlayer={playerId}, encodedLength={(encoded == null ? 0 : encoded.Length)}");
                    continue;
                }

                FRONT_Multiplayer.MPAIVInfo decoded = new FRONT_Multiplayer.MPAIVInfo();
                decoded.decode(encoded);
                if (!string.Equals(BuildLordKey(decoded), key, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogWarning($"SomeSettings ignored stored AI AIV selection for {key}: decoded data no longer matches this lord. decodedLordKey={BuildLordKey(decoded)}");
                    continue;
                }

                parent.AIVs[playerId - 1].decode(encoded);
                applied = true;
                log.LogInfo($"SomeSettings loaded/applied remembered AI AIV/lord selection: reason={reason}, key={key}, lobbyPlayer={playerId}, previousKey={(before.TryGetValue(playerId, out previousKey) ? previousKey : "<new-slot>")}, previousEncodedLength={(currentEncoded == null ? 0 : currentEncoded.Length)}, {BuildInfoSummary(decoded)}");
            }

            log.LogInfo($"SomeSettings AI selection memory apply scan finished: reason={reason}, applied={applied}");
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

        private static string FormatSlotKeys(Dictionary<int, string> slotKeys)
        {
            if (slotKeys == null)
                return "<null>";

            if (slotKeys.Count == 0)
                return "<empty>";

            StringBuilder builder = new StringBuilder();
            bool first = true;
            foreach (KeyValuePair<int, string> slot in slotKeys)
            {
                if (!first)
                    builder.Append(", ");

                first = false;
                builder.Append(slot.Key);
                builder.Append('=');
                builder.Append(slot.Value);
            }

            return builder.ToString();
        }

        private static bool TryGetAiSlotInfo(FRONT_Multiplayer parent, Platform_Multiplayer.MPLobbyMember member, out int playerId, out string lordKey)
        {
            playerId = 0;
            lordKey = string.Empty;

            if (parent == null || member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                return false;

            playerId = parent.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
            if (playerId < 1 || playerId > 8)
                return false;

            if (!string.IsNullOrEmpty(member.customLordName))
                lordKey = CustomPrefix + member.customLordName;
            else
                lordKey = BuiltInPrefix + member.GetLordType();

            return true;
        }

        private static FRONT_Multiplayer.MPAIVInfo GetAivInfo(FRONT_Multiplayer_AISettings instance)
        {
            if (instance == null)
                return null;

            return AiSettingsAivInfoField.GetValue(instance) as FRONT_Multiplayer.MPAIVInfo;
        }

        private static void UpdateHostInfo(FRONT_Multiplayer parent)
        {
            if (parent == null)
                return;

            MultiplayerUpdateHostInfoMethod.Invoke(parent, new object[] { false });
        }

        private static string BuildLordKey(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(info.lordName))
                return CustomPrefix + info.lordName;

            return BuiltInPrefix + info.lordType;
        }

        private static string BuildInfoSummary(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return "info=null";

            string mode = info.builtIn ? "default" : info.community ? "community" : info.historical ? "historical" : "custom";
            string lordMode = info.builtInLord ? "defaultLord" : "customLordConfig";
            string lordName = string.IsNullOrEmpty(info.lordName) ? "" : info.lordName;
            string lordConfigName = info.lordConfig == null ? "" : info.lordConfig.name;
            string firstAivName = info.aivs == null || info.aivs.Count == 0 ? "" : info.aivs[0].AIVName;

            return $"lordType={info.lordType}, lordName={lordName}, mode={mode}, rotation={info.rotation}, aivCount={(info.aivs == null ? 0 : info.aivs.Count)}, firstAiv={firstAivName}, lordMode={lordMode}, lordConfig={lordConfigName}";
        }

        private void LoadStore()
        {
            storedSelections.Clear();
            if (!File.Exists(storePath))
            {
                log.LogDebug($"SomeSettings AI AIV/lord selection memory file not found: {storePath}");
                return;
            }

            try
            {
                foreach (KeyValuePair<string, string> entry in ParseJsonObject(File.ReadAllText(storePath)))
                {
                    storedSelections[entry.Key] = entry.Value;
                    log.LogDebug($"SomeSettings loaded remembered AI AIV/lord selection from disk: key={entry.Key}, encodedLength={entry.Value.Length}");
                }

                log.LogDebug($"SomeSettings loaded {storedSelections.Count} remembered AI AIV/lord selections from {storePath}.");
            }
            catch (Exception ex)
            {
                log.LogWarning($"SomeSettings could not load AI AIV selection memory from {storePath}: {ex.Message}");
            }
        }

        private void SaveStore()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(storePath));
                File.WriteAllText(storePath, SerializeJsonObject(storedSelections));
            }
            catch (Exception ex)
            {
                log.LogError($"SomeSettings could not save AI AIV selection memory to {storePath}: {ex}");
            }
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
                builder.Append(EscapeJsonString(entry.Key));
                builder.Append("\": \"");
                builder.Append(EscapeJsonString(entry.Value));
                builder.Append("\"");
            }

            if (!first)
                builder.AppendLine();

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static Dictionary<string, string> ParseJsonObject(string json)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                string value = ReadJsonString(json, ref index);
                result[key] = value;
                SkipWhitespace(json, ref index);

                if (TryConsume(json, ref index, '}'))
                    return result;

                Expect(json, ref index, ',');
                SkipWhitespace(json, ref index);
            }

            throw new FormatException("Unterminated JSON object.");
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static void Expect(string text, ref int index, char expected)
        {
            if (!TryConsume(text, ref index, expected))
                throw new FormatException($"Expected '{expected}' at offset {index}.");
        }

        private static bool TryConsume(string text, ref int index, char expected)
        {
            if (index >= text.Length || text[index] != expected)
                return false;

            index++;
            return true;
        }

        private static string ReadJsonString(string text, ref int index)
        {
            Expect(text, ref index, '"');
            StringBuilder builder = new StringBuilder();
            while (index < text.Length)
            {
                char value = text[index++];
                if (value == '"')
                    return builder.ToString();

                if (value != '\\')
                {
                    builder.Append(value);
                    continue;
                }

                if (index >= text.Length)
                    throw new FormatException("Unterminated JSON escape.");

                char escaped = text[index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    default:
                        throw new FormatException($"Unsupported JSON escape '\\{escaped}'.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}

// Feature: Persist the remembered AI AIV/AIC selection per lord.
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class AiSelectionMemoryStore
    {
        // The store normally stays tiny; reject implausibly large files before building a JSON DOM.
        private const long MaxStoreFileBytes = 16L * 1024L * 1024L;
        private const string StoreFileName = "AiAivSelectionMemory.json";

        private readonly ManualLogSource log;
        private readonly Dictionary<string, string> storedSelections =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string storePath;
        // Set when an existing store could not be read. The next save keeps a backup of the
        // unreadable file instead of silently replacing every remembered selection.
        private bool storeUnreadable;

        internal AiSelectionMemoryStore(ManualLogSource log)
        {
            this.log = log;
            storePath = Path.Combine(AivAicPresetStore.GetPluginDirectory(), "LobbyModSettings", StoreFileName);
            LoadStore();
        }

        internal string StorePath => storePath;

        internal bool IsEmpty => storedSelections.Count == 0;

        internal bool TryGetEncoded(string lordKey, out string encoded)
        {
            return storedSelections.TryGetValue(lordKey, out encoded);
        }

        // False means this exact selection is already stored and the file was not touched.
        internal bool Remember(string lordKey, string encoded, out bool hadExisting)
        {
            hadExisting = storedSelections.TryGetValue(lordKey, out string existing);
            if (hadExisting && string.Equals(existing, encoded, StringComparison.Ordinal))
                return false;

            storedSelections[lordKey] = encoded;
            SaveStore();
            return true;
        }

        private void LoadStore()
        {
            storedSelections.Clear();
            storeUnreadable = false;
            if (!File.Exists(storePath))
            {
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.shared-ai-aiv-aic", () => $"Bugfixes and QoL shared AI AIV/AIC selection file not found: {storePath}");
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(storePath);
                if (fileInfo.Length > MaxStoreFileBytes)
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.ignored-oversized-ai-selection", $"Bugfixes and QoL ignored oversized AI selection store: path={storePath}, " +
                        $"size={fileInfo.Length}, maximum={MaxStoreFileBytes}.");
                    storeUnreadable = true;
                    return;
                }

                Dictionary<string, string> parsed = ParseJsonObject(File.ReadAllText(storePath));
                bool migratedStore = false;
                foreach (KeyValuePair<string, string> entry in parsed)
                {
                    if (!AiAivSelectionCodec.TryDecode(
                            entry.Value,
                            out AiAivSelectionSnapshot snapshot,
                            out bool legacyPayload,
                            out string decodeError))
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.ignored-invalid-ai-selection", $"Bugfixes and QoL ignored invalid AI selection entry for {entry.Key}: {decodeError}");
                        continue;
                    }
                    string decodedKey = AiAivSelectionCodec.BuildLordKey(snapshot);
                    if (!string.Equals(decodedKey, entry.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.ignored-stored-ai-selection", $"Bugfixes and QoL ignored stored AI selection for {entry.Key}: decoded lord is {decodedKey}.");
                        continue;
                    }

                    storedSelections[entry.Key] = legacyPayload
                        ? AiAivSelectionCodec.Encode(snapshot)
                        : entry.Value;
                    migratedStore |= legacyPayload;
                }

                if (migratedStore)
                {
                    SaveStore();
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.migrated-ai-aiv-aic", () => "Bugfixes and QoL migrated AI AIV/AIC selection memory to file references.");
                }

                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.loaded-shared-ai-aiv", () =>
                            $"Bugfixes and QoL loaded {storedSelections.Count} shared AI AIV/AIC selections from {storePath}.");
            }
            catch (Exception ex)
            {
                storedSelections.Clear();
                storeUnreadable = true;
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/LoadStore.could-not-load-ai", $"Bugfixes and QoL could not load AI selection store from {storePath}: {ex.Message}");
            }
        }

        private void SaveStore()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(storePath));
                if (storeUnreadable && File.Exists(storePath))
                {
                    string backupPath = storePath + ".unreadable-" +
                        DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + ".bak";
                    File.Copy(storePath, backupPath, overwrite: false);
                    Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/SaveStore.kept-unreadable-ai-selection", $"Bugfixes and QoL kept the unreadable AI selection store as {backupPath} before writing a new one.");
                }
                storeUnreadable = false;
                // Write a complete temporary file first, so a crash during saving cannot
                // leave a truncated store behind.
                string temporaryPath = storePath + ".tmp";
                File.WriteAllText(
                    temporaryPath,
                    SerializeJsonObject(storedSelections),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(storePath))
                    File.Replace(temporaryPath, storePath, null);
                else
                    File.Move(temporaryPath, storePath);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/SkirmishAiSelectionMemoryHook/SaveStore.could-not-save-ai", $"Bugfixes and QoL could not save AI selection store to {storePath}: {ex}");
            }
        }

        private static string SerializeJsonObject(Dictionary<string, string> values)
        {
            var jsonValues = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in values)
                jsonValues.Add(entry.Key, entry.Value);
            return Shared.DependencyFreeJson.Serialize(jsonValues);
        }

        private static Dictionary<string, string> ParseJsonObject(string json)
        {
            if (!(Shared.DependencyFreeJson.Parse(json) is Dictionary<string, object> values))
                throw new InvalidDataException("AI selection store root must be a JSON object.");

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> entry in values)
            {
                if (!(entry.Value is string text))
                    throw new InvalidDataException("AI selection store value for '" + entry.Key + "' must be a string.");
                result[entry.Key] = text;
            }
            return result;
        }
    }
}

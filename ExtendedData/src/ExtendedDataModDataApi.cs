using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtendedData
{
    public static class ExtendedDataModDataApi
    {
        private const string MapEntryName = "modmap.json";
        private const string LordExtension = ".lordjson";
        private const string ModLordExtension = ".modlord.json";
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static LordDataSnapshot activeSnapshot;
        private static bool networkSessionActive;
        private static IReadOnlyDictionary<int, string> verifiedLocalLordPaths;

        public static int ApiVersion => 1;

        internal static void SetNetworkSnapshot(LordDataSnapshot snapshot, bool sessionActive)
        {
            activeSnapshot = snapshot;
            networkSessionActive = sessionActive;
            verifiedLocalLordPaths = null;
        }

        internal static void SetVerifiedLocalLords(IReadOnlyDictionary<int, string> paths)
        {
            activeSnapshot = null;
            networkSessionActive = paths != null;
            verifiedLocalLordPaths = paths;
        }

        public static ExtendedDataModDataReadResult ReadSelectedLordNamespace(int playerId, string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid) || playerId < 1 || playerId > 8)
                return ExtendedDataModDataReadResult.InvalidRequest(modGuid, string.Empty, "A mod GUID and player ID from 1 to 8 are required.");
            if (verifiedLocalLordPaths != null &&
                verifiedLocalLordPaths.TryGetValue(playerId, out string localPath))
                return ReadLordNamespace(Path.ChangeExtension(localPath, LordExtension), modGuid);
            if (!networkSessionActive || activeSnapshot == null)
                return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, string.Empty, "The host Lord-data snapshot is not ready.");
            LordDataSlot slot = activeSnapshot.GetSlot(playerId);
            if (slot == null)
                return ExtendedDataModDataReadResult.FileNotFound(modGuid, "host Lord slot " + playerId);
            return ReadSelectedSlot(slot, modGuid);
        }

        public static ExtendedDataModDataReadResult ReadCurrentMapNamespace(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid))
            {
                return ExtendedDataModDataReadResult.InvalidRequest(
                    modGuid,
                    MapEntryName,
                    "A non-empty mod GUID is required.");
            }

            string currentPath = null;
            string source = MapEntryName;
            try
            {
                GameMapArchiveManagerAPI manager = GameMapArchiveManagerAPI.Instance;
                currentPath = manager.GetCurrentFilePath();
                if (!string.IsNullOrWhiteSpace(currentPath))
                    source = currentPath + "::" + MapEntryName;

                byte[] bytes = manager.TryReadBinaryFile(MapEntryName, ignoreCase: true);
                if (bytes == null)
                    return ExtendedDataModDataReadResult.FileNotFound(modGuid, source);
                return ReadUtf8(bytes, modGuid, source);
            }
            catch (Exception exception)
            {
                return ExtendedDataModDataReadResult.ReadError(
                    modGuid,
                    source,
                    "Could not read modmap.json from the current map archive: " + exception.Message);
            }
        }

        public static ExtendedDataModDataReadResult ReadLordNamespace(string lordJsonPath, string modGuid)
        {
            if (verifiedLocalLordPaths != null)
            {
                string requested = Path.GetFileNameWithoutExtension(lordJsonPath ?? string.Empty);
                string[] matchingPaths = verifiedLocalLordPaths.Values.Where(path =>
                    string.Equals(Path.GetFileNameWithoutExtension(path), requested,
                        StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (matchingPaths.Length != 1)
                    return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, lordJsonPath,
                        "The verified local Lord configuration is missing or ambiguous; use the player-ID API.");
                lordJsonPath = matchingPaths[0];
            }
            if (networkSessionActive && verifiedLocalLordPaths == null)
            {
                if (activeSnapshot == null)
                    return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, lordJsonPath, "The host Lord-data snapshot is not ready.");
                string configName = Path.GetFileNameWithoutExtension(lordJsonPath ?? string.Empty);
                LordDataSlot[] matches = activeSnapshot.Slots.Where(slot =>
                    string.Equals(slot.ConfigName, configName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length == 1)
                    return ReadSelectedSlot(matches[0], modGuid);
                if (matches.Length > 1)
                    return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, lordJsonPath, "The selected Lord configuration is ambiguous; use the player-ID API.");
                return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, lordJsonPath, "The file is not a selected host Lord configuration.");
            }
            if (string.IsNullOrWhiteSpace(modGuid))
            {
                return ExtendedDataModDataReadResult.InvalidRequest(
                    modGuid,
                    lordJsonPath,
                    "A non-empty mod GUID is required.");
            }
            if (string.IsNullOrWhiteSpace(lordJsonPath))
            {
                return ExtendedDataModDataReadResult.InvalidRequest(
                    modGuid,
                    lordJsonPath,
                    "A .lordjson path is required.");
            }
            if (!lordJsonPath.EndsWith(LordExtension, StringComparison.OrdinalIgnoreCase))
            {
                return ExtendedDataModDataReadResult.InvalidRequest(
                    modGuid,
                    lordJsonPath,
                    "The supplied path must end in .lordjson.");
            }

            string source = lordJsonPath.Substring(0, lordJsonPath.Length - LordExtension.Length) + ModLordExtension;
            try
            {
                if (!File.Exists(source))
                    return ExtendedDataModDataReadResult.FileNotFound(modGuid, source);
                return ReadUtf8(File.ReadAllBytes(source), modGuid, source);
            }
            catch (Exception exception)
            {
                return ExtendedDataModDataReadResult.ReadError(
                    modGuid,
                    source,
                    "Could not read the Custom Lord mod-data sidecar: " + exception.Message);
            }
        }

        public static ExtendedDataModDataReadResult ReadLordNamespace(
            CustomisationFileManager.CustomLordConfig config,
            string modGuid)
        {
            if (verifiedLocalLordPaths != null)
                return ReadLordNamespace(config?.name + LordExtension, modGuid);
            if (networkSessionActive && verifiedLocalLordPaths == null)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.name) || activeSnapshot == null)
                    return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, string.Empty, "The selected host Lord configuration is not ready.");
                LordDataSlot[] matches = activeSnapshot.Slots.Where(slot =>
                    string.Equals(slot.ConfigName, config.name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(slot.ConfigChecksum, config.checksum.ToString(), StringComparison.Ordinal)).ToArray();
                if (matches.Length == 1)
                    return ReadSelectedSlot(matches[0], modGuid);
                return ExtendedDataModDataReadResult.HostDataUnavailable(modGuid, config.name, "The selected host Lord configuration is missing or ambiguous; use the player-ID API.");
            }
            if (config == null || string.IsNullOrWhiteSpace(config.path) || string.IsNullOrWhiteSpace(config.name))
            {
                return ExtendedDataModDataReadResult.InvalidRequest(
                    modGuid,
                    string.Empty,
                    "A CustomLordConfig with a path and name is required.");
            }
            return ReadLordNamespace(Path.Combine(config.path, config.name + LordExtension), modGuid);
        }

        private static ExtendedDataModDataReadResult ReadUtf8(byte[] bytes, string modGuid, string source)
        {
            try
            {
                string json = StrictUtf8.GetString(bytes);
                if (json.Length > 0 && json[0] == '\uFEFF')
                    json = json.Substring(1);
                return ModDataNamespaceReader.Read(json, modGuid, source);
            }
            catch (DecoderFallbackException exception)
            {
                return ExtendedDataModDataReadResult.InvalidDocument(
                    modGuid,
                    source,
                    "The mod-data file is not valid UTF-8: " + exception.Message);
            }
        }

        private static ExtendedDataModDataReadResult ReadSelectedSlot(LordDataSlot slot, string modGuid)
        {
            string source = "host Lord slot " + slot.PlayerId + " (" + slot.LordName + "/" + slot.ConfigName + ")";
            if (slot.ModLordJson == null)
                return ExtendedDataModDataReadResult.FileNotFound(modGuid, source);
            return ModDataNamespaceReader.Read(slot.ModLordJson, modGuid, source);
        }
    }
}

using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    public static class ExtendedDataModDataApi
    {
        private const string LordExtension = ".lordjson";
        private static IDisposable sourceRegistration;
        private static LordDataSnapshot activeSnapshot;
        private static bool networkSessionActive;
        private static IReadOnlyDictionary<int, string> verifiedLocalLordPaths;
        private static IReadOnlyCollection<int> unresolvedLocalLordSlots;
        private static bool localSelectionActive;
        private static bool selectedLordIntegrationEnabled = true;
        internal static void SetSelectedLordIntegrationEnabled(bool value) => selectedLordIntegrationEnabled = value;

        public static int ApiVersion => 1;
        public static bool SupportsSinglePlayerSelections => true;

        internal static void InitializeSource()
        {
            if (sourceRegistration == null)
            {
                ClearSelection();
                sourceRegistration = APIShared.ModData.ModDataAccess.RegisterSelectedLordSource(
                    ExtendedDataPlugin.PluginGuid, "confirmed-lord-data", ReadConfirmedLordNamespace);
            }
        }

        internal static void UnregisterSource()
        {
            sourceRegistration?.Dispose();
            sourceRegistration = null;
            ClearSelection();
        }

        private static void ClearSelection()
        {
            activeSnapshot = null;
            networkSessionActive = false;
            verifiedLocalLordPaths = null;
            unresolvedLocalLordSlots = null;
            localSelectionActive = false;
        }

        internal static void SetNetworkSnapshot(LordDataSnapshot snapshot, bool sessionActive)
        {
            if (sourceRegistration == null) return;
            activeSnapshot = snapshot;
            networkSessionActive = sessionActive;
            verifiedLocalLordPaths = null;
            unresolvedLocalLordSlots = null;
            localSelectionActive = false;
        }

        internal static void SetVerifiedLocalLords(IReadOnlyDictionary<int, string> paths)
        {
            if (sourceRegistration == null) return;
            activeSnapshot = null;
            networkSessionActive = paths != null;
            verifiedLocalLordPaths = paths;
            unresolvedLocalLordSlots = null;
            localSelectionActive = false;
        }

        internal static void SetSinglePlayerLords(IReadOnlyDictionary<int, string> paths,
            IReadOnlyCollection<int> unresolvedSlots)
        {
            if (sourceRegistration == null) return;
            activeSnapshot = null;
            networkSessionActive = false;
            verifiedLocalLordPaths = paths;
            unresolvedLocalLordSlots = unresolvedSlots;
            localSelectionActive = true;
        }

        public static ExtendedDataModDataReadResult ReadSelectedLordNamespace(int playerId, string modGuid) =>
            FromShared(APIShared.ModData.ModDataAccess.ReadSelectedLordNamespace(playerId, modGuid));

        private static APIShared.ModData.ModDataReadResult ReadConfirmedLordNamespace(int playerId, string modGuid)
        {
            if (!selectedLordIntegrationEnabled)
                return SharedFailure(APIShared.ModData.ModDataReadStatus.HostDataUnavailable, modGuid,
                    string.Empty, "Selected Lord integration is disabled.");

            if (string.IsNullOrWhiteSpace(modGuid) || playerId < 1 || playerId > GamePlayerManagerAPI.MAX_PLAYERS)
                return SharedFailure(APIShared.ModData.ModDataReadStatus.InvalidRequest, modGuid, string.Empty, "A mod GUID and player ID from 1 to 8 are required.");
            if (verifiedLocalLordPaths != null &&
                verifiedLocalLordPaths.TryGetValue(playerId, out string localPath))
                return APIShared.ModData.ModDataAccess.ReadLocalLordNamespace(localPath, modGuid);
            if (localSelectionActive)
            {
                if (unresolvedLocalLordSlots?.Contains(playerId) == true)
                    return SharedFailure(APIShared.ModData.ModDataReadStatus.HostDataUnavailable, modGuid,
                        "local Lord slot " + playerId,
                        "The selected local Lord configuration is missing or ambiguous.");
                return SharedMissing(modGuid,
                    "local Lord slot " + playerId);
            }
            if (verifiedLocalLordPaths != null)
                return SharedMissing(modGuid,
                    "verified local Lord slot " + playerId);
            if (!networkSessionActive || activeSnapshot == null)
                return SharedFailure(APIShared.ModData.ModDataReadStatus.HostDataUnavailable, modGuid, string.Empty, "The host Lord-data snapshot is not ready.");
            LordDataSlot slot = activeSnapshot.GetSlot(playerId);
            if (slot == null)
                return SharedMissing(modGuid, "host Lord slot " + playerId);
            return ReadConfirmedSlot(slot, modGuid);
        }

        public static ExtendedDataModDataReadResult ReadCurrentMapNamespace(string modGuid) =>
            FromShared(APIShared.ModData.ModDataAccess.ReadCurrentMapNamespace(modGuid));

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

            return ReadLocalLordNamespace(lordJsonPath, modGuid);
        }

        private static ExtendedDataModDataReadResult ReadLocalLordNamespace(string lordJsonPath, string modGuid) =>
            FromShared(APIShared.ModData.ModDataAccess.ReadLocalLordNamespace(lordJsonPath, modGuid));

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

        private static ExtendedDataModDataReadResult ReadSelectedSlot(LordDataSlot slot, string modGuid) =>
            FromShared(ReadConfirmedSlot(slot, modGuid));

        private static APIShared.ModData.ModDataReadResult ReadConfirmedSlot(LordDataSlot slot, string modGuid)
        {
            string source = "host Lord slot " + slot.PlayerId + " (" + slot.LordName + "/" + slot.ConfigName + ")";
            return slot.ModLordJson == null ? SharedMissing(modGuid, source) :
                APIShared.ModData.ModDataDocumentJson.ReadNamespace(slot.ModLordJson, modGuid, source);
        }

        private static ExtendedDataModDataReadResult FromShared(APIShared.ModData.ModDataReadResult result) =>
            new ExtendedDataModDataReadResult((ExtendedDataModDataReadStatus)(int)result.Status,
                result.ModGuid, result.Source, result.Data, result.Json, result.Diagnostic);

        private static APIShared.ModData.ModDataReadResult SharedMissing(string modGuid, string source) =>
            SharedFailure(APIShared.ModData.ModDataReadStatus.FileNotFound, modGuid, source, "The mod-data file does not exist.");

        private static APIShared.ModData.ModDataReadResult SharedFailure(APIShared.ModData.ModDataReadStatus status,
            string modGuid, string source, string diagnostic) => APIShared.ModData.ModDataReadResult.Failure(status, modGuid, source, diagnostic);
    }
}

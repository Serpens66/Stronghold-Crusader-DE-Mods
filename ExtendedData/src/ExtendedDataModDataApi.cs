using SHCDESE.API;
using System;
using System.IO;
using System.Text;

namespace ExtendedData
{
    public static class ExtendedDataModDataApi
    {
        private const string MapEntryName = "modmap.json";
        private const string LordExtension = ".lordjson";
        private const string ModLordExtension = ".modlord.json";
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static int ApiVersion => 1;

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
    }
}

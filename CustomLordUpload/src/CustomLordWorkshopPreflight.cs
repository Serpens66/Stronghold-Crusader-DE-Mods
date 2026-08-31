using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CustomLordUpload
{
    /// <summary>
    /// Reports high-confidence package problems. The checks are advisory; Vanilla and the
    /// Script Extender remain the semantic authorities.
    /// </summary>
    internal static class CustomLordWorkshopPreflight
    {
        internal static IReadOnlyList<CustomLordUploadIssue> Inspect(
            string sourceLordRoot,
            CustomLordRuntimeRules rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            List<CustomLordUploadIssue> issues = new List<CustomLordUploadIssue>();
            if (!rules.IsKnownIdentity)
                issues.Add(Issue("UnknownExtenderVersion", "Version", rules.ExtenderIdentity));

            if (!CustomLordWorkshopPackagePolicy.TryCollectFilesForInspection(
                    sourceLordRoot,
                    out List<CustomLordWorkshopPackageFile> files,
                    out string error))
            {
                issues.Add(IssueWithDetail("UnsafeInspection", error));
                return issues;
            }

            Dictionary<string, CustomLordWorkshopPackageFile> byPath = files.ToDictionary(
                file => NormalizeRelativePath(file.RelativePath),
                StringComparer.OrdinalIgnoreCase);

            CheckVanillaBase(files, issues);
            CheckExtendedMetadata(byPath, rules, issues);
            CheckPackageHygiene(files, byPath, issues);
            foreach (CustomLordWorkshopPackageFile file in files)
            {
                string relativePath = NormalizeRelativePath(file.RelativePath);
                if (string.Equals(Path.GetExtension(relativePath), ".wav", StringComparison.OrdinalIgnoreCase))
                    CheckWave(file.SourcePath, relativePath, issues);
            }

            if (byPath.TryGetValue("avatar.png", out CustomLordWorkshopPackageFile? avatar))
                CheckAvatar(avatar.SourcePath, issues);

            RunPublicValidator(sourceLordRoot, rules, issues);
            return issues;
        }

        private static void CheckVanillaBase(
            IEnumerable<CustomLordWorkshopPackageFile> files,
            List<CustomLordUploadIssue> issues)
        {
            // COMPATIBILITY: Vanilla currently requires direct .lordjson and .aivjson files.
            List<CustomLordWorkshopPackageFile> lordFiles = new List<CustomLordWorkshopPackageFile>();
            List<CustomLordWorkshopPackageFile> castleFiles = new List<CustomLordWorkshopPackageFile>();
            foreach (CustomLordWorkshopPackageFile file in files)
            {
                string relativePath = NormalizeRelativePath(file.RelativePath);
                if (relativePath.IndexOf('/') >= 0)
                    continue;
                string extension = Path.GetExtension(relativePath);
                if (string.Equals(extension, ".lordjson", StringComparison.OrdinalIgnoreCase))
                    lordFiles.Add(file);
                else if (string.Equals(extension, ".aivjson", StringComparison.OrdinalIgnoreCase))
                    castleFiles.Add(file);
            }

            if (lordFiles.Count == 0)
                issues.Add(Issue("MissingLordJson"));
            if (castleFiles.Count == 0)
                issues.Add(Issue("MissingAivJson"));

            foreach (CustomLordWorkshopPackageFile file in lordFiles.Concat(castleFiles))
            {
                string relativePath = NormalizeRelativePath(file.RelativePath);
                TryInspectJson(
                    file.SourcePath,
                    relativePath,
                    value =>
                    {
                        if (!(value is Dictionary<string, object>))
                            issues.Add(Issue("VanillaJsonNotObject", "Path", relativePath));
                    },
                    issues);
            }
        }

        private static void CheckExtendedMetadata(
            Dictionary<string, CustomLordWorkshopPackageFile> files,
            CustomLordRuntimeRules rules,
            List<CustomLordUploadIssue> issues)
        {
            // COMPATIBILITY: The reviewed Extender discovers both metadata files only in the lord root.
            if (!files.TryGetValue("info.json", out CustomLordWorkshopPackageFile? infoFile))
            {
                issues.Add(Issue("MissingInfoJson"));
            }
            else
            {
                TryInspectJson(
                    infoFile.SourcePath,
                    "info.json",
                    value => InspectInfoJson(value, rules, issues),
                    issues);
            }

            if (!files.TryGetValue("lordmeta.json", out CustomLordWorkshopPackageFile? lordMetaFile))
            {
                issues.Add(Issue("MissingLordMetaJson"));
            }
            else
            {
                TryInspectJson(
                    lordMetaFile.SourcePath,
                    "lordmeta.json",
                    value => InspectLordMetaJson(value, rules, issues),
                    issues);
            }
        }

        private static void InspectInfoJson(
            object value,
            CustomLordRuntimeRules rules,
            List<CustomLordUploadIssue> issues)
        {
            if (!(value is Dictionary<string, object> root))
            {
                issues.Add(Issue("InfoJsonNotObject"));
                return;
            }

            string? guid = GetOptionalString(root, "GUID");
            if (string.IsNullOrWhiteSpace(guid))
                issues.Add(Issue("InfoGuidMissing"));

            string? version = GetOptionalString(root, "Version");
            if (!TryParseModVersion(version, out _))
            {
                issues.Add(Issue(
                    rules.UsesVersionedAssetModResolution
                        ? "InfoVersionInvalid"
                        : "InfoVersionRecommended"));
            }
        }

        private static void InspectLordMetaJson(
            object value,
            CustomLordRuntimeRules rules,
            List<CustomLordUploadIssue> issues)
        {
            if (!(value is Dictionary<string, object> root))
            {
                issues.Add(Issue("LordMetaNotObject"));
                return;
            }

            foreach (string field in root.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                if (!rules.LordInfoFields.Contains(field, StringComparer.OrdinalIgnoreCase))
                    issues.Add(Issue("UnknownLordMetaField", "Field", field));
            }

            string? incomingMessage = GetOptionalString(root, "IncomingMessage");
            if (!string.IsNullOrWhiteSpace(incomingMessage))
                issues.Add(Issue("IncomingMessageUnused"));

            if (!root.TryGetValue("Messages", out object messagesValue) || messagesValue == null)
                return;
            if (!(messagesValue is Dictionary<string, object> messages))
                throw new InvalidDataException("lordmeta.json Messages must contain a JSON object.");

            Dictionary<int, string> messageIds = new Dictionary<int, string>();
            foreach (KeyValuePair<string, object> message in messages)
            {
                if (!TryResolveMessageId(message.Key, rules.MessageTypes, out int messageId))
                {
                    issues.Add(Issue("UnknownMessageType", "Message", message.Key));
                    continue;
                }

                if (messageIds.TryGetValue(messageId, out string? existingKey) &&
                    !string.Equals(existingKey, message.Key, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Issue(
                        "DuplicateMessageId",
                        "First", existingKey,
                        "Second", message.Key,
                        "Id", messageId));
                }
                else
                {
                    messageIds[messageId] = message.Key;
                }

                if (message.Value == null)
                {
                    issues.Add(Issue("NullMessageList", "Message", message.Key));
                    continue;
                }
                if (!(message.Value is List<object> clips))
                    throw new InvalidDataException("lordmeta.json message values must contain clip arrays.");
                if (clips.Any(clip => clip == null))
                    issues.Add(Issue("NullMessageClip", "Message", message.Key));
                if (clips.Any(clip => clip != null && !(clip is Dictionary<string, object>)))
                    throw new InvalidDataException("lordmeta.json message arrays may only contain clip objects.");
            }
        }

        private static void CheckPackageHygiene(
            IEnumerable<CustomLordWorkshopPackageFile> files,
            Dictionary<string, CustomLordWorkshopPackageFile> filesByPath,
            List<CustomLordUploadIssue> issues)
        {
            // COMPATIBILITY: Recheck the reviewed Override and localized Override path conventions
            // when Script Extender asset indexing changes.
            HashSet<string> reportedDevelopmentDirectories =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CustomLordWorkshopPackageFile file in files)
            {
                string relativePath = NormalizeRelativePath(file.RelativePath);
                string[] segments = relativePath.Split('/');
                string fileName = segments[segments.Length - 1];
                string extension = Path.GetExtension(fileName);
                bool rootFile = segments.Length == 1;

                foreach (string directory in segments.Take(segments.Length - 1))
                {
                    if (CustomLordCompatibilityProfile.DevelopmentDirectoryNames.Contains(directory) &&
                        reportedDevelopmentDirectories.Add(directory))
                    {
                        issues.Add(Issue("DevelopmentDirectory", "Directory", directory));
                    }
                }

                if (CustomLordCompatibilityProfile.DevelopmentExtensions.Contains(extension))
                    issues.Add(Issue("DevelopmentFile", "Path", relativePath));

                if (!rootFile &&
                    (string.Equals(fileName, "info.json", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(fileName, "lordmeta.json", StringComparison.OrdinalIgnoreCase)) &&
                    !filesByPath.ContainsKey(fileName))
                {
                    issues.Add(Issue("MisplacedMetadata", "Path", relativePath, "File", fileName));
                }

                if (rootFile &&
                    !string.Equals(fileName, "avatar.png", StringComparison.OrdinalIgnoreCase) &&
                    (CustomLordCompatibilityProfile.RootMediaExtensions.Contains(extension) ||
                     string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(Issue("RootMedia", "Path", relativePath));
                }

                if (segments.Length > 1 && string.Equals(segments[0], "fx", StringComparison.OrdinalIgnoreCase))
                    issues.Add(Issue("RootFx", "Path", relativePath));
            }
        }

        private static void CheckWave(
            string path,
            string relativePath,
            List<CustomLordUploadIssue> issues)
        {
            try
            {
                byte[] wav = File.ReadAllBytes(path);
                if (wav.Length < 44 || ReadAscii(wav, 0) != "RIFF" || ReadAscii(wav, 8) != "WAVE")
                {
                    issues.Add(Issue("WaveHeader", "Path", relativePath));
                    return;
                }

                short format = BitConverter.ToInt16(wav, 20);
                short channels = BitConverter.ToInt16(wav, 22);
                int sampleRate = BitConverter.ToInt32(wav, 24);
                short bitsPerSample = BitConverter.ToInt16(wav, 34);
                bool hasData = HasValidWaveDataChunk(wav);

                if (format != CustomLordCompatibilityProfile.WavePcmFormat)
                    issues.Add(Issue("WaveFormat", "Path", relativePath, "Value", format));
                if (channels != 1 && channels != 2)
                    issues.Add(Issue("WaveChannels", "Path", relativePath, "Value", channels));
                if (sampleRate != CustomLordCompatibilityProfile.WaveSampleRate)
                    issues.Add(Issue("WaveSampleRate", "Path", relativePath, "Value", sampleRate));
                if (bitsPerSample != CustomLordCompatibilityProfile.WaveBitsPerSample)
                    issues.Add(Issue("WaveBits", "Path", relativePath, "Value", bitsPerSample));
                if (!hasData)
                    issues.Add(Issue("WaveData", "Path", relativePath));
            }
            catch (Exception exception)
            {
                issues.Add(IssueWithDetail("WaveUnreadable", exception.ToString(), "Path", relativePath));
            }
        }

        private static bool HasValidWaveDataChunk(byte[] wav)
        {
            int position = 12;
            while (position + 8 <= wav.Length)
            {
                string chunkId = ReadAscii(wav, position);
                int chunkSize = BitConverter.ToInt32(wav, position + 4);
                if (chunkSize < 0)
                    return false;
                long chunkEnd = (long)position + 8 + chunkSize;
                if (chunkEnd > wav.Length)
                    return false;
                if (chunkId == "data" && chunkSize > 0)
                    return true;
                position = checked(position + 8 + chunkSize + (chunkSize & 1));
            }
            return false;
        }

        private static void CheckAvatar(string path, List<CustomLordUploadIssue> issues)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length >= CustomLordCompatibilityProfile.AvatarMaximumExclusiveBytes)
                    issues.Add(Issue("AvatarSize", "Bytes", info.Length.ToString("N0", CultureInfo.CurrentCulture)));

                byte[] header = new byte[24];
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Read(header, 0, header.Length) != header.Length)
                    {
                        issues.Add(Issue("AvatarHeader"));
                        return;
                    }
                }

                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                bool isPng = BytesEqual(header, 0, signature) && ReadAscii(header, 12) == "IHDR";
                if (!isPng)
                {
                    issues.Add(Issue("AvatarHeader"));
                    return;
                }

                uint width = ReadUInt32BigEndian(header, 16);
                uint height = ReadUInt32BigEndian(header, 20);
                if (width != CustomLordCompatibilityProfile.AvatarWidth ||
                    height != CustomLordCompatibilityProfile.AvatarHeight)
                {
                    issues.Add(Issue("AvatarDimensions", "Width", width, "Height", height));
                }
            }
            catch (Exception exception)
            {
                issues.Add(IssueWithDetail("AvatarUnreadable", exception.ToString()));
            }
        }

        private static void RunPublicValidator(
            string sourceLordRoot,
            CustomLordRuntimeRules rules,
            List<CustomLordUploadIssue> issues)
        {
            if (rules.PublicValidator == null)
                return;

            try
            {
                foreach (string problem in rules.PublicValidator(sourceLordRoot) ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(problem))
                        issues.Add(IssueWithDetail("ExtenderValidatorIssue", problem));
                }
            }
            catch (Exception exception)
            {
                issues.Add(IssueWithDetail("ExtenderValidatorFailed", exception.ToString()));
            }
        }

        private static void TryInspectJson(
            string path,
            string displayName,
            Action<object> inspect,
            List<CustomLordUploadIssue> issues)
        {
            try
            {
                object document = DependencyFreeJson.Parse(File.ReadAllText(path));
                inspect(document);
            }
            catch (Exception exception)
            {
                issues.Add(IssueWithDetail("JsonUnreadable", exception.ToString(), "Path", displayName));
            }
        }

        private static string? GetOptionalString(Dictionary<string, object> root, string key)
        {
            if (!root.TryGetValue(key, out object value) || value == null)
                return null;
            if (value is string text)
                return text;
            throw new InvalidDataException(key + " must be a JSON string.");
        }

        private static bool TryResolveMessageId(
            string key,
            IReadOnlyDictionary<string, int> messageTypes,
            out int messageId)
        {
            if (messageTypes.TryGetValue(key, out messageId))
                return true;
            return int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out messageId);
        }

        private static bool TryParseModVersion(string? rawVersion, out Version version)
        {
            // COMPATIBILITY: Mirrors the currently reviewed Script Extender asset-mod version parser.
            version = new Version(0, 0, 0, 0);
            if (string.IsNullOrWhiteSpace(rawVersion))
                return false;

            string text = rawVersion!.Trim();
            if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
                text = text.Substring(1);
            int suffixIndex = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffixIndex >= 0)
                text = text.Substring(0, suffixIndex);
            if (text.IndexOf('.') < 0)
                text += ".0";

            if (!Version.TryParse(text, out Version? parsed) || parsed == null)
                return false;
            version = parsed;
            return true;
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string ReadAscii(byte[] bytes, int offset)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                return string.Empty;
            return new string(new[]
            {
                (char)bytes[offset], (char)bytes[offset + 1],
                (char)bytes[offset + 2], (char)bytes[offset + 3]
            });
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) |
                   ((uint)bytes[offset + 1] << 16) |
                   ((uint)bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static bool BytesEqual(byte[] source, int offset, byte[] expected)
        {
            if (offset < 0 || offset + expected.Length > source.Length)
                return false;
            for (int index = 0; index < expected.Length; index++)
            {
                if (source[offset + index] != expected[index])
                    return false;
            }
            return true;
        }

        private static CustomLordUploadIssue Issue(string code, params object[] replacements)
        {
            return new CustomLordUploadIssue(code, replacements);
        }

        private static CustomLordUploadIssue IssueWithDetail(
            string code,
            string technicalDetail,
            params object[] replacements)
        {
            return new CustomLordUploadIssue(code, technicalDetail, replacements);
        }
    }
}

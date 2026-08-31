using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomLordUpload
{
    /// <summary>
    /// Reports high-confidence package problems before a Custom Lord upload. These checks are
    /// advisory only; the game and Script Extender loaders remain the semantic authorities.
    /// </summary>
    internal static class CustomLordWorkshopPreflight
    {
        private static readonly HashSet<string> RootMediaExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".wav", ".ogg", ".webm", ".mp4", ".jpg", ".jpeg", ".tga"
            };

        private static readonly HashSet<string> DevelopmentExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".7z", ".aup", ".aup3", ".bak", ".cs", ".csproj", ".dll", ".exe", ".pdb",
                ".psd", ".rar", ".sln", ".tmp", ".zip"
            };

        private static readonly HashSet<string> DevelopmentDirectoryNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git", ".svn", "_LegacyMediaSource", "bin", "node_modules", "obj"
            };

        // Local copy of the loader's accepted names keeps this mod compatible with Script Extender 1.42.0
        // while retaining Branch B's corrected, unique ID for AllyNotificationCongratulations.
        private enum LordMessageType
        {
            IncomingMessage = 0,
            WillAttack = 1,
            TauntSiege2 = 2,
            TauntSiege3 = 3,
            TauntSiege4 = 4,
            AngerSiegeFailed = 5,
            AngerFortressDamaged = 6,
            PleadDeath = 7,
            PleadOutsideWalls = 8,
            NervousInsideWalls = 9,
            Counterattack = 10,
            Unk11 = 11,
            Won = 12,
            Unk13 = 13,
            RequestGoods = 14,
            ReceivedGoods = 15,
            DefeatedAgain = 16,
            AllyNotificationCongratulations = 17,
            AllyNotificationHasDefeatedEnemy = 18,
            AllyNotificationRequestReinforcements = 19,
            AllyNotificationMerryChristmas = 20,
            Unk21 = 21,
            Unk22 = 22,
            AllyNotificationWillSiegeEnemySoon = 23,
            AllyNotificationCannotAttackEnemy = 24,
            AllyNotificationWillNotAttackToday = 25,
            AllyNotificationCannotNotHelp = 26,
            AllyNotificationWillNotHelp = 27,
            AllyNotificationWillNotSendRequestedGoods = 28,
            AllyNotificationHasSentRequestedGoods = 29,
            AllyNotificationConfidentInVictory = 30,
            AllyNotificationConfidentInLosing = 31,
            AllyNotificationSentReinforcements = 32,
            AllyNotificationAgree = 33
        }

        internal static IReadOnlyList<string> Inspect(string sourceLordRoot)
        {
            List<string> issues = new List<string>();
            if (!CustomLordWorkshopPackagePolicy.TryCollectFilesForInspection(
                    sourceLordRoot,
                    out _,
                    out List<CustomLordWorkshopPackageFile> files,
                    out string error))
            {
                issues.Add("The package could not be inspected safely: " + error);
                return issues;
            }

            Dictionary<string, CustomLordWorkshopPackageFile> byPath = files.ToDictionary(
                file => NormalizeRelativePath(file.RelativePath),
                StringComparer.OrdinalIgnoreCase);

            CheckVanillaBase(files, issues);
            CheckRequiredExtendedMetadata(byPath, issues);
            CheckPaths(files, byPath, issues);
            foreach (CustomLordWorkshopPackageFile file in files)
            {
                string relativePath = NormalizeRelativePath(file.RelativePath);
                if (string.Equals(Path.GetExtension(relativePath), ".wav", StringComparison.OrdinalIgnoreCase))
                    CheckWave(file.SourcePath, relativePath, issues);
            }

            if (byPath.TryGetValue("avatar.png", out CustomLordWorkshopPackageFile? avatar))
                CheckAvatar(avatar.SourcePath, issues);

            return issues;
        }

        private static void CheckVanillaBase(
            IEnumerable<CustomLordWorkshopPackageFile> files,
            List<string> issues)
        {
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
                issues.Add("No direct .lordjson exists; Vanilla cannot load a lord configuration from this package.");
            if (castleFiles.Count == 0)
                issues.Add("No direct .aivjson exists; Vanilla cannot load a castle from this package.");

            foreach (CustomLordWorkshopPackageFile file in lordFiles.Concat(castleFiles))
            {
                string relativePath = NormalizeRelativePath(file.RelativePath);
                TryInspectJson(
                    file.SourcePath,
                    relativePath,
                    value =>
                    {
                        if (!(value is Dictionary<string, object>))
                            issues.Add($"{relativePath} must contain a JSON object for Vanilla.");
                    },
                    issues);
            }
        }

        private static void CheckRequiredExtendedMetadata(
            Dictionary<string, CustomLordWorkshopPackageFile> files,
            List<string> issues)
        {
            if (!files.TryGetValue("info.json", out CustomLordWorkshopPackageFile? infoFile))
            {
                issues.Add("info.json is missing from the lord root; Script Extender assets will not be registered.");
            }
            else
            {
                TryInspectJson(
                    infoFile.SourcePath,
                    "info.json",
                    value => InspectInfoJson(value, issues),
                    issues);
            }

            if (!files.TryGetValue("lordmeta.json", out CustomLordWorkshopPackageFile? lordMetaFile))
            {
                issues.Add("lordmeta.json is missing from the lord root; extended lord metadata will not be loaded.");
            }
            else
            {
                TryInspectJson(
                    lordMetaFile.SourcePath,
                    "lordmeta.json",
                    value => InspectLordMetaJson(value, issues),
                    issues);
            }
        }

        private static void InspectInfoJson(object value, List<string> issues)
        {
            if (!(value is Dictionary<string, object> root))
            {
                issues.Add("info.json must contain a JSON object.");
                return;
            }

            string? guid = GetOptionalString(root, "GUID");
            if (string.IsNullOrWhiteSpace(guid))
                issues.Add("info.json has no non-empty string GUID; the asset mod cannot be registered reliably.");

            string? version = GetOptionalString(root, "Version");
            if (!TryParseModVersion(version, out _))
            {
                issues.Add(
                    "info.json has no valid string Version; duplicate asset-mod resolution will treat it as an unknown version.");
            }
        }

        private static void InspectLordMetaJson(object value, List<string> issues)
        {
            if (!(value is Dictionary<string, object> root))
            {
                issues.Add("lordmeta.json must contain a JSON object.");
                return;
            }

            string? incomingMessage = GetOptionalString(root, "IncomingMessage");
            if (!string.IsNullOrWhiteSpace(incomingMessage))
                issues.Add("lordmeta.json IncomingMessage is currently not evaluated by the Script Extender.");

            if (!root.TryGetValue("Messages", out object messagesValue) || messagesValue == null)
                return;
            if (!(messagesValue is Dictionary<string, object> messages))
                throw new InvalidDataException("lordmeta.json Messages must contain a JSON object.");

            Dictionary<int, string> messageIds = new Dictionary<int, string>();
            foreach (KeyValuePair<string, object> message in messages)
            {
                // Match BuildLordEntry: named keys are case-insensitive and numeric values are accepted.
                if (!Enum.TryParse(message.Key, true, out LordMessageType messageType))
                {
                    issues.Add(
                        $"lordmeta.json message key [{message.Key}] is not a supported AI lord message type and will be ignored.");
                    continue;
                }

                int messageId = (int)messageType;
                if (messageIds.TryGetValue(messageId, out string? existingKey) &&
                    !string.Equals(existingKey, message.Key, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(
                        $"lordmeta.json message keys [{existingKey}] and [{message.Key}] both use native ID {messageId}; one replaces the other.");
                }
                else
                {
                    messageIds[messageId] = message.Key;
                }

                if (message.Value == null)
                {
                    issues.Add(
                        $"lordmeta.json message key [{message.Key}] has a null clip list and cannot be played safely.");
                    continue;
                }
                if (!(message.Value is List<object> clips))
                    throw new InvalidDataException($"lordmeta.json message key [{message.Key}] must contain a clip array.");
                if (clips.Any(clip => clip == null))
                {
                    issues.Add(
                        $"lordmeta.json message key [{message.Key}] contains a null clip and cannot be played safely.");
                }
                foreach (object clip in clips)
                {
                    if (clip != null && !(clip is Dictionary<string, object>))
                        throw new InvalidDataException($"lordmeta.json message key [{message.Key}] contains an invalid clip.");
                }
            }
        }

        private static void CheckPaths(
            IEnumerable<CustomLordWorkshopPackageFile> files,
            Dictionary<string, CustomLordWorkshopPackageFile> filesByPath,
            List<string> issues)
        {
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
                    if (DevelopmentDirectoryNames.Contains(directory) &&
                        reportedDevelopmentDirectories.Add(directory))
                    {
                        issues.Add(
                            $"Development/source directory [{directory}] is inside the publishable lord folder and will be uploaded.");
                    }
                }

                if (DevelopmentExtensions.Contains(extension))
                {
                    issues.Add(
                        $"File [{relativePath}] looks like development/archive material and is not loaded automatically; it will still be uploaded. Keep it only if another script or system consumes it intentionally.");
                }

                if (!rootFile &&
                    (string.Equals(fileName, "info.json", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(fileName, "lordmeta.json", StringComparison.OrdinalIgnoreCase)) &&
                    !filesByPath.ContainsKey(fileName))
                {
                    issues.Add(
                        $"[{relativePath}] looks misplaced; the required {fileName} is absent from the lord root, where the Script Extender reads it.");
                }

                if (rootFile &&
                    !string.Equals(fileName, "avatar.png", StringComparison.OrdinalIgnoreCase) &&
                    (RootMediaExtensions.Contains(extension) ||
                     string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)))
                {
                    issues.Add(
                        $"Media file [{relativePath}] is in the lord root and is not indexed as an asset; place it below Override with its logical asset path.");
                }

                if (segments.Length > 1 && string.Equals(segments[0], "fx", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(
                        $"[{relativePath}] is below root/fx; speech assets must be below Override/fx (optionally Override/Locales/<locale>/fx).");
                }
            }
        }

        private static void CheckWave(string path, string relativePath, List<string> issues)
        {
            try
            {
                byte[] wav = File.ReadAllBytes(path);
                if (wav.Length < 44)
                    throw new InvalidDataException("too short to contain the header required by the Script Extender");
                if (ReadAscii(wav, 0) != "RIFF" || ReadAscii(wav, 8) != "WAVE")
                    throw new InvalidDataException("missing the RIFF/WAVE header");

                short format = BitConverter.ToInt16(wav, 20);
                short channels = BitConverter.ToInt16(wav, 22);
                int sampleRate = BitConverter.ToInt32(wav, 24);
                short bitsPerSample = BitConverter.ToInt16(wav, 34);
                bool hasData = false;
                int position = 12;
                while (position + 8 < wav.Length)
                {
                    string chunkId = ReadAscii(wav, position);
                    int chunkSize = BitConverter.ToInt32(wav, position + 4);
                    if (chunkSize < 0)
                        throw new InvalidDataException("contains a negative chunk size");
                    long chunkEnd = (long)position + 8 + chunkSize;
                    if (chunkEnd > wav.Length)
                        throw new InvalidDataException("contains a chunk whose size exceeds the file length");
                    if (chunkId == "data" && chunkSize > 0)
                    {
                        hasData = true;
                        break;
                    }
                    position = checked(position + 8 + chunkSize);
                }

                List<string> defects = new List<string>();
                if (format != 1) defects.Add("PCM format 1 required");
                if (channels != 1 && channels != 2) defects.Add("mono or stereo required");
                if (sampleRate != 44100) defects.Add($"44,100 Hz required (found {sampleRate})");
                if (bitsPerSample != 16) defects.Add("16-bit required");
                if (!hasData) defects.Add("valid non-empty data chunk required");
                if (defects.Count > 0)
                    issues.Add($"WAV [{relativePath}] is unsupported: {string.Join(", ", defects)}.");
            }
            catch (Exception exception)
            {
                issues.Add($"WAV [{relativePath}] is unsupported or unreadable: {exception.Message}.");
            }
        }

        private static void CheckAvatar(string path, List<string> issues)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (info.Length >= 80000)
                    issues.Add($"avatar.png is {info.Length:N0} bytes; Vanilla requires less than 80,000 bytes.");

                byte[] header = new byte[24];
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Read(header, 0, header.Length) != header.Length)
                    {
                        issues.Add("avatar.png is too short to be a usable image; Vanilla will ignore it.");
                        return;
                    }
                }

                byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                bool isPng = BytesEqual(header, 0, signature) && ReadAscii(header, 12) == "IHDR";
                if (isPng)
                {
                    uint width = ReadUInt32BigEndian(header, 16);
                    uint height = ReadUInt32BigEndian(header, 20);
                    if (width != 144 || height != 144)
                        issues.Add($"avatar.png is {width}x{height}; Vanilla requires exactly 144x144 pixels.");
                }
            }
            catch (Exception exception)
            {
                issues.Add("avatar.png could not be inspected: " + exception.Message);
            }
        }

        private static void TryInspectJson(
            string path,
            string displayName,
            Action<object> inspect,
            List<string> issues)
        {
            try
            {
                object document = DependencyFreeJson.Parse(File.ReadAllText(path));
                inspect(document);
            }
            catch (Exception exception)
            {
                issues.Add($"{displayName} is malformed or unreadable: {exception.Message}");
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

        private static bool TryParseModVersion(string? rawVersion, out Version version)
        {
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

        private static bool BytesEqual(byte[] bytes, int offset, byte[] expected)
        {
            if (offset < 0 || bytes.Length - offset < expected.Length)
                return false;
            for (int index = 0; index < expected.Length; index++)
            {
                if (bytes[offset + index] != expected[index])
                    return false;
            }
            return true;
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) |
                   ((uint)bytes[offset + 1] << 16) |
                   ((uint)bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ReadAscii(byte[] bytes, int offset)
        {
            return System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
        }
    }
}

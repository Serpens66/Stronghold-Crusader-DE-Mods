using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomCustomTrail.Core
{
    public sealed class MissionLoader
    {
        public const int CurrentSchemaVersion = 2;
        private static readonly HashSet<int> Rotations = new HashSet<int> { 0, 90, 180, 270 };

        public LoadedMission Load(string jsonPath, int trailNumber, int missionNumber)
        {
            if (trailNumber < 1 || trailNumber > 4)
                throw new InvalidDataException("Trail number must be between 1 and 4.");
            if (missionNumber < 1 || missionNumber > 10)
                throw new InvalidDataException("Mission number must be between 1 and 10.");

            string fullJsonPath = Path.GetFullPath(jsonPath ?? throw new ArgumentNullException(nameof(jsonPath)));
            string missionRoot = Path.GetDirectoryName(fullJsonPath);
            CoopMissionDefinition definition = MissionDefinitionJson.Parse(File.ReadAllText(fullJsonPath, Encoding.UTF8));

            Validate(definition);
            List<string> bundledFiles = ResolveBundledFiles(definition, missionRoot);
            return new LoadedMission
            {
                TrailNumber = trailNumber,
                MissionNumber = missionNumber,
                JsonPath = fullJsonPath,
                MissionRoot = missionRoot,
                Definition = definition,
                BundledFiles = bundledFiles,
            };
        }

        public static string Serialize(CoopMissionDefinition definition)
        {
            Validate(definition);
            return MissionDefinitionJson.Serialize(definition);
        }

        public static void WriteAtomic(string path, CoopMissionDefinition definition)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, Serialize(definition), new UTF8Encoding(false));
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static string ResolveBundledPath(string missionRoot, string relativePath, string requiredExtension)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new InvalidDataException("Bundled asset requires a relative file path.");
            if (Path.IsPathRooted(relativePath))
                throw new InvalidDataException("Bundled asset paths must be relative.");

            string root = Path.GetFullPath(missionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(root, relativePath));
            // The trailing separator prevents a sibling with the same path prefix from passing.
            if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Bundled asset path escapes its mission directory: " + relativePath);
            if (!string.Equals(Path.GetExtension(result), requiredExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Bundled asset must use " + requiredExtension + ": " + relativePath);
            if (!File.Exists(result))
                throw new FileNotFoundException("Bundled asset was not found.", result);
            return result;
        }

        private static void Validate(CoopMissionDefinition mission)
        {
            if (mission == null)
                throw new InvalidDataException("Mission JSON is empty.");
            if (mission.SchemaVersion != CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported schemaVersion " + mission.SchemaVersion + ".");
            if (string.IsNullOrWhiteSpace(mission.DisplayName))
                throw new InvalidDataException("displayName is required.");
            ValidateAsset(mission.Map, "map", ".map");
            if (mission.Settings == null)
                mission.Settings = new CoopSettings();
            if (mission.Settings.Fairness < 1 || mission.Settings.Fairness > 5)
                throw new InvalidDataException("settings.fairness must be between 1 and 5.");
            if (mission.Settings.StartingGoodsLevel < 1 || mission.Settings.StartingGoodsLevel > 4)
                throw new InvalidDataException("settings.startingGoodsLevel must be between 1 and 4.");
            if (mission.Players == null)
                throw new InvalidDataException("players is required.");

            List<PlayerDefinition> active = mission.Players.Where(player => player != null && player.Active).ToList();
            if (active.Count < 2 || active.Count > 8)
                throw new InvalidDataException("A mission needs between two and eight active players.");
            var positions = new HashSet<int>();
            for (int i = 0; i < active.Count; i++)
            {
                PlayerDefinition player = active[i];
                if (player.KeepPosition < 1 || player.KeepPosition > 8 || !positions.Add(player.KeepPosition))
                    throw new InvalidDataException("Every active player needs a unique keepPosition from 1 through 8.");
                if (player.Team < 1 || player.Team > 8)
                    throw new InvalidDataException("Player team must be between 1 and 8.");
                if (player.Colour < 0 || player.Colour > 7)
                    throw new InvalidDataException("Player colour must be between 0 and 7.");
                if (i < 2)
                    continue;
                if (player.Lord == null)
                    throw new InvalidDataException("Every AI player requires a lord reference.");
                ValidateAsset(player.Lord, "lord", ".lordjson");
                if (player.Lord.BaseLordId < 0)
                    throw new InvalidDataException("lord.baseLordId must not be negative.");
                if (player.Aivs == null)
                    player.Aivs = new List<AivReference>();
                foreach (AivReference aiv in player.Aivs)
                {
                    ValidateAsset(aiv, "aiv", ".aivjson");
                    if (!Rotations.Contains(aiv.Rotation))
                        throw new InvalidDataException("AIV rotation must be 0, 90, 180 or 270.");
                }
                if (player.PreferredAiv < -1 || player.PreferredAiv >= player.Aivs.Count)
                    throw new InvalidDataException("preferredAiv must be -1 or an index into aivs.");
                if (player.PreferredAiv < 0 && player.Aivs.Select(aiv => aiv.Rotation).Distinct().Count() > 1)
                    throw new InvalidDataException("All selectable AIVs need the same rotation unless preferredAiv selects one explicitly.");
            }

            try
            {
                NormalizeAndValidateModSettings(mission);
            }
            catch (Exception exception)
            {
                // Mission assets remain usable; only the transactionally invalid Trail preset is discarded.
                mission.ModSettingsError = exception.Message;
                mission.ModSettings = ModSettingsDefinition.CreateUnmanaged();
            }
        }

        private static void ValidateAsset(AssetReference asset, string label, string extension)
        {
            if (asset == null)
                throw new InvalidDataException(label + " reference is required.");
            string source = (asset.Source ?? string.Empty).Trim();
            if (source != "builtIn" && source != "installed" && source != "bundled")
                throw new InvalidDataException(label + ".source must be builtIn, installed or bundled.");
            if (source == "builtIn" && label == "map" && string.IsNullOrWhiteSpace(asset.Name))
                throw new InvalidDataException("map builtIn reference requires its internal name.");
            if (source == "builtIn" && label != "map" && !asset.Id.HasValue)
                throw new InvalidDataException(label + " builtIn reference requires id.");
            if (source == "installed" && string.IsNullOrWhiteSpace(asset.Name))
                throw new InvalidDataException(label + " installed reference requires name.");
            if (source == "bundled" && (string.IsNullOrWhiteSpace(asset.File) || !asset.File.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(label + " bundled reference requires a " + extension + " file.");
        }

        private static void NormalizeAndValidateModSettings(CoopMissionDefinition mission)
        {
            mission.ModSettings = ModSettingsJson.NormalizeAndValidate(mission.ModSettings, "modSettings");
        }

        private static List<string> ResolveBundledFiles(CoopMissionDefinition mission, string root)
        {
            var files = new List<string>();
            AddBundled(files, root, mission.Map, ".map");
            foreach (PlayerDefinition player in mission.Players.Where(player => player != null && player.Active).Skip(2))
            {
                AddBundled(files, root, player.Lord, ".lordjson");
                foreach (AivReference aiv in player.Aivs)
                    AddBundled(files, root, aiv, ".aivjson");
            }
            return files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddBundled(List<string> files, string root, AssetReference asset, string extension)
        {
            if (asset != null && string.Equals(asset.Source, "bundled", StringComparison.Ordinal))
                files.Add(ResolveBundledPath(root, asset.File, extension));
        }

    }

}

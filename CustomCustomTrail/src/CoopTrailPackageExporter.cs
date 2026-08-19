using CustomCustomTrail.Core;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomCustomTrail
{
    internal sealed class CoopTrailPackageExporter
    {
        internal sealed class PreparedPackage : IDisposable
        {
            private readonly string stagingRoot;

            internal PreparedPackage(string stagingRoot, CoopTrailPackage package)
            {
                this.stagingRoot = stagingRoot;
                Package = package;
            }

            internal CoopTrailPackage Package { get; }

            internal void Publish(string destination)
            {
                string targetRoot = Path.GetFullPath(destination);
                string targetMissions = Path.Combine(targetRoot, "CoopMissions");
                string targetManifest = Path.Combine(targetRoot, "cooptrail.json");
                string backupMissions = Path.Combine(targetRoot, ".CoopMissions.old-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(targetRoot);
                bool backedUp = false;
                try
                {
                    if (Directory.Exists(targetMissions))
                    {
                        Directory.Move(targetMissions, backupMissions);
                        backedUp = true;
                    }
                    Directory.Move(Path.Combine(stagingRoot, "CoopMissions"), targetMissions);
                    CoopTrailPackageManifestJson.WriteAtomic(targetManifest, Package.Manifest);
                }
                catch
                {
                    if (Directory.Exists(targetMissions))
                        Directory.Delete(targetMissions, true);
                    if (backedUp && Directory.Exists(backupMissions))
                        Directory.Move(backupMissions, targetMissions);
                    throw;
                }

                // The package is committed once its manifest has been replaced. Failure to
                // clean an obsolete backup must not roll the valid new package back.
                if (backedUp && Directory.Exists(backupMissions))
                {
                    try
                    {
                        Directory.Delete(backupMissions, true);
                    }
                    catch
                    {
                    }
                }
            }

            public void Dispose()
            {
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, true);
            }
        }

        public PreparedPackage Prepare(
            string makerRoot,
            string destination,
            Func<string, ModSettingsDefinition> readModSettings = null)
        {
            string customTrailsRoot = Path.GetDirectoryName(Path.GetFullPath(destination));
            string stagingRoot = Path.Combine(customTrailsRoot, ".cooptrail-build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingRoot);
            try
            {
                string missionsRoot = Path.Combine(stagingRoot, "CoopMissions");
                Directory.CreateDirectory(missionsRoot);
                var jsonPaths = new List<string>();
                int ordinal = 0;
                for (int sourceIndex = 0; sourceIndex < 50 && ordinal < 40; sourceIndex++)
                {
                    string makerName = FRONT_ManageTrail.GetMakerFileName(sourceIndex);
                    string trailPath = Path.Combine(makerRoot, makerName + ".trail");
                    if (!File.Exists(trailPath))
                        continue;
                    ordinal++;
                    CoopMissionDefinition definition = CreateDefinition(
                        trailPath,
                        ordinal,
                        missionsRoot,
                        readModSettings);
                    string jsonPath = Path.Combine(missionsRoot, ordinal.ToString("00") + ".coopmission.json");
                    MissionLoader.WriteAtomic(jsonPath, definition);
                    jsonPaths.Add(jsonPath);
                }
                if (ordinal == 0)
                    throw new InvalidDataException("The Trail Maker contains no missions to export as a Coop Trail.");

                string packageId = ReadExistingPackageId(destination) ?? Guid.NewGuid().ToString("D");
                var fingerprintFiles = new List<string>(jsonPaths);
                var loader = new MissionLoader();
                for (int index = 0; index < jsonPaths.Count; index++)
                {
                    LoadedMission loaded = loader.Load(jsonPaths[index], (index / 10) + 1, (index % 10) + 1);
                    fingerprintFiles.AddRange(loaded.BundledFiles);
                }
                var manifest = new CoopTrailPackageManifest
                {
                    SchemaVersion = CoopTrailPackageManifestJson.CurrentSchemaVersion,
                    PackageId = packageId,
                    DisplayName = Path.GetFileName(Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    MissionCount = ordinal,
                    ContentFingerprint = CoopTrailPackageFingerprint.Compute(stagingRoot, fingerprintFiles),
                };
                CoopTrailPackageManifestJson.WriteAtomic(Path.Combine(stagingRoot, "cooptrail.json"), manifest);
                CoopTrailPackage package = CoopTrailPackageCatalog.Load(stagingRoot);
                var resolver = new MissionAssetResolver();
                foreach (LoadedMission mission in package.Missions)
                    resolver.Resolve(mission);
                return new PreparedPackage(stagingRoot, package);
            }
            catch
            {
                if (Directory.Exists(stagingRoot))
                    Directory.Delete(stagingRoot, true);
                throw;
            }
        }

        private static CoopMissionDefinition CreateDefinition(
            string trailPath,
            int ordinal,
            string missionsRoot,
            Func<string, ModSettingsDefinition> readModSettings)
        {
            FileHeader container = MapFileManager.Instance.GetFileInfoFromFileName(
                trailPath,
                trailPath,
                4,
                loadRestartInfo: true);
            HUD_IngameMenu.RestartSkirmishMapInfo restart = container?.restartSkirmishInfo;
            if (restart?.selectedHeader == null || restart.MPsetupData == null)
                throw new InvalidDataException("Mission " + ordinal + " has no complete saved skirmish setup.");

            List<int> activeSlots = Enumerable.Range(0, Math.Min(8, restart.lordTypes?.Count ?? 0))
                .Where(index => restart.lordTypes[index] != -9999)
                .ToList();
            if (activeSlots.Count < 2)
                throw new InvalidDataException("Mission " + ordinal + " needs at least two occupied player slots for Coop.");

            string assetRoot = Path.Combine(missionsRoot, "Assets", ordinal.ToString("00"));
            Directory.CreateDirectory(assetRoot);
            var definition = new CoopMissionDefinition
            {
                SchemaVersion = MissionLoader.CurrentSchemaVersion,
                DisplayName = string.IsNullOrWhiteSpace(restart.selectedHeader.display_filename)
                    ? restart.selectedHeader.fileName
                    : restart.selectedHeader.display_filename,
                Description = string.Empty,
                Map = CreateMapReference(restart.selectedHeader, assetRoot, ordinal),
                Settings = CreateSettings(restart.MPsetupData),
                Players = new List<PlayerDefinition>(),
                ModSettings = readModSettings != null ? readModSettings(trailPath) : ReadModSettings(trailPath),
            };

            for (int activeIndex = 0; activeIndex < activeSlots.Count; activeIndex++)
            {
                int slot = activeSlots[activeIndex];
                int keepPosition = FindKeepPosition(restart.MPsetupData.start_keep_location_order, slot);
                int team = restart.teams != null && slot < restart.teams.Count ? restart.teams[slot] : 1;
                int colour = restart.colours != null && slot < restart.colours.Count ? restart.colours[slot] : slot;
                var player = new PlayerDefinition
                {
                    Active = true,
                    KeepPosition = keepPosition,
                    Team = Math.Max(1, Math.Min(8, team)),
                    Colour = Math.Max(0, Math.Min(7, colour)),
                };
                if (activeIndex >= 2)
                    PopulateAi(player, restart, slot, assetRoot, activeIndex + 1);
                definition.Players.Add(player);
            }
            return definition;
        }

        private static MapReference CreateMapReference(FileHeader header, string assetRoot, int ordinal)
        {
            if (header.builtinMap)
                return new MapReference { Source = "builtIn", Name = header.fileName };
            string source = RequireFile(header.filePath, ".map", "map for mission " + ordinal);
            string target = Path.Combine(assetRoot, "map.map");
            File.Copy(source, target, true);
            return new MapReference { Source = "bundled", File = ToMissionRelative(target) };
        }

        private static CoopSettings CreateSettings(EngineInterface.MultiplayerSetupData setup)
        {
            bool barracks = setup.MP_BuildingsAvailable != null && setup.MP_BuildingsAvailable.Length > 0 && setup.MP_BuildingsAvailable[0] != 0;
            bool mercenaryPost = setup.MP_BuildingsAvailable != null && setup.MP_BuildingsAvailable.Length > 1 && setup.MP_BuildingsAvailable[1] != 0;
            bool stockade = setup.MP_BuildingsAvailable != null && setup.MP_BuildingsAvailable.Length > 2 && setup.MP_BuildingsAvailable[2] != 0;
            return new CoopSettings
            {
                Fairness = Math.Max(1, Math.Min(5, setup.fairness)),
                StartingGoodsLevel = Math.Max(1, Math.Min(4, setup.starting_goods_level)),
                AllowBarracksHost = barracks,
                AllowMercenaryPostHost = mercenaryPost,
                AllowStockadeHost = stockade,
                AllowBarracksGuest = barracks,
                AllowMercenaryPostGuest = mercenaryPost,
                AllowStockadeGuest = stockade,
            };
        }

        private static void PopulateAi(
            PlayerDefinition player,
            HUD_IngameMenu.RestartSkirmishMapInfo restart,
            int slot,
            string assetRoot,
            int playerNumber)
        {
            FRONT_Multiplayer.MPAIVInfo info = restart.aivs != null && slot < restart.aivs.Length ? restart.aivs[slot] : null;
            if (info == null)
                throw new InvalidDataException("AI player " + playerNumber + " has no saved AIV information.");

            int baseLordId = Math.Max(0, info.lordType);
            if (info.builtInLord)
            {
                player.Lord = new LordReference { Source = "builtIn", Id = baseLordId, BaseLordId = baseLordId };
            }
            else
            {
                string lordSource = ResolveLordConfigFile(info);
                string lordTarget = Path.Combine(assetRoot, "lord-" + playerNumber + ".lordjson");
                File.Copy(lordSource, lordTarget, true);
                player.Lord = new LordReference
                {
                    Source = "bundled",
                    Name = info.lordName,
                    File = ToMissionRelative(lordTarget),
                    BaseLordId = baseLordId,
                };
            }

            player.Aivs = new List<AivReference>();
            if (!info.builtIn && info.aivs != null)
            {
                for (int index = 0; index < info.aivs.Count; index++)
                {
                    CustomisationFileManager.CustomAIV aiv = info.aivs[index];
                    if (aiv.builtIn)
                    {
                        List<CustomisationFileManager.CustomAIV> builtIns = CustomisationFileManager.Instance.getLordAIVList(baseLordId);
                        int builtInIndex = builtIns?.FindIndex(candidate => candidate.checksum == aiv.checksum ||
                            string.Equals(candidate.AIVName, aiv.AIVName, StringComparison.OrdinalIgnoreCase)) ?? -1;
                        if (builtInIndex < 0)
                            throw new InvalidDataException("Built-in AIV could not be identified for AI player " + playerNumber + ".");
                        player.Aivs.Add(new AivReference { Source = "builtIn", Id = builtInIndex, Rotation = NormalizeRotation(info.rotation) });
                    }
                    else
                    {
                        string aivSource = ResolveAivFile(info, aiv, baseLordId);
                        string aivTarget = Path.Combine(assetRoot, "aiv-" + playerNumber + "-" + (index + 1) + ".aivjson");
                        File.Copy(aivSource, aivTarget, true);
                        player.Aivs.Add(new AivReference
                        {
                            Source = "bundled",
                            File = ToMissionRelative(aivTarget),
                            Rotation = NormalizeRotation(info.rotation),
                        });
                    }
                }
            }
            player.PreferredAiv = -1;
        }

        private static string ResolveLordConfigFile(FRONT_Multiplayer.MPAIVInfo info)
        {
            CustomisationFileManager.CustomLordConfig config = info.lordConfig;
            string direct = ResolveNamedFile(config?.path, config?.name, ".lordjson");
            if (direct != null)
                return direct;
            List<CustomisationFileManager.CustomLordConfig> installed =
                CustomisationFileManager.Instance.getLordLordList(-1, info.lordName);
            CustomisationFileManager.CustomLordConfig match = installed?.FirstOrDefault(candidate =>
                (config != null && candidate.checksum == config.checksum) ||
                string.Equals(candidate.name, config?.name, StringComparison.OrdinalIgnoreCase));
            return RequireFile(ResolveNamedFile(match?.path, match?.name, ".lordjson"), ".lordjson", "custom lord " + info.lordName);
        }

        private static string ResolveAivFile(FRONT_Multiplayer.MPAIVInfo info, CustomisationFileManager.CustomAIV aiv, int baseLordId)
        {
            string direct = ResolveNamedFile(aiv.path, aiv.AIVName, ".aivjson");
            if (direct != null)
                return direct;
            List<CustomisationFileManager.CustomAIV> installed = info.builtInLord
                ? CustomisationFileManager.Instance.getLordAIVList(baseLordId)
                : CustomisationFileManager.Instance.getLordAIVList(-1, info.lordName);
            CustomisationFileManager.CustomAIV match = installed?.FirstOrDefault(candidate =>
                candidate.checksum == aiv.checksum || string.Equals(candidate.AIVName, aiv.AIVName, StringComparison.OrdinalIgnoreCase));
            return RequireFile(ResolveNamedFile(match?.path, match?.AIVName, ".aivjson"), ".aivjson", "AIV " + aiv.AIVName);
        }

        private static string ResolveNamedFile(string path, string name, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            if (File.Exists(path) && string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                return path;
            string combined = Path.Combine(path, (name ?? string.Empty) + extension);
            return File.Exists(combined) ? combined : null;
        }

        private static int NormalizeRotation(int nativeRotation)
        {
            int normalized = ((nativeRotation % 4) + 4) % 4;
            return normalized * 90;
        }

        private static int FindKeepPosition(int[] keepOrder, int slot)
        {
            if (keepOrder != null)
            {
                for (int keep = 0; keep < keepOrder.Length; keep++)
                {
                    if (keepOrder[keep] == slot)
                        return keep + 1;
                }
            }
            throw new InvalidDataException("No keep position is assigned to occupied player slot " + (slot + 1) + ".");
        }

        private static ModSettingsDefinition ReadModSettings(string trailPath)
        {
            string sidecar = Path.ChangeExtension(trailPath, ".modjson");
            return File.Exists(sidecar) ? ModSettingsJson.Read(sidecar) : ModSettingsDefinition.CreateUnmanaged();
        }

        private static string ReadExistingPackageId(string destination)
        {
            string manifestPath = Path.Combine(destination, "cooptrail.json");
            if (!File.Exists(manifestPath))
                return null;
            try
            {
                CoopTrailPackageManifest manifest = CoopTrailPackageManifestJson.Read(manifestPath);
                CoopTrailPackageManifestJson.Validate(manifest);
                return manifest.PackageId;
            }
            catch
            {
                return null;
            }
        }

        private static string RequireFile(string path, string extension, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException("Could not locate " + label + ".", path);
            }
            return Path.GetFullPath(path);
        }

        private static string ToMissionRelative(string path)
        {
            string missionsRoot = Directory.GetParent(Directory.GetParent(Directory.GetParent(path).FullName).FullName).FullName;
            return path.Substring(missionsRoot.TrimEnd(Path.DirectorySeparatorChar).Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}

using APIShared.Maps;
using ExtendedData.Core;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    // Schema 5 export: every mission bundles an unchanged copy of its Trail (AIC, ordered castle candidates,
    // flags, rotation and portraits of every AI slot) and the map extracted from it. Neither the original map
    // nor the author's Lord installation is a source of these data any more.
    internal sealed class CoopTrailPackageExporter
    {
        internal const string BundledTrailFileName = "mission.trail";
        internal const string BundledMapFileName = "map.map";

        // Lord requirements for one Maker Trail in its original player IDs, restricted to the given AI IDs.
        internal delegate IReadOnlyList<TrailLordSlot> LordRequirementsResolver(string trailPath,
            HUD_IngameMenu.RestartSkirmishMapInfo restart, ISet<int> aiPlayerIds);

        internal sealed class PreparedPackage : IDisposable
        {
            private readonly string stagingRoot;

            internal PreparedPackage(string stagingRoot, CoopTrailPackage package)
            {
                this.stagingRoot = stagingRoot;
                Package = package;
            }

            internal CoopTrailPackage Package { get; }

            // Editable Maker sources are staged here and replaced together with the missions.
            internal string TrailMakerSourceStaging =>
                Path.Combine(stagingRoot, CoopTrailPackagePublication.TrailMakerSourceDirectory);

            internal void Publish(string destination)
            {
                CoopTrailPackagePublication.Publish(stagingRoot, destination, Package.Manifest);
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
            Func<string, ModSettingsDefinition> readModSettings,
            LordRequirementsResolver resolveLordRequirements)
        {
            if (resolveLordRequirements == null)
                throw new ArgumentNullException(nameof(resolveLordRequirements));
            string customTrailsRoot = Path.GetDirectoryName(Path.GetFullPath(destination));
            string stagingRoot = Path.Combine(customTrailsRoot, ".cooptrail-build-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingRoot);
            try
            {
                string missionsRoot = Path.Combine(stagingRoot, CoopTrailPackagePublication.MissionsDirectory);
                Directory.CreateDirectory(missionsRoot);
                var jsonPaths = new List<string>();
                var trailPathsByOrdinal = new Dictionary<int, string>();
                int ordinal = 0;
                for (int sourceIndex = 0; sourceIndex < 50 && ordinal < 40; sourceIndex++)
                {
                    string makerName = FRONT_ManageTrail.GetMakerFileName(sourceIndex);
                    string trailPath = Path.Combine(makerRoot, makerName + ".trail");
                    if (!File.Exists(trailPath))
                        continue;
                    ordinal++;
                    trailPathsByOrdinal[ordinal] = trailPath;
                    string jsonPath = Path.Combine(missionsRoot, ordinal.ToString("00") + ".coopmission.json");
                    try
                    {
                        CoopMissionDefinition definition = CreateDefinition(trailPath, ordinal, missionsRoot,
                            resolveLordRequirements, out IReadOnlyList<TrailLordSlot> lordSlots);
                        MissionLoader.WriteAtomic(jsonPath, definition);
                        TrailLordRequirements.Create(jsonPath, lordSlots).Write(jsonPath);
                        ModSettingsDefinition modSettings = readModSettings != null
                            ? readModSettings(trailPath)
                            : ReadModSettings(trailPath);
                        if (modSettings?.Mods?.Count > 0)
                            ModSettingsJson.WriteAtomic(MissionLoader.GetModSettingsPath(jsonPath), modSettings);
                    }
                    catch (Exception exception)
                    {
                        throw new CoopMissionExportException(ordinal, trailPath, exception);
                    }
                    jsonPaths.Add(jsonPath);
                }
                if (ordinal == 0)
                    throw new InvalidDataException("The Trail Maker contains no missions to export as a Coop Trail.");

                string packageId = ReadExistingPackageId(destination) ?? Guid.NewGuid().ToString("D");
                var fingerprintFiles = new List<string>(jsonPaths);
                var loader = new MissionLoader();
                for (int index = 0; index < jsonPaths.Count; index++)
                {
                    LoadedMission loaded;
                    try
                    {
                        loaded = loader.Load(jsonPaths[index], (index / 10) + 1, (index % 10) + 1);
                    }
                    catch (Exception exception)
                    {
                        throw new CoopMissionExportException(index + 1, trailPathsByOrdinal[index + 1], exception);
                    }
                    fingerprintFiles.AddRange(loaded.BundledFiles);
                    if (loaded.ModSettingsPath != null)
                        fingerprintFiles.Add(loaded.ModSettingsPath);
                    fingerprintFiles.Add(TrailLordRequirements.SidecarPath(jsonPaths[index]));
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
                // Resolve exactly as the runtime will: map header, embedded slots and portraits.
                var resolver = new MissionAssetResolver();
                foreach (LoadedMission mission in package.Missions)
                {
                    int missionOrdinal = ((mission.TrailNumber - 1) * 10) + mission.MissionNumber;
                    try
                    {
                        resolver.Resolve(mission);
                    }
                    catch (Exception exception)
                    {
                        throw new CoopMissionExportException(missionOrdinal,
                            trailPathsByOrdinal.TryGetValue(missionOrdinal, out string source) ? source : mission.JsonPath,
                            exception);
                    }
                }
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
            LordRequirementsResolver resolveLordRequirements,
            out IReadOnlyList<TrailLordSlot> lordSlots)
        {
            string assetRoot = Path.Combine(missionsRoot, "Assets", ordinal.ToString("00"));
            Directory.CreateDirectory(assetRoot);
            // One read: the bundled copy, the extracted map and the decoded setup all describe these bytes.
            byte[] trailBytes = File.ReadAllBytes(trailPath);
            byte[] restartBytes = TrailContainer.ReadRestartData(trailBytes);
            string bundledTrail = Path.Combine(assetRoot, BundledTrailFileName);
            string bundledMap = Path.Combine(assetRoot, BundledMapFileName);
            File.WriteAllBytes(bundledTrail, trailBytes);
            File.WriteAllBytes(bundledMap, TrailContainer.ExtractMap(trailBytes));

            // Vanilla's Trail decoding tolerates a missing original map (customTrailMission=true).
            FileHeader container = MapFileManager.Instance.GetFileInfoFromFileName(
                bundledTrail, bundledTrail, 4, loadRestartInfo: true);
            HUD_IngameMenu.RestartSkirmishMapInfo restart = container?.restartSkirmishInfo;
            if (restart?.MPsetupData == null || restart.aivs == null)
                throw new InvalidDataException("Mission " + ordinal + " has no complete saved skirmish setup.");
            if (restart.MPsetupData.preferredAIVs == null || restart.MPsetupData.preferredAIVs.Length != 8)
                throw new InvalidDataException("Saved multiplayer setup has no complete preferred-AIV array.");
            List<int> activeSlots = CoopTrailSlotSelection.GetActiveSlots(restart.lordTypes);
            IReadOnlyList<TrailLordSlot> makerSlots = resolveLordRequirements(trailPath, restart,
                CoopTrailSlotSelection.GetAiPlayerIds(activeSlots));
            lordSlots = CoopTrailSlotSelection.RemapForCoop(makerSlots, activeSlots);

            TrailRestartMapReference original = TrailRestartMapReference.Read(restartBytes);
            string trailReference = ToMissionRelative(bundledTrail, missionsRoot);
            var definition = new CoopMissionDefinition
            {
                SchemaVersion = MissionLoader.CurrentSchemaVersion,
                DisplayName = DisplayName(restart.selectedHeader, original),
                Description = string.Empty,
                Map = new MapReference
                {
                    Source = "bundled",
                    File = ToMissionRelative(bundledMap, missionsRoot),
                    OriginalSource = original.Source,
                    OriginalName = original.Name,
                },
                Settings = CreateSettings(restart.MPsetupData),
                Players = new List<PlayerDefinition>(),
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
                    Colour = Math.Max(1, Math.Min(8, colour)),
                };
                if (activeIndex >= 2)
                    PopulateEmbeddedAi(player, restart, slot, trailReference, activeIndex + 1);
                definition.Players.Add(player);
            }
            MissionProjection projection = MissionProjection.Create(definition);
            for (int index = 0; index < projection.ActivePlayers.Count; index++)
                projection.ActivePlayers[index].Team = projection.Teams[index];
            return definition;
        }

        // The technical file name map.map is never the mission title. Prefer the installed original's
        // translated title, otherwise the name saved in the Trail.
        private static string DisplayName(FileHeader originalHeader, TrailRestartMapReference original)
        {
            if (!string.IsNullOrWhiteSpace(originalHeader?.display_filename))
                return originalHeader.display_filename;
            if (!string.IsNullOrWhiteSpace(originalHeader?.fileName))
                return originalHeader.fileName;
            if (!string.IsNullOrWhiteSpace(original.Name))
                return Path.GetFileNameWithoutExtension(original.Name);
            throw new InvalidDataException("The Trail has no original map name.");
        }

        private static void PopulateEmbeddedAi(
            PlayerDefinition player,
            HUD_IngameMenu.RestartSkirmishMapInfo restart,
            int slot,
            string trailReference,
            int playerNumber)
        {
            FRONT_Multiplayer.MPAIVInfo info = slot < restart.aivs.Length ? restart.aivs[slot] : null;
            if (info == null)
                throw new InvalidDataException("AI player " + playerNumber + " has no saved AIV information.");
            player.Lord = new LordReference
            {
                Source = MissionLoader.EmbeddedTrailSource,
                File = trailReference,
                SourcePlayerId = slot + 1,
                BaseLordId = Math.Max(0, info.lordType),
                // Display and diagnostics only; the slot itself is the source.
                Name = info.builtInLord || string.IsNullOrWhiteSpace(info.lordName) ? null : info.lordName,
            };
            player.Aivs = new List<AivReference>();
            player.PreferredAiv = -1;
            player.NativePreferredAiv = restart.MPsetupData.preferredAIVs[slot];
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
                MultiplayerSetup = new MultiplayerSetupSettings
                {
                    StartingGameSpeed = setup.starting_gamespeed,
                    WinCondition = setup.win_condition,
                    AllowAutoTrading = setup.allow_autotrading,
                    NoKnockdownWalls = setup.no_knockdown_walls,
                    AutoSave = setup.autosave,
                    PeaceTime = setup.peacetime,
                    NoCows = setup.no_cows,
                    NoDogs = setup.no_dogs,
                    ExtremeTroops = setup.extreme_troops,
                    ExtremePowers = setup.extreme_powers,
                    ExtremePowersAroundLord = setup.extreme_powers_around_lord,
                    AllowOutposts = setup.allow_outposts,
                    AdvancedOptions = setup.advanced_options,
                    AdvancedSkirmishOptions = setup.advanced_skirmish_options,
                    PreBuild = setup.advopt_pre_build,
                    ImprovedArabSwordsmen = setup.advopt_improved_arabswordsmen,
                    ImprovedLaddermen = setup.advopt_improved_laddermen,
                    ImprovedSpearmen = setup.advopt_improved_spearmen,
                    RebalancedHorseArchers = setup.advopt_rebalanced_horsearchers,
                    ImprovedFletchers = setup.advopt_improved_fletchers,
                    UncappedPeasants = setup.advopt_uncapped_peasants,
                    FasterPeasants = setup.advopt_faster_peasants,
                    EnemyHitPoints = setup.advopt_enemy_hps,
                    ImprovedSieging = setup.global_improved_sieging,
                    Healers = setup.advopt_healers,
                    Eunuchs = setup.advopt_eunuchs,
                    NoGold = setup.advopt_nogold,
                    ImprovedSieging2 = setup.global_improved_sieging2,
                    BuildingsAvailable = CopyRequiredArray(setup.MP_BuildingsAvailable, 13, "buildings"),
                    GoodsAvailable = CopyRequiredArray(setup.MP_GoodsAvailable, 25, "goods"),
                    TroopsAvailable = CopyRequiredArray(setup.MP_TroopsAvailable, 32, "troops"),
                },
            };
        }

        private static int[] CopyRequiredArray(int[] source, int expectedLength, string label)
        {
            if (source == null || source.Length != expectedLength)
                throw new InvalidDataException("Saved multiplayer setup has no complete " + label + " availability array.");
            return source.ToArray();
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
            string sidecar = MissionLoader.GetTrailModSettingsPath(trailPath);
            return File.Exists(sidecar) ? ModSettingsJson.Read(sidecar) : ModSettingsDefinition.CreateModDefaults();
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

        private static string ToMissionRelative(string path, string missionsRoot)
        {
            string root = Path.GetFullPath(missionsRoot).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A bundled mission asset lies outside the missions directory.");
            return full.Substring(root.Length + 1)
                .Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}

using ExtendedData.Core;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ExtendedData
{
    internal sealed class MissionAssetResolver
    {
        private static readonly MethodInfo ProcessLordFileMethod = typeof(CustomisationFileManager).GetMethod(
            "ProcessExtendedLordFile",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public ResolvedMission Resolve(LoadedMission loaded)
        {
            CoopMissionDefinition definition = loaded.Definition;
            MissionProjection projection = MissionProjection.Create(definition);
            IReadOnlyList<PlayerDefinition> players = projection.ActivePlayers;
            FileHeader header = ResolveMap(loaded, definition.Map, definition.DisplayName, out string mapLabel);
            // mapType 1 is Vanilla's shared skirmish/multiplayer category; a readable map of
            // another type is a content error, not an unreadable file.
            if (header.mapType != 1)
                throw new MissionAssetException(MissionAssetException.MapNotMultiplayer, mapLabel,
                    "Map [" + mapLabel + "] is not a skirmish/multiplayer map (mapType=" + header.mapType + ").");
            if (header.maxPlayers < players.Count)
                throw new MissionAssetException(MissionAssetException.MapTooFewPlayers, mapLabel,
                    "Map [" + mapLabel + "] supports only " + header.maxPlayers + " players, mission needs " + players.Count + ".");

            var resolved = new ResolvedMission { Loaded = loaded, Header = header };

            var aiIds = new List<int>();
            var preferredAivs = new List<int>();
            var decodedTrails = new Dictionary<string, HUD_IngameMenu.RestartSkirmishMapInfo>(StringComparer.OrdinalIgnoreCase);
            for (int index = 2; index < players.Count; index++)
            {
                PlayerDefinition player = players[index];
                FRONT_Multiplayer.MPAIVInfo info = string.Equals(player.Lord.Source,
                        MissionLoader.EmbeddedTrailSource, StringComparison.Ordinal)
                    ? ResolveEmbedded(loaded, player, decodedTrails)
                    : ResolveAi(loaded, player);
                resolved.AiInfoByPlayerIndex[index] = info;
                aiIds.Add(MissionProjection.GetBaseLordId(player) + 1);
                int preferredAiv = player.NativePreferredAiv.Value;
                preferredAivs.Add(preferredAiv);
                resolved.PreferredAivByPlayerIndex[index] = preferredAiv;
            }

            CoopSettings settings = definition.Settings;
            resolved.CoopData = new FRONT_Multiplayer.CoopMissionSetupData
            {
                mapName = header.fileName,
                header = header,
                keepOrder = projection.KeepOrder,
                teams = projection.Teams,
                AIs = aiIds.ToArray(),
                AIVs = preferredAivs.ToArray(),
                fairness = settings.Fairness,
                starting_level = settings.StartingGoodsLevel,
                allowBarracksPlayer1 = settings.AllowBarracksHost ? 1 : 0,
                allowMercPostPlayer1 = settings.AllowMercenaryPostHost ? 1 : 0,
                allowStockadePlayer1 = settings.AllowStockadeHost ? 1 : 0,
                allowBarracksPlayer2 = settings.AllowBarracksGuest ? 1 : 0,
                allowMercPostPlayer2 = settings.AllowMercenaryPostGuest ? 1 : 0,
                allowStockadePlayer2 = settings.AllowStockadeGuest ? 1 : 0,
            };
            return resolved;
        }

        private static FileHeader ResolveMap(LoadedMission loaded, MapReference map, string displayName,
            out string label)
        {
            if (map.Source == "bundled")
            {
                string path = MissionLoader.ResolveBundledPath(loaded.MissionRoot, map.File, ".map");
                label = path;
                // Vanilla opens realFilePath (second argument); a bare file name would be
                // resolved against the game's working directory instead of the package.
                FileHeader bundled = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0);
                if (bundled == null)
                    throw new MissionAssetException(MissionAssetException.MapUnreadable, path,
                        "Bundled map [" + path + "] could not be read; it is damaged, truncated or not a map file.");
                // Every exported map is stored as map.map; the mission name is the meaningful title.
                if (!string.IsNullOrWhiteSpace(displayName))
                    bundled.display_filename = displayName;
                return bundled;
            }
            label = map.Name ?? map.Id?.ToString();
            FileHeader installed = MapFileManager.Instance.GetHeaderFromFileNameMP(label);
            if (installed == null)
                throw new MissionAssetException(MissionAssetException.MapMissing, label,
                    "Multiplayer map [" + label + "] is not installed.");
            return installed;
        }

        private static FRONT_Multiplayer.MPAIVInfo ResolveAi(LoadedMission loaded, PlayerDefinition player)
        {
            LordReference lord = player.Lord;
            int baseLordId = MissionProjection.GetBaseLordId(player);
            var info = new FRONT_Multiplayer.MPAIVInfo();
            info.Init(baseLordId, lord.Source == "builtIn" ? string.Empty : (lord.Name ?? Path.GetFileNameWithoutExtension(lord.File)));
            info.lordType = baseLordId;

            if (lord.Source != "builtIn")
            {
                info.builtInLord = false;
                info.lordConfig = ResolveLordConfig(loaded, lord, baseLordId);
                if (info.lordConfig == null)
                    throw new MissionAssetException(MissionAssetException.LordUnresolved,
                        lord.File ?? lord.Name, "Lord configuration [" + (lord.File ?? lord.Name) + "] could not be resolved.");
            }

            var aivs = new List<CustomisationFileManager.CustomAIV>();
            foreach (AivReference reference in player.Aivs)
                aivs.Add(ResolveAiv(loaded, lord, baseLordId, reference));
            if (player.PreferredAiv >= 0)
                aivs = new List<CustomisationFileManager.CustomAIV> { aivs[player.PreferredAiv] };

            if (aivs.Count > 0)
            {
                info.builtIn = false;
                info.community = false;
                info.historical = false;
                info.aivs.Clear();
                info.aivs.AddRange(aivs);
                int selectedIndex = player.PreferredAiv >= 0 ? player.PreferredAiv : 0;
                info.rotation = player.Aivs[selectedIndex].Rotation / 90;
            }
            else
            {
                // Init(nonEmptyName) clears builtIn. Without explicit references the AI uses Vanilla's
                // standard castles; a custom AIC is handed over separately at start.
                info.builtIn = true;
                info.community = false;
                info.historical = false;
            }
            return info;
        }

        // Reads AIC, ordered castle candidates, castle flags, rotation and portrait of one saved slot from
        // the bundled Trail with Vanilla's own decoder. The original map is not needed. Unity thread only:
        // the decoder loads portraits into textures.
        private static FRONT_Multiplayer.MPAIVInfo ResolveEmbedded(LoadedMission loaded, PlayerDefinition player,
            Dictionary<string, HUD_IngameMenu.RestartSkirmishMapInfo> decoded)
        {
            LordReference lord = player.Lord;
            string path = MissionLoader.ResolveBundledPath(loaded.MissionRoot, lord.File, ".trail");
            if (!decoded.TryGetValue(path, out HUD_IngameMenu.RestartSkirmishMapInfo restart))
            {
                FileHeader container = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 4, loadRestartInfo: true);
                restart = container?.restartSkirmishInfo;
                if (restart?.aivs == null || restart.lordTypes == null ||
                    restart.MPsetupData?.preferredAIVs == null || restart.MPsetupData.preferredAIVs.Length != 8)
                    throw new MissionAssetException(MissionAssetException.EmbeddedTrailUnreadable, path,
                        "Bundled Trail [" + path + "] has no readable saved Lord setup.");
                decoded.Add(path, restart);
            }
            int slot = lord.SourcePlayerId.Value - 1;
            FRONT_Multiplayer.MPAIVInfo saved = slot < restart.aivs.Length ? restart.aivs[slot] : null;
            // importMembers stores -1 for humans and -9999 for empty slots; only AI slots carry a Lord.
            if (saved == null || slot >= restart.lordTypes.Count || restart.lordTypes[slot] < 0)
                throw new MissionAssetException(MissionAssetException.EmbeddedSlotInvalid, path,
                    "Bundled Trail [" + path + "] has no saved AI in player slot " + lord.SourcePlayerId + ".");
            if (saved.lordType != lord.BaseLordId || restart.MPsetupData.preferredAIVs[slot] != player.NativePreferredAiv)
                throw new MissionAssetException(MissionAssetException.EmbeddedSlotInvalid, path,
                    "Player slot " + lord.SourcePlayerId + " of [" + path + "] no longer matches its mission entry.");
            if (!saved.builtInLord && saved.lordConfig == null)
                throw new MissionAssetException(MissionAssetException.EmbeddedSlotInvalid, path,
                    "Player slot " + lord.SourcePlayerId + " of [" + path + "] has a custom AIC without configuration.");
            return CoopMissionDataCopies.CopyLord(saved);
        }

        private static CustomisationFileManager.CustomLordConfig ResolveLordConfig(LoadedMission loaded, LordReference reference, int baseLordId)
        {
            if (reference.Source == "bundled")
            {
                string path = MissionLoader.ResolveBundledPath(loaded.MissionRoot, reference.File, ".lordjson");
                CustomisationFileManager.CustomLord holder = ParseBundled(baseLordId, reference.Name, path);
                return holder.configs.SingleOrDefault();
            }

            List<CustomisationFileManager.CustomLordConfig> configs = CustomisationFileManager.Instance.getLordLordList(-1, reference.Name);
            if (configs == null)
                return null;
            CustomisationFileManager.CustomLordConfig config = string.IsNullOrWhiteSpace(reference.Configuration)
                ? configs.FirstOrDefault()
                : configs.FirstOrDefault(item => string.Equals(item.name, reference.Configuration, StringComparison.OrdinalIgnoreCase));
            return config;
        }

        private static CustomisationFileManager.CustomAIV ResolveAiv(LoadedMission loaded, LordReference lord, int baseLordId, AivReference reference)
        {
            if (reference.Source == "bundled")
            {
                string path = MissionLoader.ResolveBundledPath(loaded.MissionRoot, reference.File, ".aivjson");
                CustomisationFileManager.CustomLord holder = ParseBundled(baseLordId, lord.Name, path);
                return holder.aivs.Single();
            }

            string installedLordName = reference.LordName ?? lord.Name ?? string.Empty;
            List<CustomisationFileManager.CustomAIV> list = reference.Source == "builtIn"
                ? CustomisationFileManager.Instance.getLordAIVList(baseLordId)
                : CustomisationFileManager.Instance.getLordAIVList(-1, installedLordName);
            if (list == null)
                throw new MissionAssetException(MissionAssetException.AivMissing, installedLordName,
                    "AIV catalogue was not found for " + installedLordName + ".");
            CustomisationFileManager.CustomAIV aiv = reference.Id.HasValue
                ? (reference.Id.Value >= 0 && reference.Id.Value < list.Count ? list[reference.Id.Value] : null)
                : list.FirstOrDefault(item => string.Equals(item.AIVName, reference.Name, StringComparison.OrdinalIgnoreCase));
            if (aiv == null)
                throw new MissionAssetException(MissionAssetException.AivMissing,
                    installedLordName + " (" + (reference.Name ?? reference.Id?.ToString()) + ")",
                    "AIV was not found: " + (reference.Name ?? reference.Id?.ToString()) + ".");
            return aiv;
        }

        private static CustomisationFileManager.CustomLord ParseBundled(int lordType, string name, string path)
        {
            // Reuse the game's loader so bundled assets receive the native CRC and field mapping.
            if (ProcessLordFileMethod == null)
                throw new MissingMethodException(typeof(CustomisationFileManager).FullName, "ProcessExtendedLordFile");
            var holder = new CustomisationFileManager.CustomLord
            {
                lordType = lordType,
                lordName = name ?? Path.GetFileNameWithoutExtension(path),
                lordDisplayName = name ?? Path.GetFileNameWithoutExtension(path),
                customPath = Path.GetDirectoryName(path),
            };
            ProcessLordFileMethod.Invoke(CustomisationFileManager.Instance, new object[] { path.ToLowerInvariant(), path, holder, false });
            return holder;
        }
    }
}

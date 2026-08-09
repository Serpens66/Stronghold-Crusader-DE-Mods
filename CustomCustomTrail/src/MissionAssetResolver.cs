using CustomCustomTrail.Core;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CustomCustomTrail
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
            FileHeader header = ResolveMap(loaded, definition.Map);
            if (header == null || header.mapType != 1)
                throw new InvalidDataException("The selected map is not a multiplayer skirmish map.");
            if (header.maxPlayers < players.Count)
                throw new InvalidDataException("Map supports only " + header.maxPlayers + " players, mission needs " + players.Count + ".");

            var resolved = new ResolvedMission { Loaded = loaded, Header = header };

            var aiIds = new List<int>();
            var preferredAivs = new List<int>();
            for (int index = 2; index < players.Count; index++)
            {
                PlayerDefinition player = players[index];
                FRONT_Multiplayer.MPAIVInfo info = ResolveAi(loaded, player);
                resolved.AiInfoByPlayerIndex[index] = info;
                aiIds.Add(MissionProjection.GetBaseLordId(player) + 1);
                int rotation = player.PreferredAiv >= 0
                    ? player.Aivs[player.PreferredAiv].Rotation
                    : (player.Aivs.Count > 0 ? player.Aivs[0].Rotation : 0);
                preferredAivs.Add(-(rotation / 90) - 1);
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

        private static FileHeader ResolveMap(LoadedMission loaded, MapReference map)
        {
            if (map.Source == "bundled")
            {
                string path = MissionLoader.ResolveBundledPath(loaded.MissionRoot, map.File, ".map");
                return MapFileManager.Instance.GetFileInfoFromFileName(path, Path.GetFileName(path), 0);
            }
            return MapFileManager.Instance.GetHeaderFromFileNameMP(map.Name ?? map.Id?.ToString());
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
                    throw new InvalidDataException("Lord configuration could not be resolved.");
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
            return info;
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
                throw new InvalidDataException("AIV catalogue was not found for " + installedLordName + ".");
            CustomisationFileManager.CustomAIV aiv = reference.Id.HasValue
                ? (reference.Id.Value >= 0 && reference.Id.Value < list.Count ? list[reference.Id.Value] : null)
                : list.FirstOrDefault(item => string.Equals(item.AIVName, reference.Name, StringComparison.OrdinalIgnoreCase));
            if (aiv == null)
                throw new InvalidDataException("AIV was not found: " + (reference.Name ?? reference.Id?.ToString()) + ".");
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData.Core
{
    internal static class MissionDefinitionJson
    {
        public static CoopMissionDefinition Parse(string json)
        {
            Dictionary<string, object> root = RequireObject(Shared.DependencyFreeJson.Parse(json), "Mission JSON root");
            var mission = new CoopMissionDefinition
            {
                SchemaVersion = RequiredInt(root, "schemaVersion"),
                DisplayName = RequiredString(root, "displayName"),
                Description = OptionalString(root, "description", string.Empty),
                Map = ParseMap(RequiredObject(root, "map")),
                Players = RequiredArray(root, "players").Select(ParsePlayer).ToList(),
            };

            if (root.TryGetValue("settings", out object settingsValue) && settingsValue != null)
                mission.Settings = ParseSettings(RequireObject(settingsValue, "settings"));
            if (root.ContainsKey("modSettings"))
                throw new InvalidDataException("Embedded modSettings are not supported; use the matching .modtrail.json sidecar.");

            return mission;
        }

        public static string Serialize(CoopMissionDefinition mission)
        {
            var root = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = mission.SchemaVersion,
                ["displayName"] = mission.DisplayName,
                ["description"] = mission.Description,
                ["map"] = WriteAsset(mission.Map),
                ["settings"] = WriteSettings(mission.Settings),
                ["players"] = mission.Players.Select(WritePlayer).Cast<object>().ToList(),
            };
            return Shared.DependencyFreeJson.Serialize(root);
        }

        private static CoopSettings ParseSettings(Dictionary<string, object> value)
        {
            var settings = new CoopSettings();
            OptionalInt(value, "fairness", result => settings.Fairness = result);
            OptionalInt(value, "startingGoodsLevel", result => settings.StartingGoodsLevel = result);
            OptionalBool(value, "allowBarracksHost", result => settings.AllowBarracksHost = result);
            OptionalBool(value, "allowMercenaryPostHost", result => settings.AllowMercenaryPostHost = result);
            OptionalBool(value, "allowStockadeHost", result => settings.AllowStockadeHost = result);
            OptionalBool(value, "allowBarracksGuest", result => settings.AllowBarracksGuest = result);
            OptionalBool(value, "allowMercenaryPostGuest", result => settings.AllowMercenaryPostGuest = result);
            OptionalBool(value, "allowStockadeGuest", result => settings.AllowStockadeGuest = result);
            if (value.TryGetValue("multiplayerSetup", out object setupValue) && setupValue != null)
                settings.MultiplayerSetup = ParseMultiplayerSetup(RequireObject(setupValue, "settings.multiplayerSetup"));
            return settings;
        }

        private static Dictionary<string, object> WriteSettings(CoopSettings settings)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["fairness"] = settings.Fairness,
                ["startingGoodsLevel"] = settings.StartingGoodsLevel,
                ["allowBarracksHost"] = settings.AllowBarracksHost,
                ["allowMercenaryPostHost"] = settings.AllowMercenaryPostHost,
                ["allowStockadeHost"] = settings.AllowStockadeHost,
                ["allowBarracksGuest"] = settings.AllowBarracksGuest,
                ["allowMercenaryPostGuest"] = settings.AllowMercenaryPostGuest,
                ["allowStockadeGuest"] = settings.AllowStockadeGuest,
            };
            if (settings.MultiplayerSetup != null)
                result["multiplayerSetup"] = WriteMultiplayerSetup(settings.MultiplayerSetup);
            return result;
        }

        private static MultiplayerSetupSettings ParseMultiplayerSetup(Dictionary<string, object> value)
        {
            return new MultiplayerSetupSettings
            {
                StartingGameSpeed = RequiredInt(value, "startingGameSpeed"),
                WinCondition = RequiredInt(value, "winCondition"),
                AllowAutoTrading = RequiredInt(value, "allowAutoTrading"),
                NoKnockdownWalls = RequiredInt(value, "noKnockdownWalls"),
                AutoSave = RequiredInt(value, "autoSave"),
                PeaceTime = RequiredInt(value, "peaceTime"),
                NoCows = RequiredInt(value, "noCows"),
                NoDogs = RequiredInt(value, "noDogs"),
                ExtremeTroops = RequiredInt(value, "extremeTroops"),
                ExtremePowers = RequiredInt(value, "extremePowers"),
                ExtremePowersAroundLord = RequiredInt(value, "extremePowersAroundLord"),
                AllowOutposts = RequiredInt(value, "allowOutposts"),
                AdvancedOptions = RequiredInt(value, "advancedOptions"),
                AdvancedSkirmishOptions = RequiredInt(value, "advancedSkirmishOptions"),
                PreBuild = RequiredInt(value, "preBuild"),
                ImprovedArabSwordsmen = RequiredInt(value, "improvedArabSwordsmen"),
                ImprovedLaddermen = RequiredInt(value, "improvedLaddermen"),
                ImprovedSpearmen = RequiredInt(value, "improvedSpearmen"),
                RebalancedHorseArchers = RequiredInt(value, "rebalancedHorseArchers"),
                ImprovedFletchers = RequiredInt(value, "improvedFletchers"),
                UncappedPeasants = RequiredInt(value, "uncappedPeasants"),
                FasterPeasants = RequiredInt(value, "fasterPeasants"),
                EnemyHitPoints = RequiredInt(value, "enemyHitPoints"),
                ImprovedSieging = RequiredInt(value, "improvedSieging"),
                Healers = RequiredInt(value, "healers"),
                Eunuchs = RequiredInt(value, "eunuchs"),
                NoGold = RequiredInt(value, "noGold"),
                ImprovedSieging2 = RequiredInt(value, "improvedSieging2"),
                BuildingsAvailable = RequiredArray(value, "buildingsAvailable").Select(item => RequireInt(item, "buildingsAvailable item")).ToArray(),
                GoodsAvailable = RequiredArray(value, "goodsAvailable").Select(item => RequireInt(item, "goodsAvailable item")).ToArray(),
                TroopsAvailable = RequiredArray(value, "troopsAvailable").Select(item => RequireInt(item, "troopsAvailable item")).ToArray(),
            };
        }

        private static Dictionary<string, object> WriteMultiplayerSetup(MultiplayerSetupSettings setup) =>
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["startingGameSpeed"] = setup.StartingGameSpeed,
                ["winCondition"] = setup.WinCondition,
                ["allowAutoTrading"] = setup.AllowAutoTrading,
                ["noKnockdownWalls"] = setup.NoKnockdownWalls,
                ["autoSave"] = setup.AutoSave,
                ["peaceTime"] = setup.PeaceTime,
                ["noCows"] = setup.NoCows,
                ["noDogs"] = setup.NoDogs,
                ["extremeTroops"] = setup.ExtremeTroops,
                ["extremePowers"] = setup.ExtremePowers,
                ["extremePowersAroundLord"] = setup.ExtremePowersAroundLord,
                ["allowOutposts"] = setup.AllowOutposts,
                ["advancedOptions"] = setup.AdvancedOptions,
                ["advancedSkirmishOptions"] = setup.AdvancedSkirmishOptions,
                ["preBuild"] = setup.PreBuild,
                ["improvedArabSwordsmen"] = setup.ImprovedArabSwordsmen,
                ["improvedLaddermen"] = setup.ImprovedLaddermen,
                ["improvedSpearmen"] = setup.ImprovedSpearmen,
                ["rebalancedHorseArchers"] = setup.RebalancedHorseArchers,
                ["improvedFletchers"] = setup.ImprovedFletchers,
                ["uncappedPeasants"] = setup.UncappedPeasants,
                ["fasterPeasants"] = setup.FasterPeasants,
                ["enemyHitPoints"] = setup.EnemyHitPoints,
                ["improvedSieging"] = setup.ImprovedSieging,
                ["healers"] = setup.Healers,
                ["eunuchs"] = setup.Eunuchs,
                ["noGold"] = setup.NoGold,
                ["improvedSieging2"] = setup.ImprovedSieging2,
                ["buildingsAvailable"] = setup.BuildingsAvailable.Cast<object>().ToList(),
                ["goodsAvailable"] = setup.GoodsAvailable.Cast<object>().ToList(),
                ["troopsAvailable"] = setup.TroopsAvailable.Cast<object>().ToList(),
            };

        private static PlayerDefinition ParsePlayer(object value)
        {
            if (value == null) return null;
            Dictionary<string, object> source = RequireObject(value, "player");
            var player = new PlayerDefinition();
            OptionalBool(source, "active", result => player.Active = result);
            OptionalInt(source, "team", result => player.Team = result);
            OptionalInt(source, "colour", result => player.Colour = result);
            player.KeepPosition = RequiredInt(source, "keepPosition");
            if (source.TryGetValue("lord", out object lord) && lord != null)
                player.Lord = ParseLord(RequireObject(lord, "lord"));
            if (source.TryGetValue("aivs", out object aivs) && aivs != null)
                player.Aivs = RequireArray(aivs, "aivs").Select(item => ParseAiv(RequireObject(item, "aiv"))).ToList();
            OptionalInt(source, "preferredAiv", result => player.PreferredAiv = result);
            OptionalNullableInt(source, "nativePreferredAiv", result => player.NativePreferredAiv = result);
            return player;
        }

        private static Dictionary<string, object> WritePlayer(PlayerDefinition player)
        {
            if (player == null) return null;
            var result = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["active"] = player.Active,
                ["team"] = player.Team,
                ["colour"] = player.Colour,
                ["keepPosition"] = player.KeepPosition,
                ["lord"] = WriteLord(player.Lord),
                ["aivs"] = (player.Aivs ?? new List<AivReference>()).Select(WriteAiv).Cast<object>().ToList(),
                ["preferredAiv"] = player.PreferredAiv,
            };
            if (player.NativePreferredAiv.HasValue)
                result["nativePreferredAiv"] = player.NativePreferredAiv.Value;
            return result;
        }

        private static MapReference ParseMap(Dictionary<string, object> value)
        {
            var result = new MapReference();
            ReadAsset(value, result);
            return result;
        }

        private static LordReference ParseLord(Dictionary<string, object> value)
        {
            var result = new LordReference();
            ReadAsset(value, result);
            result.Configuration = OptionalString(value, "configuration", null);
            OptionalInt(value, "baseLordId", number => result.BaseLordId = number);
            return result;
        }

        private static AivReference ParseAiv(Dictionary<string, object> value)
        {
            var result = new AivReference();
            ReadAsset(value, result);
            result.LordName = OptionalString(value, "lordName", null);
            OptionalInt(value, "rotation", number => result.Rotation = number);
            return result;
        }

        private static void ReadAsset(Dictionary<string, object> value, AssetReference asset)
        {
            asset.Source = RequiredString(value, "source");
            if (value.TryGetValue("id", out object id) && id != null)
                asset.Id = RequireInt(id, "id");
            asset.Name = OptionalString(value, "name", null);
            asset.File = OptionalString(value, "file", null);
        }

        private static Dictionary<string, object> WriteAsset(AssetReference asset)
        {
            if (asset == null) return null;
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["source"] = asset.Source,
                ["id"] = asset.Id,
                ["name"] = asset.Name,
                ["file"] = asset.File,
            };
        }

        private static Dictionary<string, object> WriteLord(LordReference lord)
        {
            if (lord == null) return null;
            Dictionary<string, object> result = WriteAsset(lord);
            result["configuration"] = lord.Configuration;
            result["baseLordId"] = lord.BaseLordId;
            return result;
        }

        private static Dictionary<string, object> WriteAiv(AivReference aiv)
        {
            if (aiv == null) return null;
            Dictionary<string, object> result = WriteAsset(aiv);
            result["lordName"] = aiv.LordName;
            result["rotation"] = aiv.Rotation;
            return result;
        }

        private static Dictionary<string, object> RequiredObject(Dictionary<string, object> source, string name)
        {
            if (!source.TryGetValue(name, out object value))
                throw new InvalidDataException(name + " is required.");
            return RequireObject(value, name);
        }

        private static Dictionary<string, object> RequireObject(object value, string name)
        {
            if (!(value is Dictionary<string, object> result))
                throw new InvalidDataException(name + " must be an object.");
            return result;
        }

        private static List<object> RequiredArray(Dictionary<string, object> source, string name)
        {
            if (!source.TryGetValue(name, out object value))
                throw new InvalidDataException(name + " is required.");
            return RequireArray(value, name);
        }

        private static List<object> RequireArray(object value, string name)
        {
            if (!(value is List<object> result))
                throw new InvalidDataException(name + " must be an array.");
            return result;
        }

        private static int RequiredInt(Dictionary<string, object> source, string name)
        {
            if (!source.TryGetValue(name, out object value))
                throw new InvalidDataException(name + " is required.");
            return RequireInt(value, name);
        }

        private static int RequireInt(object value, string name)
        {
            if (!(value is int result))
                throw new InvalidDataException(name + " must be an integer.");
            return result;
        }

        private static string RequiredString(Dictionary<string, object> source, string name)
        {
            if (!source.TryGetValue(name, out object value) || !(value is string result))
                throw new InvalidDataException(name + " must be a string.");
            return result;
        }

        private static string OptionalString(Dictionary<string, object> source, string name, string defaultValue)
        {
            if (!source.TryGetValue(name, out object value)) return defaultValue;
            if (value == null) return null;
            if (!(value is string result)) throw new InvalidDataException(name + " must be a string or null.");
            return result;
        }

        private static void OptionalInt(Dictionary<string, object> source, string name, Action<int> assign)
        {
            if (source.TryGetValue(name, out object value)) assign(RequireInt(value, name));
        }

        private static void OptionalNullableInt(Dictionary<string, object> source, string name, Action<int?> assign)
        {
            if (!source.TryGetValue(name, out object value)) return;
            assign(value == null ? (int?)null : RequireInt(value, name));
        }

        private static void OptionalBool(Dictionary<string, object> source, string name, Action<bool> assign)
        {
            if (!source.TryGetValue(name, out object value)) return;
            if (!(value is bool result)) throw new InvalidDataException(name + " must be a boolean.");
            assign(result);
        }
    }
}

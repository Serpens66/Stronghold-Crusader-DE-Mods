using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomCustomTrail.Core
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

            if (root.TryGetValue("modSettings", out object modSettingsValue) && modSettingsValue != null)
            {
                try
                {
                    mission.ModSettings = ModSettingsJson.ParseObject(RequireObject(modSettingsValue, "modSettings"));
                }
                catch (Exception exception)
                {
                    // A broken embedded preset must not make the mission assets unusable.
                    mission.ModSettings = ModSettingsDefinition.CreateDisabled();
                    mission.ModSettingsError = exception.Message;
                }
            }
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
                ["modSettings"] = Shared.DependencyFreeJson.Parse(ModSettingsJson.Serialize(mission.ModSettings)),
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
            return settings;
        }

        private static Dictionary<string, object> WriteSettings(CoopSettings settings) =>
            new Dictionary<string, object>(StringComparer.Ordinal)
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
            return player;
        }

        private static Dictionary<string, object> WritePlayer(PlayerDefinition player)
        {
            if (player == null) return null;
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["active"] = player.Active,
                ["team"] = player.Team,
                ["colour"] = player.Colour,
                ["keepPosition"] = player.KeepPosition,
                ["lord"] = WriteLord(player.Lord),
                ["aivs"] = (player.Aivs ?? new List<AivReference>()).Select(WriteAiv).Cast<object>().ToList(),
                ["preferredAiv"] = player.PreferredAiv,
            };
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

        private static void OptionalBool(Dictionary<string, object> source, string name, Action<bool> assign)
        {
            if (!source.TryGetValue(name, out object value)) return;
            if (!(value is bool result)) throw new InvalidDataException(name + " must be a boolean.");
            assign(result);
        }
    }
}

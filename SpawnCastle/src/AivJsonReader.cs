using System;
using System.Collections.Generic;
using System.IO;

namespace SpawnCastle
{
    /// <summary>
    /// Maps the AIV schema onto the shared dependency-free JSON DOM.
    /// </summary>
    internal static class AivJsonReader
    {
        public static AivJsonDocument Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            try
            {
                Dictionary<string, object> root = RequireObject(
                    Shared.DependencyFreeJson.Parse(json, allowTrailingCommas: true),
                    "root");
                var document = new AivJsonDocument();
                if (root.TryGetValue("pauseDelayAmount", out object pauseDelay))
                {
                    root.Remove("pauseDelayAmount");
                    document.pauseDelayAmount = RequireInt32(pauseDelay, "pauseDelayAmount");
                }
                if (root.TryGetValue("frames", out object frames))
                {
                    root.Remove("frames");
                    document.frames = ReadFrames(frames);
                }
                if (root.TryGetValue("miscItems", out object miscItems))
                {
                    root.Remove("miscItems");
                    document.miscItems = ReadMiscItems(miscItems);
                }
                return document;
            }
            catch (InvalidDataException ex)
            {
                throw new FormatException("Invalid AIVJSON: " + ex.Message, ex);
            }
        }

        private static List<AivJsonFrame> ReadFrames(object value)
        {
            List<object> values = RequireArray(value, "frames");
            var result = new List<AivJsonFrame>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                object rawItem = values[index];
                values[index] = null;
                Dictionary<string, object> item = RequireObject(rawItem, $"frames[{index}]");
                var frame = new AivJsonFrame();
                if (item.TryGetValue("itemType", out object itemType))
                    frame.itemType = RequireInt32(itemType, $"frames[{index}].itemType");
                if (item.TryGetValue("tilePositionOfsets", out object offsets))
                {
                    item.Remove("tilePositionOfsets");
                    frame.tilePositionOfsets = ReadInt32Array(offsets, $"frames[{index}].tilePositionOfsets");
                }
                if (item.TryGetValue("shouldPause", out object shouldPause))
                    frame.shouldPause = RequireBoolean(shouldPause, $"frames[{index}].shouldPause");
                result.Add(frame);
            }
            return result;
        }

        private static List<AivJsonMiscItem> ReadMiscItems(object value)
        {
            List<object> values = RequireArray(value, "miscItems");
            var result = new List<AivJsonMiscItem>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                object rawItem = values[index];
                values[index] = null;
                Dictionary<string, object> item = RequireObject(rawItem, $"miscItems[{index}]");
                var miscItem = new AivJsonMiscItem();
                if (item.TryGetValue("positionOfset", out object position))
                    miscItem.positionOfset = RequireInt32(position, $"miscItems[{index}].positionOfset");
                if (item.TryGetValue("itemType", out object itemType))
                    miscItem.itemType = RequireInt32(itemType, $"miscItems[{index}].itemType");
                if (item.TryGetValue("number", out object number))
                    miscItem.number = RequireInt32(number, $"miscItems[{index}].number");
                result.Add(miscItem);
            }
            return result;
        }

        private static List<int> ReadInt32Array(object value, string path)
        {
            List<object> values = RequireArray(value, path);
            var result = new List<int>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                object rawValue = values[index];
                values[index] = null;
                result.Add(RequireInt32(rawValue, $"{path}[{index}]"));
            }
            return result;
        }

        private static Dictionary<string, object> RequireObject(object value, string path) =>
            value as Dictionary<string, object>
            ?? throw new InvalidDataException(path + " must be a JSON object.");

        private static List<object> RequireArray(object value, string path) =>
            value as List<object>
            ?? throw new InvalidDataException(path + " must be a JSON array.");

        private static int RequireInt32(object value, string path)
        {
            if (value is int integer)
                return integer;
            if (value is long longInteger && longInteger >= int.MinValue && longInteger <= int.MaxValue)
                return (int)longInteger;
            throw new InvalidDataException(path + " must be an Int32.");
        }

        private static bool RequireBoolean(object value, string path) =>
            value is bool boolean
                ? boolean
                : throw new InvalidDataException(path + " must be a boolean.");
    }
}

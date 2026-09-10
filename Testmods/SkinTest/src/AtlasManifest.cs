using System;
using System.Collections.Generic;
using System.Globalization;

namespace SkinTest
{
    internal sealed class AtlasFrame
    {
        public string Name { get; }
        public int Index { get; }
        public bool Alternate { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public float PivotX { get; }
        public float PivotY { get; }
        public float PixelsPerUnit { get; }

        public AtlasFrame(string name, int index, bool alternate, float x, float y, float width, float height,
            float pivotX, float pivotY, float pixelsPerUnit)
        {
            Name = name;
            Index = index;
            Alternate = alternate;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            PivotX = pivotX;
            PivotY = pivotY;
            PixelsPerUnit = pixelsPerUnit;
        }
    }

    internal sealed class AtlasManifest
    {
        public const int NormalFrameCount = 1088;
        public const int AlternateFrameCount = 128;
        private const string FramePrefix = "body_swordsman-";

        public IReadOnlyList<AtlasFrame> Frames { get; }

        private AtlasManifest(List<AtlasFrame> frames)
        {
            Frames = frames;
        }

        public static AtlasManifest ParseAndValidate(string json, int atlasWidth, int atlasHeight)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Atlas JSON is empty.");
            object rootObject = Shared.DependencyFreeJson.Parse(json);
            var root = rootObject as Dictionary<string, object> ?? throw new InvalidOperationException("Atlas root must be an object.");
            float defaultPixelsPerUnit = ReadNumber(root, "pixelsPerUnit");
            var rawFrames = ReadArray(root, "frames");
            var frames = new List<AtlasFrame>(rawFrames.Count);
            var normal = new bool[NormalFrameCount];
            var alternate = new bool[AlternateFrameCount];

            foreach (object rawFrame in rawFrames)
            {
                var item = rawFrame as Dictionary<string, object> ?? throw new InvalidOperationException("Atlas frame must be an object.");
                string name = ReadString(item, "name");
                ParseName(name, out int index, out bool isAlternate);
                bool[] set = isAlternate ? alternate : normal;
                if (index < 0 || index >= set.Length || set[index])
                    throw new InvalidOperationException($"Atlas frame index is invalid or duplicated: {name}");
                set[index] = true;

                var rect = ReadObject(item, "rect");
                var pivot = ReadObject(item, "pivot");
                float x = ReadNumber(rect, "x");
                float y = ReadNumber(rect, "y");
                float width = ReadNumber(rect, "w");
                float height = ReadNumber(rect, "h");
                float pivotX = ReadNumber(pivot, "x");
                float pivotY = ReadNumber(pivot, "y");
                float pixelsPerUnit = item.ContainsKey("pixelsPerUnit") ? ReadNumber(item, "pixelsPerUnit") : defaultPixelsPerUnit;
                if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0f || height <= 0f ||
                    x < 0f || y < 0f || x + width > atlasWidth || y + height > atlasHeight)
                    throw new InvalidOperationException($"Atlas rectangle is invalid: {name}");
                if (!Finite(pivotX) || !Finite(pivotY) || !Finite(pixelsPerUnit) || pixelsPerUnit <= 0f)
                    throw new InvalidOperationException($"Atlas pivot or pixels-per-unit is invalid: {name}");
                frames.Add(new AtlasFrame(name, index, isAlternate, x, y, width, height, pivotX, pivotY, pixelsPerUnit));
            }

            ValidateComplete(normal, false);
            ValidateComplete(alternate, true);
            return new AtlasManifest(frames);
        }

        private static void ValidateComplete(bool[] frames, bool alternate)
        {
            for (int index = 0; index < frames.Length; index++)
                if (!frames[index])
                    throw new InvalidOperationException($"Missing atlas frame: {FramePrefix}{index}{(alternate ? "x" : string.Empty)}");
        }

        private static void ParseName(string name, out int index, out bool alternate)
        {
            if (!name.StartsWith(FramePrefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected atlas frame name: {name}");
            string suffix = name.Substring(FramePrefix.Length);
            alternate = suffix.EndsWith("x", StringComparison.Ordinal);
            string digits = alternate ? suffix.Substring(0, suffix.Length - 1) : suffix;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out index))
                throw new InvalidOperationException($"Unparseable atlas frame name: {name}");
        }

        private static Dictionary<string, object> ReadObject(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value) || !(value is Dictionary<string, object> result))
                throw new InvalidOperationException($"Atlas property '{key}' must be an object.");
            return result;
        }

        private static List<object> ReadArray(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value) || !(value is List<object> result))
                throw new InvalidOperationException($"Atlas property '{key}' must be an array.");
            return result;
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value) || !(value is string result) || string.IsNullOrEmpty(result))
                throw new InvalidOperationException($"Atlas property '{key}' must be a non-empty string.");
            return result;
        }

        private static float ReadNumber(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value))
                throw new InvalidOperationException($"Atlas property '{key}' is missing.");
            try { return Convert.ToSingle(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidOperationException($"Atlas property '{key}' must be numeric.", ex); }
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class SparseAtlasManifest
    {
        public IReadOnlyList<AtlasFrame> Frames { get; }

        private SparseAtlasManifest(List<AtlasFrame> frames)
        {
            Frames = frames;
        }

        public static SparseAtlasManifest ParseAndValidate(string json, int atlasWidth, int atlasHeight,
            string framePrefix, int expectedCount, int maximumIndex)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Sparse atlas JSON is empty.");
            var root = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>
                ?? throw new InvalidOperationException("Sparse atlas root must be an object.");
            var rawFrames = ReadArray(root, "frames");
            if (rawFrames.Count != expectedCount)
                throw new InvalidOperationException($"Sparse atlas contains {rawFrames.Count} frames instead of {expectedCount}.");
            var seen = new bool[maximumIndex + 1];
            var frames = new List<AtlasFrame>(rawFrames.Count);
            foreach (object rawFrame in rawFrames)
            {
                var item = rawFrame as Dictionary<string, object>
                    ?? throw new InvalidOperationException("Sparse atlas frame must be an object.");
                string name = ReadString(item, "name");
                if (!name.StartsWith(framePrefix, StringComparison.Ordinal) ||
                    !int.TryParse(name.Substring(framePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                    index <= 0 || index > maximumIndex || seen[index])
                    throw new InvalidOperationException($"Sparse atlas frame index is invalid or duplicated: {name}");
                if (item.TryGetValue("index", out object explicitIndex) &&
                    Convert.ToInt32(explicitIndex, CultureInfo.InvariantCulture) != index)
                    throw new InvalidOperationException($"Sparse atlas name/index mismatch: {name}");
                seen[index] = true;
                var rect = ReadObject(item, "rect");
                var pivot = ReadObject(item, "pivot");
                float x = ReadNumber(rect, "x");
                float y = ReadNumber(rect, "y");
                float width = ReadNumber(rect, "w");
                float height = ReadNumber(rect, "h");
                float pivotX = ReadNumber(pivot, "x");
                float pivotY = ReadNumber(pivot, "y");
                float ppu = item.ContainsKey("pixelsPerUnit") ? ReadNumber(item, "pixelsPerUnit") : 64f;
                if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || width <= 0f || height <= 0f ||
                    x < 0f || y < 0f || x + width > atlasWidth || y + height > atlasHeight ||
                    !Finite(pivotX) || !Finite(pivotY) || !Finite(ppu) || ppu <= 0f)
                    throw new InvalidOperationException($"Sparse atlas geometry is invalid: {name}");
                frames.Add(new AtlasFrame(name, index, false, x, y, width, height, pivotX, pivotY, ppu));
            }
            return new SparseAtlasManifest(frames);
        }

        private static Dictionary<string, object> ReadObject(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value) || !(value is Dictionary<string, object> result))
                throw new InvalidOperationException($"Sparse atlas property '{key}' must be an object.");
            return result;
        }

        private static List<object> ReadArray(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value) || !(value is List<object> result))
                throw new InvalidOperationException($"Sparse atlas property '{key}' must be an array.");
            return result;
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value) || !(value is string result) || string.IsNullOrEmpty(result))
                throw new InvalidOperationException($"Sparse atlas property '{key}' must be a non-empty string.");
            return result;
        }

        private static float ReadNumber(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out object value))
                throw new InvalidOperationException($"Sparse atlas property '{key}' is missing.");
            try { return Convert.ToSingle(value, CultureInfo.InvariantCulture); }
            catch (Exception ex) { throw new InvalidOperationException($"Sparse atlas property '{key}' must be numeric.", ex); }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

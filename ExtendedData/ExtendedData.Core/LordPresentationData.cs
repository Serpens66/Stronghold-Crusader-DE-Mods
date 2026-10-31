using APIShared.Content.Lords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData
{
    /// <summary>Portable authored presentation, independent of optional locally installed media aliases.</summary>
    public sealed class LordPresentationData
    {
        public string DisplayName { get; set; }
        public string MetadataJson { get; set; }
        public string MediaId { get; set; }
        public IReadOnlyDictionary<string, string> MediaHashes { get; set; }
        // In-memory provenance only. Never written into a Trail, network snapshot or save.
        public string SourceRoot { get; set; }

        public static object Encode(LordPresentationData value)
        {
            if (value == null) return null;
            Validate(value);
            return new Dictionary<string, object> {
                ["displayName"] = value.DisplayName, ["lordmetaJson"] = value.MetadataJson,
                ["mediaId"] = value.MediaId,
                ["mediaHashes"] = value.MediaHashes == null ? null : value.MediaHashes.ToDictionary(x => x.Key, x => (object)x.Value),
            };
        }
        public static LordPresentationData Decode(object value)
        {
            if (value == null) return null;
            var row = value as Dictionary<string, object> ?? throw new InvalidDataException("Invalid Lord presentation.");
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            object files;
            if (row.TryGetValue("mediaHashes", out files) && files != null)
                foreach (var item in files as Dictionary<string, object> ?? throw new InvalidDataException("Invalid Lord media inventory."))
                    hashes.Add(item.Key, item.Value as string);
            var result = new LordPresentationData {
                DisplayName = row["displayName"] as string, MetadataJson = row["lordmetaJson"] as string,
                MediaId = row["mediaId"] as string, MediaHashes = hashes,
            };
            Validate(result);
            return result;
        }
        public static void Validate(LordPresentationData value)
        {
            if (value == null) return;
            if (string.IsNullOrWhiteSpace(value.DisplayName)) throw new InvalidDataException("Lord display name is missing.");
            if (value.MetadataJson != null) LordMetadataDocument.Parse(value.MetadataJson);
            if (value.MediaId != null && (!IsHash(value.MediaId) || value.MediaHashes == null)) throw new InvalidDataException("Invalid Lord media identity.");
            foreach (var item in value.MediaHashes ?? new Dictionary<string, string>())
                if (value.MediaId == null || !IsHash(item.Value) || !SafeRelative(item.Key))
                    throw new InvalidDataException("Invalid Lord media file: " + item.Key);
        }
        public static string FallbackName(string name)
        {
            string normalized = (name ?? "").Replace('\\', '/').TrimEnd('/');
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }
        public static bool SafeRelative(string value) => !string.IsNullOrWhiteSpace(value) &&
            !Path.IsPathRooted(value) && value.IndexOf(':') < 0 && value.IndexOf('\\') < 0 &&
            Array.TrueForAll(value.Split('/'), part => part.Length > 0 && part != "." && part != "..");
        private static bool IsHash(string text) => text != null && text.Length == 64 &&
            Array.TrueForAll(text.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');
    }
}

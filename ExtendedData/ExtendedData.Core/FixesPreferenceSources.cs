using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtendedData
{
    // Json == null records absence, not an unknown value. A null list is a legacy capture.
    public sealed class FixesPreferenceSource
    {
        public string CustomName { get; set; }
        public int? LordType { get; set; }
        public string Json { get; set; }
    }

    public static class FixesPreferenceSources
    {
        public static string Identity(FixesPreferenceSource source)
        {
            if (source == null || (source.CustomName != null) == source.LordType.HasValue ||
                source.CustomName != null && string.IsNullOrWhiteSpace(source.CustomName) || source.LordType < 0)
                throw new InvalidDataException("Invalid Fixes preference source identity.");
            return source.CustomName != null ? "custom:" + source.CustomName.ToUpperInvariant() :
                "extended:" + source.LordType.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static void Validate(IReadOnlyList<FixesPreferenceSource> sources)
        {
            if (sources == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (FixesPreferenceSource source in sources)
            {
                if (!seen.Add(Identity(source))) throw new InvalidDataException("Duplicate Fixes preference source.");
                if (source.Json != null && !(Shared.DependencyFreeJson.Parse(source.Json) is Dictionary<string, object>))
                    throw new InvalidDataException("Fixes preference source must be a JSON object.");
            }
        }

        public static object Encode(IReadOnlyList<FixesPreferenceSource> sources)
        {
            if (sources == null) return null;
            Validate(sources);
            return sources.OrderBy(Identity, StringComparer.Ordinal).Select(source => (object)new Dictionary<string, object>
            {
                ["customName"] = source.CustomName?.ToUpperInvariant(),
                ["lordType"] = source.LordType,
                ["json"] = source.Json,
            }).ToList();
        }

        public static IReadOnlyList<FixesPreferenceSource> Decode(object value)
        {
            if (value == null) return null;
            var rows = value as List<object> ?? throw new InvalidDataException("Invalid Fixes preference sources.");
            var result = rows.Select(row =>
            {
                var item = row as Dictionary<string, object> ?? throw new InvalidDataException("Invalid Fixes source.");
                if (!item.ContainsKey("customName") || !item.ContainsKey("lordType") || !item.ContainsKey("json") ||
                    item["customName"] != null && !(item["customName"] is string) ||
                    item["lordType"] != null && !(item["lordType"] is int) ||
                    item["json"] != null && !(item["json"] is string))
                    throw new InvalidDataException("Invalid Fixes source fields.");
                return new FixesPreferenceSource
                {
                    CustomName = item["customName"] as string,
                    LordType = item["lordType"] == null ? null : (int?)Convert.ToInt32(item["lordType"]),
                    Json = item["json"] as string,
                };
            }).ToArray();
            Validate(result);
            return result;
        }

        public static string Digest(IReadOnlyList<FixesPreferenceSource> sources)
        {
            if (sources == null) return null;
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    Shared.DependencyFreeJson.Serialize(Encode(sources))))).Replace("-", "");
        }
    }
}

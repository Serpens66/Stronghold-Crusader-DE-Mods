using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtendedData
{
    public sealed class TrailLordSlot
    {
        public int PlayerId { get; set; }
        public string LordName { get; set; }
        public string ConfigName { get; set; }
        public string ConfigChecksum { get; set; }
        public IReadOnlyList<string> AivChecksums { get; set; }
        public bool RequiresInstalledPackage { get; set; }
        public string PackageDigest { get; set; }
        public string ModLordJson { get; set; }
        public string FixesJson { get; set; }
    }

    public sealed class TrailLordRequirements
    {
        public const string Suffix = ".lordrequirements.json";
        public const int MaximumBytes = 512 * 1024;
        public string MissionDigest { get; private set; }
        public IReadOnlyList<TrailLordSlot> Slots { get; private set; }

        public static TrailLordRequirements Create(string missionPath, IEnumerable<TrailLordSlot> slots)
        {
            var result = new TrailLordRequirements
            {
                MissionDigest = Hash(File.ReadAllBytes(missionPath)),
                Slots = (slots ?? Enumerable.Empty<TrailLordSlot>()).OrderBy(item => item.PlayerId).ToArray(),
            };
            Validate(result);
            return result;
        }

        public static string SidecarPath(string missionPath)
        {
            string full = Path.GetFullPath(missionPath);
            if (full.EndsWith(".coopmission.json", StringComparison.OrdinalIgnoreCase))
                return full.Substring(0, full.Length - ".coopmission.json".Length) + Suffix;
            if (full.EndsWith(".trail", StringComparison.OrdinalIgnoreCase))
                return full.Substring(0, full.Length - ".trail".Length) + Suffix;
            throw new InvalidDataException("Lord requirements need a .trail or .coopmission.json mission.");
        }

        public void Write(string missionPath)
        {
            if (!string.Equals(MissionDigest, Hash(File.ReadAllBytes(missionPath)), StringComparison.Ordinal))
                throw new InvalidDataException("Lord requirements refer to another mission.");
            Validate(this);
            string json = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            {
                ["version"] = 1,
                ["missionDigest"] = MissionDigest,
                ["slots"] = Slots.Select(slot => (object)new Dictionary<string, object>
                {
                    ["playerId"] = slot.PlayerId,
                    ["lordName"] = slot.LordName,
                    ["configName"] = slot.ConfigName,
                    ["configChecksum"] = slot.ConfigChecksum,
                    ["aivChecksums"] = slot.AivChecksums.Cast<object>().ToList(),
                    ["requiresInstalledPackage"] = slot.RequiresInstalledPackage,
                    ["packageDigest"] = slot.PackageDigest,
                    ["modLordJson"] = slot.ModLordJson,
                    ["fixesJson"] = slot.FixesJson,
                }).ToList(),
            });
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("Lord requirements exceed 512 KiB.");
            string target = SidecarPath(missionPath);
            string temporary = target + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(target)) Shared.AtomicFileReplacement.Replace(temporary, target);
                else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public static TrailLordRequirements Read(string missionPath)
        {
            string path = SidecarPath(missionPath);
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Lord requirements exceed 512 KiB.");
            var root = Object(Shared.DependencyFreeJson.Parse(new UTF8Encoding(false, true).GetString(bytes)));
            if (Convert.ToInt32(root["version"]) != 1)
                throw new InvalidDataException("Unsupported Lord requirements version.");
            var rows = root["slots"] as List<object> ?? throw new InvalidDataException("Invalid Lord requirements slots.");
            var slots = rows.Select(value =>
            {
                var row = Object(value);
                var aivs = row["aivChecksums"] as List<object> ?? throw new InvalidDataException("Invalid AIV checksums.");
                return new TrailLordSlot
                {
                    PlayerId = Convert.ToInt32(row["playerId"]),
                    LordName = row["lordName"] as string,
                    ConfigName = row["configName"] as string,
                    ConfigChecksum = row["configChecksum"] as string,
                    AivChecksums = aivs.Select(item => item as string ?? throw new InvalidDataException("Invalid AIV checksum.")).ToArray(),
                    RequiresInstalledPackage = row["requiresInstalledPackage"] is bool required && required,
                    PackageDigest = row["packageDigest"] as string,
                    ModLordJson = row["modLordJson"] as string,
                    FixesJson = row["fixesJson"] as string,
                };
            }).ToArray();
            var result = new TrailLordRequirements { MissionDigest = root["missionDigest"] as string, Slots = slots };
            Validate(result);
            if (!string.Equals(result.MissionDigest, Hash(File.ReadAllBytes(missionPath)), StringComparison.Ordinal))
                throw new InvalidDataException("Lord requirements do not match this mission.");
            return result;
        }

        private static void Validate(TrailLordRequirements value)
        {
            if (value == null || value.MissionDigest?.Length != 64 || value.Slots == null || value.Slots.Count > 8 ||
                value.Slots.Select(item => item.PlayerId).Distinct().Count() != value.Slots.Count)
                throw new InvalidDataException("Invalid Lord requirements.");
            foreach (TrailLordSlot slot in value.Slots)
            {
                if (slot == null || slot.PlayerId < 1 || slot.PlayerId > 8 ||
                    string.IsNullOrWhiteSpace(slot.LordName) || string.IsNullOrWhiteSpace(slot.ConfigName) ||
                    string.IsNullOrWhiteSpace(slot.ConfigChecksum) || slot.AivChecksums == null ||
                    slot.AivChecksums.Count > 32 || slot.RequiresInstalledPackage && slot.PackageDigest?.Length != 64)
                    throw new InvalidDataException("Invalid Lord requirement slot.");
                LordDataSnapshot.ValidateModLord(slot.ModLordJson);
            }
        }

        private static Dictionary<string, object> Object(object value) =>
            value as Dictionary<string, object> ?? throw new InvalidDataException("Expected a JSON object.");

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }
    }
}

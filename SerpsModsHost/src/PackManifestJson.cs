using System;
using System.Collections.Generic;
using System.IO;

namespace SerpsModsHost
{
    internal static class PackManifestJson
    {
        public static PackManifest Read(string json)
        {
            Dictionary<string, object> root = RequireObject(Shared.DependencyFreeJson.Parse(json), "manifest");
            return new PackManifest
            {
                SchemaVersion = ReadInt32(root, "SchemaVersion"),
                PackGuid = ReadString(root, "PackGuid"),
                PackVersion = ReadString(root, "PackVersion"),
                HostVersion = ReadString(root, "HostVersion"),
                CreatedUtc = ReadString(root, "CreatedUtc"),
                RepositoryCommit = ReadString(root, "RepositoryCommit"),
                Mods = ReadMods(root)
            };
        }

        public static string ReadStringProperty(string json, string propertyName)
        {
            Dictionary<string, object> root = RequireObject(Shared.DependencyFreeJson.Parse(json), "JSON root");
            return ReadString(root, propertyName);
        }

        public static void ReadStringProperties(
            string json,
            string firstPropertyName,
            string secondPropertyName,
            out string firstValue,
            out string secondValue)
        {
            Dictionary<string, object> root = RequireObject(Shared.DependencyFreeJson.Parse(json), "JSON root");
            firstValue = ReadString(root, firstPropertyName);
            secondValue = ReadString(root, secondPropertyName);
        }

        private static List<PackModRecord> ReadMods(Dictionary<string, object> root)
        {
            if (!TryGet(root, "Mods", out object value) || value == null)
                return new List<PackModRecord>();

            List<object> values = RequireArray(value, "Mods");
            var mods = new List<PackModRecord>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> mod = RequireObject(values[index], $"Mods[{index}]");
                mods.Add(new PackModRecord
                {
                    Name = ReadString(mod, "Name"),
                    Guid = ReadString(mod, "Guid"),
                    Version = ReadString(mod, "Version"),
                    State = ReadString(mod, "State"),
                    RelativePath = ReadString(mod, "RelativePath"),
                    ReleaseUrl = ReadString(mod, "ReleaseUrl"),
                    ReleaseTag = ReadString(mod, "ReleaseTag"),
                    SourceCommit = ReadString(mod, "SourceCommit"),
                    PackageSha256 = ReadString(mod, "PackageSha256"),
                    ExpectedSoftDependency = ReadString(mod, "ExpectedSoftDependency"),
                    Files = ReadFiles(mod, index)
                });
            }
            return mods;
        }

        private static List<PackFileRecord> ReadFiles(Dictionary<string, object> mod, int modIndex)
        {
            if (!TryGet(mod, "Files", out object value) || value == null)
                return new List<PackFileRecord>();

            List<object> values = RequireArray(value, $"Mods[{modIndex}].Files");
            var files = new List<PackFileRecord>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                Dictionary<string, object> file = RequireObject(values[index], $"Mods[{modIndex}].Files[{index}]");
                files.Add(new PackFileRecord
                {
                    Path = ReadString(file, "Path"),
                    Sha256 = ReadString(file, "Sha256"),
                    Size = ReadInt64(file, "Size")
                });
            }
            return files;
        }

        private static string ReadString(Dictionary<string, object> values, string name)
        {
            if (!TryGet(values, name, out object value) || value == null)
                return null;
            return value as string
                ?? throw new InvalidDataException(name + " must be a JSON string.");
        }

        private static int ReadInt32(Dictionary<string, object> values, string name)
        {
            if (!TryGet(values, name, out object value))
                return 0;
            if (value is int integer)
                return integer;
            if (value is long longInteger && longInteger >= int.MinValue && longInteger <= int.MaxValue)
                return (int)longInteger;
            throw new InvalidDataException(name + " must be an Int32.");
        }

        private static long ReadInt64(Dictionary<string, object> values, string name)
        {
            if (!TryGet(values, name, out object value))
                return 0;
            if (value is int integer)
                return integer;
            if (value is long longInteger)
                return longInteger;
            throw new InvalidDataException(name + " must be an Int64.");
        }

        private static bool TryGet(Dictionary<string, object> values, string name, out object value)
            => values.TryGetValue(name, out value);

        private static Dictionary<string, object> RequireObject(object value, string path)
        {
            if (!(value is Dictionary<string, object> raw))
                throw new InvalidDataException(path + " must be a JSON object.");

            var normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> pair in raw)
            {
                if (normalized.ContainsKey(pair.Key))
                    throw new InvalidDataException(path + " contains ambiguous property casing for " + pair.Key + ".");
                normalized.Add(pair.Key, pair.Value);
            }
            return normalized;
        }

        private static List<object> RequireArray(object value, string path) =>
            value as List<object>
            ?? throw new InvalidDataException(path + " must be a JSON array.");
    }
}

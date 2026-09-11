// Feature: Persist one shared map selection and sort order for the multiplayer lobby map list.
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class LobbyMapSelectionSnapshot
    {
        internal int SortColumn { get; set; }
        internal bool SortAscending { get; set; }
        internal LobbyMapIdentity Map { get; set; }

        internal LobbyMapSelectionSnapshot Clone() =>
            new LobbyMapSelectionSnapshot
            {
                SortColumn = SortColumn,
                SortAscending = SortAscending,
                Map = Map?.Clone(),
            };
    }

    internal sealed class LobbyMapIdentity
    {
        internal string Origin { get; set; } = string.Empty;
        internal string FilePath { get; set; } = string.Empty;
        internal string FileName { get; set; } = string.Empty;

        internal LobbyMapIdentity Clone() =>
            new LobbyMapIdentity
            {
                Origin = Origin,
                FilePath = FilePath,
                FileName = FileName,
            };
    }

    internal readonly struct LobbyMapCandidate
    {
        internal LobbyMapCandidate(string origin, string filePath, string fileName, object value)
        {
            Origin = origin ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            FileName = fileName ?? string.Empty;
            Value = value;
        }

        internal string Origin { get; }
        internal string FilePath { get; }
        internal string FileName { get; }
        internal object Value { get; }
    }

    internal static class LobbyMapSelectionPolicy
    {
        internal const string BuiltInOrigin = "builtin";
        internal const string UserOrigin = "user";
        internal const string WorkshopOrigin = "workshop";

        internal static bool IsFeatureEnabled(bool enableMod, bool enableSetting) =>
            enableMod && enableSetting;

        internal static bool IsValidSortColumn(int column) =>
            column >= 0 && column <= 5 || column >= 10 && column <= 16;

        internal static string GetOrigin(bool builtIn, bool user, bool workshop)
        {
            if (builtIn)
                return BuiltInOrigin;
            if (workshop)
                return WorkshopOrigin;
            if (user)
                return UserOrigin;
            return string.Empty;
        }

        internal static LobbyMapIdentity CreateIdentity(
            bool builtIn,
            bool user,
            bool workshop,
            string filePath,
            string fileName)
        {
            string origin = GetOrigin(builtIn, user, workshop);
            if (origin.Length == 0 || string.IsNullOrWhiteSpace(fileName))
                return null;

            return new LobbyMapIdentity
            {
                Origin = origin,
                FilePath = NormalizePath(filePath),
                FileName = fileName.Trim(),
            };
        }

        internal static object FindMatch(
            LobbyMapIdentity remembered,
            IEnumerable<LobbyMapCandidate> candidates)
        {
            if (!IsValidIdentity(remembered) || candidates == null)
                return null;

            string rememberedPath = NormalizePath(remembered.FilePath);
            object pathMatch = null;
            int pathMatches = 0;
            object nameMatch = null;
            int nameMatches = 0;

            foreach (LobbyMapCandidate candidate in candidates)
            {
                if (candidate.Value == null ||
                    !string.Equals(candidate.Origin, remembered.Origin, StringComparison.Ordinal))
                {
                    continue;
                }

                if (rememberedPath.Length > 0 &&
                    string.Equals(
                        NormalizePath(candidate.FilePath),
                        rememberedPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    pathMatch = candidate.Value;
                    pathMatches++;
                }

                if (string.Equals(
                        candidate.FileName,
                        remembered.FileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    nameMatch = candidate.Value;
                    nameMatches++;
                }
            }

            if (pathMatches == 1)
                return pathMatch;
            return nameMatches == 1 ? nameMatch : null;
        }

        internal static bool IsValidIdentity(LobbyMapIdentity identity) =>
            identity != null &&
            (identity.Origin == BuiltInOrigin ||
             identity.Origin == UserOrigin ||
             identity.Origin == WorkshopOrigin) &&
            !string.IsNullOrWhiteSpace(identity.FileName);

        internal static bool HasMapAuthority(
            bool skirmishGame,
            bool hasLobby,
            bool isHost) =>
            skirmishGame || hasLobby && isHost;

        internal static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized = path.Trim().Replace('/', '\\');
            try
            {
                return Path.GetFullPath(normalized).TrimEnd('\\');
            }
            catch
            {
                return normalized.TrimEnd('\\');
            }
        }
    }

    internal static class LobbyMapSelectionCodec
    {
        private const int SchemaVersion = 1;

        internal static string Serialize(LobbyMapSelectionSnapshot snapshot)
        {
            if (snapshot == null || !LobbyMapSelectionPolicy.IsValidSortColumn(snapshot.SortColumn))
                throw new ArgumentException("The lobby map-selection snapshot is invalid.", nameof(snapshot));

            var sort = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["column"] = snapshot.SortColumn,
                ["ascending"] = snapshot.SortAscending,
            };
            var root = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = SchemaVersion,
                ["sort"] = sort,
            };

            if (LobbyMapSelectionPolicy.IsValidIdentity(snapshot.Map))
            {
                root["map"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["origin"] = snapshot.Map.Origin,
                    ["path"] = snapshot.Map.FilePath ?? string.Empty,
                    ["name"] = snapshot.Map.FileName,
                };
            }

            return Shared.DependencyFreeJson.Serialize(root);
        }

        internal static bool TryDeserialize(
            string json,
            out LobbyMapSelectionSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            try
            {
                if (!(Shared.DependencyFreeJson.Parse(json) is Dictionary<string, object> root) ||
                    !TryReadInt(root, "version", out int version) ||
                    version != SchemaVersion)
                {
                    error = "unsupported or missing schema version";
                    return false;
                }

                if (!(TryGet(root, "sort") is Dictionary<string, object> sort) ||
                    !TryReadInt(sort, "column", out int column) ||
                    !LobbyMapSelectionPolicy.IsValidSortColumn(column) ||
                    !TryReadBool(sort, "ascending", out bool ascending))
                {
                    error = "invalid sort state";
                    return false;
                }

                LobbyMapIdentity map = null;
                object mapValue = TryGet(root, "map");
                if (mapValue != null)
                {
                    if (!(mapValue is Dictionary<string, object> mapValues) ||
                        !(TryGet(mapValues, "origin") is string origin) ||
                        !(TryGet(mapValues, "path") is string path) ||
                        !(TryGet(mapValues, "name") is string name))
                    {
                        error = "invalid map identity";
                        return false;
                    }

                    map = new LobbyMapIdentity
                    {
                        Origin = origin,
                        FilePath = LobbyMapSelectionPolicy.NormalizePath(path),
                        FileName = name,
                    };
                    if (!LobbyMapSelectionPolicy.IsValidIdentity(map))
                    {
                        error = "invalid map identity";
                        return false;
                    }
                }

                snapshot = new LobbyMapSelectionSnapshot
                {
                    SortColumn = column,
                    SortAscending = ascending,
                    Map = map,
                };
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static object TryGet(Dictionary<string, object> values, string key) =>
            values.TryGetValue(key, out object value) ? value : null;

        private static bool TryReadBool(
            Dictionary<string, object> values,
            string key,
            out bool result)
        {
            object value = TryGet(values, key);
            if (value is bool boolean)
            {
                result = boolean;
                return true;
            }

            result = false;
            return false;
        }

        private static bool TryReadInt(
            Dictionary<string, object> values,
            string key,
            out int result)
        {
            object value = TryGet(values, key);
            if (value is int integer)
            {
                result = integer;
                return true;
            }

            if (value is long wideInteger &&
                wideInteger >= int.MinValue &&
                wideInteger <= int.MaxValue)
            {
                result = (int)wideInteger;
                return true;
            }

            result = 0;
            return false;
        }
    }

    internal sealed class LobbyMapSelectionStore
    {
        private const long MaximumStoreBytes = 64L * 1024L;
        private const string StoreFileName = "LobbyMapSelectionMemory.json";
        private readonly ManualLogSource log;
        private readonly string storePath;
        private LobbyMapSelectionSnapshot current;

        internal LobbyMapSelectionStore(ManualLogSource log)
            : this(log, GetDefaultPath())
        {
        }

        internal LobbyMapSelectionStore(ManualLogSource log, string storePath)
        {
            this.log = log;
            this.storePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
            current = new LobbyMapSelectionSnapshot { SortColumn = 0, SortAscending = true };
            Load();
        }

        internal LobbyMapSelectionSnapshot Current => current.Clone();

        internal void RememberSort(int column, bool ascending)
        {
            if (!LobbyMapSelectionPolicy.IsValidSortColumn(column) ||
                current.SortColumn == column && current.SortAscending == ascending)
            {
                return;
            }

            current.SortColumn = column;
            current.SortAscending = ascending;
            Save();
        }

        internal void RememberMap(LobbyMapIdentity map)
        {
            if (!LobbyMapSelectionPolicy.IsValidIdentity(map) || SameMap(current.Map, map))
                return;

            current.Map = map.Clone();
            Save();
        }

        internal static string GetDefaultPath()
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string pluginDirectory = string.IsNullOrEmpty(assemblyLocation)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(assemblyLocation);
            return Path.Combine(pluginDirectory, "LobbyModSettings", StoreFileName);
        }

        private void Load()
        {
            if (!File.Exists(storePath))
                return;

            try
            {
                FileInfo info = new FileInfo(storePath);
                if (info.Length > MaximumStoreBytes)
                {
                    LogWarning(
                        $"Bugfixes and QoL ignored oversized lobby map-selection memory: " +
                        $"path={storePath}, size={info.Length}, maximum={MaximumStoreBytes}.");
                    return;
                }

                if (!LobbyMapSelectionCodec.TryDeserialize(
                        File.ReadAllText(storePath),
                        out LobbyMapSelectionSnapshot loaded,
                        out string error))
                {
                    LogWarning(
                        $"Bugfixes and QoL ignored invalid lobby map-selection memory at " +
                        $"{storePath}: {error}.");
                    return;
                }

                current = loaded;
                LogDebug(
                    $"Bugfixes and QoL loaded lobby map-selection memory: " +
                    $"column={current.SortColumn}, ascending={current.SortAscending}, " +
                    $"map={current.Map?.Origin ?? "none"}:{current.Map?.FileName ?? string.Empty}.");
            }
            catch (Exception exception)
            {
                LogWarning(
                    $"Bugfixes and QoL could not load lobby map-selection memory from " +
                    $"{storePath}: {exception.Message}");
            }
        }

        private void Save()
        {
            string directory = Path.GetDirectoryName(storePath);
            string temporaryPath = storePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    temporaryPath,
                    LobbyMapSelectionCodec.Serialize(current),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(storePath))
                    File.Replace(temporaryPath, storePath, null);
                else
                    File.Move(temporaryPath, storePath);
            }
            catch (Exception exception)
            {
                LogError(
                    $"Bugfixes and QoL could not save lobby map-selection memory to " +
                    $"{storePath}: {exception}");
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception exception)
                {
                    LogWarning(
                        $"Bugfixes and QoL could not remove temporary lobby map-selection " +
                        $"memory: {exception.Message}");
                }
            }
        }

        private static bool SameMap(LobbyMapIdentity left, LobbyMapIdentity right) =>
            left != null && right != null &&
            string.Equals(left.Origin, right.Origin, StringComparison.Ordinal) &&
            string.Equals(left.FilePath, right.FilePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);

        private void LogDebug(string message)
        {
            if (log != null)
                Shared.DebugLogHelper.LogDebug(log, message);
        }

        private void LogWarning(string message)
        {
            if (log != null)
                Shared.DebugLogHelper.LogWarning(log, message);
        }

        private void LogError(string message)
        {
            if (log != null)
                Shared.DebugLogHelper.LogError(log, message);
        }
    }
}

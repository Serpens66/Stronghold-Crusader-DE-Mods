// Feature: Persist locally suppressed Steam invite senders without runtime JSON dependencies.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class SteamInviteBlacklistStore
    {
        internal const int MaximumEntries = 1024;
        internal const long MaximumStoreBytes = 256L * 1024L;
        private const int SchemaVersion = 1;
        private const string StoreFileName = "SteamInviteBlacklist.json";

        private HashSet<ulong> blockedIds = new HashSet<ulong>();

        internal SteamInviteBlacklistStore(string storePath)
        {
            StorePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
            Load();
        }

        internal event Action Changed;

        internal string StorePath { get; }
        internal bool IsUsable { get; private set; }
        internal string LoadError { get; private set; } = string.Empty;
        internal int Count => blockedIds.Count;

        internal static string GetDefaultPath()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            string pluginDirectory = string.IsNullOrEmpty(location)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetDirectoryName(location);
            return Path.Combine(pluginDirectory, "LobbyModSettings", StoreFileName);
        }

        internal bool Contains(ulong steamId) => IsUsable && blockedIds.Contains(steamId);

        internal bool TryAdd(ulong steamId, out string error)
        {
            error = string.Empty;
            if (!IsUsable)
            {
                error = LoadError.Length == 0 ? "The blacklist is unavailable." : LoadError;
                return false;
            }
            if (steamId == 0)
            {
                error = "The Steam ID is zero.";
                return false;
            }
            if (blockedIds.Contains(steamId))
                return true;
            if (blockedIds.Count >= MaximumEntries)
            {
                error = "The blacklist entry limit was reached.";
                return false;
            }

            var updated = new HashSet<ulong>(blockedIds) { steamId };
            if (!TryWrite(updated, out error))
                return false;

            blockedIds = updated;
            Changed?.Invoke();
            return true;
        }

        internal bool TryClear(out string error)
        {
            var empty = new HashSet<ulong>();
            if (!TryWrite(empty, out error))
                return false;

            blockedIds = empty;
            IsUsable = true;
            LoadError = string.Empty;
            Changed?.Invoke();
            return true;
        }

        private void Load()
        {
            blockedIds.Clear();
            IsUsable = false;
            LoadError = string.Empty;
            if (!File.Exists(StorePath))
            {
                IsUsable = true;
                return;
            }

            try
            {
                if (new FileInfo(StorePath).Length > MaximumStoreBytes)
                    throw new InvalidDataException("The blacklist file is too large.");

                object parsed = Shared.DependencyFreeJson.Parse(File.ReadAllText(StorePath));
                if (!(parsed is Dictionary<string, object> root) ||
                    !TryReadInt(root, "version", out int version) || version != SchemaVersion ||
                    !root.TryGetValue("blockedSteamIds", out object idsValue) ||
                    !(idsValue is List<object> ids))
                    throw new InvalidDataException("The blacklist root is invalid.");
                if (ids.Count > MaximumEntries)
                    throw new InvalidDataException("The blacklist contains too many entries.");

                var loaded = new HashSet<ulong>();
                foreach (object value in ids)
                {
                    if (!(value is string text) ||
                        !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamId) ||
                        steamId == 0)
                        throw new InvalidDataException("The blacklist contains an invalid Steam ID.");
                    loaded.Add(steamId);
                }

                blockedIds = loaded;
                IsUsable = true;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        private bool TryWrite(HashSet<ulong> ids, out string error)
        {
            string directory = Path.GetDirectoryName(StorePath);
            string temporaryPath = StorePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var sorted = new List<ulong>(ids);
                sorted.Sort();
                var values = new List<object>(sorted.Count);
                foreach (ulong steamId in sorted)
                    values.Add(steamId.ToString(CultureInfo.InvariantCulture));
                var root = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["version"] = SchemaVersion,
                    ["blockedSteamIds"] = values,
                };

                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    temporaryPath,
                    Shared.DependencyFreeJson.Serialize(root),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (new FileInfo(temporaryPath).Length > MaximumStoreBytes)
                    throw new InvalidDataException("The serialized blacklist is too large.");
                if (File.Exists(StorePath))
                    File.Replace(temporaryPath, StorePath, null);
                else
                    File.Move(temporaryPath, StorePath);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }

        private static bool TryReadInt(
            Dictionary<string, object> values,
            string key,
            out int result)
        {
            result = 0;
            if (!values.TryGetValue(key, out object value))
                return false;
            if (value is long integer && integer >= int.MinValue && integer <= int.MaxValue)
            {
                result = (int)integer;
                return true;
            }
            if (value is int direct)
            {
                result = direct;
                return true;
            }
            return false;
        }
    }
}

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
        private readonly Shared.PersonalDataFile storage;

        internal SteamInviteBlacklistStore(string storePath, string legacyPath = null, Action<string> report = null)
        {
            StorePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
            storage = new Shared.PersonalDataFile(storePath, legacyPath, report,
                maximumReadBytes: MaximumStoreBytes);
            Load();
        }

        internal event Action Changed;

        internal string StorePath { get; }
        internal bool IsUsable { get; private set; }
        internal string LoadError { get; private set; } = string.Empty;
        internal int Count => blockedIds.Count;

        internal static string GetDefaultPath()
        {
            return Shared.PersonalDataFile.GetPath("BugfixesAndQoL", StoreFileName);
        }

        internal static string GetLegacyPath()
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
            if (!TryWrite(empty, out error, explicitReset: true))
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
            try
            {
                string contents = storage.ReadAllText();
                if (contents == null)
                {
                    IsUsable = true;
                    return;
                }
                object parsed = Shared.DependencyFreeJson.Parse(contents);
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
                storage.PreserveOnNextWrite();
                LoadError = ex.GetBaseException().Message;
            }
        }

        private bool TryWrite(HashSet<ulong> ids, out string error, bool explicitReset = false)
        {
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

                byte[] bytes = new UTF8Encoding(false).GetBytes(Shared.DependencyFreeJson.Serialize(root));
                if (bytes.Length > MaximumStoreBytes)
                    throw new InvalidDataException("The serialized blacklist is too large.");
                if (explicitReset) storage.ResetAllBytes(bytes);
                else storage.WriteAllBytes(bytes);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
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

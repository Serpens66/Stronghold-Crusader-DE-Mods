using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ExtendedData
{
    public sealed class LordDataSlot
    {
        public int PlayerId { get; set; }
        public string LordName { get; set; }
        public string ConfigName { get; set; }
        public string ConfigChecksum { get; set; }
        public string ModLordJson { get; set; }
        public string FixesJson { get; set; }
    }

    public sealed class LordDataSnapshot
    {
        public const int MaxSidecarBytes = 64 * 1024;
        public const int MaxSnapshotBytes = 128 * 1024;
        public const int ProtocolVersion = 1;

        public string SessionId { get; private set; }
        public string Digest { get; private set; }
        public bool FixesInstalled { get; private set; }
        public IReadOnlyList<LordDataSlot> Slots { get; private set; }
        public string WireJson { get; private set; }

        public static LordDataSnapshot Create(string sessionId, bool fixesInstalled, IEnumerable<LordDataSlot> slots)
            => CreateWithLimit(sessionId, fixesInstalled, slots, MaxSnapshotBytes);

        public static LordDataSnapshot CreateTrail(string sessionId, bool fixesInstalled,
            IEnumerable<LordDataSlot> slots)
            => CreateWithLimit(sessionId, fixesInstalled, slots,
                TrailLordRequirements.MaximumBytes * 2 + 64 * 1024);

        private static LordDataSnapshot CreateWithLimit(string sessionId, bool fixesInstalled,
            IEnumerable<LordDataSlot> slots, int maximumBytes)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new InvalidDataException("A Lord-data session ID is required.");
            LordDataSlot[] ordered = (slots ?? Enumerable.Empty<LordDataSlot>()).OrderBy(item => item.PlayerId).ToArray();
            ValidateSlots(ordered, fixesInstalled);
            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = ProtocolVersion,
                ["session"] = sessionId,
                ["fixesInstalled"] = fixesInstalled,
                ["slots"] = ordered.Select(item => (object)new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["playerId"] = item.PlayerId,
                    ["lordName"] = item.LordName,
                    ["configName"] = item.ConfigName,
                    ["configChecksum"] = item.ConfigChecksum,
                    ["modLordJson"] = item.ModLordJson,
                    ["fixesJson"] = item.FixesJson,
                }).ToList(),
            };
            string payloadJson = Shared.DependencyFreeJson.Serialize(payload);
            string digest = Hash(payloadJson);
            string wireJson = Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            {
                ["digest"] = digest,
                ["payload"] = payloadJson,
            });
            if (Encoding.UTF8.GetByteCount(wireJson) > maximumBytes)
                throw new InvalidDataException("The selected Lord data exceeds its size limit.");
            return new LordDataSnapshot
            {
                SessionId = sessionId,
                Digest = digest,
                FixesInstalled = fixesInstalled,
                Slots = ordered,
                WireJson = wireJson,
            };
        }

        public static LordDataSnapshot Parse(string wireJson)
        {
            if (string.IsNullOrEmpty(wireJson) || Encoding.UTF8.GetByteCount(wireJson) > MaxSnapshotBytes)
                throw new InvalidDataException("The Lord-data snapshot is missing or too large.");
            var envelope = RequireObject(Shared.DependencyFreeJson.Parse(wireJson));
            string payloadJson = RequireString(envelope, "payload");
            string digest = RequireString(envelope, "digest");
            if (!string.Equals(Hash(payloadJson), digest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The Lord-data snapshot checksum does not match.");
            var payload = RequireObject(Shared.DependencyFreeJson.Parse(payloadJson));
            if (Convert.ToInt32(payload["version"]) != ProtocolVersion)
                throw new InvalidDataException("Unsupported Lord-data snapshot version.");
            if (!payload.TryGetValue("fixesInstalled", out object installedValue) || !(installedValue is bool fixesInstalled))
                throw new InvalidDataException("The Fixes installation marker is invalid.");
            var items = payload["slots"] as List<object>;
            if (items == null)
                throw new InvalidDataException("The Lord-data slots are invalid.");
            LordDataSlot[] slots = items.Select(value =>
            {
                var item = RequireObject(value);
                return new LordDataSlot
                {
                    PlayerId = Convert.ToInt32(item["playerId"]),
                    LordName = RequireString(item, "lordName"),
                    ConfigName = RequireString(item, "configName"),
                    ConfigChecksum = RequireString(item, "configChecksum"),
                    ModLordJson = OptionalString(item, "modLordJson"),
                    FixesJson = OptionalString(item, "fixesJson"),
                };
            }).ToArray();
            ValidateSlots(slots, fixesInstalled);
            return new LordDataSnapshot
            {
                SessionId = RequireString(payload, "session"),
                Digest = digest,
                FixesInstalled = fixesInstalled,
                Slots = slots,
                WireJson = wireJson,
            };
        }

        public LordDataSlot GetSlot(int playerId) => Slots.FirstOrDefault(item => item.PlayerId == playerId);

        public static void ValidateModLord(string json)
        {
            if (json == null)
                return;
            if (Encoding.UTF8.GetByteCount(json) > MaxSidecarBytes)
                throw new InvalidDataException("A selected .modlord.json exceeds 64 KiB.");
            var root = RequireObject(Shared.DependencyFreeJson.Parse(json));
            if (root.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != root.Count)
                throw new InvalidDataException("The .modlord.json contains duplicate GUID namespaces.");
            foreach (KeyValuePair<string, object> entry in root)
                if (string.IsNullOrWhiteSpace(entry.Key) || !(entry.Value is Dictionary<string, object>))
                    throw new InvalidDataException("Every .modlord.json GUID namespace must be an object.");
        }

        private static void ValidateSlots(LordDataSlot[] slots, bool fixesInstalled)
        {
            if (slots.Length > 8 || slots.Any(item => item == null || item.PlayerId < 1 || item.PlayerId > 8 ||
                string.IsNullOrWhiteSpace(item.LordName) || string.IsNullOrWhiteSpace(item.ConfigName)) ||
                slots.Select(item => item.PlayerId).Distinct().Count() != slots.Length)
                throw new InvalidDataException("The selected Lord slots are invalid.");
            foreach (LordDataSlot slot in slots)
            {
                ValidateModLord(slot.ModLordJson);
                if (!fixesInstalled && slot.FixesJson != null)
                    throw new InvalidDataException("Fixes preferences were supplied without Fixes.");
            }
        }

        private static Dictionary<string, object> RequireObject(object value) =>
            value as Dictionary<string, object> ?? throw new InvalidDataException("Expected a JSON object.");
        private static string RequireString(Dictionary<string, object> value, string key) =>
            OptionalString(value, key) ?? throw new InvalidDataException("Missing Lord-data field: " + key);
        private static string OptionalString(Dictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object result) ? result as string : null;
        private static string Hash(string text)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
    }
}

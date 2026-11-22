using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using APIShared.ModSettings;
using MessagePack;

namespace MultiplayerRestartTest
{
    internal static class RestartSettingsFingerprint
    {
        internal static string Compute(IEnumerable<KeyValuePair<string, object>> endpoints)
        {
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes))
            {
                foreach (var endpoint in endpoints.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    // Editable/personal snapshots are not synchronized host state. Dynamic
                    // providers also expose working values, so require a future active-state
                    // contract for those instead of silently weakening restart validation.
                    if (endpoint.Value is PresetLobbyModSettingsViewModel model &&
                        (model.System_HasDynamicSettings || model.System_HasApplicationBackend))
                        throw new InvalidOperationException("Cannot verify active host settings for application backend: " + endpoint.Key);
                    writer.Write(endpoint.Key);
                    foreach (var property in ModSettingsApplication.GetHostProperties(endpoint.Value).OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        writer.Write(property.Name);
                        byte[] value = MessagePackSerializer.Serialize(property.PropertyType, property.GetValue(endpoint.Value));
                        writer.Write(value.Length); writer.Write(value);
                    }
                }
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "");
            }
        }
    }
}

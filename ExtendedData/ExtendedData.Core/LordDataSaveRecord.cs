using System;
using System.Collections.Generic;
using System.IO;

namespace ExtendedData
{
    public static class LordDataSaveRecord
    {
        public static string Write(LordDataSnapshot snapshot, LordPackageManifest manifest)
            => Shared.DependencyFreeJson.Serialize(new Dictionary<string, object>
            {
                ["version"] = 2,
                ["snapshot"] = snapshot?.WireJson,
                ["lordPackageManifest"] = manifest?.WireJson,
            });

        public static void Read(string json, out LordDataSnapshot snapshot, out LordPackageManifest manifest)
        {
            var envelope = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>;
            if (envelope != null && envelope.ContainsKey("lordPackageManifest"))
            {
                if (Convert.ToInt32(envelope["version"]) != 2)
                    throw new InvalidDataException("Unsupported saved Lord data format.");
                if (!envelope.TryGetValue("snapshot", out object value) || value != null && !(value is string) ||
                    envelope["lordPackageManifest"] != null && !(envelope["lordPackageManifest"] is string))
                    throw new InvalidDataException("Invalid saved Lord data envelope.");
                snapshot = value == null ? null : LordDataSnapshot.Parse((string)value);
                manifest = envelope["lordPackageManifest"] == null ? null :
                    LordPackageManifest.Parse((string)envelope["lordPackageManifest"]);
            }
            else
            {
                snapshot = LordDataSnapshot.Parse(json);
                manifest = null;
            }
        }
    }
}

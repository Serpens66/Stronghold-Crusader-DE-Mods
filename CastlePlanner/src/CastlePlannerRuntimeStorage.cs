using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;

namespace CastlePlanner
{
    internal interface ICastlePlannerRuntimeState
    {
        bool HasBlueprintHudPosition { get; set; }
        double BlueprintHudPositionX { get; set; }
        double BlueprintHudPositionY { get; set; }
    }

    // Keep this independent from settings presets: moving the HUD must never change
    // the selected preset or become part of a host-owned settings snapshot.
    internal sealed class CastlePlannerRuntimeStorage
    {
        private const string FileName = "CastlePlanner_Serp.Runtime.msgpack";
        private readonly Shared.PersonalDataFile file;
        private readonly Action<string> report;
        private Dictionary<string, byte[]> payload = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        internal CastlePlannerRuntimeStorage(string pluginAssemblyLocation, Action<string> report)
        {
            this.report = report;
            string directory = Path.GetDirectoryName(pluginAssemblyLocation);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("The CastlePlanner plugin directory is required.", nameof(pluginAssemblyLocation));
            file = new Shared.PersonalDataFile(
                Shared.PersonalDataFile.GetPath("CastlePlanner", FileName),
                Path.Combine(directory, "LobbyModSettings", FileName),
                report);
        }

        internal void Load(ICastlePlannerRuntimeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            try
            {
                byte[] bytes = file.ReadAllBytes();
                if (bytes == null) return;
                var loaded = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes)
                    ?? throw new InvalidDataException("The CastlePlanner runtime settings root is null.");
                payload = loaded;
                Read(nameof(state.HasBlueprintHudPosition), value => state.HasBlueprintHudPosition = MessagePackSerializer.Deserialize<bool>(value));
                Read(nameof(state.BlueprintHudPositionX), value => state.BlueprintHudPositionX = MessagePackSerializer.Deserialize<double>(value));
                Read(nameof(state.BlueprintHudPositionY), value => state.BlueprintHudPositionY = MessagePackSerializer.Deserialize<double>(value));
            }
            catch (Exception error)
            {
                file.PreserveOnNextWrite();
                report?.Invoke("CastlePlanner HUD position could not be loaded from " + file.Path + ": " + error.Message);
            }
        }

        internal void Save(ICastlePlannerRuntimeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            try
            {
                // This is the Extender's existing Dictionary<string, byte[]> format.
                // Preserve unknown keys rather than discarding future optional state.
                var next = new Dictionary<string, byte[]>(payload, StringComparer.Ordinal)
                {
                    [nameof(state.HasBlueprintHudPosition)] = MessagePackSerializer.Serialize(state.HasBlueprintHudPosition),
                    [nameof(state.BlueprintHudPositionX)] = MessagePackSerializer.Serialize(state.BlueprintHudPositionX),
                    [nameof(state.BlueprintHudPositionY)] = MessagePackSerializer.Serialize(state.BlueprintHudPositionY),
                };
                file.WriteAllBytes(MessagePackSerializer.Serialize(next));
                payload = next;
            }
            catch (Exception error)
            {
                report?.Invoke("CastlePlanner HUD position could not be saved to " + file.Path + ": " + error.Message);
            }
        }

        private void Read(string key, Action<byte[]> apply)
        {
            if (!payload.TryGetValue(key, out byte[] bytes)) return;
            try { apply(bytes); }
            catch (Exception error)
            {
                file.PreserveOnNextWrite();
                report?.Invoke("CastlePlanner HUD position field " + key + " was ignored: " + error.Message);
            }
        }
    }
}

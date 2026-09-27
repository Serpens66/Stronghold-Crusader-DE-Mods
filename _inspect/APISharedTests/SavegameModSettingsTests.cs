using APIShared;
using MessagePack;
using Shared;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace APISharedTests
{
    internal static class SavegameModSettingsTests
    {
        private sealed class CaptureFixture
        {
            public int Good { get; set; } = 7;
            public int Broken { get => throw new InvalidOperationException("unreadable"); set { } }
        }

        internal static void Run(Action<bool, string> check)
        {
            var record = new SavegameModSettingsRecord
            {
                Version = 2,
                Kind = (int)GameModeKind.CoopTrail,
                Variant = (int)GameModeLaunchVariant.Customized,
                Mods = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal)
                {
                    ["mod.example"] = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        ["EnableMod"] = MessagePackSerializer.Serialize(true),
                    },
                },
            };
            byte[] valid = MessagePackSerializer.Serialize(record);
            check(SavegameModSettings.TryDeserialize(valid, out SavegameModSettingsRecord restored) &&
                restored.Kind == record.Kind && restored.Mods["mod.example"].ContainsKey("EnableMod"),
                "savegame metadata failed to round-trip");
            check(SavegameModSettings.IsCurrentChoiceAllowed(restored),
                "customized Co-op Trail did not allow current host settings");
            record.LockedByConflict = true;
            check(!SavegameModSettings.IsCurrentChoiceAllowed(record),
                "conflicted mission allowed current host settings");
            check(SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out restored) &&
                restored.LockedByConflict, "saved mission conflict was lost");
            record.LockedByConflict = false;
            record.Variant = (int)GameModeLaunchVariant.Standard;
            check(!SavegameModSettings.IsCurrentChoiceAllowed(record),
                "standard Co-op Trail allowed current host settings");
            record.Kind = (int)GameModeKind.Campaign;
            check(!SavegameModSettings.IsCurrentChoiceAllowed(record),
                "campaign allowed current host settings");
            record.Kind = (int)GameModeKind.SandsOfTime;
            check(!SavegameModSettings.IsCurrentChoiceAllowed(record),
                "standard Sands of Time allowed current host settings");
            record.Kind = (int)GameModeKind.CoopTrail;
            record.Variant = (int)GameModeLaunchVariant.Customized;

            record.Version = 3;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "unknown savegame metadata version was accepted");
            record.Version = 2;
            record.Kind = (int)GameModeKind.Unknown;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "unknown savegame mode was accepted");
            record.Kind = (int)GameModeKind.CoopTrail;
            record.Variant = 99;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "unknown savegame Customize status was accepted");
            record.Variant = (int)GameModeLaunchVariant.Standard;
            record.Mods["mod.example"]["EnableMod"] = new byte[1024 * 1024 + 1];
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "oversized savegame property was accepted");
            check(!SavegameModSettings.TryDeserialize(new byte[] { 0xC1 }, out _),
                "damaged savegame metadata was accepted");

            PropertyInfo good = typeof(CaptureFixture).GetProperty(nameof(CaptureFixture.Good));
            PropertyInfo broken = typeof(CaptureFixture).GetProperty(nameof(CaptureFixture.Broken));
            Dictionary<string, byte[]> captured = SavegameModSettings.CaptureHostProperties(
                new CaptureFixture(), new[] { broken, good }, "failing.mod");
            check(captured.Count == 1 && captured.ContainsKey("Good"),
                "one failed property discarded another mod setting");
            var disabled = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["Good"] = MessagePackSerializer.Serialize(0),
                ["Broken"] = MessagePackSerializer.Serialize(0),
            };
            SavegameModSettings.OverlaySavedHostProperties(disabled, new[] { good, broken },
                new Dictionary<string, byte[]>(StringComparer.Ordinal)
                {
                    ["Good"] = captured["Good"],
                    ["Broken"] = new byte[] { 0xC1 },
                }, "failing.mod");
            check(MessagePackSerializer.Deserialize<int>(disabled["Good"]) == 7 &&
                MessagePackSerializer.Deserialize<int>(disabled["Broken"]) == 0,
                "missing or damaged property did not retain its disabled value");
            var missingModDefault = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["Good"] = MessagePackSerializer.Serialize(0),
            };
            SavegameModSettings.OverlaySavedHostProperties(missingModDefault,
                new[] { good }, null, "missing.mod");
            check(MessagePackSerializer.Deserialize<int>(missingModDefault["Good"]) == 0,
                "missing mod changed its disabled snapshot");

            var large = new SavegameModSettingsRecord
            {
                Version = 2,
                Kind = (int)GameModeKind.Campaign,
                Variant = (int)GameModeLaunchVariant.Standard,
                LockedByConflict = true,
                Mods = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal)
                {
                    ["large.mod"] = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        ["One"] = new byte[1024 * 1024],
                        ["Two"] = new byte[1024 * 1024],
                        ["Three"] = new byte[1024 * 1024],
                        ["Four"] = new byte[1024 * 1024],
                        ["Five"] = new byte[1024 * 1024],
                    },
                },
            };
            byte[] reduced = SavegameModSettings.SerializeWithinLimit(large);
            check(reduced.Length <= 4 * 1024 * 1024 &&
                SavegameModSettings.TryDeserialize(reduced, out restored) &&
                restored.Kind == (int)GameModeKind.Campaign && restored.LockedByConflict &&
                restored.Mods["large.mod"].Count < 5,
                "oversized mod properties displaced required mission metadata");

            GameModeKind kind = GameModeKind.CustomGame;
            GameModeLaunchVariant variant = GameModeLaunchVariant.Customized;
            bool conflict = false;
            GameModeHelper.ReconcileRestoredSaveMode(GameModeKind.CustomGame, true,
                GameModeKind.CustomGame, GameModeLaunchVariant.Standard, true,
                ref kind, ref variant, ref conflict);
            check(kind == GameModeKind.CustomGame && variant == GameModeLaunchVariant.Standard && conflict,
                "saved conflict was cleared for a matching mission kind");
            conflict = true;
            GameModeHelper.ReconcileRestoredSaveMode(GameModeKind.CustomGame, true,
                GameModeKind.CustomGame, GameModeLaunchVariant.Customized, false,
                ref kind, ref variant, ref conflict);
            check(conflict, "conflicting current mission evidence was cleared by saved metadata");
            GameModeHelper.ReconcileRestoredSaveMode(GameModeKind.CustomGame, false,
                GameModeKind.Unknown, GameModeLaunchVariant.Standard, false,
                ref kind, ref variant, ref conflict);
            check(conflict && variant == GameModeLaunchVariant.Standard,
                "missing mission metadata unlocked a save");
            kind = GameModeKind.Campaign;
            conflict = false;
            GameModeHelper.ReconcileRestoredSaveMode(GameModeKind.Campaign, true,
                GameModeKind.CoopTrail, GameModeLaunchVariant.Customized, false,
                ref kind, ref variant, ref conflict);
            check(kind == GameModeKind.Campaign && conflict && variant == GameModeLaunchVariant.Standard,
                "mismatched mission metadata unlocked a campaign");

            int ready = 0, cleaned = 0, reported = 0;
            MissionLifecycleService.CompleteDeferredRestore(
                () => throw new InvalidOperationException("restore failed"),
                () => ready++, () => cleaned++, _ => reported++);
            check(ready == 1 && cleaned == 1 && reported == 1,
                "deferred client restore failure prevented Ready or cleanup");
            try
            {
                MissionLifecycleService.CompleteDeferredRestore(
                    () => { }, () => throw new InvalidOperationException("Ready failed"),
                    () => cleaned++, _ => reported++);
            }
            catch (InvalidOperationException) { }
            check(cleaned == 2, "deferred client Ready failure skipped cleanup");
        }
    }
}

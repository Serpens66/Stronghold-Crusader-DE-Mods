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

        private sealed class PresetEndpointFixture : IModSettingsPresetEndpoint
        {
            public bool IsMissionPresetActive { get; private set; }
            public int ExitCount { get; private set; }
            public bool FailExit { get; set; }
            public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
                new Dictionary<string, byte[]>();
            public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable) =>
                IsMissionPresetActive = true;
            public void System_ExitMissionPreset()
            {
                ExitCount++;
                if (FailExit) throw new InvalidOperationException("exit failed");
                IsMissionPresetActive = false;
            }
        }

        internal static void Run(Action<bool, string> check)
        {
            var lease = new SavegameMissionPresetLease();
            var first = new PresetEndpointFixture();
            var second = new PresetEndpointFixture { FailExit = true };
            var replacement = new PresetEndpointFixture();
            first.System_EnterMissionPreset(null, "Savegame", false);
            second.System_EnterMissionPreset(null, "Savegame", false);
            replacement.System_EnterMissionPreset(null, "Savegame", false);
            lease.Track(42, "first", first);
            lease.Track(42, "second", second);
            lease.Track(43, "replacement", replacement);
            int exitErrors = 0;
            lease.ReleaseOnEnd(41, (_, __) => exitErrors++);
            check(first.IsMissionPresetActive && second.IsMissionPresetActive &&
                replacement.IsMissionPresetActive, "unrelated mission ended a savegame preset");
            lease.ReleaseOnEnd(42, (_, __) => exitErrors++);
            check(!first.IsMissionPresetActive && first.ExitCount == 1 &&
                second.ExitCount == 1 && exitErrors == 1 && replacement.IsMissionPresetActive,
                "savegame end did not release every owned participant independently");
            lease.ReleaseOnEnd(42, (_, __) => exitErrors++);
            check(first.ExitCount == 1 && second.ExitCount == 1,
                "duplicate mission end released a participant twice");
            lease.ReleaseOnEnd(43, (_, __) => exitErrors++);
            check(!replacement.IsMissionPresetActive && replacement.ExitCount == 1,
                "replacement session retained its savegame preset");

            var record = new SavegameModSettingsRecord
            {
                Version = 3,
                Kind = (int)GameModeKind.CoopTrail,
                Variant = (int)GameModeLaunchVariant.Customized,
                TrailCustomizeAllowed = true,
                Mods = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal)
                {
                    ["mod.example"] = new Dictionary<string, byte[]>(StringComparer.Ordinal)
                    {
                        ["EnableMod"] = MessagePackSerializer.Serialize(true),
                    },
                },
                CreatorRules = new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal)
                {
                    ["mod.example"] = new Dictionary<string, TrailCreatorRule>(StringComparer.Ordinal)
                    {
                        ["EnableMod"] = new TrailCreatorRule { Mode = 1 },
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
            record.TrailCustomizeAllowed = false;
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
            record.TrailCustomizeAllowed = true;

            record.Version = 4;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "unknown savegame metadata version was accepted");
            record.Version = 3;
            record.Kind = (int)GameModeKind.Unknown;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "unknown savegame mode was accepted");
            record.Kind = (int)GameModeKind.CoopTrail;
            record.Variant = 99;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "unknown savegame Customize status was accepted");
            record.Variant = (int)GameModeLaunchVariant.Standard;
            record.CreatorRules["mod.example"]["EnableMod"].Mode = 3;
            check(!SavegameModSettings.TryDeserialize(MessagePackSerializer.Serialize(record), out _),
                "invalid Trail creator rule was accepted");
            record.CreatorRules["mod.example"]["EnableMod"].Mode = 1;
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

            var creatorRules = new Dictionary<string, TrailCreatorRule>(StringComparer.Ordinal)
            {
                ["Good"] = new TrailCreatorRule { Mode = 1 },
            };
            var currentHost = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["Good"] = MessagePackSerializer.Serialize(9),
            };
            var trailSnapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["Good"] = MessagePackSerializer.Serialize(7),
            };
            SavegameModSettings.ApplyCreatorRules(trailSnapshot, new[] { good }, creatorRules,
                currentHost, strictTrail: false);
            check(MessagePackSerializer.Deserialize<int>(trailSnapshot["Good"]) == 9,
                "Player/Host rule did not override saved value with current value");
            creatorRules["Good"] = new TrailCreatorRule
            {
                Mode = 2, FixedValue = MessagePackSerializer.Serialize(5),
            };
            SavegameModSettings.ApplyCreatorRules(trailSnapshot, new[] { good }, creatorRules,
                currentHost, strictTrail: true);
            check(MessagePackSerializer.Deserialize<int>(trailSnapshot["Good"]) == 5,
                "non-Customize Trail did not enforce the creator's fixed value");

            var large = new SavegameModSettingsRecord
            {
                Version = 3,
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
                        ["Six"] = new byte[1024 * 1024],
                        ["Seven"] = new byte[1024 * 1024],
                        ["Eight"] = new byte[1024 * 1024],
                        ["Nine"] = new byte[1024 * 1024],
                    },
                },
                CreatorRules = new Dictionary<string, Dictionary<string, TrailCreatorRule>>(StringComparer.Ordinal),
            };
            byte[] reduced = SavegameModSettings.SerializeWithinLimit(large);
            check(reduced.Length <= 8 * 1024 * 1024 &&
                SavegameModSettings.TryDeserialize(reduced, out restored) &&
                restored.Kind == (int)GameModeKind.Campaign && restored.LockedByConflict &&
                restored.Mods["large.mod"].Count < 9,
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
            conflict = false;
            GameModeHelper.ReconcileRestoredSaveMode(GameModeKind.CustomGame, false,
                GameModeKind.Unknown, GameModeLaunchVariant.Standard, false,
                ref kind, ref variant, ref conflict);
            check(!conflict && kind == GameModeKind.CustomGame && variant == GameModeLaunchVariant.Standard,
                "legacy save did not retain current-settings fallback");
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

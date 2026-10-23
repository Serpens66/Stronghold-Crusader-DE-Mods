using ExtendedData.Core;

internal static class MissionSettingsAdapterTests
{
    internal static void Run()
    {
        var entry = new ModSettingsEntry { PlayerSettings = new[] { "z", "a", "z" } };
        var document = new ModSettingsDefinition {
            Mods = new Dictionary<string, ModSettingsEntry>(StringComparer.OrdinalIgnoreCase) {
                ["mod"] = entry, ["alias"] = entry,
                ["z-invalid"] = new ModSettingsEntry { PlayerSettings = new[] { " padded" } }
            }
        };
        var original = document.Mods;
        Reject(() => ModSettingsJson.NormalizeAndValidate(document, "source"));
        Require(ReferenceEquals(original, document.Mods), "failed validation replaced the original dictionary");
        Require(entry.PlayerSettings.SequenceEqual(new[] { "a", "z" }), "partial normalization was lost");
        document.Mods.Remove("z-invalid");
        Require(ReferenceEquals(document, ModSettingsJson.NormalizeAndValidate(document, "source")), "document identity changed");
        Require(!ReferenceEquals(original, document.Mods) && ReferenceEquals(entry, document.Mods["alias"]), "normalization lost entry aliases");
        Require(ReferenceEquals(document.Mods.Comparer, StringComparer.Ordinal), "normalization lost ordinal identity");

        // Pruning keeps the caller's dictionary/comparer and override object, including
        // case-insensitive mod lookup. A shared entry exposes changes through both keys.
        document.Mods = new Dictionary<string, ModSettingsEntry>(StringComparer.OrdinalIgnoreCase) { ["MOD"] = entry, ["alias"] = entry };
        original = document.Mods;
        entry.PlayerSettings = new[] { "z", "remove", "a", "z" };
        entry.Overrides = new Dictionary<string, object> { ["fixed"] = 1, ["obsolete"] = false };
        var overrides = entry.Overrides;
        Require(ModSettingsJson.RemoveUnknownSettings(document, "mod", new[] { "z", "a", "fixed" }).SequenceEqual(new[] { "obsolete", "remove" }), "wrong removed names");
        Require(ReferenceEquals(original, document.Mods) && ReferenceEquals(overrides, entry.Overrides), "pruning replaced dictionaries");
        Require(entry.PlayerSettings.SequenceEqual(new[] { "z", "a", "z" }) && !overrides.ContainsKey("obsolete"), "pruning normalized ordering or failed to mutate overrides");
        ModSettingsJson.RemoveUnknownSettings(document, "mod", null);
        Require(!original.ContainsKey("MOD") && ReferenceEquals(entry, original["alias"]) && entry.Overrides.Count == 0, "pruning lost alias/removal semantics");

        document = new ModSettingsDefinition { SchemaVersion = 1, Mods = null };
        Reject(() => ModSettingsJson.NormalizeAndValidate(document, null));
        Require(document.Mods == null, "rejected schema initialized Mods");
        document.SchemaVersion = 3;
        ModSettingsJson.NormalizeAndValidate(document, null);
        Require(document.Mods != null && document.Mods.Count == 0, "null Mods did not normalize");

        entry.PlayerSettings = new[] { "z", "a", "z" };
        entry.Overrides = new Dictionary<string, object> { ["huge"] = new string('x', ModSettingsJson.MaximumBytes) };
        document.Mods["mod"] = entry;
        original = document.Mods;
        Reject(() => ModSettingsJson.Serialize(document));
        Require(!ReferenceEquals(original, document.Mods) && entry.PlayerSettings.SequenceEqual(new[] { "a", "z" }), "failed serialization hid normalization");

        const string json = "{\"schemaVersion\":3,\"mods\":{\"third.party\":{\"playerSettings\":[\"value\"],\"overrides\":{\"fixed\":true}}}}";
        Require(ModSettingsJson.Serialize(ModSettingsJson.ParseObject(json)) ==
            APIShared.ModSettings.MissionModSettingsJson.Serialize(APIShared.ModSettings.MissionModSettingsJson.ParseObject(json)), "legacy and Content wire output differs");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Expected InvalidDataException");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

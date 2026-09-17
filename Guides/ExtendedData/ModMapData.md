# Mod-specific data for maps

`modmap.json` stores static, map-specific data that mods can interpret. It is not a lobby-settings file, save data, or general Script Extender metadata. A typical use is adding spawn regions, scenario rules, identifiers, or other structured information that does not exist in Vanilla's map format.

## Where the file is stored

A Script Extender map consists of the unchanged Vanilla map followed by a ZIP archive:

```text
example.map
├─ Vanilla map data
└─ appended ZIP archive
   ├─ info.json
   ├─ init.lua
   └─ modmap.json
```

Place `modmap.json` at the root of the appended archive, next to `info.json` and `init.lua`. Do not place it merely beside the `.map` file.

This layout preserves Vanilla compatibility. Vanilla reads the indexed map records and ignores the appended ZIP data. The same `.map` therefore remains loadable without the Script Extender or ExtendedData; the additional mod values are simply unavailable. See the current [Script Extender Map Creation Guide](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/map-creation-guide.md?ref_type=heads) for creating and packaging Script Extender maps.

## File responsibilities

| Data | Purpose |
|---|---|
| `info.json` | General Script Extender map/package metadata. |
| `modmap.json` | Static, mod-specific data authored as part of the map. |
| `_SE_ModData_<ModId>.msgpack` | Mutable runtime/save data managed by the Script Extender `ModSaveDataAPI`. |

`modmap.json` remains static source data even when the Script Extender preserves archive entries while saving. It does not replace synchronized multiplayer or mutable save-state data.

## JSON structure

The root is an object whose keys are mod GUIDs. Every value is an object owned by that mod:

```json
{
  "author.example-mod": {
    "schemaVersion": 1,
    "spawnRegions": [
      {
        "name": "northern-reinforcements",
        "x": 120,
        "y": 340
      }
    ]
  }
}
```

There is no global schema version. Each mod may version and validate its own namespace. Multiple mods share the document through separate namespaces:

```json
{
  "author.example-mod": {
    "schemaVersion": 1,
    "scenarioId": "desert-crossing"
  },
  "org.example.spawn-control": {
    "schemaVersion": 2,
    "waves": [
      {
        "startTick": 1200,
        "unit": "ArabianSwordsman",
        "count": 20
      }
    ]
  }
}
```

GUID identity is case-insensitive. Keys that differ only by casing conflict and make the container invalid. Prefer the stable BepInEx plugin GUID in lowercase ASCII. Empty GUIDs and non-object namespace values are invalid.

## Reading a namespace

Reference `ExtendedData.dll` and `ExtendedData.Core.dll`, then request only your own namespace:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result =
    ExtendedDataModDataApi.ReadCurrentMapNamespace("author.example-mod");

if (result.Success)
{
    IReadOnlyDictionary<string, object> data = result.Data;
    string namespaceJson = result.Json;
}
```

`Data` is a deeply read-only snapshot. Nested JSON objects are exposed as `IReadOnlyDictionary<string, object>` and arrays as `IReadOnlyList<object>`. `Json` contains only the requested namespace, never the complete shared document. ExtendedData validates the shared container but deliberately does not interpret `schemaVersion` or other mod-owned fields.

The result status is one of:

- `Success`: the requested object was returned.
- `FileNotFound`: no active archive or no `modmap.json` exists.
- `NamespaceNotFound`: the document is valid but has no matching GUID.
- `InvalidDocument`: JSON, UTF-8, the root object, GUID keys, or namespace objects are invalid.
- `ReadError`: the archive could not be read.
- `InvalidRequest`: the supplied mod GUID is empty.

Missing files and namespaces mean normal default behavior. For any other failure, log `Diagnostic` and disable only the feature that needs the data. Never make the underlying map unplayable.

Call the API after the Script Extender has loaded the map archive. The API reads on demand and does not cache values or poll. A mod should retain its own validated model only for the lifetime of the corresponding map.

## Authoring and update rules

- Encode the document as UTF-8 JSON without comments or trailing commas.
- Store static map information, not user preferences or mutable runtime state.
- Read and assign meaning only to your own GUID namespace.
- Ignore unknown fields inside your namespace unless your schema explicitly rejects them.
- When a tool updates one namespace, parse the current document, replace only that namespace, preserve every other namespace, and safely rewrite the complete file.
- The presence of a namespace does not install its mod or declare an automatically resolved dependency.
- Gameplay-relevant data must still be handled consistently by every multiplayer participant.

## Validation checklist

1. Confirm `modmap.json` is at the appended archive root.
2. Validate strict UTF-8 JSON, an object root, unique case-insensitive GUIDs, and object namespace values.
3. Validate each namespace against its owning mod's schema.
4. Test the API with the file missing, the namespace missing, and malformed data.
5. Test every consuming mod alone and together with the other consumers.
6. Load the final `.map` with ExtendedData and verify the expected namespace values.
7. Load that exact `.map` in a true no-mod/Vanilla startup and verify that the base map remains playable.
8. Reopen the distributed `.map` as an archive and confirm that all expected entries remain present.

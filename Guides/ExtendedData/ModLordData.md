# Mod-specific data for Custom Lord AICs

`name.modlord.json` stores static, mod-specific data for one Custom Lord AI configuration. It lets several mods extend the same AIC without adding unknown fields to Vanilla's `.lordjson` format.

## Files and placement

| File | Purpose |
|---|---|
| `name.lordjson` | Vanilla AI configuration. |
| `lordmeta.json` | Script Extender metadata for the complete Custom Lord package. |
| `name.modlord.json` | Mod-specific data tied to one AIC. |

Place each sidecar directly beside the matching `.lordjson` and retain the complete base name:

```text
My Custom Lord/
  aggressive.lordjson
  aggressive.modlord.json
  aggressive.v2.lordjson
  aggressive.v2.modlord.json
  lordmeta.json
```

The sidecar is optional. Its absence must never prevent Vanilla or the Script Extender from loading the AIC.

## JSON structure

The root is an object whose case-insensitive keys are mod GUIDs. Every value is an object owned and validated by that mod:

```json
{
  "com.example.first-mod": {
    "schemaVersion": 1,
    "aggressionMultiplier": 1.25,
    "preferredUnits": [
      "ArabianSwordsman",
      "HorseArcher"
    ]
  },
  "org.example.other-mod": {
    "schemaVersion": 2,
    "customFeature": {
      "enabled": true
    }
  }
}
```

There is no global schema version. Keys that differ only by casing conflict and make the shared container invalid. Empty GUIDs and non-object namespace values are invalid.

## Reading a namespace

Reference `ExtendedData.dll` and `ExtendedData.Core.dll`. If the exact `.lordjson` path is known:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    lordJsonPath,
    "author.example-mod");
```

Code that already has the game's `CustomLordConfig` can use the convenience overload:

```csharp
ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    customLordConfig,
    "author.example-mod");
```

ExtendedData derives the sidecar by replacing only the final `.lordjson` suffix with `.modlord.json`. The config overload uses `Path.Combine(config.path, config.name + ".lordjson")` first.

On success, `Data` contains a deeply read-only object tree and `Json` contains only the requested namespace. Nested objects implement `IReadOnlyDictionary<string, object>` and arrays implement `IReadOnlyList<object>`. ExtendedData validates only the shared container; the consuming mod remains responsible for its own fields and `schemaVersion`.

The status values are `Success`, `FileNotFound`, `NamespaceNotFound`, `InvalidDocument`, `ReadError`, and `InvalidRequest`. `Source` identifies the derived sidecar path and `Diagnostic` explains failures. Missing sidecars and namespaces mean default behavior. Invalid data must disable only the consuming extension, never the underlying AIC.

## Authoring and update rules

- Encode the document as UTF-8 JSON without comments or trailing commas.
- Store only information that supplements the matching AIC; do not mirror Vanilla fields.
- Use a stable BepInEx plugin GUID and read only that namespace.
- Ignore unknown fields unless the mod-owned schema explicitly rejects them.
- When updating one namespace, preserve all foreign namespaces and their unknown fields.
- Keep package-wide presentation metadata in `lordmeta.json`.
- Do not require the sidecar for normal `.lordjson` loading.

## Validation checklist

1. Confirm every sidecar has a matching `.lordjson` in the same directory.
2. Validate strict UTF-8 JSON, an object root, unique case-insensitive GUIDs, and object namespace values.
3. Test the API by exact path and by `CustomLordConfig`.
4. Test missing files, missing namespaces, invalid JSON, and unsupported mod-owned schemas.
5. Load the AIC without the sidecar and without its consuming mods.
6. Verify that packaging and Workshop upload preserve every sidecar beside its AIC.


# Mod-specific data for Custom Lord AICs

This guide defines `name.modlord.json`, a shared JSON sidecar for mod-specific data associated with a Custom Lord AI configuration (AIC). It allows several mods to attach independent data to the same `.lordjson` file without adding unknown fields to Vanilla's format or competing for a single mod-owned schema.

## What this file is for

The three Lord-related files have different owners and purposes:

| File | Purpose |
|---|---|
| `name.lordjson` | Vanilla's AI configuration. Do not add mod-specific fields to it. |
| `lordmeta.json` | Script Extender metadata for the complete Custom Lord package, such as localized text, portraits, and messages. |
| `name.modlord.json` | Independent, AIC-specific data namespaces defined and consumed by mods. |

`name.modlord.json` does not replace either of the other files. Vanilla does not need to understand it, and the file must not be required for loading the corresponding `.lordjson` configuration.

## File name and location

Place the sidecar directly beside its `.lordjson` file and reuse the exact base name:

```text
My Custom Lord/
  aggressive.lordjson
  aggressive.modlord.json
  defensive.lordjson
  defensive.modlord.json
  lordmeta.json
```

For example, `aggressive.modlord.json` belongs only to `aggressive.lordjson`. If a Lord directory contains several AICs, each AIC may have its own sidecar. An AIC that needs no mod-specific data may omit the sidecar entirely.

Keep the spelling and letter casing of the `.lordjson` base name. A base name containing dots keeps those dots: `aggressive.v2.lordjson` is paired with `aggressive.v2.modlord.json`.

## JSON structure

The root must be a JSON object. Every root key is a mod GUID, and every corresponding value must be a JSON object:

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

The root has no global `schemaVersion`: the file is only a registry of independent mod namespaces. Each mod owns the schema below its GUID and may evolve it without changing another mod's data. A positive integer `schemaVersion` inside each namespace is recommended when that mod may change its schema.

Apart from requiring an object as the namespace value, this convention does not prescribe its members. The owning mod may define nested objects, arrays, strings, numbers, booleans, and null values as appropriate.

## GUID and namespace rules

- Use the consuming mod's globally unique BepInEx plugin GUID when one exists. Prefer lowercase ASCII GUIDs such as `author.mod-name`.
- GUID ownership belongs to the mod that documents and consumes the namespace. A Lord author supplies values under that GUID according to the mod's documentation.
- Treat GUID keys as case-insensitive for identity. A file containing keys that differ only by casing is conflicting and must not be treated as two namespaces.
- A mod must read only its own namespace and must not assign meaning to another mod's data.
- Unknown namespaces and unknown members inside a known namespace must be ignored unless the owning mod explicitly documents stricter validation.
- When a tool updates one namespace, it must preserve all other namespaces and their contents. Never replace the complete file with only the updating mod's data.

## Minimal example

A sidecar used by only one mod may be as small as:

```json
{
  "author.my-mod": {
    "schemaVersion": 1,
    "enabled": true
  }
}
```

An empty root object is valid but normally unnecessary; omit the sidecar when no mod has data to store.

## Loading and failure behavior

A consuming mod should fail closed for its extension while leaving the Vanilla AIC usable:

1. Derive the sidecar path from the selected `.lordjson` path by replacing the final `.lordjson` suffix with `.modlord.json`.
2. If the sidecar is absent, behave as if no mod-specific data was configured.
3. Require a JSON object at the root and reject case-insensitive duplicate GUID keys.
4. Look up only the mod's own GUID. If it is absent, use that mod's documented defaults.
5. Require the mod's namespace value to be an object, then validate its members and supported `schemaVersion` according to that mod's schema.
6. If the file or the mod's namespace is malformed or unsupported, log a clear warning and disable only the affected extension behavior. Do not reject or alter the `.lordjson` AIC.

Consumers should not assume that another mod, the Script Extender, or Vanilla validates this file for them.

## Authoring rules

- Encode the file as UTF-8 JSON without comments or trailing commas.
- Do not copy fields from `.lordjson` merely to mirror Vanilla data. Store only information that the consuming mod needs in addition to Vanilla's AIC.
- Follow the consuming mod's documentation for property names, types, defaults, valid ranges, and namespace `schemaVersion`.
- Merge namespaces when combining data for several mods. JSON does not permit the same GUID key to appear more than once.
- Do not add a `mods` wrapper or package-wide fields at the root; root keys are reserved for mod GUIDs.
- Keep package-wide presentation metadata in `lordmeta.json`. Use `name.modlord.json` only for data tied to one particular AIC.

## Validation checklist

Before distributing a Custom Lord or Extended Lord package:

1. Confirm that every `name.modlord.json` has a matching `name.lordjson` in the same directory.
2. Parse the sidecar with a strict JSON parser.
3. Confirm that the root and every GUID value are JSON objects.
4. Check for empty GUIDs, duplicate GUIDs, and GUIDs that differ only by letter casing.
5. Validate each namespace against the documentation and supported schema versions of its consuming mod.
6. Test the `.lordjson` without the sidecar and verify that Vanilla behavior still works.
7. Test with each consuming mod absent, present individually, and present together with the other consumers.
8. Inspect the installed or downloaded package and confirm that all sidecars were preserved next to their matching AICs.

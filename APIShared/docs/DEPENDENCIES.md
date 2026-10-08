# Dependency versions

BepInEx's versioned `BepInDependency` constructor declares an inclusive **minimum**
version. It accepts newer installed plugins and enforces startup order. It is not
an exact-version pin. Keep this hard dependency for APIs needed by the plugin.

The installed Script Extender also supports arbitrary GUIDs in `info.json`:

```json
"Dependencies": [
  { "GUID": "APIShared_Serp", "MinimumVersion": "0.4.12" },
  { "GUID": "Another.Author.Plugin" }
]
```

`MinimumVersion` and `MaximumVersion` are optional inclusive bounds. Without
bounds, the entry requires presence. Soft BepInEx dependencies stay optional and
must not be converted into mandatory manifest entries. Declare an upper bound
only for a demonstrated incompatibility; routine updates use minimum bounds.

Mirror each required plugin's minimum in its BepInEx attribute and manifest.
The dependency validation checks consistency without fixing a particular release
number in a test. Runtime tests and public consumer examples validate behavior.

The SerpsMods workspace retains its existing `MinimumScriptExtenderVersion` and
`MaximumScriptExtenderVersion` fields for its host diagnostics and BepInEx loading
protection. These are distinct from the Script Extender's generic `Dependencies`
collection. The community API does not require SerpsModsHost.

The contracts were verified from the actually installed BepInEx and SHCDESE
assemblies, including the Script Extender's dependency discovery and inclusive
version comparison. Neither upstream implementation needs a custom modification.

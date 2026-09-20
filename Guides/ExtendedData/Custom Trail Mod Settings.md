# Mod settings in Maps and Custom Trails

`ExtendedData` can store shared gameplay settings in a Map or beside a Trail mission. Compatible installed mods are discovered automatically, so creators normally configure everything in the Map Editor or Trail Maker and do not edit JSON by hand.

This guide is for Trail creators. Mod developers who want their settings to appear here should use [Extended Data compatibility for mod authors](Mod%20Compatibilty%20ExtendedData.md).

## Choose the settings to store

1. Install and enable `ExtendedData` and the compatible gameplay mods that the Trail should use.
2. Open the Map Editor or Trail Maker and load or create the Map/mission.
3. Open the `ExtendedData` mod settings. Expand a compatible mod to configure its individual settings.
4. Choose one mode for each relevant setting:
   - **Mod default** uses the safe baseline supplied by that mod. For most gameplay mods, this disables the mod or feature.
   - **Player/host** uses the normal saved setting of the player in singleplayer or the multiplayer host. The Trail permits the feature but does not dictate its value.
   - **Fixed creator value** stores the value currently shown in the compatible mod's settings and applies that exact value when the Map or mission is played.
5. Configure every `Fixed creator value` in the owning mod's normal settings panel, then save the Map or Trail mission.

The selector on a mod heading changes all settings in that mod at once. `Mixed` means that its settings currently use different modes. Large related lists, including Unit Costs and Extra Features market multipliers, are deliberately presented as one atomic selection.

Only persistent host-controlled settings can become Map/Trail rules. Personal, per-player, local and transient settings always remain under each player's control.

## Resulting files

Saving a `.map` from the Map Editor writes the schema-3 document into the Map archive as:

    _SE_ModData_ExtendedData-MapModSettings.msgpack

Despite the archive suffix, the entry contains UTF-8 JSON. It is intentionally separate from `modmap.json`, whose GUID-based namespaces remain unchanged. A successful empty capture records the safe default for all compatible mods; if capture fails, an existing archive entry is retained.

Saving `Trail_Mission_1.trail` creates the optional sidecar:

    Trail_Mission_1.modtrail.json

The sidecar must remain beside its matching `.trail` file. If every compatible setting stays on `Mod default`, no mod needs to be mentioned and the file contains no active overrides. A missing sidecar likewise makes every compatible mod use its own Trail-safe baseline.

For a portable Coop Trail package, each mission uses the same base name:

    CoopMissions/01.coopmission.json
    CoopMissions/01.modtrail.json

When uploading a normal or Coop Trail to the Workshop, keep **Include mod settings** enabled to ship these files. Disable it only when deliberately publishing a Vanilla/default-only variant.

The old `.modjson` name is unsupported and is not imported, loaded or included in Workshop packages.

## What players need

Players need `ExtendedData` and every mod explicitly mentioned by the Map or mission. A missing mentioned mod is reported when its settings are activated. Mods and settings that the creator did not mention stay on their mod-defined safe baseline rather than inheriting arbitrary local gameplay settings.

After the mission ends, compatible mods restore the player's previous normal preset. Read-only Trail-owned host settings remain locked during play, while personal client settings stay editable.

For a free Singleplayer Skirmish, a Multiplayer host lobby, or a Trail opened through **Customize**, first select the Map and then press **Use Map modsettings**. Selecting a Map alone never changes a preset. The button is disabled when the selected archive has no valid Map settings, hidden in Trail Maker, and hidden for Multiplayer clients. Activating it shows the read-only preset **Map** for every compatible mod. Changing to another Map or leaving the lobby restores the previous local preset; otherwise the Map context remains active until the mission ends.

In Multiplayer only the host activates or clears Map settings. ExtendedData authenticates the host packet and binds it to the selected Map name and CRC; late joiners receive the active state. Missing mentioned mods are reported, while malformed data is rejected without partially applying it.

## Advanced JSON format

The Trail Maker is the authoritative editor. Manual editing is useful mainly for inspection or tooling; invalid files are ignored fail-closed.

The current format uses schema 3:

    {
      "schemaVersion": 3,
      "mods": {
        "StartConditions_Serp": {
          "playerSettings": [
            "EnableMod"
          ],
          "overrides": {
            "SetStartGoldHuman": 500
          }
        }
      }
    }

Each key under `mods` is the owning BepInEx plugin GUID. Property names in `playerSettings` use `Player/host`; values in `overrides` use `Fixed creator value`. A property must not occur in both collections. Unlisted properties use `Mod default`.

See `ExtendedData/Examples/01.modtrail.json.example` for a larger example. Property names and value types must match the installed compatible mod exactly, which is why saving through the Trail Maker is recommended.

## Troubleshooting

- If a mod does not appear, confirm that it supports the mission-preset compatibility contract and inspect `BepInEx/LogOutput.log` for `Map/Trail mod settings`.
- If a mod is reported missing, install the plugin whose GUID is listed by the Trail.
- If fixed values are wrong, load the Map/mission in its editor, select `Fixed creator value`, set the desired values in the owning mod's panel, and save again.
- Keep the `.trail` and `.modtrail.json` base names identical when moving files manually.

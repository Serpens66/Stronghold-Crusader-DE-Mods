# Mod settings in Custom Trails

`CustomCustomTrail` can store shared gameplay settings beside a Trail mission. Compatible installed mods are discovered automatically, so Trail creators normally configure everything in the Trail Maker and do not edit JSON by hand.

This guide is for Trail creators. Mod developers who want their settings to appear here should use [Custom Custom Trail compatibility for mod authors](Mod%20Compatibilty%20CustomCustomTrail.md).

## Create Trail mod settings

1. Install and enable `CustomCustomTrail` and the compatible gameplay mods that the Trail should use.
2. Open the Trail Maker and load or create the mission.
3. Open the `CustomCustomTrail` mod settings. Expand a compatible mod to configure its individual settings.
4. Choose one mode for each relevant setting:
   - **Mod default** uses the Trail-safe baseline supplied by that mod. For most gameplay mods, this disables the mod or feature.
   - **Player/host** uses the normal saved setting of the player in singleplayer or the multiplayer host. The Trail permits the feature but does not dictate its value.
   - **Fixed Trail value** stores the value currently shown in the compatible mod's settings and applies that exact value when the mission is played.
5. Configure every `Fixed Trail value` in the owning mod's normal settings panel, then save the Trail mission.

The selector on a mod heading changes all settings in that mod at once. `Mixed` means that its settings currently use different modes. Large related lists, including Unit Costs and Extra Features market multipliers, are deliberately presented as one atomic selection.

Only persistent host-controlled settings can become Trail rules. Personal, per-player, local and transient settings always remain under each player's control.

## Resulting files

Saving `Trail_Mission_1.trail` creates the optional sidecar:

    Trail_Mission_1.modtrail.json

The sidecar must remain beside its matching `.trail` file. If every compatible setting stays on `Mod default`, no mod needs to be mentioned and the file contains no active overrides. A missing sidecar likewise makes every compatible mod use its own Trail-safe baseline.

For a portable Coop Trail package, each mission uses the same base name:

    CoopMissions/01.coopmission.json
    CoopMissions/01.modtrail.json

When uploading a normal or Coop Trail to the Workshop, keep **Include mod settings** enabled to ship these files. Disable it only when deliberately publishing a Vanilla/default-only variant.

The old `.modjson` name is unsupported and is not imported, loaded or included in Workshop packages.

## What players need

Players need `CustomCustomTrail` and every mod explicitly mentioned by the mission. A missing mentioned mod is reported when the Trail is selected. Mods and settings that the creator did not mention stay on their mod-defined Trail baseline rather than inheriting arbitrary local gameplay settings.

After the mission ends, compatible mods restore the player's previous normal preset. Read-only Trail-owned host settings remain locked during play, while personal client settings stay editable.

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

Each key under `mods` is the owning BepInEx plugin GUID. Property names in `playerSettings` use `Player/host`; values in `overrides` use `Fixed Trail value`. A property must not occur in both collections. Unlisted properties use `Mod default`.

See `CustomCustomTrail/Examples/01.modtrail.json.example` for a larger example. Property names and value types must match the installed compatible mod exactly, which is why saving through the Trail Maker is recommended.

## Troubleshooting

- If a mod does not appear, confirm that it supports the mission-preset compatibility contract and inspect `BepInEx/LogOutput.log` for `Trail mod-settings compatibility rejected`.
- If a mod is reported missing, install the plugin whose GUID is listed by the Trail.
- If fixed values are wrong, load the mission in the Trail Maker, select `Fixed Trail value`, set the desired values in the owning mod's panel, and save again.
- Keep the `.trail` and `.modtrail.json` base names identical when moving files manually.

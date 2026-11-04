# SHCDE modding on Linux and Proton

Stronghold Crusader: Definitive Edition runs its Windows executable through Proton. Install the Windows SHCDE BepInEx loader and the official SHCDE Script Extender in the game directory. Our optional [LinuxModding helpers](../Helpers/LinuxModding/) check that installation and set the Wine override that loads BepInEx; they do not install mods or replace the Extender's updater.

This guide was checked against the locally installed Script Extender 2.14.1 and its release packaging. Use a current stable Proton version and a 64-bit Linux Steam installation. Install code mods only from authors you trust: they run with your user account's permissions.

## Install the loader and Script Extender

1. In Steam, open the game's **Properties > Compatibility**, select a current stable Proton version, and use **Manage > Browse local files** to open the game directory.
2. Download the Windows SHCDE BepInEx package from the [official releases](https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases). Copy the contents of its `Loader` directory into the game directory. Confirm that `winhttp.dll` and `BepInEx/core/BepInEx.dll` exist there.
3. Download the latest `SHCDESE_X.X.X.zip` from the [official Script Extender releases](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases). Extract it into the game directory and merge its folders. The release includes `BepInEx/plugins/000shcdese/SHCDESE.dll`, `info.json`, `data/mod-updater.sh`, `libredbird_thread_patch.so`, `msvcp140.dll` in the game root, and `shcde-vanilla.sh` in the game root.
4. Copy [install-linux.sh](../Helpers/LinuxModding/install-linux.sh) and [shcde-linux-launcher.sh](../Helpers/LinuxModding/shcde-linux-launcher.sh) from this repository into the game's `BepInEx/tools/LinuxModding/` directory.
5. Open a terminal in the game directory and run:

   ```sh
   bash "./BepInEx/tools/LinuxModding/install-linux.sh"
   ```

6. Resolve any missing-file reports. Then set this Steam launch option:

   ```text
   bash "./BepInEx/tools/LinuxModding/shcde-linux-launcher.sh" %command%
   ```

The launcher prepends `winhttp=n,b` to `WINEDLLOVERRIDES`, preserves any other overrides, and executes Steam's game command once. You can set the equivalent Wine override yourself instead of installing the optional helpers. Keep exactly one `%command%` in the Steam launch option.

Start the game through Steam. The Script Extender logo should appear in the main menu. Check `BepInEx/LogOutput.log` for BepInEx and Script Extender startup messages and the loaded Extender version. The setup checker confirms file presence, not that Proton can run every mod.

## Install and update mods

For a manual install, extract the mod author's package into the game directory so that the mod's own files land under `BepInEx/plugins/<ModName>/` (or the path specified by its author). Manual installs are updated and removed manually; keep all files supplied by the mod author together, including `info.json` when present. Check the mod's required Script Extender and game versions.

For an Extender-compatible Workshop mod, subscribe in Steam and start the game. The Extender compares subscribed map archives and their versions with manifests under `_SE/`. It stages installs, updates, and removals under `_SE/.staging`, starts `BepInEx/plugins/000shcdese/data/mod-updater.sh` through the host shell, and exits the game. The shell updater waits for the game process, applies the changes, and requests a restart through Steam. If `xdg-open` and `steam` are both unavailable, restart the game manually after the files have been applied.

The Extender rebuilds staging from Workshop content on the next start after an interrupted attempt. Do not move files out of `_SE/.staging` yourself. After three consecutive deployments that were not confirmed on a subsequent start, it stops retrying and logs the location of `_SE/.deploy_attempts`. Diagnose the cause first; removing that counter permits another attempt.

### Repair APIShared's first Workshop installation

If Script Extender cannot complete APIShared's first Workshop installation under Proton,
use the separate [Proton Workshop Fix ZIP (0.27.0)](https://github.com/SHCDE-APIShared/APIShared/releases/download/APIShared/v0.27.0/APIShared-ProtonWorkshopFix-v0.27.0.zip).
This independent patcher works before the APIShared runtime is installed. It is a
manual download, separate from the normal APIShared ZIP, Workshop package and modpack.

1. Close the game and open the downloaded ZIP.
2. Copy `BepInEx/patchers/APIShared.ProtonWorkshopFix.dll` into the same path inside
   your SHCDE game directory. Keep exactly one copy of this patcher.
3. Keep [APIShared subscribed in Steam Workshop](https://steamcommunity.com/workshop/filedetails/?id=3818609220),
   start the game through Steam and accept the normal Script Extender installation prompt.

The patcher requires the Windows SHCDE BepInEx loader and Script Extender described
above, Linux/Proton, host Bash, `flock` and GNU coreutils. It contains no APIShared
runtime, and installing its DLL does not overwrite game or Script Extender files.
It stays inactive on native Windows and under externally managed launches.
Close the game before replacing or removing it.

Normally no path configuration is needed. If automatic discovery fails, copy
`APIShared.ProtonPaths.cfg.example` from the ZIP to
`BepInEx/config/APIShared.ProtonPaths.cfg`. In its `[ProtonPaths]` section, set
`HostGameDirectory` to the absolute Linux path of this exact game installation and/or
`WineBashPath` to the absolute Windows/Wine path of the host Bash, as needed.
Restart the game after editing. APIShared's own updater uses the same optional configuration.

If the Extender's deployment counter is already exhausted, successful preflight allows
one additional confirmed attempt. Declining does not consume it; a failed retry does
not repeat automatically. Inspect `BepInEx/LogOutput.log` and the attempt's `journal.log`
under `BepInEx/cache/APIShared.ProtonWorkshopFix/`. Leave `_SE/.staging` in place.

Automated path, IL, retry, package and native Bash checks do not establish live
Proton/Steam compatibility. First installation, path mappings and overrides, failed
retries, Steam restart and subsequent APIShared updates/recovery still require live
acceptance. See APIShared's [installation contracts](https://github.com/SHCDE-APIShared/APIShared/blob/main/docs/WORKSHOP.md#linux-paths-and-first-install-repair)
and [platform acceptance limits](https://github.com/SHCDE-APIShared/APIShared/blob/main/docs/WORKSHOP.md#platform-acceptance-limits).

## What the bundled Linux files do

- `BepInEx/plugins/000shcdese/libredbird_thread_patch.so` is the native RedBird Proton thread companion shipped with the Extender. Keep it beside `SHCDESE.dll`.
- `msvcp140.dll` is shipped in the game root for PolyHook2 compatibility under Wine/Proton. If PolyHook2 initialization still crashes, check that this bundled file is present before troubleshooting its version.
- `shcde-vanilla.sh` is the Extender's separate game-root helper for toggling mods by renaming `winhttp.dll`. Run it from a terminal with `bash "./shcde-vanilla.sh" --disable` for a vanilla launch or `--enable` to restore mods; it also launches the game through Steam. Do not run it while the game is active.

## Troubleshooting

### BepInEx or the Extender does not load

- Run `install-linux.sh` again and inspect missing `winhttp.dll`, `BepInEx/core/BepInEx.dll`, Extender files, or RedBird companion reports. Re-extract the complete official package when an Extender file is missing.
- Confirm that the Steam launch option contains exactly one `%command%` and loads `winhttp=n,b`. Use the Windows SHCDE BepInEx loader, not a native Unix BepInEx build.
- Read `BepInEx/LogOutput.log`. The setup checker does not diagnose runtime initialization failures.

### Workshop deployment does not finish

- Read the latest Extender messages in `BepInEx/LogOutput.log` and check whether `_SE/.deploy_attempts` exists. Correct the logged problem before resetting the retry counter.
- If APIShared's first Workshop installation is blocked, follow the [manual repair steps](#repair-apishareds-first-workshop-installation) above. With the patcher installed, inspect its `journal.log` as well as `BepInEx/LogOutput.log` before attempting another deployment.
- Without the patcher, the Extender's Wine backend needs a host `sh` at `Z:\bin\sh` or `Z:\usr\bin\sh`, and updater/game paths reachable through that mapping. The patcher and APIShared's own updater instead translate active Wine drive mappings and use host Bash; use the optional path configuration above if discovery fails.
- A translated uninstall manifest is also needed to remove unsubscribed mods. If its path cannot be translated, the Extender warns that removals will be skipped.
- Steam Flatpak must have access to the Steam library containing the game. Grant access to that library if the updater reports path or permission failures.
- If the files were applied but the game did not reopen, start it through Steam manually. Leave `_SE/.staging` to the Extender.

## Remove the optional helpers

Remove their Steam launch option and the installed `BepInEx/tools/LinuxModding/` directory. If you still want BepInEx mods, supply the equivalent `winhttp=n,b` override yourself. The Extender's Workshop updater is independent of these helpers.

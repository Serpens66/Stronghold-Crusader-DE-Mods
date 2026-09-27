# SHCDE modding on Linux and Proton

Stronghold Crusader: Definitive Edition runs its Windows executable through Proton. Install the Windows SHCDE BepInEx loader and the official SHCDE Script Extender in the game directory. Our optional [LinuxModding helpers](../Helpers/LinuxModding/) check that installation and set the Wine override that loads BepInEx; they do not install mods or replace the Extender's updater.

This guide was checked against the locally installed Script Extender 2.11.0 and its release packaging. Use a current stable Proton version and a 64-bit Linux Steam installation. Install code mods only from authors you trust: they run with your user account's permissions.

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
- The Wine backend needs a host `sh` at `Z:\bin\sh` or `Z:\usr\bin\sh`. The updater script and game paths must be reachable through Wine's `Z:` mapping so the Extender can translate them into Linux paths. Check the mapping and permissions if those messages appear in the log.
- A translated uninstall manifest is also needed to remove unsubscribed mods. If its path cannot be translated, the Extender warns that removals will be skipped.
- Steam Flatpak must have access to the Steam library containing the game. Grant access to that library if the updater reports path or permission failures.
- If the files were applied but the game did not reopen, start it through Steam manually. Leave `_SE/.staging` to the Extender.

## Remove the optional helpers

Remove their Steam launch option and the installed `BepInEx/tools/LinuxModding/` directory. If you still want BepInEx mods, supply the equivalent `winhttp=n,b` override yourself. The Extender's Workshop updater is independent of these helpers.

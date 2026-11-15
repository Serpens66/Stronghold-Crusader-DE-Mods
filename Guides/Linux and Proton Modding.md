# SHCDE modding on Linux and Proton

This guide explains how to install and troubleshoot mods for **Stronghold Crusader: Definitive Edition** on Linux using Steam and Proton. You need the Windows SHCDE BepInEx loader, the Script Extender, and any dependencies named by your mods.

Use a current stable Proton version with 64-bit Steam. Linux/Proton support has not been fully verified in live Steam gameplay, so successful installation alone does not guarantee that every mod works. Install code mods only from authors you trust: they run with your user account's permissions.

## Install the loader and Script Extender

Close the game before copying or replacing files.

1. In Steam, open the game's **Properties > Compatibility** and select a current stable Proton version.
2. Use **Manage > Browse local files** to open the game directory. All paths below start in this directory.
3. Download the Windows SHCDE BepInEx package from the [official loader releases](https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases). Copy the contents of its `Loader` directory into the game directory. You should now have `winhttp.dll` and `BepInEx/core/BepInEx.dll`.
4. Download `SHCDESE_X.X.X.zip` from the [official Script Extender releases](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases). Extract the complete package into the game directory and merge its folders. Keep all bundled files, including `msvcp140.dll` in the game directory and `libredbird_thread_patch.so` beside `BepInEx/plugins/000shcdese/SHCDESE.dll`.
5. In Steam's **Properties > General > Launch Options**, enter:

   ```text
   WINEDLLOVERRIDES="winhttp=n,b" %command%
   ```

   This tells Proton to load BepInEx through `winhttp.dll`. If you already use launch options or DLL overrides, combine them carefully and keep exactly one `%command%`. The optional launcher below preserves existing DLL overrides for you.
6. Start the game through Steam. Look for the Script Extender logo in the main menu. If it does not appear, check `BepInEx/LogOutput.log` for startup errors.

## Optional: check the installation with our helpers

The [LinuxModding helpers](../Helpers/LinuxModding/) check for required files and provide a launcher that preserves existing DLL overrides. They do not download or install mods.

1. Download the actual script files [install-linux.sh](../Helpers/LinuxModding/install-linux.sh) and [shcde-linux-launcher.sh](../Helpers/LinuxModding/shcde-linux-launcher.sh), rather than saving their GitHub HTML pages.
2. Create `BepInEx/tools/LinuxModding/` in the game directory and place both scripts there.
3. Open a terminal in the game directory and run:

   ```sh
   bash "./BepInEx/tools/LinuxModding/install-linux.sh"
   ```

4. Resolve any `[MISSING]` reports. Replace the Steam launch option from the installation steps with:

   ```text
   bash "./BepInEx/tools/LinuxModding/shcde-linux-launcher.sh" %command%
   ```

The checker confirms file presence; it cannot prove that Proton or an individual mod will run correctly.

## Install and update mods

### Steam Workshop

Subscribe to the mod and all required dependencies, then start the game through Steam. For Script Extender packages, accept the installation prompt. The game closes while the updater applies the files, then requests a Steam restart. If it does not reopen after the update finishes, start it through Steam manually.

APIShared also provides **Mod Options > APIShared > Workshop** for packages it manages. Follow its update or recovery prompts. If an interrupted update needs recovery, keep its backups and diagnostic files until recovery is complete. Packages managed by an external mod manager should be updated through that manager.

Do not move files out of `_SE/.staging` yourself. This is the Script Extender's temporary installation folder.

### Manual downloads

Follow the mod author's installation instructions and extract the package into the game directory. A typical mod ends up under `BepInEx/plugins/<ModName>/`. Keep its supplied files together, including `info.json` when present, and check its required game, Script Extender and dependency versions.

For APIShared, copy **both** the `BepInEx/plugins` and `BepInEx/patchers` contents from the same package. Do not place a second complete APIShared package inside another plugin folder. Manually installed mods must also be updated or removed manually.

## Repair APIShared's first Workshop installation

Use this section if the Script Extender cannot finish installing APIShared under Proton. The repair is a separate download and can run before APIShared itself is installed.

1. Close the game.
2. Open the [APIShared releases](https://github.com/SHCDE-APIShared/APIShared/releases) and download the separate **`APIShared-ProtonWorkshopFix-v<version>.zip`** asset. Use a release that offers this asset; the normal APIShared ZIP is a different package.
3. Copy `BepInEx/patchers/APIShared.ProtonWorkshopFix.dll` from that ZIP into the same path inside the game directory. Keep exactly one copy of this DLL.
4. Keep [APIShared subscribed in Steam Workshop](https://steamcommunity.com/workshop/filedetails/?id=3818609220), start the game through Steam, and accept the normal Script Extender installation prompt.

The repair requires Linux/Proton, host Bash, `flock` and GNU coreutils, in addition to the loader and Extender above. It contains no APIShared runtime. It stays inactive on native Windows and when an external mod manager controls the launch. Close the game before replacing or removing it.

If previous failures have blocked further installation attempts, the repair permits one additional confirmed attempt after its checks succeed. If that attempt also fails, inspect `BepInEx/LogOutput.log` and the attempt's `journal.log` under `BepInEx/cache/APIShared.ProtonWorkshopFix/` before trying again. Leave `_SE/.staging` in place.

### Only if automatic path detection fails

Normally no path configuration is needed. If the log reports that the game directory or Bash cannot be found:

1. Copy `APIShared.ProtonPaths.cfg.example` from the repair ZIP to `BepInEx/config/APIShared.ProtonPaths.cfg`.
2. Under `[ProtonPaths]`, fill in only the override you need:
   - `HostGameDirectory`: the absolute **Linux path** to this exact game installation.
   - `WineBashPath`: the absolute **Windows/Wine path** to the Linux Bash executable, such as `Z:\bin\bash` when Z: maps to the Linux root.
3. Restart the game after editing.

APIShared's own updater uses the same optional configuration. Further details are in the [APIShared Linux installation guide](https://github.com/SHCDE-APIShared/APIShared/blob/main/docs/WORKSHOP.md#linux-paths-and-first-install-repair).

## Troubleshooting

### BepInEx or Script Extender does not load

- Check that you installed the **Windows SHCDE** BepInEx loader and extracted both packages into the game directory.
- Check the Steam launch option: it must enable `winhttp=n,b` and contain exactly one `%command%`.
- If you use our helpers, run the checker again and resolve missing-file reports.
- Re-extract the complete Extender package if its bundled files are missing. Keep `libredbird_thread_patch.so` beside `SHCDESE.dll` and `msvcp140.dll` in the game directory.
- Read `BepInEx/LogOutput.log`. For a report, follow [How to Report Errors](How%20to%20Report%20Errors.md).

### Workshop installation does not finish

- Read the latest errors in `BepInEx/LogOutput.log`. If APIShared's first installation is blocked, use the repair steps above.
- The Script Extender stops retrying after three unsuccessful deployments. Its log names `_SE/.deploy_attempts`; correct the logged cause before removing that counter to permit another attempt.
- Without the repair, the Extender needs Linux `sh` reachable as `Z:\bin\sh` or `Z:\usr\bin\sh`. APIShared and the repair use Wine's active drive mappings and host Bash. Use the optional configuration above if their automatic detection fails.
- If the log reports path or permission errors under Steam Flatpak, check that Steam has access to the library containing the game.
- Wait for an updater to finish before restarting the game. If installation completed but automatic restart failed, launch the game through Steam manually.

## Temporarily start without mods

Close the game, open a terminal in the game directory, and run the Extender's bundled helper:

```sh
bash "./shcde-vanilla.sh" --disable
```

It disables the BepInEx loader and starts the game through Steam. To restore mods, close the game and run:

```sh
bash "./shcde-vanilla.sh" --enable
```

## Remove the optional helpers

Remove their Steam launch option and the installed `BepInEx/tools/LinuxModding/` directory. If you still want to use BepInEx mods, restore the direct launch option from the installation section. The helpers are separate from the Extender's Workshop updater and the APIShared repair DLL.

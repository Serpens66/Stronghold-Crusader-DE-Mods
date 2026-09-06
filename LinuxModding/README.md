# SHCDE Linux/Proton Setup Helper

This optional helper makes the initial setup of BepInEx and the latest SHCDE Script Extender easier on Linux with Proton. It is not a BepInEx mod and does not replace or intercept any Script Extender function.

Script Extender 2.2.0 natively handles Wine detection, host-path translation, Workshop staging, updates and removals, waiting for the game to exit, and restarting it through Steam. The helper only checks the required files and supplies the `winhttp` Wine override needed to load BepInEx.

## Requirements

- A 64-bit Linux system with the Linux Steam client
- Stronghold Crusader: Definitive Edition running through a current stable Proton version
- The Windows SHCDE BepInEx 5 package
- The latest SHCDE Script Extender release (version 2.2.0 or newer)

Only install code mods from authors you trust. BepInEx mods execute code with the permissions of your user account.

## Setup

1. In Steam, open the game's **Properties > Compatibility**, force a current stable Proton version, and then select **Manage > Browse local files**.
2. Download the latest SHCDE BepInEx package from <https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases>. Copy everything from its `Loader` directory into the game directory. Use the Windows package because Proton runs the Windows game executable.
3. Confirm that `winhttp.dll` and `BepInEx/core/BepInEx.dll` exist in the game directory.
4. Download the newest `SHCDESE_X.X.X.zip` from <https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases> and extract it directly into the game directory, merging existing folders.
5. Copy `install-linux.sh` and `shcde-linux-launcher.sh` into `BepInEx/tools/LinuxModding`.
6. Open a terminal in the game directory and run:

       bash "./BepInEx/tools/LinuxModding/install-linux.sh"

7. Resolve every reported problem, then set this exact Steam launch option:

       bash "./BepInEx/tools/LinuxModding/shcde-linux-launcher.sh" %command%

The launcher adds `WINEDLLOVERRIDES=winhttp=n,b`, preserves unrelated Wine overrides, and executes Steam's game command exactly once. Experienced users may set the equivalent Wine override directly instead of using the launcher.

Start the game normally. The Script Extender logo should appear in the main menu, and `BepInEx/LogOutput.log` should show that BepInEx and the installed SHCDESE version loaded.

## Workshop mods

Subscribe, update, or unsubscribe through the Steam Workshop and start the game. When files must be changed, the official Script Extender starts `data/mod-updater.sh`, closes the game, applies the staged changes, and requests a Steam restart. This helper does not participate in that process.

If the updater cannot locate `xdg-open` or `steam`, it applies the changes but asks you to restart the game manually.

## Troubleshooting

### BepInEx does not load

- Run `install-linux.sh` and resolve missing `winhttp.dll` or `BepInEx/core/BepInEx.dll` entries.
- Check that the Steam launch option contains exactly one `%command%`.
- Use the Windows SHCDE BepInEx package, not a native Unix BepInEx build.
- Inspect `BepInEx/LogOutput.log`.

### Script Extender or Workshop updates fail

- Install the complete latest official archive again if the Extender is older than 2.2.0 or if `SHCDESE.dll`, `info.json`, `data/mod-updater.sh`, or `libredbird_thread_patch.so` is missing.
- Do not manually move files from `_SE/.staging`; start the game again so the Extender can retry.
- Ensure the game directory is writable and reachable through Wine's `Z:` drive mapping.
- If files were updated but Steam did not reopen the game, restart it manually.

### Steam Flatpak and custom libraries

Steam's Flatpak sandbox must be allowed to access the library containing the game. Grant access to that library or move the game into Steam's default library if the updater reports path or permission failures.

### Temporarily start without mods

Remove the custom Steam launch option before using an Extender-provided vanilla-start helper. Restore the option before loading BepInEx mods again.

## Removing the helper

Remove the custom Steam launch option and delete `BepInEx/tools/LinuxModding`. Script Extender's native Linux/Proton updater remains responsible for Workshop changes; only the convenience setup check and automatic `winhttp` override are removed.

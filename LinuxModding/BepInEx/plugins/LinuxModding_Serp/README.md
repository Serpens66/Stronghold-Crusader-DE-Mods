# SHCDE Linux Modding Compatibility

This package makes BepInEx mods from the **Stronghold Crusader: Definitive Edition** Steam Workshop update correctly under Linux/Proton without installing PowerShell.

The initial setup is manual. After that, subscribing, updating and unsubscribing supported Workshop mods is handled automatically when the game starts.

## Requirements

- A 64-bit Linux system
- The Linux Steam client
- Stronghold Crusader: Definitive Edition installed through Steam
- A current Proton version selected for the game
- BepInEx 5 for Windows
- The SHCDE Script Extender

Only install BepInEx code mods from authors you trust. Such mods execute native or managed code with the same permissions as your user account.

## First-time setup

### 1. Select Proton

1. Open Steam.
2. Right-click **Stronghold Crusader: Definitive Edition**.
3. Select **Properties**.
4. Open **Compatibility**.
5. Enable **Force the use of a specific Steam Play compatibility tool**.
6. Select the newest stable Proton version available in Steam.

Do not use Proton's option to delete the compatibility data after installing mods. It is normally safe to change the selected Proton version later.

### 2. Open the game directory

1. Right-click the game in Steam.
2. Select **Manage > Browse local files**.

Keep this directory open. All archives in the following steps must be extracted into this directory—the directory containing:

```text
Stronghold Crusader Definitive Edition.exe
```

Do not extract the files into a second nested directory.

### 3. Install BepInEx

1. Download the latest SHCDE BepInEx package from:
   <https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases>
2. Open the downloaded archive.
3. Copy everything inside its `Loader` directory into the game directory.

The following files must now exist:

```text
winhttp.dll
BepInEx/core/BepInEx.dll
```

Use the Windows BepInEx build even though the host operating system is Linux. Proton runs the Windows version of the game.

### 4. Install the Script Extender

1. Download the newest `SHCDESE_X.X.X.zip` from:
   <https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases>
2. Extract the archive directly into the game directory.
3. Allow existing folders to be merged.

Verify that this file exists:

```text
BepInEx/plugins/000shcdese/SHCDESE.dll
```

Keep the `msvcp140.dll` supplied by the Script Extender in the game directory. Do not download DLL files from unofficial DLL websites.

### 5. Install Linux Modding Compatibility

Copy the complete `LinuxModding_Serp` directory into:

```text
BepInEx/plugins/
```

The resulting files must include:

```text
BepInEx/plugins/LinuxModding_Serp/LinuxModding.dll
BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh
BepInEx/plugins/LinuxModding_Serp/install-linux.sh
```

Open a terminal in the game directory and run:

```bash
bash "./BepInEx/plugins/LinuxModding_Serp/install-linux.sh"
```

The script only checks the installation and prints the required Steam launch option. It does not modify Steam settings.

### 6. Set the Steam launch option

1. Open the game's **Properties** in Steam.
2. In **General > Launch Options**, enter exactly:

```text
bash "./BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh" %command%
```

This launcher automatically sets the `winhttp.dll` override required by BepInEx. Do not add a second `%command%` or place another command after it.

### 7. Start the game once

Start the game normally from Steam. In the main menu, the Script Extender logo should be visible.

You can also verify the installation in:

```text
BepInEx/LogOutput.log
```

The log should contain messages similar to:

```text
Intercepted Script Extender MapModManager.LaunchUpdaterAndExit().
Linux Workshop updater bridge active.
```

## Installing and updating Workshop mods

After the first-time setup, no manual file copying is needed for supported Workshop mods:

1. Subscribe to a compatible mod in the SHCDE Steam Workshop.
2. Wait for Steam to finish downloading the Workshop item.
3. Start the game.
4. When the Script Extender reports that mods changed, confirm the message.
5. The game closes, the Linux launcher applies the staged files and the game starts again automatically.

Updates use the same process. To remove a Workshop mod, unsubscribe and start the game once. Steam may need several minutes to report a new subscription or unsubscription.

Do not manually start a second game instance while an update is being applied.

## Installing a mod without Workshop

For a manually downloaded BepInEx mod, copy its complete folder into:

```text
BepInEx/plugins/
```

Then restart the game. The Linux compatibility launcher is primarily needed for the Script Extender's Workshop updater.

## Troubleshooting

### The game does not start or BepInEx does not load

- Run `install-linux.sh` again and resolve every `[MISSING]` entry.
- Verify the Steam launch option contains exactly one `%command%`.
- Verify that the Windows BepInEx package was installed, not a native Unix BepInEx build.
- Check `BepInEx/LogOutput.log`.

### The Script Extender crashes during startup

- Reinstall the newest official Script Extender release.
- Verify that its current `msvcp140.dll` is in the game directory next to the game executable.
- Do not use DLL download websites.

### A Workshop update fails

- Do not manually move files from `_SE/.staging`.
- Start the game again through the configured Steam launch option. The Script Extender can stage the update again.
- Check `BepInEx/LogOutput.log` and the Steam launch output for a line beginning with `LinuxModding: ERROR:`.
- Ensure the Steam library and game directory are writable by your Linux user.

### Steam is installed through Flatpak

The game and its Steam library must be accessible inside the Steam Flatpak sandbox. If Steam itself can install and launch the game but the launcher reports permission errors, grant the Steam Flatpak access to the custom library location or move the game into Steam's default library.

### Temporarily start without mods

Remove the custom launch option before using any vanilla-start helper supplied by the Script Extender. Restore the Linux Modding Compatibility launch option before using Workshop code mods again.

## Removing Linux Modding Compatibility

1. Remove the custom Steam launch option.
2. Make sure the game is closed.
3. Delete `BepInEx/plugins/LinuxModding_Serp`.

Without the compatibility launcher, the Script Extender's PowerShell-based Workshop deployment is not expected to work reliably under Linux unless PowerShell is configured separately inside the Proton environment.

## Project status

This compatibility layer has automated Windows tests for its updater protocol, path validation, rollback, restart handling and Script Extender method interception. Final compatibility still depends on the Linux distribution, Steam packaging, Proton version and the currently installed Script Extender release.

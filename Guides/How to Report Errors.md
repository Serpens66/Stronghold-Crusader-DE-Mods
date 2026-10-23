# How to Report Errors

If something goes wrong while using my mods, please send a short description and the files listed below. You do not need to understand the log files yourself.

## 1. Bugs and errors while the game is still running

For most reports, start with **`LogOutput.log` and a description of the problem**. I will ask for more files if needed.

### Where to find LogOutput.log

1. Open your Steam Library.
2. Right-click **Stronghold Crusader: Definitive Edition** and select **Manage > Browse local files**.
3. Open the **BepInEx** folder.
4. Copy **`LogOutput.log`** somewhere safe and attach that copy to your report.

The path inside the **game install** folder is:

    BepInEx\LogOutput.log

**Save the log after the problem happens and before starting the game again.** A new launch may replace logs or make the relevant session harder to find. Send the complete file, even if it looks empty or ends suddenly. Please do not send only a screenshot or a few selected lines.

### What to include in your report

- **What happened, and what you expected instead.** For example: "I enabled this option, but the building still behaves as before."
- **How to reproduce it:** the steps that lead to the problem, and whether it happens every time or only sometimes.
- **Where it happened:** singleplayer, multiplayer, the map editor, or a menu.
- **Your game and mod versions**, including the Script Extender and Fixes mod if installed. Mention any other active mods. If you cannot find a version, send the report anyway.
- A **screenshot or short video** if it helps show the problem.

If the issue depends on a particular match, attach a **save made before the problem** if available. Include the custom map or custom AI lord files if they are needed to reproduce it. Mention any relevant mod settings or attach your settings preset.

For multiplayer problems, say whether you were the host or a client and which players were affected.

## 2. Crashes to desktop

A crash means the game unexpectedly closes and returns you to the desktop. In addition to the report details above, please collect the following files **before launching the game again**.

### A. BepInEx log

Send the complete file from the game folder:

    BepInEx\LogOutput.log

### B. Crash log and memory dump

Open this folder inside the game folder:

    BepInEx\crashdumps\

Look for files matching the time of your crash. They normally have a name like:

    SHCDESE-crash-<date>-<time>-pid<number>-tid<number>.log
    SHCDESE-crash-<date>-<time>-pid<number>-tid<number>.dmp

Send **both the `.log` and `.dmp` with the same name**. Put them in a ZIP archive to reduce their size. If you are unsure which files belong to the crash, send the folder as a ZIP and tell me the approximate crash time.

If the folder is missing or empty, mention that and send the other files anyway. Not every crash produces a dump. If I ask you to try again, check that `EnableNativeCrashHandler = true` is set under `[Bootstrap]` in `BepInEx\config\SHCDESE.cfg` before launching the game.

A dump can sometimes be created during startup even when the game continues running normally. This is why the actual crash time matters.

### C. Unity player log (Windows)

Press **Windows + R**, paste this path, and press Enter:

    %USERPROFILE%\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition

Send **`Player.log`**. If you have already restarted the game, also send **`Player-prev.log`** if it exists; it may contain the previous session.

### D. Additional files, if available

From the game folder, also include:

- **`BepInEx\SerpsModsDiagnostics\`**: send the whole folder if it exists. These extra diagnostic files are only created when the relevant recording option was enabled beforehand.
- **`BepInEx\config\`**: send the configuration folder as a ZIP so I can check the mod and loader settings. Include a mod settings preset if you use one.
- A **save from shortly before the crash**, plus any custom map or AI lord files needed to load it.

### E. Describe the crash

Please tell me:

- The approximate **date and time** of the crash.
- What you were doing immediately before it happened, such as loading a save, placing a building, or ending a mission.
- Whether you can make it happen again, and how.
- In multiplayer: whether the host, a client, or everyone crashed, and whether the other players could continue. Logs from the host and the affected client are especially useful.

**Privacy:** Logs and dumps can contain personal information such as your Windows username, file paths, or Steam ID. Memory dumps may contain other data held in the game process. Share them privately with the mod author if you do not want to post them publicly.

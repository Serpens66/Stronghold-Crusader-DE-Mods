# Vanilla AIC Exporter

One-shot BepInEx utility for exporting the AIC data embedded in the currently
installed `CrusaderDE.dll` as editor-compatible `.lordjson` files.

## Export

1. Close the game.
2. Run `export_vanilla_lordjson.bat` as administrator.
3. Wait until the batch file reports success.
4. The exported files are under `Exports/<timestamp>_game-<version>/`.

The launcher builds and installs the exporter, starts the game, waits without a
fixed timeout for completion, and copies the finished export back into this
workspace. It then waits for the game to close and removes the exporter from
the game's `BepInEx/plugins` directory. A skirmish does not need to be started.

The plugin only exports when the launcher has created its one-shot request
file. It removes that request after success as an additional safeguard.

Every export includes `manifest.json` with the Steam build ID, executable and
native-library versions, and the SHA-256 of the installed `CrusaderDE.dll`.
Reserved empty DLC slots are skipped. `SK_X1` through `SK_X8` and `SK_TEMP` are
runtime/custom slots and are never treated as vanilla AICs.

# SHCDE Dispatcher Lifecycle Probe

This standalone BepInEx plugin reproduces the SHCDE-SE
`UnityMainThreadDispatcher` startup lifecycle issue.

It uses no reflection, coroutine, manual `Update()` invocation, external queue
pump, or `EnqueueAndWait()` call. The dispatched worker action only records its
managed thread ID and does not call a Unity API.

## Running the probe

1. Build and install the unchanged Script Extender.
2. Run `build.bat /nopause` from this directory.
3. Start the game and wait for the main menu.
4. Close the game and inspect `BepInEx/LogOutput.log`.

The lifecycle failure is confirmed when the log shows all of the following:

- `OBSERVER DESTROYED` with `updates=0`;
- `FIRST BEFORE-RENDER` with `earlyClrNull=False` and `earlyUnityNull=True`;
- `BACKGROUND RESULT BEFORE REPAIR` with a null worker instance and identical
  caller/dispatch thread IDs that differ from the startup thread;
- `INSTANCE AFTER OBSERVATION` with a live instance and
  `sameClrInstance=False`.

# Troop Action Button Test

Local UI testmod, GUID `TroopActionButtonTest_Serp`, version 0.1.0.
Requires APIShared 0.31.0+ and Script Extender 2.14.0+. It works without
ExtraFeatures or BugfixesAndQoL; those mods are optional participants in the mixed-selection test.

## Build

Run `build.bat /nopause /noinstall`. This driver always builds the current working
sources without installing anything or switching branches. It runs the command
regressions and scoped source checks, compiles against the real game assembly,
and creates `.local/TroopActionButtonTest.zip`.

The SDK defaults to the workspace's built APIShared output. If that DLL is older
than 0.31.0, first build the current APIShared sources without installation, or
set `SHCDE_API_SHARED_DIR` to an explicit compiled 0.31.0+ SDK directory. The
testmod does not rebuild, install or ship APIShared. Its local compile-only build
does not claim the normal APIShared release/package proof.

For an actual game test, the installed APIShared must also be 0.31.0+; any APIShared
update must use matching plugin and patcher files from one package. Compilation
against a newer SDK does not update the installed game.
The mixed-selection acceptance also needs current ExtraFeatures/BugfixesAndQoL
builds using the typed button registration; older builds with legacy hosts do
not participate in the new overflow menu.

## Game acceptance

1. Select one or more of your normal European archers. Six numbered test actions
   become eligible. APIShared decides which fit directly and places the rest under
   its existing "More actions" opener. Hover shows each action's number.
2. Click actions 1 through 6, reopening the menu when automatic closing is enabled.
   Each accepted click must produce exactly one `BUTTON_TEST_CLICK` line, such as
   `BUTTON_TEST_CLICK id=test-3 player=1 selection=12`, with a millisecond timestamp.
   Repeated clicks must produce repeated lines. No unit/game state is changed.
3. Add your knight and/or assassin to the selection, with the respective consumer
   features enabled. Knight (priority 100), Assassin Climb (200) and test actions
   (300..305) must share the same dynamically assigned direct slots and overflow.
   Each existing real action keeps its normal gameplay behavior.
4. Switch to a selection without archers, clear selection, or select foreign troops.
   Test actions must disappear. A stale test command must not log a click.
5. Check auto-close on/off, closing/reopening, hover on disabled Knight actions,
   pause, editor selection, save load and map changes. The menu must resize to
   applicable actions, and menu clicks must not issue world orders.

`BUTTON_TEST_LOADED` and `BUTTON_TEST_READY` identify registration. The first
eligible selection additionally logs `BUTTON_TEST_ARCHER_CONTEXT`, confirming a
later selection callback after startup. Missing registration/resources are logged
as errors rather than replaced with another layout implementation.

Automated command tests do not establish native Noesis layout, actual hit testing,
input timing, or real multiplayer/editor behavior. Those checks remain pending
until the game acceptance steps are performed.

# PopularityTest validation

## Dependency contract

Use the existing installed APIShared 0.9.2 assembly, SHA-256
`4EF262A5F69F16EF6B6E5238606F5013B6400B83D2F8C354E5C53F4EFDD52BC1`.
This is the explicitly requested exception to the development consumer-package
selection. The project validates that identity, does not build APIShared, and
does not package or copy any dependency DLL. `tests/dependency-before.json` records
the initial installed files and local APIShared sources/package. Verification
compares these unchanged snapshots before and after installation.

## Automated checks

Run `verify.ps1` outside the sandbox, because the maintained preset harness uses
atomic File.Replace. It checks all 41 catalog/default mappings, category ranges,
checkbox/slider/input adjacency, one-point slider steps, localized keys, tooltips,
CRLF, JSON/lifecycle rules, scoped dependency/source boundaries, production API
signatures and public XAML bindings against the installed assemblies.

The separate preset test assembly includes the maintained source-based preset
harness and this mod's unchanged settings and override implementation. It tests
restart persistence, client write rejection, authorized host updates, preservation
of local preferences, mission editing locks, reset defaults, disabled no-op,
zero/negative overrides, map-boundary restoration, duplicate checkpoints, rollback
and foreign overlay retention. These fixtures do not replace installed-assembly
type checks or prove actual Steam delivery.

The canonical build/install entry is `build.bat /nopause`. It runs preflight,
requires the game to be closed, builds only PopularityTest, installs only its own
plugin directory, verifies installed file hashes and rechecks dependencies.

## In-game acceptance still required

- Open Mod Options / Popularity Test: check layout, numeric input, tooltips,
  inactive grey sliders, preset loading/saving and reset.
- New game and editor: default checkboxes cause no overrides. Activate one source
  before a new map; confirm its component report matches the chosen point value.
- Check `POPULARITY_BOOT`, `POPULARITY_READY`, `POPULARITY_SESSION` and a later
  `POPULARITY_CALLBACK` in LogOutput.log; inspect errors rather than treating READY
  alone as a surviving runtime.
- Test ration/no-food branches, all tax and coverage tiers, signed religion/ale
  values and fear-factor multiplication. Negative religion retains the SE's
  unsigned auxiliary religion counter behavior; its meaning remains unconfirmed.
- Save/load, mission end, failed initialization, restart and a subsequent map
  with unchecked settings must not retain owned overrides. In-game editing is
  locked. Existing plague correction and event effects remain independently active.
- Real multiplayer requires host and client logs proving multiple humans, matching
  effective settings, late join, map transition and saved-game restoration. No
  real multiplayer pass may be inferred from the preset fixture.

## Display correction verification (2026-10-17)

The previous build's correct simulation parameters did not update Vanilla's
independent report calculation. The new presentation adapter registers at
APIShared 0.9.2 GuiChecks Post, reads effective values only for successfully
applied session toggles, and updates public HUD properties without changing
PlayState or native resources. Church/cathedral amounts use signed int values;
the Vanilla identity byte and localized building label remain untouched.

Passed before installation:
- 1,761 report/callback assertions: all 41 parameters, disabled identity,
  vanilla/min/max/zero/sign changes, coverage boundaries, preview selection,
  integer church sums, event remainder, repeated callbacks, HUD recreation,
  session gates, perspective state, direction and PlayState immutability.
- 227 existing settings/persistence/authority/override-ownership assertions and
  maintained preset-storage harness. Harness has an existing CS0649 warning.
- Real installed assembly signature/public-access check, including SE enums;
  scoped Shared boundaries, dependency metadata, runtime lifecycle, XAML,
  tooltips, locale parity and CRLF checks.
- Installed APIShared remains 0.9.2.0, SHA-256
  4EF262A5F69F16EF6B6E5238606F5013B6400B83D2F8C354E5C53F4EFDD52BC1.

The old dependency-before.json records the earlier task, not today's mutable
APIShared checkout. dependency-display-before.json captures this task's source
and installed-package baseline; both are retained. No APIShared files were edited.

Explicit working-source build: build.bat /working /nopause. The default branch
menu remains available; /working does not create a commit or branch worktree.
The runtime now logs POPULARITY_DISPLAY once per active session. This is the
pending real-game callback evidence; tests use engine/UI boundary stand-ins.
In-game visual acceptance, editor/save/map transitions and real multiplayer
remain unverified until a new game run. No such success is inferred from tests.

Build/install completed 2026-10-17 16:11:02, exit code 0, via elevated
build.bat /working /nopause. Installed PopularityTest SHA-256:
351072a6b2213a4e5345f2b41a0f01d9abd14de6bd87617ebf89bee00b60f8b4
All installed mod files matched the build; installed APIShared identity and
the task dependency snapshot remained unchanged after installation.

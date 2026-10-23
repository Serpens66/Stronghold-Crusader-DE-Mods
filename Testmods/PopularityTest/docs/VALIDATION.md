# PopularityTest validation

## Dependency and ownership

Use `SHCDE_API_SHARED_DIR` to select the SDK for every verification/build/install child.
For the installed development SDK set it to the game's `BepInEx/plugins/APIShared_Serp_dev`,
then run `build.bat /working /nopause` (add `/noinstall` for a build only).
`/working /apishared:installed-dev /nopause` is an equivalent explicit convenience selection.
Other selections retain the normal workspace package/provenance check. No dependency is
silently rebuilt or selected instead. DLL name, manifest GUID/version, minimum dependency
and a run-wide DLL SHA-256 are checked; no APIShared DLL is included with this consumer.
The dev SDK must contain the rounded fear API, not merely share its version string.

APIShared owns the automatic display correction and rounded fear calculation. This mod
owns host settings, opt-in activation, localization and its ordinary SE overrides.
Fear accepts -5..+5 in 0.1 steps, including decimal comma input. The exact coefficient is
multiplied by the signed fear level, then rounded to whole report points; midpoint ties
round away from zero. Ordinary source settings retain integer steps and existing bounds.

## Relevant checks

The build runs scoped dependency/source-boundary, runtime JSON/lifecycle guardrails,
asset/locale/CRLF checks, compiled signatures and XAML bindings against the actual SDK,
Extender and original game assembly, plus this mod's preset/override behavior tests.
Source scans are guardrails, not behavioral proof. No exact source text, control adjacency,
private method layout, current setting count or foreign consumer suite is a build contract.

The standalone preset fixture compiles the real settings and maintained API preset sources;
only external engine/UI/logging boundaries are doubles. It tests persistence (including
fractional fear), reset, host/client authority, local preference preservation, mission
locking, disabled no-op, duplicate lifecycle checkpoints, zero/negative overrides,
partial rollback and foreign stack retention. Run the BAT/fresh Windows PowerShell
outside the sandbox: the fixture exercises atomic File.Replace. General API tests stay
in APIShared; the installed API member check separately validates the consumer binary boundary.

APIShared's targeted suites cover rounding/signs/ownership/display and actual installed SE
IL rewrite/generation. SE compatibility follows coefficient/input/output provenance,
resource identity and control flow, without a fixed SE version/hash or site count.
Equivalent layouts are accepted; changed meaning and unsupported flows are rejected.
The native game ABI guard remains separate. APIShared's UpdateToNewDLL.md explains
how to extend supported semantic analysis without weakening its safety boundaries.

## Game acceptance still required

Check Mod Options layout, numeric input, tooltips, disabled controls, presets and reset.
Test new game, editor, save/load and subsequent maps with checked and unchecked sources.
Check `POPULARITY_BOOT`, `POPULARITY_SESSION`/`POPULARITY_END`, `FEAR_POPULARITY_READY`,
`FEAR_POPULARITY_APPLIED` and `SE_POPULARITY_DISPLAY` where applicable. Confirm signed fear
products, ties, totals, and preserved unrelated event contributions. Real host/client,
late-join and saved-game synchronization require actual game logs; CLI tests prove none
of those. The existing negative-religion auxiliary unsigned-counter limitation remains.

## Historical testmod display verification (2026-10-17, before transfer)

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


## 2026-10-18 failure reporting

The optional rounded-fear registration is isolated from required mission registration and independent SE overrides. Owned override writes and release are checked during those operations. Only detected failures emit Warning/Error; normal operation emits no health summaries, per-feature success records or fear samples. No periodic health observer or HUD readback check is added. The next game run should use factor 2 and nonzero signed fear, check popularity/fear reports, end the mission and load a save. Logs expose detected failures; visual effects, multiplayer and unexercised paths still require manual acceptance.

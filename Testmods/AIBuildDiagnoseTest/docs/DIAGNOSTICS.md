# Diagnostic ownership

This test mod owns AI construction observations, copied native snapshots and
explicit disposable-save probes. They are not APIShared services. APIShared still
provides the shared mission lifecycle and general AIV build-step capability.

`src/Diagnostics` separates copied evidence contracts, observation/capture behavior,
native hook installation and the adapter to BugfixesAndQoL. The test mod validates
its own native baseline before installing its two observation hooks.

BugfixesAndQoL owns the existing economy/search interception points. Its internal
`AiBuildObservation` bridge retains one sink until process exit and forwards calls
synchronously on the native caller thread. Without this test mod, the bridge is
inert. It does not install hooks or capture memory. The coarse-grid test can publish
additional records through the same mod-owned bridge.

The sink preserves attempt nesting, player attribution, independent native-hook
failures and restoration of temporary test data. Published hooks are never removed
at plugin cleanup or map changes. These are diagnostic integration contracts, not
a supported extension surface for third-party gameplay features.

## Local tests

Set `SHCDE_GAME_DIR` to the installed game directory. Then run:

```powershell
dotnet test ./tests/Diagnostics.Tests/Diagnostics.Tests.csproj --configuration Release
```

The suite compiles the actual diagnostic and bridge sources. It tests dormancy,
ownership conflicts, attempts, callback failures and restoration. It also runs the
production NativeX64 probes against private copies of both installed prologues:
scheme, displacement, patch form, pointer slot and entry checks. It never patches
the installed image or another game process. Fixtures run serially and clear only
their unpublished managed registrations between tests.

The normal `build.bat /nopause` sets the game path and runs this suite before
building and installing the test mod. Game acceptance remains separate: verify
startup cleanup, diagnostic records and explicit disposable-save probes in-game.

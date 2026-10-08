# Contributing

Open an issue with a reproducible problem or submit a pull request against `main`.
Describe the resulting behavior, affected contracts and tests you ran. Contributions
can add services or reorganize implementation; the current folder layout is a guide,
not a requirement. No AI tools or separate mod workspace are needed.

## Setup

On Windows, install the .NET 10 SDK and Visual Studio or Build Tools with the
.NET Framework 4.8.1 targeting pack. Full integration tests also need a local SHCDE
installation with BepInEx and Script Extender. Never commit these proprietary assemblies.

Set `SHCDE_GAME_DIR` to your installation directory before opening `APIShared.sln` or
running commands. `GameDir` is the equivalent MSBuild property.
`SHCDESE_EXTENDER_DIR` / `ExtenderDir` override the usual extender directory.
The build driver detects MSBuild using Visual Studio Installer; `SHCDE_MSBUILD` is
an optional override. Paths are not tied to the maintainer's machine.

## Tests

Tests use MSTest and appear in Visual Studio Test Explorer. Run the same commands
from PowerShell. The core suite needs no game installation:

```powershell
dotnet test tests/Core.Tests/Core.Tests.csproj
dotnet test tests/Core.Tests/Core.Tests.csproj --filter FullyQualifiedName~JsonTests
```

With the installed dependencies configured:

```powershell
dotnet test tests/APISharedTests/APISharedTests.csproj
dotnet test tests/LobbyModSettingsPresetTests/LobbyModSettingsPresetTests.csproj
& './build.bat' /nopause /noinstall
```

The full driver validates runtime contracts and installed interop, runs all suites,
compiles public consumer examples and prepares the plugin package. Remove `/noinstall`
to install it after closing the game; elevation is only needed for protected directories.
TRX results from the driver are stored in `.local/test-results`.

Core tests compile the actual dependency-free implementations. UnitAccess doubles model
the external SDK boundary, not native layout. Runtime tests use the real installed
assemblies and isolated memory/backend fixtures. Preset tests compile the actual
settings implementation with UI/session doubles; they verify persistence and convergence,
not rendering in the game. Process-wide fixtures run serially and restore mutated state.
Temporary preset and atomic-file directories are cleaned up after each test.

Pull-request CI runs core tests and source/metadata/XAML checks on a GitHub-hosted
Windows runner. It does not claim to run installed-game integration or gameplay tests.
For changes affecting gameplay, report the relevant game checks and any remaining gaps.

## Compatibility and releases

Keep required public contracts and serialized formats compatible unless a breaking
change is explicitly intended and documented. APIShared owns its sources independently;
consumer mods use the public DLL. References in consumer packages use `Private=false`.
Avoid tests that prescribe source wording, filenames or implementation order. Test
observable behavior; retain byte/layout expectations where they express a native contract.

SHCDE destroys startup plugin components. Persistent services need a static root or
long-lived publisher. Published hooks remain installed until process exit; settings
use logical activation. The runtime's dependency-free JSON parser avoids loading extra
serializer assemblies. See [native compatibility](docs/NATIVE_COMPATIBILITY.md) before
changing native behavior and the [API catalog](docs/API_CATALOG.md) for threading rules.

Releases are prepared from a reviewed, committed and pushed standalone checkout:
`release.bat` builds and creates a GitHub draft. Version bumps belong to release preparation,
not test iterations. Community commits are explicitly imported into the maintainer's
mod workspace; this does not impose workspace tools on contributors.

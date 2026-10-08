from pathlib import Path
root=Path.cwd().parent/'SHCDE-APIShared'
def write(p,t):
    p.parent.mkdir(parents=True,exist_ok=True)
    expected=t.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'); p.write_bytes(expected); assert p.read_bytes()==expected
write(root/'README.md','''# APIShared

Shared services for Stronghold Crusader Definitive Edition mods: mission and lobby
events, HUD extensions, building services, selections, and optional preset settings.
Mods share one BepInEx plugin instead of installing competing hooks.

**Plugin GUID:** `APIShared_Serp` · **Assembly:** `APIShared.dll` ·
**Runtime:** .NET Framework 4.8.1 · **License:** MIT

## Install

Install [Script Extender](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender)
and one central APIShared copy under `BepInEx/plugins/APIShared_Serp`.
Download from [releases](https://github.com/SHCDE-APIShared/APIShared/releases).
Until the first release here, downloads remain in the
[previous repository](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases?q=APIShared).
If your mod pack already includes APIShared, use that copy; do not install a duplicate.
Consumer mods must not bundle the APIShared DLL. SerpsModsHost is not required.

## Use

Reference the installed DLL with `Private=false`. Declare the minimum APIShared and
Script Extender versions your mod needs in BepInEx and
[info.json](docs/DEPENDENCIES.md); newer versions are accepted.
Use your own stable plugin GUID:

```csharp
private static readonly APIShared.ModApiClient Api =
    APIShared.ApiShared.ForMod("Example.Author.MyMod");

// Call from Awake on the Unity thread; retain handlers in a persistent runtime.
if (Api.TryGetMissionLifecycle(out var missions, out var diagnostic))
    missions.TryRegisterObserver("main", OnStarted, OnEnded, null, out diagnostic);
```

Managed capabilities can be available before native initialization finishes.
`WhenReady` reports completion, including failure; always check individual `TryGet`
results. Late callbacks run synchronously on the registering thread. Registrations
and hooks persist until process exit because the game destroys startup plugin components.

- [API catalog](docs/API_CATALOG.md): capabilities, contracts and limitations.
- [Mod author guide](docs/THIRD_PARTY_GUIDE.md): mode policies, settings and packaging.
- [Compilable examples](examples/ThirdPartyMod): missions, settings, custom profiles and HUD.
- [Contributing](CONTRIBUTING.md): development setup, tests and pull requests.
- [Architecture](ARCHITECTURE.md): runtime responsibilities and integration boundaries.
''')
write(root/'CONTRIBUTING.md','''# Contributing

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
''')
write(root/'ARCHITECTURE.md','''# Architecture

APIShared is one process-wide BepInEx service library. Assembly identity `APIShared`
and plugin GUID `APIShared_Serp` are stable. Its public entry point binds a caller's
GUID without installing hooks or reserving a capability.

## Responsibilities

| Area | Purpose |
|---|---|
| Core | Initialization, capability diagnostics, ownership and native infrastructure |
| Missions, Lobby, Players | Shared observations and immutable state notifications |
| Presentation, Buildings | HUD/briefing extensions, building repair and gatehouse services |
| GameModes | Context capture and optional caller-defined permission profiles |
| ModSettings, Savegames | Settings UI integration, presets, convergence and persistence |
| Units, Pathfinding, Diagnostics | Queries and advanced integrations |
| UnitCommands | Required internal command, formation and MoatMove runtime |
| SerpsMods | Explicit compatibility profiles for existing Serps consumers |

Directories help navigation; public namespaces define the contracts. Capabilities
use `APIShared`; settings and general profiles use `APIShared.ModSettings` and
`APIShared.GameModes`. Serps profiles are opt-in and never constrain an unrelated mod.
Internal command/formation integration is retained for existing friend-assembly
consumers; it is not an additional public entry point.

## Publication

`APISharedPlugin.Awake` publishes managed services. `CrusaderLibrary.LibraryLoaded`
initializes native services. `ApiSharedRuntime.ProcessInstance`, static registries
and persistent publishers retain services after startup cleanup. Global completion
and individual capability availability are separate; one native failure must not
disable independent managed services.

Readiness callbacks run outside the initialization lock with exception isolation.
There is no implicit thread dispatch. Registration ownership, ordering, replay and
callback contracts are described in the [API catalog](docs/API_CATALOG.md).

## Settings and dependencies

The preset view model integrates source selection, UI commands and persistence.
Per-player state owns lobby convergence. Settings, presets and saved-game data keep
their existing formats. Internal JSON parsing and atomic publication are owned by
APIShared; no source links or automatic synchronization connect them to a mod workspace.

The compiled runtime uses the real installed game and Extender assemblies.
[Native compatibility](docs/NATIVE_COMPATIBILITY.md) describes native ownership and
update checks. Native catalogs and tests express supported contracts, not a universal
promise that an arbitrary game build is supported.

## Development

The solution contains the runtime, two local integration suites, a game-independent
core suite and public consumer examples. Test-only packages never become plugin
dependencies. The standalone build uses no neighboring repositories. Additional
mod integration tests belong in their consumer repositories.
''')
write(root/'docs/NATIVE_COMPATIBILITY.md','''# Native compatibility

The runtime's current reference native SHA-256 is
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The authoritative supported hash, function hashes, bytes, addresses and layouts are
the runtime catalogs. This document explains their purpose; it does not override them.

## Ownership and validation

| Service | Native contract / owner |
|---|---|
| Building repair | Validated native callable functions; no competing detour |
| Gatehouse distance/timing/automation | APIShared owns the coordinated intervals in the shared gatehouse functions |
| AIV build-step observation | APIShared owns the detour at reference RVA `0x51790` |
| HUD and recruitment | Shared managed/native presentation hooks and registration brokers |
| Internal commands and formations | Permanent APIShared command runtime for existing integrations |
| Assassin selection | Script Extender owns its function detour; APIShared uses its audited integration points |

Capability failures remain independent. Hash-bound catalogs, live-byte validation
and the existing unique-pattern resolution policy determine support. A pattern
match is not permission to bypass function, layout, overlap or backend validation.
Game or Extender version numbers alone are not evidence of native compatibility.

## Hook backend contracts

Published hooks remain installed for the process lifetime. Activation changes are
logical gates; inactive callbacks preserve the original behavior. Installation
rollback is limited to a candidate that has not been published.

RedBird inline-hook size is a minimum: actual displacement is instruction-aligned
and can exceed the requested size. Function detours have a separate scheme-dependent
contract. Validate the real backend, complete displaced range, incoming branches,
continuation and ABI before publishing. Context register masks must preserve live
registers; SIMD state and flags need separate verification. Do not assume a context
wrapper preserves the original flags. Keep assembler labels valid and decode emitted
stubs, not only their intended pseudo-code.

The gatehouse centered-distance replacement loads both unit coordinates before
using `cdq`, because `cdq` overwrites EDX and would otherwise destroy the live record
offset. Timing and automation share coordinated ranges; changes must preserve their
non-overlap and the original inactive paths. Existing backend tests execute productive
stubs on isolated buffers, never on another running game's executable pages.

## Updating support

1. Identify the actual installed native image and Extender/backend versions.
2. Audit the complete affected feature flow, including validation, incoming edges,
   displaced instructions, register/flag use and fallback behavior.
3. Compare real managed/interop visibility, field types, offsets and native strides.
   Publicized assemblies are not proof of runtime access.
4. Check overlapping hooks, particularly Script Extender and
   [Fixes](https://gitlab.com/rawra-stronghold-crusader/shcde-fixes).
5. Update catalogs and behavior/byte/backend tests together; retain fail-closed
   behavior for unsupported contracts.
6. Run local integration tests and game acceptance for the affected service.

`tools/Validation/Verify-Interop.ps1` verifies consumed installed layouts;
`tests/APISharedTests` contains native transactions, byte generation and backend
fixtures. UI/session doubles and synthetic memory are not gameplay acceptance.
Relevant gameplay checks include startup cleanup, map transitions, save/load,
host/client synchronization, gatehouse orientations and formations/MoatMove.
''')
print('Replaced historical prose with current author/contributor and native contract documentation.')

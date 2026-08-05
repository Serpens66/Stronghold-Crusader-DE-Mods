# AIV Placement Lobby

This BepInEx adapter implements Chats 11 through 13 of
`MapParser/AIV_PLACEMENT_ROADMAP.md`.
It observes the current skirmish lobby through persistent managed detours and
builds immutable placement requests containing the selected map, AI player ID,
keep-slot assignment, AIV candidate list, initial rotation, host/client role and
`advopt_pre_build` value.

Vanilla does not randomly choose one entry from a selected AIV list.
`FRONT_Multiplayer.StartSkirmishGame` imports every selected candidate in list
order, after which the native placement scan tests candidates/rotations and keeps
the best fit. Requests therefore retain the complete ordered list and expose
`NativeBestFit` as their selection policy; downstream evaluation must never reduce
it to a random candidate before testing.

The shared package-free `AIVParser.Core.AivJsonFileLoader` loads candidate files
without requiring `System.Text.Json` or another assembly in the game process.

The extended AIV/AIC selection list shows green, yellow, red or grey placement
markers with localized tooltips; a missing marker means that the current
generation is still being evaluated. In network games the host cannot become
ready or start while checks are pending. At start, a multi-AIV list is reduced
temporarily to one random candidate among exact best-fit ties. Single-player
keeps Vanilla's native selection unchanged.
Status tooltips use the same resolution and UI-scale factor as the game's
frontend popups. Their content uses a fully opaque dark background, while every
status marker has a contrasting outline so partial-fit yellow remains visible
against the lobby artwork.

For no-prebuild games, the worker normalizes serialized keep starts before
testing AIV elements and then rebuilds earlier AI starts in native player order.
`NoRot` uses Vanilla's map-facing keep direction. In multiplayer, a randomly
chosen exact best-score tie is retained for both that sequential reconstruction
and the actual start, so later checks see the candidate that will really be used.

`advopt_pre_build=1` deliberately produces
`NotEvaluable/PreBuildSequenceUnsupported` until Chats 14-16 add an exact
sequential model. Missing map or AIV files also remain explicit `NotEvaluable`
inputs instead of crashing the lobby.

Every changed lobby snapshot receives a monotonically increasing generation.
Asynchronous results are published from the lobby's main-thread update hook only
while `LobbyRequestGenerationGate` still accepts their generation.

The package-free evaluation service runs map/AIV file access, parsing, projection
and rule evaluation on background workers. It evaluates the complete ordered AIV
candidate list and applies the documented native multi-candidate thresholds and
tie order. Script Extender assets are captured as plain text on the main thread;
background code never touches Unity or Script Extender objects.

Map snapshots and placement results use separate bounded LRU caches. Result keys
contain map and AIV file identity, keep slot, retained/rebuilt start state,
initial rotation, analyzer version and `advopt_pre_build`. Concurrent identical
requests share one computation.
File size or UTC modification-time changes create a new identity, so cached
success and `NotEvaluable` results are both invalidated. Logs report cache state
and separate map-parse, snapshot, AIV-parse, projection and rule timings.

The test executable normally runs synthetic repository tests. A read-only local
production-worker check can additionally be run as:

    dotnet run --project AIVPlacementLobby.Tests -c Release -- \
      --integration "<map path>" "<aivjson path>"

`build.bat` recreates the complete local package on every run instead of relying
on files from an earlier build. It copies the canonical `info.json`, all freshly
the built assemblies and all official Vanilla AIVJSON files currently supplied
by the editor, validates the package structure without assuming a fixed lord or
AIV count, stages the game installation and compares every installed file
by SHA-256 through `System.Security.Cryptography.SHA256`, without depending on
PowerShell module auto-loading. Deleting the installed
`AIVPlacementLobby_Serp` directory therefore
requires no manual recovery; running `build.bat` restores the complete mod.

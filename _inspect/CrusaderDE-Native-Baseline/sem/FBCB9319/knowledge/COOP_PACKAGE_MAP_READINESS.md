# Packaged Coop maps and Ready admission [Scope: Vanilla + mod integration]

Observed 2026-10-26 against native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
and installed Assembly-CSharp SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
This is a confirmed-static managed control-flow contract; the reproduced run did
not record the private map-valid flag. No native RVA or patch is required.

## Closed relevant path

- Coop page XAML sends `Ready` through `MainViewModel.MultiplayerMenuCommandFunction`
  to `FRONT_Multiplayer.ButtonClicked`. `ReadyLock` is a distinct command.
- `CoopMissionChanged` obtains the selected mission's `header` from the Coop table
  and assigns `selectedMPHeader`. Host `UpdateHostInfo` publishes that header's
  `fileName` and its CRC cast to `int` via `Platform_Multiplayer.UpdateHostLobbyInfo`.
- `MapFileManager.GetFileInfoFromFileName` reads its second argument as the actual
  local file path. It returns a header; it does not register that header in an MP
  catalog. Its returned `fileName` is the basename without extension.
- The client's `FRONT_Multiplayer.Update` checks `MPMapChecked` and `MPLastMapName`.
  When necessary, it calls `GetHeaderFromFileNameMP(name, parsedLobbyCrc)` after
  waiting for an active map retrieval. That overload searches only BuiltInMP,
  WorkshopMP and UserMP, using case-insensitive names and the existing CRC comparison.
- Missing or nonmatching headers leave `MPMapValid=false`, clear local readiness
  unless locked, and expose Vanilla's map-retrieval UI. A matching header updates
  `selectedMPHeader` and the briefing/radar and sets `MPMapValid=true`.
- `ButtonClicked("Ready")` changes readiness only when `MPMapValid || isHost`;
  otherwise it publishes false without an explanatory error. Returning from the
  command is therefore not evidence of acceptance. Unready and unlock are separate
  transitions and must remain possible.
- Header CRC is a public `uint`; the lobby query uses `int`. Preserve the game's
  comparison and transport semantics. A matching CRC alone does not establish
  package integrity: the content provider must independently validate its package.

## Package consequence and correction boundary [mod integration]

The inspected Suffer of the antler package contains 40 bundled map references,
each under `Assets/<mission>/map.map`. Direct package loading can succeed while
the subsequent catalog lookup cannot find that local map. Different missions
also share the Vanilla basename `map`; name-only map-check caching is insufficient
after a package mission changes.

APIShared's exact-name/CRC lookup broker allows a consumer to supply its current
verified package header without changing global catalogs. ExtendedData scopes that
offer to the active Steam lobby, package fingerprint and selected mission, and
invalidates the cached check when this identity changes. Vanilla remains the writer
of successful map validity. Other callers and inactive providers retain Vanilla.

## Chat observation boundary

Steam lobby sends and the Extender receive event do not prove visible chat text.
`FRONT_Multiplayer.receivedLobbyChat` skips ordinary messages when `MPChatMuted`,
truncates individual text to 300 characters, and appends accepted messages to the
shared lobby history. Its `systemMessage` argument bypasses that mute check.
APIShared's system-message classification retains this original renderer exactly
once; ExtendedData splits its own warnings into numbered messages of at most 280
characters and never treats chat text as network authority.

Source verification: the managed files selected through CURRENT.json and the
installed assembly metadata. Runtime acceptance still requires a real host/client
run, including a bundled mission change and ordinary installed-map comparison.

## AI roster and the advertised count

Checked 2026-11-07 against the native and managed hashes above. In the managed
`Platform_Multiplayer.CreateLobby` path, the host constructs `MPLobby` without
setting `AIPlayers`. `CreateLobbyResult` publishes an initial `aiplayers` value
of `0` to Steam. `UpdateHostLobbyInfo` publishes the current value from
`MPLobby.CountAIPlayers()` without writing it back to the host's `AIPlayers`
field. The non-host branch in `RefreshLobbyList` reads the advertised value
into `AIPlayers`. The field can therefore differ on host and client even with
the same actual roster.

`CountAIPlayers()` returns the integer number of entries in `MPLobby.members`
whose IDs are below the game's AI boundary of 1000. The host publishes its
member order and slot mapping. The client applies that order using
`setHostMemberOrder`, adding AI entries found in the host order and removing
those no longer present. The live count therefore converges as the roster is
applied. `ButtonClicked("Play")` publishes the host setup before calling
`HostStartGame`, which publishes `GO!` with the AIV checksum. The later start
path consumes the lobby setup. This finding concerns managed lobby data before
native initialization and requires no native offset or patch.

For APIShared's new-map readiness comparison, use the live count on both peers.
Keep a real roster, AIV or settings mismatch as a reason to withhold agreement.
The static finding explains an independent false mismatch across peers. The
recorded Crater Lake host-side drift had no per-field snapshot and does not
identify the AI count as its trigger.

## Inactive Coop Trail fields in standard lobbies

Checked 2026-11-07 against the same installed native and managed hashes. The
standard multiplayer frontend calls `CreateLobby` with the default
`_coopTrailGame = -1`. The host stores `coopTrailGame = false` and
`coopTrailID = -1`. `CreateLobbyResult` publishes Steam metadata `cooptrail=-1`.

The client constructs a lobby with the managed default `coopTrailID = 0`.
On join and on subsequent non-host refreshes, `coopTrailGameSetup("-1")`
sets `coopTrailGame = false` and clears progress, then returns without
assigning the ID, selected mission or order flag. Only the active trail path
decodes those fields. An inactive trail ID can thus differ for the same
lobby indefinitely. The later standard game start passes no active trail
selection to the native initializer.

### APIShared integration

The installed map-readiness protocol 3 hashes the inactive ID on both sides.
The default standard-lobby states alone therefore produce different hashes,
so the client waits before acknowledging and the host cannot publish GO.
On Crater Lake at 22:52, the client logged the received host digest and a
different local digest with `trail=False/0/0/False` while still awaiting
readiness. The complete host-side values were not recorded, so this
observation does not establish whether any other input also differed.

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

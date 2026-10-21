# Coop Trail Customize presentation

Static audit: 2026-10-21. Native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
installed `Assembly-CSharp.dll` SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Both were compared with CURRENT.json / DATABASE_INFO.json before use.

## Managed state and consumers

- `FRONT_Multiplayer.CoopMissionChanged(int,int,bool)` receives zero-based Trail
  and one-based mission selection. It creates the roster and setup, updates the
  Coop page radar and host data, but does not initialize `MPLobbyMember.teamShield`.
- `ReSortTeamInfo()` sorts one-based player IDs and can normalize team IDs.
  `CreateTeamShields()` returns immediately while `coopGame` is true.
- `PopulateTeamsPanel()` calls `ReSortTeamInfo()`, resets each member's
  `teamShield` to -1, then assigns team IDs to grouped members. It populates the
  hidden panel's faces/names/slices without changing `Show_SkirmishTeamsPanel`.
- `Update()` uses `teamShield` for lobby-row background colors. The radar reads
  the same field through `getKeepTeamShield(int zeroBasedKeepIndex)`; regenerate
  radar shields after generating team presentation. Opening the team panel runs
  `PopulateTeamsPanel()`; closing it updates the radar. A presentation refresh
  must not replay mission selection, which would replace customized settings.
- `ShowSetupScreen()` changes visibility/readiness presentation, but does not
  project the full setup into skirmish captions, advanced-option indicator or map
  metadata. `SetupSkirmishModeSettings()` and `PopulateMapDetailsPanel(FileHeader)`
  provide those projections; the latter overwrites `StandaloneMissionText`.

## Input callback boundary

`SetupSkirmishModeSettings()` sets all four Coop game-speed sliders.
Each page subscribes `ValueChanged` to
`FRONT_Multiplayer.MP_Settings_GameSpeed_Slider_ValueChanged`. That handler returns
when `!panelLoaded || !panelActive`. Otherwise it modifies `MPTEMPsetupData`, or
the active Coop `MPsetupData` based on the currently visible Trail page; when none
is visible its initial speed is zero. Vanilla `doOpen()` leaves `panelActive`
false while setting up controls. A late presentation refresh must likewise
suppress these callbacks and restore the previous gate in `finally`.

The team and UI state is managed. Native `DLL_GetTrailMissionInfo` (RVA 0x84600,
confirmed export; built-in non-Coop customization comparison) and
`DLL_ApplyMultiplayerSetupData` (RVA 0x81050, confirmed export; setup submission)
do not initialize these managed presentation fields. Their unnamed helpers at
0x286C0 and 0x26370 retain candidate names in the semantic database.

Evidence: hash-matched managed decompilation of FRONT_Multiplayer, all four
FRONT_CoopTrail pages, MainViewModel, Platform_Multiplayer and EngineInterface;
semantic function queries for the exports and their callees. The executable
member check in `ExtendedData/Test-RuntimePreflight.ps1` verifies the selected
installed assembly's reflection/direct-access signatures. Adapter tests execute
production source against boundary doubles; they do not prove Noesis rendering
or live host/client synchronization. In-game visual acceptance remains pending.

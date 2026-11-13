# Coop Trail Customize presentation [Scope: Vanilla + mod integration]

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

## Custom partner keep assignment, 2026-10-25 [mod integration]

Reverified the installed native and managed SHA-256 values above. This is a
managed lobby-state defect; no new native hook or structure offset is needed.

`CoopMissionChanged` creates the local AI partner in player slot 2, then writes
the complete mission keep order, accounting for `coopOrderSwapped`. ExtendedData
supplies the same mission table contract through `MissionProjection.KeepOrder`.
First selection and Coop continuation reach this initialization from
`FRONT_Multiplayer.Update_Coop`; mission selection and SwapCoop also call it.

`updateSteamIDMappings` removes absent identities through
`remove_player_from_keep_locations`, then adds new identities through
`add_player_to_keep_locations`. The latter uses `System.Random.Next` among free
keeps when the player lacks an assignment. BugfixesAndQoL's former partner
replacement removed Vanilla's slot-2 identity and inserted a temporary Custom
Lord identity before finalizing it. The mapping update therefore discarded the
correct mission keep and could choose another free keep. Preserving the member's
list index, colour and team did not preserve its map position.

The corrected path configures Vanilla's existing member and publishes a freshly
prepared MPAIVInfo, retaining the member identity, all eight mappings and keep
assignments. Final launch validation checks the selected name against both the
member and AIV selection. Saved/customized setup positions are not reconstructed
from mission defaults at launch. Normal creation of new random opponents keeps
its existing placement semantics.

Native data-flow cross-check: confirmed export `DLL_ApplyMultiplayerSetupData`
at RVA `0x81050` calls generic candidate-named helper `0x26370`, copying transfer
ints 10..17 into eight signed-byte keep assignments. Candidate-named lookup
`0x89870` returns the one-based keep whose assignment equals playerId-1, or zero.
The start path at `0x94350` uses that lookup to obtain keep coordinates for both
fixed and automatic AIV selection and the no-fit fallback. These are static
call/data-flow findings for the recorded hash, not runtime placement evidence.
`UpdateHostInfo(bool)` submits setup data immediately even with delayed=true;
only lobby transport is delayed. Retaining the identity avoids transient wrong
submissions as well as the final wrong assignment.

The actual managed assembly exposes the required lobby/member/AIV data publicly;
the existing private `UpdateHostInfo(System.Boolean)` method is accessed through
a signature-checked reflection adapter. Automated tests execute the production
configuration and launch-validation helpers against actual game data types.
Fresh-game visual and spawn acceptance remains a separate in-game check.

## Advanced options in local and network Coop [Vanilla facts]

Static audit on 2026-11-07 against the native and managed hashes above.
The Trail Maker opens the skirmish setup. Its advanced checkbox writes
`advanced_skirmish_options`. The host's regular multiplayer setup dialog
writes `advanced_options`.

Local Coop starts through `FRONT_Multiplayer.StartSkirmishGame`. Network Coop
starts through `Platform_Multiplayer.StartGame`. The confirmed preinitializer
`DLL_PreInitMap_Multiplayer` (RVA `0x86270`) sets the native mode at
RVA `0x8574B90` to 99 for skirmish and 1 for network starts. Both paths
submit their setup through the confirmed export `DLL_ApplyMultiplayerSetupData`
(RVA `0x81050`). Its helper at `0x26370` reads the 32-bit flags from
transfer indices `0x16` and `0x17` and Improved Spearmen from `0x1B`.
Either advanced flag permits the individual rules to be copied. With both
flags clear, the helper clears Improved Spearmen. The fresh skirmish map
initializer at `0x94350` preserves this choice through its reset.

In the ordinary Spearman movement branch at `0x143BD9` of function
`0x143400`, Improved Spearmen at RVA `0x87EE304` permits running when the
selected advanced flag is also set. Mode 99 reads the skirmish flag at
`0x87EE2F4`. Other modes read the multiplayer flag at `0x87EE2F0`.
Unit-state guards also affect the decision. The projectile hit function
`0x192750` uses the same flag selection for its Spearman damage adjustment.
These instruction checks explain the mode difference for unequal flags.
The player log did not record the values of those flags.

## Frontend selection visibility, 2026-11-13 [Vanilla facts]

Rechecked against the native and managed SHA-256 values recorded above and actual
managed module MVID `29ade4ef-f587-422f-a1eb-f81dff066e0a`. These are managed/XAML
contracts; no new native address or hook is involved.

`MainMenu_CombatButton` sends `ButtonMainMenuCommand` with parameter `Skirmish`.
`MainViewModel.ButtonMainMenuFunction` forwards it to `FrontendMenus.ButtonClicked`.
That branch clears the old panels, sets `Show_FrontMenus=true` and
`Show_Frontend_Skirmish=true`, and begins `MainMenu_ShowSkirmish` (its animation
content is commented out in this hash). In `FrontendMenus.xaml`, the ancestor
Grid at line 2632 binds its Visibility to `Show_Frontend_Skirmish`. It contains
`CoopMenu_Button`, whose command parameter is `Coops` at line 2697. The `Coops`
branch clears the Skirmish panel and sets `Show_Frontend_Coop=true`, displaying
the next, separate Coop-selection screen. `Coop` then selects frontend page 21
and opens the ordinary Coop setup, provided `SteamUser.BLoggedOn()` succeeds.
Controls placed beside `CoopMenu_Button` belong to the Skirmish screen, even
when their names or purpose refer to Coop. Looking up a named control does not
prove that its ancestor screen is visible.

## Local Coop restart roster and identity, 2026-11-13 [Vanilla facts]

`StartSkirmishGame` captures `RestartSkirmishMapInfo.importMembers` and
`importAIVs` before starting the simulation. Real Steam-lobby human members have
neither `SkirmishMember` nor `SkirmishHumanMember` set. `importMembers` tests only
`SkirmishHumanMember`: flagged humans produce the sentinel -1, while other
members produce `(int)member.id.m_SteamID`. For Steam IDs with a nonnegative low
Int32, the local human therefore has nonnegative serialized Lord data.
`MPLobby(RestartSkirmishMapInfo)` reconstructs that entry as a Skirmish member
without the human flag. The constructor does not write `coopTrailGame`,
`coopTrailID` or `coopSelectedMission`; they retain false/0/0. This was checked
against actual assembly IL, not inferred from a publicized reference.

`RestartSkirmishGame` restores that roster, AIVs, setup and header, updates the
mapping, removes dummy slots and updates the mapping again. It does not restore
those three lobby fields. Static `FRONT_Multiplayer.coopGame` retains the actual
Coop context. The subsequent `StartSkirmishGame` explicitly treats
`member.SkirmishHumanMember || (coopGame && mappedPlayerId == 1)` as human and
calls `RegisterMPPlayer`; the other members take the AI-registration/AIV path.
Code classifying local Coop AI must preserve this distinction without changing
member flags or identities. Extra simulation ticks cannot repair or alter this
synchronous snapshot/reconstruction contract.

The actual start derives native trail 1..4 from `FrontendMenus.CurrentSelectedTrail`
21..24 and passes its already zero-based `selectedCoopMissionID` unchanged.
`CurrentSelectedTrail` is a public static Int32; `selectedCoopMissionID` is a
private instance Int32. By contrast, lobby `coopSelectedMission` is one-based:
`CoopMissionChanged` forwards its UI mission unchanged to `SetCoopTrailProgress`.
Those lobby fields are not the source of restart launch identity. Normal local
Coop starts do not guarantee a briefing: only `!coopGame` schedules the deferred
`ButtonGotoBriefing`; the Coop branch clears blackout directly.

Static evidence: hash-matched FrontendMenus, MainViewModel, HUD_Main,
HUD_IngameMenu, FRONT_Multiplayer, Platform_Multiplayer and EngineInterface,
plus installed assembly visibility/constructor IL checks. Current task evidence:
`.work/a/trail-migration/001/start-audit.json` and `restart-audit.json`; the first
start-route observation was corrected by the ancestor audit above. Runtime
rechecks using ordinary frontend/HUD commands are archived at
`.work/t/trail-game/005/2024c96d14ea/LogOutput.log` (2026-11-13 01:20:20..26)
and `.work/t/trail-game/006/d39959e6ea58/LogOutput.log` (01:21:41..46).
Both record a visible/enabled package control on the Skirmish screen, a local
Coop session, a new linked restart session and PASS after fresh PlayState/ticks.
These runs validate the managed local route; they do not establish multiplayer
acceptance or new native layout facts.

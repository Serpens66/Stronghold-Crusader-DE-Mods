# Lobby save loading and Skirmish load-button visibility

Static audit date: 2026-10-14. Native SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2;
managed SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Both match the installed canonical binaries. Script Extender commit
5908e1f12437deb7ef5f5b1dc2d64e301178fba3, tree
c238441ea12db35ee36c5c5668d3c8fce0897295 matches current baseline provenance.

## Frontend gates and visibility writers

The installed real FRONT_Multiplayer.Update assigns RefLoadButton.IsEnabled at
managed IL 0x17D8. Its expression is (flag5 && !flag6) | skirmishGame. In normal
multiplayer, flag5 requires the lobby member-count and capacity gates, local
readiness and every human member's ready flag. Unlike the play button, it does not
require enough opposing teams. flag6 records that the lobby has AI players.
The SE FRONT_Multiplayer_Update_ILHook changes only the rejection of an all-AI
opposition in the shared start eligibility calculation; it leaves flag6 intact.
Thus a host with AI can start alone but the load button remains disabled. With no
AI, one human also fails Vanilla's minimum member-count gate.

Coop trails use coopGame; ordinary custom Coop uses the independent public static
customCoopGame flag, set for MPGameType 2/3 and gameTypeCoop == "1". The solo
extension excludes both flags and requires current lobby gameTypeCoop == "0".
Unknown or transitional lobby category data cannot obtain the new permission.

## Additional frontend-mode audit

Crosschecked against APIShared MissionModePolicy/MISSION_LIFECYCLE, Vanilla callers
in FrontendMenus, FRONT_EditorSetup, MainViewModel and GameData, plus workspace
TrailCustomizationFeature, MultiplayerLobbyReturnFeature, MapOriginSortHook,
MultiplayerAivSyncRuntime and SpectatorSavePolicy. Native dispatch and parameter
contracts remain those of the confirmed exports above; no new native branch is
replaced. The mode audit expanded the initial frontend audit to include custom
Coop independently of coopGame.

| Context | Audited entry/evidence | New behavior |
| --- | --- | --- |
| Ordinary MP host | Open(false), real current Steam lobby, gameTypeCoop "0", network human member | Solo load permitted only when ready |
| MP client or lobby list | isHost false or Show_MPGameCreation false | Original result |
| Local Skirmish | Open(true), human marked SkirmishMember/SkirmishHumanMember | Visibility repair only |
| Local AI spectator | EnableSpectatorMode removes local human; skirmishGame true | Visibility repair only; existing spectator-save policy retained |
| Coop trails, host invite, client, resume | Open(false, coopSetup:true), coopTrailGame and Coop panels | Original result |
| Custom Coop | MPGameType 2/3, customCoopGame or lobby gameTypeCoop "1" | Original result |
| Local/customized Coop | singlePlayerCoop; workspace sets skirmishGame and coopGame true | Original result |
| Customized Vanilla/Sands/Extreme or custom Trails | Open(true) or StartCustomTrailMission; customizedTrail/extremeTrailCustomised | No MP permission; local Skirmish visibility repair only |
| Trail-maker/editor test | FRONT_EditorSetup opens Skirmish with trailMaker:true | No MP permission; local Skirmish visibility repair only |
| MP post-game host handoff | Workspace doOpen(false, fromNew:false), reattaches real lobby | Same validated solo-host eligibility after fresh readiness |
| Campaign, standalone, tutorial, editor map | Separate managed start/load roots | No new load path |

The extension also rejects a zero Steam lobby ID, coopTrailGame, singlePlayerCoop,
trailMakerMode, customizedTrail and extremeTrailCustomised, plus inconsistent
member flags. Local Skirmish humans can possess a valid Steam ID but are not
network participants. AI members require SkirmishMember without SkirmishHumanMember;
an unresolved default member is not silently accepted as AI. Current lobby identity
must match Platform_Multiplayer.activeLobby. Old gameMembers, editor flags or
mission snapshots are intentionally not used as lobby authority.

ShowLobbyScreen hides RefLoadButton. ShowSetupScreen restores it for a multiplayer
host but omits that assignment for skirmishGame. doOpen and ShowSetupScreen use
the same MainViewModel.FRONTMultiplayer instance across mode changes. No other
RefLoadButton.Visibility writer restores the local Skirmish branch. The XAML
LoadButton initially has default Visible visibility and no visibility binding.

FatControler.Update invokes FRONTMultiplayer.Update while Show_MultiplayerSetup is
active. This is the persistent Unity-main-thread frontend path; no BepInEx plugin
Update callback is needed. ShowSetupScreen is also called by existing consumer
map-selection/customization paths; their Hook trampolines must remain chained.

## Selection, restore and absent humans

ButtonClicked("Load") retains the Multiplayer requester, sends save responses and
passes CountHumanPlayers()-1 as the expected remote response count. GetMPSaves
filters save category, not the original human count. HUD_LoadSaveRequester gates
rows on retrieveCRCChecks matching that response count; zero remote humans needs
zero responses. This is not evidence of a comprehensive save-content validation.

HostLoadGame clears the nine-slot remapping array at indices 1..8 to -1 and writes
the filename to Steam lobby data. FRONT_Multiplayer.Update resolves that filename
and invokes StartSave. Missing filenames retain Vanilla's error path. The host
waits for acknowledgement only from actual remote human gameMembers; no remote
humans therefore require no acknowledgements.

StartSave registers current human participants in sequential 1-based slots, loads
the MP save through DLL_LoadMapToPlay (confirmed export RVA 0x84AC0) with the save
byte set, and records the saved local player ID returned by that load. Clients
report their old/new slot mapping in packet 9; the host distributes the completed
mapping in packet 4. Saved AI descriptors are restored separately. Native
preinitialization is DLL_PreInitMap_Multiplayer (confirmed export RVA 0x86270);
human registration is DLL_RegisterMultiplayerUser (confirmed export RVA 0x864C0).

The enclosing loader routes the save to RVA 0x94350 (candidate internal name,
control flow inspected directly). Its save branch reads the archive, retains
restored world state, skips new-game castle/unit/resource initialization and
returns 0xFFFFFFFF; invalid archive input returns zero. The enclosing loader
populates LoadMapReturnData, including saved local player ID and AI descriptors.
DLL_StartMultiplayerGame (confirmed export RVA 0x87880) preserves the saved random
seed when fromSave is set; RVA 0x8600 rebuilds the native random table from it.

DLL_RemapPlayers (confirmed export RVA 0x86580) receives Int32[9] and the saved
1-based local player ID. It rebuilds the human-presence map. Every slot absent
from that map with no saved AI registration enters RVA 0xCD190. This internal
function has a candidate name, but its inspected effects are concrete: owned
buildings are marked for removal through 0xC3F10/0xC4290; owned live units change
AliveState 2 to 3 through 0xA1280; owned wall/tile records and ownership masks are
cleared through 0x71E90/0x71E30/0x62EB0/0x516D0. There is no conversion to AI in
this path. These are deferred-removal writes, not an observed immediate game-frame
result. DLL_StartMultiplayerGameSynced (confirmed export RVA 0x878B0) and the
managed simulation-thread start complete the normal host/client start sequence.

Consequently Vanilla does not reject a three-human save solely because only two
humans joined the lobby. The missing human's slot follows the removal path. The
same existing mechanism applies to a solo host; no new save format or remapping
algorithm is required.

## Consumer implementation and acceptance limits

BugfixesAndQoL owns separate default-on PresetLocal options
EnableSoloMultiplayerSaveLoading and FixSkirmishLoadButtonVisibility. A permanent
IL transform changes only the final load Boolean for a validated solo host; a
separate permanent ShowSetupScreen hook restores local Skirmish visibility after
the original chained call. Private MPLocalReady and RefLoadButton are accessed
through typed reflection validated against the real assembly. APIShared and SE
are unchanged. The canonical Fixes source has no overlapping frontend/load hook
or LoadButton XAML modification in the reviewed source.

Tests compile the complete original and SE-start-patched FRONT_Multiplayer.Update
with the installed Unity Mono runtime and check missing/changed/unsafe insertion
sites fail closed. Policy and static settings/locale/tooltip checks do not execute
Noesis or load a save. Actual solo save reload, three-human saves with fewer
participants, AI preservation and subsequent simulation remain game acceptance
tests; do not infer them from an installation or test PASS marker.

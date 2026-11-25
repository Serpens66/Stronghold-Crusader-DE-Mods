# Multiplayer loading acceptance

The integrated **Load a save during multiplayer** preference is enabled by default.
Find it under **QoL / Game setup, multiplayer and spectators**. Only the host
changes it; Map, Trail and saved-game rules cannot capture or restore it. Its
value is adopted for the next mission, independently of the gameplay master switch.

Build BugfixesAndQoL with its normal build driver. Its offline checks include the
production-linked return protocol, actual-game stop barrier and runtime fixtures,
localization and settings metadata. These are not game acceptance tests.

Before installing, close both games. Remove the old
`BepInEx/plugins/MultiplayerRestartTest_Serp` installation on both PCs when replacing
it with the integrated mainmod build. A discovered old testmod disables only the
new integrated feature to prevent duplicate hooks. APIShared remains unchanged.

## Manual two-PC acceptance

1. Use matching mainmod builds and compatible dependencies on both PCs. Start a
   real multiplayer game with two humans. On the host choose the original ESC
   **Load** entry. The client cannot initiate the operation; there is no extra mod
   menu entry. Cancel the first confirmation once and verify the match continues.
2. Confirm the exit. Both players must join the same private lobby automatically,
   without entering a code. Each becomes Ready once through the normal path.
   Manually unready must never be overridden by another automatic attempt.
3. The existing MP save dialog must open directly with both ordinary and Coop
   saves, without a category popup. Load a matching ordinary save, then repeat
   with a Custom-Coop save in the same process. Check player assignments, rendered
   map and continued simulation on both PCs.
4. Repeat from a Custom-Coop match. Also load a Coop save through the normal
   Customize-Coop lobby path. Ordinary lobby Load commands retain their existing
   category behavior; only the integrated action's own requester is combined.
5. Cancel the save requester after the coordinated return. Everyone stays in the
   lobby; the previous match is not resumed. Manually opening and cancelling a
   requester before the automatic opening must not produce a delayed duplicate.
6. Provide a missing or different local save on one peer. Vanilla's normal file
   response checks must still exclude it. No save is copied or substituted.
7. Before another mission disable the host preference. Verify the native ESC
   behavior and ordinary lobby starts/loads remain unchanged. Re-enable between
   missions and repeat. Save/Trail source changes must not overwrite this preference.
8. Exercise pre-commit disconnect and lobby-creation/join failure separately.
   Before commitment the old match may remain; after commitment there is no
   simulation rollback. Recovery retains the existing controlled main-menu path.

Keep both logs. `MP_SAVE_LOAD` marks the return stages; `LOAD_MENU_READY` and
`VANILLA_SAVE_DIALOG category=all-multiplayer` alone do not prove a successful load.
Confirm the actual native save path, successful lifecycle completion and later
simulation activity on both peers. Include an ordinary manual leave/new-lobby/load
comparison when investigating a repeat-load failure rather than adding new loader
rules. The separate KI-storage warning and optional missing-mod policy are outside
this feature.

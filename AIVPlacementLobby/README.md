# AIV Placement Lobby

This BepInEx adapter implements Chat 11 of `MapParser/AIV_PLACEMENT_ROADMAP.md`.
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

No visible UI is changed. `advopt_pre_build=1` deliberately produces
`NotEvaluable/PreBuildSequenceUnsupported` until Chats 14-16 add an exact
sequential model. Missing map or AIV files also remain explicit `NotEvaluable`
inputs instead of crashing the lobby.

Every changed lobby snapshot receives a monotonically increasing generation.
`LobbyRequestGenerationGate` is the hand-off point for Chat 12: asynchronous
results may only be published while their generation is still current.

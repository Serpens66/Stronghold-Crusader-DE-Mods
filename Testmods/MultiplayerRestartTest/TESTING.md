# Multiplayer Save Load Test

This standalone testmod enables the original **Load** button for the host during a running multiplayer match. After the host confirms that the match will end, the current human participants move automatically to a private multiplayer lobby. The host then chooses a save through the normal multiplayer save dialog. Nobody needs to find the lobby or enter its code manually.

The confirmation is consequential: once the coordinated exit is committed, the old match is ended. Cancelling the subsequent save dialog leaves the group in the load lobby; it does not resume the old match.

## Build and installation

1. Build with `build.bat /nopause`. The build checks the working sources against the selected installed dependencies and creates `.local/MultiplayerRestartTest.zip` plus `.local/build.json`. It does not install the mod or start the game.
2. Close the game on every participating computer. Install the same resulting ZIP on each one, with the dependency minimums declared in `info.json` and mutually compatible multiplayer mods.
3. The existing package identity is deliberately retained: `BepInEx/plugins/MultiplayerRestartTest_Serp/MultiplayerRestartTest.dll`. This replaces the previous experiment in that location; do not install a second copy under another directory. The displayed name is **Multiplayer Save Load Test**.
4. Fully start the game again on every computer. The process-lived simulation checkpoint must be installed before the simulation thread starts.

## Use

1. Start or normally load a multiplayer match. For genuine multiplayer acceptance, use at least two human participants.
2. Open ESC on the host. Use the original **Load** button, whose normal label follows the game's language. Clients cannot initiate the coordinated return. No additional Load entry is added to the mod-actions page.
3. Confirm **End match?**. Dismissing this confirmation leaves the current match in place. After confirmation, wait for the group's automatic return to the private lobby.
4. At **Coop saves?**, choose **Yes** for cooperative saves or **No** for ordinary multiplayer saves. This is a category choice, not a way to undo the completed exit.
5. Choose a save in the normal dialog. Its existing eligibility checks and normal APIShared contributions remain responsible for which saves can be selected and started.
6. Cancelling this dialog leaves everyone in the load lobby. The host can use its normal Load command again or leave the lobby.
7. After a save successfully starts and its multiplayer simulation is running, the host can use the in-game Load action again.

After the shared lobby handoff, every participant automatically requests **Ready** through the normal local button path. Already-ready players are left unchanged. Client map validation and all installed launch guards remain in effect. Rejected attempts back off and stop after five attempts; waiting for map validation is bounded to 90 seconds. Manual Ready remains available. This is an initial one-shot action: an actual manual Ready-to-Unready change cancels it even during its initial delay or retries. Later Vanilla settings/map resets are respected, and no match is started automatically.

The host's category question opens after its initial Auto-Ready action finishes. Auto-Ready waits while native confirmation or save dialogs are visible, so a guard cannot replace the category question's callbacks. If the host opens the normal multiplayer save requester manually during this wait, its Opened event cancels the pending category question, even when the requester is closed before the next update. The testmod does not open another requester afterward.

The mod coordinates the transition of the currently present humans. It does not reconstruct an original mission, keep the initial Steam lobby alive for the whole match, create an automatic beginning save, or choose a save for the host.

## Save eligibility and categories

The native multiplayer picker checks locally available saves through its existing name/checksum/time exchange. A selected save normally needs a matching local multiplayer-save copy on every participant's computer. This flow does not copy a missing save from the host to another computer.

The cooperative and ordinary picker categories retain their normal filtering. The testmod adds no Lord, package, original-map, team, slot or modsettings criteria of its own. Existing checks belonging to Vanilla, the Script Extender, APIShared and other installed mods still apply. A save that the ordinary multiplayer workflow refuses must not become accepted through this menu action.

An existing save is loaded as the state it contains. Starting a mission from its original beginning requires a save made at that beginning; this mod does not derive an initial map from a later save. Saving, stored modsettings, saved Lord data and eventual next-mission behavior remain owned by their normal components.

## Manual acceptance

First complete case A in the repeated-load comparison below: ordinary manual lobby loading with this testmod absent from both processes. Only after that baseline succeeds, compare cases B and C. Do not compensate for a failing manual baseline with additional testmod loading logic.

- With two humans and a running ordinary multiplayer match, use the host's original Load button. Confirm that both players reach the same private lobby automatically, with the same host and no manual join or invitation.
- Confirm that both players become Ready after joining without extra clicks. Recheck an already-ready player, a temporarily missing client map, an initially blocking mod guard and a participant leaving before the attempt. Manually unready before the first automatic attempt, during a rejected retry, and after initial success: the testmod must not force Ready again. Also change a setting after initial success. A rejected guard must remain effective and retries must stop.
- Open and immediately cancel the normal multiplayer save requester while the host category question is pending. No delayed duplicate question or requester may appear.
- Load an ordinary multiplayer save already available on both computers. Check the rendered map, the intended local player slots, teams and continuing simulation ticks on both peers. A loader-return log or a living process alone is insufficient.
- Repeat with the cooperative category and a cooperative save. Exercise the other category separately; a category question answered No must open ordinary multiplayer saves, not cancel the transition.
- Start the transition from a Custom-Coop match as well as from an ordinary match. The replacement lobby must be generic, without inheriting the previous Custom-Coop two-player mode. Load a matching cooperative save through Yes, then repeat and cancel the requester; select an ordinary multiplayer map and start it from that same lobby. Verify the lobby mode and continuing simulation on both peers. This checks the origin separately from the chosen save category.
- Where available, include a saved game with nonconsecutive human slots, such as 1 and 8. Let the normal save-load remapping finish, then check both players' actual controlled Lords before gameplay.
- Include saved Lord data and mission modsettings through the ordinary installed mod set. The first Launch click should enter the existing coordinated wait and start once peers acknowledge; do not require a second click. Cancel or replace a pending selection and then load another save to check that the old Lord publication is retired. Compare the result with a normal lobby load of the same save; this mod must not introduce a second settings or source-restoration path.
- Remove a matching save copy from one test computer, or provide a different revision under the same name. Observe the native picker/readiness result and compare it with the ordinary multiplayer load workflow. Do not weaken its checks to make the test pass.
- Cancel **End match?** and confirm that the current match continues. Separately confirm the exit, then cancel the native save dialog: everyone must remain in the lobby, and the ended match must not resume.
- After one save load, repeat the in-game transition and load again. Then leave the match and create or load another ordinary game, checking that the old transition has not retained control of the new session.
- Verify that a client cannot initiate the action and that a missing or incompatible testmod on a participant prevents the coordinated host action. Preserve normal singleplayer/editor menu behavior.
- With other ESC actions installed, inspect the main menu and additional-actions page. There must be one original Load action and no remaining custom Restart Mission action from this experiment.
- Use different game languages on the two computers. The two confirmation headings and the short recovery heading must follow the game catalog and fit their single-line popup headings. The recovery notice uses a statement heading and a single OK button; OK starts the controlled return to the main menu.

## Repeated-load comparison

Run each row in a fresh process on both computers, with the same APIShared, Extended Data, other mods, participants and existing save copies. A disabled testmod means its plugin is absent before startup on both peers; hiding its menu action or changing a setting in an already running process does not remove its permanent hooks. Do not overwrite either source save.

| Case | Testmod | Transition and sequence |
| --- | --- | --- |
| A: ordinary workflow baseline | Absent on both peers | Manually leave the match, create and join a new lobby, load save A; repeat the manual leave/new-lobby workflow to load save B, then start a fresh game. |
| B: ordinary workflow with hooks | Active on both peers | Perform exactly case A using the normal lobby commands, without the testmod's in-game Load action. |
| C: coordinated return | Active on both peers | Use the in-game Load action to load save A, repeat it to load save B, then manually leave and start a fresh game. |

For every load, record the selected file's actual path and native load path on both peers, lobby identity, player remapping, first simulation tick and sustained tick advancement. Exercise A then A as well as A then B. A managed Started marker alone is not a completed multiplayer start. Separately record a cancelled/rejected load followed by another ordinary load and a fresh game. Compare cases A/B before attributing a failure to the coordinated-return action; case A still includes the installed SDK and other mods, so it is not a mod-free Vanilla control.

## Failure and cancellation cases

- Change or disconnect a participant before the protocol commits. The incomplete transition must be refused; do not silently form a smaller replacement group.
- Disconnect a participant after commitment or while the private lobby is being created or joined. The mod must report a failed transition rather than open a save dialog for an incomplete party.
- Fail or delay lobby creation. A pending creation callback must not write into a discarded placeholder during recovery. Keep both logs. When the native recovery notice is offered, its only OK button must start the controlled main-menu return; there must be no misleading Yes/No choice. The transition guard remains active until OK and recovery completes.
- Exercise the normal loader's own save rejection and cancellation paths. Once the group's return is complete, those remain normal lobby operations; the testmod must not force a load, substitute a save, bypass a refusal or overwrite settings to satisfy it.
- An error after the coordinated exit cannot roll back the old simulation. Do not attempt to continue a partially stopped match. Ending the program remains available if a test failure prevents normal recovery.

## Evidence and limits

Keep **both** BepInEx logs. This version uses the `MP_SAVE_LOAD` prefix. Useful transition markers include `INITIALIZED`, `SESSION`, `PREPARED`, `STOP_REQUESTED`, `SOURCE_STOPPED`, `LOBBY_REQUESTED`, `LOBBY_OPEN`, `PARTY_JOINED`, `LOBBY_RELEASED`, `LOAD_MENU_READY`, `VANILLA_SAVE_DIALOG`, `FAILED` and `CANCELLED`. Retain any ordinary save-loader or APIShared rejection messages as well.

`LOAD_MENU_READY` means the coordinated lobby return is complete. `VANILLA_SAVE_DIALOG` means the host opened a picker; neither proves that a save loaded or that the simulation is advancing. Confirm the actual loaded player assignments, populated rendered map and sustained tick progress on both computers.

Offline protocol and runtime fixtures check only their documented state, serialization, method-binding and isolated control-flow contracts. Their external Steam, Unity, UI and native boundaries are test adapters, not real multiplayer acceptance. Use the successful build record and verification log for the exact source/package identity and completed checks. No game test is run by the build; the user's manual multiplayer acceptance remains outstanding until performed.

Uninstall by removing only `BepInEx/plugins/MultiplayerRestartTest_Serp` while the game is closed. This testmod is not part of the main modpack.

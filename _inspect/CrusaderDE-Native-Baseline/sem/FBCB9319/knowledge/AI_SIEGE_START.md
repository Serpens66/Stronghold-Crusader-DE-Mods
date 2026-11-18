# AI siege start and ally attack request [Scope: Vanilla]

Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed DLL hash checked
2026-11-18). Managed: baseline export `BC8B6A39`. Static audit 2026-11-18 from the semantic Ghidra pseudocode,
raw Ghidra xrefs and the managed decompilation; no game run. Tags: **[CS]** confirmed-static (data/control flow
read from pseudocode/xrefs), **[C]** candidate (meaning or runtime behavior not proven). Function labels are
candidate. Player fields are relative to `379ADD0` (stride `0x583C`, Extender `GamePlayerResources`). AIC fields
are `404C950 + (identity-1)*0x5E4 + off`, named after `SHCDESE.AICDecoder/InternalAIC.cs`. Continues
[AI_MILITARY_ADMISSION.md](AI_MILITARY_ADMISSION.md#active-siege-controller-handoff-evidence).

## Scheduling

- [CS] `57330` runs the AI only when `8574B90` (multiplayer mode) != 0 (skirmish 99 or MP 1). `2AE40` handles a
  player only if `+0x2300` (AIC identity) != 0, `+0x2244` (WinLossState) == 0 and `+0x2210` (IsPaused) == 0.
- [CS] Peace time `38722DC` and `37EF974` gate only the recruitment/classification calls (`40430`, `40230`,
  `40740`, `2ECD0`, `29520`, `29B40`, `40620`, `2EAC0`). `3C2E0` (siege), `2F080` (recruitment mode) and
  `2F670` (target choice) run regardless. No new siege troops are recruited during peace time.
- [CS] `3C2E0` runs on the `+2D07C` slot pass, `2F080(...,1)` and `2F670` on the `+2D080` slot pass (separate
  `2AE40` calls). [C] One pass per player was observed at roughly 200 ticks (run `5ca1626871b8`).

## State 0 -> 1 -> 2 (`3C2E0`, `2FEF0`)

- [CS] `+0x384C == 2` (defend-me request) with live requester `+0x38E4` returns before all siege code.
- [CS] `2FEF0` is called only in state 0. It has no timer, peace-time or target-liveness test:
  1. `+0x384C == 1` (ally attack request) -> start at once, no troop check.
  2. If `+0x38E8` (ApproximateArmyStrengthScore) **of the target** `+0x2BD8` < 101: own `+0x38A0` := 0.
  3. Require `+0x30F0 - +0x2BA8 >= siege_trigger_level (0x1F8) + +0x38A0`, else no start
     (debug text "Not Sieging, not enough troops").
  4. Joint wait if `+0x3908` (RequestedJointSiegeAllyCount) != 0: over players 1-8 != self, alive (`187E60`),
     `8574BCC[i] == -1` (no human), `8574C44[i] != 0` [C: slot present], same team (`37EDF3C`). An ally with
     siege state != 0 -> start; allies are counted ready/unready by the same troop formula. Start if
     ready >= `+0x3908` or no unready ally; otherwise `+0x3930++`, start when > 48 and `3665F28 != 0`
     [C: game type].
- [CS] `+0x30F0` = number of the owner's units with role 10..20 (one increment per role case in `182B00`);
  `+0x2BA8` = role-10 engineers. The left side is siege-role troops without engineers. [C] Reset point
  (per-tick recount) not located.
- [CS] Start writes `+0x3970 = 0`, state 1, `+0x3854 = 0`, `+0x3930 = 0`. `2B6C0` sets `+0x3908` to 2/1/0 by
  `rand%100` against `percent_chance_waiting_for_joint_attack (0x214) + 4*attempts` (capped 90; below half -> 2).
  `2D500`: `+0x3924++` (attempts) and `+0x38A0 = rand%100*siege_trigger_variance(0x1FC)/100 + attempts*mult`,
  mult = `siege_normal_wave_multiplier 0x458` if gold `+0x50C` < 10001, else `siege_high_gold_wave_multiplier
  0x45C`, capped `siege_max_troops 0x454`. This is the size bonus for the **next** siege. `+0x38A0` has no
  other writer, so the first siege only needs `siege_trigger_level` [C: assumes a zeroed record].
- [CS] Same pass: target check `187E60(+0x2BD8)`; a dead target in states < 4 -> 9 (retreat). Then state 1:
  `2A600`, state 2, `3BD50` picks the rally tile `+0x2B98` (nearest `2D250` candidate); `+0x2B98 > 0` ->
  `3D6E0` rally move, else back to 0. State 1 never persists.

## Target choice (`2F670`, every `+2D080` pass)

- [CS] Mode 1/2 with dead requester `+0x38E4` -> mode 0. Mode 2 -> target 0. Mode 1 with live `+0x3850` ->
  `+0x2BD8 = +0x3850` (no team test), else mode 0.
- [CS] Otherwise it re-picks if `who_to_pick_on (0x2F0) > 1`, the target is 0/dead, or state < 2, among alive
  players of another team: 4 = closest human only (`8574BCC != -1`), 2 = closest, 0 = most gold, 1 = lowest
  `gold/100 + 5*[+0x2180] + [+0x38E8] + 2*distance`; nothing found -> closest enemy. Writes `+0x2BD4/+0x2BD8`.
  AI targets are valid except with value 4.
- [C] `3C2E0` and `2F670` run in different passes: the first state-0 pass after an attack request can start
  against the previous `+0x2BD8` before `2F670` copies `+0x3850`. A single enemy makes this irrelevant.

## Siege progression (`3C2E0`, per pass of the player)

- [CS] In every pass with state != 0 first: `2FD40` true (and state != 9), last stand `+0x38F0 > 0` with
  state < 6, or `+0x3970 > 199` -> 8 (retreat bundle, then 9 in the same pass). `2FD40` sums cover `+0x2BC4`
  (/8 after 12-16 passes), storm `+0x2BC0`, diversion `+0x2BB4`, assassins `+0x2BB0`, wall `+0x2BD0`, reserve
  `+0x2BCC` and shock `+0x2BC8` (moat `+0x2BAC` is added only in state 6, otherwise used as a ratio); retreat if
  the sum <= 1, or <= 4 while moat/2 > 2*sum. Ladders, tunnelers and engineers do not count.
- [CS] 2 -> 3: `29A20` true = of all populated window groups (`2C8040`), the percentage with tribe short
  `+0x614 < 20` >= `siege_troops_on_site_percent (0x204)` [C: meaning of `+0x614`]. One-shot dispatch.
- [CS] 3 -> 4: `298C0`, same test without storages 18/190/15; also clears tribe `+0x634`. `+0x3970++` per pass.
- [CS] 4 -> 5/6: `+0x381C++` per pass; if the player has no units of types 39/40/58/59/77 in state 3 (`2A6B0`)
  it is set to 100 first. Leaves when `> siege_soften_up_delay (0x20C)`; then `CF360(player, target)` (keep
  regions equal or reachable, flag 0) -> 6, else 5 with diversion, assassins, ladders.
- [CS] 5: strategic dispatch every 4th pass; `CF360` or `CF400` (flag 1) true -> 6; `+0x2B98 < 1` -> 0.
  6: `3B450` lord assault; if it fails and nothing is reachable -> 5. Lord dead: states 4-6 -> 7, wait
  `siege_victory_delay (0x210)` passes -> 9; 9 lasts at most 20 passes (or until `297A0`/`2FD40`) -> 0.
- [C] An open target keep goes 4 -> 6 and skips 5; a closed castle is needed to observe state 5.

## Ally attack request (Vanilla UI path)

- [CS] Managed `HUD_AlliesPanel` "Attack0..5":
  `EngineInterface.GameAction(Enums.GameActionCommand.Ally_Orders /*1061*/, enemyIndex+1, allyPlayer, enemyPlayer)`;
  "Defend" uses mode 11, "Cancel" mode 10. Allies are players of the viewer's team, enemies players of any other
  team, human or AI. `GameAction` takes `EngineInterface.threadLock` itself.
- [CS] `DLL_GameAction (81870)` case `0x425` -> `D7520(mode, ally, target)`: returns if `8574BCC[ally] == -1 &&
  8574C44[ally] == 0`; stages sub-op 0, target (only for mode < 7), ally, requester = native view `88E3D70` and
  mode, then queues Chore 113 (`23990`). Handler `1A6F0` sub-op 0 -> `D7580(target, ally, requester, mode)`.
- [CS] `D7580`, AI ally (`8574BCC[ally] == -1`), mode not 10/11: `41500` accepts only if the ally's `+0x3820`
  == 0 (written by `2DFC0` from its own threat value; [C] "needs help itself") and `+0x30F0 - +0x2BA8 >=
  siege_troops_before_will_come_to_rescue (0x200)`; otherwise refusal speech and nothing is set. On acceptance:
  `+0x384C = 1`, `+0x3850 = target`, `+0x38E4 = requester`, `2A580` (retreat bundle `2B5F0`, `304B0`, `30550`)
  and siege state := 0, also in the middle of a siege. No team test in native code.
- [CS] Mode 1 is cleared only by `2F670` (requester or target dead), mode 10 (`D7580`) and the confirm/cancel
  handlers `D7360`/`D77D0` (actions 1065/1066). While it stays 1, every return to state 0 starts a new siege.
- [CS] No Script Extender wrapper: `shcde-script-extender/src` only names Chore 113 and the three fields.
  Other ally sub-ops: 1 `D78C0`, 2 `D7AD0`, 3 `D7450` (goods), 4/5 above.

## Answers and recipe

- Troops first: siege troops (roles 11-20) come from recruitment mode 2 (`+0x30F8`, `2F080`) before the siege;
  `40740` stops siege recruitment while the state is != 0 [CS]. A troop-less ally request is refused unless
  `siege_troops_before_will_come_to_rescue <= 0`; even then the next pass in state 2 retreats through `2FD40`
  (2 -> 8 -> 9 -> 0, and with mode 1 still set it restarts) [CS].
- `2F080` [CS]: before tick `0x12C0` (4800), when all three stance chances are 0, or on the defensive roll, the
  mode is 2 if `+0x30E8 >= defense_patrol_trigger_level (0x174)`, else 0; otherwise harass/siege by the
  `chance_of_*` weights of the stance (`0x12C/0x138/0x144`).
- Target may be AI: the natural path (except `who_to_pick_on` 4) and the ally request accept any live player of
  another team; the natural path needs no human. The attacker must be AI (`122B40` returns at once for a human
  owner) [CS].
- Fastest Vanilla-conform recipe [C until measured]: skirmish (mode 99), no peace time; human P1 and AI attacker
  on team A, one enemy AI with a closed castle on team B; attacker AIC with small `siege_trigger_level` and
  `siege_troops_before_will_come_to_rescue`, `defense_patrol_trigger_level` 0, siege-only stance chances, a small
  `siege_soften_up_delay` and enough gold. Wait until `+0x30F0 - +0x2BA8` reaches both thresholds and the
  `2FD40` sum is > 4, then let `2FEF0` trigger or call `GameAction(Ally_Orders, 1, attacker, enemy)` from the
  human view. Expect 0->1->2 in one pass, 2->3 and 3->4 after the groups reach the rally point, 4->5/6 after the
  soften-up delay, then 6/7/9. Cancel with mode 10.

## Observed in game 2026-11-18

Two runs of `Testmods/AiGroupAdmissionTest` scenario `ai-siege` (map `test AI overbuild eachother.map`, free-for-all
skirmish without peace time, human P1 idle, speed 250; evidence `.work/t/aigroups/001/ea0a7eb63b2d` and
`7505b2c67f2c`). The attacker's army consisted of siege-role units admitted through APIShared in state 0.

- No team setup was needed: `GameAction(Ally_Orders, 1, attacker, enemy)` from the human view was accepted by a
  non-allied AI as soon as `+0x30F0` reached `siege_troops_before_will_come_to_rescue` (run 1, lords Rat+Pig: 30,
  accepted at 32; run 2, lords Wolf+Rat: 8, accepted at 8; which lord held player 2 was not logged). Below the threshold nothing changed (`+0x384C` stayed 0).
- `+0x30F0` counted the admitted siege-role units (+4 per batch of four), engineers (`+0x2BA8`) separately.
- State sequence run 1: 0 -> 2 (1 never observed) -> 3 after ~150 ticks -> 4 after ~1700 -> 5 after ~200
  (soften delay 0) -> 9 after ~15300 ticks (target lord alive) -> 0 after ~1350 -> 2 (mode 1 still set).
- Run 2 (soften delay 4): 0 -> 2 -> 3 -> 4 -> 5 -> 6, then 6 -> 5 -> 6 -> 5 -> 6 (the 6 -> 5 fallback of
  `3B450`) -> 9 -> 0 -> 2. State 7 was not reached because the target lord survived.
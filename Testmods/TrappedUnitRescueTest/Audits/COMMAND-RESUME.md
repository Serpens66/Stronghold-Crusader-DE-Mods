# Command continuation audit [Scope: Mod work, not Vanilla]

Snapshot: 2026-11-25. Offline analysis and pure fixtures only; no installation or game execution. Native facts below apply to the canonical installed CrusaderDE.dll SHA256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The database is selected through `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`, with that hash in every function query. Type names and public enum values were checked against the source and actual installed SHCDESE assembly. Mod changes remain in this testmod.

## Dispatch and phase matrix [Vanilla facts]

Native dispatch182B00 reads the per-type handler table at321CB0. The group-order capability table3226B0 is copied by19A240/19C130 and may be overridden by19D960. Capability unitA9E==1 also occurs for Deer, Lion, Rabbit, Camel, WarDog, Goat, Hyena and Crocodile. It is not a military-type or resume-safety proof.

The following matrix distinguishes type recognition from functional continuation. `R` means the current active ordinary segment may be admitted under the complete identity/intent/pose/crew contracts below. `N` means native owner required; it is not an unsupported-type inference. These are not blanket stationary permissions.

| Extender type suffix | Handler RVA | AI0 | AI1 | AI101 |
| --- | --- | --- | --- | --- |
| ARCHER | 13F540 | R | N | R |
| XBOWMAN | 141DA0 | R | R | R |
| SPEARMAN | 143400 | R | R | R |
| PIKEMAN | 145030 | R | R | R |
| MACEMAN | 146A70 | R | R | R |
| SWORDSMAN | 1483F0 | R | R | R |
| KNIGHT | 1493E0 | R | R | R |
| LADDERMAN | 14A3F0 | R | R | R |
| ENGINEER | 14AED0 | N | N | R |
| MONK | 151090 | R | R | R |
| CATAPULT | 1520D0 | R | R | R |
| TREBUCHET | 1535F0 | N | N | N |
| SIEGE_TOWER | 15ECE0 | R | R | R |
| BATTERING_RAM | 160420 | R | R | R |
| PORTABLE_SHIELD | 161220 | R | R | R |
| ARAB_BOW | 167D40 | R | N | R |
| ARAB_SLAVE | 169B70 | R | R | R |
| ARAB_SLINGER | 16B220 | R | N | R |
| ARAB_ASSASIN | 16CD70 | R | R | R |
| ARAB_HORSEMAN | 16E440 | R | R | R |
| ARAB_SWORDSMAN | 16F590 | R | R | R |
| ARAB_GRENADIER | 170570 | R | N | R |
| ARAB_BALLISTA | 171C50 | R | R | R |
| BEDOUIN_CAMEL_LANCER | 1731F0 | R | R | R |
| BEDOUIN_HEALER | 174100 | R | R | R |
| BEDOUIN_EUNUCH | 174F90 | R | R | R |
| BEDOUIN_AMBUSHER | 1768A0 | R | N | R |
| BEDOUIN_SKIRMISHER | 178350 | R | N | R |
| BEDOUIN_HEAVY_CAMEL | 17A590 | R | R | R |
| BEDOUIN_SAPPER | 17B540 | R | R | R |
| BEDOUIN_DEMOLISHER | 17CF50 | R | R | R |

The six ranged `N` AI1 entries have no corresponding top-level AI1 switch case. Crossbow141DA0 AI1 resolves its shared animation tail at142DF2/142E0E to AI0 and181890. Slave169B70 AI1 similarly resolves16AE2C/16AE32 to AI0 and181890. The melee/support AI0/1 branches choose idle animation, native home movement, combat/patient selection or a new native role. They do not become frozen under flags5. These autonomous changes must win over the previous command receipt.

Engineer14AED0 AI0 can select payload work (9E0), digging/filling or a group role. Its199C70 call is a wrapper for11E960 command15, which traverses group members and can assign engineer8/9 and new building targets. AI1 also has an engineer-specific manning path. These phases remain with their native owner; the testmod does not infer that an empty payload by itself makes them ordinary. Engineer AI101 requires both9E0 andAA4 zero; its late command20/payload path is thereby excluded.

Healer174100 AI0/1 may call18DE10. That helper selects using current fine position/height, candidate health/lifetime, range and visibility. Nearby selection installs patientAAC/AB0 and AI7 together; distant selection uses successful196280 and AI101. Healing belongs to AI7, which is not admitted. Eunuch174F90 AI1 calls187A40 with parameter0 (idle animation); parameter1's1853F0 path is not the admitted call.

Trebuchet type40 dispatches to1535F0, independently of Catapult1520D0. Its AI101 unconditionally becomes AI0 without188340; AI0 requires three crew and native fire/ammunition logic, and AI1 is not a normal movement-arrival branch. An artificial A9E override must not enable independent rescue.

### Dig/fill selector correction

The relevant command selector is **word9F4**, not word956. For example, Archer13F540 assigns its command pointer from `DAT_1867E8DF4`, then compares6/7 in AI0; Slave169B70 and Skirmisher178350 independently read the same addresses for their dig/fill branches. Successful dig travel writes AI124, fill travel AI125. Native11E960 is a command producer; the type handlers may reset9F4 to3 after rejected work selection. The Extender enum is `TribeAICommand.DigMoatTileId` for6; the audited fill value7 is presently named `Unknown7` by the SDK. Word956 is a separate work-mode field and value6 there alone must not be interpreted as digging. The fixture contains that counterexample.

## Movement, contact and crew [Vanilla facts]

Native188340 accepts ordinary arrival when74E==0, independently of distance if portal8EC==0. Therefore a failed196280 after path termination cannot safely be represented as an unowned flags0 unit. Native196280 can clear path bytes before failing while leaving old active flags/cursors; those leftovers do not prove a fresh route. Only its actual positive return plus validated live publication supplies that proof.

Native193B80 sets74E=5 **and** copies currentAI918 into91E. Its current-image callers are178350,17CF50,1855A0 and195170. The testmod therefore does not call it as an otherwise harmless generic parking routine. The movement dispatcher1855A0 still performs186AD0/186AF0 and180230 under flags5;180230 can commit Next if raw740 is nonzero. An owned barrier needs substep8, raw740 DWORD0, coherent packed Current, and Next==Current.

The five mobile siege handlers above require complete crew counts Catapult2, Tower4, Ram4, Shield1, ArabBallista2. Capture retains all four engine slot IDs96C..972 and globals974..980, plus countA0C. Attach writes Engineer9F4=3, AI109/resume5 and the engine slots; it does not establish a reliable Engineer9F6/9F8 backlink. Engineer14AED0 AI5 uses its own974 DWORD as a watchdog. Native182B00 refreshes that watchdog for count-selected matching-global AI5 crew. Retry validation therefore checks captured count/slots, alive Engineer AI5, hidden9B0 and owner; it must not invent a backlink or scan all units at every retry. Initial engine alias detection remains a separate root admission guard.

Ram1946A0 uses current tile/coordinates, A00 and6E8 to recognize present physical contact within a one/two-ring neighbourhood. It can197950 and select AI107 even while188340 is blocked. Tower181E30 validates current PCL, wall approach PCLs, connected edges and free cells; an approach change can also197950. These are legitimate native owner changes, not false arrival. Tower approachA10, siege input6E8 and target98E belong in command continuity. Native197950 clears path length/cursor752,92A and8EC, not AI/goal/flags. Generic193DE0 likewise uses current physical contact and may choose native attack/work phases.

Catapult AI0 through18FC00 may callD5710 to debit ten player stone and add twenty unit ammunition without arrival. This is native global replenishment. Ammo9BA and natural animation/cooldown timers must not be copied back or treated as immutable command identity. No testmod code replays replenishment, stock, yield, crew attachment or job producers.

## Continuation and ownership [mod integration]

Worker and Command are explicit receipt kinds in binary schema3. A Command receipt contains `CommandJourneySnapshot`, no worker Job and empty external worker bindings. It retains exact selected identity, AI/resumeAI, tribe ID/stamp, attack-move goal, work/role/last-command fields, melee/ranged adversary identity, context unit/global/building tile/coordinates, native role context and A9E. Siege adds full crew and native control/target; Tower addsA10; Engineer adds empty-payload proof; Demolisher adds queued748/74A goal. Route flags, path buffer, movement/substep, storedMoat9C8, animation, ammunition and natural timers are not command identity. No whole-unit copies or state restoration are used.

A reservation precedes every relocation/planner mutation; capacity512 failure causes no mutation. The safe flags5 barrier writes only74E after identity/AI/intent/pose validation. Retry uses actual native result and the final API authority predicate. Immediate successful publication validates the same identity/AI/intent/crew/goal and actual active route or true arrival; fresh marker9C8==1 is allowed only in this success proof, never inferred from stale active flags after failure.

Moat proof is reserve→qualified coherent-pose park→one original-goal probe. Accepted publication releases without teleport or a second planner call. Rejection may invoke one root-qualified relocation and one resume; worst case is three native functions (probe, relocation, resume). Delayed causal capture may accept the originally proven next tile, including an exactly completed original segment with liveflags0, but cannot accept arbitrary current-position flags0 stops or a second unproven step. Worker delayed continuity ignores only the specifically audited role countdowns before taking a fresh exact operation snapshot.

External order Pre immediately revokes old authority. Post/abort callbacks update bounded metadata only. Reconciliation occurs at the supervisor's own safe boundary, after the complete native wrapper; a failed foreign order after a successful own call may leave stale flags2 and must not be mistaken for a fresh foreign route. Its live new goal can be safely parked/rebound, never replaced by the old target. Aborted calls invent no goal/result and permit no blind rebind. A coherent external pose change revokes the old spatial proof. Save capture rejects pending half-observed external operations and transient reservations.

An integration review found a concrete adapter gap: its failed external-order branch formerly required flags5 before even constructing a replacement. In ownNative1→foreignNative0, native196280 can retain flags2 after clearing the path bytes. The adapter returned Keep; the supervisor retained foreign authority and therefore could not enter Continue's parking path. The fake supervisor world had modelled the intended parking, so its passing tests did not cover this actual adapter branch. The adapter now calls the same read-only `TryPrepareFailedOrderHandoff` core as the offline fixture. Actual0, exact lifetime/tribe/ordinary mode, coherent substep8/Current==Next/raw740zero and a fresh admitted intent qualify flags0/2/4/5. Newly adopted0/2/4 require the old AI to remain; already owned5 retains the previously allowed fresh Command/AI metadata handoff. Workers always require unchanged AI/job and siege requires unchanged crew identities. Only then does the native adapter call TryPark on the fresh live goal at the supervisor boundary. Native1 is not converted into a refusal handoff; its release still needs the actual positive-publication path. The new fixture checks this actual production reader core for every admitted worker R segment, rather than relying on FakeWorld alone.

Ordinary Tick visits and native attempts have explicit budgets. Dormant jobs cause no unit scan. External handoff reconciliation is separately bounded by the512 owned-receipt maximum and performs no planner call; it is not claimed to fit a smaller Tick visit budget.

### Stationary admission boundary

An active-command audit is not automatically stationary admission. `IsSupportedStationaryPhase` admits only the AI0/1 entries marked R, with the same exact command semantic capture, ordinary coherent pose and root causal terrain proof. It supplies no old-goal restoration permission. Engineer0/1, all Trebuchet phases, the six absent ranged AI1 cases and completed AI101 flags0 are not inherited by this helper. Spearman AI2 is additionally admitted by that helper through its separately audited native animation/181890 proof. Root must store/check stationary semantic proof independently from an active continuation receipt, so a stationary unit does not accidentally enter active reissue code.

In particular Current==Requested is not sufficient to prove idle semantic state:188340 with ordinary flags0 does not require this equality. A stationary Engineer with payload can otherwise pass a generic type/AI0 classifier and enter native work without an arrival check. A stationary contact phase needs its own complete phase-specific admission before broadening this boundary.

### Native vegetation domain at the worker boundary

The automatic worker adapter formerly passed native selected-tree9A0 directly to the SDK's one-based vegetation getter. That bound the previous array record. Roles with a unit-stamped serial could reject a valid job; Apple, which lacks that serial, could capture the wrong neighbour's lifetime. The independently verified108040/135E30 contract uses a positive native array index and zero sentinel; the public getter resolves array[id-1]. Runtime and Fixture.Workers now share NativeVegetationAccess: reject index<=0 or index>=current span length, translate to SDK index+1, then require the returned pointer to equal the actual span array base+nativeIndex. No pointer is retained. NativeVegetationAccessFixture invokes that same pointer core with adjacent controlled records: a dead or reused selected tree cannot be masked by the alive unchanged previous record. Wrong getter indexing fails closed. This corrects the binding lookup; it does not by itself solve the separate P7 need to retain an external binding's lifetime from the original Pre snapshot until delayed admission.

The follow-up temporal review confirmed that the original mutation pipeline also needed the selected tree's **pre-mutation** external global, not merely the corrected index translation. Apple carries no vegetation serial in its unit job snapshot; deleting/reusing the selected slot during construction can leave9A0 unchanged. NativeWorld.TryCapture now requires `before.HasCapturedVegetation` and an exact `before.CapturedVegetationGlobal` match through the shared pointer-core TryReadCapturedAliveGlobal; Wood additionally retains its native700 serial comparison. The root SnapshotUnit/causal-save pipeline owns capture and serialization of these two fields. The controlled fixture includes Pre222→dead and Pre222→reused999 with alive previous111, as well as the missing-Pre-evidence rejection. A late fresh read cannot supply historical identity.

## Verification and remaining game acceptance

The isolated executable compiles the actual policy/ledger/supervisor/adapter against the genuine installed SDK, with a fake native world for bounded ownership decisions. Fixtures exercise type×phase admission, every immutable field mutation, independent work-mode versus dig selector, width/schema/codec corruption, full crew identity, native0 stale publication, original accepted-step completion, sixteen nested foreign-order combinations, aborted orders, source-pose takeover, save ownership and reserve-before-mutation. These prove pure contracts and compilation, not native gameplay effects.

Controlled game tests remain required for each admitted family: local rescue, bridge/moat source and next-tile cases, started/completed steps, groups, siege crew identity/health/occupancy, actual native AI transitions, live foreign orders, save/load/editor lifecycle, and payload/resource conservation. Trebuchet, engineer job-owner phases, missing ranged AI1 handlers and stationary completed-contact phases are explicit boundaries, not passed game tests. Worker/open ox or moat-worker cases retain their separately documented limitations.

Reproducible local analysis inputs are `.work/a/rescue-impl/001/command-types.py`, `command-audit.py`, `command-case-pack.py`, `command-ranged-pack.py`, the compact type/case ledgers and canonical hash-qualified function query interface. Regenerable full handler dumps are working evidence, not an independent baseline. Canonical baseline changes are coordinated by the root chat.

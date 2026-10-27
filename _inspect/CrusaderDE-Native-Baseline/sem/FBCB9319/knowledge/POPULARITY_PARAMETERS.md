# Popularity contribution parameters [Scope: Vanilla + mod integration]

Audit: 2026-10-16. Installed Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The installed file matches CURRENT.json and the current-hash function records.
Local SE source commit `5908e1f12437deb7ef5f5b1dc2d64e301178fba3`, tree
`c238441ea12db35ee36c5c5668d3c8fce0897295`, matches database provenance.
Installed SHCDESE assembly version: 2.14.1.0. Its GameGlobalsManager declarations
were independently decompiled and the consumer type-checked against that DLL.

## Audited path and scope

Current-hash semantic records: resource scheduler 0xD1AD0 calls popularity updater
0xCB090 outside the excluded app modes. The updater rebuilds church/cathedral
counts through 0xCCD20, iterates player IDs 1..8, and checks player eligibility.
Special app-mode branches assign fixed popularity; small-population protection
can assign 7500 instead of executing the component block. These bypasses remain
authoritative; configuring a contribution does not force those branches to run.

The ordinary component block selects ration, food-variety, crowding and tax
contributions, followed by fair, blessing/building, ale and fear contributions.
The SE's existing inline replacement reads 41 ManagedValue<int> parameters for
these contributions, 13 separate coverage/ratio thresholds, and publishes
OnPlayerCalculatePopularity(Pre). This event is before subsequent native event
penalties and the final clamp, not a final-popularity notification.

The names above are semantic role descriptions. Generic database function names
retain their candidate classification. The component branches, constants and
managed replacement are supported by the current-hash decompiler records and
the matching SE implementation; no new hook span or instruction claim is made.

## Display scale and defaults

Total popularity uses 100 internal units per displayed point (0..10000 internally).
Component reports instead divide their signed contribution by 25; this is directly
visible in the current managed FatControler consumer. Configuring component points
therefore multiplies by 25, not 100.

| Components | Defaults in report points |
|---|---|
| Rations None/Half/Full/Extra/Double | -8/-4/0/+4/+8 |
| No preferred/available food; no population | -8; +8 |
| Food variety exactly 2/3/4 types | +1/+2/+3 |
| Crowding 101..120/121..140/141..160/161..180/>180% | -2/-4/-6/-8/-10 |
| Taxes modes HugeDonation through Cruellest | +7/+5/+3/+1/-2/-4/-6/-8/-12/-16/-20/-24 |
| Donation without positive gold; unknown mode | +1; -24 |
| Fair event flag active | +16 |
| Blessing 25..49/50..74/75..94/>=95% | +2/+4/+6/+8 |
| At least one church; at least one cathedral | +1; +2 (additive, not per building) |
| Ale coverage 25..49/50..74/75..99/100% | +2/+4/+6/+8 |
| Fear coefficient, multiplied by signed fear boost | +1 per level |

Ration-mode contribution requires positive population and preferred-food state;
otherwise the separate fallback applies. Food variety is added independently.
Donation selection requires positive gold for modes below None. Thresholds use
strict > for religion (24/49/74/94), >= for ale (25/50/75/100), and strict > for
overpopulation (100/120/140/160/180). Ale coverage helper 0xCEA90 computes the
coverage from inn state and population and clamps to 0..100. Configuring the
coefficient does not change those producers or thresholds.

## Downstream effects and acceptance limits

The replacement updates the normal contribution report fields, the current
popularity accumulator and its worst-source selection, then resumes the remaining
native path. Plague and other timed events continue afterwards; the existing
BugfixesAndQoL plague correction at 0xCB57C remains outside this mod's ownership.
Vanilla clamps the final total to 0..10000 and retains its popularity-change
speech/history decisions. Report producer 0x19D960 independently recomputes component values using hardcoded
Vanilla constants; it does NOT consume the SE replacement contribution fields.
The original 2026-10-16 claim that these report fields followed SE values was incorrect.

Religion also adds `(uint)combinedReligionContribution / 25` to
`r_UnknownPopularityAccumulationByCurrentDividedBy25`. The SE preserves this
unsigned native bookkeeping expression. A negative configured religion sum
therefore has a large unsigned quotient in that auxiliary counter. Its gameplay
meaning is unconfirmed; do not claim that negative religion values preserve all
unrelated statistics. PopularityTest intentionally exposes the existing direct SE
parameters and adds no correction hook. Negative modifiers' complete in-game
effects and report presentation still require runtime acceptance.

No game UI or real multiplayer acceptance is inferred from source/type checks.
PopularityTest's independent catalog/UI checks compare all 41 SE defaults and
exercise signed settings, persistence and anonymous-stack ownership separately.

## Display-path correction audit (2026-10-17)

Native hash above unchanged; managed SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Installed SE 2.14.2 still replaces the simulation block only. Latest game log:
15:37:34 applied 17 overrides; 15:37:40 callback accumulator=10025 with 17 active
(overall final delta is not logged). The earlier loaded save had zero overrides.

Confirmed static flow: native 0x19D960 -> PlayStateReturnData ->
EngineInterface.CopyPlayStateStruct -> GameMap -> GameData.setGameState ->
FatControler.NoesisGUIUpdateChecksInGame -> public MainViewModel properties.
The native report contains hardcoded contributions for all eight groups and builds
upcoming_total_popularity from these plus the existing event amounts. Ration and
tax modes incorporate the selected panel preview. food_types_available carries
the same native preferred-food field used by the simulation. Fear uses the signed
fear_factor. Crowding report amounts identify its selected tier (-50..-250).
Religion coverage thresholds: 25/50/75/95, ale: 25/50/75/100.

Vanilla church_adjustment (25/50/75) is copied from byte to sbyte and serves both
as a numeric bonus and building identity for localized labels. Do not replace it
with configurable values: signed/large settings overflow and break label dispatch.
PopularityTest corrects only public HUD text properties after the existing
APIShared GuiChecks publisher, preserving PlayState and native simulation.
The report's zero-population ration value is zero whereas simulation uses +200;
when that explicit fallback is enabled its absolute effective value is displayed.
Thus that known Vanilla discrepancy is not a valid default-equality assertion.

The GUI callback also owns the increase/decrease booleans and event-details-button
visibility (the latter depends on the displayed event sum). Public text setters
update their associated face icons. Panel wire values 16:{2,3,4,69,72,73,79} are
Vanilla protocol constants; SE exposes no corresponding panel enum. Normal GUI
checks also run paused. No new native hook ownership is introduced.

Runtime acceptance of the correction, including multiplayer, remains pending.

## Transfer to APIShared (2026-10-17) [mod integration]

The user confirmed the original PopularityTest correction working in-game. The
reusable calculator and presentation adapter now live in APIShared ScriptExtenderFixes.
This transfer reads effective SE values independently of consumer toggles and only
corrects values differing from Vanilla. Consequently the zero-population discrepancy
above remains Vanilla when its SE modifier is unchanged. The 13 separate threshold
parameters are outside this correction's scope. Simulation and override ownership
remain unchanged. The transferred implementation still requires separate game,
editor/save/load and real host/client acceptance; no installation is part of this task.

## Fractional fear coefficients (2026-10-18)

Same native fingerprint; the installed SE fingerprint audited for the managed adapter is
`577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175`.
The SE callback has two signed-boost multiplication paths (getter/GetValue, signed local,
`mul`), then stores `r_GoodBadThingsPopularityModifier`, adds to the accumulator and
compares with the previous worst component before publishing the Pre event. A correction
only at that event is too late to preserve the worst-source decision. APIShared therefore
changes just those managed multiplication instructions through one permanent adapter;
no new native span is installed. It rounds signed level times exact decimal coefficient
away from zero at midpoint ties, then multiplies whole report points by 25. No registration
leaves the original unchecked SE product intact. Existing eligibility bypasses and later
native events/clamp remain unchanged. Display correction uses that same calculator.
Static installed-body generation and pure fixtures passed; actual Mono/game, editor,
save/load and host/client acceptance remain outstanding for the new adapter.

## Semantic SE compatibility (2026-10-18)

The original SE hash above remains historical audit evidence. It no longer gates the
rounded fear adapter. Startup discovers the owner from coefficient access plus native
contribution storage, checks loaded/disk module identity, and analyzes CFG/stack/locals
and ref/out resource-pointer provenance. All coefficient reads/products must reach the
signed-fear multiplication and 32-bit contribution of the same player resources. Site
count, generated carrier/method names, local encoding and operand order are not fixed.
Unknown or ambiguous meaning fails closed; native game/report fingerprints remain their
separate ABI boundaries. The game and native calculation audited earlier are unchanged.


## 2026-10-18: managed IL operand representation

The installed Extender fear method has two audited coefficient sites. Inside MonoMod ILContext.Invoke its branches use ILLabel/ILLabel[] operands; direct Cecil-only CFG traversal falsely reported unreachable coefficient access. APIShared now resolves and validates label targets without mutating operands. This is a managed-analysis correction; the hash-bound native formula, resource identity, signed input and hook sites remain unchanged. Offline regression invokes the real installed method through ILContext.Invoke; runtime gameplay acceptance is still required.

## Fear-level production and fixed contributions (2026-10-25)

Native SHA-256 remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The current raw function/instruction packets were compared with the canonical installed
DLL: 3,734 instructions across 0xBCEB0, 0xCD850, 0xCB090, 0x19D960, 0xD1AD0 and
0x180560 matched byte-for-byte. Generic function names retain their candidate labels;
the following local machine/data-flow observations are confirmed-static.

The resource scheduler 0xD1AD0 calls building recount 0xBCEB0 before productivity
0xCD850 and the popularity component path 0xCB090. Recount clears counts and level
for all eight player slots, excludes building statuses 0 and 3, and classifies owned
building types using the native ranges/bitmask. Bad types are 62..64 and 91..98;
good types in this exact build are 65, 66, 100, 101 and 103. This records the observed
classification, not an assumption that every decorative building counts.

Resource identity is anchored by manager VA 0x18366C210 (scheduler caller 0xCDE60),
record stride 0x583C and native slot prefix 0x12EBC0. Slot 1 begins at manager+0x1343FC,
matching the Extender array. Signed fear boost at struct+0x2134 is native slot+0x130CF4;
its neighboring bad/good counts are struct+0x212C/+0x2130 and total population +0x2180.
The independent report producer copies those same count/level fields. For nonnegative
population, recount computes denominator = trunc(population/16)+1, then
level = clamp(trunc((goodCount-badCount)/denominator), -5, +5). Division truncates toward
zero (signed IDIV); equal counts retain the zero from the initial clear. The next-stage
field records the remaining net buildings needed. Thus population 100 requires seven
net buildings for each level. Building count, population and ownership are inputs;
a popularity-contribution override does not change this producer.

Productivity is separately 100-10*level percent for native levels -5..5, clamped at
150/50 percent at the extremes by 0xCD850. The popularity path multiplies the signed
level by 25 internal report units; its result reaches component storage, accumulator
and worst-source selection before events and the final popularity clamp. Report
0x19D960 independently computes level*25, so the API display correction uses the same
selected contribution as simulation. 0x180560 independently consumes the level in its
own arithmetic; changing the popularity contribution leaves that input untouched.

APIShared's fixed-level tables use the existing managed multiplication adapter, with
no new hook or change to native branches. Tables select on the signed input level,
not the sign of the configured output. Unspecified levels reveal earlier applicable
registrations or the complete original SE multiplication. Level zero stays neutral.
The Extender's configurable multiplier directly exposes Vanilla's constant 25; that
is a technical explanation, not evidence of the author's personal design motivation.
Actual game, report/preview and real host/client acceptance remain separate.

## Human/AI targeting feasibility (2026-10-25) [mod integration]

Canonical native SHA-256 was rechecked and remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The earlier complete scheduler/component/report audit above therefore remains the
native contract; no new native address or hook span is proposed here. The installed
SHCDESE 2.14.3.0 assembly SHA-256 is
`5CB1C33BE5463086505A17F5C48AAA357D733CFFF7240E101C821F0E3C668498`.
Its actual BulkPlayerDetours and GamePlayerManagerAPI bodies were decompiled for
this feasibility check; generic native function labels retain their earlier trust
classification. The managed observations below are confirmed-static.

The popularity replacement already obtains the current native player ID from R15
and resolves that same player's resource pointer before reading the component
parameters. GamePlayerManagerAPI publicly exposes IsAIPlayer(playerId), backed by
the AI-lord lineup. Classification must use a valid participating slot; a false AI
result alone does not prove that an empty or invalid slot is an active human.

All ordinary popularity coefficients currently come from global ManagedValue<int>
objects. ExtraFeatures publishes its owned overrides for the session, and the
existing FearPopularity selection accepts a level without player identity. These
interfaces do not currently support Human/AI targeting. Existing calculation
context makes such a capability feasible, but both the ordinary coefficient reads
and the fear selection would need player-aware selection before their consumers.
Unmatched players must retain existing SE/foreign values and the original branches.

The installed callback raises OnPlayerCalculatePopularity(Pre) only after component
stores, the auxiliary unsigned religion accumulation and worst-source comparisons.
Changing only its final Popularity value cannot reproduce player-specific component
semantics. The native event/clamp path and eligibility/small-population bypasses
remain independently authoritative. Temporarily changing global stacks per player
would also expose transient global values to other readers; it is not a verified
targeting mechanism.

The independent report path follows the selected native viewpoint. APIShared's
current presentation snapshot captures component inputs but does not select ordinary
or fear policies by that player's identity. A future targeting implementation must
apply the same player-specific selection to simulation, report and previews; the
existing public PlayerPerspectiveAPI.GetViewedPlayerId is the relevant identity
boundary, including uncertainty handling. No targeting behavior, lifecycle roster
timing or real multiplayer execution was implemented or runtime-tested in this check.

## Targeted policy implementation audit (2026-10-25) [mod integration]

This is an additional dated implementation observation, retaining the preceding
feasibility snapshot. Native SHA-256 remains
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
installed SE 2.14.3 SHA-256 is
`5CB1C33BE5463086505A17F5C48AAA357D733CFFF7240E101C821F0E3C668498`.
Managed game report identity remains BC8B6A39 as documented above.
Evidence: workspace `.work/a/pop-targets/001` (installed method/player API
extraction) and `.work/a/pop-human-ai/001` (tests, review and delivery).

Native scheduler 0xD1AD0 calls 0xCB090 only for eligible resource slots. Its
signed checks at manager offsets 0x12EC54 and 0x12ED94 require values >0.
Subtracting resource base 0x12EBC0 yields public `r_KeepId` +0x94 and
`r_KeepDoorId` +0x1D4, with slot stride 0x583C. The targeting classifier uses
these typed public fields to exclude empty/unresolved slots, then the public
SE `IsAIPlayer` query. `ActivePlayerListVA` must not be interpreted as a human
roster: its values distinguish native slot activity, not controller type.

The installed SE callback reads its player ID from the native callback argument's
`NativePointer<X64SmartCPUContext>.Pointer->R15`, narrows to Int32 and passes that
same value to `TryGetPlayerResourcesById`. The new startup data-flow validator
requires the resource pointer for every resource access and source read to be
that resolver's output, and the ID local to retain that same origin. It covers
all declared ordinary source getter/GetValue reads and the existing fear products.
The rewritten real method is generated and JIT-prepared by installed-assembly
tests; a counterexample replacing R15 by R14 is rejected before player edits
and retains the independent legacy fear manipulation.

Selection occurs immediately after successful resource resolution. Each ordinary
GetValue result is replaced only for the captured role/source; missing entries
retain its effective foreign/global value. Fear consumes the same captured policy.
Original arithmetic/branches, strongest-component comparisons, religion auxiliary
bookkeeping, event paths and final clamp remain downstream. No native hooks,
global stack swaps, per-read locks, allocations or roster scans were added.
The independent report adapter captures the viewed player's policy and applies it
to its detached component/preview data. Explicit targeted values equal to a SE
default remain present, covering the known zero-population report discrepancy.

Automated tests prove installed managed data flow and isolated policy/report
behavior. They do not execute native gameplay or establish Mono rendering,
save/load gameplay or Host/Client acceptance; those remain separate game checks.

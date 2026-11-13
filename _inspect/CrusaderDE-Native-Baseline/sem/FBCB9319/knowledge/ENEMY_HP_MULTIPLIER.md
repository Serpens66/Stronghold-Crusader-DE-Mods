# Enemy HP: offline team reference and real multiplayer gate [Scope: Vanilla]

Static audit, 2026-11-04. Installed native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installed managed SHA-256:
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Both installed binaries were hashed against CURRENT.json / DATABASE_INFO.json.
No new game or real host/client acceptance run was performed.

## Producers, mode gate and team reference

The hash-matched managed export `Platform_Multiplayer.cs:3923` starts an actual
network game through `initMultiplayerGame(skirmishGame: false, ...)`, including
the Coop arguments. Human players and AIs are registered separately before the
native map load at line 4029. Offline `FRONT_Multiplayer.StartSkirmishGame`
instead passes true (line 9963). The native export
`DLL_PreInitMap_Multiplayer`, RVA `0x86270` (confirmed export/signature), assigns
state VA `0x188574B90` to 99 for offline skirmish and 1 for real multiplayer.
This was independently decoded from the installed PE: test at `0x86352`,
conditional selection at `0x8636E`, store at `0x86377`.

`DLL_ApplyMultiplayerSetupData` (`0x81050`, confirmed export) delegates to
`0x26370` (candidate generated name). Its advanced-option branch copies the
Enemy HP enum to manager + `0x27A004`, VA `0x1887EE324`; neither branch writes
the effective per-owner table at VA `0x183666058 + playerId * 4`.
Do not equate the stored global option with an applied HP multiplier.

The common skirmish map initializer `0x94350` (candidate generated name) saves
and restores the native mode around map loading. Its ordinary Enemy HP table
writer requires mode 99 or 0. Installed machine instructions `0x95449..0x95450`
compare against 99, accept zero, and branch past the table writes for other
values. The alternative built-in Trail difficulty branch also requires 99.
Consequently a regular new real multiplayer game (mode 1) does not apply this
global Enemy HP option to human players or AI players, regardless of teams.
There is no host-team exemption calculation or per-human enemy calculation in
that path. Map-specific/saved values and independent Lord profile HP are separate.

In the eligible offline path, the initializer compares each player's team with
the team of the native reference/view player; equal teams retain their existing
modifier. Offline skirmish first selects player 1. With the spectator flag set,
it selects the first occupied AI lineup slot (`0x94646..0x94675`), which is player
1 when occupied. Vanilla's managed spectator startup removes the human member
and issues the spectator action before loading the map
(`FRONT_Multiplayer.cs:5575`, `10045..10049`). Thus the reference AI and its team
can retain normal HP while opponents get 150%. This is a team comparison, not
an AI-classification test. Unassigned lobby teams are given distinct native
team identities earlier in the initializer.

## HP consumers and alternate writers

The reset/default paths initialize the per-owner modifiers to enum 1 (normal).
The new-game normalizer `0x197EF0` and type/spawn initializer `0x19A240` read
the effective modifier using the unit's one-based owner. Enum 3 scales default
type HP by signed `6 / 4` (150%), with integer truncation. Both current and
maximum HP are assigned; there is no owner-1 exception in the consumer.
`0x17FEF0 -> 0x19A240` initializes a newly allocated unit; the Lord caller
`0xC23C0` can subsequently multiply both HP fields by the AI profile's Lord HP
percentage. This latter gate accepts native state 1 as well as 99, so different
AI Lord profiles can still have different HP in real multiplayer.

The remaining audited per-owner table writers are reset/default initialization
and mission initialization `0x100530`; the mission branch uses difficulty/team
state and is not the ordinary network map-start path. Type/reset consumers
`0x195D10` and `0x19C760` use `0x19A240`, not the global advanced option.
Save load `0x19A8C0 -> 0x19C300` can normalize full-health units from the
per-owner table; it does not apply the global Enemy HP setting anew. The older
format migration `0x19BF70` is not a universal load step. See the existing
ExtraFeatures session callback audit for these independently audited HP paths.

## Object classes: civilians and buildings (2026-11-04)

The modifier block in the common unit type initializer `0x19A240` is
unconditional with respect to unit type: it reads the controlling owner at
manager-relative record offset `0x6EE`, the default HP indexed by type, and the
owner modifier before storing current/max HP. There is no military/Lord-only
predicate. Thus civilian workers and peasants are covered, as are military
units and mobile siege units created through the same initializer. The existing
unit normalizers `0x197EF0` and `0x19C300` likewise have alive/health gates, not
military-type gates. The factor applies only when the unit's effective owner
has the non-normal modifier; neutral ownership is not an enemy player bonus.

Building spawn `0xB47E0` instead reads default HP directly from VA
`0x1802E7510 + buildingType * 4` and stores the low word in current/max HP at
manager-relative record offsets `0x168/0x16A`. Independently decoded installed
instructions `0xB4C89`, `0xB4C92`, `0xB4C9B` confirm that direct load/store chain.
The owner-dependent fields and type-specific spawn branches do not multiply
building HP by Enemy HP. Building default-table readers/normalizers `0xCA110`
and `0xCA5F0` also use the building-type table, without the per-owner unit HP
modifier. Ordinary buildings therefore receive no Enemy HP bonus. Mobile siege
units should not be classified as ordinary buildings for this conclusion.

All internal function names above remain candidate labels; the statements
describe inspected hash-bound control/data flows, not newly published APIs.
The conclusions concern the vanilla global Enemy HP option. They make no claim
about arbitrary external HP writers or about runtime acceptance of mods.

## Initialization identity and safety audit (2026-11-04)

The same canonical hash was checked during the shared-HP implementation. The
three direct callers of type initializer `0x19A240` are allocator `0x17FEF0`,
same-slot transition `0x195D10` and bulk reset `0x19C760`. Installed machine code
writes the fresh unit GlobalId at `0x17FF74` and alive state NeedsInit at
`0x17FF99`, before calling the initializer at `0x180000`. The initializer writes
the final type at `0x19A27B`. The remaining allocator path does not replace this
identity. Same-slot transitions retain GlobalId and set IsAlive at `0x195D3D`
before the call at `0x195DF6`; bulk reset accepts existing states 1/2 before the
call at `0x19C7B3`. Thus an observer can queue the concrete identity on return,
but cannot use unchanged slot/GlobalId/type as proof that no reinitialization
occurred. Completed outer initialization still matters for subsequent Lord
profile HP and building integration.

Native unit percentage readers use signed Int32 current HP times 100, giving a
safe coordinated unit maximum of 21,474,836. Building health is signed Int16
current/unsigned Int16 maximum at record `0x10C/0x10E`; repair `0xD5030` also
multiplies current HP by positive wood/stone costs. A safe positive building
maximum is at most 32,767 and Int32.MaxValue divided by each positive repair
cost. These are arithmetic bounds, not evidence of a Vanilla scaling policy.

Siege tower unit58 and its later building69 are linked by unit ID/global fields
`0x334/0x364` and building `0x110/0x114`. Deployment `0x15ECE0` can occur later;
both damage directions (`0x7EB00`, `0x15ECE0`) use a signed Int32 product involving
the unit maximum and building health. Scaling both independently can overflow
even while their individual storage bounds are respected. The common coordinated
bound must additionally satisfy unit maximum times proxy maximum <= Int32.MaxValue.

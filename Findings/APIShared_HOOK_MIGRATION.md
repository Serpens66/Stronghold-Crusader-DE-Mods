# APIShared hook migration audit — 2026-10-10

## Result

APIShared 0.6.0 owns the generally reusable command, HUD and native market query
interception points listed below. Consumer mods register policies through public
contracts. Replaced mod-owned detours and source-linked recruitment context/policy
implementations are removed from production. Historical pure-policy test fixtures
are retained under `_inspect/.../Legacy`; no runtime project compiles them.

| Shared contract | Publishers / consumers migrated |
| --- | --- |
| Commands/GameActionEvents | EngineInterface.GameAction(GameActionCommand,int,int,int); UnitCosts, UnitLimit, APIShared recruitment presentation, ExtraFeatures knight Stop preparation, Bugfixes Ctrl trade/spectator guard, CastlePlanner pause guard |
| PresentationEvents.GuiChecks | FatControler.NoesisGUIUpdateChecksInGame; recruitment availability, repair HUD, HD market icons and Ctrl market availability |
| PresentationEvents.BuildingRollover | HUD_Main.UpdateRollover; BuildingCosts and BuildingLimit |
| RecruitmentEnter / RecruitmentLeave | MainViewModel recruitment hover; UnitCosts, UnitLimit and shared recruitment variants |
| TroopPanelEnter / TroopPanelLeave | MainViewModel troop-panel hover; costs/limits siege hover, Lord stance tooltip and siege reload tooltip |
| RechargeSiegeAmmo | MainViewModel.ButtonUnitRechargeRock; deferred Bugfixes replacement, cancellable before any replacement effect |
| RecruitmentMaterialUi | One validated 26-site European recruitment HUD IL transformation; OR-combined consumer predicates, stock display only |
| RecruitmentRequestPolicy | Shared concrete/Ctrl ceiling and pending reservation arithmetic |
| Economy/MarketPriceEvents | Native buy CEB10 and sell CEB90; ExtraFeatures AI default-price policy, third-party price query policies |

## Event semantics

Registration identity is owner GUID plus registration ID. Order is ascending explicit
order, then ordinal owner and ID, independent of hook installation order. Immutable
snapshots pair Pre and Post even when a callback registers another participant.
Handlers run synchronously on the actual publisher thread, without replay or a
short-lived plugin Update. Registrations and hooks live for the process lifetime;
consumers use logical activation rather than removing executable patches.

Successful vetoes are sticky. A failing Pre rolls back its input mutations and veto,
discards its private state, logs a bounded diagnostic and leaves other participants
running. Each registration receives only its own State in Post. Inputs freeze after
Pre; cross-thread and later mutation fail. Recursive calls made during notification
bypass that publisher's notifications and preserve its original call.

GameAction Accepted is read-only and runs only after every Pre, before an unvetoed
original chain. Recruitment preparation and knight Stop cleanup use this phase.
GameAction Post is a managed return/veto observation, not a native Chore execution
or soldier creation acknowledgement. A zero return is not a recruitment failure.

Presentation replacements run after all Pre with the final parameter. A later veto
suppresses both replacement and original. Post runs on veto and on an original or
replacement exception; the exception then propagates. OriginalCompleted means the
wrapped original chain returned normally without APIShared veto/replacement. It
cannot prove what an independently installed foreign detour did inside that chain.
Post cannot undo already completed effects.

Market price Pre can set ReplacementTotal and veto the arithmetic helper. A veto
without a replacement returns zero. This changes a query result, not cancellation
of the encompassing trade. Player/good/amount inputs stay unchanged; consumers must
validate IDs before table access. Policies must agree across planning, execution,
ally valuation and multiplayer peers. No raw native pointer is exposed publicly.

## Workspace inventory and retained ownership

The initial workspace search is preserved in `.inspect/APIShared-workspace-hook-inventory.txt`.
Its 601 lines include hook calls, targets, transaction helpers and diagnostics, not
601 distinct active hooks. All production mod roots, APIShared, Shared and Testmods
were included. Active projects/source links and published patch lifetime are checked
by Test-SharedBoundaries and Test-PermanentNativeRuntimePatches.

| Remaining owner | Decision |
| --- | --- |
| APIShared | Existing mission/lobby/savegame, defeat loot, gatehouse/bridge, pathfinding, marked-unit selection and HUD services already have shared ownership. Keep these services and their internal hooks. |
| BugfixesAndQoL | Formation command orchestration, work targets, MoatMove, native instruction-specific fixes, capture/repair algorithms, path routing and specialized rendering/navigation remain consumer features. Their general GameAction/HUD access now uses the shared publishers. |
| ExtraFeatures | AI repair, healers, disease, kill rewards, fear/plague, elevated moat, knight timers and session setting algorithms remain private. Share market query ownership, Stop preparation and GUI scheduling, not their settings/protocols. |
| UnitCosts / UnitLimit | Keep affordability, cost tables, cap rules, native execution/observed-unit reconciliation and UI text policy. Share command/hover/GUI interception and pure request arithmetic. |
| BuildingCosts / BuildingLimit | Keep cost/cap calculations, cache and tower-siege placement semantics. Share rollover interception. |
| CastlePlanner / AIVPlacement | Keep blueprint/editor camera, AI selection dialogs, AIV synchronization and preview transition transactions. Shared mission APIs already describe gameplay lifecycle; preview/front-end staging around StartSkirmishGame/LeaveLobby has distinct scope and cannot be replaced by a gameplay-start notification. Share the cross-cutting pause command gate. |
| ExtendedData | Keep trail/map persistence, requester callbacks, host/lord name binding and front-end setting staging. These compose existing save-data/network/lifecycle publishers; they are not interchangeable with a new generic gameplay event. |
| ExtremePowers / CheatMod | Keep Extreme HUD/settings integration and the Cheat listener for SetLocalPlayerExtremePowersEnabled. No duplicated migrated target or generic native simulation hook was found. |
| ImprovedHunters | Keep hunter state-machine fixes and diagnostics. Context-register edits at instruction interiors require specific live-register/flags/continuation contracts; exposing a generic cancellable callback would not establish a safe whole-operation veto. |
| RandomEvents | Keep native event generation, payload construction and wildlife/source rules. This is event production through audited native paths, not a common hook notification interface. |
| StartConditions / SerpsMods / SerpsModsHost / SerpPresets | No additional mod-owned overlapping interception point requiring relocation in this inventory. Existing shared/extender services remain the integration boundary. |
| Testmods | Keep experimental algorithms and native backend/diagnostic fixtures out of the production API. Permanent shared capability consumers continue using their existing public contracts. |

The retained hooks are not advertised as compatible with arbitrary independent
patches of the same instructions. Managed MonoMod chains retain foreign layers;
native occupied price entries fail closed. Third-party subscribers avoid competing
owners by using APIShared. Instruction-specific fixes require separate compatible
capabilities only once a meaningful complete operation contract is available.

## Verified baseline and compatibility

Native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2
and real Assembly-CSharp SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789
match CURRENT.json / the installed game. Installed Script Extender is 2.14.1.0.
Canonical local Fixes source: v1.26.3, commit cbabdecac15a6e81cb45404f1a3501a4856dae39.
The existing Fixes preference/AI siege findings were read; this change introduces no
new preference translation or siege algorithm. Searches found no price-helper
ownership in Fixes or the extender. The extender's GUI augmentation remains inside
the wrapped original chain, before shared Post observers.

Recruitment audit covers action producer, helper previews, MakeTroop Chore and
native execution; requests are 1/Shift 5/Ctrl 1000 and execution is asynchronous.
Stop audit covers GameAction 3F2, selection dispatcher 90510/199B50, Chore 36
serialization 12BF0 and the complete 199C70/11E960 execution branch. Shared Accepted
keeps knight cancellation before native Stop without doing it on a later veto.
All eight managed interception signatures are checked against the real installed
assembly, including four private methods invoked only through validated detours.

The full market audit covers both 31-byte helpers plus all five callers (29650,
29700,3EC90,D78C0,D7AD0). The helper ignores the player argument, reads the manager
price table indexed by good and computes signed (basePrice/5)*amount with Int32
overflow. Sell also values ally transfers. Current semantic xrefs contain zero
incoming targets into either helper interior. Exact helper bytes are verified live.
Only NativeX64 Indirect is permitted: 6 patch bytes, 10 displaced bytes (3+7), next
instruction is the 5-byte division multiplier load. Pointer slot, hook entry,
padding, relocated bytes and +10 trampoline continuation are checked after commit,
before publication. Actual installed backend SHA-256:
0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0.

## Validation

APIShared's full build passes 168 tests (88 Core, 66 installed/native contracts,
14 preset tests), compiles the public consumer and third-party example, and installs
the package through build.bat. Native fixtures exercise the productive backend,
actual ABI arithmetic, pointer-slot patch and continuation, without patching game
code. Source checks cover JSON, plugin callbacks, lifecycle, permanent hooks,
XAML single roots, CRLF, public surface and real signatures. Workspace consumers,
Fixes implementation checks, unit access and existing preset contracts pass.

Per-mod build results and installed artifact identity are recorded after completion
in the adjacent final validation report. No interactive gameplay or multiplayer
host/client run has been performed; compilation/native fixtures do not substitute
for that coverage. README files are unchanged. APIShared is 0.6.0; the seven migrated
consumers require 0.6.0, with their own versions unchanged during testing. Existing
0.5.0 changelog records are intentionally historical.
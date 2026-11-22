# Ally goods and gold transfer execution [Scope: Vanilla + mod integration]

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Inspection: 2026-11-04, installed canonical DLL, current semantic function/xref
records and complete managed ally panel/XAML. This is static evidence, not an
in-game or two-peer acceptance record. Generated `FUN_...` names remain candidate
names; the dataflow statements below are confirmed-static unless qualified.

## Entry, parameters and transport [Vanilla facts]

`HUD_AlliesPanel.GetAllyList` selects a different valid human/AI player on the
same team with nonnegative merit/Lord status. Its visible list has six slots.
`UpdateGoods` enables confirmation only for a positive amount covered by the
sender's displayed resource balance. These are UI conditions, not checks inside
the final transfer primitive.

The actual Vanilla XAML offers 21 goods: indices 2,3,4,6,7,9,10,11,12,13,14,15,
16,17,18,19,20,21,22,23,24. They correspond to the Extender `eGoods` symbols for
wood planks, hops, stone, iron, raw pitch, wheat, bread, cheese, meat, fruit, ale,
gold, flour and eight weapon/armour kinds. Wood logs, cow hides and refined pitch
are absent. The broader managed `G01..G24` switch does not expand the visible
selection contract. All amounts, including gold, use integer units without scaling.

`ButtonClicked("ConfirmGoods")` calls
`EngineInterface.GameAction(Ally_SendGoods, recipient, good, amount)` under the
public managed engine monitor. Native `DLL_GameAction` RVA `0x81870`, case 1063
(`0x427`), maps refined pitch to raw pitch for balance/capacity checks. It rejects
an insufficient sender balance; goods also require a nonnegative capacity from
`0xC5AD0`. A smaller capacity caps the queued amount, including zero. Gold uses
the current signed gold balance and no storage query. Native return 0 means local
queue submission, not successful execution.

It stages Chore 113 mode 2. The mode-dependent payload has a signed Int16 mode
and four signed Int32 values, 18 bytes total. Handler `0x1A6F0` reads mode 2 as
`(recipient, good, amount, sender)` and calls `0xD7AD0`. Player IDs are one-based.
The normal producer obtains the sender/queue issuer from the native viewpoint;
a foreign report viewpoint is not an authenticated participant identity. Chore
transport and simultaneous execute-mode delivery are described in `CHORE_SYSTEM.md`.

## Complete transfer and storage paths [Vanilla facts]

`0xD7AD0` is 585 bytes, SHA-256
`206BDE68C11D963B744BF671B185935EC32EF05CB1EBB3A479E308095CC03612`, with
ABI `void(int recipient, int good, int amount, int sender)`. Its sole direct
code caller is `0x1A907` in handler `0x1A6F0`; three additional xrefs are data
function-entry references. It performs no general alliance, defeated-player,
positive-amount or recipient-overflow validation.

Gold (`eGoods.STORED_GOLD`, 15) is a direct signed-balance transfer: the amount is
capped by sender funds, then added to recipient gold and subtracted from sender
gold. Received/sent valuation statistics are updated directly. There is no signed
overflow check and no pending incoming-gold credit. Ordinary public SE add/sub
resource events do not cover these direct gold writes.

For other goods, refined pitch (8) becomes raw pitch (7). The function calls
recipient add `0xB58B0`, then sender subtract `0xB7D80` with the original amount.
The add result is ignored; no new sender-availability or recipient-capacity clamp
is performed by this outer function. It values the gift twice via the shared
sell-price helper `0xCEB90`, whose complete 31-byte contract is signed
`(sellPrice / 5) * amount`, division before unchecked multiplication. Those two
price calls are not two gifts.

Capacity query `0xC5AD0` is 319 bytes, SHA-256
`978F663FC2E2F1FAF8CF89FE73BFD17ABD9FA728F13A57AF1325828DB9E1DDA9`, ABI
`int(buildingManager, recipient, good)`. Complete relevant callees are
`BFD30` (good-to-storage table), `C6FC0` (Armoury shared free space) and `BD470`
(Granary food capacity). No compatible storage returns -1; an existing full
storage returns 0. Armouries (`STRUCT_ARMOURY`, 0xB) share 50 places; Granaries
(`STRUCT_GRANARY`, 0x13) use 250 food places; Goods Yard stacks use the native
per-good capacity table. A native virtual-storage class (0x28) returns 1,000,000.
Its source and type must not be generalized into an arbitrary mod's storage rule.

`B58B0` rechecks total recipient capacity before mutation. It distributes Armoury
goods through `B5E90/C6EA0`, food through `B6000/BD470`, and ordinary stacks through
`B6100`. `B6100` checks building identity and local capacity, updates local/global
counts and resource visuals. `B7D80` removes goods over suitable sender storage,
updates local/global counts and visuals, and reaches `C1CE0`, which reconstructs
stored totals from living storage buildings while preserving gold. Building
owner/type/state, rather than faction, select these branches. No faction branch
occurs in the inspected transfer/capacity/allocation chain. These gifts populate
physical storage immediately, not the incoming-delivery reserve table.

Consequently, enqueue-time checks do not reserve inventory/capacity. For example,
if a 10-unit gift was queued while 10 recipient spaces were free and production
fills them before execution, `B58B0` returns false but `D7AD0` still calls the
sender subtraction for 10. Likewise simultaneous pending gifts can share stale
checks. This is a concrete statically reachable mismatch; occurrence in a live
match has not been demonstrated by this audit.

## Requests, statistics and presentation follow-through [Vanilla facts]

After accounting, `D7AD0` clears a matching requested-good/amount pair and enters
the `3F870` response path. `187E60` validates a current living Lord record;
`3F870` updates the associated AI response value and may call
`103AA0 -> 103290` for local Lord speech. The exact economic interpretation of
that response value is not promoted here. The response uses a local 20-second
speech throttle.

For a locally viewed recipient, `D7AD0` uses a local 30-second throttle and selects
the reception presentation category. `2940 -> 1A4190` appends a native presentation
event; `EDF0 -> 1A4270` prepares mono-sound playback and stores the speech filename.
These `timeGetTime` branches do not select or change transfer quantity. The local
gift message helper `1C8D0` writes ten ring entries with signed Int16 quantity
fields; quantities larger than 32767 cannot be represented as positive amounts
there. UI/AI speech and gift statistics can therefore be side effects even when
the resource add/sub effects did not balance. Statistic accumulation itself keeps
Vanilla's unchecked Int32 arithmetic.

## Shared adapter and hook evidence [mod integration]

APIShared's `Economy.AllyTransfers` uses the read-only capacity function and the
explicit-sender gift primitive inside the shared synchronous mutation scope.
Per-operation source reserve, recipient ceiling, current capacity and signed
balance headroom are checked immediately; one safe call is capped to 32767 for
the message quantity contract. This service is not a synchronization protocol.
The consumer authorizes the sender and schedules identical operations on all peers.

Corrective inspection, 2026-11-05: the earlier integration inventory missed
`BulkPlayerDetours.c_game_player_ai_send_good_hook`, whose signature starts
`40 55 56 41 54 48 83 EC` and resolves to `D7AD0`. The installed Extender assembly
SHA-256 is `5CB1C33BE5463086505A17F5C48AAA357D733CFFF7240E101C821F0E3C668498`;
the corresponding local source commit is `4014ca67c96974a5e6d697665baf5351cbfebde6`.
The first live game test refused APIShared's second hook at startup, as expected
for an occupied entry. This corrects the former no-competing-owner claim.

The loaded managed wrapper raises mutable `OnPlayerAIRequestReceivedGoods` Pre,
checks `SkipOriginalFunction`, then invokes its Original delegate using the final
recipient/good/amount/sender properties. It raises Post only after that invocation;
Post arguments are the wrapper's initial tuple, not the mutated tuple. Its Boolean
return derives from an opaque native integer and is not a resource receipt.

APIShared now adapts the single Original invocation in that managed wrapper,
without another native entry hook. Its events bracket the actual primitive after
SE Pre; an SE veto therefore produces no primitive event. Service calls use the
same wrapper and report zero-effect veto when its Original invocation is skipped.
Final redirected service arguments are rejected and service bounds are rechecked.
Both pristine native bodies remain pinned; capacity remains read-only. The native
site evidence is observational, with no APIShared-exclusive transfer entry. SE
also owns downstream B58B0/B7D80 resource hooks, which remain in the normal chain.

The primitive's implicit global addresses, record strides, table contracts,
quantity representation and local presentation are pinned by the complete native
hash. A function-prologue pattern alone cannot validate those data contracts on a
new build. The v1 service therefore fails closed for unknown hashes rather than
calling relocated old addresses. No independent service or original Vanilla gift
behavior depends on successful activation of this optional adapter.

Pre/Post here means actual gift entry/completion, not managed GameAction intent.
Post reports separately observed net sender debit and recipient credit; unknown
readbacks, nested transfers or unequal deltas are not successful-amount evidence.
Gold, SE resource-policy veto/mutation and original-call exceptions are separate
tested cases. Production backend tests execute an isolated native fixture with
the exact prologue and validate the actual 9-byte trampoline. These tests do not
establish live game/editor/save/load or real host/client acceptance.


## Chore 113 request mode [Vanilla facts]

Static follow-up on 2026-11-08, same native and managed hashes as above.
`HUD_AlliesPanel.ButtonClicked("ConfirmGoods")` in request mode calls
`EngineInterface.GameAction(Ally_RequestGoods, requestedPlayer, good, amount)`
and then issues the panel's nested `Exit` click. The requester is the implicit
native viewpoint at `DAT_1888E3D70`, and the player IDs are one-based.

Confirmed export `DLL_GameAction` at `0x81870`, case `0x428`, checks the
requester's physical capacity with `0xC5AD0` and donor availability with
`0x30290`. Missing storage, insufficient space and donor refusal each return 1.
Gold skips storage and goes through the donor check. Success submits Chore 113
through `0x23990` and returns zero. Mode 1 carries signed 32-bit
`(requestedPlayer, good - 1, amount, requester)` after the signed 16-bit mode.
The local scheduled branch uses the larger of the current tick and last
scheduled tick plus the manager delay. The ready-slot scan at `0x21EE0` checks
the due tick. Submission reserves no storage.

The statically confirmed dispatcher `0x1A6F0` calls `0xD78C0` for mode 1.
The human target branch stores good and amount in signed 16-bit fields and
can issue rate-limited local feedback. In the non-gold AI branch, `0xD78C0`
credits the requester with `0xB58B0`, then debits the donor through `0xB7D80`
and updates valuation. The credit can fail its execution-time capacity check.
The outer handler ignores its result and still debits the donor. For example,
production can consume the last eligible space while a request is queued.
The managed return and the pre-queue capacity check cannot prove a balanced
transfer. This case was verified statically and has no live reproduction in
this audit.


## Gift message production and display, 2026-11-22 [Vanilla facts]

Rechecked installed native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
installed managed SHA-256:
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Native identity also matches CURRENT.json and the queried function binaryHash.
These are confirmed-static dataflow findings, not a reproduction of a player's
match. VA = image base `0x180000000` + each RVA below. Generated function names
remain candidate names; the stated branches were checked against exports and
Iced-verified instructions. External source provenance in the semantic index is
older than the current Extender checkout; it is not used to prove current SE code.

`1A6F0` mode 2 reaches `D7AD0(recipient, good, amount, sender)` with four signed
Int32 parameters. The native payload has no automatic/manual origin label.
After the resource mutations, `D7AD0:D7BD2-D7C44` checks whether the recipient is
local viewpoint `DAT_1888E3D70`. Only then it compares unsigned
`timer[sender] < timeGetTime()`, table VA `180905B90`, stride four bytes.
The failed comparison skips receipt reaction and receipt text; it does not skip
the transfer. All goods from the same sender share this local timer. A passing
check writes a second `timeGetTime() + 30000`, i.e. thirty seconds of wall-clock
milliseconds rather than simulation ticks. It also emits presentation event16
through `2940 -> 1A4190`: subtype36 for iron/gold, subtype39 for weapon IDs17-24,
otherwise subtype35. These are event subtypes, not gift-text categories.

`D4DF0`, called by generic reset `87D10`, initializes the nine table entries to
one current `timeGetTime()` value, without adding thirty seconds. Thus the first
receipt is eligible once the clock exceeds initialization; exact millisecond
equality blocks it. This is not an initial thirty-second suppression.

Both gift-text branches require native mode `DAT_188574B90 != 0 && != 99`:

- `D7C58-D7C8E`: local recipient and the successful shared timer check enqueue
  category10 `(recipient, sender, amount, good)` through `1C8D0` at `D7C7B`,
  then call `EDF0` with `fx\speech\ally_request09.wav`.
- `D7C93-D7CB2`: local sender enqueues category15
  `(sender, recipient, amount, good)` through `1C8D0` at `D7CB2`, independently
  of that timer. This explains repeated sender rows while later recipient rows
  are not produced during the thirty-second interval. There is no queued
  recipient row waiting to be released when that interval ends.

`1C8D0` has no deduplication or timer. It writes five 16-bit fields
`(category, self, other, amount, good)` to manager `188574320 + 275A08 + index*10`,
with index at `+275A00` (VA `1887E9D20`). The Int32 amount is truncated to Int16
for subsequent signed managed consumption. The helper increments its index and
resets it to zero at ten. `DLL_RunTick` (`86680`) clears this index before native
processing and calls `19D960` after processing. The exporter copies the index
byte to output offset `899` and the five parallel ten-short arrays to byte
offsets `89C/8B0/8C4/8D8/8EC`. The decompiler's `undefined4*` pointer arithmetic
must be scaled by four when interpreting these offsets.

This buffer has a separate static overload boundary: ten writes between that
reset and export yield count zero; eleven yield only the overwritten first
record. This is not evidence that the ordinary thirty-second recipient behavior
is an overflow, and no live overload reproduction was performed here.

`EngineInterface` copies the count and fifty shorts into managed
`chat_store_data[10,5]`, preserving the field order. `FatControler.GUICheck`
consumes them only in multiplayer or skirmish mode. Case10 formats
`TEXT_SKIRMISH_MISC` text1; case15 formats text2. Both append the signed short
amount, translated goods name and other player's skirmish name, and call
`HUD_MPChatMessages.recieveIngameChat`. No origin label survives this payload or
the formatted-message arguments.

The HUD keeps up to five current rows and a five-entry replay cache. A new row
removes the oldest at capacity, receives `DateTime.UtcNow + duration` expiry
(default twenty seconds), and is enqueued without deduplication. Update expires
one oldest row per call when the chat panel is not open. Reopening the panel
copies the cache; it does not produce previously throttled native receipts.

The separate AI response `3F870 -> 103AA0 -> 103290` uses a global twenty-second
speech throttle and a ten-entry pending speech queue. Its queue and timeout do
not regulate gift-text categories10/15. `D7AD0` still clears a matching goods
request and performs the response follow-through after either text branch.

Exact source anchors, relative to this semantic directory:

- `exports/semantic-decompiled-functions.c`: lines15509 (dispatch), 126006-126084
  (transfer and message branches), 124273-124320 (timer initialization),
  79448 (reset caller), 17145-17165 (message writer), 78406 (RunTick reset),
  78490 (export caller), 237510-237525 (count/array exporter),
  1473 (2940), 239083-239093 (event16), 36627-36648 (AI response).
- `managed/BC8B6A39/decompiled/EngineInterface.cs`: lines2558-2564
  (native-layout count/short array), 5934 (count copy), 6015-6018 (array copy).
- `managed/BC8B6A39/decompiled/FatControler.cs`: lines2324-2328 (mode/count
  guard), 2357-2359 (category10), 2372-2374 (category15).
- `managed/BC8B6A39/decompiled/CrusaderDE/HUD_MPChatMessages.cs`:
  lines47-77 (enqueue/cap/expiry/cache), 79-100 (replay), 102-116 (expiry).

### Current integration boundary, 2026-11-22 [mod integration]

The local Extender checkout `7c3a252794974ee7af5cca02f446ba994fb9d22d` has
`src/SHCDESE.BepInEx/ManagedHooks/HUD_MPChatMessages_Hooks.cs:15-25`: it calls
chat handling inside a try/catch and then unconditionally invokes its trampoline.
This current source observation is distinct from the older SE provenance above.
The installed SE assembly SHA-256 is
`E1B73D1E64E809EC51B9F2540F8F5DBF5357B5970492C688E9A8A7A8283E7D37`.
The coordinating reviewer checked its actual IL for this unconditional call;
compact evidence is `.work/a/autosend-interval/001/installed-chat-hook.txt` and
`chat-hook-proof.json` relative to the workspace root. This identity supersedes
the older integration snapshot only for this dated observation.
The chat event is observational, not a text-cancellation API;
no already-implemented source-specific suppression service is claimed here.

Source attribution is precise only while an actual owned primitive invocation is
identified; an ambient service frame alone also spans SE Pre/Post and nested
foreign calls. Category, sender, recipient, amount or formatted text alone cannot
prove an automated origin. Suppressing only such an attributed text would leave
resource effects, the recipient's shared thirty-second timer, receipt speech and
AI response unchanged. This audit supplies the boundary, not a suppression design
or a newly installed hook.


## Native chat gate versus mission-mode domains, 2026-11-22 [Vanilla facts]

Follow-up inspection used the same freshly verified installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
and managed SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
CURRENT.json and the queried function binary_hash match. Complete `D7AD0`
(585 bytes, SHA-256 `206BDE68C11D963B744BF671B185935EC32EF05CB1EBB3A479E308095CC03612`)
was re-read through the query interface and independently disassembled with
Capstone from the installed PE. Findings below are confirmed-static. They do not
identify the actual unlogged gate value of a historical test or player session.

Several native mode domains are independent. All addresses below are RVAs,
relative to the loaded DLL; the canonical preferred image base is `180000000`.

| RVA | Domain and source |
| --- | --- |
| `3665F28` | Game type, SE `GetCurrentGameTypeMode`; `eGameTypeModes.GAMETYPE_MULTIPLAYER` = 3. This value alone does not distinguish singleplayer skirmish from network multiplayer and does not enable gift-chat rows. |
| `3669040` | Skirmish origin, SE `GetCurrentSkirmishGameMode`; `eSkirmishGameMode` Custom0, Trail1, CustomTrail2, TestMission3, NotSkirmish-1. |
| `88E6A04` | Skirmish rules mode, SE `GetCurrentSkirmishMode`; `SkirmishMode` Unknown0, Normal1, Crusader2, Deathmatch3. |
| `8574B90` | Separate native gate read by both gift-chat branches, manager `8574320 + 870`. The transfer skips both row producers when this Int32 equals 0 or 99. It must not be interpreted through any of the three enums above. |

These SE field mappings were independently resolved against the canonical image
using the current checkout's anchors in
`shcde-script-extender/src/SHCDESE.BepInEx/GameGlobals/GameGlobalsManager.cs:1452-1493`:
GameType anchor `4078C`, SkirmishGameMode anchor `28C5A`, and
GameSkirmishMode anchor `D14E0`. Their RIP-relative destinations are the three
RVAs in the table. The SE enum declarations describe the enum values; native
machine destinations establish that these are different fields.

Native `DLL_PreInitMap_Multiplayer` (`86270`) resets managers, then sets the chat
gate to 99 for `skirmishGame=true`, or 1 for false. This is also visible in machine
code: `86352: 40 84 F6` tests the saved Boolean; `8635F: B8 63 00 00 00`
selects 99; `862CE: 41 BE 01 00 00 00` prepares R14D=1;
`8636E: 41 0F 44 C6` selects that register when false;
`86377: 89 05 13 E8 4E 08` writes the result to `8574B90`.
Managed fresh-SP creation passes true in `FRONT_Multiplayer.cs:9963/9983`;
network creation passes false in `Platform_Multiplayer.StartGame:3923`.
A fresh local skirmish can therefore have GameType3 while Vanilla generates
neither gift-chat category10 nor category15. This is a producer gate, not HUD
deduplication, a deferred receipt, or evidence of failed resource transfer.

Reset `87D10` and `8ADC0` write gate0; manager reset `211A0` writes manager+870=0
and independently sets GameType `3665F28` to3. The map-load function `94350`
captures the old gate in `local_df8` and restores that exact value after loading;
it does not normalize a fresh-SP gate99 to1. `DLL_StartMultiplayerGame87880`
and `DLL_StartMultiplayerGameSynced878B0` do not change this gate. Other observed
lifecycle paths include explicit restoration of99 for local-skirmish saves;
there is no basis to infer a network gate merely from the mission's GameType.

The text-producing instructions confirm the physical layout:
`D7C49: 8B 05 41 CF 49 08` reads `8574B90`, followed by
`85 C0; 74 64; 83 F8 63; 74 5F` to skip both branches for0/99.
`D7C93: 3B 2D D7 C0 80 08` compares the sender with viewpoint `88E3D70`;
the sender branch has no receipt timer. Both branches use R13 as the chat manager:
its gold-path LEA at `D7B1A` and goods-path LEA at `D7BA9` resolve to `8574320`.
Consequently index `8574320 + 275A00 = 87E9D20` and records at index+8 are
unchanged. Calls `D7C7B: E8 50 4C F4 FF` and `D7CB2: E8 19 4C F4 FF`
reach writer `1C8D0`, giving physical returns `D7C80` and `D7CB7`.

`DLL_SetEditorPlayer872E0`, for ordinary player IDs<=1000, writes viewpoint
`88E3D70` at `872FC: 89 0D 6E CA 85 08`. Its additional alliance-field writes
occur only when GameType `3665F28` equals1. It does not change chat gate8574B90.
Borrowing the viewpoint in GameType3 therefore cannot make SP gate99 emit rows.
IDs>1000 follow a different editor-selection branch and do not set this viewpoint.

Exact anchors in `exports/semantic-decompiled-functions.c`: lines78104-78134
(PreInit), 79428 (reset), 81363-81370 (alternate reset), 19482-19496 (manager
reset and GameType), 86525/86849 (map-load capture/restore), 79109-79149
(multiplayer starts), 78820-78847 (SetEditorPlayer), 126044-126077 (gift gates).
Managed paths are in `managed/BC8B6A39/decompiled/CrusaderDE/FRONT_Multiplayer.cs`
and `managed/BC8B6A39/decompiled/Platform_Multiplayer.cs` at the lines above.

A silent-transfer test with no rows must have a positive Vanilla-chat control
under the same native gate before it can prove suppression. A confirmed resource
receipt alone is insufficient. No native gate writes, new hooks, production
changes or new live acceptance results are established by this follow-up.

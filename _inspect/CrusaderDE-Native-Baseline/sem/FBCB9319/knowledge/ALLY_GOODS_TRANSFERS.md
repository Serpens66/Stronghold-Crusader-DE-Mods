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

The shared native execution broker owns only `D7AD0..D7AD9`: `40 55` (push rbp),
`56` (push rsi), `41 54` (push r12), `48 83 EC 40` (sub rsp,40h). NativeX64 Indirect
requires at least six bytes and rounds to these nine complete bytes. There is no
RIP-relative operand or branch inside the displaced span. The complete current
xref dataset has no target in its interior; direct calls and data references hit
the entry itself. The body contains no branch into the displaced interior. Exact
backend validation covers scheme, 9-byte displacement, target, pointer slot,
trampoline, patch padding and continuation `D7AD9`.

Actual installed-backend fixture execution on 2026-11-04 showed relocated bytes
`55 56 41 54 48 83 EC 40`, followed by the return jump: Iced removes the redundant
REX `40` prefix from `push rbp`. The overwritten source span is still nine bytes;
the relocated four-instruction span is eight. The shared validator accepts only
the exact original nine bytes or this exact normalized eight-byte equivalent,
then decodes the following jump and requires original continuation `D7AD9`.

Full pristine/live bytes of both functions are verified before publication.
Capacity remains unpatched; the registry records it and the transfer continuation
as Observe evidence and the displaced transfer entry as Exclusive. The current
SE/Fixes/APIShared inventory contained no competing owner at D7AD0/C5AD0. SE owns
downstream B58B0/B7D80 resource hooks, which remain in the normal call chain.

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

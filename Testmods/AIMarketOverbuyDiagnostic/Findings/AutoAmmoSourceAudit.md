# Automatic stone-ammunition demand, 2026-11-22

Native hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Complete relevant bodies: D5710, D55B0, 1520D0, 1535F0, 18FC00 and18AE0 in
CURRENT's `exports/semantic-decompiled-functions.c`.

D5710 consumes ten stone atomically and adds twenty to the native signed16
ammunition word. With fewer than ten stone it may assign pending stone10.
This is a resource requirement, not an AIC batch. D55B0 can manually spend fewer
stone and add twice the spent amount, or fill the word to20 with sufficient stock.
Treating the automatic source as a divisible twenty-round delivery credit is
wrong: source stock9/ammo0, manual refill9 gives ammo18/stock0; buying the apparent
remaining stone1 cannot be consumed by automatic D5710's ten-stone branch.

All five ordinary automatic call sites are dominated by signed ammunition<=0:

| Call RVA | Return RVA | Caller |
| --- | --- | --- |
| 153052 | 153057 | 1520D0 |
| 153151 | 153156 | 1520D0 |
| 1542C1 | 1542C6 | 1535F0 |
| 1543C2 | 1543C7 | 1535F0 |
| 18FD85 | 18FD8A | 18FC00 |

18AE0 instead tail-jumps at18BB5 without an empty-ammunition guard. A source's
current empty ammo alone therefore does not prove automatic origin. At D5791,
RBX retains the unit's one-based game ID; the D5710 caller return address is at
original RSP+38h (push RDI plus sub RSP,30h). The relay frame is128 bytes below
that original RSP. The adapter admits only the five proven return addresses.
Initialization checks those direct CALL targets, the D5710 stack prolog and
MOVSXD RBX,EDX, plus the actual installed SDK field layout.

`MarketAmmoDemandSource` stores only copied UnitId/GlobalId/CreationTick/Owner.
The owner supplies session/source epochs. Immediate live lookup uses public
`APIShared.UnitAccess.TryGetById`; pointers are not cached. Global/creation reuse,
owner changes, nonliving state or the native death marker end the old goal.
Unreadable/unavailable lookup and invalid stock yield Unknown and authorize no
write. Neutral owner0 and manual/unknown sources cannot create ownership.

For the same living unit, native ammo>0 ends its old empty trigger. Otherwise
the desired purchase is max(0,10-currentStone). Thus manual refill9 cancels the
old automatic request; after the unit later fires empty, a fresh atomic reload
can legitimately require ten stone again. No ammo-delivery credits or extra
ammo-write hooks are used.

The ammunition word at GameUnit+35E spans the SDK's StoneAmmoLeft and
StoneAmmoStacksLeft byte fields. Reading only the low byte would misclassify
word256 as empty. Layout also checks Alive88, Owner92, GlobalId94, Creation98
and native death-word29C. Actual installed SHCDESE metadata exposes these fields
publicly; no publicized Assembly-CSharp member access is involved.

`AmmoDemandSourceTests` exercises the production reader with a real GameUnit
layout, all caller/frame cases, high ammo byte, nine-stone deficit, partial refill,
fresh atomic reload, dead/corpse/reused/transferred units and fail-closed changed
call bytes. It runs within TradeEntries.Tests against installed references.
The existing native relay fixtures additionally execute the B8122 loss store,
checking old stock23 and planned zero before replay, both gates/stack alignments,
exact operand addressing, unchanged GPR/flags/SSE and one continuation.
These are offline checks; root-owned hidden game tests provide integration proof.

# Siege ammunition storage and signed consumers [Scope: Vanilla]

## Identity and evidence

Audited 2026-11-14 against the installed `CrusaderDE.dll`, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Addresses below are RVAs in that exact image. Evidence is the semantic export
`../exports/semantic-decompiled-functions.c` and direct x64 decoding of the
installed image with PE section mapping and Capstone. These are static facts;
this audit does not establish an in-game or multiplayer acceptance result.

## Storage, consumption and refill

The Catapult and Trebuchet update functions read their stone ammunition as one
16-bit word at the native unit-record origin plus `0x9BA`. Relative to the
Script Extender's typed `GameUnit` origin (`native origin + 0x65C`), this is
`+0x35E`: the adjacent `r_StoneAmmoLeft` and `r_StoneAmmoStacks` bytes.
Splitting the word into two bytes does not change how its consumers compare it.

| Function | Read and comparison | Positive branch |
| --- | --- | --- |
| Catapult update `0x1520D0` | `0x153111 MOVZX EAX, word [RCX+R13+0x9BA]`; `0x15311A TEST AX,AX`; `0x15311D JLE 0x15312D` | `0x15311F DEC AX`, store at `0x153122`, continue at `0x15315C` |
| Trebuchet update `0x1535F0`, first firing branch | `0x1541CE MOVZX EAX, word [RDX+R11+0x9BA]`; `0x1541D7 TEST AX,AX`; `0x1541DA JLE 0x154293` | decrement at `0x1541E0`, store at `0x1541E9` |
| Trebuchet update `0x1535F0`, second firing branch | `0x154375 MOVZX EAX, word [RCX+R11+0x9BA]`; `0x15437E TEST AX,AX`; `0x154381 JLE 0x15439E` | decrement at `0x154383`, store at `0x154386` |

`JLE` after the 16-bit `TEST` is a signed comparison. Consequently the positive
finite supply range is `1..32767`; `0` is empty, and raw words `32768..65535`
are negative to these consumers. Zero-extension into EAX does not make the
subsequent AX comparison unsigned. The nonpositive branches include distinct
AI handling; they must not be generalized into a claim that every AI siege
unit becomes unable to fire.

The ordinary stone-refill function `0xD55B0` also reads the word signed:
`0xD5649 MOV EBX,20`, `0xD5651 MOVSX EAX,word [RDI]`,
`0xD5654 SUB EBX,EAX`, followed by the nonpositive-needed branch at `0xD5658`.
Its target supply is 20, not the full unsigned storage range.

The important distinction is between the 16-bit storage capacity and the
positive range accepted by the firing consumers. A raw increment from 32760
by 20 produces 32780 (`-32756` when interpreted as signed 16-bit). Merely
saturating at 65535 cannot repair that invalid finite supply.

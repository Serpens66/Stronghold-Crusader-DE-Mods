# Save writer routes and synchronized saves [Scope: Vanilla]

Audited 2026-10-31 against canonical native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
and managed SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Function evidence is available through the semantic `function`/`callers` queries;
the internal function names remain candidate names, not public call contracts.

## Native routes

- Confirmed export `DLL_SaveSaveGame`, RVA `0x86F80`, sets the output filename
  through `0xA850` and reaches common writer `0x52B0`. The export's `mapSave`
  flag distinguishes the ordinary game-save/map-export call.
- The complete 1102-byte function at `0x13030` handles synchronized Chore 39.
  Phase 1 serializes screen coordinates, the autosave flag and wide filename;
  phase 0 reads them, chooses `autosave.msv`, numbered `autosave%d.msv` or the
  supplied filename, sets the native output filename and calls `0x52B0`
  synchronously. It has no managed arguments and returns void. It does not call
  the `DLL_SaveSaveGame` export. The autosave scheduler at `0x1F6E0` queues
  Chore `0x27` through `0x23990` after its time/mode checks.
- Confirmed export `DLL_CreateTrailMission`, RVA `0x816B0`, also calls `0x52B0`
  after choosing its output filename/restart data. Both this template route and
  the synchronized save route pass zero as the writer's third argument; that
  value alone cannot classify gameplay saves.
- Current-image callers of writer `0x52B0` are `0x13030`, `0x816B0`, `0x86F80`.
  The writer allocates buffers, serializes native header/sections, appends data
  before file close (boundary `0x5A4C`) and returns success; allocation/open
  failures return failure. Reaching the append boundary alone is not proof of
  a completed file write.

## Synchronized entry boundary

`0x13030`: `mov [rsp+8],rbx` (5 bytes), `push rdi` (1 byte).
The next instruction at `0x13036` is `mov eax,0x1D40`, followed by
`call 0x1A6990` (`chkstk`) and `sub rsp,rax`. All direct branches/calls in
the function and current-image xrefs were checked: none target the interior
of `(0x13030,0x13036)`. This observation is tied to the image hash; it does
not establish a hook-backend or unknown-version compatibility contract.

## Managed Lord identities

Vanilla `FRONT_Multiplayer.MPAIVInfo` can retain an empty `lordName` with
`builtInLord=false` after selecting a custom AIC for an Extended Lord. The
configuration's `lordType` identifies that Extended Lord; the lobby slot's
`lordType` can describe a different selection role. `RestartMPInfo` encodes
`LordNames` and `LordType`, but the inspected MP writer only updates names;
the type array is not a reliable AIC configuration identity. Optional media
names do not independently prove configuration identity.

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

## Native write completion

Rechecked 2026-11-10 in the native image bound to the hash above. At
`0x52B0` the binary CRT writes reach `WriteFile` through
`0x1DDB94` → `0x1DDC2C` → `0x1DDD4C`. The writer ignores the
write and close results after a successful open and returns 1 even if
the resulting file is shorter than declared. The current writer emits a
4036-byte section directory including its tag. The word after the tag
declares the size of the following native payload. The Script Extender
can append a ZIP after that payload before the file is closed.

## Managed save gate and Trail setup strings

Checked 2026-11-10 in the decompilation bound to the managed hash above.
`EngineInterface.SaveSaveGame` calls `Director.SafeToSave(wait: true)` before
setting the wait cursor or entering the native writer. If queued frames never
reach zero outside the engine, the gate gives up after 100 sleeps of 10 ms.
The wrapper then returns `false` without attempting to open the output file.
A native write failure can also return `false`, so the Boolean alone does not
identify why saving failed.

`MultiplayerSetupData.FromString` reads the first field as either a negative
version prefix or nonnegative fairness in the original unversioned format.
Starting speed and autosave are respectively one and six fields after
fairness, counting from zero. `ToString` writes a negative version prefix.

## Managed Lord identities

Vanilla `FRONT_Multiplayer.MPAIVInfo` can retain an empty `lordName` with
`builtInLord=false` after selecting a custom AIC for an Extended Lord. The
configuration's `lordType` identifies that Extended Lord; the lobby slot's
`lordType` can describe a different selection role. `RestartMPInfo` encodes
`LordNames` and `LordType`, but the inspected MP writer only updates names;
the type array is not a reliable AIC configuration identity. Optional media
names do not independently prove configuration identity.

## Multiplayer autosave interval and save preamble

Rechecked 2026-11-20 against the same native and managed hashes. The managed
multiplayer start applies the live setup after initialization.
`DLL_ApplyMultiplayerSetupData` (`0x81050`) calls `0x26370`, which copies
transfer DWORD 6 (`autosave`, minutes) to network-state offset `+0x279F98`.
For the network-state instance at RVA `0x8574320`, that is RVA `0x87EE2B8`.

`DLL_RunTick` (`0x86680`) calls the autosave scheduler at `0x1F6E0`.
After its mode, host and state gates pass, the scheduler compares the
unsigned elapsed `timeGetTime()` milliseconds with the stored interval
times 60,000. It updates the timer and queues synchronized Chore 39 when
elapsed time exceeds the threshold. The Chore dispatch and the eventual
file write are separate events.

The writer at `0x52B0` stores the same interval DWORD at payload offset 8
of the sixth length-prefixed preamble block, following the initial magic.
The loader reads that DWORD back into the same native global. This records
the active value at save time, without recording the history of lobby edits.

### Multiplayer lobby draft and save notification

The Vanilla multiplayer Setup dialog initializes `MPTEMPsetupData` from
`MPsetupData.ToString()`. With `coopGame` true and `singlePlayerCoop` false,
`Settings_Autosave` cycles the value on `MPsetupData` and displays that value.
`ApplySettings` then imports `MPTEMPsetupData.ToString()` into `MPsetupData`,
including its original interval. The dedicated Coop options panel instead
uses `CloseCoopOptions`, which publishes the live setup with `UpdateHostInfo`.

`Platform_Multiplayer` displays the localized saving message when processing
Chore 39 for outgoing or incoming transport. That notification precedes
the scheduled phase-0 save. Timed autosave and manual `DLL_TriggerMPSave`
(`0x87940`) enqueue the same opcode via `0x23990` with flags 1 and 0
respectively. The autosave directory comes from `GetMpAutoSavePath` through
`DLL_SetPath` (`0x876F0`) and `0x26BE0`. The handler at `0x13030` selects
that directory and the autosave basename for flag 1. For flag 0 it selects
the normal save directory and the supplied filename. Both paths converge
at writer `0x52B0`. The Chore handler discards the writer's return value
and advances the native save handshake even when the write returned failure.

### Native pause and the pending autosave

Rechecked 2026-11-20 against the same native and managed hashes. The
managed `EngineInterface.run` passes `Director.Paused` to `DLL_RunTick`.
This argument is separate from the native pause DWORD at RVA `0x366A0F8`,
which `DLL_GameAction` (`0x81870`) writes for `Game_Paused`.
`DLL_RunTick` calls the autosave scheduler `0x1F6E0` before the managed
pause branch. The scheduler does not check the native pause DWORD.
It stamps its deadline and submits Chore 39 to `0x23990` on expiry.

In the normal live network path, `0x23990` assigns a future simulation
tick and serializes the command for transport. The pending-slot selector
`0x21EE0` compares the scheduled tick to the current simulation tick,
and `0x1F7E0` invokes eligible Chores in phase 0. The simulation updater
`0xCDE60` increments the current tick only when the native pause DWORD is
zero and its other update gates pass. Consequently pause can leave an
already announced save queued until the simulation reaches the target
tick after unpause. The announcement does not imply phase-0 execution.
This is a static path audit. It does not establish a successful post-pause
write in any particular session.

## Camera serialization and presentation readiness (26 November 2026)

Rechecked on the native/managed identities at the top of this document. Complete86F80 stores screenCentreX/Y at native globals3A11E58/3A11E5C and separately stores realScreenCentreX/Y at38722A0/38722A4 before52B0. These are distinct presentation quantities. Complete876D0 only records the requested rotation/centre and raises the pending flag; its return does not prove an applied rendered camera.

The managed Vanilla SaveSaveGameOrMap path supplies actual Screen.width/64/zoom and Screen.height/16/zoom to getScreenXYForSaveCentring. That helper quantizes in three-unit and32/16-pixel steps; passing1,1 is not an equivalent camera-save recipe. getRotationCentre derives the currently displayed centre through CalcMapTileFromMousePos. The native save Boolean still has the write-completion limits documented above.

EditorDirector.postLoading resets zoom and requests a forced map rotation. GameMap.processTestMap applies SetCameraFromGameState; after rotateHappened it finishes the pending rotation and reapplies the camera before publishing lastGameState. Fresh PlayState availability alone is therefore not a promise of completed rotation. Even after pendingRotationtriggered and midZoom are clear, CameraControls2D.Update can apply boundsFixCamera on a later Unity frame with zero movement axes, using the current orthographic size, zoom, UI bounds and rotation. Resolution/zoom updates can also change orthographic size before this clamp.

Measurement consequence [test policy]: use Vanilla save arguments, wait for actual load/rotation/zoom presentation, exclude a genuine advancing-tick warmup, and establish the first measured loaded camera as the explicit comparison reference. Verify every subsequent identical-save load against it at the same post-warmup boundary and keep camera/zoom unchanged throughout each measured window. Preserve pre-save and early-load values separately. This is a measurement contract, not permission to write camera/native state or weaken gameplay save validation. Rescue runs123/125 exposed the old fixture assumptions before any valid timing window; run127 uses the corrected presentation boundary.

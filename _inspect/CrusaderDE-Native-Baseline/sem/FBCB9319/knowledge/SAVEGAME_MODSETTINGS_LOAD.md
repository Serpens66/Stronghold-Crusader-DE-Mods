# Savegame settings load boundaries [Scope: Vanilla + mod integration]

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed SHA-256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Installed Script Extender: 2.14.1; source provenance in the current database manifest.

## Audited flow

- Multiplayer confirmation occurs inside an existing lobby. HostLoadGame publishes
  the selected save basename; FRONT_Multiplayer then calls StartSave on each peer.
- StartSave calls initMultiplayerGame and loadMultiplayerMap with the save flag set.
  DLL_PreInitMap_Multiplayer (RVA 0x86270, confirmed export) initializes the multiplayer
  family; DLL_LoadMapToPlay (0x84AC0, confirmed export) passes the save byte unchanged
  to the skirmish initializer (0x94350, internal name still candidate confidence).
- The initializer reads the file through the existing reader and its save branch
  returns 0xFFFFFFFF after restoring state, bypassing ordinary new-game construction.
  The enclosing loader fills LoadMapReturnData. A zero reader result is failure.
- Local saves, including local Coop, instead use DLL_LoadSaveGame (0x853F0,
  confirmed export), whose successful return and output errorCode are both 1.
- The host posts the random seed and completes managed postLoading. Clients complete
  immediately with an already received seed or later through packet 2. Packet 3 is
  a Vanilla loading acknowledgement, not a modsettings fingerprint. The host's
  monitor later sends packet 4 and starts simulation; the current roster can have
  no remote humans. Original player remapping remains authoritative.
- Coop frontend/provider trail IDs are 0..3; native IDs are 1..4. Frontend mission
  IDs are decremented once by CoopMissionChanged. Save retData is authoritative
  for the actual loaded mission; the selected lobby mission is not a substitute.

## Managed mismatch and ownership [mod integration]

The installed Extender OnLoadMap sets its archive IsSaveFile to false even with
MultiplayerSave set. Its LoadContext therefore cannot by itself distinguish a
multiplayer save from a map. APIShared previously rejected its settings entry at
that flag check. Confirmed managed save entries now read the existing appended
metadata independently; no extra native hook or memory patch is required.

APIShared metadata is written only by the multiplayer host. A client save can
legitimately lack that entry. Vanilla matches save names plus its play checksum/time
evidence; whole-file SHA equality between different peers is not established.

## Managed requester timing (2026-10-19 audit) [mod integration]

The installed native and managed hashes were rechecked against the identities above.
The real public `HUD_LoadSaveRequester.OpenLoadSaveRequester` selects the visible
requester instance and opens it with confirmation/cancellation callbacks. Selection
updates `selectedHeader`; double-click routes through public `ButtonClicked(1, true)`.
`ButtonClicked(1, ...)` hides all three requester surfaces **before** `RunOKAction`
invokes the confirmation callback. Reading `IsVisible` only in that callback therefore
cannot establish whether the user was offered an option. Capture visibility/choice
before the original ButtonClicked executes, retaining the file-specific decision.

Editor overwrite confirmation defers RunOKAction until its popup is confirmed;
declining the overwrite restores the requester without confirming the save.
`CloseRequester` hides the surfaces and invokes CancelAction. RunOKAction returning
only establishes that its delegate returned: asynchronous host-load admission and
mission initialization still have their existing independent completion boundaries.
No native load flag, player remapping or archive identifier changes are required
to move the requester owner. APIShared's save-contract validator checks the public
requester methods and accessed managed fields against the installed assembly.

## Validation limits

Managed follow-up, 20.10.2026: both installed hashes remain unchanged. The private,
instance `HUD_LoadSaveRequester.RunOKAction(): void` invokes `OKAction` and **then**
updates remembered map/save names and `GameData.currentFileName`, reading the
current requester selection/name again. Editor overwrite executes it later from
the popup callback. Serializing only ButtonClicked or the consumer delegate does
not enclose this delayed caller's bookkeeping. APIShared therefore also encloses
the validated private RunOKAction entry with a permanent managed hook; nested
requester opens wait until that enclosing operation and attempt cleanup return.
The contract validator checks its real visibility/signature; no publicized direct
call or additional native offset is used.

Source, installed IL and the matching native export were inspected. This establishes
the routing mismatch, not a reproduced multiplayer desynchronization. Automated
readiness, serialization, mode reconciliation and compiled admission checks are
separate from real Solo-Multiplayer, host/client and Coop game acceptance.

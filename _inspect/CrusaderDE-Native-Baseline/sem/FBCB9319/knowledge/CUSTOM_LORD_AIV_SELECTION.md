# Custom Lord initial selection and failed dialog refresh [Scope: Vanilla + mod integration]

Static audit dated 2026-11-07. The installed binaries were read, not changed:

- `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- `Assembly-CSharp.dll` SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
- Managed MVID: `29ade4ef-f587-422f-a1eb-f81dff066e0a`.

Managed source links below refer to the hash-bound Vanilla export under
`managed/BC8B6A39/decompiled`. They describe this assembly, not a future game version.
The native import export at RVA `0x84850` / VA `0x180084850` is `confirmed` in the
current semantic dataset. The failure discussed here occurs before that native call.

## Identity, package lists and initial selection [Vanilla facts]

[`CustomisationFileManager.BuildCustomLordDirectory`](../managed/BC8B6A39/decompiled/CustomisationFileManager.cs#L622)
uses the directory name for a local Lord and `<Workshop-ID>\<directory-name>` for a
Workshop Lord. It enumerates that directory's files, appends successfully parsed AIVs
and configurations, and registers the Lord only when both lists are nonempty.
An AIV filename need not equal the Lord name. `getLordAIVList(-1, lordName)` and
`getLordLordList(-1, lordName)` resolve the named Lord's own lists (lines 936 and 953).

[`FRONT_Multiplayer`'s `AddCustomLord` branch](../managed/BC8B6A39/decompiled/CrusaderDE/FRONT_Multiplayer.cs#L5317)
initializes the selected slot, assigns `customLord.configs[0]` and adds exactly
`customLord.aivs[0]`. `MPAIVInfo.Init` (line 37) sets rotation 0 and, for a nonempty
Lord name, sets both `builtIn` and `builtInLord` false. The fresh Custom Lord default
therefore means the first package castle and first package AIC, not another built-in
Lord's data. Do not independently sort the lists when reproducing that default.

[`MPLobbyMember.SetValidCustomLordType`](../managed/BC8B6A39/decompiled/Platform_Multiplayer.cs#L975)
encodes `29 + slotIndex` into the synthetic member ID; `GetLordType` (line 937)
decodes the type. That number is slot-dependent, whereas the named package identity
is separate. A stored numerical type is not sufficient identity for a named Lord.

## Empty selection and partial dialog initialization [Vanilla facts]

[`FRONT_Multiplayer_AISettings.Init`](../managed/BC8B6A39/decompiled/CrusaderDE/FRONT_Multiplayer_AISettings.cs#L155)
changes an empty explicit selection to `builtIn=true`, including a named Custom Lord.
It then assigns the heading and loads the available lists by `lordName` when nonempty;
the numerical Lord type does not choose a different package in that branch.

[`populateList`](../managed/BC8B6A39/decompiled/CrusaderDE/FRONT_Multiplayer_AISettings.cs#L211)
assigns the current `AIVInfo`, clears its private row collections, and disables the
default controls for types at least 29. Those disabled controls do not undo an already
set `builtIn` flag. The `builtIn` branch (lines 264-277) clears the selected AIVs and
unconditionally adds `aivList[0]` through `aivList[7]` when `doPopulate` is true.
With a two-castle Custom Lord, the third lookup throws `ArgumentOutOfRangeException`
after two of that Lord's own castles have already been added. The assignment
`RefFileLists.ItemsSource = fileRows` occurs only at line 386, after this failing block.

Thus this branch can leave a changed heading/current selection alongside the old
visible source. This is a control-flow fact, not evidence that the Lord's package lists
or persisted file references were cross-assigned.

## Default upload failure [Vanilla facts]

[`FRONT_Multiplayer.StartSkirmishGame`](../managed/BC8B6A39/decompiled/CrusaderDE/FRONT_Multiplayer.cs#L10008)
uses `UploadDefaultAIV` when the selection has `builtIn=true` or zero candidates.
Its Coop Trail branch also uses that default upload directly. Explicit custom
candidates otherwise use bank `playerId - 1`, sequential candidate indices and
`custom=1`; the selected custom AIC is applied independently afterwards.

[`AIVLoader.getAIVData`](../managed/BC8B6A39/decompiled/AIVLoader.cs#L50479)
handles built-in types 0 through 28 and returns null for a Custom Lord type. Its
[`UploadDefaultAIV`](../managed/BC8B6A39/decompiled/AIVLoader.cs#L50676) passes that
result to `EngineInterface.ImportAIV`. The
[`managed wrapper`](../managed/BC8B6A39/decompiled/EngineInterface.cs#L3999) reads
`data.Length` while preparing the native call, so null produces a managed
`NullReferenceException` before `DLL_ImportAIV` executes. This is not a native
importer failure or evidence of a malformed castle payload.

## Remembered selection and stale enhanced rows [mod integration]

The released modpack source retained at `.work/b/modpack-main/001` identifies commit
`6f461700f6187c54afb12449c9229f7ed1665dfa`. In that source:

- `AiAivSelectionCodec` lines 136-156 converts an unresolved/empty explicit selection
  into the built-in castle/AIC fallback, including named Custom Lords. `Capture`
  lines 208-215 can serialize an empty Custom Lord selection as `BuiltIn=true`.
- `AiCastleSettingsListEnhancementHook.RefreshEnhancedLists` binds its own new row
  collection at line 903; the enhanced selected-castle footer refreshes at line 932.
  `ShowHook` invokes Vanilla at line 350 before either refresh.

If Vanilla throws in the two-castle case above, the post-show refresh never runs.
Previously bound enhanced rows and footer entries can therefore remain visible,
giving the appearance that the newly opened Lord selected the previous Lord's
castles. Repair must happen before the original dialog initialization; cleaning up
only at game start does not prevent this UI failure.

## Supplied evidence and limits [mod integration]

The user-supplied `AiAivSelectionMemory.json` inspected on 2026-11-07 has SHA-256
`05E277DC1B9936DBD1E861C4E43A4CF88A510E5A0E5FABCF867162C659B06530`.
Its Bishop entry (`3816486986\The Bishop`) contains two Bishop AIV references but
`BuiltIn=true`, `BuiltInLord=true` and no AIC. Its Champion entry
(`3816480454\The Champion`) contains two Champion AIV references, explicit castles,
`BuiltInLord=true` and no AIC. No Bishop/Champion cross-assignment of persisted AIV
references is established by these entries.

The screenshot supplied in the chat shows the Bishop heading and Champion rows in
both available and selected lists, with Standard castle/AIC modes. This is consistent
with the stale-row path above, but does not by itself prove the earlier initialization
exception. The supplied `LogOutput.log` (SHA-256
`5F80D6BDB9E670E8EC2BC0D4DE379FC83CC407E78B985644D697BACCEB3C9F39`)
contains no Error/Fatal record or `ArgumentOutOfRangeException` for that dialog
action. The earlier `message.txt` stack establishes the null default-upload failure,
not the first writer of the invalid flags. No deleted or renamed user file is assumed.

Confidence is `confirmed` for the inspected static state/control flow and decoded
snapshot values. The screenshot's exact live callback sequence and the original
writer of its invalid flags remain unverified. No in-game or host/client acceptance
is claimed by this document; runtime changes and their tests are tracked separately.

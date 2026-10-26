# Trail Maker custom AIC export and confirmation overlay

Verified 2026-10-08. Native baseline FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Installed Assembly-CSharp SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789 matches managed/BC8B6A39/decompiled.
The contracts below are managed; no native RVA or new detour is involved.

- FRONT_Multiplayer.MPAIVInfo.Init sets lordName to the supplied name. A standard Lord uses an empty name.
- FRONT_Multiplayer_AISettings.Init obtains standard Lord configs/AIVs through getLordLordList(lordType) and getLordAIVList(lordType) when lordName is empty. LordUser sets builtInLord=false without assigning lordName; config selection assigns lordConfig.
- HUD_IngameMenu.RestartSkirmishMapInfo.importAIVs preserves this state. encode/decode preserves the empty name and custom config; CustomLordConfig.encode/decode does not serialize the source path. An empty name with builtInLord=false and a valid standard Lord type is legitimate.
- CustomisationFileManager.BuildExtendedLordDirectory assigns types by directory name, creates built-in AIV checksum entries and loads .lordjson configs. ConfigSettings.extendedLordPaths maps zero-based type indexes to directory names. Named CustomLords use their own name, including the Workshop prefix; their identity must not be inferred from the standard Lord type.
- FRONT_ManageTrail DoExport uses ShowConfirmationMessage(..., MPConf:true). FRONT_Multiplayer.xaml binds the visible popup overlay to Show_HUD_ConfirmationMP.
- HUD_ConfirmationPopup.ConfirmationClicked clears the visibility flags before invoking the confirmation action. The action exports, rescans the folder and closes Show_TM_Export; it does not subsequently clear the confirmation flags.
- ShowConfirmationOKMessage configures every popup instance but enables only Show_HUD_Confirmation. For Trail Maker export errors, use the already established BugfixesAndQoL ShowMultiplayerConfirmationOkMessage pattern: configure the OK message, disable the ordinary overlay, enable Show_HUD_ConfirmationMP, update popup scale.

Runtime evidence: Extended Data 1.0.8, local LogOutput.log, 2026-10-08 21:37:30.261: SelectIndex -> Capture -> CreateDefinition -> Prepare -> ExportHook fails with an empty author Lord name. The user confirmed a standard Lord with custom .lordjson and no visible popup.
This demonstrates the pre-fix failures. Post-fix export, launch and popup visibility still require a gameplay acceptance run.

## Duplicate local and Workshop AIC sources (2026-10-14)

The installed CustomLordConfig.encode/decode roundtrip also drops the workshop flag: decoded path is null and workshop is false, even for a selected Workshop configuration. ExtendedData.Upload.Tests exercises the actual installed codec. The default false value cannot identify a local source after loading a Trail.

CustomisationFileManager's Workshop scan appends standard Lord configurations to the same Extended Lord collection as local configurations. Their names, types and checksums can therefore coincide while their source directories differ. Named Custom Lords instead retain the Workshop item ID prefix in lordName. Both ExtendedData Trail export paths reach TrailLordPackageRuntime.Capture; the normal path reads exported .trail files and the Coop path reads the Maker .trail files before preparing the package.

Equivalent candidates without a saved path can be resolved by comparing gameplay file fingerprints and the selected .modlord.json, Override/Fixes/preferences.json, info.json and lordmeta.json. The existing gameplay digest excludes those synchronized or presentation JSON files and alone does not establish equivalence. Explicit saved paths remain authoritative. These are managed serialization/export contracts; no native addresses changed. Automated codec, replica-selection and sidecar/bundle regressions do not establish in-game upload acceptance.

## Managed publication, portable Lords and Coop slot filtering (2026-10-14)

Rechecked against the same installed managed/native hashes above. The affected chain is managed: ExportHook -> CoopTrailPackageExporter.Prepare/CreateDefinition -> TrailLordPackageRuntime.Capture -> PreparedPackage.Publish; launch readiness and preparation both use LordDataSyncCoordinator.TryInspectTrail -> TrailLordPackageRuntime.TryResolve. No native RVA or hook change is involved.

Fault injection against the original publication body proved that a failed backup rename still reached recursive deletion of the old mission directory. The corrected production publisher tracks backup completion and ownership of newly installed missions separately; only its own new directory can be removed during rollback. Atomic manifest replacement is the commit point. Recovery errors retain the backup path and both exceptions. Automated regressions exercise the production publisher with real temporary files and injected backup/install/manifest/recovery/cleanup failures.

A fully embedded Lord with RequiresInstalledPackage=false must not require any installed configuration. Installed-candidate type, registration and gameplay digest remain mandatory only for RequiresInstalledPackage=true; optional media absence or ambiguity cannot block embedded gameplay data. Production TryResolve is source-linked into tests, including missing and mismatching mandatory packages. Existing Fixes snapshot compatibility checks are retained.

Coop turns the first two occupied saved slots into humans. Resolve author sources only for the remaining original player IDs, before Capture, both for playable Coop missions and the later TrailMakerSource Lord sidecars. Those editable sidecars retain original IDs; playable Coop sidecars remap to compact player IDs. The ordinary Custom Trail export still captures all relevant AI slots. These are automated managed-contract checks, not an in-game acceptance result.

## Unnamed embedded Lords and live snapshot membership (2026-10-26)

Reverified the installed native and managed hashes against the identities above. The
closed path is managed: RestartSkirmishMapInfo.encode/decode preserves builtInLord=false,
the complete AIC/AIV data and an empty lordName; ResolveEmbedded and CopyLord retain
that selection. PrepareTrail builds authoritative slots from the verified requirements,
using their author-facing names when no optional installed media provider is found.
CaptureLobby includes these prepared embedded slots. An installed media name is not
evidence of gameplay validity and its absence cannot remove a verified embedded slot.

The subscribed Suffer of the antler package (packageId
fca8c817-579d-48dc-93e9-3e78a9ebfdbc, content fingerprint
b37597cbafb5e8fb6f789dfd7bc409ac66cf5b81c1b58a4f7affbf6b23028afb) passes the
production loader's complete 40-mission validation. Direct inspection of mission 1's
bundled restart data found empty names and complete custom AIC/castle data at slots
3 through 8; its six Lord requirements all have RequiresInstalledPackage=false.
The prior IsReadyToLaunch filter omitted these slots solely for their empty names,
while the snapshot included them. The installed production slot comparator rejects
that empty selection list and accepts the complete six-slot list. Host log
2026-10-26 16:48:59.742 reports this exact slot-mismatch gate; the client receives it.

Live membership must include active embedded Custom AICs whose type, configuration
name and checksum still match their prepared data, regardless of local media. Exact
snapshot membership, current configuration binding, required external packages and
participant acknowledgements remain separate launch requirements. Saved-snapshot
reconstruction retains its existing path. No native address or hook changes here;
new two-peer gameplay acceptance is still required after the managed correction.

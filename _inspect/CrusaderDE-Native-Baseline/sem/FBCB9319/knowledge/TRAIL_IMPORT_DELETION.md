# Trail import list and local deletion

Verified against installed binaries on 2026-10-10.
Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed SHA-256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Script Extender commit: 5908e1f12437deb7ef5f5b1dc2d64e301178fba3; tree c238441ea12db35ee36c5c5668d3c8fce0897295.

This feature's selection, filesystem and confirmation contracts are managed; it requires no native address or detour.

- FRONT_ManageTrail.ButtonClicked("Import") populates ObservableCollection<FileRow> importRows using GetCustomTrails(true), stores the exact CustomTrailInfo.Name in Text1, clears selection and disables ImportImportButton. The selected row's Text1 is subsequently used as the source folder identity, not as a translated display name.
- The ImportList selection handler enables ImportImportButton for a non-null row but does not disable it on null selection. A deletion must explicitly clear and disable it.
- A local CustomTrailInfo may have workshopUploadInfoAvailable=true and a Steam icon while workshop=false. An upload does not make the local source a subscribed Workshop trail.
- MapFileManager constructs subscribed entries with workshop=true, a prefixed Name and an absolute FullPath. GetCustomTrails(true) filters workshop, not workshopUploadInfoAvailable.
- RescanCustomTrailsFolder removes and rebuilds only non-workshop entries. Unlike initial scanning, it does not assign FullPath on rebuilt local entries; use the verified local name under ConfigSettings.GetUserCustomTrailsPath() when FullPath is empty.
- Vanilla scans only direct child folders and their top-level *.trail files. ExtendedData also adds local and subscribed Coop packages, including packages with editable sources under TrailMaker. Their public FileRow.trail metadata now carries the source package root and workshop flag. Deleting a local Coop trail removes the entire source package, not the separate active TrailMaker folder.
- The import backup checkbox backs up the active TrailMaker folder before clearing/importing missions. It does not back up the selected CustomTrails package. The new deletion path never invokes BackupMakerFolder, DoBackup, ClearMakerFolder or ImportTrailMissions.
- Trail Maker confirmations require ShowConfirmationMessage(..., MPConf:true). The message-bearing OK helper has an optional fourth Sands argument, not an MPConf overload; after configuring it, select Show_HUD_ConfirmationMP and update popup scale. FrontEndMenu is a public MainViewModel field.
- Noesis list Loaded/SelectionChanged events and GameXAMLManagerAPI's singleton binding registry supply the new callback path. The deletion ViewModel is also explicitly retained by static plugin state. No hook teardown or long-lived MonoBehaviour scheduling is introduced.

Tests: BugfixesAndQoL/Test-DeleteTrail.ps1 exercises the actual deletion policy and ViewModel with isolated UI publishers, including subscription/name conflicts, confirmation cancellation and selection changes, live settings revalidation, missing targets, full package deletion and locked files. Direct and nested junctions are rejected. Test-DeleteTrailXaml.ps1 uses the actual canonical XamlPatcher source and verifies both patch orders with ExtendedData. Test-DeleteTrailContracts.ps1 checks public members against the real installed Assembly-CSharp.dll.

Gameplay acceptance on 2026-10-10: the installed game registered and applied the deletion binding, then successfully deleted three local Coop Trail packages at 15:39:10.769, 15:39:16.954 and 15:39:20.167, each explicitly logged without a backup. The latest game session contains no Error or Fatal entries. The user accepted the feature on the condition that its logs show no problems; this condition is met.

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$utf8 = New-Object Text.UTF8Encoding($false)
$changes = [ordered]@{}
$pending = [ordered]@{}
function Edit-Document([string]$relative, [object[]]$replacements) {
    $path = Join-Path $root $relative
    $original = [IO.File]::ReadAllText($path)
    $updated = $original.Replace("`r`n", "`n")
    foreach ($pair in $replacements) {
        while ($pair.Count -eq 1 -and $pair[0] -is [array]) { $pair = $pair[0] }
        if ($pair.Count -ne 2 -or $null -eq $pair[1]) { throw ("Invalid replacement in ${relative}: " + (ConvertTo-Json -InputObject $pair -Compress -Depth 4)) }
        $old = $pair[0].Replace("`r`n", "`n")
        $new = $pair[1].Replace("`r`n", "`n")
        if (-not $updated.Contains($old)) { throw "Missing replacement in ${relative}: $old" }
        $updated = $updated.Replace($old, $new)
    }
    $updated = $updated.Replace("`n", "`r`n")
    if ($updated -eq $original) { return }
    if ([regex]::IsMatch($updated, '(?<!\r)\n')) { throw "Bare LF: $relative" }
    $pending[$path] = $updated
    $changes[$relative] = ([regex]::Matches($updated, "`r`n")).Count
}

Edit-Document 'Guides\ExtendedData\Mod Compatibilty ExtendedData.md' @(
    ,@(('`0.4.0`'), ('`0.9.0`'))
    ,@(('"0.4.0"'), ('"0.9.0"'))
    ,@(('Shared.PresetLobbyModSettingsViewModel'), ('APIShared.ModSettings.PresetLobbyModSettingsViewModel'))
    ,@(('using Shared;'), ('using APIShared.ModSettings;'))
    ,@(('clr-namespace:Shared;assembly=APIShared'), ('clr-namespace:APIShared.ModSettings;assembly=APIShared'))
    ,@(('Use this namespace in the settings XAML because the shared controls now live in APIShared:'), ('Use the public settings namespace in the settings XAML:'))
    ,@(('this, Logger, Info.Metadata.GUID, Settings,'), ('this, Logger, Info.Metadata.Name, Settings,'))
    ,@(('2. Reference the installed `APIShared.dll` with `Private=false`.'), ('2. Reference the installed `APIShared.dll` with `Private=false`. Declare the same minimum APIShared version in `info.json` under `Dependencies`; see [dependency metadata](../../APIShared/docs/DEPENDENCIES.md).'))
    ,@(('2. Referenziere die installierte `APIShared.dll` mit `Private=false`.'), ('2. Referenziere die installierte `APIShared.dll` mit `Private=false`. Deklariere dieselbe APIShared-Mindestversion in `info.json` unter `Dependencies`; siehe [Abhängigkeitsmetadaten](../../APIShared/docs/DEPENDENCIES.md).'))
    ,@(('#### Requirements'), ("#### Requirements`n`nThe integration examples target the checked workspace APIShared 0.9.0 assembly. Use a matching package; select a published release that contains these public types before distributing your mod."))
    ,@(('#### Voraussetzungen'), ("#### Voraussetzungen`n`nDie Integrationsbeispiele richten sich an die geprüfte APIShared-Assembly 0.9.0 im Workspace. Verwende ein passendes Paket; wähle vor der Veröffentlichung deines Mods ein veröffentlichtes Release mit diesen öffentlichen Typen."))
    ,@(('No Shared source links, compile symbols, or ExtendedData reference are required.'), ('No Shared source links, compile symbols, or ExtendedData reference are required. Retain the settings model and runtime callbacks in static state or a persistent publisher; SHCDE destroys startup plugin components. Register on the Unity thread and use logical activation instead of lifecycle teardown.'))
    ,@(('Shared-Quelllinks, Compile-Symbole und eine ExtendedData-Referenz sind nicht erforderlich.'), ('Shared-Quelllinks, Compile-Symbole und eine ExtendedData-Referenz sind nicht erforderlich. Verwurzle das Settings-Modell und Runtime-Callbacks statisch oder über einen langlebigen Publisher; SHCDE zerstört Startup-Plugin-Komponenten. Registriere auf dem Unity-Thread und verwende logische Aktivierung statt Lifecycle-Teardown.'))
)

Edit-Document 'Guides\ExtendedData\ModSettings Presets.md' @(
    ,@(('Shared.PresetLobbyModSettingsViewModel'), ('APIShared.ModSettings.PresetLobbyModSettingsViewModel'))
    ,@(('1. Add a reference and hard dependency on the required APIShared version.'), ('1. Reference `APIShared.dll` with `Private=false` and declare matching hard minimum dependencies in BepInEx and `info.json`. These examples target the checked workspace assembly 0.9.0; use a published release containing the APIs before distributing a consumer.'))
    ,@(('1. APIShared referenzieren und als harte Abhängigkeit mit passender Mindestversion angeben.'), ('1. `APIShared.dll` mit `Private=false` referenzieren und übereinstimmende harte Mindestabhängigkeiten in BepInEx und `info.json` angeben. Diese Beispiele richten sich an die geprüfte Workspace-Assembly 0.9.0; vor der Veröffentlichung eines Consumers ein veröffentlichtes Release mit den verwendeten APIs wählen.'))
    ,@(('5. Copy the standard Load/Save XAML block from a preset-capable mod in this repository.'), ('5. Import `clr-namespace:APIShared.ModSettings;assembly=APIShared` in XAML. Use the [public settings example](../../APIShared/examples/ThirdPartyMod/ExampleSettings.cs) and [XAML example](../../APIShared/examples/ThirdPartyMod/Override/ScriptExtenderUI/APISharedExample.xaml) for registration and bindings. Retain the model for the process lifetime; registration and UI operations run on Unity.'))
    ,@(('5. Den Standard-XAML-Block für Laden/Speichern aus einem presetfähigen Mod dieses Repositories übernehmen.'), ('5. Im XAML `clr-namespace:APIShared.ModSettings;assembly=APIShared` importieren. Das [öffentliche Settings-Beispiel](../../APIShared/examples/ThirdPartyMod/ExampleSettings.cs) und [XAML-Beispiel](../../APIShared/examples/ThirdPartyMod/Override/ScriptExtenderUI/APISharedExample.xaml) zeigen Registrierung und Bindings. Das Modell für die Prozesslebensdauer verwurzeln; Registrierung und UI-Operationen laufen auf Unity.'))
    ,@(('No Shared source links, preset compile symbols, or ExtendedData reference are required. A minimal header contains:'), ('No Shared source links, preset compile symbols, or ExtendedData reference are required.'))
    ,@(('```xml'), ('A minimal preset header contains:' + "`n`n" + '```xml'))
)

Edit-Document 'APIShared\README.md' @(
    ,@(('Downloads are also available from [releases](https://github.com/SHCDE-APIShared/APIShared/releases);' + "`n" + 'until the first release here, use the' + "`n" + '[previous repository](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases?q=APIShared).'), ('Downloads are also available from [releases](https://github.com/SHCDE-APIShared/APIShared/releases).'))
    ,@(('Script Extender versions your mod needs. Consumer mods must not bundle the DLL.'), ('Script Extender versions your mod needs. Consumer mods must not bundle the DLL.' + "`n" + 'Documentation describes this source checkout. Confirm that the installed release' + "`n" + 'contains each API you use; source-checkout features can precede a published package.'))
)

Edit-Document 'APIShared\ARCHITECTURE.md' @(
    ,@(('  ScriptExtenderFixes/           audited Extender/dependency corrections and permanent managed hooks' + "`n" + '    Events/'), ('    Events/'))
    ,@(('    Internal/Native/            pattern resolution, memory, native reservations'), ('    Internal/Native/            pattern resolution, memory, native reservations' + "`n" + '  ScriptExtenderFixes/           audited Extender/dependency corrections and permanent managed hooks'))
    ,@(('The initial UU-ImGUI shutdown fix'), ('The UU-ImGUI shutdown fix'))
    ,@(('Published presets keep their contracts, JSON schema, file discovery and dialog row models'), ('Published preset contracts, JSON schema, file discovery and dialog row models live'))
)

Edit-Document 'APIShared\docs\THIRD_PARTY_GUIDE.md' @(
    ,@(('Since APIShared 0.7.0, use'), ('Declare APIShared 0.7.0 or newer and use'))
    ,@(('never component teardown. No new mandatory member was added to `IApiShared`.'), ('never component teardown.'))
    ,@(('Since 0.6.1, APIShared includes'), ('APIShared includes'))
    ,@(('The initial fix addresses'), ('The shutdown fix addresses'))
    ,@(('provider if you use that combined heading. Legacy notice properties remain unchanged.'), ('provider if you use that combined heading. The direct-launch and Trail-source notice' + "`n" + 'bindings are also available separately.'))
    ,@(('Shared file formats and legacy `__Serp*` persistence keys are retained for existing saves; they do not restrict mod GUIDs or require the Serps pack.'), ('Persistence uses the documented file formats and `__Serp*` keys with arbitrary mod GUIDs.'))
    ,@(('## Migration and packaging'), ('## Namespaces and packaging'))
    ,@(('Older APIShared releases exposed settings and mode types under `Shared`. When updating such a consumer, use `APIShared.ModSettings` and `APIShared.GameModes`, update its XAML namespace imports, and rebuild against the selected APIShared release.'), ('Use `APIShared.ModSettings` for settings and `APIShared.GameModes` for mode contracts. Settings XAML imports `clr-namespace:APIShared.ModSettings;assembly=APIShared`. Compile against the selected APIShared package and check that it contains every API used by your mod; this guide describes the source checkout.'))
    ,@(('Since APIShared 0.7.1, Optional APIShared consumers may call'), ('Optional consumers requiring APIShared 0.7.1 or newer may call'))
    ,@(('This additive, thread-safe registration'), ('This thread-safe registration'))
    ,@((' Existing attribute behavior and other plugins remain unchanged.'), (' The exclusion applies only to the registered type and its derived types.'))
    ,@(('for legacy idle-only completion'), ('for idle-only completion'))
    ,@(('The experimental `IAssassinTraversalView`/traversal-provider API was removed with maintainer approval;' + "`n" + 'no consumer-defined alternative search or mainmod prerequisite is involved in obstacle completion.'), ('Obstacle completion does not expose consumer-defined alternative traversal search' + "`n" + 'and requires no command-engine mod.'))
    ,@('## Namespaces and packaging', @'
### External settings and application backends

Settings adapters can override `DynamicSettingsProvider` with an
`IDynamicPresetSettingsProvider` when settings are described at runtime. Descriptors
use stable keys, exact CLR types and non-null code defaults; dynamic scopes are
Host or Local. The provider validates and replaces complete working snapshots.
An optional `IModSettingsApplicationBackend` separates desired values from applied
values and identifies settings requiring restart. Use the public
[`dynamic/provider contracts`](../src/ModSettings/Presets/DynamicPresetSettings.cs)
and [`application coordinator`](../src/ModSettings/ModSettingsApplication.cs);
do not treat every edited working value as an already applied runtime setting.

For a separately registered foreign page, use
`LobbyModSettingsPresetRegistration.AttachExternalWorkingCopy` to attach presets
without a second network registration, or `RegisterExternalWorkingCopy` for a
participant without a page. `PrepareExternalWorkingCopy` creates an unpublished
candidate; activate it only when the surrounding integration is ready. These
entry points are Unity-thread operations. Optional consumer adapters and their
activation rules belong to the integrating mod; presets do not require ExtendedData.

## Namespaces and packaging
'@)
)

Edit-Document 'APIShared\docs\API_CATALOG.md' @(
    ,@(('Available since 0.7.0 through'), ('Requires APIShared 0.7.0 or newer; register through'))
    ,@((' Existing `IApiShared` interfaces remain unchanged.'), (''))
    ,@(('The experimental `IAssassinTraversalView`/traversal-provider API was removed with maintainer approval;' + "`n" + 'no consumer-defined alternative search or mainmod prerequisite is involved in obstacle completion.'), ('Obstacle completion does not expose consumer-defined alternative traversal search' + "`n" + 'and requires no command-engine mod.'))
    ,@(('Since APIShared 0.7.1, Optional APIShared consumers may call'), ('Optional consumers requiring APIShared 0.7.1 or newer may call'))
    ,@(('This additive, thread-safe registration'), ('This thread-safe registration'))
    ,@((' Existing attribute behavior and other plugins remain unchanged.'), (' The exclusion applies only to the registered type and its derived types.'))
    ,@(('`APIShared.Workshop.WorkshopInventory` retains the historical inventory schema.'), ('`APIShared.Workshop.WorkshopInventory` exposes the schema-1 file inventory.'))
    ,@(('Available since **0.8.0**.'), ('Requires APIShared **0.8.0** or newer.'))
    ,@(('The additive' + "`n" + '[mode availability collection]'), ('The' + "`n" + '[mode availability collection]'))
    ,@('## Multiplayer packets', @'
The catalog describes this source checkout. Check the selected package and its
public declarations before assigning a consumer minimum version; source features
may not yet be present in published downloads.

## Multiplayer packets
'@)
    ,@('| Native market queries |', @'
| Building tooltip text | [Public broker](../src/Presentation/BuildingTooltip/BuildingTooltipPresentation.cs) | Shared presentation in that file | [Tooltip](../examples/ThirdPartyMod/BuildingTooltipExample.cs) |
| Situational audio | [Sound contracts](../src/Audio/SoundContracts.cs), [event broker](../src/Audio/SoundEvents.cs) | [Audio contracts and implementation](AUDIO.md) | [Sound](../examples/ThirdPartyMod/SoundExample.cs) |
| Workshop packages | [Owner-bound client](../src/Workshop/WorkshopClient.cs) | [Publishing and deployment](WORKSHOP.md) | [Workshop](../examples/ThirdPartyMod/WorkshopExample.cs) |
| Gate access rules | [Passive registry](../src/Pathfinding/GateRoutes/GateAccessRules.cs) | [Provider contracts](GATE_ROUTING.md) | [Gate access](../examples/ThirdPartyMod/GateAccessExample.cs) |
| Native market queries |
'@)
)

Edit-Document 'APIShared\docs\WORKSHOP.md' @(
    ,@(('[Historical inventories](#workshop-package-cleanup): APIShared self-cleanup and retained schema.'), ('[File inventories](#workshop-package-cleanup): APIShared self-cleanup and schema.'))
    ,@(('installation-migration-and-recovery'), ('installation-and-recovery'))
    ,@(('receipts, migration and transactions.'), ('receipts, embedded-copy replacement and transactions.'))
    ,@(('identifies the existing inventory schema. The Extender staging cleanup now applies exclusively to previous APIShared releases; foreign `.map` packages remain Extender-owned. The inventory helpers remain available with unchanged signatures/schema.'), ('identifies the schema-1 file inventory. The Extender staging callback cleans only obsolete APIShared package files; foreign `.map` packages are Extender-owned.'))
    ,@(('Existing `info.json` contracts remain authoritative'), ('`info.json` contracts are authoritative'))
    ,@(('before distributing the new format'), ('before distributing loose packages'))
    ,@(('When migrating an embedded APIShared copy, subscribing to the separate map item does not remove the old copy. The Extender installs the central manager first; the subsequent confirmed payload migration removes only historical, hash-matching files inside the old payload root.'), ('An embedded APIShared copy requires explicit package replacement; subscribing to the central map item alone leaves that copy installed. The Extender installs the central manager first. The confirmed payload transaction removes only files inside the embedded payload root whose hashes match verified deliveries.'))
    ,@(('exclusively for cleaning its own previous releases.'), ('exclusively for cleaning obsolete files from its own installed packages.'))
    ,@(('The existing inventory helpers and schema described below remain available for packaging and historical evidence; foreign maps are no longer cleaned through this callback.'), ('The inventory helpers and schema below describe delivered files and verified release hashes. This callback does not clean foreign maps.'))
    ,@(('The existing JSON contract is schema 1'), ('The JSON contract is schema 1'))
    ,@(('APIShared has no package-specific migration paths.'), ('Cleanup is driven by inventories and hashes, not package-specific paths.'))
    ,@(('This extends update cleanup; the Extender''s existing unsubscribe behavior is unchanged.'), ('The Extender also owns removal of unsubscribed map items.'))
    ,@(('plugin inventory contracts and runtime binary restrictions remain unchanged.'), ('plugin inventories and runtime binary restrictions are separate contracts.'))
    ,@(('Existing maintainer drivers continue using the same helper.'), ('Maintainer drivers use the same helper.'))
    ,@(('This capability is in development. The runtime version is intentionally unchanged during platform testing; assign the next release version before distributing new API consumers and use that release as their minimum dependency.'), ('This capability is under platform testing in the source checkout. Before distributing API consumers, choose a published release containing the capability and declare that release as their minimum dependency.'))
    ,@(('do not become historical map inventories'), ('do not become map inventories'))
    ,@(('Private Windows/Proton, Steam, migration, external-manager and Extender/Fixes tests'), ('Private Windows/Proton, Steam, embedded-copy replacement, external-manager and Extender/Fixes tests'))
    ,@(('The loose host and its GUIDs remain unchanged during migration.'), ('Package replacement preserves the loose host identity and GUIDs.'))
    ,@(('### Installation, migration and recovery'), ('### Installation and recovery'))
    ,@(('offered for migration with backup.'), ('offered for confirmed adoption with backup.'))
    ,@(('survive removal and migration.'), ('survive removal and package replacement.'))
    ,@(('APIShared''s new format does not add an importer to it.'), ('Loose APIShared packages require an importer explicitly supported by that manager.'))
    ,@(('test migration at the existing modpack ID'), ('test package replacement at the existing modpack ID'))
    ,@(('Do not publish the new format until'), ('Do not publish loose packages until'))
    ,@(('The staging callback only cleans previous APIShared releases.'), ('The staging callback only cleans obsolete APIShared package files.'))
    ,@(('`WorkshopInventory` and its existing schema remain public helpers'), ('`WorkshopInventory` and its schema are public helpers'))
    ,@(('The additive owner-bound'), ('The owner-bound'))
    ,@(('APIShared itself continues to be distributed as'), ('APIShared itself is distributed as'))
)

Edit-Document 'APIShared\docs\DEPENDENCIES.md' @(
    ,@(('"MinimumVersion": "0.6.0"'), ('"MinimumVersion": "0.9.0"'))
    ,@(('The installed Script Extender also supports arbitrary GUIDs in `info.json`:'), ('The Script Extender supports arbitrary GUIDs in `info.json`. This example targets' + "`n" + 'the checked workspace APIShared 0.9.0 assembly; select a published release containing' + "`n" + 'your required APIs before distributing a consumer:'))
)

Edit-Document 'Guides\ExtendedData\README.md' @(
    ,@(('This directory collects user and developer documentation for `ExtendedData` and the file conventions it supports.'), ('This directory collects user and developer documentation for `ExtendedData` and its file conventions. Ordinary presets use the public `APIShared.ModSettings` API; ExtendedData supplies optional Map, Trail and Lord workflows. The integration examples identify the checked APIShared package they require.'))
    ,@(('Dieses Verzeichnis enthält Benutzer- und Entwicklerdokumentation für `ExtendedData` und die unterstützten Dateikonventionen.'), ('Dieses Verzeichnis enthält Benutzer- und Entwicklerdokumentation für `ExtendedData` und seine Dateikonventionen. Normale Presets verwenden die öffentliche API `APIShared.ModSettings`; ExtendedData ergänzt optionale Map-, Trail- und Lord-Abläufe. Die Integrationsbeispiele nennen das dafür geprüfte APIShared-Paket.'))
)

Edit-Document 'APIShared\docs\AUDIO.md' @(
    ,@(('Available since APIShared **0.8.0**;'), ('Requires APIShared **0.8.0** or newer;'))
    ,@(('The existing Arabian draw entry is confirmed; new interpretations are inferred,' + "`n" + 'including currently unconfirmed Arabian sheathing reachability. Enum values stay' + "`n" + 'stable when an individual source is confirmed. This is an explicitly approved' + "`n" + 'breaking change: named contexts no longer imply in-game confirmation.'), ('The Arabian draw entry is confirmed; the other interpretations are inferred,' + "`n" + 'including currently unconfirmed Arabian sheathing reachability. Enum values stay' + "`n" + 'stable when an individual source is confirmed. A named context alone does not' + "`n" + 'imply in-game confirmation.'))
    ,@(('compose as before.'), ('compose under the same callback contract.'))
    ,@(('A named situation may now be provisional.'), ('A named situation may be provisional.'))
    ,@(('The Arabian default rule continues to match its confirmed source.'), ('The Arabian example rule matches its confirmed source.'))
    ,@(('The old Arabian-handler detour is removed. Original native return addresses'), ('Original native return addresses'))
    ,@(('Audio source capture now owns wrapper ranges'), ('Audio source capture owns wrapper ranges'))
    ,@((' Catalog sites verify' + "`n" + 'CALL rel32 targets. The former 0x16F590 detour is no longer installed.'), (' Catalog sites verify' + "`n" + 'CALL rel32 targets.'))
    ,@(('No native bytes, marker meanings, lookup identities or clip lifetimes changed.' + "`n"), (''))
    ,@(('Timing is indicative: baseline is one sample and the' + "`n" + 'optimized timing is the median of five; scheduling/GC vary.'), ('Timing is indicative and uses the median of five samples; scheduling/GC vary.'))
    ,@(('| Registrations / callbacks | Bytes before | Bytes after | ns before | ns after |' + "`n" + '| --- | ---: | ---: | ---: | ---: |' + "`n" + '| 0 | 24 | 0 | 59.8 | 27.3 |' + "`n" + '| 1 / Pre | 256 | 144 | 404.1 | 184.0 |' + "`n" + '| 1 / Post | 368 | 232 | 375.1 | 216.1 |' + "`n" + '| 1 / Pre + Post | 408 | 264 | 496.9 | 257.6 |' + "`n" + '| 16 / Pre | 976 | 144 | 1825.1 | 721.0 |' + "`n" + '| 16 / Post | 1808 | 1552 | 1599.5 | 857.4 |' + "`n" + '| 16 / Pre + Post | 2448 | 1704 | 3157.9 | 1447.7 |' + "`n" + '| 16 / all disabled | 24 | 0 | 140.9 | 66.2 |'), ('| Registrations / callbacks | Bytes per call | ns per call |' + "`n" + '| --- | ---: | ---: |' + "`n" + '| 0 | 0 | 27.3 |' + "`n" + '| 1 / Pre | 144 | 184.0 |' + "`n" + '| 1 / Post | 232 | 216.1 |' + "`n" + '| 1 / Pre + Post | 264 | 257.6 |' + "`n" + '| 16 / Pre | 144 | 721.0 |' + "`n" + '| 16 / Post | 1552 | 857.4 |' + "`n" + '| 16 / Pre + Post | 1704 | 1447.7 |' + "`n" + '| 16 / all disabled | 0 | 66.2 |'))
    ,@(('The matrix uses the same general registry scenarios on both versions to compare' + "`n" + 'allocation changes. Separate tests clone the actual compiled IL entry and'), ('The matrix measures the general registry scenarios. Separate tests clone the' + "`n" + 'actual compiled IL entry and'))
)

Edit-Document 'APIShared\docs\GATE_ROUTING.md' @(
    ,@(('rule contributors. Existing `EnemyGatePathPolicyBridge.TryRegister` remains' + "`n" + 'passive and keeps its old contract.'), ('rule contributors. `EnemyGatePathPolicyBridge.TryRegister` is passive.'))
    ,@(('The temporary route-observer bridge retains its existing `Current` identity.' + "`n" + '`CurrentRequested` is an additive demand-aware accessor; existing registrations' + "`n" + 'start enabled.'), ('The temporary route-observer bridge exposes `Current` and the demand-aware' + "`n" + '`CurrentRequested` accessor. Registrations start enabled.'))
    ,@(('All additions are additive; existing gate-policy and observer interfaces are' + "`n" + 'unchanged. No existing registration implicitly installs these hooks.'), ('Passive policy and observer registrations do not implicitly install these hooks.'))
    ,@(('native execution. Local migration builds retain the current test version; the' + "`n" + 'release minimum must be raised to the eventual API release containing this API' + "`n" + 'before distribution. Do not infer availability from an older package sharing a' + "`n" + 'local development version.'), ('native execution. These APIs are present in the checked workspace assembly 0.9.0.' + "`n" + 'Before distribution, declare the minimum published release containing them;' + "`n" + 'a matching local development version alone does not establish package availability.'))
)

Edit-Document 'Guides\Linux and Proton Modding.md' @(
    ,@(('locally installed Script Extender 2.11.0'), ('locally installed Script Extender 2.14.1'))
)
Edit-Document 'Guides\Replace Assets with Script Extender.md' @(
    ,@(('locally installed Script Extender `2.12.0`'), ('locally installed Script Extender `2.14.1`'))
    ,@(('lokal installierten Script Extender `2.12.0`'), ('lokal installierten Script Extender `2.14.1`'))
    ,@(('`target-normalized`: legacy behaviour for deliberately identical canvases.'), ('`target-normalized`: preserves the normalized target pivot for deliberately identical canvases.'))
    ,@(('`target-normalized`: altes Verhalten für absichtlich identische Leinwände'), ('`target-normalized`: erhält den normalisierten Zielpivot für absichtlich identische Leinwände'))
)
foreach ($entry in $pending.GetEnumerator()) {
    [IO.File]::WriteAllText($entry.Key, $entry.Value, $utf8)
    if (-not [string]::Equals([IO.File]::ReadAllText($entry.Key), $entry.Value, [StringComparison]::Ordinal)) {
        throw "Write verification failed: $($entry.Key)"
    }
}
$changes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'changed-documents.json') -Encoding UTF8
$changes.GetEnumerator() | ForEach-Object { Write-Output ("Updated {0}: {1} CRLF" -f $_.Key, $_.Value) }

[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$utf8 = New-Object Text.UTF8Encoding($false)
$guides = @(Get-ChildItem -LiteralPath (Join-Path $root 'Guides') -Recurse -File -Filter '*.md')
$apiDocs = @(Get-ChildItem -LiteralPath (Join-Path $root 'APIShared') -File -Filter '*.md') +
    @(Get-ChildItem -LiteralPath (Join-Path $root 'APIShared\docs') -File -Filter '*.md')
$relatedDocs = @(Get-Item -LiteralPath (Join-Path $root 'Helpers\AtlasBuilder\USAGE.md'))
$localLinks = 0
$jsonExamples = 0
foreach ($document in @($guides + $apiDocs + $relatedDocs)) {
    $content = [IO.File]::ReadAllText($document.FullName)
    if (([regex]::Matches($content, '(?m)^```[^\r\n]*').Count % 2) -ne 0 -or
        [regex]::IsMatch($content, '(?m)^`(?:xml|csharp|json|powershell)\s*$')) {
        throw "Unbalanced or damaged code fence: $($document.Name)"
    }
    foreach ($block in [regex]::Matches($content, '(?ms)^```json\s*\r?\n(.*?)^```')) {
        $text = $block.Groups[1].Value.Trim()
        # DEPENDENCIES documents a property fragment, not a complete object.
        if ($text.StartsWith('"Dependencies"')) { $text = '{' + $text + '}' }
        $null = ConvertFrom-Json -InputObject $text
        $jsonExamples++
    }
    $prose = [regex]::Replace($content, '(?ms)^```.*?^```[^\r\n]*', '')
    foreach ($link in [regex]::Matches($prose, '\[[^\]\r\n]+\]\(([^)\r\n]+)\)')) {
        $reference = $link.Groups[1].Value.Trim().Trim('<', '>')
        if ($reference -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { continue }
        $parts = $reference.Split('#', 2)
        $relative = [Uri]::UnescapeDataString($parts[0])
        $target = if ($relative.Length -eq 0) { $document.FullName } else {
            [IO.Path]::GetFullPath((Join-Path $document.Directory.FullName $relative))
        }
        if (-not (Test-Path -LiteralPath $target)) { throw "Broken link in $($document.Name): $reference" }
        if ($parts.Length -eq 2 -and [IO.Path]::GetExtension($target) -eq '.md') {
            $targetText = [regex]::Replace([IO.File]::ReadAllText($target), '(?ms)^```.*?^```[^\r\n]*', '')
            $seen = @{}
            $anchors = @(foreach ($heading in [regex]::Matches($targetText, '(?m)^#{1,6}\s+([^\r\n]+)')) {
                $slug = [regex]::Replace($heading.Groups[1].Value.Trim().ToLowerInvariant(), '[^\p{L}\p{Nd}_ -]', '').Replace(' ', '-')
                if ($seen.ContainsKey($slug)) { $seen[$slug]++; $slug + '-' + $seen[$slug] } else { $seen[$slug] = 0; $slug }
            })
            if ([Uri]::UnescapeDataString($parts[1]) -notin $anchors) { throw "Broken anchor in $($document.Name): $reference" }
        }
        $localLinks++
    }
}
Write-Output "PASS: $($guides.Count) Guides, $($apiDocs.Count) APIShared documents, $($relatedDocs.Count) related guide; $localLinks local links/anchors; $jsonExamples JSON examples."

$changed = @(
    'Helpers\AtlasBuilder\USAGE.md',
    'Guides\Translation.md', 'Guides\Linux and Proton Modding.md', 'Guides\Replace Assets with Script Extender.md',
    'Guides\ExtendedData\README.md', 'Guides\ExtendedData\Mod Compatibilty ExtendedData.md',
    'Guides\ExtendedData\ModSettings Presets.md', 'Guides\ExtendedData\ModMapData.md',
    'Guides\ExtendedData\Custom Trail Mod Settings.md', 'APIShared\README.md', 'APIShared\ARCHITECTURE.md',
    'APIShared\docs\THIRD_PARTY_GUIDE.md', 'APIShared\docs\API_CATALOG.md', 'APIShared\docs\DEPENDENCIES.md',
    'APIShared\docs\WORKSHOP.md', 'APIShared\docs\AUDIO.md', 'APIShared\docs\GATE_ROUTING.md'
)
$rows = @(foreach ($relative in $changed) {
    $text = [IO.File]::ReadAllText((Join-Path $root $relative))
    if ([regex]::IsMatch($text, '(?<!\r)\n|\r(?!\n)')) { throw "Non-CRLF line ending: $relative" }
    if ($text.Contains([string][char]0xFFFD) -or $text.Contains('\r\n')) { throw "Damaged text: $relative" }
    if ($text.Contains('Shared.PresetLobbyModSettingsViewModel') -and -not $text.Contains('APIShared.ModSettings.PresetLobbyModSettingsViewModel')) { throw "Old preset namespace: $relative" }
    if ($text.Contains('clr-namespace:Shared;assembly=APIShared') -or $text.Contains('using Shared;')) { throw "Old consumer import: $relative" }
    [pscustomobject]@{ Document = $relative; CRLF = ([regex]::Matches($text, "`r`n")).Count }
})
$rows | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'checked-documents.json') -Encoding UTF8
Write-Output "PASS: CRLF and text checks for $($changed.Count) changed documents."

$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$managed = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed'
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
$api = Join-Path $root 'APIShared\BepInEx\plugins\APIShared_Serp\APIShared.dll'
$apiIdentity = [Reflection.AssemblyName]::GetAssemblyName($api)
if ($apiIdentity.Version.ToString() -ne '0.9.0.0') { throw "Unexpected fixture reference: $apiIdentity" }
$references = @(
    'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1\Facades\netstandard.dll',
    $api, (Join-Path $extender 'SHCDESE.dll'), (Join-Path $game 'BepInEx\core\BepInEx.dll'),
    (Join-Path $managed 'UnityEngine.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'Noesis.NoesisGUI.dll'), (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $extender 'MessagePack.dll'), (Join-Path $extender 'MessagePack.Annotations.dll'),
    (Join-Path $game 'BepInEx\plugins\ExtendedData_Serp\ExtendedData.dll'),
    (Join-Path $game 'BepInEx\plugins\ExtendedData_Serp\ExtendedData.Core.dll')
)
foreach ($reference in $references) { if (-not (Test-Path -LiteralPath $reference)) { throw "Missing public reference: $reference" } }
$compatibility = [IO.File]::ReadAllText((Join-Path $root 'Guides\ExtendedData\Mod Compatibilty ExtendedData.md'))
$blocks = @([regex]::Matches($compatibility, '(?ms)^```csharp\s*\r?\n(.*?)^```') | ForEach-Object { $_.Groups[1].Value.Trim() })
$settings = $blocks | Where-Object { $_.Contains('public sealed class ExampleSettings') } | Select-Object -First 1
$register = $blocks | Where-Object { $_.StartsWith('LobbyModSettingsPresetRegistration.Register(') } | Select-Object -First 1
if (-not $settings -or -not $register) { throw 'Missing documented settings/registration example.' }
$mapText = [IO.File]::ReadAllText((Join-Path $root 'Guides\ExtendedData\ModMapData.md'))
$mapBlock = [regex]::Match($mapText, '(?ms)^```csharp\s*\r?\n(.*?)^```').Groups[1].Value
$mapBody = [regex]::Replace($mapBlock, '(?m)^using [^;]+;\s*', '')
$fixture = @'
using System;
using System.Collections.Generic;
using BepInEx;
using APIShared;
using APIShared.ModSettings;
using ExtendedData;
using SHCDESE.API.Components.Network;

'@ + ([regex]::Replace($settings, '(?m)^using [^;]+;\s*', '')) + @'

public sealed class DocumentRegistrationFixture : BaseUnityPlugin
{
    private static readonly ExampleSettings Settings = new ExampleSettings();
    public void CheckRegistration()
    {
'@ + $register + @'

    }
    public static void CheckMapRead()
    {
'@ + $mapBody + @'

    }
    public static void CheckSettingsTypes()
    {
        Type[] contracts = { typeof(IModSettingsPresetEndpoint), typeof(IModSettingsWorkingCopyEndpoint),
            typeof(ModSettingsWorkingSource), typeof(IDynamicPresetSettingsProvider),
            typeof(IModSettingsApplicationBackend), typeof(ModSettingsSearch), typeof(ModSettingsMode) };
    }
}
'@
$fixturePath = Join-Path $PSScriptRoot 'DocumentExamples.cs'
$presetText = [IO.File]::ReadAllText((Join-Path $root 'Guides\ExtendedData\ModSettings Presets.md'))
$bindings = @([regex]::Matches($presetText, '\bSystem_[a-zA-Z0-9_]+') | ForEach-Object { $_.Value } | Sort-Object -Unique)
$bindingReads = @($bindings | ForEach-Object { 'Settings.' + $_ })
$bindingMethod = 'public static void CheckDocumentedBindings() { object[] values = { ' + ($bindingReads -join ', ') + ' }; }' + [Environment]::NewLine
$fixture = $fixture.Replace('public static void CheckSettingsTypes()', $bindingMethod + 'public static void CheckSettingsTypes()')
$fixture = [regex]::Replace($fixture, '\r?\n', [Environment]::NewLine)
[IO.File]::WriteAllText($fixturePath, $fixture, $utf8)
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$arguments = @('/nologo', '/target:library', '/langversion:latest', ('/out:' + (Join-Path $PSScriptRoot 'DocumentExamples.dll')))
$arguments += @($references | ForEach-Object { '/reference:' + $_ })
$arguments += $fixturePath
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Document example compilation failed: $LASTEXITCODE" }
Write-Output "PASS: documented settings, registration, map-data and public type examples compile against APIShared $($apiIdentity.Version) and the real installed game/Extender assemblies."
Write-Output "PASS: $($bindings.Count) documented preset bindings compile against public assembly members."

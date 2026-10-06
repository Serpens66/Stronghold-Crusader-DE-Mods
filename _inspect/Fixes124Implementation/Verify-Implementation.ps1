[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$workspace=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$mods=@('ExtendedData','APIShared','Testmods\AIAttackTest')
$forbidden='System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
foreach($relative in $mods) {
    $root=Join-Path $workspace $relative
    $sources=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs')
    $projects=@(Get-ChildItem -LiteralPath $root -File -Filter '*.csproj')
    foreach($file in @($sources)+@($projects)) {
        $text=[IO.File]::ReadAllText($file.FullName)
        if($text -match $forbidden) { throw "Runtime JSON dependency: $($file.FullName)" }
        if($file.Name -like '*Plugin.cs' -and $text -match '\b(Update|LateUpdate|FixedUpdate|StartCoroutine|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw "Plugin lifetime callback: $($file.FullName)" }
        if($text -match '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\(' -and $file.Name -ne 'ExtendedDataPlugin.cs') { throw "Unexpected runtime teardown: $($file.FullName)" }
    }
    $texts=@(Get-ChildItem -LiteralPath $root -File -Recurse | Where-Object { $_.FullName -notmatch '\\(?:bin|obj|BepInEx)\\' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.xaml','.lordjson','.aivjson') -and $_.Name -ne 'README.md' })
    foreach($file in $texts) {
        $text=[IO.File]::ReadAllText($file.FullName)
        if($text -match '(?<!\r)\n') { throw "Non-CRLF text: $($file.FullName)" }
    }
    foreach($file in @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xaml' | Where-Object { $_.FullName -notmatch '\\BepInEx\\' })) {
        [xml]$xml=Get-Content -LiteralPath $file.FullName -Raw
        foreach($content in @($xml.SelectNodes("//*[local-name()='Content']"))) {
            $elements=@($content.ChildNodes | Where-Object NodeType -eq 'Element')
            if($elements.Count -ne 1) { throw "XAML patch Content must have one root: $($file.FullName)" }
        }
    }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if(-not $?) { throw 'Workspace permanent hook regression failed.' }
$overrides=[IO.File]::ReadAllText((Join-Path $workspace 'Testmods\AIAttackTest\src\AIAttackPermanentNativeOverrides.cs'))
if($overrides -notmatch 'if \(published\) return;' -or $overrides -notmatch 'recruitTransaction' -or $overrides -match '\b(CodePatch\.Write|VirtualProtect|Undo|Disable|Enable)\s*\(') { throw 'AIAttackTest permanent capability contract failed.' }
& (Join-Path $workspace 'ExtendedData\Test-RuntimePreflight.ps1')
if(-not $?) { throw 'Real managed assembly contract verification failed.' }
Write-Host 'PASS: Fixes 1.24 implementation preflight; no runtime build or installation performed.'

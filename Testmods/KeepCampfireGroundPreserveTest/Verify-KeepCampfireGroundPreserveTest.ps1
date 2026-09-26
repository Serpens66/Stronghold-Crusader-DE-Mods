$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$textFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json') -and
    $_.FullName -notmatch '\\(obj|bin|BepInEx)\\'
})
foreach ($file in $textFiles) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($content, '(?<!\r)\n')) { throw "Non-CRLF text: $($file.FullName)" }
    $literalNewline = [string][char]92 + 'r' + [char]92 + 'n'
    if ($content.Contains($literalNewline)) { throw "Literal escaped newline: $($file.FullName)" }
}
$runtimeFiles = @((Join-Path $root 'KeepCampfireGroundPreserveTest.csproj')) +
    @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$runtimeText = ($runtimeFiles | ForEach-Object { [IO.File]::ReadAllText($_) }) -join "`n"
if ($runtimeText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
    throw 'Forbidden runtime JSON dependency.'
}
if ($runtimeText -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Forbidden long-lived MonoBehaviour callback or teardown.'
}
if ($runtimeText -match 'Assembly-CSharp-publicized') { throw 'Publicized Assembly-CSharp reference forbidden.' }
$hook = [IO.File]::ReadAllText((Join-Path $root 'src\CampgroundNativeHook.cs'))
if ($hook -notmatch 'if \(published\) throw' -or $hook -notmatch 'Interlocked\.Exchange' -or
    $hook -notmatch 'DisplacedByteCount != CampgroundVisualGate\.DisplacedBytes') {
    throw 'Published hook lifetime or installed-backend span guard missing.'
}
if ($runtimeText -match 'CodePatch\.Write|VirtualProtect|\.Undo\s*\(|\.Apply\s*\(\s*\)|\.Disable\s*\(') {
    throw 'Unexpected executable-code mutation mechanism.'
}
if (($runtimeText | Select-String -Pattern '\.Dispose\s*\(' -AllMatches).Matches.Count -ne 3 -or
    $hook -notmatch 'transaction\?\.Dispose\(\)' -or
    $runtimeText -notmatch 'private void RollbackUnpublished\(\)' -or
    $runtimeText -notmatch 'Published terrain hooks must remain installed' -or
    $runtimeText -notmatch 'Published terrain probes remain installed') {
    throw 'Unexpected hook teardown path.'
}
if (($runtimeText | Select-String -Pattern 'transaction\.AddDetour\(' -AllMatches).Matches.Count -ne 2 -or
    ($runtimeText | Select-String -Pattern 'transaction\.AddInline\(' -AllMatches).Matches.Count -ne 2 -or
    $runtimeText -notmatch 'terrainPhase\?\.FlushCompleted\(\)' -or
    $runtimeText -notmatch 'TerrainPhaseDiagnostic\.TryCreate' -or
    $runtimeText -notmatch 'TerrainStoreTrace\.TryCreate') {
    throw 'Terrain phase hooks or long-lived diagnosis path missing.'
}
if ($runtimeText -notmatch 'BuildingR3EventHooks\.OnBuildingSpawn' -or
    $runtimeText -notmatch 'GameTimeManagerAPI\.Instance\.OnTick' -or
    $runtimeText -match 'Application\.onBeforeRender' -or
    $runtimeText -notmatch 'MissionInitializationPhase\.BeforeLoad') {
    throw 'Long-lived event path or pre-load activation missing.'
}
$sprites = @(Get-ChildItem -LiteralPath (Join-Path $root 'assets\fire-source') -Filter '*.png' -File)
if ($sprites.Count -ne 9) { throw 'Hearth overlay requires exactly nine Vanilla source sprites.' }
$masks = @(Get-ChildItem -LiteralPath (Join-Path $root 'assets\fire-mask') -Filter '*.png' -File)
if ($masks.Count -ne 9) { throw 'Hearth overlay requires exactly nine foreground masks.' }
foreach ($sprite in @($sprites) + @($masks)) {
    $bytes = [IO.File]::ReadAllBytes($sprite.FullName)
    if ($bytes.Length -lt 24 -or
        [BitConverter]::ToInt32([byte[]]@($bytes[19],$bytes[18],$bytes[17],$bytes[16]),0) -ne 64 -or
        [BitConverter]::ToInt32([byte[]]@($bytes[23],$bytes[22],$bytes[21],$bytes[20]),0) -ne 32) {
        throw "Unexpected hearth PNG dimensions: $($sprite.Name)"
    }
}
$overlay = [IO.File]::ReadAllText((Join-Path $root 'src\CampfireOverlayRuntime.cs'))
if ($overlay -match 'new Piece\(|globalX|globalY|elliptical|Application\.onBeforeRender' -or
    $runtimeText -notmatch 'CapturedGraphic\(' -or
    $runtimeText -notmatch 'MaskedSpriteCatalog') {
    throw 'Old fixed-piece overlay or missing captured-ID mask path.'
}
foreach ($sprite in $sprites) {
    if (-not (Test-Path -LiteralPath (Join-Path $root ('assets\fire-mask\' + $sprite.Name)))) {
        throw "Missing matching mask: $($sprite.Name)"
    }
}
[xml](Get-Content -LiteralPath (Join-Path $root 'KeepCampfireGroundPreserveTest.csproj') -Raw) | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $root 'info.json') -Raw | ConvertFrom-Json
if ($manifest.GUID -ne 'KeepCampfireGroundPreserveTest_Serp' -or
    $manifest.NetworkMode -ne 0 -or $manifest.Version -ne '0.1.0') {
    throw 'Manifest identity or test version mismatch.'
}
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$sha = [Security.Cryptography.SHA256]::Create()
try { $hash = [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($native))).Replace('-', '') }
finally { $sha.Dispose() }
if ($hash -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Installed native build differs from audited build.'
}
$workspace = (Resolve-Path (Join-Path $root '..\..')).Path
$changed = @(git -C $workspace diff --unified=0 -- '*.cs' | Where-Object {
    $_.StartsWith('+') -and -not $_.StartsWith('+++') -and
    $_ -match 'CodePatch\.Write|Marshal\.Write(?:Byte|Int16|Int32|Int64)|VirtualProtect|NativeDetour|X64InlineHook|\.Undo\s*\(|\.Apply\s*\(|\.Dispose\s*\('
})
if ($changed.Count -gt 0) { Write-Warning "Workspace contains $($changed.Count) hook/mutation additions; review each before build." }
Write-Output 'Preserve test preflight passed: CRLF, JSON, lifecycle, hook lifetime, native hash, manifest.'

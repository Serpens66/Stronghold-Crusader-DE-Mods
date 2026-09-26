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
if ($runtimeText -match 'CodePatch\.Write|VirtualProtect|NativeDetour|\.Undo\s*\(|\.Apply\s*\(') {
    throw 'Unexpected executable-code mutation mechanism.'
}
if (($runtimeText | Select-String -Pattern '\.Dispose\s*\(' -AllMatches).Matches.Count -ne 1 -or
    $hook -notmatch 'transaction\?\.Dispose\(\)') {
    throw 'Unexpected hook teardown path.'
}
if ($runtimeText -notmatch 'BuildingR3EventHooks\.OnBuildingSpawn' -or
    $runtimeText -notmatch 'GameTimeManagerAPI\.Instance\.OnTick' -or
    $runtimeText -notmatch 'MissionInitializationPhase\.BeforeLoad') {
    throw 'Long-lived event path or pre-load activation missing.'
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

$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File)
$files += Get-Item -LiteralPath (Join-Path $PSScriptRoot 'OutpostTest.csproj')
$shared = Join-Path $PSScriptRoot '..\..\Shared'
$files += Get-Item -LiteralPath (Join-Path $shared 'DebugLogHelper.cs'), (Join-Path $shared 'NativePatternResolver.cs')
if ($files | Select-String -Pattern 'System\.Text\.Json|Newtonsoft\.Json|JavaScriptSerializer|System\.Web\.Extensions|DataContractJsonSerializer|JsonUtility') { throw 'Forbidden runtime JSON dependency.' }
if ($files | Select-String -Pattern '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\(') { throw 'Forbidden runtime lifecycle teardown.' }
$textFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json') -and $_.FullName -notmatch '\\(obj|bin|BepInEx)\\'
})
foreach ($file in $textFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Non-CRLF text: $($file.FullName)" }
}
[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'OutpostTest.csproj') -Raw) | Out-Null
$metadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Raw | ConvertFrom-Json
if ($metadata.GUID -ne 'OutpostTest_Serp' -or $metadata.Version -ne '0.1.0') { throw 'Metadata mismatch.' }
Write-Output 'OutpostTest JSON/lifecycle, CRLF, project and metadata preflight passed.'
# Passive-run invariants: the entry point must not construct the production runtime.
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\OutpostTestPlugin.cs'))
$observer = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\VanillaObserver.cs'))
if ($plugin -match 'new OutpostRuntime|new OutpostNative' -or $plugin -notmatch 'new VanillaObserver') { throw 'Production runtime enabled in passive build.' }
if ($observer -match 'CreateUnitLocal\(|AssignUnit\(|IssueMoveHereCommand\(|new OutpostNative|SkipOriginalFunction\s*=|ReturnValue\s*=|->\w+\s*=(?!=)') { throw 'Mutation in passive observer.' }
Write-Output 'Passive observer entry-point and read-only API checks passed.'

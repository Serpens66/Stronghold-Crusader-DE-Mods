param([Parameter(Mandatory = $true)][string]$ProjectDir)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($ProjectDir)
$textFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -in '.cs', '.csproj', '.json', '.bat', '.ps1', '.md' -and
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]'
})
$runtimeFiles = @($textFiles | Where-Object { $_.Extension -in '.cs', '.csproj' })
$sourceFiles = @($textFiles | Where-Object { $_.Extension -eq '.cs' })
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\('

if ($runtimeFiles | Select-String -Pattern $forbiddenJson) { throw 'Forbidden runtime JSON dependency or serializer found.' }
if ($sourceFiles | Select-String -Pattern $forbiddenLifecycle) { throw 'Forbidden Unity teardown lifecycle method found.' }
$xamlFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -eq '.xaml' -and $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]'
})
if ($xamlFiles.Count -ne 0) { throw 'Unexpected XAML patch found in this test mod.' }

foreach ($file in $textFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Non-CRLF newline found: $($file.FullName)" }
    $literalEscapedNewline = [string][char]92 + 'r' + [char]92 + 'n'
    if ($text.Contains($literalEscapedNewline)) { throw "Literal escaped newline sequence found: $($file.FullName)" }
}

$versionFiles = @(
    Join-Path $root 'info.json'
    Join-Path $root 'Properties\AssemblyInfo.cs'
    Join-Path $root 'src\WaterboyTargetReservationPlugin.cs'
)
foreach ($file in $versionFiles) {
    $text = [System.IO.File]::ReadAllText($file)
    if (-not $text.Contains('0.1.0')) { throw "Active version 0.1.0 missing: $file" }
}

Write-Host 'WaterboyTargetReservationTest preflight passed.'

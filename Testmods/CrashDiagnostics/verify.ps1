[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](obj|bin|BepInEx|__pycache__)[\\/]' -and $_.Extension -in @('.cs','.cpp','.h','.csproj','.ps1','.bat','.md','.txt','.json','.py') })
foreach ($file in $sourceFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Bare LF in $($file.FullName)" }
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/CrashDiagnostics' -ConsumerOnly
if ($LASTEXITCODE) { throw 'Scoped shared-boundary preflight failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -Workspace $workspace -ProjectFiles (Join-Path $PSScriptRoot 'CrashDiagnostics.csproj')
if ($LASTEXITCODE) { throw 'Dependency metadata preflight failed.' }
Write-Host 'PASS: CrashDiagnostics source and dependency preflight.'

$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
& (Join-Path $PSScriptRoot 'ValidatePreset.ps1')
if (Get-Process -Name '*Stronghold*' -ErrorAction SilentlyContinue) { throw 'Close the game before installing the test module.' }
foreach ($name in @('DefaultAudit.cs','TweakerDefaultAudit.csproj','build.ps1','build.bat','info.json','Override\CrusaderDETweaker\preset_default-reset-check.json')) {
    $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot $name))
    if ($text -match "(?<!`r)`n") { throw "Bare LF: $name" }
    if ($name -eq 'DefaultAudit.cs' -and $text -match '\b(Update|LateUpdate|FixedUpdate|OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|Dispose)\s*\(|Newtonsoft|System.Text.Json|JsonUtility|JavaScriptSerializer') { throw 'Forbidden runtime pattern' }
}
& (Join-Path $PSScriptRoot '..\..\Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace runtime audit failed' }
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' (Join-Path $PSScriptRoot 'TweakerDefaultAudit.csproj') /t:Build /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$destination = Join-Path $game 'BepInEx\plugins\TweakerDefaultAudit_Serp'
[IO.Directory]::CreateDirectory($destination) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\TweakerDefaultAudit.dll') -Destination $destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $destination
$presetDirectory = Join-Path $destination 'Override\CrusaderDETweaker'
[IO.Directory]::CreateDirectory($presetDirectory) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Override\CrusaderDETweaker\preset_default-reset-check.json') -Destination $presetDirectory
if ((Get-FileHash (Join-Path $PSScriptRoot 'bin\TweakerDefaultAudit.dll')).Hash -ne (Get-FileHash (Join-Path $destination 'TweakerDefaultAudit.dll')).Hash) { throw 'Installation hash mismatch' }
Write-Output 'Read-only default audit installed. No configuration files changed.'

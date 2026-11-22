param([switch]$nopause,[switch]$noinstall)
$ErrorActionPreference='Stop'
try {
    $workspace=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
    & (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -ModDirectory 'Testmods/AIMarketAcceptance'
    if(-not $?) { throw 'Runtime source validation failed.' }
    & dotnet build (Join-Path $PSScriptRoot 'AIMarketAcceptance.csproj') --configuration Release --nologo
    if($LASTEXITCODE -ne 0) { throw 'Acceptance compilation failed.' }
    if(-not $noinstall) {
        if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before installation.' }
        $target='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\AIMarketAcceptance_Serp'
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BepInEx\plugins\AIMarketAcceptance_Serp\AIMarketAcceptance.dll') -Destination $target -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $target -Force
    }
} catch { Write-Error $_; exit 1 }
if(-not $nopause) { Read-Host 'Build complete; Enter to close' | Out-Null }

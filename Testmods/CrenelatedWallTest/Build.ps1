[CmdletBinding()]
param([int]$NoInstall = 0, [int]$VerifyOnly = 0)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$sdk = $env:SHCDE_API_SHARED_DIR
if (-not $sdk) { $sdk = Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp' }
$sdk = [IO.Path]::GetFullPath($sdk)
$priorSdk = $env:SHCDE_API_SHARED_DIR
$env:SHCDE_API_SHARED_DIR = $sdk
$run = New-ShortWorkDirectory -Root $workspace -Kind a -Name crenel -Identity 'working-source APIShared compatibility build'
$installedApi = Join-Path $game 'BepInEx\plugins\APIShared_Serp'
function Read-Dependencies {
    $rows = @{}
    foreach ($directory in (@($sdk, $installedApi) | Select-Object -Unique)) {
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
            $rows[$file.FullName] = Get-Sha256Hex $file.FullName
        }
    }
    return $rows
}
$before = Read-Dependencies
function Assert-DependenciesUnchanged {
    $after = Read-Dependencies
    if ($after.Count -ne $before.Count) { throw 'SDK/installed APIShared file set changed during this build.' }
    foreach ($path in $before.Keys) {
        if (-not $after.ContainsKey($path) -or $after[$path] -cne $before[$path]) { throw "APIShared changed during this build: $path" }
    }
}
$record = [ordered]@{ StartedUtc = [DateTime]::UtcNow.ToString('o'); Source = $PSScriptRoot; Sdk = $sdk; VerifyOnly = [bool]$VerifyOnly;
    Installed = $false; Success = $false; DependencyFiles = $before }
try {
    Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] CrenelatedWallTest selected SDK={1}; record={2}' -f [DateTime]::Now,$sdk,$run)
    & (Join-Path $PSScriptRoot 'verify.ps1') -ApiSharedDirectory $sdk
    if (-not $?) { throw 'Mod verification failed.' }
    Assert-DependenciesUnchanged
    if (-not $VerifyOnly) {
        if (-not $NoInstall -and (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue)) {
            throw 'Close the game before installing CrenelatedWallTest.'
        }
        $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
        & $msbuild (Join-Path $PSScriptRoot 'CrenelatedWallTest.csproj') /t:Rebuild /p:Configuration=Debug ("/p:ApiSharedDir=" + $sdk) /nologo /verbosity:minimal
        if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed.' }
        $output = Join-Path $PSScriptRoot 'BepInEx\plugins\CrenelatedWallTest_Serp'
        $dll = Join-Path $output 'CrenelatedWallTest.dll'
        $compiled = [Reflection.Assembly]::ReflectionOnlyLoadFrom($dll)
        $reference = @($compiled.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'APIShared' })
        $selectedVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $sdk 'APIShared.dll')).Version
        if ($reference.Count -ne 1 -or $reference[0].Version -ne $selectedVersion) { throw 'Runtime did not compile against the selected SDK.' }
        if (@(Get-ChildItem -LiteralPath $output -File -Filter '*.dll').Count -ne 1) { throw 'Testmod must not ship dependency assemblies.' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination (Join-Path $output 'info.json') -Force
        Assert-DependenciesUnchanged
        $record['SdkVersion'] = $selectedVersion.ToString()
        $record['RuntimeSha256'] = Get-Sha256Hex $dll
        if (-not $NoInstall) {
            $target = Join-Path $game 'BepInEx\plugins\CrenelatedWallTest_Serp'
            foreach ($file in Get-ChildItem -LiteralPath $output -Recurse -File) {
                $relative = $file.FullName.Substring($output.Length + 1)
                $destination = Join-Path $target $relative
                [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
                Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
            }
            & (Join-Path $PSScriptRoot 'verify-installed.ps1')
            if (-not $?) { throw 'Installation verification failed.' }
            $record.Installed = $true
        }
    }
    Assert-DependenciesUnchanged
    $record.Success = $true
    Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PASS CrenelatedWallTest; installed={1}; APIShared unchanged within this build (no cross-version hash pin).' -f [DateTime]::Now,$record.Installed)
} finally {
    $record['FinishedUtc'] = [DateTime]::UtcNow.ToString('o')
    [IO.File]::WriteAllText((Join-Path $run 'result.json'), (($record | ConvertTo-Json -Depth 5) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    $env:SHCDE_API_SHARED_DIR = $priorSdk
}

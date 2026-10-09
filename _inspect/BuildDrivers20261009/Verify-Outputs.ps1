$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$baseline = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'baseline.json') | ConvertFrom-Json
Add-Type -Path (Join-Path $game 'BepInEx/core/Mono.Cecil.dll')
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $game 'BepInEx/core'))
$resolver.AddSearchDirectory((Join-Path $game 'Stronghold Crusader Definitive Edition_Data/Managed'))
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.AssemblyResolver = $resolver
. (Join-Path $root 'APIShared/tools/BuildProof.ps1')
$null = Assert-ApiBuildProof (Join-Path $root 'APIShared')
$apiVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $root 'APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll')).Version.ToString()
$plugins = [Collections.Generic.List[object]]::new()
$differences = [Collections.Generic.List[object]]::new()
$matched = 0
foreach ($driver in $baseline.Drivers) {
    $modRoot = Split-Path -Parent (Join-Path $root $driver)
    $packageRoot = Join-Path $modRoot 'BepInEx'
    if (-not (Test-Path -LiteralPath $packageRoot)) { continue }
    foreach ($dll in (Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.dll')) {
        $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll.FullName,$reader)
        try {
            foreach ($type in $assembly.MainModule.Types) {
                $attr = @($type.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' })
                if (-not $attr.Count) { continue }
                $guid = [string]$attr[0].ConstructorArguments[0].Value
                $version = [string]$attr[0].ConstructorArguments[2].Value
                $manifest = Get-Content -Raw -LiteralPath (Join-Path $dll.Directory.FullName 'info.json') | ConvertFrom-Json
                if ($manifest.GUID -ne $guid -or $manifest.Version -ne $version) { throw "Compiled plugin differs from manifest: $guid" }
                $authoritative = Join-Path $modRoot 'info.json'
                if (Test-Path -LiteralPath $authoritative) {
                    $source = Get-Content -Raw -LiteralPath $authoritative | ConvertFrom-Json
                    if ($source.GUID -eq $guid -and $source.Version -ne $version) { throw "Source version differs: $guid" }
                }
                $api = @($assembly.MainModule.AssemblyReferences | Where-Object Name -EQ 'APIShared')
                if ($api.Count -and $api[0].Version.ToString() -ne $apiVersion) { throw "Stale API assembly reference: $guid" }
                $dependencies = @($manifest.Dependencies)
                if ($manifest.MinimumScriptExtenderVersion) {
                    $extenderAttribute = @($type.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInDependency' -and $_.ConstructorArguments.Count -gt 1 -and [string]$_.ConstructorArguments[0].Value -eq '000shcdese' })
                    if ($extenderAttribute.Count) { $dependencies += [pscustomobject]@{GUID='000shcdese';MinimumVersion=$manifest.MinimumScriptExtenderVersion} }
                    elseif (@($assembly.MainModule.AssemblyReferences | Where-Object Name -EQ 'SHCDESE').Count) { throw "Script Extender reference has no loader dependency: $guid" }
                    else { Write-Host "NOTE: $guid has no Script Extender assembly dependency; its minimum is declared only in the existing manifest." }
                }
                foreach ($dependency in $dependencies) {
                    if (-not $dependency.MinimumVersion) { continue }
                    $match = @($type.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInDependency' -and $_.ConstructorArguments.Count -gt 1 -and [string]$_.ConstructorArguments[0].Value -eq $dependency.GUID })
                    if (-not $match.Count -or [string]$match[0].ConstructorArguments[1].Value -ne $dependency.MinimumVersion) { throw "Minimum dependency differs: $guid -> $($dependency.GUID)" }
                }
                $plugins.Add([pscustomobject]@{GUID=$guid;Version=$version;APIReference=if ($api.Count) {$api[0].Version.ToString()} else {''};Driver=$driver})
            }
        } finally { $assembly.Dispose() }
    }
    foreach ($file in (Get-ChildItem -LiteralPath $packageRoot -Recurse -File)) {
        $relative = [IO.Path]::GetRelativePath($packageRoot,$file.FullName)
        $installed = Join-Path $game ('BepInEx/' + $relative)
        if ($relative -like 'plugins\APIShared_Serp\*' -and -not (Test-Path -LiteralPath $installed)) { $installed = Join-Path $game ('BepInEx/plugins/SerpsMods_Serp/Infrastructure/' + $relative.Substring(8)) }
        if (-not (Test-Path -LiteralPath $installed)) {
            if ($driver -eq 'Testmods\AivLobbyPresetTest\build.bat' -and $file.Name -eq 'AivLobbyPresetTest.pdb') { Write-Host 'NOTE: AivLobbyPresetTest does not install its local debug PDB.'; continue }
            if ($driver -eq 'Testmods\StartupPerformanceDiagnostic\build.bat' -and $relative -eq 'patchers\StartupPerformanceDiagnostic.Patcher.pdb') { Write-Host 'NOTE: StartupPerformanceDiagnostic installs only the patcher DLL, not its local debug PDB.'; continue }
            $differences.Add([pscustomobject]@{Driver=$driver;File=$relative;State='NotInstalled'})
        } elseif ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) {
            $differences.Add([pscustomobject]@{Driver=$driver;File=$relative;State='DifferentHash'})
        } else { $matched++ }
    }
}
foreach ($pair in @(@('verified-plugins.json',$plugins.ToArray()),@('installation-differences.json',$differences.ToArray()))) {
    $json = (ConvertTo-Json -InputObject @($pair[1]) -Depth 5).Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot $pair[0]),$json,[Text.UTF8Encoding]::new($false))
}
if ($differences.Count) { $differences | Format-Table -AutoSize; throw "$($differences.Count) installation differences need review." }
Write-Host "PASS: $($plugins.Count) plugin versions/minimum dependencies, $matched installed file hashes, API assembly reference $apiVersion and fresh API build proof."

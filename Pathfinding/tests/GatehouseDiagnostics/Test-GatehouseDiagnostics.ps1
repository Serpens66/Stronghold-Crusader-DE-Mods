param([switch]$Diagnostic, [string]$BaselineRef)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'Use pwsh for this in-memory source fixture.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$files=@('Pathfinding/src/GateRouting/ReachableEnemyGatehouseRuntime.cs','Pathfinding/src/GateRouting/SynchronizedGatehouseReachabilityPolicy.cs',
    'APIShared/src/Buildings/Gatehouse/GatehouseDrawbridgeCoupling.cs','Shared/Runtime/Gameplay/GatehouseQueryUnitIdPolicy.cs',
    'APIShared/src/Buildings/BuildingGeometry.cs','APIShared/src/Buildings/Gatehouse/GatehouseClosingRules.cs',
    'Pathfinding/tests/GatehouseDiagnostics/GatehouseFixture.cs')
$options=@('/unsafe','/nowarn:0649,0414')
if($Diagnostic){$files+='Pathfinding/src/GateRouting/ReachableEnemyGatehouseRuntime.DevDiag.cs';$options+='/define:SERP_DEVDIAG'}
if($BaselineRef){
    $commit=(& git -C $root rev-parse --verify ($BaselineRef+'^{commit}')) -join ''
    if($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$'){throw 'Invalid baseline commit.'}
    . (Join-Path $root 'Shared/Tools/Paths/WorkPaths.ps1')
    $fixture=New-ShortWorkDirectory -Root $root -Kind t -Name 'gate-baseline' -Identity $commit
    # Compile runtime helpers from the same historical image. The current fixture supplies
    # external boundaries only; never borrow a newer cache/scope implementation silently.
    $historicalSources=@{}
    function Read-HistoricalSource([string]$file){
        $source=@(& git -C $root show ($commit+':'+$file) 2>$null)
        if($LASTEXITCODE -ne 0){throw "Unsupported historical dependency at ${commit}: $file"}
        return ($source -join "`r`n")
    }
    $runtimePath='Pathfinding/src/GateRouting/ReachableEnemyGatehouseRuntime.cs'
    $historicalSources[$runtimePath]=Read-HistoricalSource $runtimePath
    $helpers=@{
        SimulationRunScope='Pathfinding/src/Core/Common/SimulationRunScope.cs'
        GatehouseFootprintGeometryCache='Pathfinding/src/GateRouting/GatehouseFootprintGeometryCache.cs'
    }
    foreach($name in @($helpers.Keys|Sort-Object)){
        if($historicalSources[$runtimePath] -match ('\b'+[regex]::Escape($name)+'\b')){
            $path=$helpers[$name]
            $historicalSources[$path]=Read-HistoricalSource $path
            $files+=$path
        }
    }
    $files=@(foreach($file in $files){
        if($historicalSources.ContainsKey($file) -or
           $file -match '^Pathfinding/src/GateRouting/ReachableEnemyGatehouseRuntime(\.DevDiag)?\.cs$' -or
           $file -eq 'Shared/Runtime/Gameplay/GatehouseQueryUnitIdPolicy.cs'){
            if(!$historicalSources.ContainsKey($file)){$historicalSources[$file]=Read-HistoricalSource $file}
            $path=Join-Path $fixture ([IO.Path]::GetFileName($file))
            [IO.File]::WriteAllText($path,$historicalSources[$file],[Text.UTF8Encoding]::new($false)); $path
        }else{Join-Path $root $file}
    })
    Write-Output ("HISTORICAL COMPARISON commit="+$commit+"; historical runtime dependencies="+(@($historicalSources.Keys|Sort-Object)-join ','))

}else{
    $files+='Pathfinding/src/GateRouting/GatehouseFootprintGeometryCache.cs'
    $files+='Pathfinding/src/Core/Common/SimulationRunScope.cs'
    $files+='Pathfinding/tests/GatehouseDiagnostics/GeometryCacheTests.cs'
    $options+='/define:GEOMETRY_OPTIMIZED'
    $files=@($files|ForEach-Object{Join-Path $root $_})
}
$references=@(Get-ChildItem (Join-Path $PSHOME ref) -File -Filter '*.dll'|ForEach-Object FullName)
try { Add-Type -Path $files -ReferencedAssemblies $references -CompilerOptions $options }
catch {
    if($BaselineRef){throw "Unsupported historical dependency or fixture contract at ${commit}: $($_.Exception.Message)"}
    throw
}
[Pathfinding.GatehouseFixtureRunner]::Run()
[Pathfinding.GatehouseFixtureRunner]::AllocationReport

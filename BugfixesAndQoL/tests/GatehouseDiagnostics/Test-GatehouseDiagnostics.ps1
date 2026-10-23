param([switch]$Diagnostic, [string]$BaselineRef)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'Use pwsh for this in-memory source fixture.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$files=@('BugfixesAndQoL/src/ReachableEnemyGatehouseRuntime.cs','BugfixesAndQoL/src/SynchronizedGatehouseReachabilityPolicy.cs',
    'APIShared/src/Buildings/Gatehouse/GatehouseDrawbridgeCoupling.cs','Shared/Runtime/Gameplay/GatehouseQueryUnitIdPolicy.cs',
    'BugfixesAndQoL/tests/GatehouseDiagnostics/GatehouseFixture.cs')
$options=@('/unsafe','/nowarn:0649,0414')
if($Diagnostic){$files+='BugfixesAndQoL/src/ReachableEnemyGatehouseRuntime.DevDiag.cs';$options+='/define:SERP_DEVDIAG'}
if($BaselineRef){
    $commit=(& git -C $root rev-parse --verify ($BaselineRef+'^{commit}')) -join ''
    if($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$'){throw 'Invalid baseline commit.'}
    . (Join-Path $root 'Shared/Tools/Paths/WorkPaths.ps1')
    $fixture=New-ShortWorkDirectory -Root $root -Kind t -Name 'gate-baseline' -Identity $commit
    $files=@(foreach($file in $files){
        if($file -match '^BugfixesAndQoL/src/ReachableEnemyGatehouseRuntime(\.DevDiag)?\.cs$'){
            $source=@(& git -C $root show ($commit+':'+$file))
            if($LASTEXITCODE -ne 0){throw "Missing baseline source: $file"}
            $path=Join-Path $fixture ([IO.Path]::GetFileName($file))
            [IO.File]::WriteAllText($path,($source -join "`r`n"),[Text.UTF8Encoding]::new($false)); $path
        }else{Join-Path $root $file}
    })
}else{
    $files+='BugfixesAndQoL/src/GatehouseFootprintGeometryCache.cs'
    $files+='BugfixesAndQoL/src/SimulationRunScope.cs'
    $files+='BugfixesAndQoL/tests/GatehouseDiagnostics/GeometryCacheTests.cs'
    $options+='/define:GEOMETRY_OPTIMIZED'
    $files=@($files|ForEach-Object{Join-Path $root $_})
}
$references=@(Get-ChildItem (Join-Path $PSHOME ref) -File -Filter '*.dll'|ForEach-Object FullName)
Add-Type -Path $files -ReferencedAssemblies $references -CompilerOptions $options
[BugfixesAndQoL.GatehouseFixtureRunner]::Run()
[BugfixesAndQoL.GatehouseFixtureRunner]::AllocationReport

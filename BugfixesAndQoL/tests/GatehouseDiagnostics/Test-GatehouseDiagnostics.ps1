param([switch]$Diagnostic)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'Use pwsh for this in-memory source fixture.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$files=@('BugfixesAndQoL/src/ReachableEnemyGatehouseRuntime.cs','BugfixesAndQoL/src/SynchronizedGatehouseReachabilityPolicy.cs',
    'APIShared/src/Buildings/Gatehouse/GatehouseDrawbridgeCoupling.cs','Shared/Runtime/Gameplay/GatehouseQueryUnitIdPolicy.cs',
    'BugfixesAndQoL/tests/GatehouseDiagnostics/GatehouseFixture.cs')
$options=@('/unsafe','/nowarn:0649,0414')
if($Diagnostic){$files+='BugfixesAndQoL/src/ReachableEnemyGatehouseRuntime.DevDiag.cs';$options+='/define:SERP_DEVDIAG'}
$references=@(Get-ChildItem (Join-Path $PSHOME ref) -File -Filter '*.dll'|ForEach-Object FullName)
Add-Type -Path @($files|ForEach-Object{Join-Path $root $_}) -ReferencedAssemblies $references -CompilerOptions $options
[BugfixesAndQoL.GatehouseFixtureRunner]::Run()

param([Parameter(Mandatory=$true)][string]$AssemblyPath, [ValidateSet('true','false')][string]$DiagnosticMode)
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$marker = @($assembly.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' -and $_.ConstructorArguments[0].Value -eq 'SerpDevDiagnostics' -and $_.ConstructorArguments[1].Value -eq 'true' }).Count -eq 1
$worker = $null -ne $assembly.GetType('BetterPathfindingTest.DiagnosticRuntime', $false)
$capture = $null -ne $assembly.GetType('BetterPathfindingTest.SnapshotCapture', $false)
$expected = $DiagnosticMode -eq 'true'
if ($marker -ne $expected -or $worker -ne $expected -or $capture -ne $expected) { throw "Binary diagnostic boundary mismatch: marker=$marker worker=$worker capture=$capture expected=$expected" }
if (@($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -in @('BugfixesAndQoL','MoatMove','Newtonsoft.Json','System.Text.Json') }).Count) { throw 'Forbidden runtime dependency.' }
Write-Host "PASS: binary diagnostic boundary; devdiag=$expected; no consumer/oracle assembly dependency."

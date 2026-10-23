[CmdletBinding()]
param([switch]$Diagnostic)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $root 'Shared/Tools/Paths/WorkPaths.ps1')
$work=New-ShortWorkDirectory -Root $root -Kind t -Name engine-present -Identity ('Production engine presentation fixture; Diagnostic='+$Diagnostic)
$project=Join-Path $root 'BugfixesAndQoL/tests/EnginePresentation.Tests/EnginePresentation.Tests.csproj'
$msbuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$diagnosticValue=if($Diagnostic){'true'}else{'false'}
& $msbuild $project /t:Build /p:Configuration=Release /nologo /verbosity:minimal ('/p:SerpDevDiagnostics='+$diagnosticValue) ('/p:OutputPath='+$work+'/bin/') ('/p:IntermediateOutputPath='+$work+'/obj/')
if($LASTEXITCODE -ne 0){throw 'Engine presentation fixture compilation failed.'}
& (Join-Path $work 'bin/EnginePresentation.Tests.exe')
if($LASTEXITCODE -ne 0){throw 'Engine presentation fixture failed.'}
Write-Output ('Fixture evidence: '+$work)

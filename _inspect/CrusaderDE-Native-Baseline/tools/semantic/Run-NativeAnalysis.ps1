param([switch]$ResumeOnly)
$ErrorActionPreference = 'Stop'
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$baselineRoot = Split-Path -Parent (Split-Path -Parent $toolDirectory)
$workspace = Split-Path -Parent (Split-Path -Parent $baselineRoot)
Set-Location -LiteralPath $workspace
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
$analysis = Join-Path $toolDirectory 'native_analysis.py'
function Assert-Success([string]$Step) { if ($LASTEXITCODE -ne 0) { throw "$Step failed ($LASTEXITCODE). Existing baseline remains selected." } }
$prepared = & $python $analysis prepare
Assert-Success 'Analysis preflight'
$preparedData = ($prepared | Out-String) | ConvertFrom-Json
if ($null -ne $preparedData.PSObject.Properties['reusedPublished'] -and $preparedData.reusedPublished) { Write-Host 'Verified published generation reused; no analysis or database writes.'; exit 0 }
$generation = $preparedData.generation
$profile = Get-Content -LiteralPath (Join-Path $generation 'profile.json') -Raw | ConvertFrom-Json
$work = Join-Path $generation 'work'
[IO.Directory]::CreateDirectory($work) | Out-Null
$env:JAVA_HOME = Join-Path $workspace '.tools\temurin-jdk-21.0.12.1+1\jdk-21.0.12.1+1'
$ghidra = Join-Path $workspace '.tools\ghidra-12.1.3\ghidra_12.1.3_PUBLIC\support\analyzeHeadless.bat'
$current = Get-Content -LiteralPath (Join-Path $baselineRoot 'CURRENT.json') -Raw | ConvertFrom-Json
$semantic = Join-Path $baselineRoot $current.semanticDirectory
$project = Join-Path $semantic 'ghidra'
Write-Host "$(Get-Date -Format o) Analysis generation: $generation; functions selected: $($preparedData.selectedFunctions)"
if (-not $ResumeOnly) {
    & $ghidra $project 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $toolDirectory -postScript ExportNativeAnalysis.java $work $profile.binaryHash (Join-Path $generation 'selection.txt') $profile.factsDigest -log (Join-Path $generation 'ghidra.log') -scriptlog (Join-Path $generation 'export.log')
    Assert-Success 'Read-only Ghidra analysis'
}
if (-not (Test-Path -LiteralPath (Join-Path $work 'COMPLETE.json') -PathType Leaf)) { throw 'Ghidra did not complete the exporter; inspect ghidra.log (headless can exit zero after script errors).' }
$history = $profile.historical
$historyWork = Join-Path $generation 'historical\work'
$historyProject = Join-Path $baselineRoot ($history.comparisonDirectory + '/ghidra')
Write-Host "$(Get-Date -Format o) Historical machine/flow export; functions selected: $($history.selection.Count)"
if (-not $ResumeOnly) {
    & $ghidra $historyProject 'CrusaderDE-Historical' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $toolDirectory -postScript ExportNativeAnalysis.java $historyWork $history.binaryHash (Join-Path $generation 'historical\selection.txt') $history.factsDigest -log (Join-Path $generation 'historical\ghidra.log') -scriptlog (Join-Path $generation 'historical\export.log')
    Assert-Success 'Read-only historical Ghidra analysis'
}
if (-not (Test-Path -LiteralPath (Join-Path $historyWork 'COMPLETE.json') -PathType Leaf)) { throw 'Historical Ghidra export incomplete; candidate retained.' }
$decoderProject = Join-Path $toolDirectory 'NativeFacts\NativeFacts.csproj'
& dotnet build $decoderProject --configuration Release --nologo
Assert-Success 'Offline decoder build'
& dotnet (Join-Path $workspace '.tools\native-analysis\NativeFacts\Release\net10.0\NativeFacts.dll') $profile.nativePath $profile.binaryHash $work
Assert-Success 'Independent Iced/PE verification'
& dotnet (Join-Path $workspace '.tools\native-analysis\NativeFacts\Release\net10.0\NativeFacts.dll') $history.nativePath $history.binaryHash $historyWork
Assert-Success 'Historical Iced/PE verification'
& dotnet run --project (Join-Path $toolDirectory 'NativeFacts.Tests\NativeFacts.Tests.csproj') --configuration Release
Assert-Success 'Actual installed backend fixture contracts'
$evidence = Join-Path $generation 'backend'
[IO.Directory]::CreateDirectory($evidence) | Out-Null
$assemblies = $profile.installed
foreach ($entry in @(
    @{ Type='RedBird.X64.Hooks.X64InlineHook'; Assembly=$assemblies.'RedBird.X64.dll'.path; File='inline.cs' },
    @{ Type='RedBird.Backends.NativeX64.JumpFactory'; Assembly=$assemblies.'RedBird.Backends.NativeX64.dll'.path; File='jumps.cs' },
    @{ Type='RedBird.Backends.NativeX64.PrologueAnalyzer'; Assembly=$assemblies.'RedBird.Backends.NativeX64.dll'.path; File='prologue.cs' }
)) {
    $code = & ilspycmd -t $entry.Type $entry.Assembly
    Assert-Success "Installed backend inspection $($entry.Type)"
    [IO.File]::WriteAllText((Join-Path $evidence $entry.File), ([string]::Join("`r`n", @($code)) + "`r`n"), [Text.UTF8Encoding]::new($false))
}
& $python $analysis finalize $generation
Assert-Success 'Fact and coverage validation'
& $python $analysis publish $generation
Assert-Success 'Transactional index publication'
Write-Host "$(Get-Date -Format o) Analysis published and validated."

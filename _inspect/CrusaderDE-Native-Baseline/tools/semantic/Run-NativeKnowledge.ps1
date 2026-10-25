param(
    [string]$SourceDirectory,
    [Parameter(Mandatory)][string]$RunDirectory
)
$ErrorActionPreference = 'Stop'
$tools = Split-Path -Parent $MyInvocation.MyCommand.Path
$baseline = Split-Path -Parent (Split-Path -Parent $tools)
$workspace = Split-Path -Parent (Split-Path -Parent $baseline)
$current = Get-Content -LiteralPath (Join-Path $baseline 'CURRENT.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$semantic = Join-Path $baseline $current.semanticDirectory
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
$catalog = Join-Path $semantic 'knowledge\native-field-catalog.json'
$plan = Join-Path $semantic 'knowledge\native-field-ghidra.json'
$ghidra = Join-Path $workspace '.tools\ghidra-12.1.3\ghidra_12.1.3_PUBLIC\support\analyzeHeadless.bat'
$jdk = Join-Path $workspace '.tools\temurin-jdk-21.0.12.1+1\jdk-21.0.12.1+1'
$env:JAVA_HOME = $jdk
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'Shared\Tools\Steam\SteamPackPolicy.ps1')
Assert-ShortWorkDirectory -Path $RunDirectory
foreach ($used in @('apply.json', 'apply-console.log', 'wrong-image.json', 'protected-before.json')) {
    if (Test-Path -LiteralPath (Join-Path $RunDirectory $used)) { throw 'Use a fresh allocated run directory; existing evidence is preserved.' }
}
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if ((Get-SteamPackFileSha256 -Path $native) -ne $current.currentNativeHash) { throw 'Installed native image differs from baseline.' }
function Assert-KnowledgeExit([string]$Step) {
    if ($LASTEXITCODE -ne 0) { throw "$Step failed ($LASTEXITCODE)." }
}
function Get-KnowledgeProtectedSnapshot {
    $result = [ordered]@{}
    foreach ($root in @((Join-Path $baseline $current.currentNativeHash), (Join-Path $semantic 'analysis'))) {
        foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName)) {
            $result[$file.FullName.Substring($baseline.Length)] = Get-SteamPackFileSha256 -Path $file.FullName
        }
    }
    return ($result | ConvertTo-Json -Depth 4)
}
$protectedBefore = Get-KnowledgeProtectedSnapshot
[IO.File]::WriteAllText((Join-Path ([IO.Path]::GetFullPath($RunDirectory)) 'protected-before.json'), $protectedBefore + "`r`n", [Text.UTF8Encoding]::new($false))
try {
if ($SourceDirectory) {
    $source = (Resolve-Path -LiteralPath $SourceDirectory).Path
    $sourceBefore = @{}
    Get-ChildItem -LiteralPath $source -File | ForEach-Object { $sourceBefore[$_.Name] = Get-SteamPackFileSha256 -Path $_.FullName }
    dotnet run --project (Join-Path $tools 'KnowledgeExport\KnowledgeExport.csproj') --configuration Release "-p:NativeKnowledgeSourceRoot=$source" -- $source $catalog
    Assert-KnowledgeExit 'Production catalog export'
    $exported = Get-Content -LiteralPath $catalog -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($file in $exported.sourceFiles) {
        if ($sourceBefore[$file.name] -ne $file.sha256) { throw "Catalog source changed during export: $($file.name)" }
    }
    & $python (Join-Path $tools 'native_knowledge.py') $catalog $plan --native-hash $current.currentNativeHash --workspace $workspace
} else {
    & $python (Join-Path $tools 'native_knowledge.py') $catalog $plan --native-hash $current.currentNativeHash
}
Assert-KnowledgeExit 'Knowledge view preparation'
& $python -m unittest discover -s $tools -p test_native_knowledge.py
Assert-KnowledgeExit 'Knowledge transfer regressions'
$wrongPlan = Join-Path $RunDirectory 'wrong-image.json'
$invalid = Get-Content -LiteralPath $plan -Raw -Encoding UTF8 | ConvertFrom-Json
$invalid.nativeHash = '0' * 64
[IO.File]::WriteAllText([IO.Path]::GetFullPath($wrongPlan), (($invalid | ConvertTo-Json -Depth 100) + "`r`n"), [Text.UTF8Encoding]::new($false))
$wrongReport = Join-Path $RunDirectory 'wrong-image-report.json'
& $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $tools -postScript ImportNativeKnowledge.java $wrongPlan apply $wrongReport -log (Join-Path $RunDirectory 'wrong-image.log') *> (Join-Path $RunDirectory 'wrong-image-console.log')
if ((Test-Path -LiteralPath $wrongReport) -or -not (Select-String -LiteralPath (Join-Path $RunDirectory 'wrong-image-console.log') -SimpleMatch 'hash does not match Ghidra program' -Quiet)) { throw 'Wrong-image rejection did not execute as expected.' }
foreach ($pass in @('apply', 'repeat', 'verify')) {
    $mode = if ($pass -eq 'verify') { 'verify' } else { 'apply' }
    $report = Join-Path $RunDirectory ($pass + '.json')
    $options = @()
    if ($pass -eq 'verify') { $options += '-readOnly' }
    Write-Host ([DateTime]::UtcNow.ToString('o') + ' Ghidra NativeKnowledge ' + $pass)
    & $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -noanalysis @options -scriptPath $tools -postScript ImportNativeKnowledge.java $plan $mode $report -log (Join-Path $RunDirectory ($pass + '.log')) *> (Join-Path $RunDirectory ($pass + '-console.log'))
    Assert-KnowledgeExit ('Ghidra ' + $pass)
    if (-not (Test-Path -LiteralPath $report)) { throw "Ghidra script did not complete: $pass (see logs)." }
    $result = Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($result.status -ne 'ok' -or ($pass -eq 'repeat' -and $result.changed -ne 0)) { throw "Knowledge $pass validation failed." }
    Write-Host ("Knowledge {0}: fields={1}, types={2}, changed={3}" -f $pass, $result.fieldIds.Count, $result.types, $result.changed)
    $functionReport = Join-Path $RunDirectory ($pass + '-functions.json')
    & $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -noanalysis @options -scriptPath $tools -postScript ImportNativeFunctionNames.java $plan $mode $functionReport -log (Join-Path $RunDirectory ($pass + '-functions.log')) *> (Join-Path $RunDirectory ($pass + '-functions-console.log'))
    Assert-KnowledgeExit ('Ghidra function ' + $pass)
    if (-not (Test-Path -LiteralPath $functionReport)) { throw "Function-name $pass did not complete." }
    $functionResult = Get-Content -LiteralPath $functionReport -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($pass -eq 'repeat' -and $functionResult.changed -ne 0) { throw 'Function-name import is not idempotent.' }
    Write-Host ("Functions {0}: functions={1}, changed={2}" -f $pass, $functionResult.functions, $functionResult.changed)
}
$guardReport = Join-Path $RunDirectory 'guards.json'
& $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $tools -postScript ImportNativeKnowledge.java $plan guardtest $guardReport -log (Join-Path $RunDirectory 'guards.log') *> (Join-Path $RunDirectory 'guards-console.log')
Assert-KnowledgeExit 'Manual annotation guard'
if (-not (Test-Path -LiteralPath $guardReport)) { throw 'Manual annotation guard did not complete.' }
$functionGuard = Join-Path $RunDirectory 'guards-functions.json'
& $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $tools -postScript ImportNativeFunctionNames.java $plan guardtest $functionGuard -log (Join-Path $RunDirectory 'guards-functions.log') *> (Join-Path $RunDirectory 'guards-functions-console.log')
Assert-KnowledgeExit 'Function annotation guard'
if (-not (Test-Path -LiteralPath $functionGuard)) { throw 'Function annotation guard did not complete.' }
& $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $tools -postScript ExportCrusaderSemantics.java (Join-Path $semantic 'exports') $current.currentNativeHash types-only -log (Join-Path $RunDirectory 'types-export.log') *> (Join-Path $RunDirectory 'types-export-console.log')
Assert-KnowledgeExit 'Knowledge type export'
if (-not (Select-String -LiteralPath (Join-Path $RunDirectory 'types-export-console.log') -SimpleMatch 'SEMANTIC_TYPES count=' -Quiet)) { throw 'Type exporter did not complete.' }
$functionPlan = Get-Content -LiteralPath $plan -Raw -Encoding UTF8 | ConvertFrom-Json
if ($functionPlan.functions.Count -gt 0) {
    $functionExportReport = Join-Path $RunDirectory 'functions-export.json'
    & $ghidra (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $tools -postScript ExportKnowledgeFunctions.java $plan (Join-Path $semantic 'exports') $functionExportReport -log (Join-Path $RunDirectory 'functions-export.log') *> (Join-Path $RunDirectory 'functions-export-console.log')
    Assert-KnowledgeExit 'Function-name exports'
    if (-not (Test-Path -LiteralPath $functionExportReport)) { throw 'Function-name exports did not complete.' }
}
} finally {
    $protectedAfter = Get-KnowledgeProtectedSnapshot
    [IO.File]::WriteAllText((Join-Path ([IO.Path]::GetFullPath($RunDirectory)) 'protected-after.json'), $protectedAfter + "`r`n", [Text.UTF8Encoding]::new($false))
    if ($protectedBefore -cne $protectedAfter) { throw 'Protected raw baseline or historical analysis generation changed.' }
    Write-Host 'Raw baseline and historical analysis files unchanged.'
}

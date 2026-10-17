$ErrorActionPreference = 'Stop'
$driver = Join-Path $PSScriptRoot '../CrusaderDE-Native-Baseline/tools/semantic/Build-SemanticBaseline.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($driver, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$guard = $ast.Find({param($node)
    $node -is [Management.Automation.Language.IfStatementAst] -and
    $node.Extent.Text.StartsWith('if ($RequireUnchangedGhidraInputs -and $runGhidraCurrent)')
}, $true)
if (-not $guard) { throw 'Missing Ghidra guard.' }
$check = [scriptblock]::Create($guard.Extent.Text)
$decisionPath = 'test-decision.json'
foreach ($case in @(@($false,$false,$false), @($false,$true,$false), @($true,$false,$false), @($true,$true,$true))) {
    $RequireUnchangedGhidraInputs = $case[0]
    $runGhidraCurrent = $case[1]
    $didThrow = $false
    try { & $check } catch {
        if ($_.Exception.Message -notlike '*Unexpected Ghidra inputs*test-decision.json*') { throw }
        $didThrow = $true
    }
    if ($didThrow -ne $case[2]) { throw 'Guard behavior mismatch.' }
}
Write-Output 'PASS: default behavior preserved; explicit guard blocks unexpected Ghidra work and allows unchanged inputs.'

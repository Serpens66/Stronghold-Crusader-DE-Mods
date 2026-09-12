$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SteamPackPolicy.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Steam pack policy test failed: $Message" }
}

Assert-True ((Resolve-SteamPackVersion -PreviousVersion '1.0.10' -PreparedVersion '1.0.12') -ceq '1.0.12') 'a prepared host version must not be lowered'
Assert-True ((Resolve-SteamPackVersion -PreviousVersion '1.0.11' -PreparedVersion '1.0.11') -ceq '1.0.12') 'an unchanged host version must advance past the published pack'
Assert-True ((Resolve-SteamPackVersion -PreviousVersion $null -PreparedVersion '0.9.0') -ceq '1.0.0') 'the first pack must start at least at 1.0.0'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $workspace 'APIShared')) -gt 0) 'the delivered APIShared XAML patches must satisfy the single-root contract'

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('serps-steam-policy-' + [Guid]::NewGuid().ToString('N'))
try {
    $validRoot = Join-Path $testRoot 'valid\Patches'
    $invalidRoot = Join-Path $testRoot 'invalid\Patches'
    [void](New-Item -ItemType Directory -Path $validRoot,$invalidRoot -Force)
    [IO.File]::WriteAllText((Join-Path $validRoot 'Valid.xaml'), '<Patch><Operation Type="Add" XPath="/root"><Content><Grid><Button /></Grid></Content></Operation></Patch>')
    [IO.File]::WriteAllText((Join-Path $invalidRoot 'Invalid.xaml'), '<Patch><Operation Type="Add" XPath="/root"><Content><Border /><Button /></Content></Operation></Patch>')
    Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $testRoot 'valid')) -eq 1) 'a single Content root must be accepted'
    $rejected = $false
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $testRoot 'invalid')) }
    catch { $rejected = $_.Exception.Message -match 'exactly one direct Content root element' }
    Assert-True $rejected 'multiple Content roots must be rejected'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

Write-Host 'Steam pack policy tests succeeded.' -ForegroundColor Green

$ErrorActionPreference = 'Stop'
$modDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [IO.Path]::GetFullPath((Join-Path $modDir '..\..'))
$gameDll = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$expectedBytes = '0f8446090000664283bc23480a000000'

$stream = [IO.File]::OpenRead($gameDll)
$sha256 = [Security.Cryptography.SHA256]::Create()
try {
    $actualHash = [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '')
}
finally {
    $sha256.Dispose()
    $stream.Dispose()
}
if ($actualHash -ne $expectedHash) {
    throw 'Installed CrusaderDE.dll hash differs from the audited native baseline.'
}
$rizin = Join-Path $workspace '.native-analysis\Run-Rizin-With-Ghidra.cmd'
$actualBytes = (& $rizin -q -c 'p8 16 @ 0x18015c477' $gameDll | Out-String).Trim().ToLowerInvariant()
if ($LASTEXITCODE -ne 0 -or $actualBytes -ne $expectedBytes) {
    throw "Reward-guard bytes differ: $actualBytes"
}
$displacementBytes = [byte[]]@(4, 6, 8, 10 | ForEach-Object {
    [Convert]::ToByte($actualBytes.Substring($_, 2), 16)
})
$skipTarget = 0x15c477 + 6 + [BitConverter]::ToInt32($displacementBytes, 0)
if ($skipTarget -ne 0x15cdc3 -or (0x15c477 + 16) -ne 0x15c487) {
    throw 'The original JE target or displaced span changed.'
}

$sources = @(
    (Join-Path $modDir 'NoDefeatLootTest.csproj'),
    (Join-Path $modDir 'info.json'),
    (Join-Path $modDir 'build.bat'),
    (Join-Path $modDir 'verify.ps1'),
    (Join-Path $modDir 'UpdateToNewDLL.md'),
    (Join-Path $modDir 'Properties\AssemblyInfo.cs'),
    (Join-Path $modDir 'src\NoDefeatLootTestPlugin.cs'),
    (Join-Path $modDir 'src\DefeatLootHook.cs')
)
foreach ($path in $sources) {
    $content = [IO.File]::ReadAllText($path)
    if ([regex]::IsMatch($content, '(?<!\r)\n')) { throw "Bare LF in $path" }
    $escapedNewline = [string][char]92 + 'r' + [char]92 + 'n'
    if ($content.Contains($escapedNewline)) { throw "Literal backslash-r-backslash-n in $path" }
}
$runtimeFiles = @(
    (Join-Path $modDir 'NoDefeatLootTest.csproj'),
    (Join-Path $modDir 'src\NoDefeatLootTestPlugin.cs'),
    (Join-Path $modDir 'src\DefeatLootHook.cs')
)
$runtime = ($runtimeFiles | ForEach-Object { [IO.File]::ReadAllText($_) }) -join "`n"
if ([regex]::IsMatch($runtime, 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility')) {
    throw 'Runtime has a forbidden JSON dependency.'
}
if ([regex]::IsMatch($runtime, '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(')) {
    throw 'Runtime contains a forbidden long-lived Unity callback or teardown.'
}
if ($runtime -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Enable\s*\(|\.Disable\s*\(|\.Undo\s*\(') {
    throw 'Runtime contains an executable-memory mutation or hook teardown outside initialization.'
}
if ($runtime -notmatch 'transaction\.Dispose\(\)' -or $runtime -notmatch 'catch\s*\{') {
    throw 'The candidate-only rollback contract changed and needs manual review.'
}
Write-Host 'No Defeat Loot Test pre-build checks passed.'

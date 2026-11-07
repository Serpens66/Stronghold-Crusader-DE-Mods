# Exercise the file and result boundaries without starting Steam or writing to the game.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
. (Join-Path $PSScriptRoot '../ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $PSScriptRoot 'InGameTest.Transaction.ps1')
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$root = New-ShortWorkDirectory -Root $workspace -Name 'ingame-runner'
$install = Join-Path $root 'install'
$backup = Join-Path $root 'rollback'
$source = Join-Path $root 'package'
$plugin = Join-Path $install 'plugin'
$config = Join-Path $install 'config'
foreach ($path in @($plugin, $config, $source, $backup)) { $null = [IO.Directory]::CreateDirectory($path) }
$null = [IO.Directory]::CreateDirectory((Join-Path $config 'empty'))
[IO.File]::WriteAllBytes((Join-Path $config 'settings.cfg'), [byte[]]@(0,255,13,10))
[IO.File]::WriteAllText((Join-Path $plugin 'plugin.dll'), 'previous')
[IO.File]::WriteAllText((Join-Path $plugin 'previous-only.dll'), 'previous-only')
[IO.File]::WriteAllText((Join-Path $source 'plugin.dll'), 'diagnostic')
[IO.File]::WriteAllText((Join-Path $source 'new-only.dll'), 'new-only')
$patcher = Join-Path $install 'patcher.dll'
$newPatcher = Join-Path $source 'patcher.fixture'
[IO.File]::WriteAllText($newPatcher, 'diagnostic-patcher')

$snap = Save-InGameTestState $config (Join-Path $backup 'config') -Directory
$swaps = [Collections.Generic.List[object]]::new()
Set-InGameTestPackage $source $plugin (Join-Path $backup 'plugin') $swaps -Directory
Set-InGameTestPackage $newPatcher $patcher (Join-Path $backup 'patcher') $swaps
if ([IO.File]::Exists((Join-Path $plugin 'previous-only.dll')) -or -not [IO.File]::Exists($patcher)) {
    throw 'The staged files do not match the test package.'
}
$null = [IO.Directory]::CreateDirectory((Join-Path $config 'generated/child'))
[IO.File]::WriteAllText((Join-Path $config 'generated/child/value'), 'added')
[IO.Directory]::Delete((Join-Path $config 'empty'), $false)
[IO.File]::WriteAllBytes((Join-Path $config 'settings.cfg'), [byte[]]@(1,2,3))
Restore-InGameTestState $snap
Restore-InGameTestPackage $swaps[1]
Restore-InGameTestPackage $swaps[0]
if ([IO.File]::Exists($patcher) -or [IO.File]::Exists((Join-Path $plugin 'new-only.dll')) -or
    -not [IO.File]::Exists((Join-Path $plugin 'previous-only.dll')) -or
    [IO.File]::ReadAllText((Join-Path $plugin 'plugin.dll')) -cne 'previous' -or
    [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $config 'settings.cfg'))) -cne 'AP8NCg==' -or
    [IO.Directory]::Exists((Join-Path $config 'generated')) -or -not [IO.Directory]::Exists((Join-Path $config 'empty'))) {
    throw 'Restore did not recover the original tree and bytes.'
}

$log = Join-Path $root 'fixture.log'
$run = [Guid]::NewGuid().ToString('N')
$old = [Guid]::NewGuid().ToString('N')
[IO.File]::WriteAllLines($log, [string[]]@("INGAME_TEST run=$old result=PASS", "INGAME_TEST run=$run result=PASS step=last"))
if ((Get-InGameTestResult $log $run) -cne 'PASS') { throw 'The current run PASS was not found.' }
[IO.File]::AppendAllLines($log, [string[]]@("INGAME_TEST run=$run result=PASS"))
$rejected = $false
try { $null = Get-InGameTestResult $log $run }
catch { if ($_.Exception.Message -notlike 'INVALID_RESULT:*') { throw }; $rejected = $true }
if (-not $rejected) { throw 'Duplicate terminal markers were accepted.' }

$apiPackage = Join-Path $root 'api'
$null = [IO.Directory]::CreateDirectory($apiPackage)
$apiRuntime = Join-Path $apiPackage 'APIShared.dll'
$apiContent = Join-Path $apiPackage 'APIShared.Content.dll'
[IO.File]::WriteAllText($apiRuntime, 'runtime')
[IO.File]::WriteAllText($apiContent, 'content')
[IO.File]::WriteAllText((Join-Path $apiPackage 'info.json'), '{"GUID":"APIShared_Serp","Version":"1.0.0"}')
$pair = @{
    Schema = 1; Guid = 'APIShared_Serp'; Version = '1.0.0'
    RuntimeSHA256 = (Get-ApiRepositoryHash $apiRuntime)
    ContentSHA256 = (Get-ApiRepositoryHash $apiContent)
    BootstrapSHA256 = (Get-ApiRepositoryHash $newPatcher)
}
[IO.File]::WriteAllText((Join-Path $apiPackage 'dependency-bootstrap-pair.json'), ($pair | ConvertTo-Json))
Assert-InGameTestApiPair $apiPackage $newPatcher
[IO.File]::WriteAllText($newPatcher, 'different build')
$rejected = $false
try { Assert-InGameTestApiPair $apiPackage $newPatcher }
catch { if ($_.Exception.Message -notlike '*digest mismatch: BootstrapSHA256') { throw }; $rejected = $true }
if (-not $rejected) { throw 'Mixed runtime and patcher were accepted.' }
Remove-InGameTestTree $backup $root
Write-Host "PASS: package swap, APIShared pair, settings restoration and terminal marker isolation. Fixture: $root"

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$standalone = 'D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\SHCDE-APIShared'
$testRoot = Join-Path $PSScriptRoot 'subtree-roundtrip'
if (Test-Path -LiteralPath $testRoot) { throw 'The preserved round-trip fixture already exists; inspect it rather than replacing it.' }
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
function Invoke-TestGit([string]$Repository, [string[]]$Arguments) {
    $output = @(& git -C $Repository @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "Git fixture command failed: $($Arguments -join ' ')" }
    return $output
}
function Write-TestText([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($Path) -cne $Text) { throw 'Fixture readback mismatch.' }
}
$donor = Join-Path $testRoot 'community'
$local = Join-Path $testRoot 'workspace'
& git clone --quiet $standalone $donor
if ($LASTEXITCODE -ne 0) { throw 'Could not clone the independent fixture.' }
[IO.Directory]::CreateDirectory($local) | Out-Null
[void](Invoke-TestGit $local @('init', '-b', 'main'))
foreach ($repository in @($local, $donor)) {
    [void](Invoke-TestGit $repository @('config', 'user.name', 'APISharedRoundTrip'))
    [void](Invoke-TestGit $repository @('config', 'user.email', 'roundtrip@example.invalid'))
}
Write-TestText (Join-Path $local 'Unrelated.txt') "Other mods remain untouched.`r`n"
[void](Invoke-TestGit $local @('add', '--all'))
[void](Invoke-TestGit $local @('commit', '-m', 'Unrelated mod fixture'))
$base = (Invoke-TestGit $donor @('rev-parse', 'HEAD')).Trim()
[void](Invoke-TestGit $local @('fetch', $donor, $base))
[void](Invoke-TestGit $local @('read-tree', '--prefix=APIShared/', '-u', $base))
[void](Invoke-TestGit $local @('commit', '-m', 'Existing independent API prefix'))
$mainline = (Invoke-TestGit $local @('rev-parse', 'HEAD')).Trim()
$initialTree = (Invoke-TestGit $local @('rev-parse', 'HEAD^{tree}')).Trim()
[void](Invoke-TestGit $local @('merge', '-s', 'ours', '--no-commit', '--allow-unrelated-histories', $base))
$message = Join-Path $testRoot 'join-message.txt'
Write-TestText $message "Connect the existing identical APIShared subtree without changing files.`r`n`r`ngit-subtree-dir: APIShared`r`ngit-subtree-mainline: $mainline`r`ngit-subtree-split: $base`r`n"
[void](Invoke-TestGit $local @('commit', '-F', $message))
if ((Invoke-TestGit $local @('rev-parse', 'HEAD^{tree}')).Trim() -ne $initialTree) { throw 'The bootstrap changed the workspace tree.' }
$initialSplit = (Invoke-TestGit $local @('subtree', 'split', '--prefix=APIShared')).Trim()
if ($initialSplit -ne $base) { throw 'The existing subtree did not reuse the independent source history.' }
Write-TestText (Join-Path $donor 'community-probe.txt') "Reviewed community change.`r`n"
[void](Invoke-TestGit $donor @('add', '--all'))
[void](Invoke-TestGit $donor @('commit', '-m', 'Community fixture change'))
$upstream = (Invoke-TestGit $donor @('rev-parse', 'HEAD')).Trim()
[void](Invoke-TestGit $local @('fetch', $donor, $upstream))
[void](Invoke-TestGit $local @('subtree', 'merge', '--prefix=APIShared', '--squash', $upstream, '--message', 'Import the explicitly reviewed fixture'))
if ([IO.File]::ReadAllText((Join-Path $local 'Unrelated.txt')) -cne "Other mods remain untouched.`r`n") { throw 'The import changed another mod.' }
Write-TestText (Join-Path $local 'APIShared/workspace-probe.txt') "Reviewed workspace API change.`r`n"
[void](Invoke-TestGit $local @('add', '--all'))
[void](Invoke-TestGit $local @('commit', '-m', 'Workspace API fixture change'))
$split = (Invoke-TestGit $local @('subtree', 'split', '--prefix=APIShared', '--rejoin')).Trim()
$apiTree = (Invoke-TestGit $local @('rev-parse', 'HEAD:APIShared')).Trim()
[void](Invoke-TestGit $donor @('fetch', $local, $split))
[void](Invoke-TestGit $donor @('merge', '--ff-only', $split))
if ((Invoke-TestGit $donor @('rev-parse', 'HEAD^{tree}')).Trim() -ne $apiTree) { throw 'The exported tree differs from the API subtree.' }
if (Test-Path -LiteralPath (Join-Path $donor 'Unrelated.txt')) { throw 'The export leaked another mod.' }
if (@(Invoke-TestGit $local @('status', '--porcelain')).Count -ne 0) { throw 'The round-trip left uncommitted workspace changes.' }
Write-Output 'PASS: unchanged bootstrap tree, explicit community import, API-only export, fast-forward independent adoption, unrelated mod preserved and clean workspace.'

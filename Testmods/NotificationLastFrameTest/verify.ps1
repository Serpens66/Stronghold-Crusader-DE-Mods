[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$managed = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$source = Join-Path $project 'src\NotificationLastFrameTestPlugin.cs'
$projectFile = Join-Path $project 'NotificationLastFrameTest.csproj'
$files = @($source, $projectFile, (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'), (Join-Path $project 'build.bat'), $MyInvocation.MyCommand.Path)

foreach ($path in $files) {
    $content = [IO.File]::ReadAllText($path)
    $literalNewlineEscape = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)' -or
        $content.Contains($literalNewlineEscape)) {
        throw "CRLF or literal newline escape failure: $path"
    }
}

$runtime = [IO.File]::ReadAllText($source) + "`n" + [IO.File]::ReadAllText($projectFile)
if ($runtime -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
    throw 'Forbidden runtime JSON dependency found.'
}
if ($runtime -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Destroyed Unity component callback or teardown path found.'
}
if ($runtime -match 'Assembly-CSharp-publicized|NativeDetour|X64InlineHook|HookTransaction|CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Apply\s*\(|\.Disable\s*\(|\.Enable\s*\(') {
    throw 'Unexpected publicized assembly or executable runtime mutation found.'
}
if ($runtime -notmatch 'private static Hook videoEndedHook' -or
    $runtime -notmatch 'GameTimeManagerAPI\.Instance\.OnTick \+= OnGameTick' -or
    $runtime -notmatch 'NOTIFICATION_LAST_FRAME_TEST_POST_STARTUP' -or
    $runtime -notmatch 'originalVideoEnded\(self, sender, args\)' -or
    $runtime -notmatch 'sfx\.requestBinkPlayState != 3' -or
    $runtime -notmatch 'media\.Opacity = 1f') {
    throw 'Permanent publisher, Vanilla trampoline or display contract differs.'
}
$rollback = [regex]::Matches($runtime, '\.Undo\s*\(|\.Dispose\s*\(')
if ($rollback.Count -ne 2 -or $runtime -notmatch 'if \(videoEndedHook == null && candidate != null\)') {
    throw 'Published hook teardown or unvalidated rollback path found.'
}

$manifest = Get-Content -Raw -LiteralPath (Join-Path $project 'info.json') | ConvertFrom-Json
if ($manifest.GUID -ne 'NotificationLastFrameTest_Serp' -or
    $manifest.Version -ne '0.1.0' -or $manifest.NetworkMode -ne 1) {
    throw 'Testmod identity, version or network mode differs.'
}
function Get-Sha256([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '') }
    finally { $sha256.Dispose(); $stream.Dispose() }
}
if ((Get-Sha256 $native) -ne
    'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Installed native DLL differs from the audited baseline.'
}
if ((Get-Sha256 $managed) -ne
    'BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789') {
    throw 'Installed managed DLL differs from the audited baseline.'
}

Write-Host 'Notification last-frame source, lifetime, hash and CRLF checks passed.'

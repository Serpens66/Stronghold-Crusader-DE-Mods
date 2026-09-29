$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'AIBuildDiagnoseTest.csproj'
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
if ((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Installed native DLL differs from the audited woodcutter build.'
}
$functions = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\exports\semantic-functions.jsonl'
$audited = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(51540|58020|58950|58BE0|C3BF0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $audited[$item.rva] = $item }
if ($audited['0x51540'].calleeRvas -notcontains '0x58950' -or
    $audited['0x58950'].calleeRvas.Count -ne 0 -or
    $audited['0x58950'].dataRvas -contains '0x50EC690' -or
    $audited['0x58BE0'].calleeRvas -notcontains '0xE2610' -or
    $audited['0x58BE0'].dataRvas -notcontains '0x50EC690') {
    throw 'Audited 0x58950 and 0x58BE0 contracts differ.'
}
$rizin = Join-Path $workspace '.tools\Cutter-v2.4.1-Windows-x86_64\Cutter-v2.4.1-Windows-x86_64\rizin.exe'
$routeEntry = (& $rizin -q -e scr.color=false -c 's 0x1800c3bf0; p8 7; q' $native) -join ''
if ($routeEntry.Trim().ToLowerInvariant() -ne '4883ec384963c0') {
    throw 'Route hook displaced instruction bytes differ from the audited seven bytes.'
}
$redbird = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\RedBird.Backends.NativeX64.dll'
if ((Get-Item -LiteralPath $redbird).VersionInfo.FileVersion -ne '1.5.0.0') {
    throw 'Installed NativeX64 backend version differs from the audited detour implementation.'
}
$calls = (& $rizin -q -e scr.color=false -c 's 0x180051587; pd 1; s 0x1800515cd; pd 1; s 0x1800515ff; pd 1; s 0x180051670; pd 1; q' $native) -join "`n"
foreach ($edge in @(@('51587','58020'), @('515cd','58950'),
    @('515ff','c3bf0'), @('51670','6d580'))) {
    if ($calls -notmatch ("0x1800" + $edge[0] + '\s+call\s+0x1800' + $edge[1])) {
        throw "Woodcutter call edge differs: $($edge[0]) -> $($edge[1])"
    }
}
$textFiles = @($sourceFiles) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($sourceFiles + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($runtimeText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
    throw 'Forbidden runtime JSON dependency.'
}
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Long-lived or teardown MonoBehaviour callback in diagnostic runtime.'
}
if ($runtimeText -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\(') {
    throw 'Diagnostic mod must not own executable-memory mutations.'
}
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $fullPath" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $fullPath" }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace native runtime regression check failed.' }
Write-Host 'PASS: Diagnostic runtime and CRLF checks.'

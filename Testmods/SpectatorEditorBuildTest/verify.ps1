$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @(
    (Join-Path $root 'SpectatorEditorBuildTest.csproj'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'verify.ps1'),
    (Join-Path $root 'src\SpectatorEditorBuildTestPlugin.cs')
)
$forbidden = @(
    'System\.Web\.Extensions', 'JavaScriptSerializer', 'System\.Text\.Json',
    'Newtonsoft\.Json', 'DataContractJsonSerializer', 'JsonUtility',
    '\bOnDestroy\s*\(', '\bOnDisable\s*\(', '\bOnApplicationQuit\s*\(',
    '\b(?:Update|LateUpdate|FixedUpdate)\s*\(', '\bStartCoroutine\s*\(',
    '\b(?:CodePatch\.Write|Marshal\.Write(?:Byte|Int16|Int32|Int64)|VirtualProtect)\s*\(',
    '\b(?:Undo|Disable)\s*\(', '\bUnpatchSelf\s*\('
)
$source = Get-Content -LiteralPath (Join-Path $root 'src\SpectatorEditorBuildTestPlugin.cs') -Raw
$project = Get-Content -LiteralPath (Join-Path $root 'SpectatorEditorBuildTest.csproj') -Raw
$runtimeInputs = $source + [Environment]::NewLine + $project
$candidateRollback = [regex]::Matches($source, 'candidate\.UnpatchSelf\s*\(').Count
if ($candidateRollback -ne 1 -or $source -notmatch 'catch \(Exception error\)') {
    throw 'The only hook rollback must be the unpublished initialization candidate.'
}
foreach ($pattern in $forbidden) {
    $matches = [regex]::Matches($runtimeInputs, $pattern)
    if ($pattern -eq '\bUnpatchSelf\s*\(') {
        if ($matches.Count -ne 1) { throw "Unexpected UnpatchSelf count: $($matches.Count)" }
    } elseif ($matches.Count -ne 0) {
        throw "Forbidden runtime pattern: $pattern"
    }
}
foreach ($path in $files) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $text = [Text.Encoding]::UTF8.GetString($bytes)
    $literalBreak = [string][char]92 + 'r' + [string][char]92 + 'n'
    if ($text.Contains($literalBreak)) { throw "Literal backslash-r/backslash-n in $path" }
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Bare LF in $path" }
}
$patches = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xaml')
foreach ($patch in $patches) {
    [xml]$xml = Get-Content -LiteralPath $patch.FullName -Raw
    foreach ($content in $xml.SelectNodes('//*[local-name()="Content"]')) {
        if (@($content.ChildNodes | Where-Object { $_.NodeType -eq 'Element' }).Count -ne 1) {
            throw "XAML Content must have exactly one root: $($patch.FullName)"
        }
    }
}
Write-Host 'SpectatorEditorBuildTest prebuild checks passed.'

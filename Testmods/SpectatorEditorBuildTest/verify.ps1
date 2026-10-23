$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @(
    (Join-Path $root 'SpectatorEditorBuildTest.csproj'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'verify.ps1')
)
$files += @(Get-ChildItem -LiteralPath (Join-Path $root 'src'),(Join-Path $root 'tests'),(Join-Path $root 'Locales'),(Join-Path $root 'Patches') -Recurse -File | ForEach-Object FullName)
$forbidden = @(
    'System\.Web\.Extensions', 'JavaScriptSerializer', 'System\.Text\.Json',
    'Newtonsoft\.Json', 'DataContractJsonSerializer', 'JsonUtility',
    '\bOnDestroy\s*\(', '\bOnDisable\s*\(', '\bOnApplicationQuit\s*\(',
    '\b(?:Update|LateUpdate|FixedUpdate)\s*\(', '\bStartCoroutine\s*\(',
    '\b(?:CodePatch\.Write|Marshal\.Write(?:Byte|Int16|Int32|Int64)|VirtualProtect)\s*\(',
    '\b(?:Undo|Disable)\s*\(', '\bUnpatchSelf\s*\('
)
$source = (@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine)
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
$patches = @(Get-ChildItem -LiteralPath (Join-Path $root 'Patches') -Recurse -File -Filter '*.xaml')
foreach ($patch in $patches) {
    [xml]$xml = Get-Content -LiteralPath $patch.FullName -Raw
    foreach ($content in $xml.SelectNodes('//*[local-name()="Content"]')) {
        if (@($content.ChildNodes | Where-Object { $_.NodeType -eq 'Element' }).Count -ne 1) {
            throw "XAML Content must have exactly one root: $($patch.FullName)"
        }
    }
    foreach ($control in $xml.SelectNodes('//*[local-name()="Button" or local-name()="CheckBox" or local-name()="TextBox"]')) {
        if (-not $control.HasAttribute('ToolTip') -or $control.GetAttribute('ToolTipService.ShowDuration') -ne '60000') {
            throw 'Every test UI control requires a 60000-ms tooltip.'
        }
    }
}
$english = @{}
foreach ($line in [IO.File]::ReadAllLines((Join-Path $root 'Locales\en-US.txt'))) { if ($line -match '^([^=]+)=(.+)$') { $english[$Matches[1]] = $Matches[2] } }
foreach ($locale in Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -Filter '*.txt') {
    $keys = @([IO.File]::ReadAllLines($locale.FullName) | ForEach-Object { if ($_ -match '^([^=]+)=(.+)$') { $Matches[1] } })
    if (@(Compare-Object @($english.Keys | Sort-Object) @($keys | Sort-Object)).Count) { throw "Locale key mismatch: $($locale.Name)" }
}
foreach ($patch in $patches) {
    $markup = [IO.File]::ReadAllText($patch.FullName)
    foreach ($match in [regex]::Matches($markup, 'Text\[([^\]]+)\]')) {
        if (-not $english.ContainsKey('Tools.' + $match.Groups[1].Value)) { throw "Missing tooltip/text key: $($match.Value)" }
    }
}
if ($source -notmatch 'finally\s*\{ forcedOrder = previous;' -or $source -notmatch 'lock \(engineLock\)') { throw 'Missing force-scope cleanup or native serialization.' }
& (Join-Path $root 'tests\Test-ActionPolicy.ps1')
& (Join-Path $root 'tests\Test-CompactLayout.ps1')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-CreditFocus.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Credit input focus tests failed.' }
& (Join-Path $root 'tests\Test-InstalledContracts.ps1')
& (Join-Path $root 'tests\Test-NativeOrderContract.ps1')
& (Join-Path $root 'tests\Test-NativeActions.ps1')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-OptionalPowers.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Optional ExtremePowers adapter tests failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-NativePowerContext.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Native power context tests failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-OptionalGates.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Optional ExtraFeatures adapter tests failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tests\Test-SpectatorSelection.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Spectator selection integration tests failed.' }
Write-Host 'SpectatorEditorBuildTest prebuild checks passed.'

param([string]$ProjectDir)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectDir).Path
$project = Join-Path $root 'TimerCountdownTest.csproj'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs')
$patches = @(Get-ChildItem -LiteralPath (Join-Path $root 'Patches') -File -Recurse -Filter '*.xaml')
$textFiles = @($sources.FullName) + @($patches.FullName) + @(
    $project,
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'tests\Program.cs'),
    (Join-Path $root 'tests\TimerCountdownTest.Tests.csproj'),
    $PSCommandPath
)

foreach ($file in $textFiles) {
    $content = [IO.File]::ReadAllText($file)
    if ([regex]::IsMatch($content, "(?<!`r)`n")) { throw "CRLF violation: $file" }
    $escapedLineBreak = [string][char]92 + 'r' + [char]92 + 'n'
    if ($content.Contains($escapedLineBreak)) { throw "Literal backslash-r-n sequence: $file" }
}

$runtimeFiles = @($sources.FullName) + @($project)
$runtimeText = [string]::Join("`n", @($runtimeFiles | ForEach-Object { [IO.File]::ReadAllText($_) }))
$badJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
if ($runtimeText -match $badJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw 'Lifecycle teardown requires audit.' }
$pluginText = [IO.File]::ReadAllText((Join-Path $root 'src\TimerCountdownTestPlugin.cs'))
if ($pluginText -match '\b(Update|LateUpdate|FixedUpdate|StartCoroutine|Start)\s*\(') {
    throw 'Plugin must not depend on MonoBehaviour callbacks after startup cleanup.'
}
if ($runtimeText -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|FlushInstructionCache|\.Undo\(|\.Disable\(|NativeDetour|X64InlineHook') {
    throw 'Unexpected executable runtime mutation or hook teardown.'
}
if ($runtimeText.Contains('Assembly-CSharp-publicized.dll')) {
    throw 'Compile against the installed runtime Assembly-CSharp.dll.'
}

foreach ($patch in $patches) {
    [xml]$xml = [IO.File]::ReadAllText($patch.FullName)
    foreach ($content in @($xml.Patch.Operation.Content)) {
        if ($null -eq $content) { continue }
        $elementChildren = @($content.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
        if ($elementChildren.Count -ne 1) { throw "XAML Content must have exactly one root: $($patch.FullName)" }
    }
}

$null = Get-Content -Raw -LiteralPath (Join-Path $root 'info.json') | ConvertFrom-Json
$null = [xml][IO.File]::ReadAllText($project)
Write-Output 'Timer countdown JSON, lifecycle, hook, XAML and CRLF preflight passed.'

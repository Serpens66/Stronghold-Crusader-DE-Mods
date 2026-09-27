$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File)
$projectFile = Get-Item -LiteralPath (Join-Path $projectRoot 'SpectatorPerspectiveTest.csproj')
$runtimeFiles = @($sourceFiles) + @($projectFile)
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenPatch = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Apply\(|\.Undo\(|\.Disable\(|NativeDetour|X64InlineHook'
foreach ($file in $runtimeFiles) {
    $body = [IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($body, $forbiddenJson)) { throw "Forbidden JSON dependency: $($file.FullName)" }
    if ([regex]::IsMatch($body, $forbiddenLifecycle)) { throw "Forbidden lifecycle callback: $($file.FullName)" }
    if ([regex]::IsMatch($body, $forbiddenPatch)) { throw "Unexpected executable hook mutation: $($file.FullName)" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectivePlugin.cs'))
if ($plugin -match '\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(') { throw 'Plugin MonoBehaviour callback found.' }
$runtime = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectiveRuntime.cs'))
if ($runtime -notmatch 'Application\.onBeforeRender\s*\+=' -or $runtime -notmatch 'SPECTATOR_PERSPECTIVE_RUNTIME_ALIVE') {
    throw 'Missing post-cleanup static publisher or runtime marker.'
}
if ($runtime -notmatch 'MapLoaderR3EventHooks\.OnPostLoad' -or $runtime -notmatch 'MapLoaderR3EventHooks\.OnUnloadMap') {
    throw 'Missing map lifecycle event registration.'
}
$textFiles = @($sourceFiles) + @($projectFile) + @(
    (Get-Item -LiteralPath (Join-Path $projectRoot 'build.bat')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'Verify-SpectatorPerspective.ps1')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'info.json')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'))
)
foreach ($file in $textFiles) {
    $body = [IO.File]::ReadAllText($file.FullName)
    $escapedLineEnding = [string][char]92 + 'r' + [string][char]92 + 'n'
    if ([regex]::IsMatch($body, '(?<!\r)\n') -or $body.Contains($escapedLineEnding)) {
        throw "Invalid line endings: $($file.FullName)"
    }
}
[xml]$project = Get-Content -LiteralPath $projectFile.FullName -Raw
$metadata = Get-Content -LiteralPath (Join-Path $projectRoot 'info.json') -Raw | ConvertFrom-Json
if ($metadata.GUID -ne 'SpectatorPerspectiveTest_Serp' -or $metadata.Version -ne '0.1.0') { throw 'Mod metadata mismatch.' }
$patchPath = Join-Path $projectRoot 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'
[xml]$patch = Get-Content -LiteralPath $patchPath -Raw
$contents = @($patch.SelectNodes('/Patch/Operation/Content'))
if ($contents.Count -ne 1) { throw 'Expected exactly one XAML Content node.' }
$elements = @($contents[0].ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
if ($elements.Count -ne 1) { throw 'XAML Content must have exactly one direct root element.' }
Write-Output 'SpectatorPerspectiveTest preflight passed: JSON, lifecycle, hook mutation, publisher, CRLF, project, metadata and XAML root.'

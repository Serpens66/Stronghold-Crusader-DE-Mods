param([string]$ProjectDir)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectDir).Path
$project = Join-Path $root 'AivLobbyPresetTest.csproj'
$files = @(
    $project,
    (Join-Path $root 'src\LobbyPreset.cs'),
    (Join-Path $root 'src\TestSeries.cs'),
    (Join-Path $root 'src\AivLobbyPresetTestPlugin.cs'),
    (Join-Path $root '..\..\Shared\DependencyFreeJson.cs'),
    (Join-Path $root 'CraterLakePreset.json'),
    (Join-Path $root 'AivLobbyTestSeries.json'),
    (Join-Path $root 'AivLobbyStartRebuildRegressionSeries.json'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'tests\AivLobbyPresetTest.Tests.csproj'),
    (Join-Path $root 'tests\Program.cs'),
    (Join-Path $root 'tests\Test-Preflight.ps1')
)
foreach ($file in $files) {
    $content = [IO.File]::ReadAllText($file)
    if ([regex]::IsMatch($content, '(?<!\r)\n')) { throw "CRLF violation: $file" }
}
$sources = $files | Where-Object { $_ -like '*.cs' -or $_ -like '*.csproj' }
$badJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
if (@(Select-String -LiteralPath $sources -Pattern $badJson).Count) { throw 'Forbidden runtime JSON serializer.' }
$runtime = [IO.File]::ReadAllText((Join-Path $root 'src\AivLobbyPresetTestPlugin.cs'))
if ($runtime -match '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\(') { throw 'Lifecycle teardown requires audit.' }
if ($runtime -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Undo\(|\.Disable\(') { throw 'Unexpected runtime hook mutation.' }
$projectText = [IO.File]::ReadAllText($project)
if ($projectText.Contains('Assembly-CSharp-publicized.dll')) {
    throw 'The test mod must compile against the installed runtime Assembly-CSharp.dll.'
}
$sharedLobby = [IO.File]::ReadAllText((Join-Path $root '..\..\APIShared\src\LobbyPreparationOverride.cs'))
if ($sharedLobby -match '\b(view|lobby)\s*==\s*null') {
    throw 'FRONT_Multiplayer null checks must use ReferenceEquals; Noesis overloads ==.'
}
$privateMemberAccess = '\b(?:view|lobby)\.(?:PlayerCap|MPsetupData|selectedMPHeader|RefFileLists|UpdateHostInfo|UpdateRadarShieldPositions|updateSteamIDMappings|ReSortTeamInfo)\b'
if ($runtime -match $privateMemberAccess -or $sharedLobby -match $privateMemberAccess) {
    throw 'Direct access to a private FRONT_Multiplayer member is invalid at runtime.'
}
$document = [xml][IO.File]::ReadAllText($project)
$null = Get-Content -Raw -LiteralPath (Join-Path $root 'CraterLakePreset.json') | ConvertFrom-Json
$null = Get-Content -Raw -LiteralPath (Join-Path $root 'AivLobbyTestSeries.json') | ConvertFrom-Json
$null = Get-Content -Raw -LiteralPath (Join-Path $root 'AivLobbyStartRebuildRegressionSeries.json') | ConvertFrom-Json
$null = Get-Content -Raw -LiteralPath (Join-Path $root 'info.json') | ConvertFrom-Json
Write-Output 'AIV lobby preset JSON, lifecycle, hook and CRLF preflight passed.'

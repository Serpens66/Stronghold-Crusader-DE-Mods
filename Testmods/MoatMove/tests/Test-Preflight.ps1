$ErrorActionPreference = 'Stop'
$modDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$project = [IO.File]::ReadAllText((Join-Path $modDir 'MoatMove.csproj'))
$runtimePaths = @($project.Project.ItemGroup.Compile | Where-Object { $_.Include } | ForEach-Object { [IO.Path]::GetFullPath((Join-Path $modDir $_.Include)) })
$forbidden = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization|OnDestroy\s*\(|OnDisable\s*\(|OnApplicationQuit\s*\(|\b(?:Update|LateUpdate|FixedUpdate)\s*\(|StartCoroutine\s*\('
foreach ($file in @($runtimePaths) + @(Join-Path $modDir 'MoatMove.csproj')) {
    $text = [IO.File]::ReadAllText($file)
    if ([regex]::IsMatch($text, $forbidden)) { throw "Forbidden JSON/lifecycle dependency: $file" }
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Bare LF: $file" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $modDir 'src\MoatMovePlugin.cs'))
if ($plugin -match '\.Dispose\s*\(' -or $plugin -notmatch 'private static FriendlyMoatMovementRuntime runtime;' -or $plugin -notmatch 'private static ManualLogSource persistentLog;') { throw 'Invalid process lifetime ownership.' }
foreach ($path in $runtimePaths) {
    if ([IO.File]::ReadAllText($path) -match '\bruntime\??\.Dispose\s*\(') { throw "Published runtime teardown: $path" }
}
foreach ($reference in $project.Project.ItemGroup.Reference) {
    if ($reference.Include -in @('APIShared','BugfixesAndQoL')) { throw 'Standalone mod references another mod.' }
}
$textPaths = @(Get-ChildItem -LiteralPath $modDir,(Join-Path $modDir 'src'),$PSScriptRoot -File | Where-Object { $_.Extension -in @('.cs','.csproj','.ps1','.py','.bat','.json','.md') })
foreach ($file in $textPaths) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Bare LF: $($file.FullName)" }
}
$manifest = [IO.File]::ReadAllText((Join-Path $modDir 'info.json')) | ConvertFrom-Json
if ($manifest.Version -ne '0.1.0' -or $manifest.GUID -ne 'MoatMove_Serp' -or $manifest.NetworkMode -ne 1) { throw 'Manifest identity mismatch.' }
if ($plugin -notmatch 'PluginVersion = "0\.1\.0"' -or $plugin -notmatch 'AssemblyVersion\("0\.1\.0\.0"\)' -or $plugin -notmatch 'AssemblyFileVersion\("0\.1\.0\.0"\)') { throw 'Assembly version mismatch.' }
Write-Output "PASS preflight: $($runtimePaths.Count) runtime sources, JSON/lifecycle rules, process ownership, standalone references, CRLF and version 0.1.0."

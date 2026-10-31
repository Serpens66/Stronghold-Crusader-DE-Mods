$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $project 'tools\Verify-Native.py')
if ($LASTEXITCODE -ne 0) { throw 'Candidate native audit failed.' }
$files = @(Get-ChildItem -LiteralPath $project -Recurse -File | Where-Object {
    $_.Extension -in '.cs','.csproj','.ps1','.py','.md','.json','.bat' -and $_.FullName -notmatch '[\\/](bin|obj|BepInEx|TestResults)[\\/]'
})
foreach ($file in $files) {
    if ([IO.File]::ReadAllText($file.FullName) -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $($file.FullName)" }
}
# Narrow guardrails for the permanent ownership and runtime serialization boundaries;
# behavior/ABI are verified by the tests, not by private class-name expectations here.
$runtime = (Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($runtime -match 'Newtonsoft|System.Text.Json|JavaScriptSerializer|JsonUtility|\b(OnDestroy|OnDisable|Update|FixedUpdate|LateUpdate)\s*\(\s*\)|\.Disable\(|\.Undo\(') { throw 'Runtime serialization, teardown or repatching boundary violated.' }
if ($runtime -match 'CreateBuilding\(|FindPath\(|GetUnitsAsSpan\(|GetTreesAsSpan\(') { throw 'Extra native query/search or broad worker/tree scan in safety runtime.' }
$manifest = Get-Content -LiteralPath (Join-Path $project 'info.json') -Raw | ConvertFrom-Json
if ($manifest.NetworkMode -ne 1 -or @($manifest.Dependencies | Where-Object GUID -eq 'AIBuildDiagnoseTest_Serp').Count -ne 1) { throw 'Gameplay network mode / paired diagnostic dependency missing.' }
Write-Host 'PASS: native candidate ownership, runtime guardrails, hard dependencies and CRLF.'

[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$gameDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$msbuildPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
[xml]$project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ArabianIdleSoundTest.csproj'))
$sources = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_.Include)).Path
})
foreach ($path in @((Join-Path $PSScriptRoot 'ArabianIdleSoundTest.csproj')) + $sources) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match 'JavaScriptSerializer|System\.Web\.Extensions|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility|publicized') { throw "Forbidden runtime reference: $path" }
    if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|Start|OnEnable)\s*\(|\bStartCoroutine\s*\(') { throw "Unaudited runtime lifecycle: $path" }
    if ($text -match '\bVirtualProtect\s*\(|\bFlushInstructionCache\s*\(|\bCodePatch\.Write\s*\(|\bMarshal\.Write\w*\s*\(|\.(?:Disable|Undo)\s*\(') { throw "Executable mutation/teardown: $path" }
}
$runtimePath = Join-Path $PSScriptRoot 'src/SoundRuntime.cs'
$runtime = [IO.File]::ReadAllText($runtimePath)
if ($runtime -match '\.Assembly\.Location|TryGetUnitById' -or
    [regex]::Matches($runtime, 'APIShared\.UnitAccess\.TryGetById').Count -ne 2 -or
    $runtime -notmatch 'new InstalledAudioFiles\(BepInEx.Paths.GameRootPath\)') { throw 'Stable binary paths/central UnitAccess contract changed' }
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/Plugin.cs'))
if ($plugin.IndexOf('; waiting for native library;') -gt $plugin.IndexOf('LibraryLoaded += LibraryLoaded')) { throw 'Waiting message must precede synchronous library callback' }
$rollbackStart = $runtime.IndexOf('private void RollbackUnpublished()')
$rollbackEnd = $runtime.IndexOf('private void OnUnit()', $rollbackStart)
if ($rollbackStart -lt 0 -or $rollbackEnd -lt 0) { throw 'Missing unpublished rollback boundary' }
$rollback = $runtime.Substring($rollbackStart, $rollbackEnd - $rollbackStart)
if ($rollback -notmatch 'if \(publicationStarted\) throw' -or $rollback -notmatch 'unitHook.IsInstalled' -or
    $rollback -notmatch 'emitHook.IsInstalled' -or $rollback -notmatch 'managedHook.IsApplied') { throw 'Unpublished rollback guards changed' }
if ([regex]::Matches($runtime, 'RollbackUnpublished\(\);').Count -ne 1 -or
    $runtime -notmatch 'catch \{ RollbackUnpublished\(\); throw; \}' -or
    [regex]::Matches($runtime, '\.Enable\s*\(').Count -ne 2 -or
    [regex]::Matches($runtime, '\.Apply\s*\(').Count -ne 1 -or
    $runtime.IndexOf('publicationStarted = true;') -gt $runtime.IndexOf('managedHook.Apply();') -or
    $runtime.IndexOf('SoundPolicy.SetReady(true);') -lt $runtime.IndexOf('emitHook.Enable();')) { throw 'Permanent publication contract changed' }
foreach ($path in $sources | Where-Object { $_ -like '*ArabianIdleSoundTest\src\*' }) {
    $text = [IO.File]::ReadAllText($path)
    if ($path -eq $runtimePath) { $text = $text.Remove($rollbackStart, $rollbackEnd - $rollbackStart) }
    if ($text -match '\.(?:Dispose|Undo|Disable)\s*\(|public\s+void\s+Dispose\s*\(') { throw "Published runtime teardown: $path" }
}
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json','.py')
}) {
    if ([IO.File]::ReadAllText($file.FullName) -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
& (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/ArabianIdleSoundTest'
& (Join-Path $PSScriptRoot 'tests/Test-SharedGateRegression.ps1')
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'tests/native_audit.py') $workspace $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Native span/control-flow regression failed' }
# This builds only the isolated test host, never the mod or installed outputs.
& $msbuildPath (Join-Path $PSScriptRoot 'tests/ArabianIdleSoundTests.csproj') /nologo /verbosity:minimal /p:Configuration=Debug
if ($LASTEXITCODE -ne 0) { throw 'Isolated test host compilation failed' }
& (Join-Path $PSScriptRoot 'tests/bin/Debug/ArabianIdleSoundTests.exe') $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Installed-member, sound transport, IL or backend tests failed' }
Write-Host 'PASS: ArabianIdleSoundTest runtime preflight, CRLF, native audit and isolated tests.'

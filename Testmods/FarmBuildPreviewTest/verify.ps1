[CmdletBinding()]
param([string]$ExtenderDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
. (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$identity = Get-Content -Raw -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') | ConvertFrom-Json
$hash = Get-Sha256Hex -Path $native
if ($hash -ine $identity.currentNativeHash -or $hash -ine 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') { throw 'Native baseline differs.' }
$mod = [pscustomobject]@{ Name='FarmBuildPreviewTest'; Project='Testmods\FarmBuildPreviewTest\FarmBuildPreviewTest.csproj'; Plugin='Testmods\FarmBuildPreviewTest\src\FarmBuildPreviewTestPlugin.cs' }
Assert-SERuntimeModPreflight $mod $workspace
$runtimeFiles = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -File -Filter '*.cs')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\managed-contract.ps1') -GameDir 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if ($LASTEXITCODE -ne 0) { throw 'Real managed contracts failed.' }
$runtime = ($runtimeFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join [Environment]::NewLine
if ($runtime -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|InvokeRepeating|Update|LateUpdate|FixedUpdate)\s*\(') { throw 'Forbidden runtime lifecycle/scheduling path.' }
if ($runtime -match 'permanentHook\??\.(Dispose|Undo|Disable)\s*\(|CodePatch\.Write|Marshal\.Write|VirtualProtect\s*\(') { throw 'Published hook teardown/executable rewrite.' }
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\FarmBuildPreviewTestPlugin.cs'))
if ([regex]::Matches($plugin, 'permanentHook\.Original\(').Count -ne 1) { throw 'Vanilla must be called exactly once.' }
if ([regex]::Matches($plugin, 'candidate\?\.Dispose\(').Count -ne 1 -or $plugin -notmatch 'if \(!published\)\s*\{[^}]*candidate\?\.Dispose\(') { throw 'Candidate rollback must be guarded before publication.' }
if ($runtime -notmatch 'Volatile\.Read\(ref active\)' -or $runtime -notmatch 'Volatile\.Write\(ref active, 0\)' -or $runtime -notmatch 'FARM_PREVIEW_RUNTIME_CONFIRMED') { throw 'Permanent logical gate/runtime marker missing.' }
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](obj|bin|BepInEx)[\\/]' -and $_.Extension -match '^\.(cs|csproj|json|md|ps1|bat|py|txt|xaml)$' }) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ($content -match '(?<!\r)\n' -or $content.Contains([string]::Concat('\','r','\','n'))) { throw "Invalid line endings: $($file.FullName)" }
}
$backend = Join-Path $ExtenderDir 'RedBird.Backends.NativeX64.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($backend).Version -ne [version]'1.5.0.0') { throw 'Unreviewed RedBird backend.' }
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
& $python (Join-Path $PSScriptRoot 'tests\native-audit.py') $native $workspace
if ($LASTEXITCODE -ne 0) { throw 'Native machine audit failed.' }
& $python (Join-Path $PSScriptRoot 'tests\presentation-check.py') $workspace
if ($LASTEXITCODE -ne 0) { throw 'Presentation/locale composition failed.' }
Write-Host "PASS: native hash=$hash; runtime JSON/lifecycle, permanent hooks, CRLF and complete native incoming-edge audit."

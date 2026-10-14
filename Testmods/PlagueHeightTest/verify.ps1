[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if ((Get-Sha256Hex -Path $native) -cne 'fbcb93195fc7efca9bdac5204852efdd76f9818f59a6711750d77c9cef2831e2') { throw 'Unknown native DLL; cannot build this test fix.' }
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
$backendHashes = @{
    'RedBird.X64.dll' = 'e085b83b2b87b5d2142c980090d8d9ec616a90a443bd125c17b41e0bb47a44eb'
    'RedBird.Core.dll' = 'ee4b036c486077f7d8898ae054f124edde73e6ae1a059e8991c361e2b720d29f'
    'RedBird.Abstractions.dll' = '339cece116f66f59ecc7da5e9a5c0b2dfe24f4e7a5e7efc005864fa77c027a1e'
    'Iced.dll' = 'cc915d1c2e7be60933c5b129842ca3d08d1fe3cda6c6d6ade8824dcdde16191f'
}
foreach ($name in $backendHashes.Keys) { if ((Get-Sha256Hex (Join-Path $extender $name)) -cne $backendHashes[$name]) { throw "Installed backend changed: $name" } }
& (Join-Path $workspace 'Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory (Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp')
if ($LASTEXITCODE -ne 0) { throw 'Canonical APIShared package verification failed.' }
$files = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.py') })
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Bare LF: $($file.FullName)" }
    if ($file.Extension -eq '.cs' -and $file.FullName -notmatch '[\\/]tests[\\/]') {
        if ($text -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility') { throw "Forbidden runtime JSON: $($file.Name)" }
        if ($text -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine|InvokeRepeating)\s*\(') { throw "Forbidden lifecycle path: $($file.Name)" }
        if ($text -match '\.(?:Enable|Disable|Undo|Apply|Unpatch|UnpatchAll)\s*\(|CodePatch\.Write') { throw "Runtime hook mutation: $($file.Name)" }
        if ($text -match 'VirtualProtect|FlushInstructionCache|VirtualFree' -and $file.Name -ne 'PreparedNativeCode.cs') { throw "Unexpected code memory operation: $($file.Name)" }
        if ($text -match 'Assembly-CSharp-publicized|System.Runtime.Serialization.Json') { throw "Forbidden reference: $($file.Name)" }
    }
}
$prepared = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\PreparedNativeCode.cs'))
if ($prepared -notmatch 'if \(published\) throw' -or @([regex]::Matches($prepared, 'if \(!VirtualProtect')).Count -ne 1) { throw 'Native code preparation/publication boundary changed.' }
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\PlagueHeightRuntime.cs'))
if ($runtime -notmatch 'if \(published\) throw' -or $runtime -match 'public void Dispose') { throw 'Published transaction teardown boundary changed.' }
[xml]$project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PlagueHeightTest.csproj'))
foreach ($node in $project.SelectNodes('//*[local-name()="Reference"]')) {
    if ([string]$node.Include -match 'Json|Serialization|Web.Extensions') { throw "Forbidden assembly: $($node.Include)" }
}
. (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
Assert-SERuntimeModPreflight ([pscustomobject]@{ Name='PlagueHeightTest'; Project='Testmods\PlagueHeightTest\PlagueHeightTest.csproj'; Plugin='Testmods\PlagueHeightTest\src\PlagueHeightTestPlugin.cs' }) $workspace
$metadata = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'info.json') | ConvertFrom-Json
if ($metadata.GUID -ne 'PlagueHeightTest_Serp' -or $metadata.Version -ne '0.1.0' -or $metadata.NetworkMode -ne 1) { throw 'Metadata mismatch.' }
# The global published-hook checker currently excludes most Testmods. Cover new runtime separately above.
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1')
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'tests\audit_native.py') $native
if ($LASTEXITCODE -ne 0) { throw 'Native audit regression failed.' }
& dotnet build (Join-Path $PSScriptRoot 'tests\PlagueHeightTest.Tests.csproj') --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Native tests compilation failed.' }
& dotnet (Join-Path $PSScriptRoot 'tests\bin\Debug\net10.0\PlagueHeightTest.Tests.dll') $native
if ($LASTEXITCODE -ne 0) { throw 'Native execution tests failed.' }
Write-Output 'PASS: PlagueHeightTest preflight, CRLF, JSON/lifecycle, native control flow, installed backend and execution tests.'

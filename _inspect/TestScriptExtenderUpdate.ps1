[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

$inventory = @(Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json)
Assert-True ($inventory.Count -eq 28) 'Expected 28 runtime mods.'
Assert-True (@($inventory | Where-Object Plugin).Count -eq 27) 'Expected 27 C# runtime mods.'
$order = @(Get-SEBuildOrder $inventory)
Assert-True ($order[0].Name -eq 'SerpNativeAPI') 'SerpNativeAPI must build first.'
Assert-True ([array]::IndexOf([string[]]$order.Name, 'APITest') -gt [array]::IndexOf([string[]]$order.Name, 'SerpNativeAPI')) 'APITest must follow SerpNativeAPI.'
Assert-True ($order[-1].Name -eq 'BugfixesAndQoL') 'BugfixesAndQoL must build last.'

$categories = Get-SEChangeCategories @('src/SHCDESE.BepInEx/Detours/Test.cs','src/SHCDESE.BepInEx/Interop/Test.cs','ReverseEngineering/structs/test.h','src/SHCDESE.BepInEx/API/Test.cs','deps/Override/Test.xaml','docs/test.md')
Assert-True ($categories.Native.Count -eq 3) 'Native/AOB/interop classification failed.'
Assert-True ($categories.ManagedApi.Count -eq 1) 'Managed API classification failed.'
Assert-True ($categories.Assets.Count -eq 1) 'Asset classification failed.'
Assert-True ($categories.Documentation.Count -eq 1) 'Documentation classification failed.'

$validRange=[pscustomobject]@{MinimumScriptExtenderVersion='2.2.0';MaximumScriptExtenderVersion=''}
Assert-SEManifestExtenderRange $validRange '2.3.0' 'valid'
$boundedRange=[pscustomobject]@{MinimumScriptExtenderVersion='2.2.0';MaximumScriptExtenderVersion='2.3.0'}
Assert-SEManifestExtenderRange $boundedRange '2.3.0' 'bounded'
$rangeFailed=$false
try { Assert-SEManifestExtenderRange ([pscustomobject]@{MinimumScriptExtenderVersion='2.2.0';MaximumScriptExtenderVersion='2.2.9'}) '2.3.0' 'excluded' } catch { $rangeFailed=$true }
Assert-True $rangeFailed 'MaximumScriptExtenderVersion did not exclude an unsupported target.'

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$temp = Join-Path $tempBase ('SHCDE-SEUpdateTests-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    & git -C $temp init --quiet; & git -C $temp config user.email test@example.invalid; & git -C $temp config user.name Test
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "one`r`n", [Text.UTF8Encoding]::new($false))
    & git -C $temp add tracked.txt; & git -C $temp commit --quiet -m initial
    $commit=(& git -C $temp rev-parse HEAD).Trim();$tree=(& git -C $temp rev-parse 'HEAD^{tree}').Trim()
    Assert-True (Test-SEGitIdentity $temp $commit $tree) 'Clean Git identity was rejected.'
    [IO.File]::WriteAllText((Join-Path $temp 'ignored.tmp'), 'ignored', [Text.UTF8Encoding]::new($false))
    Assert-True (Test-SEGitIdentity $temp $commit $tree) 'Untracked build-like artifact changed provenance.'
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "two`r`n", [Text.UTF8Encoding]::new($false))
    Assert-True (-not (Test-SEGitIdentity $temp $commit $tree)) 'Tracked modification was not detected.'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

$missing = Join-Path $tempBase ('missing-' + [Guid]::NewGuid().ToString('N'))
$failed=$false;try{Resolve-SEExtenderDirectory '' $missing|Out-Null}catch{$failed=$true}
Assert-True $failed 'Missing installed extender did not fail closed.'
$alternate = Join-Path $tempBase ('alternate-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($alternate) | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $alternate 'SHCDESE.dll'), [byte[]]@(0))
    Assert-True ((Resolve-SEExtenderDirectory $alternate $missing) -eq (Resolve-Path -LiteralPath $alternate).Path) 'Explicit ExtenderDir was not selected.'
}
finally {
    if (Test-Path -LiteralPath $alternate) { Remove-Item -LiteralPath $alternate -Recurse -Force }
}

$buildTemp = Join-Path $tempBase ('SHCDE-SEBuildTests-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($buildTemp) | Out-Null
    $hitOne=Join-Path $buildTemp 'one.hits';$hitTwo=Join-Path $buildTemp 'two.hits'
    $nl=[Environment]::NewLine
    $oneText='@echo off'+$nl+'echo hit>>"'+$hitOne+'"'+$nl+'exit /b 0'+$nl
    $twoFailText='@echo off'+$nl+'echo hit>>"'+$hitTwo+'"'+$nl+'exit /b 7'+$nl
    $twoPassText='@echo off'+$nl+'echo hit>>"'+$hitTwo+'"'+$nl+'exit /b 0'+$nl
    [IO.File]::WriteAllText((Join-Path $buildTemp 'one.bat'), $oneText, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path $buildTemp 'two.bat'), $twoFailText, [Text.Encoding]::ASCII)
    $fakeMods=@(
        [pscustomobject]@{Name='One';Plugin='one';BuildDriver='one.bat';BuildOrder=1;DependsOn=@()},
        [pscustomobject]@{Name='Two';Plugin='two';BuildDriver='two.bat';BuildOrder=2;DependsOn=@('One')}
    )
    $fakeState=@{CompletedBuilds=@()}
    $stopped=$false
    try { Invoke-SECheckpointBuild $fakeMods $buildTemp $buildTemp $fakeState {} {} } catch { $stopped=$true }
    Assert-True ($stopped -and $fakeState.CompletedBuilds.Count -eq 1 -and $fakeState.CompletedBuilds[0] -eq 'One') 'Failed build did not preserve the last safe checkpoint.'
    [IO.File]::WriteAllText((Join-Path $buildTemp 'two.bat'), $twoPassText, [Text.Encoding]::ASCII)
    Invoke-SECheckpointBuild $fakeMods $buildTemp $buildTemp $fakeState {} {}
    Assert-True (@(Get-Content -LiteralPath $hitOne).Count -eq 1) 'Resume rebuilt an already successful mod.'
    Assert-True ($fakeState.CompletedBuilds.Count -eq 2) 'Resume did not complete the remaining build.'
}
finally {
    if (Test-Path -LiteralPath $buildTemp) { Remove-Item -LiteralPath $buildTemp -Recurse -Force }
}

$projects=@($inventory|Where-Object Plugin|ForEach-Object{Join-Path $workspace $_.Project})
foreach($project in $projects){$text=[IO.File]::ReadAllText($project);$installed=$text.IndexOf('BepInEx\plugins\000shcdese</ExtenderDir>');$local=$text.IndexOf("LocalScriptExtenderBuildOutput)\SHCDESE.dll");Assert-True ($installed-ge0-and($local-lt0-or$installed-lt$local)) "Installed extender is not first in $project"}
foreach($driver in @($inventory|Where-Object Plugin|ForEach-Object{Join-Path $workspace $_.BuildDriver})){Assert-True ([IO.File]::ReadAllText($driver).Contains('SHCDESE_EXTENDER_DIR')) "Explicit override missing in $driver"}

Write-Host 'PASS: Script Extender update inventory, classification, provenance, reference selection and build order.'

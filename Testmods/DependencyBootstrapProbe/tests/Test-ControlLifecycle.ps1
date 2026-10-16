$ErrorActionPreference='Stop'
$probe=Split-Path -Parent $PSScriptRoot
& (Join-Path $probe 'Test-CorrectionControl.ps1')
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'control-lifecycle-fixture'))
if(-not $root.StartsWith([IO.Path]::GetFullPath($PSScriptRoot)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture root escaped tests.'}
$directory=Join-Path $root 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp'
$target=Join-Path $directory 'info.json'
[IO.Directory]::CreateDirectory($directory)|Out-Null
$source=Join-Path $probe 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp\info.json'
Copy-Item -LiteralPath $source -Destination $target -Force
$remove=Join-Path $probe 'Remove-CorrectionControl.ps1'
& $remove -GameDir $root -WhatIf
if(-not (Test-Path -LiteralPath $target)){throw 'WhatIf removed control.'}
$extra=Join-Path $directory 'foreign.txt'
[IO.File]::WriteAllText($extra,'synthetic unrelated file')
$rejected=$false
try { & $remove -GameDir $root } catch { $rejected=$true }
if(-not $rejected -or -not (Test-Path -LiteralPath $target) -or -not (Test-Path -LiteralPath $extra)){throw 'Foreign-file guard failed.'}
Remove-Item -LiteralPath $extra
[IO.File]::AppendAllText($target,' ')
$rejected=$false
try { & $remove -GameDir $root } catch { $rejected=$true }
if(-not $rejected -or -not (Test-Path -LiteralPath $target)){throw 'Changed-file guard failed.'}
Copy-Item -LiteralPath $source -Destination $target -Force
& $remove -GameDir $root
if(Test-Path -LiteralPath $directory){throw 'Exact control directory was not removed.'}
& $remove -GameDir $root
Write-Host 'PASS: control cleanup WhatIf, unrelated-file preservation, changed-hash preservation, exact removal and repeated removal.'

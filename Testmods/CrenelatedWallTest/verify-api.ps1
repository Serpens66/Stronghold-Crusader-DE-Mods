$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$installedApi = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\APIShared_Serp\APIShared.dll'
if ((Get-Sha256Hex $installedApi) -cne '2797b06042b0634ccc97ee5d2175c96944f95edfa1924d764776d9a6bde14758' -or
    [Reflection.AssemblyName]::GetAssemblyName($installedApi).Version.ToString() -cne '0.9.2.0') { throw 'The user-approved installed APIShared 0.9.2 identity changed.' }
foreach ($test in @('api-unchanged.py','installed-api.py')) {
    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot ('tests\' + $test)) check $workspace
    if ($LASTEXITCODE -ne 0) { throw 'APIShared dependency changed; this testmod must not rebuild or copy it.' }
}
Write-Output 'PASS: explicit testmod exception uses the existing installed APIShared 0.9.2 SDK; development and installed packages unchanged.'

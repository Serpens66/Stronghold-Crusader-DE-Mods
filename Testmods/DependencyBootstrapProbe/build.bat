@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/DependencyBootstrapProbe/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
set "PROBE_NOPAUSE=0"
set "PROBE_NOINSTALL="
set "PROBE_TRIAL="
set "PROBE_LIFECYCLE="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "PROBE_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "PROBE_NOINSTALL=-NoInstall"
for %%A in (%*) do if /I "%%~A"=="/correctiontrial" set "PROBE_TRIAL=-CorrectionTrial"
for %%A in (%*) do if /I "%%~A"=="/lifecycle" set "PROBE_LIFECYCLE=-LifecycleObservation"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %PROBE_NOINSTALL% %PROBE_TRIAL% %PROBE_LIFECYCLE%
set "PROBE_RESULT=%ERRORLEVEL%"
if "%PROBE_NOPAUSE%"=="0" pause
exit /b %PROBE_RESULT%

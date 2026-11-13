@echo off
rem SHCDE_BUILD_PARAMETERS: explicit SDK, installation suffix and no-install settings from the dispatcher.
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../." -Driver "Spectator/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
echo [%date% %time%] START Spectator
set "NO_INSTALL=0"
if defined SHCDE_PREPARE_ONLY set "NO_INSTALL=%SHCDE_PREPARE_ONLY%"
set "NO_PAUSE=0"
for %%A in (%*) do (
 if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
 if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
)
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" goto finish_failed
rem Build.ps1 validates, compiles and packages; only its final step installs into the game.
if "%NO_INSTALL%"=="1" goto prepared_no_install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -ApiSharedDir "%API_SHARED_DIR%" -NoInstall 0
if not "%ERRORLEVEL%"=="0" goto finish_failed
goto write_manifest

:prepared_no_install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -ApiSharedDir "%API_SHARED_DIR%" -NoInstall 1
if not "%ERRORLEVEL%"=="0" goto finish_failed

:write_manifest
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName Spectator
if not "%ERRORLEVEL%"=="0" goto finish_failed
set "RESULT=0"
goto finish

:finish_failed
set "RESULT=1"
:finish
echo [%date% %time%] END: exit code %RESULT%
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%

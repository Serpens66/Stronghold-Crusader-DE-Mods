@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Helpers/MapParser/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START MapParser
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" goto :build_driver_directory_failed
rem The outer driver owns the pause, including failures before compilation.
call :build_driver_main %* /nopause
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:build_driver_directory_failed
echo ERROR: Cannot enter the build directory "%~dp0".
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b 1

:build_driver_main
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "PARSER_EXE=%PROJECT_DIR%MapParser.Cli\bin\Release\net10.0\MapParser.exe"

where dotnet >nul 2>nul
if not "%ERRORLEVEL%"=="0" (
  echo Das .NET SDK wurde nicht gefunden.
  echo Installiere das .NET 10 SDK und starte build.bat danach erneut.
  exit /b 1
)

pushd "%PROJECT_DIR%"

echo Baue MapParser in der Release-Konfiguration...
echo [%date% %time%] Compile projects
dotnet build "MapParser.sln" -c Release
if not "%ERRORLEVEL%"=="0" (
  popd
  echo.
  echo Build fehlgeschlagen.
  exit /b 1
)

echo.
echo Fuehre die synthetischen Tests aus...
echo [%date% %time%] Run tests
dotnet run --project "MapParser.Tests\MapParser.Tests.csproj" -c Release --no-build
if not "%ERRORLEVEL%"=="0" (
  popd
  echo.
  echo Mindestens ein Test ist fehlgeschlagen.
  exit /b 1
)

popd

if not exist "%PARSER_EXE%" (
  echo.
  echo Build meldete Erfolg, aber MapParser.exe wurde nicht gefunden:
  echo %PARSER_EXE%
  exit /b 1
)

echo.
echo Build und Tests waren erfolgreich.
echo Der Parser liegt hier:
echo %PARSER_EXE%
exit /b 0

@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START AIVParser
cd /d "%~dp0"
if errorlevel 1 goto :build_driver_directory_failed
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
set "PARSER_EXE=%PROJECT_DIR%AIVParser.Cli\bin\Release\net10.0\AIVParser.exe"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo Das .NET SDK wurde nicht gefunden.
  echo Installiere das .NET 10 SDK und starte build.bat danach erneut.
  echo.
  exit /b 1
)

pushd "%PROJECT_DIR%"

echo Baue AIVParser in der Release-Konfiguration...
echo.
echo [%date% %time%] Compile projects
dotnet build "AIVParser.sln" -c Release
if errorlevel 1 (
  popd
  echo.
  echo Build fehlgeschlagen.
  echo.
  exit /b 1
)

echo.
echo Fuehre die automatischen Tests aus...
echo.
echo [%date% %time%] Run tests
dotnet run --project "AIVParser.Tests\AIVParser.Tests.csproj" -c Release --no-build
if not "%ERRORLEVEL%"=="0" (
  popd
  echo.
  echo Mindestens ein Test ist fehlgeschlagen.
  echo.
  exit /b 1
)

popd

if not exist "%PARSER_EXE%" (
  echo.
  echo Build meldete Erfolg, aber AIVParser.exe wurde nicht gefunden:
  echo %PARSER_EXE%
  echo.
  exit /b 1
)

echo.
echo Build und Tests waren erfolgreich.
echo Der Parser liegt hier:
echo %PARSER_EXE%
echo.
echo Die Anwendung wird in README.md erklaert.
echo.
exit /b 0

@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "PARSER_EXE=%PROJECT_DIR%AIVParser.Cli\bin\Release\net10.0\AIVParser.exe"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo Das .NET SDK wurde nicht gefunden.
  echo Installiere das .NET 10 SDK und starte build.bat danach erneut.
  echo.
  pause
  exit /b 1
)

pushd "%PROJECT_DIR%"

echo Baue AIVParser in der Release-Konfiguration...
echo.
dotnet build "AIVParser.sln" -c Release
if errorlevel 1 (
  popd
  echo.
  echo Build fehlgeschlagen.
  echo.
  pause
  exit /b 1
)

echo.
echo Fuehre die automatischen Tests aus...
echo.
dotnet run --project "AIVParser.Tests\AIVParser.Tests.csproj" -c Release --no-build
if errorlevel 1 (
  popd
  echo.
  echo Mindestens ein Test ist fehlgeschlagen.
  echo.
  pause
  exit /b 1
)

popd

if not exist "%PARSER_EXE%" (
  echo.
  echo Build meldete Erfolg, aber AIVParser.exe wurde nicht gefunden:
  echo %PARSER_EXE%
  echo.
  pause
  exit /b 1
)

echo.
echo Build und Tests waren erfolgreich.
echo Der Parser liegt hier:
echo %PARSER_EXE%
echo.
echo Die Anwendung wird in README.md erklaert.
echo.
pause
exit /b 0

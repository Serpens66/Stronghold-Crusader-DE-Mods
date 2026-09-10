@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "PARSER_EXE=%PROJECT_DIR%MapParser.Cli\bin\Release\net10.0\MapParser.exe"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo Das .NET SDK wurde nicht gefunden.
  echo Installiere das .NET 10 SDK und starte build.bat danach erneut.
  exit /b 1
)

pushd "%PROJECT_DIR%"

echo Baue MapParser in der Release-Konfiguration...
dotnet build "MapParser.sln" -c Release
if errorlevel 1 (
  popd
  echo.
  echo Build fehlgeschlagen.
  exit /b 1
)

echo.
echo Fuehre die synthetischen Tests aus...
dotnet run --project "MapParser.Tests\MapParser.Tests.csproj" -c Release --no-build
if errorlevel 1 (
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

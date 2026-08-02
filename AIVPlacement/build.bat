@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo Das .NET SDK wurde nicht gefunden.
  echo Installiere das .NET 10 SDK und starte build.bat danach erneut.
  exit /b 1
)

pushd "%PROJECT_DIR%"

echo Baue AIVPlacement in der Release-Konfiguration...
dotnet build "AIVPlacement.sln" -c Release
if errorlevel 1 (
  popd
  echo.
  echo Build fehlgeschlagen.
  exit /b 1
)

echo.
echo Fuehre die synthetischen Tests aus...
dotnet run --project "AIVPlacement.Tests\AIVPlacement.Tests.csproj" -c Release --no-build
if errorlevel 1 (
  popd
  echo.
  echo Mindestens ein Test ist fehlgeschlagen.
  exit /b 1
)

popd

echo.
echo Build und Tests waren erfolgreich.
echo Die paketfreie Bibliothek liegt unter AIVPlacement.Core\bin\Release\netstandard2.0.
exit /b 0

@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "SOLUTION=%PROJECT_DIR%TrailEditor.sln"
set "CLI_EXE=%PROJECT_DIR%TrailEditor.Cli\bin\Release\net10.0\TrailEditor.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] FEHLER: dotnet wurde nicht gefunden.
    exit /b 1
)

pushd "%PROJECT_DIR%" || exit /b 1

echo [%date% %time%] Baue TrailEditor im Release-Modus ...
dotnet build "%SOLUTION%" -c Release
if errorlevel 1 goto :failed

echo [%date% %time%] Fuehre TrailEditor-Tests aus ...
dotnet run --project "%PROJECT_DIR%TrailEditor.Tests\TrailEditor.Tests.csproj" -c Release --no-build
if errorlevel 1 goto :failed

popd

if not exist "%CLI_EXE%" (
    echo [%date% %time%] FEHLER: Das erwartete Programm wurde nicht erzeugt: "%CLI_EXE%"
    exit /b 1
)

echo [%date% %time%] TrailEditor wurde erfolgreich gebaut und getestet.
echo [%date% %time%] Programm: "%CLI_EXE%"
exit /b 0

:failed
set "BUILD_EXIT=%errorlevel%"
popd
echo [%date% %time%] FEHLER: Build oder Tests fehlgeschlagen ^(Exitcode %BUILD_EXIT%^).
exit /b %BUILD_EXIT%

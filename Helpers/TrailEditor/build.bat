@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START TrailEditor
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
set "SOLUTION=%PROJECT_DIR%TrailEditor.sln"
set "CLI_EXE=%PROJECT_DIR%TrailEditor.Cli\bin\Release\net10.0\TrailEditor.exe"

if not defined TrailEditorDependencyRoot (
    for %%I in ("%PROJECT_DIR%..\..") do set "TrailEditorDependencyRoot=%%~fI"
)
if not defined TrailEditorMapParserProject set "TrailEditorMapParserProject=%TrailEditorDependencyRoot%\Helpers\MapParser\MapParser.Core\MapParser.Core.csproj"
if not defined TrailEditorAivDecoderSourceRoot set "TrailEditorAivDecoderSourceRoot=%TrailEditorDependencyRoot%\shcde-script-extender\src\SHCDESE.AIVDecoder\src\SHCDESE.AIVDecoder"
if not defined TrailEditorAicDecoderSourceRoot set "TrailEditorAicDecoderSourceRoot=%TrailEditorDependencyRoot%\shcde-script-extender\src\SHCDESE.AICDecoder\src\SHCDESE.AICDecoder"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] FEHLER: dotnet wurde nicht gefunden. Installiere das .NET 10 SDK und stelle sicher, dass dotnet ueber PATH erreichbar ist.
    goto :configurationFailed
)

dotnet --list-sdks | %SystemRoot%\System32\findstr.exe /B /C:"10." >nul 2>&1
if errorlevel 1 (
    echo [%date% %time%] FEHLER: Das .NET 10 SDK wurde nicht gefunden.
    echo [%date% %time%] Eine Runtime allein reicht zum Bauen nicht aus.
    goto :configurationFailed
)

if not exist "%TrailEditorMapParserProject%" (
    echo [%date% %time%] FEHLER: MapParser.Core wurde nicht gefunden: "%TrailEditorMapParserProject%"
    echo [%date% %time%] Setze TrailEditorDependencyRoot oder TrailEditorMapParserProject. Details stehen in README.md.
    goto :configurationFailed
)
if not exist "%TrailEditorAivDecoderSourceRoot%\Models\SaveData.cs" (
    echo [%date% %time%] FEHLER: SHCDESE.AIVDecoder-Quellen wurden nicht gefunden: "%TrailEditorAivDecoderSourceRoot%"
    echo [%date% %time%] Setze TrailEditorDependencyRoot oder TrailEditorAivDecoderSourceRoot. Details stehen in README.md.
    goto :configurationFailed
)
if not exist "%TrailEditorAivDecoderSourceRoot%\AIVDecoder.cs" goto :missingAivSources
if not exist "%TrailEditorAivDecoderSourceRoot%\AIVEncoder.cs" goto :missingAivSources
if not exist "%TrailEditorAicDecoderSourceRoot%\InternalAIC.cs" goto :missingAicSources
if not exist "%TrailEditorAicDecoderSourceRoot%\PublicAIC.cs" goto :missingAicSources
goto :dependenciesFound

:missingAivSources
    echo [%date% %time%] FEHLER: SHCDESE.AIVDecoder-Quellen sind unvollstaendig: "%TrailEditorAivDecoderSourceRoot%"
    echo [%date% %time%] Setze TrailEditorDependencyRoot oder TrailEditorAivDecoderSourceRoot. Details stehen in README.md.
    goto :configurationFailed

:missingAicSources
    echo [%date% %time%] FEHLER: SHCDESE.AICDecoder-Quellen sind unvollstaendig: "%TrailEditorAicDecoderSourceRoot%"
    echo [%date% %time%] Setze TrailEditorDependencyRoot oder TrailEditorAicDecoderSourceRoot. Details stehen in README.md.
    goto :configurationFailed

:dependenciesFound
if not exist "%PROJECT_DIR%sources\Trail_Mission_1.trail" (
    echo [%date% %time%] FEHLER: Die Testdatei fehlt: "%PROJECT_DIR%sources\Trail_Mission_1.trail"
    goto :configurationFailed
)
if not exist "%PROJECT_DIR%sample\Trail_Mission_2.trail" (
    echo [%date% %time%] FEHLER: Die Custom-Lord-Testdatei fehlt: "%PROJECT_DIR%sample\Trail_Mission_2.trail"
    goto :configurationFailed
)

pushd "%PROJECT_DIR%" || exit /b 1

echo [%date% %time%] Baue TrailEditor im Release-Modus ...
echo [%date% %time%] Compile projects
dotnet build "%SOLUTION%" -c Release
if errorlevel 1 goto :failed

echo [%date% %time%] Fuehre TrailEditor-Tests aus ...
echo [%date% %time%] Run tests
dotnet run --project "%PROJECT_DIR%TrailEditor.Tests\TrailEditor.Tests.csproj" -c Release --no-build
if not "%ERRORLEVEL%"=="0" goto :failed

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

:configurationFailed
exit /b 1

@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "CLI_PROJECT=%PROJECT_DIR%TrailEditor.Cli\TrailEditor.Cli.csproj"
set "DIST_DIR=%PROJECT_DIR%dist\win-x64"
set "TrailEditorNoPause=1"

call "%PROJECT_DIR%build.bat"
if errorlevel 1 goto :failed

echo [%date% %time%] Erzeuge selbstenthaltenden Windows-x64-Release ...
dotnet publish "%CLI_PROJECT%" -c Release -r win-x64 --self-contained true -o "%DIST_DIR%" -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if errorlevel 1 goto :failed

copy /y "%PROJECT_DIR%unpack-all-trails.bat" "%DIST_DIR%\unpack-all-trails.bat" >nul || goto :failed
copy /y "%PROJECT_DIR%repack-all-trails.bat" "%DIST_DIR%\repack-all-trails.bat" >nul || goto :failed
copy /y "%PROJECT_DIR%packaging\win-x64\README.md" "%DIST_DIR%\README.md" >nul || goto :failed

if not exist "%DIST_DIR%\sources\" mkdir "%DIST_DIR%\sources" || goto :failed
if not exist "%DIST_DIR%\unpacked\" mkdir "%DIST_DIR%\unpacked" || goto :failed
if not exist "%DIST_DIR%\repacked\" mkdir "%DIST_DIR%\repacked" || goto :failed

if not exist "%DIST_DIR%\TrailEditor.exe" (
    echo [%date% %time%] FEHLER: Die erwartete portable EXE fehlt: "%DIST_DIR%\TrailEditor.exe"
    exit /b 1
)

echo [%date% %time%] Portabler Release erfolgreich erzeugt: "%DIST_DIR%"
exit /b 0

:failed
set "PUBLISH_EXIT=%errorlevel%"
if "%PUBLISH_EXIT%"=="0" set "PUBLISH_EXIT=1"
echo [%date% %time%] FEHLER: Der Windows-x64-Release konnte nicht erzeugt werden ^(Exitcode %PUBLISH_EXIT%^).
exit /b %PUBLISH_EXIT%

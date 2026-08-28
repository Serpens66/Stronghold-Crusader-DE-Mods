@echo off
setlocal EnableExtensions

rem ============================================================================
rem Steam Workshop upload configuration.
rem Leave ITEM_ID empty: the first successful upload stores it automatically.
rem Set ITEM_ID manually only to adopt an already existing Workshop item.
rem ============================================================================
set "UPLOAD_FOLDER=D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\SerpsMods"
set "ITEM_NAME=Serps Mod"
set "ITEM_ID="
set "VISIBILITY=Public"

set "APP_ID=3024040"
set "UPLOAD_TOOL=%~dp0.tools\pdengine-steamugc-tool-win-x64\pdengine.steamugc.tool.exe"
set "PREVIEW_IMAGE=%UPLOAD_FOLDER%\preview.png"

set "NO_PAUSE=0"
set "POWERSHELL_FLAGS="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/validate" set "POWERSHELL_FLAGS=-Validate"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Steam\Upload-Workshop.ps1" ^
  -UploadFolder "%UPLOAD_FOLDER%" ^
  -ItemName "%ITEM_NAME%" ^
  -ConfiguredItemId "%ITEM_ID%" ^
  -Visibility "%VISIBILITY%" ^
  -AppId "%APP_ID%" ^
  -ToolPath "%UPLOAD_TOOL%" ^
  -ContentOnlyUpdate %POWERSHELL_FLAGS%
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  echo ========================================================================
  echo [FEHLER] Der Steam-Workshop-Upload ist fehlgeschlagen. Exit Code: %EXIT_CODE%
  echo Der genaue Fehler und der Logpfad stehen oberhalb.
  echo ========================================================================
)

if "%NO_PAUSE%"=="0" pause
exit /b %EXIT_CODE%

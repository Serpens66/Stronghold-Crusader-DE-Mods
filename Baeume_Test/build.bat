@echo off
setlocal

set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "SOURCE_DIR=%~dp0SH1Baeume"
set "TARGET_DIR=%GAME_DIR%\BepInEx\plugins\SH1Baeume"

if not exist "%SOURCE_DIR%\info.json" (
    echo ERROR: Source mod not found: "%SOURCE_DIR%"
    set "RESULT=1"
    goto :finish
)

if not exist "%GAME_DIR%\BepInEx\plugins\000shcdese\SHCDESE.dll" (
    echo ERROR: Script Extender installation not found below "%GAME_DIR%".
    set "RESULT=1"
    goto :finish
)

robocopy "%SOURCE_DIR%" "%TARGET_DIR%" /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
set "ROBOCOPY_RESULT=%ERRORLEVEL%"
if %ROBOCOPY_RESULT% GEQ 8 (
    echo ERROR: Installation failed with robocopy exit code %ROBOCOPY_RESULT%.
    set "RESULT=%ROBOCOPY_RESULT%"
    goto :finish
)

echo Installed SH1Baeume to "%TARGET_DIR%".
set "RESULT=0"

:finish
if /I not "%~1"=="/nopause" pause
exit /b %RESULT%

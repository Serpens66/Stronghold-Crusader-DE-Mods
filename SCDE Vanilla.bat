@echo off
setlocal EnableExtensions DisableDelayedExpansion

call :FindGame
if not defined GAME_DIR (
    echo Stronghold Crusader Definitive Edition wurde nicht gefunden.
    echo.
    echo Lege diese Datei in den Spielordner oder repariere den Steam-Pfad.
    pause
    exit /b 1
)

if exist "%GAME_DIR%\winhttp.dll" (
    if exist "%GAME_DIR%\winhttp.dll.mods-disabled" (
        echo Sowohl winhttp.dll als auch winhttp.dll.mods-disabled sind vorhanden.
        echo Aus Sicherheitsgruenden wurde nichts umbenannt.
        pause
        exit /b 1
    )
    ren "%GAME_DIR%\winhttp.dll" "winhttp.dll.mods-disabled" || goto :RenameFailed
)

if not exist "%GAME_DIR%\winhttp.dll.mods-disabled" (
    echo Weder winhttp.dll noch winhttp.dll.mods-disabled wurde in "%GAME_DIR%" gefunden.
    pause
    exit /b 1
)

start "" /D "%GAME_DIR%" "%GAME_DIR%\Stronghold Crusader Definitive Edition.exe"
exit /b 0

:FindGame
set "GAME_DIR="
for %%K in (
    "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3024040"
    "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3024040"
) do for /f "tokens=2,*" %%A in ('reg query %%K /v InstallLocation 2^>nul ^| find /i "InstallLocation"') do if exist "%%B\Stronghold Crusader Definitive Edition.exe" set "GAME_DIR=%%B"
if defined GAME_DIR exit /b

for %%K in (
    "HKCU\SOFTWARE\Valve\Steam"
    "HKLM\SOFTWARE\WOW6432Node\Valve\Steam"
) do for /f "tokens=2,*" %%A in ('reg query %%K /v SteamPath 2^>nul ^| find /i "SteamPath"') do if exist "%%B\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition.exe" set "GAME_DIR=%%B\steamapps\common\Stronghold Crusader Definitive Edition"
if defined GAME_DIR exit /b

if exist "%~dp0Stronghold Crusader Definitive Edition.exe" set "GAME_DIR=%~dp0"
if defined GAME_DIR if "%GAME_DIR:~-1%"=="\" set "GAME_DIR=%GAME_DIR:~0,-1%"
exit /b

:RenameFailed
echo winhttp.dll konnte nicht deaktiviert werden. Starte diese Datei als Administrator.
pause
exit /b 1

@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================================================
rem Serps Mods Steam pack configuration. Add a mod by adding the next number.
rem ==========================================================================
set "PACK_NAME=Serps Mods"
set "PACK_GUID=SerpsMods_Serp"
set "WORKSHOP_PACKAGER_PATH=AUTO"
set "PREVIEW_PATH=%~dp0SerpsModsHost\steam-preview.png"

set "STEAM_MOD_01=BugfixesAndQoL"
set "STEAM_MOD_02=BuildingCosts"
set "STEAM_MOD_03=BuildingLimit"
set "STEAM_MOD_04=ExtraFeatures"
set "STEAM_MOD_05=RandomEvents"
set "STEAM_MOD_06=SpawnCastle"
set "STEAM_MOD_07=StartConditions"
set "STEAM_MOD_08=UnitCosts"
set "STEAM_MOD_09=UnitLimit"

set "SERPS_STEAM_MODS="
for /L %%N in (1,1,99) do (
  set "INDEX=0%%N"
  set "INDEX=!INDEX:~-2!"
  for %%I in (!INDEX!) do if defined STEAM_MOD_%%I (
    if defined SERPS_STEAM_MODS (
      set "SERPS_STEAM_MODS=!SERPS_STEAM_MODS!|!STEAM_MOD_%%I!"
    ) else (
      set "SERPS_STEAM_MODS=!STEAM_MOD_%%I!"
    )
  )
)

if not defined SERPS_STEAM_MODS (
  echo.
  echo [FEHLER] Die Steam-Modliste ist leer.
  echo.
  pause
  exit /b 2
)

set "POWERSHELL_FLAGS="
for %%A in (%*) do (
  if /I "%%~A"=="/validate" set "POWERSHELL_FLAGS=!POWERSHELL_FLAGS! -Validate"
  if /I "%%~A"=="/nopause" set "POWERSHELL_FLAGS=!POWERSHELL_FLAGS! -NoPause"
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Steam\Create-SteamModPack.ps1" -PackName "%PACK_NAME%" -PackGuid "%PACK_GUID%" -WorkshopPackagerPath "%WORKSHOP_PACKAGER_PATH%" -PreviewPath "%PREVIEW_PATH%" !POWERSHELL_FLAGS!
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  echo ========================================================================
  echo [FEHLER] Serps Mods konnte nicht erstellt werden. Exit Code: %EXIT_CODE%
  echo Der genaue Fehler und der Logpfad stehen oberhalb.
  echo ========================================================================
)

set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
if "%NO_PAUSE%"=="0" pause
exit /b %EXIT_CODE%

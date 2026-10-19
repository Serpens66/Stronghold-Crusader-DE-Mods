@echo off
setlocal EnableExtensions
set "TARGET=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\PerfFixTest_Serp"
if not exist "%TARGET%\PerfFixTest.dll" (
  echo PerfFixTest is not installed.
  exit /b 0
)
rmdir /S /Q "%TARGET%"
if errorlevel 1 exit /b 1
echo PerfFixTest removed from the game.
exit /b 0

@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "EXE=%ROOT%TrailEditor.exe"
set "SOURCES=%ROOT%sources"
set "OUTPUT=%ROOT%unpacked"

if not exist "%EXE%" (
  set "EXE=%ROOT%dist\win-x64\TrailEditor.exe"
)

if not exist "%EXE%" (
  echo FEHLER: Der portable TrailEditor wurde nicht gefunden.
  echo Erwartet wurde "%ROOT%TrailEditor.exe" oder "%ROOT%dist\win-x64\TrailEditor.exe".
  echo Erzeuge ihn als Entwickler mit publish-win-x64.bat oder kopiere das vollstaendige Release-Paket.
  goto :failed
)

if not exist "%SOURCES%\" (
  echo FEHLER: Der Eingabeordner fehlt: "%SOURCES%"
  goto :failed
)

dir /s /b /a-d "%SOURCES%\*.trail" >nul 2>&1
if errorlevel 1 (
  echo FEHLER: In "%SOURCES%" wurden keine .trail-Dateien gefunden.
  goto :failed
)

"%EXE%" export-all "%SOURCES%" "%OUTPUT%"
set "RESULT=%errorlevel%"
if not "%RESULT%"=="0" (
  echo FEHLER: Das Entpacken ist fehlgeschlagen ^(Exitcode %RESULT%^).
  echo Beachte: Bereits vorhandene Ausgabeordner werden absichtlich nicht ueberschrieben.
  goto :failedWithCode
)

echo Fertig. Die entpackten Dateien liegen in "%OUTPUT%".
exit /b 0

:failed
set "RESULT=1"

:failedWithCode
echo Druecke eine beliebige Taste, um das Fenster zu schliessen.
pause >nul
exit /b %RESULT%

@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "EXE=%~dp0SaveExtractor.exe"
set "RESULT=1"

if not exist "%EXE%" (
  echo FEHLER: SaveExtractor.exe fehlt neben diesem Starter.
  goto :done
)
if "%~1"=="" (
  echo Ziehe eine .sav-Datei auf SaveExtractor-Start.cmd.
  goto :done
)
if not "%~2"=="" (
  echo FEHLER: Bitte nur eine .sav-Datei gleichzeitig ablegen.
  goto :done
)

set "SOURCE=%~f1"
set "OUTPUT=%~dp1%~n1.extracted"
if not exist "%SOURCE%" (
  echo FEHLER: Die Save-Datei wurde nicht gefunden: "%SOURCE%"
  goto :done
)
if exist "%OUTPUT%" (
  echo FEHLER: Der Ausgabeordner existiert bereits: "%OUTPUT%"
  goto :done
)

"%EXE%" extract "%SOURCE%" "%OUTPUT%"
set "RESULT=%errorlevel%"
if not "%RESULT%"=="0" goto :done
echo.
echo Fertig. Die Dateien liegen in "%OUTPUT%".

:done
if not "%RESULT%"=="0" echo Extraktion fehlgeschlagen ^(Exitcode %RESULT%^).
if not "%SaveExtractorNoPause%"=="1" pause
exit /b %RESULT%

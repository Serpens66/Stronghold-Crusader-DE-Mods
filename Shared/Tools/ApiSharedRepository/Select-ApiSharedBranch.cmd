@echo off
rem Called from a consumer build.bat with the driver arguments. Sets API_SHARED_DIR and
rem SHCDE_API_SHARED_MAIN_SELECTION in the caller's environment for /apishared:main|dev.
set "APISHARED_SELECTION_FILE=%TEMP%\shcde-apishared-%RANDOM%%RANDOM%%RANDOM%.cmd"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Select-ApiSharedBranch.ps1" -Workspace "%~dp0..\..\.." -OutFile "%APISHARED_SELECTION_FILE%" %*
if errorlevel 1 (
  if exist "%APISHARED_SELECTION_FILE%" del /Q "%APISHARED_SELECTION_FILE%" >nul 2>&1
  set "APISHARED_SELECTION_FILE="
  exit /b 1
)
if not exist "%APISHARED_SELECTION_FILE%" (
  echo APIShared branch selection produced no result.
  set "APISHARED_SELECTION_FILE="
  exit /b 1
)
call "%APISHARED_SELECTION_FILE%"
del /Q "%APISHARED_SELECTION_FILE%" >nul 2>&1
set "APISHARED_SELECTION_FILE="
exit /b 0

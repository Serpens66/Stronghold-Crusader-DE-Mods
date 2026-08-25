@echo off
setlocal

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\DocumentationImages\CreateDocuImages.ps1"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  echo Dokumentationsbilder konnten nicht erstellt werden. Exit Code: %EXIT_CODE%
  echo %* | findstr /I /C:"/nopause" >nul || pause
  exit /b %EXIT_CODE%
)

echo.
echo Alle Dokumentationsbilder wurden erstellt.
echo %* | findstr /I /C:"/nopause" >nul || pause
exit /b 0

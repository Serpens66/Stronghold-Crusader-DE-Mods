@echo off
call "%~dp0..\Shared\Release\Invoke-Release.bat" APIShared /called %*
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo Release failed. Exit code: %EXIT_CODE%
  echo %* | findstr /I /C:"/nopause" >nul || pause
)
exit /b %EXIT_CODE%

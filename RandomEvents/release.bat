@echo off
call "%~dp0..\Shared\Release\Invoke-Release.bat" RandomEvents /called %*
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo Release fehlgeschlagen. Exit Code: %EXIT_CODE%
  echo %* | findstr /I /C:"/nopause" >nul || pause
)
exit /b %EXIT_CODE%

@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "GIT_BASH=C:\Program Files\Git\bin\bash.exe"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

if not exist "%GIT_BASH%" goto failed
"%GIT_BASH%" "%PROJECT_DIR%tests\windows-launcher-tests.sh" "%PROJECT_DIR%."
set "TEST_EXIT_CODE=%ERRORLEVEL%"
if not "%TEST_EXIT_CODE%"=="0" goto failed

echo.
echo Alle launcher-only Tests waren erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo.
echo Launcher-only Testlauf fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1

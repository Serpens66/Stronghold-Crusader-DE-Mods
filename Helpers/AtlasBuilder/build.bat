@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START AtlasBuilder
cd /d "%~dp0"
if errorlevel 1 goto :build_driver_directory_failed
rem The outer driver owns the pause, including failures before compilation.
call :build_driver_main %* /nopause
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:build_driver_directory_failed
echo ERROR: Cannot enter the build directory "%~dp0".
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b 1

:build_driver_main
setlocal
cd /d "%~dp0"

set "ATLAS_PYTHON=%ATLAS_BUILDER_PYTHON%"
if not defined ATLAS_PYTHON set "ATLAS_PYTHON=python"
echo [%date% %time%] Check Python and packaging dependencies
"%ATLAS_PYTHON%" --version >nul 2>&1
if errorlevel 1 (
  echo Python was not found. Install Python or set ATLAS_BUILDER_PYTHON to python.exe.
  goto :failed
)
if exist "%~dp0.deps\" set "PYTHONPATH=%~dp0.deps;%PYTHONPATH%"
"%ATLAS_PYTHON%" -c "import PIL, UnityPy, PyInstaller"
if errorlevel 1 (
  echo Packaging dependencies are missing or incompatible with this Python version.
  echo Install requirements.txt for the selected Python interpreter.
  goto :failed
)

echo Building AtlasBuilder portable Windows package...
"%ATLAS_PYTHON%" fetch_texconv.py
if errorlevel 1 goto :failed

"%ATLAS_PYTHON%" -m PyInstaller --noconfirm --clean AtlasBuilder.spec
if errorlevel 1 goto :failed

"%ATLAS_PYTHON%" package_release.py
if errorlevel 1 goto :failed

echo Build completed: dist\AtlasBuilder-portable-win64.zip
exit /b 0

:failed
echo Build failed.
exit /b 1

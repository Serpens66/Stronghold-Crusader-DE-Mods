@echo off
setlocal
cd /d "%~dp0"

set "ATLAS_PYTHON=%ATLAS_BUILDER_PYTHON%"
if not defined ATLAS_PYTHON set "ATLAS_PYTHON=python"

echo Building AtlasBuilder portable Windows package...
"%ATLAS_PYTHON%" -m PyInstaller --noconfirm --clean AtlasBuilder.spec
if errorlevel 1 goto :failed

"%ATLAS_PYTHON%" package_release.py
if errorlevel 1 goto :failed

echo Build completed: dist\AtlasBuilder-portable-win64.zip
if /i not "%~1"=="/nopause" pause
exit /b 0

:failed
echo Build failed.
if /i not "%~1"=="/nopause" pause
exit /b 1

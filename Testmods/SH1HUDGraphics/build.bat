@echo off
setlocal EnableExtensions
set "HUD_NOPAUSE=0"
set "HUD_RESULT=1"
for %%A in (%*) do (
    if /I "%%~A"=="/nopause" set "HUD_NOPAUSE=1"
    if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" goto :invalid_argument
)
rem This asset-only driver always builds working sources without installing.
rem BC7 conversion uses official texconv in ..\.tools\texconv, or SHCDE_TEXCONV.
rem texconv and Python/Pillow are build tools; they are never shipped in the mod.
set "HUD_PYTHON=%SHCDE_PYTHON%"
if not defined HUD_PYTHON set "HUD_PYTHON=D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe"
if not exist "%HUD_PYTHON%" (
    echo ERROR: Set SHCDE_PYTHON to a Python 3.11+ executable with Pillow.
    goto :done
)
"%HUD_PYTHON%" -B -m unittest discover -s "%~dp0tests" -v
if errorlevel 1 goto :done
rem Package verification below checks JSON, text endings, all XAML operations and
rem atlas pixels directly. Runtime/SDK checks do not apply to this DLL-free mod.
"%HUD_PYTHON%" -B "%~dp0tools\hud_assets.py" build
set "HUD_RESULT=%ERRORLEVEL%"
goto :done

:invalid_argument
echo ERROR: Supported arguments: /nopause /noinstall. This driver never installs.

:done
if "%HUD_NOPAUSE%"=="0" pause
exit /b %HUD_RESULT%

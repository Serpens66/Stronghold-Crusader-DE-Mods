@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%TrailEditor.Cli\bin\Release\net10.0\TrailEditor.exe"

if not exist "%EXE%" (
  echo [%date% %time%] Building TrailEditor...
  dotnet build "%ROOT%TrailEditor.sln" -c Release
  if errorlevel 1 exit /b 1
)

"%EXE%" export-all "%ROOT%sources" "%ROOT%unpacked"
exit /b %errorlevel%

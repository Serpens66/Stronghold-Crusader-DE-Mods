@echo off
setlocal EnableExtensions
if /I not "%~1"=="/nopause" (
  echo Expected /nopause but received: %*
  exit /b 9
)
echo Fake build driver received /nopause.
exit /b 0

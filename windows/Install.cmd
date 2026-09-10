@echo off
setlocal
echo Installing Acapella for your Windows account...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 (
  echo Installation failed. Please copy the message above when reporting the problem.
  pause
  exit /b 1
)
echo Acapella is installed. You can open it from the Start menu.
pause

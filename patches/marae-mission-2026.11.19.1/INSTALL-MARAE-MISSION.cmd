@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Mission.ps1" -NoPause
set "missionInstallResult=%ERRORLEVEL%"
echo.
pause
exit /b %missionInstallResult%

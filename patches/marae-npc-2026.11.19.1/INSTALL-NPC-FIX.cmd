@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-NpcPatch.ps1" -NoPause
set "npcInstallResult=%ERRORLEVEL%"
echo.
pause
exit /b %npcInstallResult%

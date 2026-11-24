@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-MapHunt.ps1" -InstallCompatibility -NoPause
pause

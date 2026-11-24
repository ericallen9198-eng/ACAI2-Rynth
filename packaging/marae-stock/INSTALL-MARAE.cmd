@echo off
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File "%~dp0Support\Install-Marae.ps1"
if errorlevel 1 pause

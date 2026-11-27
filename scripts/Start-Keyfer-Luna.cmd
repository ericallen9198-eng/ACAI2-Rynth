@echo off
setlocal
if not exist "%~dp0Bridge\ACAI2.Bridge.exe" (
  echo Keep this launcher beside the Bridge folder from the update ZIP.
  pause
  exit /b 1
)
set "ACAI2_OPENAI_MODEL=gpt-6-luna"
cd /d "%~dp0Bridge"
ACAI2.Bridge.exe
pause

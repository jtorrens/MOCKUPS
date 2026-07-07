@echo off
setlocal

cd /d "%~dp0"
npm.cmd run desktop

if errorlevel 1 (
  echo.
  echo Desktop app failed to start. See the error output above.
  pause
)

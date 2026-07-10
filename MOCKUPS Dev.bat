@echo off
setlocal

cd /d "%~dp0"

if not exist node_modules (
  call npm.cmd install
  if errorlevel 1 (
    pause
    exit /b 1
  )
)

call npm.cmd run desktop

if errorlevel 1 pause

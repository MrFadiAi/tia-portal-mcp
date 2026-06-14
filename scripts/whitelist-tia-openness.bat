@echo off
REM Launcher for tia-openness-whitelist.ps1 — self-elevates (UAC) so the script can edit HKLM.
setlocal
set "SCRIPT=%~dp0tia-openness-whitelist.ps1"

REM If not admin, relaunch this .bat elevated.
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

if not exist "%SCRIPT%" (
    echo ERROR: script not found: %SCRIPT%
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
echo.
pause

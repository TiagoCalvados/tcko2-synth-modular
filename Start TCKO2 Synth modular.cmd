@echo off
cd /d "%~dp0"
if not exist "%~dp0bin\TCKO2Synth.exe" (
    echo Build the synth first: powershell -ExecutionPolicy Bypass -File build.ps1
    pause
    exit /b 1
)
start "" "%~dp0bin\TCKO2Synth.exe"

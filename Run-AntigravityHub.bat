@echo off
title Antigravity Multi-Account Hub
cd /d "%~dp0"

if not exist "%~dp0release\AntigravityHub.exe" (
    echo [ERROR] Prebuilt executable not found in release\AntigravityHub.exe
    echo Please run build.bat first to compile the project.
    pause
    exit /b 1
)

start "" "%~dp0release\AntigravityHub.exe"
exit /b 0

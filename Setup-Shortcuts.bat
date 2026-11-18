@echo off
title Setup Antigravity Multi-Account Hub
cd /d "%~dp0"

echo ============================================================================
echo   Antigravity Multi-Account Hub - Initial Setup
echo   Generating 3 Isolated Profiles, Custom Badged Icons ^& AUMID Shortcuts...
echo ============================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\generate-shortcuts.ps1"
if errorlevel 1 (
    echo.
    echo [ERROR] Shortcut generation encountered an issue.
    pause
    exit /b 1
)

echo.
echo ============================================================================
echo  SETUP COMPLETED SUCCESSFULLY!
echo.
echo  3 Independent desktop shortcuts created for Account 1, 2, and 3.
echo  You can now start the Hub dashboard by double-clicking 'Run-AntigravityHub.bat'.
echo ============================================================================
echo.
pause
exit /b 0

@echo off
title Setup Antigravity Multi-Account Hub
cd /d "%~dp0"

echo ============================================================================
echo   Antigravity Multi-Account Hub - Initial Setup
echo   Generating 3 Isolated Profiles, Custom Badged Icons ^& AUMID Shortcuts...
echo ============================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\generate-shortcuts.ps1" -CopyToDesktop
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
echo  Shortcuts created in Start Menu ("Antigravity Hub" - Pin to Start ready) and Desktop.
echo  You can now start the Hub from Start Menu or 'Run-AntigravityHub.bat'.
echo ============================================================================
echo.
pause
exit /b 0

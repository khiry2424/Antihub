@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"

if not defined USERPROFILE (
    set "USERPROFILE=%SystemDrive%\Users\%USERNAME%"
)
if not defined NUGET_PACKAGES (
    if exist "%USERPROFILE%\.nuget\packages" (
        set "NUGET_PACKAGES=%USERPROFILE%\.nuget\packages"
    ) else if exist "%SystemDrive%\Users\%USERNAME%\.nuget\packages" (
        set "NUGET_PACKAGES=%SystemDrive%\Users\%USERNAME%\.nuget\packages"
    )
)

echo ============================================================================
echo  ANTIGRAVITY MULTI-ACCOUNT HUB - AUTOMATED BUILD ^& PACKAGING PIPELINE
echo ============================================================================

echo.
echo [1/4] Generating 3 Isolated Profiles, Custom Icons ^& AUMID Shortcuts...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\generate-shortcuts.ps1"
if errorlevel 1 (
    echo [ERROR] Shortcut generation failed.
    exit /b 1
)

echo.
echo [2/4] Building React + TypeScript + Tailwind CSS Dashboard UI...
if not exist "%~dp0node_modules" (
    call npm install
)
call npm run build
if errorlevel 1 (
    echo [ERROR] Frontend build failed.
    exit /b 1
)

echo.
echo [3/4] Compiling Native Windows Desktop Host (AntigravityHub.exe)...
if /I "%~1"=="self-contained" (
    dotnet publish "%~dp0src-native\AntigravityHub.csproj" -c Release -r win-x64 --self-contained true -o "%~dp0release" --nologo
) else (
    dotnet publish "%~dp0src-native\AntigravityHub.csproj" -c Release -o "%~dp0release" --source "%NUGET_PACKAGES%" --nologo
)
if errorlevel 1 (
    echo [ERROR] Native Windows executable build failed.
    exit /b 1
)

echo.
echo [4/4] Staging Frontend Assets into Release Bundle...
if not exist "%~dp0release\dist" mkdir "%~dp0release\dist"
xcopy /E /I /Y "%~dp0dist\*" "%~dp0release\dist\" >nul

if not exist "%~dp0release\scripts" mkdir "%~dp0release\scripts"
xcopy /E /I /Y "%~dp0scripts\*" "%~dp0release\scripts\" >nul

if not exist "%~dp0release\assets" mkdir "%~dp0release\assets"
xcopy /E /I /Y "%~dp0assets\*" "%~dp0release\assets\" >nul

echo.
echo ============================================================================
echo  BUILD COMPLETE!
echo  Executable : %~dp0release\AntigravityHub.exe
echo  Shortcuts  : %APPDATA%\Antigravity-Profiles\Shortcuts\
echo  Shared Dir : %USERPROFILE%\.gemini\antigravity
echo ============================================================================
exit /b 0

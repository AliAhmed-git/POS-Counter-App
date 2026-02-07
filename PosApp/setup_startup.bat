@echo off
setlocal

:: Get the current directory (where the app is published)
set "APP_DIR=%~dp0"
set "EXE_NAME=PosApp.Desktop.exe"
set "FULL_PATH=%APP_DIR%%EXE_NAME%"

if not exist "%FULL_PATH%" (
    echo [ERROR] %EXE_NAME% not found in %APP_DIR%
    echo Please build/publish the app before running this script.
    pause
    exit /b 1
)

echo Setting up %EXE_NAME% for Windows Startup...
echo Path: %FULL_PATH%

:: Add to HKCU\Software\Microsoft\Windows\CurrentVersion\Run
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "AsifSuperStorePOS" /t REG_SZ /d "\"%FULL_PATH%\"" /f

if %errorlevel% equ 0 (
    echo [SUCCESS] Application will now start automatically with Windows.
) else (
    echo [FAILED] Failed to set registry key. Try running as administrator if it failed.
)

pause

@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

echo ==========================================================
echo  LockScreenWallpaper installer
echo ==========================================================
echo.
echo Step 1/3: Creating your local code-signing certificate...
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%signing\create-certificate.ps1"
if errorlevel 1 goto :error

echo.
echo Step 2/3: Building and signing the app...
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%signing\publish-and-sign.ps1"
if errorlevel 1 goto :error

echo.
echo Step 3/3: Trusting the certificate and installing to Program Files.
echo A User Account Control prompt will appear -- click Yes to continue.
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%signing\run-elevated.ps1" -ScriptPath "%SCRIPT_DIR%signing\install-elevated.ps1"
if errorlevel 1 goto :error

echo.
echo ==========================================================
echo  Installed. Launching LockScreenWallpaper...
echo ==========================================================
start "" "%ProgramFiles%\LockScreenWallpaper\LockScreenWallpaper.exe"
echo Look for its icon in the system tray.
pause
exit /b 0

:error
echo.
echo Something went wrong -- see the messages above for details.
pause
exit /b 1

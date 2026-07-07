@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

echo ==========================================================
echo  LockScreenWallpaper uninstaller
echo ==========================================================
echo.
echo A User Account Control prompt will appear -- click Yes to continue.
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%signing\run-elevated.ps1" -ScriptPath "%SCRIPT_DIR%signing\uninstall-elevated.ps1"
if errorlevel 1 goto :error

echo.
echo Done. LockScreenWallpaper has been removed.
pause
exit /b 0

:error
echo.
echo Something went wrong -- see the messages above for details.
pause
exit /b 1

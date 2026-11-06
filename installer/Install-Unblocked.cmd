@echo off
rem One-click helper: remove the "downloaded from internet" mark (Mark-of-the-Web)
rem that makes Windows SmartScreen block the installer, then start the installer.
setlocal
cd /d "%~dp0"
echo [1/2] Removing internet mark from exe files ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Filter *.exe -File | Unblock-File -ErrorAction SilentlyContinue"
echo [2/2] Starting installer ...
start "" "%~dp0ShangtiPowerSetup.exe"
endlocal

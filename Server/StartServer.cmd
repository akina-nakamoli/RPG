@echo off
title RPG Backend - Manual Start
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0StartServer.ps1"
echo.
echo This controller has finished. Any error message is shown above.
pause

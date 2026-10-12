@echo off
title RPG Backend - Manual Stop
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0StopServer.ps1"
echo.
pause

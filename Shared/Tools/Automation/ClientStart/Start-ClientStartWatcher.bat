@echo off
rem Start on the multiplayer test client before paired tests; close the window to stop it.
rem The watcher only starts SHCDE through Steam when the test host requests it.
title SHCDE client start watcher
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Watch-ClientStartRequests.ps1"
pause

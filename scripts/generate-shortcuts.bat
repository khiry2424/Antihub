@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0generate-shortcuts.ps1" %*

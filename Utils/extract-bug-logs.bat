@echo off
REM Bug Report Log Extraction Tool - Windows Batch Launcher
REM This script provides an easy way to run the extraction tool

setlocal enabledelayedexpansion

REM Check if Python is available
python --version >nul 2>&1
if %errorlevel% equ 0 (
    set PYTHON_AVAILABLE=1
) else (
    set PYTHON_AVAILABLE=0
)

REM Check if PowerShell is available
powershell -Command "exit 0" >nul 2>&1
if %errorlevel% equ 0 (
    set POWERSHELL_AVAILABLE=1
) else (
    set POWERSHELL_AVAILABLE=0
)

REM Determine which version to use
if "%1"=="" goto :show_help

if !PYTHON_AVAILABLE! equ 1 (
    echo Using Python version...
    python "%~dp0extract-bug-logs.py" %*
) else if !POWERSHELL_AVAILABLE! equ 1 (
    echo Using PowerShell version...
    powershell -ExecutionPolicy Bypass -File "%~dp0extract-bug-logs.ps1" %*
) else (
    echo ERROR: Neither Python nor PowerShell is available!
    echo Please install Python 3.7+ or ensure PowerShell is enabled.
    exit /b 1
)

goto :eof

:show_help
echo.
echo Bug Report Log Extraction Tool
echo Usage: extract-bug-logs.bat [bug-type] [options]
echo.
echo Examples:
echo   extract-bug-logs.bat --list
echo   extract-bug-logs.bat test-generation-failure
echo   extract-bug-logs.bat general --no-compress
echo.
if !PYTHON_AVAILABLE! equ 1 (
    python "%~dp0extract-bug-logs.py" --list
) else if !POWERSHELL_AVAILABLE! equ 1 (
    powershell -ExecutionPolicy Bypass -File "%~dp0extract-bug-logs.ps1" -List
) else (
    echo Please install Python or PowerShell to see available bug types.
)

:eof

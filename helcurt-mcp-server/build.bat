@echo off
REM Build script for Helcurt MCP Server

echo ========================================
echo Building Helcurt MCP Server
echo ========================================
echo.

REM Check if Node.js is installed
where node >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Error: Node.js is not installed or not in PATH
    exit /b 1
)

REM Check if npm is installed
where npm >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Error: npm is not installed or not in PATH
    exit /b 1
)

echo Step 1: Installing dependencies...
call npm install
if %ERRORLEVEL% NEQ 0 (
    echo Error: Failed to install dependencies
    exit /b 1
)
echo.

echo Step 2: Building executable...
call npm run build
if %ERRORLEVEL% NEQ 0 (
    echo Error: Build failed
    exit /b 1
)
echo.

echo ========================================
echo Build Complete!
echo ========================================
echo.
echo The executable has been created: helcurt-server.exe
echo.
echo Next steps:
echo 1. Copy helcurt-server.exe to your target machine
echo 2. Copy helcurt-config.example.json and rename to helcurt-config.json
echo 3. Edit helcurt-config.json to add your monitored folders
echo 4. Configure VS Code to use this MCP server
echo.

pause

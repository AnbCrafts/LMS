@echo off
title EduPulse Services Launcher
echo ========================================================
echo         EduPulse LMS - Starting Background Services
echo ========================================================
echo.

set "ROOT_DIR=%~dp0"
set "RAG_DIR=%ROOT_DIR%WebApplication1\Rag_Service"
set "IIS_CONFIG=%ROOT_DIR%.vs\LMSPractice.slnx\config\applicationhost.config"

echo [1/3] Starting Redis In-Memory Cache (port 6379)...
where redis-server >nul 2>&1
if "%ERRORLEVEL%"=="0" (
    start "EduPulse Redis" /MIN redis-server
    echo Redis service started from PATH.
) else if exist "%USERPROFILE%\.redis\redis-server.exe" (
    start "EduPulse Redis" /MIN "%USERPROFILE%\.redis\redis-server.exe"
    echo Redis service started from %USERPROFILE%\.redis.
) else if exist "C:\Program Files\Redis\redis-server.exe" (
    start "EduPulse Redis" /MIN "C:\Program Files\Redis\redis-server.exe"
    echo Redis service started from Program Files.
) else (
    echo [INFO] Redis server executable not found. Running with Redis cache disabled (graceful fallback).
)

echo [2/3] Starting Python RAG AI Assistant Microservice (port 8000)...
cd /d "%RAG_DIR%"
start "EduPulse AI Microservice (Port 8000)" /MIN python api.py

echo [3/3] Checking IIS Express Web Application (port 44301)...
tasklist /FI "IMAGENAME eq iisexpress.exe" 2>NUL | find /I /N "iisexpress.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo IIS Express is already running.
) else (
    echo Starting IIS Express...
    if exist "C:\Program Files\IIS Express\iisexpress.exe" (
        start "EduPulse IIS Express" /MIN "C:\Program Files\IIS Express\iisexpress.exe" /config:"%IIS_CONFIG%" /site:"WebApplication1"
    ) else if exist "C:\Program Files (x86)\IIS Express\iisexpress.exe" (
        start "EduPulse IIS Express" /MIN "C:\Program Files (x86)\IIS Express\iisexpress.exe" /config:"%IIS_CONFIG%" /site:"WebApplication1"
    ) else (
        echo [ERROR] IIS Express not found!
    )
)

echo.
echo ========================================================
echo  EduPulse services initialized!
echo  - Web Application: https://localhost:44301/ (HTTP: http://localhost:57794/)
echo  - Python AI Service: http://127.0.0.1:8000/
echo  - Redis Cache: localhost:6379
echo ========================================================
echo.
timeout /t 3 >nul
start https://localhost:44301/

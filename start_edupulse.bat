@echo off
title EduPulse Services Launcher
echo ========================================================
echo         EduPulse LMS - Starting Background Services
echo ========================================================
echo.

echo [1/3] Starting Redis In-Memory Cache (port 6379)...
start "EduPulse Redis" /MIN "C:\Users\anubhawgupta\.redis\redis-server.exe" "C:\Users\anubhawgupta\.redis\redis.windows.conf"

echo [2/3] Starting Python RAG AI Assistant Microservice (port 8000)...
cd /d "D:\Anubhaw\Projects\Assignments\LMSPractice\WebApplication1\Rag_Service"
start "EduPulse AI Microservice (Port 8000)" /MIN python api.py

echo [3/3] Checking IIS Express Web Application (port 44301)...
tasklist /FI "IMAGENAME eq iisexpress.exe" 2>NUL | find /I /N "iisexpress.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo IIS Express is already running.
) else (
    echo Starting IIS Express...
    start "EduPulse IIS Express" /MIN "C:\Program Files\IIS Express\iisexpress.exe" /config:"D:\Anubhaw\Projects\Assignments\LMSPractice\.vs\LMSPractice.slnx\config\applicationhost.config" /site:"WebApplication1"
)

echo.
echo ========================================================
echo  All services are running!
echo  - Web Application: https://localhost:44301/
echo  - Python AI Service: http://127.0.0.1:8000/
echo  - Redis Cache: localhost:6379
echo ========================================================
echo.
timeout /t 3 >nul
start https://localhost:44301/

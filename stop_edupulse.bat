@echo off
title EduPulse Services Stopper
echo Stopping EduPulse Background Services...

taskkill /F /IM redis-server.exe 2>nul
taskkill /F /FI "WINDOWTITLE eq EduPulse AI Microservice*" 2>nul
taskkill /F /IM iisexpress.exe 2>nul

echo All EduPulse services stopped.
pause

@echo off
cd /d "%~dp0.."

echo ========================================
echo   SS14 Prototype Redactor
echo ========================================
echo.
echo Building project and extracting metadata...
dotnet build Content.Redactor/Content.Redactor.csproj -c Debug

if errorlevel 1 (
    echo.
    echo Build failed. Please fix errors and try again.
    pause
    exit /b 1
)

echo.
echo Starting Redactor editor at http://localhost:5555/
echo Press Ctrl+C to stop the server.
echo.

dotnet "%~dp0..\bin\Content.Redactor\Content.Redactor.dll" serve "%~dp0.."
pause

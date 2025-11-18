@echo off
REM Master build script for AffinityPluginLoader
REM Builds both .NET assemblies and native AffinityBootstrap.dll

setlocal enabledelayedexpansion

set CONFIGURATION=%1
if "%CONFIGURATION%"=="" set CONFIGURATION=Release

echo ========================================
echo Building AffinityPluginLoader
echo ========================================
echo.

REM Build .NET projects
echo [1/2] Building .NET projects...
dotnet build -c %CONFIGURATION%
if %errorlevel% neq 0 (
    echo Error: .NET build failed
    exit /b 1
)
echo.

REM Build AffinityBootstrap
echo [2/2] Building AffinityBootstrap...
cd AffinityBootstrap
call build.bat
set BOOTSTRAP_RESULT=%errorlevel%
cd ..

if %BOOTSTRAP_RESULT% neq 0 (
    echo Error: AffinityBootstrap build failed
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
exit /b 0

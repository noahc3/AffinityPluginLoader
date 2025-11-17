@echo off
REM AffinityBootstrap build script for Windows
REM Supports both MSVC (cl.exe) and MinGW (gcc)

setlocal

REM Create build directory
if not exist build mkdir build

REM Try MSVC first
where cl.exe >nul 2>&1
if %errorlevel% == 0 (
    echo Building AffinityBootstrap with MSVC...
    cl.exe /LD bootstrap.c ole32.lib oleaut32.lib mscoree.lib /Fe:build\AffinityBootstrap.dll /nologo
    if %errorlevel% == 0 (
        echo Build successful: build\AffinityBootstrap.dll
        del bootstrap.obj >nul 2>&1
        del build\AffinityBootstrap.exp >nul 2>&1
        del build\AffinityBootstrap.lib >nul 2>&1
        exit /b 0
    ) else (
        echo MSVC build failed
        exit /b 1
    )
)

REM Try MinGW
where gcc.exe >nul 2>&1
if %errorlevel% == 0 (
    echo Building AffinityBootstrap with MinGW...
    gcc -shared -o build\AffinityBootstrap.dll bootstrap.c -lole32 -loleaut32 -luuid -lmscoree
    if %errorlevel% == 0 (
        echo Build successful: build\AffinityBootstrap.dll
        exit /b 0
    ) else (
        echo MinGW build failed
        exit /b 1
    )
)

REM Try x86_64-w64-mingw32-gcc
where x86_64-w64-mingw32-gcc.exe >nul 2>&1
if %errorlevel% == 0 (
    echo Building AffinityBootstrap with x86_64-w64-mingw32-gcc...
    x86_64-w64-mingw32-gcc -shared -o build\AffinityBootstrap.dll bootstrap.c -lole32 -loleaut32 -luuid -lmscoree
    if %errorlevel% == 0 (
        echo Build successful: build\AffinityBootstrap.dll
        exit /b 0
    ) else (
        echo x86_64-w64-mingw32-gcc build failed
        exit /b 1
    )
)

echo Error: No suitable compiler found (cl.exe, gcc.exe, or x86_64-w64-mingw32-gcc)
exit /b 1

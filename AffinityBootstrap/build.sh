#!/bin/bash
# AffinityBootstrap build script for Linux (using MinGW cross-compiler)

set -e

cd "$(dirname "$0")"

# Create build directory
mkdir -p build

# Try different MinGW compiler names
if command -v x86_64-w64-mingw32-gcc &> /dev/null; then
    COMPILER="x86_64-w64-mingw32-gcc"
elif command -v x86_64-w64-mingw32-gcc-posix &> /dev/null; then
    COMPILER="x86_64-w64-mingw32-gcc-posix"
elif command -v x86_64-w64-mingw32-gcc-win32 &> /dev/null; then
    COMPILER="x86_64-w64-mingw32-gcc-win32"
else
    echo "Error: MinGW cross-compiler not found (x86_64-w64-mingw32-gcc)"
    echo "On Ubuntu/Debian, install with: sudo apt-get install mingw-w64"
    exit 1
fi

echo "Building AffinityBootstrap with $COMPILER..."
$COMPILER -shared -o build/AffinityBootstrap.dll bootstrap.c -lole32 -loleaut32 -luuid -lmscoree

if [ -f build/AffinityBootstrap.dll ]; then
    echo "Build successful: build/AffinityBootstrap.dll"
    exit 0
else
    echo "Build failed"
    exit 1
fi

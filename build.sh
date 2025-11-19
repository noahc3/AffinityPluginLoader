#!/bin/bash
# Master build script for AffinityPluginLoader
# Builds both .NET assemblies and native AffinityBootstrap.dll
set -e

CONFIGURATION="${1:-Release}"

ROOTDIR=$(dirname "$(readlink -f "$0")")

pushd "$ROOTDIR"

echo "========================================"
echo "Building AffinityPluginLoader"
echo "========================================"
echo

# Build .NET projects
echo "[1/3] Building .NET projects..."
dotnet build -c "$CONFIGURATION"
echo

# Build AffinityBootstrap
echo "[2/3] Building AffinityBootstrap..."
cd AffinityBootstrap
bash build.sh
cd ..

# Build d2d1.dll for Wine (x86_64-unix)
echo "[3/3] Building d2d1.dll (Wine native)..."
cd WineFix/lib/d2d1
if command -v winegcc &> /dev/null; then
    make TARGET=x86_64-unix
else
    echo "Warning: winegcc not found. Skipping d2d1.dll build."
    echo "Install wine-devel to build d2d1.dll for Wine."
fi
cd ../../..

echo
echo "========================================"
echo "Build completed successfully!"
echo "========================================"

popd
